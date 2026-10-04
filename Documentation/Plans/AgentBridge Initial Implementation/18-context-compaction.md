# 18 — Реализовать сжатие контекста с учётом версий

Статус: **Реализован, адресно проверен и принят координатором; локальный коммит только утверждённых 34 файлов разрешён**. Зависимости: **10, 14, 17** приняты; учтена композиция16. Исходный HEAD af72252c5d5638137738957c200492923a425da8, ветка master, исходные tree/index чистые. После локального коммита — **ОСТАНОВКА ПОСЛЕ БЛОКА14–18**; этапы19–25 не начинать, следующий блок ведёт только новый координатор.

## Цель

Уменьшать рабочий контекст, сохраняя каноническое состояние внешнего сервиса и архивную историю.

## Задачи

- [x] Вызывать `/v1/responses/compact` с полным необходимым сохраняемым контекстом.
- [x] Сохранять новое каноническое окно контекста, непрозрачные элементы и охваченный диапазон истории.
- [x] Активировать новый контекст только после успешного сохранения с проверкой актуальности версии.
- [x] Соблюдать настраиваемый порог токенов и максимальное число проходов; прекращать сжатие, если размер не уменьшается.
- [x] Сохранять предыдущий принятый контекст при ошибке и не подменять compact скрытой генерацией обычного текстового резюме.
- [x] Хранить исходную историю до настроенного срока истечения или явного удаления; мягкий порог байтов не запускает удаление архива.

## Проверка и завершение

Проверить непрозрачные результаты compact, повторное сжатие, отсутствие уменьшения, ошибку и потерю актуальности версии. Этап завершён, когда сжатие и хранение разделены, а неограниченный цикл сжатия невозможен.

Источник: [сжатие контекста](<../../Business logic/03-context-and-compaction.md>).

## Принятые решения и реализация

Пользователь через координатора согласовал: (1) сжимать только сохраняемую завершённую историю — active window + следующий непрерывный terminal prefix, включая0; (2) валидное opaque окно с неизвестной полной оценкой сохранить и вернуть UnknownBudget. Провайдеры/new input/InProgress tail входят в полный бюджет, но не сохраняются внутри compact. Generation guard17 остаётся отдельным обязательным вызовом; server estimator и usage fallback не добавлены.

ContextCompactor вызывает actual ContextBuilder один раз, фиксирует providers, использует IContextTokenCounter, IModelGateway, IDialogContextWriter и свежий TimeProvider UTC перед save. Original token не refresh; следующий token только после успешного save. Внешние ожидания вне write UoW. Failed второго прохода оставляет первое успешно принятое окно. Empty/broken candidate, incomplete/failed/canceled, stale/expired не активируются. NoReduction не сохраняет кандидат, MaxPasses ограничивает цикл. Полная история и expiry неизменны.

CodexLbModelGateway.CompactAsync использует actual HttpClientLibrary0.0.0.5, отдельный CompactTimeout, canonical POST `/v1/responses/compact`. CompactRequestWriter/CompactJsonReader проверяют actual compact shape: response.compact* и output array, status optional. Unknown/opaque output/envelope сохраняются; continuation null, compact id не становится previous_response_id. Tools и generation controls остаются в полном request, компактная проекция не обещает их применение.

Все обязательные source paths доступны. Codex-lb AGENTS и actual v1_requests.py/requests.py/models.py/api.py прочитаны перед анализом. HttpClientLibrary AGENTS/Clients контракт и FileVersion проверены. Existing IDialogContextWriter/DialogContextUnitOfWork прочитаны; EF-код, EFCoreLibrary0.0.5, другие соседние проекты и generated migrations не менялись. Нет необходимости переносить код Telegram/AquaByte или повторять DB integration.

## Проверки 2026-10-04

Все команды выполнялись с workdir `D:\Media\User\source\repos\agent-bridge`. До сборки проверены конкретные csproj, ancestor Directory.Build.props/targets/NuGet.config/locks и generated package imports. Project custom Exec/hooks не обнаружены, packages/проектные ссылки не менялись. Каркас решения в корне сохранён; slnx добавляет только ссылку на новый технический документ.

```powershell
dotnet restore .\tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -p:GeneratePackageOnBuild=false
dotnet build .\tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false
dotnet build .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false
dotnet test .\tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --filter 'FullyQualifiedName~ResponsesJsonTests|FullyQualifiedName~ResponsesSseTests' --logger 'trx;LogFileName=stage18-transport-final.trx' --results-directory .\artifacts\test-results\stage18
dotnet test .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --filter 'FullyQualifiedName~ContextCompactorTests|FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ContextBudgetGuardTests|FullyQualifiedName~ContextTokenCounterTests' --logger 'trx;LogFileName=stage18-core-final.trx' --results-directory .\artifacts\test-results\stage18
```

Test builds также собрали затронутые production ядро/CodexLb и actual HttpClientLibrary: **0 warnings / 0 errors**. Restore успешен. Core: первый запуск163/0/0; после дополнительных save/counter/terminal cases — **172 passed / 0 failed / 0 skipped**, из них42 ContextCompactor cases и130 regression16/17. Transport: первый запуск **160 passed / 1 failed / 0 skipped**; ошибочное ожидание fixture для HTTP400 исправлено с Rejected на принятый Validation, production mapper не менялся. Следующий запуск161/0/0; итог после stream/cancellation/I/O дополнений — **165 passed / 0 failed / 0 skipped**. Compact cases36 заменяют один прежний Unsupported case (чистый прирост35), остальные129 — JSON/SSE regression. Итого актуальных адресных проверок337/0/0; новых cases78, чистый прирост77. Первоначальные TRX сохранены отдельно от final.

## Ограничения

Статическая проверка: 34 файла — UTF-8 без BOM, LF, без U+FFFD/mojibake/четырёх question marks; main/delta requirements совпадают буквально, slnx валиден как XML. `git diff --check` финально exit0; первоначальная лишняя пустая строка в конце этого отчёта удалена. Git предупреждает об autocrlf при будущих операциях, текущие файлы LF; настройки Git не менялись. Индекс пуст, HEAD остался af72252c5d5638137738957c200492923a425da8.

- **Пропущено по указанию пользователя:** real codex-lb/OpenAI/upstream/accounts/tokens, local HTTP servers/hosting/TestServer/WebApplicationFactory/Telegram/application, pack/publish, external uploads и этапы19–25.
- DB/Docker/SQLite/PostgreSQL/native backup и integration не повторялись: persistence не менялся, изолированные fake writer проверки не доказывают атомарность реальной БД или EFCoreLibrary0.0.5 integration. SQLServer/MySQL не проверены.
- OpenSpec CLI отсутствует (`Get-Command openspec` не нашёл команду). CLI validation не выполнена, программы не устанавливались, change не архивирован. Статическая сверка main/delta не заменяет CLI.
- До приёмки Git add/commit/push не выполнялись. После приёмки разрешён только локальный коммит утверждённого manifest; собственный hash возвращается в отчёте после commit, не записывается заранее. Push запрещён. Полный manifest ниже включает исходные untracked; outputs/TRX не входят в него.

## Приёмка и передача новому координатору

2026-10-04 координатор принял production/DI/tests/docs, лично проверил final TRX core172/0/0 + transport165/0/0, manifest34, UTF-8/LF, совпадение main/delta, чистый ordinary diff check и пустой index. Разрешён локальный English Conventional Commit только 34 перечисленных ниже файлов. Код и тесты после приёмки не расширялись; выполнены только acceptance notes и однозначная формулировка guard в main/delta. OpenSpec CLI task остаётся невыполненным, change не архивирован.

**ОСТАНОВКА ПОСЛЕ БЛОКА14–18.** После этого локального коммита этапы19–25 не начинать. Следующий этап19 остаётся **Не начат / NotStarted**; продолжение блока — только с новым координатором и отдельным поручением.

| Принятый этап | Полный hash |
| --- | --- |
| 14 — JSON Responses | d8a673fff514a93d2d26d7b986e3a36e018e290c |
| 15 — SSE Responses | 1d99721c4c4689cb06f9d71be2ba02380cdd0458 |
| 16 — ContextBuilder | 085779a15c4126ac567a6d1307f497dcae8dc9c5 |
| 17 — Offline tokenizer/guard | af72252c5d5638137738957c200492923a425da8 |

Передача18: IModelGateway.CompactAsync через actual HttpClientLibrary и CompactTimeout; ContextCompactor + AddAgentBridgeCompaction + ContextCompactionResult/Status, existing IDialogContextWriter.SaveAsync. Решения пользователя: только сохраняемый terminal prefix; валидный opaque candidate сохранять с UnknownBudget. Полный generation guard отдельно, providers фиксированы, fresh UTC/token до save, failure второго прохода сохраняет первое окно. Проверки337/0/0, builds0warnings/errors; исходный fixture failure описан выше. Ограничения: fake HTTP/writer не live compatibility/DB atomicity; EFCoreLibrary0.0.5 integration не повторялась; CLI отсутствует, без archive; никаких server estimator, tools19, AgentRunner20 или settings21. [Точный API](<../../Technical documentation/18-context-compaction.md>).

## Полный manifest перед приёмкой

- `AGENTS.md` — изменён
- `Application/AGENTS.md` — изменён
- `Application/ContextBuilder.cs` — изменён
- `Configuration/AGENTS.md` — изменён
- `Documentation/Business logic/03-context-and-compaction.md` — изменён
- `Documentation/Plans/AgentBridge Initial Implementation/18-context-compaction.md` — изменён
- `Documentation/Plans/AgentBridge Initial Implementation/README.md` — изменён
- `Documentation/README.md` — изменён
- `Documentation/Technical documentation/03-http-and-codex-lb.md` — изменён
- `Documentation/Technical documentation/07-tokenizer-and-settings.md` — изменён
- `Documentation/Technical documentation/14-responses-json-adapter.md` — изменён
- `Documentation/Technical documentation/16-context-composition.md` — изменён
- `Documentation/Technical documentation/README.md` — изменён
- `README.md` — изменён
- `adapters/AgentBridge.CodexLb/AGENTS.md` — изменён
- `adapters/AgentBridge.CodexLb/Responses/AGENTS.md` — изменён
- `adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs` — изменён
- `agent-bridge.slnx` — изменён
- `openspec/specs/agent-runtime/context.md` — изменён
- `openspec/specs/agent-runtime/spec.md` — изменён
- `tests/AGENTS.md` — изменён
- `tests/AgentBridge.CodexLb.Tests/ResponsesJsonTests.cs` — изменён
- `Application/ContextCompactor.cs` — новый
- `Application/Models/ContextCompactionResult.cs` — новый
- `Application/Models/ContextCompactionStatus.cs` — новый
- `Configuration/AgentBridgeCompactionExtensions.cs` — новый
- `Documentation/Technical documentation/18-context-compaction.md` — новый
- `adapters/AgentBridge.CodexLb/Responses/CompactJsonReader.cs` — новый
- `adapters/AgentBridge.CodexLb/Responses/CompactRequestWriter.cs` — новый
- `openspec/changes/context-compaction/context.md` — новый
- `openspec/changes/context-compaction/proposal.md` — новый
- `openspec/changes/context-compaction/specs/agent-runtime/spec.md` — новый
- `openspec/changes/context-compaction/tasks.md` — новый
- `tests/AgentBridge.Tests/ContextCompactorTests.cs` — новый

Всего 34 файла; 12 новых. Только agent-bridge; секреты, generated, bin/obj/artifacts и чужие изменения в manifest не входят.

# Результаты этапа04

Дата: 2026-11-07. Этап04 — **in-progress**, 04.01 — **verification**, 04.02 — **not-started**. [Этап](main.md) · [04.01](04.01-connector-provenance.md) · [Главный план](../main.md).

## Согласованный объём

Поддержка ранее сохранённых диалогов обоих коннекторов отменена. Используется один новый формат отчётов, без reader старого JSON, ветвления v1/v2 и backfill. Прежние публичные сигнатуры и физическая схема БД сохраняются. Отдельный budget snapshot v1/v2 не меняется. Sub2Api compact остаётся Unsupported; T05 и отрицательное основание03.03.02 не изменились.

## Реализация04.01

ModelResponse и ModelRequest получили отдельное immutable ConnectorProvenance. IModelContextGateway обоих встроенных коннекторов создаёт локальный binding connector/dialog/owner/agent/endpoint/key без TurnId и без сохранения ключа. Это доверенные локальные metadata от случайного смешивания, не авторизация и не upstream identity.

ContextModelGuard проверяет каждый opaque источник отдельно: active compact даже без continuation, каждый непокрытый model output и residual input/tools с сохранением occurrences. Одинаковый model ID и model compatibility приложения не обходят guard. AgentRunner проверяет историю и новые provider/input до Begin, историю между шагами и маркирует только проверенный подготовленный запрос. Неизвестный новый opaque input отклоняется, обычный текст допускается. Standalone ContextCompactor тоже проверяет источники до маркировки. Custom gateway без новой capability сохраняет прежний путь.

Direct gateways отклоняют opaque запрос без родной метки до HTTP; Sub2Api делает это до factory и callbacks. Полученные lifecycle reports и сохранённый runner partial output получают provenance. Sub2Api continuation остаётся null; CodexLb continuation сохраняет свой отдельный контракт. CodexLb compact использует binding generation endpoint для следующей generation. Маркер не добавляется в wire body, canonical envelope/output или логи. Sub2Api использует существующий safe outcome log, runner — существующий итог операции.

ModelResponseRecord пишет единственный wrapper в EnvelopeJson: envelope — исходный JSON как строка либо null, connector/binding — отдельная пара либо оба null. Исходное содержимое envelope, включая whitespace/escaping, восстанавливается отдельно. OutputJson и ContinuationJson сохраняют прежние роли. Старый raw EnvelopeJson/null и неверная форма отклоняются явно. ResponseFormatVersion=1 остаётся физическим discriminator существующей схемы, не выбирает версии reader. Миграции и SQL не менялись.

## Проверка компиляции

После статической проверки csproj и отсутствия применимых Directory.Build.props/targets и NuGet.config в цепочке каталогов выполнена команда ниже для каждого из девяти проектов таблицы. Restore, pack и сборка соседних ProjectReference не запускались.

```powershell
dotnet build <project.csproj> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false
```

| project.csproj | Итог последнего compile-check |
| --- | --- |
| AgentBridge/agent-bridge.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.Sub2Api/AgentBridge.Sub2Api.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.Integration/AgentBridge.Integration.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.Tests/AgentBridge.Tests.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj | 0 warnings, 0 errors |

Первый compile-check persistence tests выявил nullable warning в новом assertion; после исправления повторная компиляция прошла без предупреждений. Тесты не запускались: явного поручения на запуск не было. Предыдущие passed counts этапа03 не подтверждают изменения04.01.

## Подготовленные тесты и ограничения

Добавлены сценарии compact без continuation, same-model foreign/unknown state, родное окно с чужим tail, duplicate residual occurrences, изменения binding и новый TurnId, plaintext/unknown input. Actual Sub2Api gateway/runner/standalone compactor проверяются на отказ до factory/Begin/save и передачу родного synthetic opaque input в JSON/SSE без marker в wire. Actual CodexLb gateway проверяется для generation/compact. Persistence cases покрывают все lifecycle, отдельное provenance, точное содержимое envelope и отказ старого/повреждённого wrapper. Существующие protocol/compaction fixtures адаптированы к явному происхождению.

Эти сценарии только добавлены и скомпилированы, их выполнение не подтверждено. Реальный persistence restart, несколько turns с business tools после пересоздания DI и recovery относятся к04.02 и не начинались. Нет новой поставки, проверки frozen consumer против fresh closure или live OAuth/deployment. Sub2Api/HttpClientLibrary/EFCoreLibrary, baseline kit, frozen DLL, исторические01–02, evidence03 и main spec не редактировались. Git, проектные scripts, OpenSpec CLI, host, live HTTP/OAuth, БД/SQL, Docker и deployment не выполнялись.

Большой AgentRunner получил только integration calls; самостоятельная новая координация вынесена в AgentConnectorContext. Большой ResponsesJsonTests получил только настройку marker в существующих helpers; новые самостоятельные проверки находятся в CodexLbProvenanceTests. Остальные новые/изменённые C# файлы меньше20000 байт.

Статическая проверка охватила50 файлов и261 относительную Markdown-ссылку: отсутствующих targets, ошибок strict UTF-8, U+FFFD, четырёх question marks и проверенных mojibake-последовательностей нет. XML solution корректен, новый results04 включён в навигацию и solution. Hashes16 защищённых исторических файлов/evidence/завершённого03.03.01/main spec совпали с прежним snapshot. Исходные LF и CRLF AgentRunner/solution сохранены. Frozen kit не открывался и не пересобирался; совпадение его hashes в этом проходе не проверялось.

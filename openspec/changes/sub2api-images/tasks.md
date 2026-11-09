# Реализация и проверка

- [x] Прочитать инструкции, действующие specs/changes, результат usage и фактические server/client контракты.
- [x] Подготовить change до изменения поведения.
- [x] Расширить existing auxiliary generation/edit transport, безопасные результаты и локальную валидацию.
- [x] Добавить и выполнить isolated actual transport/DI tests, включая ошибки, cancellation/deadline/cleanup и no retry/secrets.
- [x] Выполнить локальный restore и compile-check конкретных проектов.
- [x] Синхронизировать main spec, контекст, документацию, пример и ближайшие AGENTS.
- [ ] Выполнить strict OpenSpec CLI validation после отдельного разрешения точной команды.
- [ ] Обновить DLL штатным упаковщиком после отдельного разрешения точной команды и проверить binary consumer.
- [ ] Выполнить реальную приёмку generation/edit после отдельного разрешения.

## Изолированные результаты 2026-11-08

Перед сборкой проверены test csproj и все transitive project references, ancestor Directory.Build.props/targets, NuGet config и lock-файлы (для этой closure lock отсутствует). Custom executable targets/hooks не обнаружены; GeneratePackageOnBuild=false. Вывод всех проектов, включая read-only HttpClientLibrary/EFCoreLibrary, направлен внутрь AgentBridge artifacts. Соседние исходники не изменялись.

Выполнены команды из `D:/Media/User/source/repos/agent-bridge`:

```powershell
dotnet restore tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false --artifacts-path D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-images -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-images/bin/
dotnet build tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false --artifacts-path D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-images -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-images/bin/
dotnet test --project tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-images -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-images/bin/ --no-progress
```

Локальный restore: exit 0, без сетевого источника. Финальный build тестового проекта и production closure: 0 warnings/errors. Финальный MTP run: **374 passed, 0 failed, 0 skipped**; 134 новых Images cases и 240 прежних cases. Сохранены usage, selector isolation, JSON/SSE, provenance/compaction guards и legacy public/protected API ядра/CodexLb/Integration. Public optional DI test дополнен обоими image методами; app override и lazy/scoped semantics сохранены.

Новые cases проходят actual HttpClientLibrary: exact JSON generation и multipart edits, копирование mutable коллекции/bytes до await, fresh content, все параметры, b64/url/mixed results, fragmented whitespace heartbeat, HTTP 200 с error, malformed/empty/missing payload/duplicate fields/invalid UTF-8, HTTP 302/400/401/403/404/413/429/5xx, per-request credentials и no fallback/retry. Send/success-body/error-body cancellation/deadline, caller priority, borrowed HttpClient timeout, primary/cancellation/standalone cleanup failures и request/response content disposal проверены локальными streams. Safe logs/errors не содержат synthetic private prompt/bytes/URL/key; Completed появляется после проверки и cleanup. Отказ outcome logger не меняет success/rejection и число отправок.

Обновлённый Consumer/Sub2ApiAuxiliaryUsage.cs source-linked и скомпилирован, его методы не исполнялись. Это не внешний binary consumer и не DLL-поставка. Проверены XML solution, существование новых solution items, UTF-8 без U+FFFD/четырёх вопросительных знаков/mojibake, размеры новых/изменённых C# (все менее 20 000 bytes), локальные AGENTS и размещение source по ответственности. Main requirements и delta сверены на точное совпадение после нормализации CRLF/LF; это **не OpenSpec CLI validation**.

Git, проектные скрипты/упаковщик, OpenSpec CLI, host, live HTTP/OAuth, БД/SQL/migrations, Docker и deployment не выполнялись. Telegram-бот, обычные входные фото AgentRunner и исторические завершённые планы не изменялись. Change не архивирован. DLL delivery, strict CLI и реальная доступность по платформе/группе/модели/upstream остаются открытыми отдельными проверками. Изображение не декодируется: gateway проверяет JSON-форму и наличие строкового payload, возвращая снимок приложению.

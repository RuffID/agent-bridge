# Безопасный режим полного контекста

- [x] Проследить оба server compact protocol и encrypted-content recovery; сообщить выводы в чате.
- [x] Согласовать сохранение блокировки compact без изменения sub2api.
- [x] Подготовить OpenSpec change до изменения поведения.
- [x] Закрыть обход через обычную generation до HTTP; сохранить canonical/provenance и CodexLb.
- [x] Добавить и выполнить isolated gateway/compactor/runner проверки сохранности истории, unknown budget, expiry и отказов.
- [x] Синхронизировать main requirements и актуальную документацию.
- [x] Выполнить адресные compile-check и статическую сверку документов/кодировки.
- [ ] Strict OpenSpec CLI validation после отдельного разрешения команды.

Compact transport/reader/активация и закрытие T05 не входят в выбранный объём. Live HTTP/OAuth, БД, Docker, deployment и упаковка не выполняются. Change не архивировать без required validation.

## Изолированные результаты 2026-11-09

До production правки новые boundary tests воспроизвели обход: 15 failed, 2 passed. Canonical trigger/окно попадали в generation; часть duplicate-type случаев возвращала результат без ожидаемого Unsupported. После запрета эти сценарии проходят. Два прежних positive-теста, отправлявших compact окно, переведены на полный prefix согласно новому контракту; canonical ordering/tools/reasoning/controls assertions сохранены.

Финальные независимые наборы:

| Набор | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| AgentBridge.Sub2Api.Tests, весь isolated проект, включая usage/images и 29 новых safety cases | 403 | 0 | 0 |
| AgentBridge.Tests, ядро, включая budget/ContextCompactor/save/expiry/tools | 569 | 0 | 0 |
| AgentBridge.CodexLb.Tests, прежний JSON/SSE/compact | 422 | 0 | 0 |
| Persistence Sub2Api.*, actual reader/UoW и serialized restart, включая 3 новых safety cases | 19 | 0 | 0 |
| Persistence DialogWritePortsTests, actual writer над repository doubles | 33 | 0 | 0 |

Всего 1446 уникальных cases, 32 новых; повторные прогоны не складываются. Проверены trigger в разных позициях и JSON/SSE intent, duplicate type, родное provenance, отсутствие factory/callback/HTTP, повторный force, fixed expiry, полный input и function pairs, HTTP200 response.failed, malformed/EOF, encrypted-content HTTP400 без client retry и serialized restart с неизменной историей. Core/CodexLb проверяют отдельные прежние save conflict/failure/activation и tool-boundary сценарии; они не являются sub2api compact integration. Actual writer не получает sub2api compact candidate: выбранный режим запрещает сам dispatch.

Build chain конкретных четырёх test csproj и transitive project references проверен до запуска: custom executable targets/hooks отсутствуют. Existing HttpClientLibrary Directory.Build.props влияет только на собственный HttpClientLibrary.Tests, который не собирался. Lock-файлы closure отсутствуют. Restore использовал только C:/Users/Spike/.nuget/packages с NuGetAudit=false. GeneratePackageOnBuild=false; ArtifactsPath и BaseOutputPath для всех transitive проектов перенаправлены внутрь AgentBridge/artifacts/sub2api-compact-safety (отдельные core/codexlb/persistence подпапки для независимых проверок). Build: 0 warnings/errors. Исходники соседних библиотек не менялись; generated outputs находятся внутри AgentBridge artifacts.

Из D:/Media/User/source/repos/agent-bridge для каждого test csproj выполнены dotnet restore с --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false --artifacts-path <его-каталог> -p:BaseOutputPath=<его-каталог>/bin/, затем dotnet build -c Debug --no-restore с теми же output flags. Тесты: dotnet test --project <test.csproj> -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:ArtifactsPath=<его-каталог> -p:BaseOutputPath=<его-каталог>/bin/ --no-progress. Persistence запускался только двумя точными фильтрами: --filter-class 'AgentBridge.Persistence.EfCore.Tests.Sub2Api.*' и --filter-class '*DialogWritePortsTests', оба с --filter-not-trait 'Dependency=Database'. Testcontainers/реальные providers не исполнялись; migrations libraries только скомпилированы как references, tooling/migrations не запускались.

T05 не закрыт: блокируется клиентский запрос с opaque заменой covered prefix, а не исправляется server recovery. Fixture не доказывает пригодность deployed pool. Compact transport/reader, повторное успешное сжатие и sub2api save-before-activation намеренно не реализованы. Strict OpenSpec CLI, упаковка, host, HTTP/OAuth, реальные БД, Docker, deployment и Git не выполнялись. Исторические планы и Telegram не изменены.

После последней правки positive-теста выполнены compile-check Sub2Api test project (0 warnings/errors) и адресный повтор его 2 cases (2 passed, 0 failed/skipped); они уже входят в 403 и не прибавляются к итогу. Статически сверены exact main/delta safety requirement, XML solution и новые solution items, локальные Markdown ссылки, отсутствие duplicate requirement titles, UTF-8 без U+FFFD/четырёх вопросительных знаков/mojibake и размеры изменённых C# (менее 20 000 bytes). Content правки выполнены apply_patch; исходные CRLF в main spec, solution и существующем generation test восстановлены отдельно без изменения текста, LF остальных файлов сохранён. Это не strict CLI validation. Локальные AGENTS отражают новые границы; исходники новой группы выделены в CompactSafety, persistence restart cases остаются в существующей тематической Sub2Api папке.

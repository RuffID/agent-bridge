# 17 — Выбрать и подключить токенизатор модели

Статус: **Реализован, адресно проверен и принят координатором; локальный коммит31 утверждённого файла разрешён**. Зависимости: **13, 16** приняты. Исходный HEAD085779a15c4126ac567a6d1307f497dcae8dc9c5, tree/index были чистыми. Этап18 и следующие не начинались.

## Цель

Использовать полноценную библиотеку токенизации для .NET с проверенной поддержкой кодировок моделей через `IContextTokenCounter`.

## Задачи

- [x] Выбрать поддерживаемую библиотеку токенизации, совместимую с .NET 10 и кодировками поддерживаемых моделей.
- [x] Проверить соответствие моделей и кодировок, исключив произвольный выбор токенизатора для неизвестной модели.
- [x] Подсчитывать токены подготовленных текстовых инструкций, сообщений, схем инструментов и результатов.
- [x] Разделять локальную токенизацию текста и данные для оценки бюджета непрозрачного или мультимодального содержимого.
- [x] Применять настраиваемый запас и проверку бюджета входных данных модели.
- [x] Документировать зависимость, подтверждение соответствия кодировок и границы точного и оценочного подсчёта.

## Проверка и завершение

Проверить образцы известных кодировок с русским текстом, числами, знаками препинания и JSON инструментов. Этап завершён, когда `text.Length / 4` не выдаётся за точную токенизацию, а неподдерживаемые соответствия моделей и кодировок обозначаются явно.

Источник: [решения по токенизатору](<../../Technical documentation/07-tokenizer-and-settings.md>).

## Фактическая реализация

Прочитаны ancestor/root/Application/Configuration/tests/Documentation/Plans AGENTS, csharp style/build/agents/domain, service-result, DI и OpenSpec context rules; main spec/context, бизнес/техничка, actual13 catalog/InputContextWindow и16 ContextBuilder/принятый отчёт/API. EFCoreLibrary/HttpClientLibrary paths доступны; соседние проекты не анализировались/не изменялись, их новые контракты не требовались. OpenSpec model-tokenizer proposal/tasks/spec/context созданы до coding, main spec/context синхронизированы. CLI недоступен, установка не выполнялась.

ContextTokenCounter за existing IContextTokenCounter использует Microsoft.ML.Tokenizers2.0.0 и embedded data packages той же версии, без runtime download. Direct Bcl.Memory10.0.4 исправляет NU1903 транзитивной9.0.4. Конечный ordinal mapping из direct OpenAI tiktoken0.12.0 entries: gpt-5/gpt-4.1/gpt-4o/o1/o3/o4-mini→o200k_base; gpt-4/gpt-3.5-turbo→cl100k_base. No prefixes/fallback/case normalization. Координатор подтвердил finite scope; обязательных дополнительных ID/defaultmodel нет. Все другие IDs, включая версии/codex-lb aliases/gpt-6, Unsupported, не утверждение о доступности сервера.

Factory применяет regex и embedded resource layout pinned package nuspec commit efefa92f4486a43047c5b47618885a71bf7f0967; special markers ordinary. CountTokens использует local рабочие данные и lock-protected LRU, singleton проверен bounded concurrency. Vocabulary tests восстанавливают token/rank после Capacity header и удаления ranks package build: оба canonical SHA256 совпали с OpenAI0.12.0. Источники/дата2026-10-04/hashes/package-layout/regex/cancellation limits подробно в техничке07; public GitHub API rate limit при дополнительном запросе commit history не обходился credentials, release tag+source hashes достаточны.

KnownTokens учитывает instructions и весь prepared input/tools/schema/results/input parameters. Estimate известного входа — max(known, BPE JSON framing), только локальная оценка без upper-bound/billing/server гарантии. Reserve отдельно. Unknown fields/types/roles/controls, opaque reasoning/compact/multimodal/files и continuation скрытого server state у встроенного counter → known отдельно/estimate=null/opaque=true. Encrypted/base64/IDs не выдаются за input text. Независимый порт сохраняет обоснованную полную оценку custom counter при opaque=true; новый estimator/servercount/usage shortcut не реализован.

ContextBudgetGuard.CheckAsync(request, ModelSettingsSnapshot, callerToken) повторно проверяет exact selection/settings, использует только InputContextWindow, reserve/window subtraction без overflow, equality допускает по оценке; ThresholdReached при estimate>=threshold. Null estimate Unsupported; превышение Rejected; same typed failure сохраняется до late cancellation, успешный counter после cancellation отклоняется. Assessment constructor self-consistent. Explicit AddAgentBridgeTokenization TryAdd singleton counter/transient guard сохраняет counter приложения. Guard не встроен в transport: приложение вызывает его явно, AgentRunner20 отсутствует. Не изменены composition/history/expiry/DB/HTTP и не реализованы18/19/20/21.

## Команды и результаты 2026-10-04

Все commands с workdir `D:\Media\User\source\repos\agent-bridge`. Перед restore/build статически проверены root/test csproj, ancestor Directory.Build.props/targets/NuGet.config/locks и generated imports; дополнительных project Exec/hooks/lockfiles нет. Каркас csproj/slnx сохранён в корне, добавлены только4 PackageReferences; pack/publish нет. Применены csharp-project-rules, aspnetcore-project-rules и service-result-pattern; OpenSpec docs разделяют norms и rationale.

```powershell
dotnet restore .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -p:GeneratePackageOnBuild=false
dotnet build .\agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false
dotnet build .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false
dotnet test .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --filter 'FullyQualifiedName~ContextTokenCounterTests|FullyQualifiedName~ContextBudgetGuardTests' --logger 'trx;LogFileName=stage17-tokenizer.trx' --results-directory .\artifacts\test-results\stage17
dotnet test .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --filter 'FullyQualifiedName~ContextTokenCounterTests|FullyQualifiedName~ContextBudgetGuardTests|FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ApplicationPortsTests|FullyQualifiedName~ModelSelectionTests|FullyQualifiedName~ConfigurationTests' --logger 'trx;LogFileName=stage17-affected-final.trx' --results-directory .\artifacts\test-results\stage17
```

| Граница | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Новый первоначальный набор до vocabulary/concurrency additions | 68 | 0 | 0 |
| Промежуточный affected набор | 186 | 0 | 0 |
| Финальные ContextTokenCounterTests | 51 | 0 | 0 |
| Финальные ContextBudgetGuardTests | 20 | 0 | 0 |
| ContextBuilderTests regression | 59 | 0 | 0 |
| ApplicationPortsTests regression | 27 | 0 | 0 |
| ModelSelectionTests regression | 14 | 0 | 0 |
| ConfigurationTests regression | 18 | 0 | 0 |
| **Итого финальный affected** | **189** | **0** | **0** |

Финальный TRX `artifacts/test-results/stage17/stage17-affected-final.trx`; он перезаписан финальным успехом, исходные failures не скрываются. Финальные root/test builds:0 warnings/errors, repeated restore после Bcl.Memory override без NU1903. Основные проверки: Russian/math/punctuation/tool JSON/ordinary markers vectors, все8 exact aliases и unsupported IDs; actual builder providers/history/new input/instructions/tool calls/results/schemas; input controls против transport metadata; reserve/threshold/budget equality, int/long overflow; opaque/unknown fields/controls/continuation, caller и late cancellation/failure identity; DI и shared cache.

### Исходные ошибки проверок

- Первоначальный restore успешен с NU1903 (Bcl.Memory9.0.4); исправлен pinned10.0.4 по первичному advisory, audit не отключался.
- Два первых test compile attempts:2 errors CS1729 (private constructors IDs), затем2 errors CS1929 (ошибочное имя Create вместо actual From). Fixture исправлена. В первом attempt также2 xUnit2031 warnings; заменены overload Assert.Single.
- После failed build был ошибочно запущен `--no-build` filtered test на старой DLL: новых tests не найдено,0 cases; это не успех. Затем actual успешная сборка/новые tests.
- XML comments исправлялись: successful test build сначала19 CS1591 warnings, затем38 CS1591/CS1587 из-за комментариев после attributes; перенесены перед attributes. Final0warnings/errors.
- Два последующих vocabulary test runs:187 passed/2 failed/0 skipped каждый. Первое предположение о raw hash оказалось неверным: package strips ranks; во второй реконструкции ещё не был пропущен Capacity header. После чтения actual pinned eng/TokenizerData.targets header/ranks восстановлены правильно, оба canonical hashes совпали; final189/0/0. Golden BPE payload vectors с первого actual запуска прошли.

## Ограничения и запрещённые проверки

- **Пропущено по указанию пользователя:** real codex-lb/OpenAI/upstream/accounts/tokens/HTTP; hosting/локальные HTTP servers/TestServer/WebApplicationFactory/Telegram/application; arbitrary project/user/demo scripts; Windows program installs; pack/publish/deploy/внешние uploads/push/fetch/pull/PR; рабочие DB/SQL/migrations. Они не являются test skips или успешными checks.
- DB/Docker/backup/integration не запускались: этап17 не меняет их. Старые SQLite/PostgreSQL результаты не перепроверены здесь и не доказывают SQLServer/MySQL/текущую EFCoreLibrary0.0.5.
- OpenSpec CLI отсутствует (`Get-Command openspec`), strict validation не выполнена; change не архивирован. Static main/delta/docs checks не заменяют CLI validation.
- Only affected189, не весь core/adapter/persistence/library набор. Offline vectors/DI/concurrency не доказывают live compatibility, server count или correctness неизвестных model mappings.
- Нет нового server estimator; built-in null full estimate запрещает guard success, даже при большом reserve. Внутри одного synchronous BPE/serialization cancellation проверяется после возврата. Source/package update требует повторной проверки layout/regex/vocabulary.
- Координатор принял этап17 и разрешил локальный English Conventional Commit ровно31 файла из manifest ниже после normal/staged diff проверки. CLI limitation принята как невыполненная проверка, change не архивируется. Будущий hash17 не записывается заранее; фактический полный hash и show/stat/status возвращаются отдельным отчётом после commit. После17 этот executor останавливается,18–25 не начинать.

## Полный manifest для приёмки

**31 файл:18 tracked modifications +13 новых/untracked**, только agent-bridge. Ручные правки apply_patch. Generated/build outputs исключены Git ignore, соседние библиотеки и адаптеры не изменены. `git diff --check` чистый; `git diff --cached --name-only` пустой. Для18 tracked files сравнение raw `git show HEAD:<path>` bytes с working bytes подтвердило UTF-8 без BOM/LF в обеих версиях, EOL/BOM mismatch отсутствуют; предупреждение Git LF→CRLF связано с checkout conversion policy, не сменой blob EOL. Для всех31 файлов strict UTF-8 decode и проверка replacement/mojibake/четырёх question marks без находок. Main spec содержит exact delta block. Эти static checks не OpenSpec CLI validation.

```text
M AGENTS.md
M Application/AGENTS.md
M Application/Models/ContextTokenCount.cs
M Configuration/AGENTS.md
M Documentation/Business logic/03-context-and-compaction.md
M Documentation/Plans/AgentBridge Initial Implementation/17-model-tokenizer.md
M Documentation/Plans/AgentBridge Initial Implementation/README.md
M Documentation/README.md
M Documentation/Technical documentation/01-architecture.md
M Documentation/Technical documentation/07-tokenizer-and-settings.md
M Documentation/Technical documentation/09-application-ports.md
M Documentation/Technical documentation/16-context-composition.md
M Documentation/Technical documentation/README.md
M README.md
M agent-bridge.csproj
M openspec/specs/agent-runtime/context.md
M openspec/specs/agent-runtime/spec.md
M tests/AGENTS.md
?? Application/ContextBudgetGuard.cs
?? Application/Models/ContextBudgetAssessment.cs
?? Configuration/AgentBridgeTokenizationExtensions.cs
?? Tokenization/AGENTS.md
?? Tokenization/ContextTokenCounter.cs
?? Tokenization/ModelEncodingMap.cs
?? Tokenization/OrdinaryTokenizerFactory.cs
?? openspec/changes/model-tokenizer/context.md
?? openspec/changes/model-tokenizer/proposal.md
?? openspec/changes/model-tokenizer/specs/agent-runtime/spec.md
?? openspec/changes/model-tokenizer/tasks.md
?? tests/AgentBridge.Tests/ContextBudgetGuardTests.cs
?? tests/AgentBridge.Tests/ContextTokenCounterTests.cs
```

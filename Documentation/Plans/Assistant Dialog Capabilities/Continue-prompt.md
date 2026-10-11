# Промпт для продолжения

```text
Продолжи приостановленную реализацию библиотечных возможностей AgentBridge для Ledger 01A.

Рабочий репозиторий: D:\Media\User\source\repos\agent-bridge.
Сначала прочитай:
D:\Media\User\source\repos\agent-bridge\Documentation\Plans\Assistant Dialog Capabilities\Checkpoint.md
и применимые AGENTS.md/скиллы. Не начинай работу заново и не выдавай старые результаты за проверки текущих исходников.

Источники требований только для чтения:
D:\Media\User\source\repos\work\AquaByte-Ledger\Documentation\Plans\ai-assistant-widget-2026-10-10\01a-agentbridge-change-request.md
D:\Media\User\source\repos\work\AquaByte-Ledger\Documentation\Plans\ai-assistant-widget-2026-10-10\01a-storage-and-recovery-contract.md
D:\Media\User\source\repos\work\AquaByte-Ledger\Documentation\Plans\ai-assistant-widget-2026-10-10\01a-agentbridge-dialog-capabilities.md

Разрешаю продолжить точечные изменения через apply_patch, необходимые restore только из локального package cache C:\Users\Spike\.nuget\packages, compile-check и изолированные тесты по профильным правилам после проверки inputs/hooks.
Запрещены Git, сеть, реальные БД/SQL/backup/migrate, Docker, браузер, hosting/dev/watch, publish/deployment. Проектные скрипты и исполняемые файлы отдельно не запускай без разрешения точной команды. Не создавай subagents без отдельного указания. Locks и generated migrations вручную не редактируй.
Ledger код/vendor/locks/статусы/исторические планы не изменяй, этапы 02–09 не начинай. Сохрани exact EF 10.0.12 / SqlClient 7.0.2, обязательные roots и exact/RID/lock guards.

Сейчас источник не подтверждён сборкой: последнее расширение DialogWriteGuard переименовано в LoadRunAsync, но named lease/catalogControl call sites ещё вызывают LoadAsync. Исправь эти вызовы, сохрани legacy LoadAsync и его бинарную сигнатуру, затем compile-check. Последние evidence/quiescence/constructor изменения не тестировались.

Заверши bounded scope/profile catalog, atomic library-owned index/feed, durable lifecycle/readiness/fencing и append-only recovery/effective context по нормативному контракту. Проверь последние IDialogRecoveryEvidenceResolver/IDialogRunQuiescenceVerifier: evidence I/O вне transaction, затем CAS/epoch recheck; idempotent durable operation lookup без повторной проверки. Не превращай Unknown в NotStarted/Canceled по cancel/read-only/lease. Не replay прежний handler, не выдумывай успешный output, confirmed outputs сохраняй неизменными. Продолжение только после durable Ready, тот же dialogId, новый явный turnId/контекст.

Добавь substantive actual-public-API tests недостающих evidence/CAS/races/failures и свежие affected/core проверки. До последних изменений отдельно прошли 41 новый case и 542 core tests; full isolated persistence имел 3 strict snapshot failures из-за отсутствующих migrations. Это прежнее evidence, не текущий green.

Generated migrations/snapshots пока отсутствуют для всех трёх providers. Installed dotnet-ef driver 10.0.11, runtime/Design 10.0.12; сети нет. Определи совместимый штатный local tooling путь и запроси точные команды перед генерацией, БД/SQL не запускай. Не ослабляй snapshot assertions.

Обнови owning AGENTS и актуальную документацию/OpenSpec, сохрани UTF-8/line endings. Подготовь новую полную coherent SQL Server DLL/XML/props/manifest поставку для win-x64/linux-x64/linux-arm64 штатным mechanism. Перед запуском Assemble-Delivery.ps1/Assemble-NuGetDelivery.ps1 запроси разрешение точных команд с проверенными inputs/provenance. Prepare-DeliveryTests.ps1 не запускай как есть: default network restore запрещён. Старую поставку/manifest hash не переиспользуй как новую.

В конце передай фактические публичные сигнатуры и обоснованные отличия от предлагаемых, свежие результаты проверок, путь/hash новой поставки либо точный blocker, непроверенные SQL/provider/native/Linux/live категории и порядок отдельного принятия в Ledger. Не объявляй Ledger 01A завершённым этой работой.
```

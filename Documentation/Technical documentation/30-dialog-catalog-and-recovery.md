# Библиотечный каталог диалогов и durable recovery

Реализованный source API для Ledger 01A. Это не HTTP/widget/profile implementation Ledger и не подтверждение его завершения. [Evidence и поставка](<../Plans/Assistant Dialog Capabilities/Evidence.md>) отделяют isolated checks от непроверенной SQL/provider/runtime приёмки.

## Public API

Все типы находятся в `AgentBridge.Application.Models/Ports`; ServiceResult — существующий AgentBridge result. CancellationToken в методах ниже optional `= default`.

```csharp
IDialogCatalogCreator.CreateAsync(DialogCatalogCreate request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogCatalogState>>
IDialogCatalogReader.ReadAsync(DialogCatalogRead request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogCatalogSlice>>
IDialogCatalogReader.ReadStateAsync(DialogAccess access, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogCatalogState>>
IDialogCatalogChangeReader.ReadChangesAsync(DialogCatalogChangeRead request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogCatalogChangeSlice>>
IDialogCatalogProjector.Key -> string; Version -> int
IDialogCatalogProjector.Project(SavedDialogMessage message, DialogCatalogTextState current, DialogProjectionPolicy policy)
    -> ServiceResult<DialogCatalogTextUpdate>
IDialogContinuationReader.ReadAsync(DialogAccess access, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogContinuationState>>
IDialogRecovery.RecoverAsync(DialogRecoveryRequest request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogRecoveryResult>>
IDialogRecovery.ReadAsync(DialogAccess access, Guid recoveryId, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogRecoveryResult>>
IDialogReader.ReadCatalogAsync(DialogCatalogAccess access, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogSnapshot>>
IDialogRunLifecycle.BeginAsync(DialogRunBegin request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogRunState>>
IDialogRunLifecycle.RenewAsync(DialogAccess access, DialogRunLease expected, TimeSpan leasePeriod, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogRunLease>>
IDialogRunLifecycle.FinalizeAsync(DialogRunFinalize request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogContinuationState>>
IDialogRunLifecycle.FenceAsync(DialogRunFence request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogContinuationState>>
IDialogRecoveryEvidenceResolver.ResolveAsync(DialogRecoveryEvidenceRequest request, CancellationToken cancellationToken)
    -> Task<ServiceResult<DialogRecoveryEvidence>>
IDialogRunQuiescenceVerifier.VerifyAsync(DialogRunFence request, CancellationToken cancellationToken)
    -> Task<ServiceResult>
```

Epoch-aware overloads existing ports сохраняют остальные аргументы/ServiceResult:

```csharp
IDialogTurnWriter.AppendAsync(DialogRunWriteAccess access, DialogWriteToken expected,
    Guid turnId, IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> modelSteps, CancellationToken cancellationToken)
IDialogToolAttemptWriter.StartAsync(DialogRunWriteAccess access, DialogWriteToken expected,
    ToolExecutionIdentity identity, CancellationToken cancellationToken)
IDialogToolAttemptWriter.SaveOutcomesAsync(DialogRunWriteAccess access, DialogWriteToken expected,
    Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken)
IDialogContextWriter.SaveWithModelAsync(DialogRunWriteAccess access, DialogWriteToken expected,
    long throughTurnSequence, ModelResponse compaction, string selectedModel, CancellationToken cancellationToken)
// Каждый возвращает Task<ServiceResult<DialogWriteToken>>.
```

Главные DTO constructors:

```csharp
DialogCatalogCreate(DialogId DialogId, DialogCatalogScope Scope, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc, CatalogAccessProfile Profile, DialogProjectionPolicy Policy)
DialogCatalogScope(DialogOwnerId OwnerId, string SiteId, string AgentId)
CatalogAccessProfile(string key, int version, JsonElement data)
DialogProjectionPolicy(string Key, int Version, int MaxTitleScalars, int MaxSnippetScalars, int MaxMessageBytes)
DialogCatalogRead(DialogCatalogScope Scope, DialogCatalogCursor? After, int Limit, DateTimeOffset NowUtc)
DialogCatalogChangeRead(DialogCatalogScope Scope, long Checkpoint, int Limit, DateTimeOffset NowUtc)
DialogCatalogAccess(DialogAccess Access, DialogCatalogScope Scope, CatalogAccessProfile Profile)
DialogRunWriteAccess(DialogAccess Access, DialogRunLease Lease)
DialogRunLease(Guid IncarnationId, Guid TurnId, long Epoch, Guid LeaseId, DateTimeOffset DeadlineUtc)
DialogRunBegin(DialogAccess access, DialogWriteToken expected, Guid turnId,
    IEnumerable<CanonicalModelItem> input, TurnModelSettings settings, TimeSpan leasePeriod,
    DialogCatalogScope scope, CatalogAccessProfile profile)
DialogRunFinalize(DialogAccess Access, DialogWriteToken Expected, DialogRunLease Lease, DialogTurnStatus Status)
DialogRunFence(DialogAccess Access, DialogWriteToken Expected, DialogRunLease Lease, Guid OperationId,
    DialogRunFenceReason Reason = ExpiredLease, string? EvidenceReference = null)
DialogRecoveryRequest(DialogAccess access, DialogWriteToken expected, Guid recoveryId,
    long expectedRecoveryRevision, long expectedEpoch, Guid turnId, IEnumerable<DialogRecoveryResolution> resolutions)
DialogRecoveryResolution(Guid TurnId, Guid StepId, int OutputIndex, DialogRecoveryKind Kind,
    string? AcknowledgementReference = null)
DialogRecoveryEvidenceRequest(DialogRecoveryRequest Request, DialogRecoveryResolution Resolution)
DialogRecoveryEvidence(DialogRecoveryKind Kind, string Reference, CanonicalModelItem? Output)
DialogRecoveryRecord(Guid RecoveryId, long Revision, DialogPendingCall Position, CanonicalModelItem Output,
    DialogRecoveryKind Kind, string? EvidenceReference)
```

AgentRunRequest сохраняет прежний constructor; overload добавляет DialogRunOptions с полным scope/profile/явным LeasePeriod. DialogSnapshot сохраняет прежний constructor и `Turns`; добавлены Catalog/Continuation/EffectiveTurns/EffectiveContext. StoredDialogContext добавляет overload с RecoveryRevision, прежний constructor использует revision 0.

## Отличия от предложенных сигнатур

1. Scope объединён в DialogCatalogScope, обязательный immutable профиль передаётся full value. Списки копируются, JsonElement клонируется; full profile match включает key/version/raw JSON. Это не current permission lookup: host повторно авторизует immutable профиль перед UI/history/model.
2. Projector имеет Key/Version: библиотека проверяет exact single registration до Create и каждого canonical save. Unknown/ambiguous version отменяет write. Проектируемый Ledger question envelope не выдан за библиотечный API и не разбирается production-кодом AgentBridge.
3. Full-profile history read назван ReadCatalogAsync: overload ReadAsync нарушал source compatibility target-typed `new(...)` существующих вызовов. Legacy ReadAsync сохранён, но registered root требует полный профиль.
4. RecoveryId lookup добавлен для authoritative read после unknown commit; успешный retry того же ID/payload возвращает принятый факт без повторного handler/verifier.
5. KnownCanceled/ConfirmedExternalOutcome и ConfirmedQuiescence используют отдельные trusted host ports, без raw client output/флага. Evidence разрешается вне transaction, затем CAS/epoch проверяются повторно. Отсутствующий port — Unsupported.
6. Snippet отсутствия разрешённого текста представлен empty string в compact text DTO; host проецирует его в nullable UI snippet. LastSavedMessageAtUtc nullable. Recovery record.Output может быть truthful error или trusted external output, поэтому не назван ErrorOutput.
7. Старые public constructors writer/reader и LoadAsync guard сохранены явными overloads; расширенный внутренний guard назван LoadRunAsync для исключения optional-overload ambiguity. Legacy port implementations получают Unsupported на новые overloads, без fallback primitive write.

## Metadata и storage

Пустой зарегистрированный чат — «Новый чат», без last date/snippet. Первое сохранённое user question фиксирует title; последующие сообщения title не меняют. Pure whitespace normalization/scalar truncation не разрывает surrogate pair, search key — Form C/invariant uppercase. Host projector извлекает только разрешённый question/visible assistant text; selection/tool/reasoning/delta не становится metadata.

Server SavedAtUtc/стабильная turnSequence/itemPosition фиксируются вместе с canonical items, root CAS, profile, compact projection и ordered change record. Инструменты/checkpoints/compact/recovery/terminal меняют только status/readiness, не last message date/snippet. Registered expiry фиксирован при Create; activity/renew/compact его не продлевают. Legacy roots сохраняют прежнюю retention semantics и не импортируются автоматически.

Catalog query читает один slice 1..256, без full snapshots/count/refill. SortTime = last saved date либо creation; DESC, затем RFC UUID hex DESC с binary collation. Concurrent messages могут дать повторы/пропуски между keyset pages; это не frozen snapshot. Host dedup/refresh не создаёт hidden refill loop. Library cursor привязан к scope/keyset; signature/expiry/search/PageSize binding и title-only literal search принадлежат host adapter, не реализованы здесь.

Десять таблиц current model: прежние шесть + DialogCatalog/Clocks/Changes/RecoveryOperations. Scope hash не заменяет exact owner/site/agent check. Raw profile и metadata не предназначены для браузера; перед выдачей host выполняет актуальный gate. Expired/deleted state скрывает title/snippet. Registered history проверяет profile/expiry и root/projection revision до выдачи children, повторно проверяет root/runtime после reads.

Feed — ordered bounded page 1..256 с scope checkpoint; gap/unknown checkpoint — Unsupported `catalog_reset_required`. Cache применяет checkpoint вместе с projection, dedup по incarnation/revision, tombstone доминирует late same-incarnation events. Reconciliation использует feed и один compact ReadState; отдельный bounded rebuild проходит catalogue keyset. Ни один library feed/rebuild path не читает full snapshots. Tombstone/feed/clock сохраняются после root delete, registered ID reuse запрещён.

AddAgentBridgePersistence регистрирует новые scoped ports, base repositories/staging и TimeProvider. Transaction boundaries используют existing EFCoreLibrary Serializable scope без retry; app не использует base repositories для обхода библиотечных ports. Projector/evidence/verifier регистрирует host; DI не выполняет I/O.

## Run и recovery

Begin/input/settings/lease acquire — одна transaction, потом внешний model/handler I/O. LeasePeriod положителен, максимум сутки; приложение задаёт срок, покрывающий bounded run, либо явно renew. Renew не продлевает диалог и не меняет history revision; root RuntimeJson CAS защищает concurrent renew/fence. Epoch-aware writes отвергают старую lease/epoch, даже если caller получил актуальный token.

Readiness: Ready/Active/RecoveryRequired. Expired lease остаётся Active до explicit fence; ExpiredLease сравнивает persisted deadline с TimeProvider, ConfirmedQuiescence требует host evidence остановки исполнителя. Fence блокирует late writes, не доказывает внешний outcome; original InProgress/terminal сохраняются. Finalize после awaited workers атомарно сохраняет terminal/release/readiness. Незакрытые пары дают RecoveryRequired.

Recovery идентифицирует позицию incarnation/turn/step/outputIndex, не глобальный call_id. Full batch <=1024, root lifecycle <=2 MiB, call_id <=256 символов; history recovery budget <=4096 операций (overflow явно отклоняется). Confirmed external output <=1 MiB и также проходит общий lifecycle budget. Original history/confirmed outputs/reports/terminal неизменны, handler/gateway не replay.

| Kind | Основание | Effective protocol output |
| --- | --- | --- |
| NotStarted | Durable journal до handler | tool_not_started |
| AcknowledgedUnknown | Explicit owner acknowledgement reference | tool_outcome_unknown; original outcome остаётся Unknown |
| KnownCanceled | Trusted resolver доказывает отсутствие результата | tool_canceled |
| ConfirmedExternalOutcome | Trusted resolver возвращает exact bound protocol output | Неизменный подтверждённый output, отдельно от original journal |

Synthetic errors имеют recovery/original identity provenance, не являются saved assistant messages. Evidence reference <=256, отсутствие resolver/mismatch/refusal/OCE/exception не создаёт Ready. Caller/lease timeout/read-only сами по себе не являются evidence.

Full batch, quiescence, token/recovery revision/epoch проверяются одной финальной transaction; preflight до evidence не заменяет повторную проверку. Same RecoveryId/payload идемпотентен, altered payload — Conflict. Recovery/Delete/Begin с active lease отказываются без mutation. Unknown save/commit/finalize/read остаётся exception/неподтверждённым фактом; authoritative read прежнего ID/recoveryId в новом scope определяет storage state, не внешний результат.

ContextBuilder/runner проверяют readiness до dispatch, используют original calls + confirmed outputs + ровно один resolution на pending position. Compact старой RecoveryRevision игнорируется; unresolved calls не скрываются. Fenced original InProgress удерживает последующую историю в несжимаемом suffix, не переписывается для компактирования. После Ready следующий вопрос использует тот же DialogId, новый явный TurnId и свежий provider context.

## Приёмка и последующее подключение Ledger

Generated AddCatalogAndDurableRecovery трёх providers проверяется strict snapshot/designer/current model differ и Up/Down metadata. Legacy registration false, recovery revision 0, nullable runtime/message timestamps сохраняют исторические данные. Применение migration, SQL transactions/locking/query plans/actual restart не проверялись.

Новая поставка должна приниматься отдельным разрешением Ledger: verify size/hash/assembly/API всех файлов, импорт штатного props после app PackageReferences, explicit supported RID, local-cache locked restore/build. При изменении graph только NuGet генерирует затронутые locks. Затем actual adapter/projector/recovery tests через новую DLL и свежая Ledger coverage. SQL/provider/native/Linux/live/runtime/hosting acceptance отдельны; эта работа их не закрывает и не начинает Ledger 02–09.

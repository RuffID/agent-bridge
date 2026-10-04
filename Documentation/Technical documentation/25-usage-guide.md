# Подключение и использование AgentBridge

Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). Проверяемые исходники: [Consumer](../../tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj); команды и границы подтверждения: [отчёт25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>). Это compile-only библиотека без entry point. Ни один метод примера при проверке не исполнялся.

## 1. Подключить комплект DLL

Выберите один полный комплект `artifacts/delivery/stage24-win-x64/Sqlite` либо `PostgreSql` и перенесите его целиком в каталог поставки приложения. Каждый содержит39 managed DLL,35 XML, native x64 SQLite, props и evidence/manifest. Комплекты относятся к .NET10/win-x64/Debug. Не смешивайте версии, RID и migrations assemblies. Подробности состава и внешних требований: [поставка24](24-dll-delivery.md).

В SDK-style .NET10 проекте используйте бинарный Import, как в проверенном consumer:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <AgentBridgeDeliveryRoot>C:\MyApplication\vendor\AgentBridge\Sqlite</AgentBridgeDeliveryRoot>
</PropertyGroup>
<Import Project="$(AgentBridgeDeliveryRoot)/AgentBridge.Delivery.props" />
```

Путь здесь — пример абсолютного каталога приложения. Import подключает все managed DLL и доставляет XML/native стандартными SDK items. Для runtime нужен .NET10 и соответствующий хост приложения. PostgreSQL сервер/права/`pg_dump` в DLL не входят. Наличие SQLite native в PostgreSQL kit связано с общей статической dependency closure и не меняет выбранный provider. Для существующего приложения отдельно проверьте конфликты его package/runtime версий; compile-only consumer не доказывает их отсутствие.

## 2. Настроить options и зависимости приложения

[UsageRegistration.AddUsageGuide](../../tests/Delivery/Consumer/UsageRegistration.cs) показывает реальную регистрацию:

1. `AddAgentBridgeConfiguration` получает раздел `AgentBridge` с группами `Agent`, `Retention`, `Compaction`.
2. `AddCodexLbConfiguration` получает `CodexLb`; `AddDatabaseConfiguration` — `Database`. Provider обязателен.
3. `AddAgentBridgePersistence` регистрирует scoped хранилище. `AddAgentBridgeDiagnostics` подключает ILogger, сохраняя pipeline приложения.
4. Приложение явно передаёт фабрики `IIndividualModelKeySource`, управляемого `HttpClient`, **упорядоченного** списка `IContextProvider` и scoped бизнес-сервиса примера. `ContextBuilder` регистрируется явно: `AddAgentBridgeRunner` не создаёт его или providers.
5. `AddCodexLbResponses` подключает каталог/access/settings reader и JSON/SSE/compact gateway через actual HttpClientLibrary. Повторно подключать `AddCodexLbModelCatalog` не требуется.
6. `AddAgentBridgeTool`, `AddAgentBridgeTokenization`, `AddAgentBridgeCompaction`, `AddAgentBridgeRunner`, `AddAgentBridgeSettings`, `AddAgentBridgeDialogCleanup` подключают отдельные сценарии без их запуска.

Пример формы configuration (endpoint, модель и путь условные; доступность model/effort проверяется каталогом):

```json
{
  "AgentBridge": {
    "Agent": { "Instructions": "Отвечай по разрешённым данным.", "MaxToolSteps": 8 },
    "Retention": { "RetentionPeriod": "7.00:00:00", "SoftContentLimitBytes": 10485760 },
    "Compaction": { "TokenThreshold": 32000, "InputTokenReserve": 4096, "MaxPasses": 3 }
  },
  "CodexLb": {
    "BaseAddress": "https://codex-lb.example.invalid/",
    "Model": "gpt-5", "ReasoningEffort": "medium",
    "GenerationTimeout": "00:03:00", "CompactTimeout": "00:03:00"
  },
  "Database": { "Provider": "SQLite", "ConnectionString": "Data Source=C:\\MyApplication\\data\\agent-bridge.db" }
}
```

Для PostgreSQL замените provider на `PostgreSql` и предоставьте реальную строку подключения через secret configuration приложения; поставьте выбранную PostgreSQL migrations DLL. `CodexLb:SharedApiKey` также приходит из secret store/configuration, а не из коммитимого примера. Строки подключения, ключи, raw headers/body и canonical payload не выводятся в logs/UI.

Все числовые значения выше — переопределяемые начальные значения, а не обязательная политика внедрения. `RetentionPeriod` положителен и фиксирует expiry при создании; новая конфигурация не пересчитывает старые сроки. `SoftContentLimitBytes` положителен, даёт предупреждение при `bytes >= limit`, не запрещает запись и не вызывает cleanup. `TokenThreshold` положителен, `InputTokenReserve >= 0`, `MaxPasses > 0`. Проверенный threshold+reserve должен укладываться в **input_context_window**, context_window не заменяет неизвестный входной бюджет. [Полный options API](05-configuration-and-lifecycle.md), [бюджет/tokenizer](07-tokenizer-and-settings.md).

Logging/Serilog provider и его redaction настраивает приложение. Фабрики DI не выполняют I/O, HttpClient/handlers принадлежат приложению; не добавляйте retry или замену ключа/аккаунта. Scoped зависимости не разделяются между параллельными tools. Для ASP.NET Core вызывайте групповую регистрацию в composition root; development DI validation остаётся включённой. В приложении без host явно проверяйте полученные options и lifetimes: compile-check не исполняет `ValidateOnStart` или контейнер.

## 3. Подготовить БД отдельной операцией

Регистрация не создаёт БД и не применяет migrations. Перед пользовательскими обращениями приложение подготавливает хранилище через [явный maintenance API](06-database-maintenance.md#подключение-agentbridge-этапа-12). Проверенный [BinaryContractProbe.Register](../../tests/Delivery/Consumer/BinaryContractProbe.cs) содержит `AddAgentBridgeDatabaseMaintenance(backup, MaintenanceExecutionMode.SingleInitializer)`; `UsageRegistration` намеренно оставляет maintenance отдельной процедурой приложения.

Обязательны абсолютный backup directory и явный положительный backup retention. Для PostgreSQL — полный установленный toolchain, абсолютный pg_dump path, совпадающий server/dump major и конечный cleanup timeout. SingleInitializer требует остановки других экземпляров/writes/DDL приложением. В отдельном scope явно выбираются `InspectAsync`, `InitializeNewAsync` либо `UpdateExistingAsync` с timeout/отменой. Ошибка подключения не означает отсутствующую БД; автоматического restore/fallback нет. Cleanup диалогов и retention backup — разные операции приложения.

## 4. Ключи, каталог и выбор model/effort

[IndividualKeySource](../../tests/Delivery/Consumer/IndividualKeySource.cs) — адаптер delegate к secret store приложения. Он возвращает исходный ключ: **только null** означает отсутствие и разрешает shared key. Пустая строка/ошибка заданного ключа/отказ источника не разрешают общий ключ. `ModelAccess` живёт только на вызов и не сохраняется.

[UsageFlow.ReadModelsAsync](../../tests/Delivery/Consumer/UsageFlow.cs) сначала вызывает `IModelAccessResolver.ResolveAsync(owner)`, затем `IModelCatalog.ReadAsync(access)` в scoped boundary. UI получает `ModelCatalogSnapshot.Models`: `Id`, `ReasoningEfforts`, nullable `InputContextWindow` и другие safe capabilities. Это динамический каталог выбранного ключа; не заменяйте его фиксированным списком или default effort при отказе.

Для сохранения выбора сначала `ReadSettingsAsync` → проверить `ServiceResult.Success` → отобразить `AgentSettingsSnapshot` → `SelectAsync` с **исходными** `Token` и `SelectionVersion` и выбранными exact model/effort. Не перечитывайте версии для автоматического принятия устаревшего UI. Conflict требует отдельного нового действия пользователя. Выбор сохраняется для конкретного диалога с независимой CAS version и не прерывает active run.

Приоритет обращения: явный override → saved dialog selection → defaults приложения. `UsageFlow.RunAsync` принимает nullable `modelOverride`/`effortOverride`; override не persist. Активный run сохраняет атомарный `TurnModelSettings`. Selected model и actual `ServerModel` различны; server name не доказывает совместимость. Opaque другой/неизвестной selected model требует доказательного `IContextModelCompatibility` приложения. В примере этот порт не подставлен: отсутствие proof даёт Unsupported, история сохраняется. Совместимость не доказывает token budget. [Подробный контракт21](21-settings-and-dialog-status.md).

## 5. Создать диалог и вызвать агента

Полный проверяемый C# исходник: [UsageFlow](../../tests/Delivery/Consumer/UsageFlow.cs). Порядок вызовов приложения:

1. Авторизовать пользователя и выбранный `AgentId`; `ApplicationCallContext` сам не является разрешением.
2. Создать `DialogId.From(Guid.NewGuid())`, `DialogOwnerId.From(authenticatedSubject)` в пространстве ID приложения. Вызвать `CreateAsync(root, dialogId, owner, ct)`. Оно использует `TimeProvider`, `DialogRetentionOptions.CalculateExpiresAtUtc` и `IDialogCreator.CreateAsync` в отдельном коротком scope. Проверить typed результат до продолжения; существующий ID даёт Conflict.
3. Создать `ApplicationCallContext(dialogId, owner, Guid.NewGuid(), authorizedAgentId)` для **нового** turn. Вызвать `RunAsync(root, call, text, onUpdate, ct)`. Проверенный исходник строит canonical user message через `JsonSerializer.SerializeToElement`, а не конкатенацией JSON; передаёт только новый input и exact tool names.
4. `onUpdate=null` выбирает JSON; non-null — SSE. Callback awaited, соблюдает токен отмены и передаёт UI только разрешённый видимый текст. `Item`, envelope и tool output могут содержать чувствительные данные; не логируйте их. Delta предварительна, не подтверждает terminal сохранение.
5. Проверить `AgentRunResult.Status`, `TerminalSaved`, `Error`, при необходимости сохранённый `Turn`. `LastResponse`/`LastTools` сами не доказывают принятие storage. Успешный ответ — Completed с подтверждённым terminal save.

`ToolExecutionLimits(8,16,2,1 minute)` в примере — явные limits приложения; `AgentOptions.MaxToolSteps` дополнительно ограничивает шаги. Timeout cooperative: handler/callback обязаны завершаться по cancellation, library ожидает начатые tasks/scopes.

Providers выполняются последовательно один раз на run и сами авторизуют свой вклад. Допустимы исходные canonical roles; инструкции находятся в `ModelRequest.Instructions`. Входной request состоит из providers, active context, непокрытой истории и нового input; reports/envelope/continuation не добавляются повторно. Runner не использует compact id как generation previous_response_id. Compact покрывает только непрерывный terminal prefix, включая0; InProgress не считается покрытым. История не удаляется ради бюджета.

## 6. Подключить свои инструменты

Проверенный пример read-only инструмента состоит из [AccountSummaryTool](../../tests/Delivery/Consumer/AccountSummaryTool.cs), [AccountSummaryValidator](../../tests/Delivery/Consumer/AccountSummaryValidator.cs) и [IAccountSummarySource](../../tests/Delivery/Consumer/IAccountSummarySource.cs).

`IAccountSummarySource` — **пример порта приложения**, не тип поставляемой AgentBridge. Реализацию scoped factory передаёт приложение: настоящие текущие права owner/agent, повторная авторизация внутри чтения и safe разрешённая сводка. Заглушка успешных DB/HTTP вызовов не поставляется. Validator проверяет всю простую schema: exact name, object без полей (`properties={}`, `required=[]`, `additionalProperties=false`), затем актуальные права. Handler повторно проверяет допуск до бизнес-чтения; metadata совпадают с регистрацией. Для собственной сложной схемы приложение обеспечивает полную schema validation, executor не является общим JSON Schema engine.

`AddAgentBridgeTool<THandler,TValidator>(definition)` даёт отдельный invocation scope. Selected names не являются авторизацией. Бизнес-I/O выполняется после durable Started и вне write transaction AgentBridge. Не передавайте рабочий DbContext нескольких tasks в singleton. Подтверждённый отказ возвращается `ServiceResult.Fail` с безопасным сообщением; исключение/timeout после начала не объявляйте подтверждённым отказом, результат может быть Unknown. [Tools19](19-application-tools.md), [durable runner20](20-agent-turn-orchestration.md).

## 7. Показать status/expiry и запланировать очистку

`UsageFlow.ReadStatusAsync` возвращает `ServiceResult<DialogStatus>` через `AgentSettingsService.GetStatusAsync`. UI отображает `ExpiresAtUtc` в своей timezone и `IsExpired`, bytes/soft warning, selected model/effort, actual server model и контекст. При `now >= expiry` запись запрещена, но lifecycle metadata доступны до физического удаления. Активность/compact/settings срок не продлевают. `ExpiresAtUtc` — граница доступности, **не гарантированное время физического удаления**: оно зависит от расписания cleanup приложения.

`ContextSize.KnownTokens` и nullable `EstimatedInputTokens` различаются. Status измеряет только сохранённое окно/историю без следующего input/providers/instructions/tools; `CanContinue` позволяет подготовку, не гарантирует полный generation budget. `CompactionThresholdReached=null` не означает false. Unknown/opaque не считается нулём. Exact offline mapping: o200k — gpt-5/gpt-4.1/gpt-4o/o1/o3/o4-mini; cl100k — gpt-4/gpt-3.5-turbo. Другие ID, включая gpt-6 и suffixed IDs, не поддержаны offline; server estimator не реализован. Guard откажет при неизвестной полной оценке.

`UsageFlow.CleanupAsync(root, limit, ct)` показывает отдельный caller scope и один явный bounded пакет. Limit положителен и определяется приложением; расписание и системная авторизация также у приложения. Read освобождается до последовательных delete с отдельными scopes/fresh UTC. Completed подтверждает только обработанный пакет, не отсутствие всех истёкших строк. Partial/Failed/Canceled/Interrupted и candidate Unknown не выдаются за успех. Если нужен прогресс после exception, выполняйте прямой вызов в собственном caller scope и читайте `cleanup.LastResult` **до его освобождения**, как в [контракте22](22-expired-dialog-cleanup.md); helper `UsageFlow.CleanupAsync` такого доступа вызывающему не предоставляет. Новый запланированный вызов может обработать следующую порцию; внутри сценария нет drain-loop/refresh/retry.

## Обработка ошибок и recovery

Typed ServiceResult failures передаются UI по semantic `ServiceError.Type` и безопасному сообщению, которое обязано обеспечивать и приложение. Conflict/Expired/Unsupported/Timeout не превращаются в новую попытку. Неожиданные exceptions и aggregate cleanup errors распространяются; диагностируйте только безопасные codes/counts, не `Exception.ToString()` вместе с driver/HTTP секретами.

Run `Canceled`, `Incomplete`, `Interrupted` не являются Completed. Поздняя отмена может дать `Canceled` с `TerminalSaved=true` и уже принятым `Turn.Completed`: статус caller и факт commit — разные сведения. Candidate/tool Unknown означает отсутствие acknowledgement, а не rollback или разрешение retry. Synthetic acknowledgement tests не доказывают потерю реальной сети.

Existing TurnId не исполняется повторно; restart не возобновляет Started tool автоматически. Известный function_call без output, включая partial arguments SSE, блокирует следующий запрос. Нельзя удалить историю, придумать output либо заменить TurnId ради повторного действия. Повторные завершённые пары с одинаковым call_id допустимы и не дедуплицируются глобально. Recovery/новое действие после диагностики определяет приложение, библиотека не предоставляет автоматический replay.

## Границы подтверждения

Примеры скомпилированы с каждым kit вне репозитория,0 warnings/errors, по206 references=39 kit+167 framework; нет project/package references. Проверены копирование XML/native и metadata inheritdoc; generated XML хранит `<inheritdoc/>`, автоматическое разворачивание конкретной IDE не проверено. EFCoreLibrary CRUD не генерирует XML, три SQLitePCLRaw managed DLL также без XML.

Runtime/DI/options execution, SQLite native loading из комплекта, Release/другие RID/AOT/trimming/single-file не проверены. Предшествующие реальные SQLite/PostgreSQL tests относятся к своим исходникам и окружению, не к runtime kit. Fake HTTP actual HttpClientLibrary не доказывает live codex-lb/OpenAI compatibility. Live HTTP/Telegram/hosting/demo — **Пропущено по указанию пользователя**. OpenSpec CLI отсутствует, CLI validation не выполнена, changes не архивированы. [Карта evidence00–25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>).

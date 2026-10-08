# Настройки и статус диалога

Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md); rationale и решения пользователя — [change context](../../openspec/changes/dialog-settings-and-status/context.md). Фактические команды/evidence — [этап21](<../Plans/AgentBridge Initial Implementation/21-settings-and-dialog-status.md>).

## API

`services.AddAgentBridgeSettings()` регистрирует scoped `AgentSettingsService` и `ContextModelGuard`, default `IContextContentInspector` и `TimeProvider` через TryAdd без I/O. Приложение предоставляет конфигурацию, `IModelSettingsReader`, `IContextTokenCounter` и persistence ports; `AddAgentBridgePersistence()` регистрирует `IDialogSettingsWriter`. Опциональный `IContextModelCompatibility` реализует приложение, имеющее доказанный контракт совместимости своего opaque-состояния.

| Вызов | Результат |
| --- | --- |
| `ReadAsync(call, ct)` | `ServiceResult<AgentSettingsSnapshot>`: проверенный model/effort, limits, `SelectionVersion` и safe `Token` того же snapshot |
| `SelectAsync(call, expected, expectedSelectionVersion, model, effort, ct)` | `ServiceResult<DialogModelSelection>`: сохранённый выбор с новой independent version |
| `GetStatusAsync(call, ct)` | `ServiceResult<DialogStatus>`: срок/байты/context count/compact count/selected и actual server model; context/catalog failure отдельно от доступных lifecycle metadata |

`ApplicationCallContext` и авторизацию `AgentId` определяет приложение. Библиотека проверяет owner/dialog даже при custom reader. Settings/status не возвращают ключи, подключения, instructions, canonical items, raw envelope/headers. Token не является разрешением записи: writer атомарно проверяет его.

## Выбор и конкурентные изменения

Пользователь 2026-10-04 выбрал хранение **в БД AgentBridge для конкретного диалога** и **отдельную версию settings с продолжением активного хода**. Выбор применяется к последующим обращениям независимо от AgentId вызывающего приложения. Приоритет model/effort: явный override обращения → saved dialog selection → defaults приложения. Override не записывается.

DialogSettings имеет `Id=DialogId`, `Version`, `Model`, `Effort`; отсутствие строки — version0/defaults. Новый выбор всегда проверяется exact каталогом доступного ключа. UI передаёт обе исходные версии: root Token защищает состояние, по которому проверялась compatibility, SelectionVersion защищает выбор. Изменение settings не повышает root revision/LastChangedAtUtc и не продлевает expiry. Если root изменился пока проверялся каталог, выбор может получить Conflict; активный ход продолжает использовать свой прежний snapshot. Нет refresh/retry.

Typed stale/CAS/доказанный PK collision дают Conflict. Настоящая SQLite busy либо PostgreSQL Serializable serialization failure остаётся исключением согласно общему UoW; все одновременные операции не обещают исключительно typed Conflict. Неизвестный commit/cleanup не объявляется успехом.

Runner сохраняет `TurnModelSettings` атомарно с BeginWithSettingsAsync: model/effort, threshold/reserve/input window. Это независимый version1 SettingsJson, не canonical metadata. Terminal updates сохраняют snapshot. Legacy custom writer без новых контрактов даёт Unsupported; primitive fallback нет.

## Opaque и provenance

`IContextContentInspector` классифицирует canonical формы отдельно от tokenizer model mapping. Например, выбор доступной gpt-6 для обычной текстовой истории допустим по каталогу; unsupported offline tokenizer затем сообщает отдельный unknown-count failure, не «несовместимость текста».

При другой либо неизвестной исходной selected model opaque/unknown input требует `IContextModelCompatibility.CheckAsync(call, source, target, ct)`. Без порта — Unsupported. Source содержит отдельно selected model, actual server model из соответствующего envelope (когда есть) и исходные canonical элементы. Server name не заменяет selected provenance; равенство server/target не является подтверждением совместимости. Отдельные output occurrences и residual input/tool outputs сохраняются; repeated call_id или одинаковые payload не дедуплицируются. Совместимость не подтверждает бюджет opaque.

AgentRunSession записывает SelectedModel compact через SaveWithModelAsync отдельно от полного envelope. **Standalone ContextCompactor использует primitive SaveAsync и не сохраняет selected provenance автоматически**. Historical/standalone окно с null selected требует порт даже при совпадающем server name. Null metadata не подменяются defaults, история не очищается.

## Статус и измерение

`CreatedAtUtc`/`ExpiresAtUtc` сохранены при создании. Fresh UTC на возврате даёт IsExpired при now >= expiry; метаданные читаются до физического удаления. Status сохраняет safe Token и saved selection даже при отказе каталога. Последний report генерации даёт ServerModel только при наличии своего `model`; прежнее или выбранное имя не подставляется.

ContentBytes — UTF-8 canonical items, полные generation/compact reports и tool journal. Owner/model selection/turn settings/provenance относятся к metadata, не входят в content count; физический overhead БД также не считается. SoftContentLimitReached при bytes >= настроенный порог — предупреждение, не удаление/запрет записи. Ранний warning percentage не вводится. Смена RetentionPeriod меняет ExpiresAtUtc существующего диалога в новом scope; null означает бессрочное хранение. Выбор модели и compact дату создания не меняют.

ContextSize — active window + непокрытая история, **без instructions/providers/new input/tools**. KnownTokens отдельно от nullable EstimatedInputTokens; unknown opaque не становится нулевым бюджетом. CompactionCount — номер последней принятой последовательной версии, включая repeated compact одного prefix. ThresholdReached nullable: неизвестная оценка не выдаётся за недостигнутый threshold. Pending known function call without output даёт явный context refusal. CanContinue означает возможность подготовки следующего обращения по доступному сохранённому состоянию, не гарантирует приём следующего полного request. Runner заново проверяет полный generation budget после providers/new input/tools и любого compact outcome.

## Пример read → select

Фрагмент выполняется внутри scope приложения, после регистрации options/catalog/tokenizer/persistence и AddAgentBridgeSettings. `call`/`ct` принадлежат приложению.

```csharp
AgentSettingsService service = scope.ServiceProvider.GetRequiredService<AgentSettingsService>();
ServiceResult<AgentSettingsSnapshot> read = await service.ReadAsync(call, ct);
if (!read.Success) return;
AgentSettingsSnapshot current = read.Data!;
ServiceResult<DialogModelSelection> changed = await service.SelectAsync(
    call, current.Token, current.SelectionVersion, "gpt-5", "high", ct);
if (!changed.Success) return; // UI показывает safe Error; библиотека не повторяет запись.
ServiceResult<DialogStatus> status = await service.GetStatusAsync(call, ct);
```

Для одного запроса: `new AgentRunRequest(call, input, selectedToolNames, limits, effort: "low")`. Model null берётся из сохранённого dialog selection. Возможность gpt-5/high/low проверяется actual динамическим каталогом, пример не обещает её для любого ключа.

## Схема и проверенные границы

AddDialogSettings создан штатным tooling SQLite/PostgreSQL после разрешения двух точных команд. Up добавляет отдельную DialogSettings и nullable SettingsJson/SelectedModel; historical rows получают null без backfill выдуманного выбора. Down удаляет именно эти новые settings/metadata; Up возвращает null, не восстанавливает потерянный выбор. Canonical history/reports, fixed dates и content count сохраняются. Existing migrations20/initial не регенерированы и не менялись вручную.

Адресные actual SQLite/PostgreSQL проверки21 подтверждают restart/CAS, running snapshot, rollback после SQL SaveChanges, guards/cascade, concurrency и Down/Up. HTTP — actual HttpClientLibrary + fake handler/local responses без сети. SQLite/PostgreSQL не подтверждают SQL Server/MySQL; fake HTTP не подтверждает live codex-lb/OpenAI. OpenSpec CLI отсутствует, validation не выполнена, change не архивирован.

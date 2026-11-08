# Usage, документы и изображения

`IModelAuxiliaryGateway` — optional независимый порт ядра. Конкретные адаптеры используют существующую `HttpClientLibrary`. [Требования](../../openspec/specs/agent-runtime/spec.md), [контекст sub2api](../../openspec/specs/agent-runtime/sub2api.md).

## Sub2Api: чтение usage

После logging и регистрации модуля БД приложения:

```csharp
using AgentBridge.Integration;

services.AddAgentBridgeSub2ApiConnector();
services.AddAgentBridge(configuration, httpClientFactory);
services.AddAgentBridgeSub2ApiAuxiliary();
```

Последний вызов явно подключает scoped `IModelAuxiliaryGateway`, сохраняет заранее зарегистрированный app override и идемпотентен. Он требует выбранного Sub2Api, не регистрирует CodexLb и не создаёт HTTP-клиент. Новых обязательных options нет: deadline чтения — существующий `Sub2Api:GenerationTimeout` (send и body). Без последнего вызова потребители продолжают работать без auxiliary.

После авторизации приложения получите выбранный `ModelAccess` через `IModelAccessResolver.ResolveAsync(owner, ct)` и передайте его без замены при отказе:

```csharp
IModelAuxiliaryGateway auxiliary = scope.ServiceProvider
    .GetRequiredService<IModelAuxiliaryGateway>();
ServiceResult<ModelAuxiliaryResult> usage = await auxiliary.ReadUsageAsync(selectedAccess, ct);
```

Полный компилируемый пример: [Sub2ApiAuxiliaryUsage.cs](../../tests/Delivery/Consumer/Sub2ApiAuxiliaryUsage.cs). Он source-linked в изолированный тестовый проект; методы примера при compile-check не исполняются. Новый DLL-комплект подготовлен штатным упаковщиком в `artifacts/delivery-tests/current`, 42 metadata-теста прошли. Внешняя бинарная компиляция нового примера и live usage ещё не проверены; [результаты](../../openspec/changes/sub2api-usage/tasks.md).

Выполняется один GET `/v1/usage`, с сохранением BaseAddress prefix и нормализацией одного конечного `/v1`. Bearer задаётся для каждого запроса из переданного доступа; JWT и upstream OAuth credentials не используются. Приложение владеет HttpClient и handlers, отключает retry/автоматические redirect. Ошибка не запускает смену ключа, fallback или повтор.

`usage.Data.Content` при успехе — самостоятельный JSON-снимок непосредственного ответа sub2api с `mode=quota_limited` или `unrestricted`. Не преобразовывайте его в формат codex-lb. Поля `quota`, `rate_limits`, `subscription`, `balance`, `remaining`, `usage`, `daily_usage`, `model_stats` могут отсутствовать; отсутствие не означает ноль или безлимит. `Unrestricted` означает отсутствие key-level ограничений, а не бесконечный баланс. Expired/quota_exhausted ключ может читать свой usage при прохождении базовой авторизации сервера. Данные относятся к sub2api и не доказывают ChatGPT quota или доступный context window. JSON содержит чувствительные данные и не предназначен для логирования.

HTTP 401/403 возвращаются как Unauthorized/Forbidden, остальные HTTP-отказы и некорректный JSON — Rejected без server text/body/URL. Caller cancellation сохраняет токен; локальный либо более короткий HttpClient timeout — Timeout. Cleanup failures остаются наблюдаемыми, borrowed HttpClient не освобождается gateway. `UploadFileAsync`, `GenerateImageAsync`, `EditImageAsync` возвращают Unsupported до HTTP factory.

## CodexLb: usage, документы и изображения

`CodexLbAuxiliaryGateway` подключается через `AddCodexLbAuxiliary(configuration.GetSection("CodexLb:Auxiliary"))` после `AddAgentBridge` либо `AddCodexLbResponses`; facade автоматически его не включает.

Раздел Auxiliary требует положительные TimeSpan `MetadataTimeout`, `FileCreateTimeout`, `FileUploadTimeout`, `FileFinalizeTimeout`, `ImageTimeout`. HttpClient, logger и handlers принадлежат приложению; retries, автоматические redirect и logging подписанного URL недопустимы.

| Метод | Поведение |
|---|---|
| `ReadUsageAsync(ModelAccess, ct)` | GET `/v1/usage`, immutable JSON object |
| `UploadFileAsync(ModelFileUpload, ModelAccess, ct)` | registration → signed PUT без Bearer → finalize success; возвращает подтверждённый file_id |
| `GenerateImageAsync(ModelImageRequest, ModelAccess, ct)` | JSON `/v1/images/generations`, stream=false |
| `EditImageAsync(ModelImageEditRequest, ModelAccess, ct)` | multipart `/v1/images/edits`, snapshot входных bytes до await |

Модель/размер/качество задаёт приложение; шлюз не угадывает server enum и не выполняет повтор. `ModelAuxiliaryResult.Content` клонирован; ожидаемые отказы возвращаются через ServiceResult без raw errors/headers/body. Caller cancellation распространяется; локальный deadline — Timeout. Ошибка cleanup не маскируется обычным отказом.

Для использования фото/file_id/opaque compact с offline BPE приложение может явно выбрать `AgentBridge:Compaction:BudgetPolicy = ServerValidation`. Default `RequireLocalEstimate` сохранён. При opt-in guard проверяет известную часть с резервом, оставляет полную оценку null и возвращает RequiresServerValidation=true. Превышение известной части и неподдержанный каталог не допускаются; окончательный приём неизвестной части решает сервер. Policy фиксируется AgentRunner на весь run.

Ручной `ContextCompactor.CompactAsync(..., cancellationToken, force: true)` разрешён ниже автоматического порога. Он сохраняет canonical окно после version-aware write и не удаляет историю/не продлевает срок. При opaque результате UnknownBudget сохраняется как честный итог оценки; следующий full guard применяется отдельно с той же явной policy.

Для standalone guard приложение передаёт policy явно в `new ContextBudgetGuard(counter, policy)`; прежний constructor с одним counter и DI tokenization guard остаются строгими. Старые бинарные сигнатуры CompactAsync, ContextBudgetGuard и ContextBudgetAssessment сохранены отдельными overloads.

Проверки: AuxiliaryGatewayTests, ContextBudgetGuardTests и ContextCompactorTests изолированы; Telegram consumer проверяет actual DLL с local HTTP/storage doubles. Live codex-lb, Telegram, SQL Server и Linux native runtime этой интеграцией не проверены.

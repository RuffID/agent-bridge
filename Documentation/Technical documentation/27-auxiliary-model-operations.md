# Usage, документы и изображения

`IModelAuxiliaryGateway` — optional независимый порт ядра. Конкретные адаптеры используют существующую `HttpClientLibrary`. [Требования](../../openspec/specs/agent-runtime/spec.md), [контекст sub2api](../../openspec/specs/agent-runtime/sub2api.md).

## Sub2Api: usage и изображения

После logging и регистрации модуля БД приложения:

```csharp
using AgentBridge.Integration;

services.AddAgentBridgeSub2ApiConnector();
services.AddAgentBridge(configuration, httpClientFactory);
services.AddAgentBridgeSub2ApiAuxiliary();
```

Последний вызов явно подключает scoped `IModelAuxiliaryGateway`, сохраняет заранее зарегистрированный app override и идемпотентен. Он требует выбранного Sub2Api, не регистрирует CodexLb и не создаёт HTTP-клиент. Новых обязательных options нет: deadline usage и изображений — существующий `Sub2Api:GenerationTimeout` (send и body). Без последнего вызова потребители продолжают работать без auxiliary.

После авторизации приложения получите выбранный `ModelAccess` через `IModelAccessResolver.ResolveAsync(owner, ct)` и передайте его без замены при отказе:

```csharp
IModelAuxiliaryGateway auxiliary = scope.ServiceProvider
    .GetRequiredService<IModelAuxiliaryGateway>();
ServiceResult<ModelAuxiliaryResult> usage = await auxiliary.ReadUsageAsync(selectedAccess, ct);
```

Полный компилируемый пример usage/generation/edits: [Sub2ApiAuxiliaryUsage.cs](../../tests/Delivery/Consumer/Sub2ApiAuxiliaryUsage.cs). Он source-linked в изолированный тестовый проект; методы примера при compile-check не исполняются. Комплект `artifacts/delivery-tests/current` подготовлен на этапе usage и не обновлён для изображений; source-linked compile не подтверждает новую DLL-поставку. [Результаты usage](../../openspec/changes/sub2api-usage/tasks.md), [изображения и границы приёмки](../../openspec/changes/sub2api-images/tasks.md).

Выполняется один GET `/v1/usage`, с сохранением BaseAddress prefix и нормализацией одного конечного `/v1`. Bearer задаётся для каждого запроса из переданного доступа; JWT и upstream OAuth credentials не используются. Приложение владеет HttpClient и handlers, отключает retry/автоматические redirect. Ошибка не запускает смену ключа, fallback или повтор.

`usage.Data.Content` при успехе — самостоятельный JSON-снимок непосредственного ответа sub2api с `mode=quota_limited` или `unrestricted`. Не преобразовывайте его в формат codex-lb. Поля `quota`, `rate_limits`, `subscription`, `balance`, `remaining`, `usage`, `daily_usage`, `model_stats` могут отсутствовать; отсутствие не означает ноль или безлимит. `Unrestricted` означает отсутствие key-level ограничений, а не бесконечный баланс. Expired/quota_exhausted ключ может читать свой usage при прохождении базовой авторизации сервера. Данные относятся к sub2api и не доказывают ChatGPT quota или доступный context window. JSON содержит чувствительные данные и не предназначен для логирования.

HTTP 401/403 возвращаются как Unauthorized/Forbidden, остальные HTTP-отказы и некорректный JSON — Rejected без server text/body/URL. Caller cancellation сохраняет токен; локальный либо более короткий HttpClient timeout — Timeout. Cleanup failures остаются наблюдаемыми, borrowed HttpClient не освобождается gateway. `UploadFileAsync` возвращает Unsupported до HTTP factory.

### Генерация и редактирование

`GenerateImageAsync(ModelImageRequest, selectedAccess, ct)` выполняет один JSON POST `/v1/images/generations`. `EditImageAsync(ModelImageEditRequest, selectedAccess, ct)` выполняет один multipart POST `/v1/images/edits` с файлами `image[]`; коллекция и bytes копируются до первого await. Отдельный UploadFileAsync для этого не нужен. JSON edits с `images[].image_url` — другой серверный контракт, здесь не используется.

```csharp
ServiceResult<ModelAuxiliaryResult> generated = await auxiliary.GenerateImageAsync(new()
{
    Model = selectedImageModel, Prompt = prompt, Count = 1, Size = "1024x1024",
    Quality = "high", Background = "transparent", OutputFormat = "png"
}, selectedAccess, ct);

ServiceResult<ModelAuxiliaryResult> edited = await auxiliary.EditImageAsync(new()
{
    Model = selectedImageModel, Prompt = editPrompt, Count = 1, Size = "auto",
    Quality = "auto", Background = "auto", OutputFormat = "webp",
    Images = [new ModelFileUpload { FileName = "input.png", MimeType = "image/png", Bytes = inputBytes }]
}, selectedAccess, ct);
```

`selectedImageModel`, prompt и inputBytes предоставляет приложение после своей авторизации. Выбранные Model/Count/Size/Quality/Background/OutputFormat сохраняются; текстовая Sub2Api.Model не подставляется. Передаётся stream=false. Нет повторов, смены модели/ключа или удаления параметров ради fallback. API-ключ относится к sub2api; OAuth, загрузку upstream и выбор аккаунта выполняет сервер.

Положительный результат — самостоятельный JSON в `Data.Content` с непустым `data` и непустым строковым `b64_json` либо `url` в каждом элементе. Дополнительные поля сохраняются. Адаптер проверяет JSON и наличие payload, но не декодирует изображение. Ведущие whitespace heartbeat допустимы. HTTP 200 с non-null `error`, malformed JSON/UTF-8, duplicate root/image fields, пустой `data` и отсутствующий payload дают Rejected. URL возвращается приложению без скачивания; API-ключ на него не отправляется. Не логируйте bytes, prompt, JSON, URL или credentials. Безопасный event 5201 сообщает только operation/outcome/error type после cleanup, без server text.

Endpoint не гарантирует доступ: важны платформа, разрешение группы, модель, размер, native options и доступные upstream-аккаунты. Явные параметры влияют на server routing; клиент не упрощает запрос после отказа. Внутренние server retries возможны. Async tasks, batches, masks и image streaming не включены. Обычные входные фотографии AgentRunner — отдельный сценарий с прежним provenance guard.

Проверки исходников: 374 isolated Sub2Api tests passed (134 новых image cases), build без warnings/errors. Проверены actual transport/public DI с локальными handlers/streams, без сети, host и БД. Live generation/edit, OpenSpec CLI и обновлённая DLL-поставка не проверялись; [точные команды и результаты](../../openspec/changes/sub2api-images/tasks.md).

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

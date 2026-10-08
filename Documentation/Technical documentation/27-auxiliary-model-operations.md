# Usage, документы и изображения

`IModelAuxiliaryGateway` — optional порт ядра. `CodexLbAuxiliaryGateway` реализует его через existing `HttpClientLibrary` FileVersion 0.0.0.5. Регистрация `AddCodexLbAuxiliary(configuration.GetSection("CodexLb:Auxiliary"))` выполняется после `AddAgentBridge` либо `AddCodexLbResponses`; facade автоматически её не включает.

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

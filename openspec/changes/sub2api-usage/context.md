# Контекст

Источники: sub2api backend/internal/server/routes/gateway.go, middleware/api_key_auth.go, handler/gateway_handler.go (Usage, buildUsageData, buildAPIKeyDailyUsage, usageQuotaLimited, usageUnrestricted), gateway_handler_usage_test.go и api_key_auth_test.go.

GET /v1/usage авторизуется gateway API-ключом. Пользовательский JWT и upstream OAuth не нужны. Expired/quota_exhausted не запрещают чтение собственного usage; проверки существования/статуса ключа, пользователя, группы и IP сохраняются. Статистика best-effort может отсутствовать. Unrestricted описывает отсутствие key-level лимитов, а не бесконечный кошелёк или upstream quota.

Подключение отдельно от bundle сохраняет прежний opt-in auxiliary API CodexLb и не расширяет требования конфигурации. Используется существующий конечный GenerationTimeout. Gateway получает отложенную HTTP factory: неподдержанные методы и невалидный доступ не создают HTTP-клиент. Пример результата: {"mode":"quota_limited","isValid":true,"status":"expired"}; отсутствующий remaining остаётся отсутствующим.

Ошибки проецируются по HTTP-статусу без серверного текста. Неожиданные и combined cleanup failures сохраняют существующий контракт HttpClientLibrary; их нельзя логировать как безопасные результаты. DI и fake handler tests не подтверждают доступность реального сервера или поставку DLL.

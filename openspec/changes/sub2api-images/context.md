# Контекст и проверка

Источники только для чтения: sub2api backend/internal/server/routes/gateway.go, handler/openai_images.go, service/openai_images.go, openai_images_responses.go, openai_images_direct.go, openai_images_json_keepalive.go и соответствующие tests; image_generation_intent.go, account.go и openai_account_scheduler.go определяют group/capability gates.

JSON generation передаёт model/prompt/n/size/quality/background/output_format/stream=false. Multipart edits передаёт те же значения и image[] с собственной копией файлов. JSON edits имеет отдельный images[].image_url контракт и здесь не используется. Например, выбор Count=2, Quality=high и OutputFormat=webp сохраняется даже при отказе сервера; повтор с другими параметрами не выполняется.

Явные model/size и native options влияют на server routing. Endpoint сам по себе не доказывает доступность: нужны подходящие платформа, группа, модель и upstream. API-ключ принадлежит sub2api; upstream OAuth, upload, account selection и внутренние retries остаются на сервере.

Heartbeat — ведущие JSON whitespace, не SSE. После него HTTP 200 может содержать error envelope. Нужны объект, непустой data и image payload в каждом элементе; malformed JSON/error/empty result означают отказ. Результат сохраняется JSON-снимком, URL автоматически не скачиваются. Prompt, bytes, URL, ключи и raw exceptions не пишутся в журнал.

Используются существующие optional DI, per-call ModelAccess и GenerationTimeout на send/body. Borrowed HttpClient принадлежит приложению; приложение отключает retry и redirect. Cleanup failures сохраняются по контракту HttpClientLibrary. Отмена caller приоритетнее deadline.

Usage завершён в изолированном объёме: [результат](../sub2api-usage/tasks.md). Его CLI/live-пункты не закрываются изображениями. Проверки новой задачи — actual transport/DI с fake handler/local streams, локальный restore/compile, без host/сети/БД. Текущая DLL-поставка до отдельной штатной упаковки не включает новые изменения.

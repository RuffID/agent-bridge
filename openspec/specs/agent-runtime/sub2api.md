# Коннектор sub2api: принятый объём и проверка

[Действующие требования](spec.md) · [Общий контекст](context.md) · [Завершённый план](../../../Documentation/Plans/sub2api-connector-2026-11-06-(completed)/main.md) · [Архив change](../../changes/archive/2026-11-08-add-sub2api-connector/proposal.md)

## Назначение и подключение

Приложение выбирает один connector bundle до вызова AddAgentBridge: sub2api либо codex-lb. Без явного выбора сохраняется codex-lb и прежний прикладной API. Коннектор использует ключ sub2api; OAuth upstream выбирает сервер. HTTP остаётся на HttpClientLibrary, хранение PostgreSQL/SQLite/SQL Server — на существующем persistence adapter и EFCoreLibrary. Регистрация DI не запускает внешние операции.

## Optional usage

После выбора Sub2Api и AddAgentBridge приложение отдельно вызывает `AddAgentBridgeSub2ApiAuxiliary()` из AgentBridge.Integration. Используется существующий `IModelAuxiliaryGateway.ReadUsageAsync(ModelAccess, ct)` и `GenerationTimeout`; нового раздела Auxiliary не нужно. Без opt-in порт не подключается. HTTP factory не вызывается при регистрации, разрешении gateway и Unsupported операциях.

GET `/v1/usage` использует выбранный gateway API-ключ, без JWT/upstream OAuth и без повторов или смены доступа. BaseAddress сохраняет prefix и допускает один конечный `/v1`. Ответ — непосредственный JSON `quota_limited`/`unrestricted`; `ModelAuxiliaryResult.Content` сохраняет optional-поля, вложенные значения и null. Например, `{"mode":"quota_limited","isValid":true,"status":"expired"}` не содержит remaining: это неизвестное значение, а не 0 или безлимит.

Unrestricted означает отсутствие key-level лимитов sub2api, при этом кошелёк или подписка могут иметь ограничения. Дополнительная статистика best-effort может отсутствовать. Expired/quota_exhausted ключ может читать свой usage при прохождении базовой авторизации sub2api. Сведения не доказывают upstream ChatGPT quota или доступное окно контекста и не используются generation budget guard. UploadFileAsync остаётся Unsupported до HTTP. [Подключение и пример](../../../Documentation/Technical%20documentation/27-auxiliary-model-operations.md), [usage change и ограничения приёмки](../../changes/sub2api-usage/tasks.md).

## Завершённые изображения

Тот же optional gateway поддерживает GenerateImageAsync (JSON POST `/v1/images/generations`) и EditImageAsync (multipart POST `/v1/images/edits`, файлы `image[]`). Коллекция и bytes фиксируются до await; отдельный upload не нужен. Модель изображений задаётся в ModelImageRequest, без подстановки текстовой Sub2Api.Model. Count/Size/Quality/Background/OutputFormat сохраняются, stream=false. Например, Count=2, Quality=high, OutputFormat=webp не заменяются значениями по умолчанию при server refusal.

Sub2api выполняет upstream OAuth/upload/account selection. Доступность зависит от платформы, group AllowImageGeneration, модели и capability/size routing аккаунтов. Явные model/size и native options влияют на маршрут; адаптер не удаляет их и не делает повторов. Внутренние server retries не запрещаются клиентским no-retry контрактом.

Ведущие whitespace heartbeat допускаются при чтении JSON. HTTP 200 с error, пустой data, malformed JSON/UTF-8 и отсутствие payload хотя бы у одного элемента дают отказ. Успешный ModelAuxiliaryResult сохраняет data[].b64_json или data[].url и дополнительные поля без скачивания URL. JSON/bytes/prompt/URL/ключи не логируются. Общий GenerationTimeout покрывает send/body, caller cancellation и cleanup сохраняют existing library contract. Приложение владеет HttpClient и отключает retry/redirect.

Объём не включает masks, streaming, async tasks, batches и входные фото AgentRunner; provenance guard сохранён. [Change и результаты проверок](../../changes/sub2api-images/tasks.md). Live-доступность и обновлённая DLL-поставка требуют отдельной приёмки.

## Полный контекст и неизвестный бюджет

Первый принятый объём sub2api использует HTTP JSON/SSE Responses с полным canonical контекстом без previous_response_id. По явной политике ServerValidationWithUnknownWindow каталог читается из existing /backend-api/codex/models без client_version; exact model/effort/API-доступность проверяются как объявления сервера. Входное окно остаётся null и RequiresServerValidation=true. Порог и локальная приблизительная оценка не заменяют неизвестный серверный лимит. Прежние policies сохраняют известное окно и ordinary /v1/models.

Например, после первого turn с console_probe новый процесс читает тот же DialogId и отправляет сохранённые user/output/function_call/function_call_output вместе с новым вопросом. Сохранённый tool result читается как данные; handler повторно не выполняется. Каждый model step проходит отдельный generation budget guard.

## Сохранность и ограничения

Automatic compact sub2api отключён; ручной/force compact возвращает Unsupported до HTTP и принятия окна. При превышении бюджета возвращается явный отказ с сохранением истории, без обрезания, повторной отправки, смены выбора или replay business tools. Длина диалога ограничена доступным бюджетом.

Безопасный режим полного контекста также закрывает обход через GenerateAsync: canonical items с type compaction_trigger, compaction или compaction_summary отклоняются до HTTP factory/callbacks, даже с родным provenance. Неоднозначные повторяющиеся type не обходят проверку; обычные слова в message не являются protocol signal. История и прежнее окно не изменяются, автоматического восстановления prefix и отправки только хвоста нет. После serialized restart действует тот же запрет. [Действующий safety change](../../changes/sub2api-compact-safety/proposal.md), [проверки и ограничения](../../changes/sub2api-compact-safety/tasks.md).

В текущем sub2api native remote compaction v2 — POST /v1/responses с stream:true и compaction_trigger последним в input. Handler сохраняет streaming и нормализует trigger в последнюю позицию. Legacy /v1/responses/compact — отдельный путь; его stream bridge/keepalive могут завершиться response.failed при HTTP 200. Ни один путь сейчас не подключён как compact в AgentBridge. T05 сохраняется: encrypted-content recovery может удалить compaction/compaction_summary перед успешной generation. Completed, echo модели и локальное сохранение окна не подтверждают сохранность covered prefix. Сервер не изменяется; best-effort и текстовая замена compact не включаются.

Canonical гарантия относится к отправке sub2api, полученному ответу и истории AgentBridge. Серверные normalization/recovery/retries допускаются; requested model echo не доказывает actual upstream identity. Локальный connector provenance хранится отдельно от canonical envelope/continuation и не отправляется upstream. Сохранённые отчёты используют один новый JSON-формат; чтение старых диалогов, автоматический backfill и version dispatch этого формата не поддерживаются. Независимый budget snapshot v1/v2 сохраняется.

T01 остаётся исследованием доказанного источника input budget. T05 и compact03.03.02–03.03.06 остаются отдельной отложенной работой: positive gate blocked, transport/response/активация не реализованы. Успех generation не доказывает пригодность compact-окна после server recovery. Новые server API/headers, pool revision и single-attempt profile не требуются. Исторический legacy-only план не задаёт протокол новой реализации; актуальное различие v2/legacy описано выше.

## Проверенные границы

На05.01 прошли1707 isolated tests и strict validation. На05.02 подтверждены fresh kits,23 external builds,42 Metadata/23 ABI/7 Windows binary cases; frozen baseline не изменён. Реальная PostgreSQL recovery-проверка04 содержит5 cases и учитывается отдельно.

На2026-11-08 live05.03 подтвердил JSON → новый процесс/DI → SSE,2 terminal turns, полный input, сохранённые отчёты/provenance и единственную console_probe пару без replay. Отправлены3 generation и2 manifest requests; все HTTP200,25 stream updates. Пользователь подтвердил тестовую БД, OAuth-группу и тег deployment latest; точный digest/числовая версия не проверялись. [Команды и безопасные hashes](../../../Documentation/Plans/sub2api-connector-2026-11-06-(completed)/05-acceptance/05.03-live-acceptance.md) сохраняют фактический результат без ключей, connection string, payload и reasoning.

Изолированные отказы/отмена/overflow не выдаются за специально воспроизведённые live-сбои. Linux execution, произвольный pool, server drain, биллинг и успешный sub2api compact этой приёмкой не подтверждены. Архив относится к согласованному объёму первой версии; отложенная работа не объявляется выполненной.

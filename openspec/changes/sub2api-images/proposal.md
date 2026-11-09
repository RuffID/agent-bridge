# Изображения через optional sub2api auxiliary

## Причина

Этап usage реализовал optional IModelAuxiliaryGateway и отдельную регистрацию sub2api. GenerateImageAsync и EditImageAsync пока возвращают Unsupported, хотя сервер предоставляет синхронные Images endpoints.

## Изменение

Расширить существующий gateway: JSON POST /v1/images/generations и multipart POST /v1/images/edits через HttpClientLibrary, с выбранными параметрами и проверкой завершённого JSON. Сохранить usage, selector isolation, CodexLb API, текстовый JSON/SSE и provenance guard AgentRunner.

## Границы

Только AgentBridge. Без image streaming, async tasks, batches, масок, новых параметров, клиентского OAuth/upload, скачивания URL и повторов. Исторические планы и соседние репозитории не изменяются. CLI, штатная DLL-упаковка и live-приёмка требуют отдельных разрешений.

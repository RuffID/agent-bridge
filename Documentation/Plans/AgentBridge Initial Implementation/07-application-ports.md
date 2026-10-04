# 07 — Независимые контракты прикладного слоя

Статус: **Реализован и принят; запрещённые проверки пропущены**. Зависимости: **06**. Этап 08 не начат.

## Дополнительная проверка 2026-10-04

Повторены **121 core / 61 CodexLb / 164 isolated persistence** tests, 0 failed/skipped. Реальные public storage ports этапа 10 дополнительно проверены на SQLite/PostgreSQL: полный output/envelope/continuation/errors всех lifecycle, новое DI-root чтение, guards и отсутствие восстановления старой жизни ID. Атомарность backing UoW проверена actual CAS и отказом после SQL save до commit. Model catalog проходит actual HttpClientLibrary с local handler. Независимость Application от инфраструктуры сохранена; будущие gateway/tokenizer/tool реализации не запускались. [39 integration cases и команды](README.md#дополнительный-интеграционный-запуск-2026-10-04); первоначальная история ниже не исправлялась.

## Цель

Определить узкие контракты сценариев агента без утечки инфраструктурных типов.

## Задачи

- [x] Определить контракты шлюза модели, поставщика контекста, обработчика инструментов и подсчёта токенов.
- [x] Определить минимальные операции чтения диалога и контракты изменения данных конкретных сценариев.
- [x] Определить результаты операций, различающие завершение, частичный результат, ошибку и отмену.
- [x] Исключить `DbContext`, `IQueryable`, EF expressions и типы HTTP-библиотеки из прикладных контрактов.
- [x] Документировать типы и методы интерфейсов на русском; в реализациях использовать `<inheritdoc/>`.

## Проверка и завершение

Сопоставить контракты со сценариями диалогов и инструментов. Этап завершён, когда реализации можно заменить изолированными заглушками без ссылок на инфраструктурные сборки.

Источник: [архитектура](<../../Technical documentation/01-architecture.md>).

## Фактический результат

В `Application/` добавлены четыре порта внешних функций и семь узких портов чтения/изменения/удаления диалогов. Read-only сценарии не получают UoW. Изменяющий порт выражает одну короткую атомарную операцию; сеть не передаётся внутрь транзакции. Будущий адаптер реализует её через общий сценарный scope/UoW этапа 10.

`ServiceResult`/`ServiceResult<T>` отделяют ожидаемый отказ без данных от успеха с ненулевыми данными. `ModelResponse` представляет lifecycle-отчёт Completed/Incomplete/Failed/Canceled с известным output; получение отчёта не означает Completed. Неожиданные исключения распространяются. `ModelAccess` фиксирует выбранный ключ на вызов, без публичного свойства секрета и без раскрытия в ToString.

Канонические элементы, полный envelope, метаданные продолжения, схемы/аргументы/выход инструментов владеют независимыми JSON-снимками. Неизвестные и opaque-поля сохраняются. `StoredModelStep` и принятый compact сохраняют полный отчёт отдельно от input-items. Снимки коллекций не меняются после изменения исходных списков. Подсчёт получает весь подготовленный запрос и разделяет известные токены/оценку полного бюджета/opaque-содержимое.

`DialogWriteToken` выражает сохраняемые incarnation/revision, а не private lifetime существующего Domain-объекта. Порты требуют атомарные existence/owner/expiry/version checks. Реализации, persistence и rehydration отсутствуют. Terminal-prefix metadata не разрешает фильтровать отдельные Responses items. Фактический API: [прикладные контракты](<../../Technical documentation/09-application-ports.md>).

## Проверки 2026-10-03

Перед сборкой прочитаны конкретные проекты, ancestor/build-файлы и NuGet-generated imports; проектных Exec/hooks, дополнительных build-файлов и lock-файлов нет. Restore не требовался. Все команды выполнены из `D:\Media\User\source\repos\agent-bridge`:

```powershell
dotnet build .\agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
git diff --check
```

Ядро и тестовый проект: **0 warnings / 0 errors**. Итог тестов: **93 passed / 0 failed / 0 skipped**, включая **27 новых** contract cases. Проверены lifecycle с непустым output, caller cancellation/timeout, передача ожидаемого отказа без данных, неожиданная ошибка callback, срок жизни JSON, неизменяемость коллекций, per-call secret snapshot, полный envelope/continuation и отдельное хранение шагов/compact. Fake storage демонстрирует выражение всех обязательных guards, сохранность данных при отказе, смену incarnation и чтение срока после expiry без права продолжения.

**Пропущено по указанию пользователя:** запуск приложения/hosting, реальный HTTP, любые БД/SQL (включая SQLite in-memory), Docker, migrations, native backup/restore, внешние процессы и произвольные скрипты. Runtime-проверки будущих адаптеров, tokenizer, UoW/concurrency и restart не выполнялись: реализаций в этапе 07 нет. Коммит этапа разрешён координатором после приёмки; точный hash локального коммита — в истории Git. Push не выполняется.

OpenSpec CLI отсутствует в PATH, поэтому CLI validation не выполнена и не объявляется успешной. Нормативные требования и сценарии проверены статически; tool installation и запуск сторонних скриптов не выполнялись.

Статическая проверка: 53 добавленных/изменённых файла — строгий UTF-8 без BOM, исходный LF сохранён, U+FFFD/mojibake/четырёх вопросительных знаков нет; 126 локальных Markdown targets существуют. `git diff --check` прошёл. Предупреждение Git autocrlf не означает изменение фактических EOL. До приёмки: ветка `master`, staged diff пустой; HEAD остался `309a3d63870c6cf536ee1a0d9eb34b6701e24825`. Соседние проекты и build/generated файлы не изменялись.

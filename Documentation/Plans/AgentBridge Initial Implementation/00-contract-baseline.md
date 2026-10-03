# 00 — Проверка исходных контрактов

Статус: **Завершён: статическая проверка принята** (2026-10-03). Зависимости: **Нет**.

## Цель

Установить точные локальные контракты будущей реализации до переноса кода из других проектов.

## Задачи

- [x] Перечитать актуальные маршруты Responses, compact и каталога моделей codex-lb, модели запросов и спецификации.
- [x] Повторно проверить базовые репозитории EFCoreLibrary, scoped-контекст и возможности обслуживания.
- [x] Повторно проверить обёртки JSON/потоковых ответов HttpClientLibrary и поведение логирования.
- [x] Определить, какие фрагменты TelegramCodexRelayBot соответствуют текущим контрактам, а какие требуют переработки.
- [x] Зафиксировать проверенные версии исходников или датированные сведения, не выполняя Git-команды без запроса.

## Результаты сверки

| Область | Проверенный результат и точные источники |
| --- | --- |
| Responses / compact / models | [Карта HTTP и wire-контрактов](<../../Technical documentation/03-http-and-codex-lb.md>): маршруты, формы JSON, function tools, reasoning, каталог, terminal SSE и непрозрачное compact-состояние |
| EFCoreLibrary | [Контракты CRUD и scope](<../../Technical documentation/02-efcorelibrary.md>): текущие IContext-репозитории, TContextKey, tracking, сохранение и условия общей scoped-сессии |
| HttpClientLibrary | [Фактические обёртки и ограничения](<../../Technical documentation/03-http-and-codex-lb.md>): статус/заголовки успешных ответов, владение потоком, cancellation, ошибки и текущее логирование |
| TelegramCodexRelayBot | [Матрица переиспользования](<../../Technical documentation/04-telegram-relay-reuse.md>): переносимые приёмы и несовместимые DTO, compact-память, роль System, token heuristic и SSE EOF |
| AquaByte-Ledger | [Обслуживание БД](<../../Technical documentation/06-database-maintenance.md>): проверен синхронный SQL Server flow; backup API EFCoreLibrary для SQLite/PostgreSQL отсутствует |

## Датированный срез исходников

Срез локальных файлов: **2026-10-03, Asia/Novosibirsk**. Пути проверены; применимые AGENTS.md прочитаны до анализа выбранных областей. Соседние проекты только читались, Git-операции в них не выполнялись. Дата и значения project-файлов не подтверждают deployed-версию или неизменность всего рабочего дерева соседнего проекта.

| Источник | Данные исходного проекта |
| --- | --- |
| AgentBridge | `D:\Media\User\source\repos\agent-bridge`; исходный HEAD `692488f`, до правок `git status --short` пустой |
| codex-lb | `D:\Media\User\source\repos\codex-lb`; [pyproject.toml](../../../../codex-lb/pyproject.toml): `1.25.0-beta.9`, Python `>=3.13` |
| EFCoreLibrary | `D:\Media\User\source\repos\work\EFCoreLibrary`; [csproj](../../../../work/EFCoreLibrary/EFCoreLibrary.csproj): `0.0.4`, `net10.0`, EF/DI `10.0.3` |
| HttpClientLibrary | `D:\Media\User\source\repos\work\HttpClientLibrary`; [csproj](../../../../work/HttpClientLibrary/HttpClientLibrary.csproj): FileVersion `0.0.0.4`, `net8.0;net10.0`, Logging `10.0.2` |
| TelegramCodexRelayBot | `D:\Media\User\source\repos\TelegramCodexRelayBot`; [Infrastructure.csproj](../../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Infrastructure/TelegramCodexRelayBot.Infrastructure.csproj): `net10.0`, EF SqlServer `10.0.8`, Telegram.Bot `22.10.0.1` |
| AquaByte-Ledger | `D:\Media\User\source\repos\work\AquaByte-Ledger\AquaByteLedger.Infrastructure\Services\DataBase`; [Infrastructure.csproj](../../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/AquaByteLedger.Infrastructure.csproj): `net10.0`, EF `10.0.11`, SqlClient `7.0.2` |

## Фактические проверки и ограничения

- Статически сопоставлены текущие реализации, интерфейсы, роуты и относящиеся к ним требования OpenSpec. Примеры из скилла и README не использованы вместо исходников. Новое поведение AgentBridge не реализовано; нормативные требования не изменены.
- Проверены локальные Markdown-ссылки изменённых документов, diff и кодировка UTF-8 без BOM с LF; нет U+FFFD, четырёх вопросительных знаков и обнаруженных повреждений кириллицы.
- Сборка, restore и автоматические тесты не запускались: изменены только документы, проверка этапа статическая. Бинарная совместимость и содержимое DLL не проверены.
- Приложения, hosting, реальные HTTP-запросы, БД/SQL, migrations и backup/restore: **Пропущено по указанию пользователя**. Работоспособность развёрнутого шлюза и конкретных моделей не заявляется.
- Известные gaps: безопасное HTTP-логирование — этап 04; общий backup/check/migrate API — этап 05. Эти этапы не начаты, соседние библиотеки не изменены.
- Дополнительно подтверждены недоступность error response headers и возможное усечение error JSON в HttpClientLibrary; наличие модели в каталоге не гарантирует compact, поскольку каталог включает model sources, а compact subscription-only. Решения о развитии контрактов не приняты; зависимая реализация не начата. Подробные доказательства приведены в HTTP-документе.
- Статическая проверка принята координатором. Коммит выполняется после приёмки; его hash указывается в итоговом отчёте. Этапы 01–25 не начаты.

## Проверка и завершение

Этап завершён, когда необходимые возможности сопоставлены с подтверждёнными контрактами, а устаревшие DTO бота не принимаются за источник истины. Новые спорные ограничения зависимостей обсуждаются с пользователем.

Источник: [анализ переиспользования](<../../Technical documentation/04-telegram-relay-reuse.md>).

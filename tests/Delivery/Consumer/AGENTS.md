# Внешние compile-only примеры

- Библиотека подключает один целый binary kit через AgentBridgeDeliveryRoot, без ProjectReference. Примеры копируются за пределы репозиториев и только компилируются.
- SimpleRegistration сохраняет default codex-lb; Sub2ApiRegistration выбирает sub2api до facade. Логгер, HttpClient, конфигурация, источник ключа и явная регистрация модуля БД принадлежат приложению.
- Sub2ApiAuxiliaryUsage явно подключает optional usage и читает независимый JSON с выбранным ModelAccess. Source-linked compile в Sub2Api tests не подтверждает external binary kit; для нового API нужна новая штатная поставка и проверка матрицы.
- Не добавлять credentials, запуск host/HTTP/SQL или автоматические maintenance operations. Не исполнять методы при compile-check.
- Новые примеры должны собираться во всей заявленной provider/RID матрице; SQL Server-only исходники исключаются csproj в остальных вариантах.

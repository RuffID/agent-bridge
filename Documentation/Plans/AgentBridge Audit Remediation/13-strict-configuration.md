# 13 — Явная конфигурация и fail-fast

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **не начат**. Зависимость: 00; 12 для MSSQL-specific settings. Область: Configuration, adapter options и контракты приложения.

## Цель и исходное состояние

Все настройки приходят из выбранного IConfiguration приложения; обязательные параметры не заменяются скрытыми defaults. Сейчас binding и ValidateOnStart уже есть, но MaxToolSteps, retention, compaction, effort и timeouts имеют начальные значения. Непереданный ключ может пройти validation с таким значением — это отличие от согласованного строгого режима.

## Работы

1. Составить точную таблицу: параметр, владелец, тип/диапазон, обязательность выбранного режима, источник и безопасный текст ошибки. Включить Agent/Retention/Compaction/CodexLb/Database и отдельно включаемое Maintenance.
2. Проверять существование выбранного раздела и обязательных ключей, пустые значения, malformed binding и диапазоны до первой операции. Нулевой допустимый reserve отличать от отсутствующего reserve; значение, равное прежнему default, не доказывает передачу настройки.
3. Не добавлять собственный config loader или appsettings lookup. Поддержать merged root/section, отдельный config и environment/secret providers с обычным приоритетом IConfiguration. В runtime services сохранять typed options.
4. Убрать скрытые defaults обязательных параметров либо применять явную проверку их передачи в configuration API; programmatic overload подчинить той же обязательности. Это изменение публичного поведения: назвать несовместимость и migration path, не выдавать её за refactoring.
5. Уточнить режим key source и источника Instructions: необязательное значение допустимо только по explicit выбранному контракту. Не заставлять приложения с индивидуальными keys хранить общий секрет и не вводить fallback при его ошибке.
6. Сохранить модельную проверку exact ID/effort/full budget в catalog/guard. Required options validation не запускает HTTP для проверки модели и не создаёт DB connection.
7. Расписание cleanup и batch limit описать как settings приложения: приложение читает их из своего IConfiguration, создаёт short scope и явно вызывает CleanupAsync. Retention и compact не становятся расписанием или background job.
8. Документировать logger ownership: file path/rotation/sinks задаются и валидируются приложением; AgentBridge получает ILogger. Нет второго logger/file writer в библиотеке. Для обязательного файлового режима отсутствие пути останавливает регистрацию логирования приложения; console/custom logger не требует фиктивный путь.

## Проверки

B: missing/empty section, каждый отсутствующий обязательный ключ, invalid types/ranges, разрешённый explicit zero, conditional keys/maintenance, несколько IConfiguration providers и overrides. При errors проверять безопасные paths/codes без secret values.

Проверить стандартный IStartupValidator и явную проверку для потребителя без host. BuildServiceProvider внутри registration, скрытый host, DB/HTTP/file logging и background jobs запрещены. Compile-check конкретных production/test проектов; logging fixture в памяти.

## Критерии завершения

Для каждого обязательного settings field отсутствие воспроизводимо даёт исключение до первого library сценария, а явно заданные значения работают. Нет незаметного default/fallback, секретов в errors, собственного файла config или logger. Несовместимость с прежним поведением отражена в docs/tests.

## Результаты

Изменения options/registration и проверки ещё не выполнялись.

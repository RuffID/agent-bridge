# 01 — Конфигурация, DI, ключи и выбор модели

Статус: **не начат**. Предпосылки: 00; схема ответственности приложения и библиотеки.

## Цель и вопросы

Проверить явность регистрации и границы прав, безопасные ошибки, настройку provider и приоритет модели/effort/ключа. Отделить defaults от бизнес-обязательств.

## Компоненты и зависимости

[Configuration](../../../Configuration), [CodexLbOptions](../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbOptions.cs), [CodexLbModelAccessResolver](../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelAccessResolver.cs), [CodexLbModelSettingsReader](../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelSettingsReader.cs), [ModelSelectionValidator](../../../Application/ModelSelectionValidator.cs), persistence/Configuration и [Diagnostics](../../../Diagnostics). Тесты ConfigurationTests, ModelSelectionTests, ModelCatalogTests, DiagnosticsTests.

## Способ проверки и границы

Проследить DI lifetimes и отсутствие I/O при регистрации; обязательные app factories, сохранение custom counter/inspector, отсутствие host. Проверить null, пустой/невалидный индивидуальный ключ, исключение источника, 401/403 и отсутствие shared fallback. Проверить exact ID/effort, неизвестный input budget, threshold+reserve ровно на границе и переполнение, отсутствие provider, неверные timeout/retention. Сверить override → сохранённый выбор → defaults и snapshot активного run. Проверить отсутствие секретов в settings/status, ошибках и диагностике, включая JsonStructure. Смена ключа между resolver и catalog не должна менять pinned access.

## Разрешения

A; существующие isolated DI/catalog/logger тесты — B, без живого HTTP. Источник ключей только синтетический. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Таблица регистраций и владельцев ресурсов, варианты выбора ключа/модели, safe-field перечень, ссылки на тесты отказов. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Все группы настроек и пути доступа имеют проверенный статический маршрут и результаты выбранных разрешённых проверок. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Доступность модели реальному аккаунту, полноту следующего контекста и runtime DI конкретного приложения.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.

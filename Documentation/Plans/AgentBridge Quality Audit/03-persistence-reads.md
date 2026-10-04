# 03 — Чтение и целостность сохраняемых данных

Статус: **не начат**. Предпосылки: 02; карта доменных инвариантов.

## Цель и вопросы

Проверить, что чтение не смешивает владельцев, поколения и состояния диалога, а сериализация не теряет протокольные данные.

## Компоненты и зависимости

[DialogReader](../../../adapters/AgentBridge.Persistence.EfCore/Reading/DialogReader.cs), ExpiredDialogReader, Repositories/*RecordQueries, Mapping и [AgentBridgeDbContext](../../../adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs). EFCoreLibrary: IContextGetItemByIdRepository, IContextGetItemByPredicateRepository. Тесты DialogReaderTests, BaseRepositoryAdapterTests, PersistencePayloadTests, PersistenceModelTests.

## Способ проверки и границы

Проследить parent-aware ключи turn/step, сортировку до limit, no-tracking и повторную проверку root/settings после детей. Негативные случаи: чужой owner до чтения детей, delete/recreate между чтениями, изменение revision/selection, orphan item, повреждённый JSON/FormatVersion, непринятый compact, отсутствующий parent. Проверить unknown/opaque fields, полные envelopes, отдельность Items и ModelSteps, nullable historical journal/settings/provenance. Сверить ContentBytes с сохраняемым UTF-8, исключив служебные метаданные и физический размер БД.

## Разрешения

A; metadata/serialization/fake-repository тесты — B; реальные query translation, collation и restart — C. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Карта шести таблиц, ключей/порядка, field round-trip и read interleavings; отдельные отметки fake/actual provider. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Каждая read-проекция, parent filter и формат хранения имеет результат или явно непроверенную provider-границу. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Транзакционный снимок чтения, атомарные записи и enforcement FK на основании одной EF metadata.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.

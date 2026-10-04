# 13 — Поставка DLL, XML и подключение

Статус: **не начат**. Предпосылки: 01, 05, 11–12; известны обязательные runtime и app-зависимости.

## Цель и вопросы

Проверить автономность комплекта и соответствие примеров публичному API, отделив компиляцию от загрузки.

## Компоненты и зависимости

[Delivery Build](../../../tests/Delivery/Build/AgentBridge.Delivery.csproj), [Consumer](../../../tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj), AgentBridge.Delivery.props, Metadata/DeliveryMetadataTests.cs, [руководство](<../../Technical documentation/25-usage-guide.md>), [поставка](<../../Technical documentation/24-dll-delivery.md>), csproj обоих adapters/migrations и библиотек.

## Способ проверки и границы

Сверить DLL/XML/native/embedded tokenizer closure, версии/SHA256 manifest и RID, selected migrations assembly, отсутствие ProjectReference/PackageReference в внешнем consumer. Проверить .NET10 и PostgreSQL toolchain как внешние требования; generated XML inheritdoc против документированного интерфейса. Негативные сценарии: отсутствующая dependency/migrations DLL, другой RID, смешанные версии, путь к исходникам, конфликт с пакетами приложения, отсутствие custom factories. Статически разобрать примеры создания/run/settings/tools/cleanup и их ошибки. Для B отдельно согласовать external compile каталог и оба provider kits; методы примера не исполнять.

## Разрешения

A; binary compile/PE/XML существующих проверок — B. Native load, DI runtime и приложение — C/D, не вывод из compile-check. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Manifest comparison, карта managed/native/XML, список обязанностей приложения, результаты обоих compile вариантов при разрешении. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Каждое обещание поставки имеет доказательство нужного уровня либо явную неподтверждённую платформу. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Runtime/native совместимость, конкретную IDE, Release/AOT/trimming/single-file или другие RID по win-x64 Debug.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.

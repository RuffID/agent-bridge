# 09 — Tokenizer, бюджет и сжатие

Статус: **не начат**. Предпосылки: 04, 08; подтверждена граница сохраняемого префикса.

## Цель и вопросы

Проверить полноту локального подсчёта, честную неизвестную оценку и принятие только допустимого сохранённого окна.

## Компоненты и зависимости

[ContextTokenCounter](../../../Tokenization/ContextTokenCounter.cs), ModelEncodingMap/OrdinaryTokenizerFactory, [ContextBudgetGuard](../../../Application/ContextBudgetGuard.cs), ContextCompactor, CompactRequestWriter/CompactJsonReader, IDialogContextWriter. Тесты ContextTokenCounterTests, ContextBudgetGuardTests, ContextCompactorTests и compact cases ResponsesJsonTests.

## Способ проверки и границы

Exact mapping против каталога, unknown/suffixed model, special-token literals, embedded словари; инструкции, providers, input, schemas и результаты вместе. Проверить threshold-1/равно/+1, estimate+reserve на границе, overflow, null estimate, opaque/multimodal/continuation, отсутствие подмены прошлым usage. Compact: только terminal prefix, не transient/new input; отдельная проекция controls без tools, собственный input guard, MaxPasses, пустой/растущий candidate, malformed response, unknown opaque estimate. Проверить save-before-activation, fresh UTC/stale token, сбой второго прохода после сохранённого первого. После любого compact outcome нужен отдельный полный guard.

## Разрешения

A; offline/fake gateway/writer проверки — B; реальная атомарность принятия окна — C; server count и live compact — D. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Таблица known/estimated/unknown, точные границы, состав compact и следующего generation, запись каждой принятой версии. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Подсчёт и каждый исход compact разобраны без обещания измерить opaque; различие fake/real writer явно записано. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Server billing/точный upstream count, сохранение каждой смысловой детали или возможность продолжить любое сжатое состояние.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.

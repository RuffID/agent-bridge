# 07 — Завершение gated test work при раннем выходе

[Навигатор](README.md). Статус: **не начат**. Зависимость: 00. Находка: **ABQA-010, подтверждённый пробел проверки, S4**.

## Цель и область

Гарантировать завершение и наблюдение начатых test tasks при assertion failure и timeout, сохраняя смысл исходного concurrency теста. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), evidence14. Это надёжность тестов, а не доказанный production hang/leak.

Две отдельные области:

- [UnitOfWorkScopeTests.SharedGateRejectsReadAndWriteWhileFirstOperationIsPending](../../../tests/AgentBridge.Persistence.EfCore.Tests/UnitOfWorkScopeTests.cs) в AgentBridge.
- [CoordinatorTests.Fatal_failure_poison_precedes_release_to_waiter](../../../../work/EFCoreLibrary/tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs) в EFCoreLibrary, по отдельному поручению.

## Работы

1. Перенести release/cancel и await всех начатых задач в гарантированный finally. Использовать идемпотентное завершение сигналов; не оставлять задачу, которая ждёт gate без cancellation.
2. Сохранить действующие assertions порядка и blocking/poison. Cleanup не должен превращать assertion failure в pass или навсегда задерживать его наблюдение.
3. Проверить обе задачи/entrant в EF coordinator case, а не только первую. Bounded ожидания должны завершаться также при ранней ошибке.
4. Создать локальный различающий сценарий раннего выхода вокруг того же test lifecycle без намеренно падающего постоянного xUnit case. Проверить наблюдение исходной ошибки и завершённость tasks после cleanup; не добавлять runtime API ради теста.
5. Новые gated fixtures этапов01–06 сразу выполнять с finally/await, не откладывая их корректность до этого этапа. Не переписывать весь test suite механически.

## Проверки

B: обычный путь двух исходных тестов, ранняя assertion-подобная ошибка, timeout/cancellation. Все tasks завершены и observed; исходная failure сохраняется. Подставные gates/ports не создают БД/процессов.

Compile-check затронутых двух test проектов отдельно; для AgentBridge persistence — `Dependency!=Database`, для цепочек EF — `GeneratePackageOnBuild=false`. Не суммировать повторные runs.

## Критерии завершения

Оба исходных места исправлены и адресно проверены. Если разрешена только одна библиотека, ABQA-010 закрывается лишь частично. Нет unobserved/pending work после раннего выхода; прежний смысл assertions сохранён.

## Результаты

Изменения тестов и проверки ещё не выполнялись.

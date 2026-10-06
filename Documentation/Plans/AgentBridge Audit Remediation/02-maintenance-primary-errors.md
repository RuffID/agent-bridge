# 02 — Сохранение primary error при maintenance cleanup

[Навигатор](README.md). Статус: **не начат**. Зависимость: 00; 01 для совместной регрессии coordinator. Находка: **ABQA-007, S3, подтверждена статически**.

## Цель и область

Сохранить первичную безопасную причину при вторичном отказе cleanup. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), контракт primary/secondary maintenance errors.

Владелец — EFCoreLibrary: DatabaseMaintenance/connection pin, BackupProcessRunner и SqliteBackupStepper. Работы в соседней библиотеке требуют отдельного поручения. Raw exception text, пути/подключения и секреты не становятся public errors или логами.

## Работы

1. Создать различающий тест: BackupNotConfirmed → ошибка CloseConnectionAsync → наружу CleanupUnconfirmed с сохранённым primary code. Использовать actual координацию, а не fake, заранее возвращающий нужный PrimaryError.
2. Проверить и исправить capture primary вокруг владения pin, сохраняя действующую классификацию и poison. Не подменять причину новым generic кодом.
3. Раздельно проверить process primary failure + unknown stop и native copy primary failure/OCE + Finish failure. Внести точечные исправления сохранения причины во всех связанных путях записи ABQA-007.
4. Для отмены и deadline сверить существующий контракт: успешный cleanup сохраняет прежнюю семантику; неподтверждённый cleanup не скрывается штатной отменой. Не придумывать новый error contract внутри реализации.
5. Проверить projection в AgentBridge на actual библиотечной границе. Если изолированная проверка process/native пути требует нового injectable boundary, сначала согласовать его в EFCoreLibrary; реальный процесс не запускать под видом B.

## Проверки

- B: primary-only, cleanup-only, primary+cleanup и successful path для каждого из трёх путей, включая caller/deadline контроли.
- Проверять safe code/PrimaryError, gate blocking, отсутствие начала DDL при неподтверждённом backup; primary не исчезает и secondary не считается успехом.
- Compile-check конкретных maintenance/provider/test проектов, которые действительно изменены; `GeneratePackageOnBuild=false`.
- Если native/process boundary нельзя проверить изолированно, записать незакрытую часть и передать её17. Переход основного сценария на MSSQL не закрывает исходные process/native проявления автоматически. Одного coordinator test недостаточно для закрытия всей записи.

## Критерии завершения

Комбинации ошибок подтверждены адресным evidence; все три проявления учтены. Primary/secondary доступны в действующем безопасном контракте. Интеграционные ограничения явны; код AgentBridge не обходит библиотеку.

## Результаты

Реализация и проверки ещё не выполнялись.

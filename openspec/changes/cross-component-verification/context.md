# Матрица проверки этапа23

Цель — закрыть границу между ранее проверенными компонентами, сохранив действующие контракты [main spec](../../specs/agent-runtime/spec.md). DB fixtures20–22 проверяли actual persistence с управляемым Application gateway; JSON/SSE fixtures проверяли actual transport без durable runner.

| Риск | Адресная проверка |
| --- | --- |
| JSON/SSE и repeated call_id теряются между transport/executor/storage | Actual runner выполняет две завершённые пары с одним ID, отдельные Started видны до handlers; restart читает полные steps/envelopes/outputs |
| Partial SSE случайно разрешает handler или следующий запрос | Сохранить незакрытый call, повторный turn запрещён, новый turn блокируется без generation и fake output; чтение каталога допустимо |
| Compact случайно включает provider/current turn, теряет opaque или разрешает generation | Настроенный threshold, actual BPE и compact transport, durable window prefix и provenance; unknown budget блокирует generation |
| Настройки/access меняются внутри active run | Реальный каталог, индивидуальный/null/error источник, saved selection и request effort, отдельная settings CAS версия, input_context_window |
| Отмена/cleanup/restart повторяют внешний эффект | Gated handler после real Started, cancellation и expiry equality/cleanup в отдельном scope; late outcome не возрождает строки |
| Успех действия теряется при неизвестном commit | Fault после настоящего commit outcomes, fresh root читает journal/output; existing TurnId не повторяется |
| Backup прежней схемы не доказывает новые поля на EFCoreLibrary0.0.5 | Два actual backup/restore SQLite/PostgreSQL после tool journal, settings snapshot и opaque compact; equality всей схемы/данных и public read/no-replay restored DB |

Например, два ответа модели с `call_id=same` требуют двух различных попыток и двух outputs. Новый root не переисполняет уже существующий TurnId независимо от того, получил ли первый caller подтверждение commit.

HTTP остаётся fake handler/local streams через actual HttpClientLibrary; сеть/hosting/working codex-lb запрещены пользователем. SQLite/PostgreSQL разрешены исходным поручением, повторное разрешение не требуется. Unknown commit не означает rollback. Искусственная потеря acknowledgement не подтверждает реальный сетевой разрыв. Полный historical maintenance39 не повторяется. Итоговые32 DB cases включают28 новых и4 существующих rollback/start acknowledgement;25 isolated проверяют metadata/DI/design-time. Подробности и ограничения — [отчёт23](<../../../Documentation/Plans/AgentBridge Initial Implementation/23-cross-component-verification.md>).

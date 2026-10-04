# Контекст этапа21

Назначение — позволить WPF/боту/сайту использовать правила библиотеки без собственных вычислений лимитов. Источник требований: [delta](specs/agent-runtime/spec.md) и [main](../../specs/agent-runtime/spec.md).

2026-10-04 пользователь прямо выбрал: «в БД AgentBridge, к конкретному диалогу». Defaults остаются у приложения; выбор сохраняется только для указанного owner/dialog. Например, dialog A выбирает model X и effort high, dialog B продолжает использовать свои настройки; override low одного обращения не записывает high→low.

Ключ не сохраняется в settings. Каталог проверяется actual IModelSettingsReader; HTTP только fake handler через HttpClientLibrary при проверках. Размер контекста разделяет known/full estimate; opaque не превращается в доказанный budget.

Новая схема проверяется на собственных тестовых SQLite/PostgreSQL. Наличие fake UoW не доказывает атомарность настоящей БД. OpenSpec CLI не найден; static review не является CLI validation, change не архивируется без проверки.

Пользователь подтвердил отдельную версию: активный ход продолжает работу. Поэтому DialogSettings не меняет root Revision/LastChangedAtUtc; BeginWithSettings атомарно записывает primitive snapshot. Например, ход A начинает high, UI сохраняет low, ход A завершает high с прежним token, следующий ход использует low. Повторный UI save старой settings version получает Conflict. Busy/serialization/commit uncertainty остаются исключениями драйвера, без скрытого retry.

Для непрозрачного состояния пользователь подтвердил отдельный порт приложения и Unsupported без подтверждения. Selected model и server model не приравниваются. Shape inspector работает независимо от model→BPE mapping, поэтому неизвестная текстовая модель каталога не считается opaque. Standalone compact не знает selected provenance; historical/standalone opaque требует подтверждения, даже если server имя совпадает с новым выбором. Output occurrences связываются с metadata соответствующих reports без дедупликации истории.

Status измеряет только сохранённый input, без transient providers/new input/tools; CanContinue не гарантирует бюджет полного следующего run. При отказе каталога/compatibility/оценки срок и байты доступны. Settings, pinned snapshots и provenance — metadata, исключённые из ContentBytes. Down/Up сохраняет content count и canonical историю, но теряет новые настройки/metadata и возвращает null.

Обе migrations созданы штатным EF tooling после отдельного разрешения пользователя. Actual DB tests проверяют restart, rollback, CAS/races, running snapshot и Down/Up; HTTP остаётся actual HttpClientLibrary с fake handler. Финальные команды, counts, первоначальные сбои, manifest и очистка ресурсов находятся в [отчёте21](<../../../Documentation/Plans/AgentBridge Initial Implementation/21-settings-and-dialog-status.md>). Этап21 принят координатором, локальный коммит ровно78 файлов manifest разрешён. Запрет add/commit до приёмки — историческая граница; этап22 не начат.

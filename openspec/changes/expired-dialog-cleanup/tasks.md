# Этап22

- [x] Реализовать public bounded cleanup/report и явное DI-подключение без автозапуска.
- [x] Проверить partial/cancel/unknown, fresh UTC, independent scopes, bound/no retry и cleanup failures изолированно.
- [x] Проверить actual SQLite/PostgreSQL equality/cascade/settings, partial rollback, stale/recreate и delete-vs-active-run.
- [x] Синхронизировать main spec/context, business/technical docs, README, AGENTS и план с фактическими API/evidence.
- [x] Проверить diff, UTF-8/EOL, ссылки и полный manifest; передать на приёмку без add/commit.

OpenSpec CLI не обнаружен через Get-Command. Не устанавливать; повторная проверка доступности и явный отчёт об отсутствии CLI validation обязательны. Не архивировать непроверенный change.

Фактическая static проверка: main/delta requirement совпадают, manifest30 соответствует modified/untracked, strict UTF-8 без BOM/LF и254 локальные ссылки проходят, diff --check/index проверены. CLI validation не выполнена. Evidence: [отчёт22](../../../Documentation/Plans/AgentBridge%20Initial%20Implementation/22-expired-dialog-cleanup.md).

Этап22 принят координатором; local commit ровно30 файлов manifest разрешён отдельным поручением после независимой проверки TRX79/61/16 и отсутствия ресурсов22. До приёмки add/commit не выполнялись. Change не архивировать без CLI validation.

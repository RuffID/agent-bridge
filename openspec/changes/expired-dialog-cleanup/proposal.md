# Очистка истёкших диалогов — этап22

Предоставить приложению явную ограниченную оркестрацию existing `IExpiredDialogReader` и `IExpiredDialogDeletion`. Один вызов читает один пакет, удаляет последовательно в отдельных коротких scopes и сохраняет честный отчёт при частичном отказе или отмене.

Расписание остаётся у приложения. Существующие guards, base CRUD EFCoreLibrary, cascade включая DialogSettings, schema/migrations и активный pinned snapshot не меняются. Мягкий bytes threshold не является условием очистки.

Нормативные требования: [delta](specs/agent-runtime/spec.md). Контекст: [context](context.md). Проверки: [tasks](tasks.md).

# Токенизация подготовленного запроса

Этап17 реализует IContextTokenCounter полноценным offline .NET tokenizer и добавляет явную проверку input budget подготовленного ModelRequest. Подсчёт последнего сообщения не заменяет инструкции, всю композицию, function schemas/calls/results и входные параметры.

Область: ядро, изолированные тесты, документация. Compact18, tools19, orchestration20, persistence settings21, HTTP/DB, соседние библиотеки и packaging не изменяются. Нормативная область: [agent-runtime](../../specs/agent-runtime/spec.md), объяснение: [context](context.md).

# agent-bridge

AgentBridge — C#-библиотека ИИ-агентов для .NET10-приложений, подключаемая обычными DLL. Приложение → AgentBridge → [codex-lb](https://github.com/Soju06/codex-lb) → модель. Библиотека собирает разрешённый контекст, выполняет инструменты приложения и сохраняет диалог в SQLite или PostgreSQL.

## Подключение

1. Выберите полный локальный комплект `artifacts/delivery/stage24-win-x64/Sqlite` либо `PostgreSql`: .NET10/win-x64/Debug,39 managed DLL,35 XML и native SQLite. Перенесите комплект целиком; [состав и внешние требования](<Documentation/Technical documentation/24-dll-delivery.md>).
2. Импортируйте `AgentBridge.Delivery.props` в .NET10 SDK-style проект. [Проверенный бинарный consumer](tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj) не содержит ProjectReference/PackageReference:

```xml
<PropertyGroup>
  <AgentBridgeDeliveryRoot>C:\MyApplication\vendor\AgentBridge\Sqlite</AgentBridgeDeliveryRoot>
</PropertyGroup>
<Import Project="$(AgentBridgeDeliveryRoot)/AgentBridge.Delivery.props" />
```

3. Настройте options ядра, codex-lb и выбранного provider; ключи/подключения храните в secret configuration приложения. [UsageRegistration](tests/Delivery/Consumer/UsageRegistration.cs) явно регистрирует scoped ключи, ordered `ContextBuilder` providers, tools, persistence, tokenizer, runner, settings и cleanup. Фабрики приложения и logging provider нужно предоставить самостоятельно.
4. Отдельно подготовьте БД через [явный maintenance API](<Documentation/Technical documentation/06-database-maintenance.md#подключение-agentbridge-этапа-12>) с SingleInitializer/backup options. DI-регистрация не создаёт БД и не запускает операции.

## Использование

Проверенные C# методы находятся в [UsageFlow](tests/Delivery/Consumer/UsageFlow.cs); последовательность и обработка результатов — в [руководстве](<Documentation/Technical documentation/25-usage-guide.md>).

1. Авторизуйте owner и agent, создайте новый DialogId через `CreateAsync`; expiry фиксируется от создания по `DialogRetentionOptions`.
2. Получите динамический каталог через `ReadModelsAsync`; `ReadSettingsAsync` → `SelectAsync` сохраняет exact model/effort с обеими исходными версиями. Override запроса имеет приоритет над saved выбором и defaults, не сохраняется. Individual key приоритетен; shared используется только при null, без fallback при ошибке.
3. Создайте новый `ApplicationCallContext`/TurnId и вызовите `RunAsync` только с новым input. Callback null выбирает JSON, non-null SSE. Проверяйте итоговый `Status` и `TerminalSaved`: stream delta и `LastResponse` не подтверждают сохранение. Existing turn/Unknown tool не разрешают replay.
4. Инструменты регистрируйте через `AddAgentBridgeTool<THandler,TValidator>`; [read-only пример](tests/Delivery/Consumer/AccountSummaryTool.cs) показывает полную простую schema и порт бизнес-данных/прав приложения. Selected names не заменяют авторизацию.
5. `ReadStatusAsync` даёт expiry, bytes, selected/server model и nullable оценку контекста. `CleanupAsync` обрабатывает один bounded пакет; limit/расписание у приложения. Partial/Canceled/Unknown не считаются успехом.

Срок7 дней, soft bytes10 МиБ, compact threshold32 000, reserve4096 и3 passes — переопределяемые начальные значения. Soft bytes не вызывает удаление, compact не продлевает expiry; неизвестный полный token budget не подменяется known count. [Конфигурация и ограничения](<Documentation/Technical documentation/25-usage-guide.md#2-настроить-options-и-зависимости-приложения>).

Примеры скомпилированы с обоими комплектами вне репозитория; методы не исполнялись. Runtime/native загрузка, конкретная IDE и live upstream не подтверждены. OpenSpec CLI отсутствует, validation не выполнена, changes не архивированы. Реализация первоначального плана00–25 завершена в документированных границах; этап25 принят координатором. Full hash итоговой локальной фиксации сообщается отдельно и доступен в Git history. [Отчёт25 и evidence всех этапов](<Documentation/Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>).

[Документация](Documentation/README.md) · [Нормативный OpenSpec](openspec/specs/agent-runtime/spec.md) · [План](<Documentation/Plans/AgentBridge Initial Implementation/README.md>) · [Решение для работы с исходниками](agent-bridge.slnx)

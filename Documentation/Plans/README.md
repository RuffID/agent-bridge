# Планы AgentBridge

Содержимое планов пишется на русском, имена файлов и папок — на английском. Планы разделены на небольшие Markdown-этапы. Поведение продукта определяется [бизнес-логикой](<../Business logic/README.md>) и [требованиями OpenSpec](../../openspec/specs/agent-runtime/spec.md).

| План | Статус | Содержание |
| --- | --- | --- |
| [Первоначальная реализация AgentBridge](<AgentBridge Initial Implementation/README.md>) | Реализация 00–25 завершена в документированных границах; итоговый локальный commit c8da604. OpenSpec validation не выполнена, changes не архивированы | Исторические этапы и доказательства; отчёты не переписываются |
| [Аудит качества AgentBridge](<AgentBridge Quality Audit/README.md>) | Статический проход 00–15 завершён с ограничениями; общий аудит A/B/C/D частичный | Результаты исследования без исправлений; [реестр ABQA-001–010](<AgentBridge Quality Audit/Findings.md>) |
| [Исправление проблем аудита AgentBridge](<AgentBridge Audit Remediation/README.md>) | План подготовлен; этапы 00–20 не начаты; Q-003–005 согласованы | Исправления, MSSQL, strict IConfiguration, единая регистрация, win-x64/linux-x64/linux-arm64 и проверки; [решения](<AgentBridge Audit Remediation/Decisions.md>) |

Создание плана не разрешает начинать реализацию, менять соседние библиотеки, применять миграции, запускать приложение или подключаться к БД.

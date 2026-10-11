# Планы AgentBridge

Содержимое планов пишется на русском, имена файлов и папок — на английском. Планы разделены на небольшие Markdown-этапы. Поведение продукта определяется [бизнес-логикой](<../Business logic/README.md>) и [требованиями OpenSpec](../../openspec/specs/agent-runtime/spec.md).

| План | Статус | Содержание |
| --- | --- | --- |
| [Каталог диалогов и recovery для Ledger 01A](<Assistant Dialog Capabilities/README.md>) | Возобновлено 11.10.2026; библиотечная source реализация проверяется отдельно от SQL/runtime и Ledger acceptance | [API](<../Technical documentation/30-dialog-catalog-and-recovery.md>) и [Evidence](<Assistant Dialog Capabilities/Evidence.md>); checkpoint паузы сохранён как историческое состояние |
| [Первоначальная реализация AgentBridge](<AgentBridge Initial Implementation/README.md>) | Реализация 00–25 завершена в документированных границах; итоговый локальный commit c8da604. OpenSpec validation не выполнена, changes не архивированы | Исторические этапы и доказательства; отчёты не переписываются |
| [Аудит качества AgentBridge](<AgentBridge Quality Audit/README.md>) | Статический проход 00–15 завершён с ограничениями; общий аудит A/B/C/D частичный | Результаты исследования без исправлений; [реестр ABQA-001–010](<AgentBridge Quality Audit/Findings.md>) |
| [Исправление проблем аудита AgentBridge](<AgentBridge Audit Remediation/README.md>) | 00/08 приняты в A;01–07/10–15 в A/B;09 в A workflow/B CLI;16 в B;17 подготовка A/B, C отложен;18 Windows часть;19 подготовка A;20 итог принят в частичной границе | MSSQL, strict IConfiguration, facade и пять kits реализованы/локально проверены. [Частичный итог20](<AgentBridge Audit Remediation/20-final-acceptance.md#результаты>) сохраняет обязательные provider/Linux/live/crash остатки17–19; [решения](<AgentBridge Audit Remediation/Decisions.md>) |

Создание плана само по себе не разрешает реализацию. На 2026-10-06 отдельное прямое поручение пользователя разрешает работы00–20 и необходимые адресные изменения в указанных репозиториях; границы — в [плане исправлений](<AgentBridge Audit Remediation/README.md#границы-исполнения>). Приложение, реальные БД/SQL, migrations tooling и внешние интеграции этим поручением не разрешены.

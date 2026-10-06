# OpenSpec workflow AgentBridge

[Действующая main spec](specs/agent-runtime/spec.md) · [Документация](../Documentation/README.md) · [Результаты09](<../Documentation/Plans/AgentBridge Audit Remediation/09-openspec-reconciliation.md>).

## Действующие требования и история

По принятому Q-003 действующий контракт находится в main spec. Исходные18 change-папок сохраняют историю и границы своих этапов. Структурное разделение длинных требований09 сохраняет каждое исходное нормативное предложение и прежние сценарии; [карта положений](<../Documentation/Plans/AgentBridge Audit Remediation/09-openspec-clause-map.json>) показывает перенос по новым блокам. Промежуточные ограничения JSON14/SSE15 не отменяют compact18. Старый delta нельзя повторно применять как новый продуктовый контракт. Папки, proposals, contexts и tasks не переименовываются и не архивируются автоматически.

Служебные заголовки OpenSpec: `## Purpose`, `## Requirements`, `### Requirement:`, `#### Scenario:`. Названия и содержание требований остаются на русском. Delta использует `## ADDED/MODIFIED/REMOVED/RENAMED Requirements`.

## Карта compact и названий

| Источник | Историческое содержание | Действующий контракт |
| --- | --- | --- |
| `responses-json-adapter`, «Безопасные ошибки и ограниченный вызов JSON» | Streaming callback и compact Unsupported в JSON14 | Main «Безопасные ошибки и ограниченный JSON вызов»: отдельный compact-контракт; SSE выбирается callback |
| `responses-json-adapter`, «Продолжение только своего вызова» | Историческое имя continuation requirement | Main «Продолжение только своего JSON вызова»; binding и отсутствие fallback сохраняются |
| `responses-sse-adapter`, MODIFIED «Безопасные ошибки и ограниченный JSON вызов» | Compact Unsupported на checkpoint15 | Main ссылается на «Отдельный JSON compact transport» |
| `context-compaction`, четыре исходных ADDED | Compact transport, ошибки/отмена, сохраняемый префикс, ограниченное принятие; длинные blocks разделены09 | Все четыре исходных правила и их выделенные обязанности присутствуют в main; окно сохраняется отдельно от полной истории |
| Main «Ошибки освобождения HTTP» и выделенные cleanup правила09 | Добавлено remediation06 после original18 | Primary/cleanup contract действует для JSON/SSE/compact; нормативные положения original18 сохранены |

## Validation и завершение changes

Сначала сверить текущие папки с original18 в Results09; новые changes учитывать отдельно. CLI в PATH или версия package.json не заменяют фактический запуск. Version/help, strict validation, повтор после правки и любые mutation-команды требуют точного разрешения согласно root AGENTS.

Постоянное разрешение пользователя (userMessage `01a10fa0-7b81-70e1-a47e-5fc67440131d`, «разрешаю всегда прогон этих команд») действует на exact19 strict validation commands из Results/evidence09: main agent-runtime и перечисленные original18, тот же wrapper `D:/Media/User/AppData/npm/openspec.cmd`, cwd `D:/Media/User/source/repos/agent-bridge`, flags `--type spec|change --strict --json --no-interactive`. Повтор именно этих команд не требует нового разрешения. Оно не распространяется на version/help, новые changes, installation или любые CLI mutations, включая sync/rename/archive.

На 2026-10-06 фактически проверен CLI1.14.1. Help подтвердил `validate <item> --type spec|change --strict --json --no-interactive`. [Evidence09](<../Documentation/Plans/AgentBridge Audit Remediation/09-openspec-cli-evidence.json>) сохраняет before/grammar-after и отдельный final после структурных правок: main и все18 changes passed,19 Exit0, ERROR0/WARNING0. Два INFO о collision при archive сохранены отдельно. Этап09 принят в A workflow/B CLI: ABQA-003 закрыт в CLI boundary, ABQA-004 принят по Q-003 workflow. Успешный validate не доказывает archive readiness или runtime compact.

## Подготовка последующего завершения

Семантика ниже проверена чтением установленного пакета1.14.1 (`dist/core/specs-apply.js`, `dist/core/archive.js`), без исполнения sync/archive/rename. Help не содержит отдельной top-level sync или rename команды. RENAMED — операция delta над заголовком requirement, а не переименование change-папки.

Delta применяется в порядке RENAMED → REMOVED → MODIFIED → ADDED. RENAMED меняет имя, сохраняя содержимое: source отсутствует при existing target — no-op; оба имени одновременно существуют — отказ. MODIFIED заменяет полный exact-name блок и проверяет потерю сценариев; идентичный блок не переписывается. ADDED с existing идентичным блоком — no-op, с отличающимся — collision. Поэтому rename сам по себе не устраняет старый запрет compact, а повторное MODIFIED SSE может вернуть его. Наличие одинаковых сценариев не защищает изменённый MUST от такого возврата.

Archive по умолчанию готовит обновление main из deltas; ветка `skipSpecs` пропускает обновление. Это статическая информация о потенциальном способе сохранить main, не готовая разрешённая команда и не evidence успешного archive. Final validation сообщает collision для `initial-provider-migrations` и `responses-json-adapter`; отсутствие INFO у SSE не означает безопасность повторного применения его MODIFIED.

До отдельного поручения оставить original18 на месте. Перед будущим завершением отдельно проверить актуальную версию/help, complete evidence задач, точные commands/manifest и сохранность main. Исторические уже учтённые deltas завершать с сохранением main и истории по Q-003; любые нужные sync/rename/skip-specs операции сначала представить с точной командой и последствиями. Не создавать второй stale requirement и не возвращать Unsupported compact. Strict failure не обходить отключением strict/validation. Archive readiness и само archive остаются отдельной работой.

# Полный ход агента — этап20

Объединить existing ContextBuilder/guard/compactor/gateway/tool executor и короткие write ports в public AgentRunner. Добавить явный durable журнал попыток в ModelSteps, не изменяя canonical payload, envelope или continuation. Исторический null означает отсутствие журнала.

Только этап20; UI/settings use cases21, cleanup22 и следующие этапы не входят. Новые provider migrations создаются штатным tooling после согласования пользователя; existing migrations не регенерируются.

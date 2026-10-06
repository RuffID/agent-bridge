# 19 — Внешние контракты и recovery после аварии

[Навигатор](README.md) · [Решения](Decisions.md). Статус: **не начат**. Зависимости: 03,04,06,10–18 в применимой части; оставшиеся ресурсы **Q-002**, отдельные разрешения C/D. Контракты Q-004/005 уже согласованы; implementation evidence ещё требуется.

## Цель и область

Получить evidence там, где fake HTTP/new DI root/synthetic acknowledgement не отвечают на вопрос о deployment. Источник — [итог15 аудита](<../AgentBridge Quality Audit/15-final-reconciliation.md>) и [открытые вопросы](<../AgentBridge Quality Audit/OpenQuestions.md>).

Работа относится только к выделенным app/process/endpoint и test данным. Не запускать production, Telegram, реальные бизнес-действия или чужие ресурсы. Если собственное средство проверки отсутствует, его создание требует отдельного поручения.

## Подготовка

Endpoint пользователь предоставит позже; сейчас внешнее подключение отложено. Перед запуском зафиксировать deployed codex-lb commit/exact model IDs, аккаунт/ключевую политику, synthetic данные, небольшой конкретный предел расходов, разрешённые запросы, crash/disconnect точки, test processes/MSSQL storage и cleanup. Secrets/raw user payload не писать в отчёт. Для каждого исполняемого файла/команды получить требуемое разрешение; не придумывать готовый запуск до определения ресурсов.

## Работы: внешний transport и compact

1. Проверить live catalog/JSON/SSE/compact в согласованной версии, canonical controls/continuation и safe error metadata. Invalid individual key не должен незаметно переключаться на shared key.
2. Проверить отмену до данных, partial/terminal response и explicit Failed priority после04; throwing cleanup из06 — только если имеется согласованное наблюдаемое средство. Фактическую сетевую утечку не выводить из local counters без релевантного evidence.
3. Проверить pre-HTTP отказ malformed/duplicate known nested controls10 и FIFO repeated-ID association11 на конечном compact payload, включая protected/fitting и distinct args/results. Unknown nested fields сохраняются; неопределимая связь отклоняет сжатие без потери последнего окна. Не заменять deployed результат static helper trace.
4. Проверить full-request guard после compact/tools и UnknownBudget при opaque. Local KnownTokens не объявлять server billing count или полной оценкой скрытого состояния.

## Работы: recovery и no-replay

1. С synthetic handler/счётчиком согласовать остановки до/после durable Started, после test action до outcomes и после terminal save. Не использовать внешний необратимый бизнес-эффект ради доказательства.
2. Новый процесс должен читать actual journal через текущий EF adapter и не запускать неизвестную/незавершённую попытку повторно. Повторяемость показаний счётчика и persisted identity фиксируются явно.
3. Согласованный реальный disconnect/потерю acknowledgement отличать от test exception после настоящего commit. Проверить token/step persistence, blocked session и honest incomplete/unknown/canceled outcome после03.
4. Проверить owner/app authorization boundary только на выделенных users/data. Сохранить fixed expiry и независимый dialog selection snapshot.

## Проверки и критерии завершения

Есть C/D evidence для каждого включённого сценария, current versions, команды/exit codes и подтверждённая очистка. Fixtures, synthetic действия и actual process/network fault обозначены отдельно. No-replay не объявляется external exactly-once.

Недоступная fault injection, endpoint или unresolved environment Q-002 блокируют только соответствующий вывод. Незапущенный случай остаётся открытым; успешный JSON запрос не закрывает SSE/compact/crash матрицу целиком.

## Результаты

Создание средств проверки и C/D запуски ещё не выполнялись.

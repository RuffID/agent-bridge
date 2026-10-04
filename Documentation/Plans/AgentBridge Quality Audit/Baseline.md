# Исходное состояние подготовки

Дата: 2026-10-04, Asia/Novosibirsk. Это подготовительная инвентаризация, не отчёт выполненного этапа 00.

## Текущие исходники

| Репозиторий | Локальный HEAD | Начальное состояние |
| --- | --- | --- |
| AgentBridge | c8da6046c4a4bebdcd006a960e0608b022e6121a | Чистое дерево |
| EFCoreLibrary | 3a8a53187af3c5df049770dfd6727b5065159d1f | Чистое дерево; версия 0.0.5 |
| HttpClientLibrary | 6d0528d940d1d8494c722c22464051dd961d6bf7 | Чистое дерево; FileVersion 0.0.0.5 |
| codex-lb | f8ffbac2099a113fba54dfd8d77774f5bca80ffa | Неотслеживаемая .vs/; не изменялась |

Проверены локальные status/log/show/rev-parse, карта исходников и публичные контракты, применимые инструкции. Соседние проекты не изменялись.

Обязательные пути доступны и связаны ProjectReference адаптеров:

- D:/Media/User/source/repos/work/EFCoreLibrary — базовые CRUD, IAppDbContext, IUnitOfWorkContext, maintenance SQLite/PostgreSQL.
- D:/Media/User/source/repos/work/HttpClientLibrary — IHttpApiClient, HttpStreamResponseResult, HTTP diagnostics/error contracts.
- D:/Media/User/source/repos/codex-lb — выборочная статическая сверка app/modules/proxy/api.py и schemas.py: /v1/models, /v1/responses/compact, input_context_window.
- D:/Media/User/source/repos/TelegramCodexRelayBot и D:/Media/User/source/repos/work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase доступны, но их код в подготовке заново не анализировался: для карты аудита достаточно текущих контрактов AgentBridge и обязательных библиотек. Перед будущей сверкой прочитать их ancestor/root/local AGENTS. Наличие пути не доказывает совместимость.

## Реализация и прежние проверки

Git содержит последовательность реализации 00–25. Последний commit c8da604 — docs: complete AgentBridge usage guide and implementation plan, 36 файлов; предыдущие 495e2b8 — DLL, 7e9d533 — межкомпонентные проверки. [Карта evidence00–25](<../AgentBridge Initial Implementation/25-usage-guide-and-closure.md>) и [checkpoint](<../AgentBridge Initial Implementation/README.md>) подтверждают закрытие первоначальной реализации в её документированных границах. Старые STOP внутри датированных отчётов не являются текущим запретом на подготовку нового плана.

| Историческое доказательство | Применимость и предел |
| --- | --- |
| Дополнительные DB проверки 00–13 | Использовали EFCoreLibrary 0.0.4; не подтверждают всю текущую 0.0.5 |
| Отдельная совместимость 0.0.5 | Compile/isolated; не новый общий DB прогон |
| Этапы 20–23 | Адресные SQLite/PostgreSQL проверки текущих сценариев; HTTP подставной |
| Этап 23 | Отчёт: 32 DB / 25 isolated; синтетические отказы acknowledgement |
| Этапы 24–25 | Бинарная компиляция SQLite/PostgreSQL kits и 8 PE/XML cases; повтор одного набора, без исполнения методов |
| OpenSpec | По отчётам CLI не найден, validation не выполнена; доступность CLI сейчас не проверялась |

В openspec/changes остаются 18 неархивированных change-папок: agent-turn-orchestration, application-tools, base-repository-adapters, context-compaction, context-composition, cross-component-verification, database-startup-and-backup, dialog-settings-and-status, dll-delivery, expired-dialog-cleanup, initial-provider-migrations, model-catalog-and-keys, model-tokenizer, persistence-models, responses-json-adapter, responses-sse-adapter, scenario-unit-of-work, usage-guide-and-closure. Девять tasks.md имеют явный незакрытый пункт CLI validation. Отсутствие такого пункта в остальных не доказывает запуск validation. Статусы и архив не изменены.

## Что фактически сделано сейчас

Выборочно прочитаны production entry points и порты, зависимости, build-файлы, бизнес/техническая документация, spec, change tasks и исторические отчёты в объёме планирования. Это не сплошное ревью каждой ветви и не подтверждение исправности системы. Сохранены прежние имена бизнес-файлов, добавлены тематические разделы и новый план.

Приложения, тесты, сборки, проектные скрипты, БД, SQL, migrations, Docker и внешние HTTP не запускались. Старые результаты приведены по отчётам; повторная проверка TRX и их соответствия всему текущему коду отложена в этап 14. OpenSpec не валидировался и не архивировался. Production и нормативные требования не изменены.

## Вопросы перед будущими запусками

- Какое окружение внедрения требуется подтвердить: ОС/RID, SQLite или PostgreSQL, версии runtime и внешних утилит?
- Какие exact model IDs нужны приложению? Каталог и встроенная карта tokenizer — разные множества.
- Есть ли у приложения обоснованный полный estimator и контракт совместимости opaque-состояния? Встроенный счётчик этого не обещает.
- Какие внешние действия изменяют бизнес-данные, как приложение устанавливает их фактический исход и защищается от дубликатов?
- Какие B/C/D проверки будут отдельно разрешены? До разрешения их состояние — «не проверено».

Эти вопросы не вводят новых требований и не мешают начать разрешённый статический этап. Возможные расхождения фиксируются в [Findings](Findings.md).

## Состав подготовительных изменений

Создано 24 файла, изменено 9; все находятся внутри Documentation.

Новая папка Documentation/Plans/AgentBridge Quality Audit содержит 20 файлов:

- README.md
- Baseline.md
- Methodology.md
- Findings.md
- 00-contract-baseline.md
- 01-configuration-and-access.md
- 02-domain-invariants.md
- 03-persistence-reads.md
- 04-writes-and-concurrency.md
- 05-migrations-and-maintenance.md
- 06-http-json-contracts.md
- 07-sse-and-cancellation.md
- 08-context-composition.md
- 09-token-budget-and-compaction.md
- 10-tools-and-side-effects.md
- 11-agent-run-and-recovery.md
- 12-status-and-cleanup.md
- 13-dll-delivery.md
- 14-test-evidence-and-gaps.md
- 15-final-reconciliation.md

В существующей Documentation/Business logic актуализированы 7 файлов:

- README.md
- 01-purpose-and-scope.md
- 02-dialogs-and-tools.md
- 03-context-and-compaction.md
- 04-storage-and-retention.md
- 05-application-configuration.md
- 06-models-and-status.md

Там же созданы 4 раздела:

- 07-users-and-access.md
- 08-tools-and-permissions.md
- 09-errors-and-recovery.md
- 10-scenarios-and-limitations.md

Навигационные изменения: Documentation/README.md и Documentation/Plans/README.md. Корневой README, solution/build, исторический Initial Implementation, OpenSpec и все недокументальные файлы не изменялись.

## Проверка подготовленных документов

Статически проверены навигация и локальные ссылки всех 33 документов, UTF-8 без BOM, LF, отсутствие U+FFFD, четырёх подряд вопросительных знаков и признаков повреждённого русского текста. Прочитаны итоговый diff/status и исходные строки доказательств через Git. Все 16 этапов остаются «не начат»; index пуст. Это контроль оформления документов, не выполнение аудита или CLI validation. Коммит не создавался.

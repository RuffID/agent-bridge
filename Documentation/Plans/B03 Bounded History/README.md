# B03-BoundedHistory

Дата: 11.10.2026. Статус: **Completed в разрешённой библиотечной, isolated и DLL-поставочной границе**. SQL/provider concurrency/query plans/restart/production не приняты; интеграция Ledger03 остаётся отдельной работой.

Рабочий checkout: `D:/Media/User/source/repos/agent-bridge`. Ledger — только чтение контекста/контрактов; принятые DLL/код/config/исторические планы не изменялись. User scope: library-owned bounded history до materialization, защищающая History API и actual runner. 01A compact catalogue/durable recovery уже существуют и сохраняются. Ledger03 остаётся In Progress; 04–09/production activation не начинаются.

## Реализовано

- Явные `DialogHistoryLoadLimits`, actual `IBoundedDialogReader`, SQL Server registration и strict `AgentRunner.RunBoundedAsync`; прежняя ABI и единая actual orchestration сохранены.
- Все seven query adapters работают через existing EFCoreLibrary base Query с bounded scalar preflight и независимым server payload byte predicate/Take, общий operation record/query budget. Все reports/provenance/metadata/compact/internal loaders включены.
- Full snapshot либо safe overflow/conflict; gate/rechecks/CAS/incarnation/fencing/function pairs/no-replay/Unknown/finalization/recovery сохраняются. Cancellation/I/O/cleanup не маскируются. Read refusal не изменяет canonical state. Новая storage schema и hard write cap не добавлены.
- Current API/options/errors/подключение/совместимость и доказанная граница — [technical31](<../../Technical documentation/31-bounded-history.md>); owning AGENTS и OpenSpec main синхронизированы.
- Fresh tests/coverage/Release outputs и isolated binary API probe — [Evidence](Evidence.md).

## Поставка и приёмка

После прямого разрешения пользователя выполнены ровно четыре [упаковочные команды](Packaging-commands.md). Созданы три source SDK kits (114/112/112 files) и полный новый schema3 DLL/NuGet kit:10 собственных DLL,9 XML,3 props,12 required package roots, EF10.0.12/SqlClient7.0.2 exact; net10.0/Release, win-x64/linux-x64/linux-arm64.

Полный kit: `D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/SqlServer`.

Manifest SHA-256: `fd66709eb625acc138b90ed5da1834bf0a988bb10258630ef43ecb4a84dac633`.

Source/SDK inputs перепроверены до упаковки; own DLL/XML hashes идентичны трём RID. External binary consumer вне всех repo использует **полный kit/props**, без ProjectReference: три local-cache-only locked restore/build0 warnings/errors и actual B03 compile-only API sample. Windows external binary isolated tests:29 passed,0 failed/skipped; семь negative exact/duplicate/framework/RID/lock-disabled/lock-drift guards отказали ожидаемым target/error code. Metadata/XML/PE/closure/source-graph и native metadata проверены без native loading. Подробности — [Evidence](Evidence.md).

B03 закрыт в указанной границе; library/delivery blocker отсутствует. Ledger kit в этой задаче **не заменён**; все22 accepted files совпадают с прежним manifest. [Самодостаточный prompt следующего чата](Continue-prompt.md) поручает отдельно проверить/принять новый kit в Ledger и завершить только весь mandatory03.04–09 и production activation запрещены; при следующей передаче prompt обязательно обновляется.

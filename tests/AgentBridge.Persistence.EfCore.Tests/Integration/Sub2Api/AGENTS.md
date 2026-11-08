# Реальный persistence restart Sub2Api

- Public Integration facade, AgentRunner, Sub2Api/HttpClientLibrary и production persistence ports работают с отдельной PostgreSQL существующего DatabaseIntegrationFixture. Только HTTP и прикладной инструмент подставные; host и live Sub2Api не запускаются.
- Каждый restart освобождает прежний facade DI root и создаёт новый над той же БД. Это reconnect приложения, не перезапуск PostgreSQL или ОС.
- Потеря acknowledgement вводится после настоящего SQL commit через техническую IUnitOfWorkSession. Не подменять transaction, запись journal или repositories. Отдельно проверять Started и Succeeded; injected failure не является настоящим сетевым сбоем.
- Lease expiry задаётся управляемым server TimeProvider без ожидания реальной минуты. Fence не доказывает NotStarted; неизвестный исход требует явного recovery, original rows остаются неизменными. Ни recovery, ни новый turn не повторяют прежний инструмент.
- Все тесты имеют Dependency=Database и collection DatabaseIntegration. Запускать адресно этот класс; isolated фильтр его исключает. Ресурсы и cleanup принадлежат существующему fixture, рабочие подключения запрещены.

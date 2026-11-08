# Изолированные проверки usage sub2api

- Public optional DI проверяется через Integration с disconnected provider graph; transport — actual HttpClientLibrary с shared Sub2ApiCatalogFixture и локальными потоками.
- Сохранение direct JSON/optional-полей, per-request доступа, safe errors, send/body cancellation/deadline и cleanup проверяются отдельно. Unsupported не создаёт HTTP-клиент.
- Linked Consumer/Sub2ApiAuxiliaryUsage.cs только компилируется; это не приёмка внешнего binary kit или live sub2api. Никаких host/БД/OAuth/сети/процессов.

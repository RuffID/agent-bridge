# Изолированные проверки композиции sub2api

- xUnit v3/Microsoft.Testing.Platform, actual public Integration DI и options. SQL Server module используется только для disconnected graph/metadata; нет подключения, host, SQL, Docker, live HTTP/OAuth или процессов из тестов.
- ValidateScopes и ValidateOnBuild включены. HTTP factory и scoped key source наблюдаются отдельно от регистрации; fake overrides проверяются как identity, их методы не имитируют успех приложения.
- Unsupported staged портов — ожидаемая граница этапа02, а не реализация коннектора. Общие JSON/SSE regression находятся в CodexLb.Tests и используют actual HttpClientLibrary/local handlers.
- Legacy surface checks сравнивают текущие DLL с embedded текстовым Fixtures/legacy-api-baseline.json. Профиль пользователя, внешний kit, artifacts поставки и папки плана для этого не нужны. Эталон получен из проверенных старых DLL; не обновлять expected по новым исходникам ради зелёного теста.
- Сохранность внешнего kit/frozen binaries и совпадение текстового эталона со старыми PE отдельно проверяет Delivery/LegacyBaseline с явным AgentBridgeLegacyBaselineRoot. Сравнение API не заменяет resolution неперекомпилированных consumers против нового delivery closure, runtime и live приёмку.
- Restore/build с GeneratePackageOnBuild=false; тестовый runner только после compile-check с тем же BaseOutputPath. Отсутствующий embedded resource является ошибкой, silent Skip и автоматического восстановления нет.

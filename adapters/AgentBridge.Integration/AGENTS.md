# Стандартная интеграция

- SDK library net10.0 собирает существующие ядро, CodexLb и EF adapters. Обратные зависимости из ядра запрещены; ASP.NET Core/host не требуется.
- Конкретные провайдеры не входят в facade dependencies. Приложение явно регистрирует AddAgentBridgeSqlServer/Sqlite/PostgreSql до фасада; Database.Provider выбирает ровно один подключённый модуль. При переносе descriptors IValidateOptions сохраняются через TryAddEnumerable, чтобы не потерять required/module validators.
- `AddAgentBridge(configuration, httpClientFactory)` принимает root/выбранный раздел с AgentBridge, CodexLb, Database. Options проходят existing required binding/validation13; IConfiguration не становится runtime dependency.
- Приложение заранее регистрирует ILoggerFactory и optional индивидуальный источник. HTTP callback обязателен, scoped и без I/O при создании; приложение владеет HttpClient/handlers/timeout/disposal. Pipeline — actual AddCodexLbResponses/HttpClientLibrary, не собственный HTTP.
- Shared без app source использует внутренний null source; Individual без заранее зарегистрированного app source отклоняется options validation. Ошибки источника не меняют ключ.
- Existing contracts сохраняются через TryAdd при переносе module descriptors. Ordered IContextProvider перечисляются без сортировки внутри scoped ContextBuilder; custom ContextBuilder сохраняется. Tools/validators остаются scoped по existing tool registration.
- Повтор с теми же config/factory references — no-op; другие аргументы требуют нового ServiceCollection и явно отклоняются. Не создавать промежуточный ServiceProvider.
- DI не выполняет startup/maintenance/миграции/cleanup/compact/HTTP, не создаёт logger/files/auth/endpoints/scheduler. AddAgentBridgeDiagnostics вызывается только после проверки app logger factory.
- Проверки public composition находятся в existing persistence tests, только Dependency!=Database; actual EF metadata без подключения, fake handler/local streams без сети. Compile конкретного проекта с GeneratePackageOnBuild=false; kits/runtime относятся к15–19.

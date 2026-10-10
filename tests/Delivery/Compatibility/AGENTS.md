# Бинарная совместимость комплекта Ledger

- Проект зависит только от DLL-поставки через AgentBridgeDeliveryRoot и пакетов P00; ProjectReference и зависимости на исходники запрещены.
- Копировать csproj и C# вне репозиториев. RuntimeIdentifier задавать явно; import выполняется после прямых PackageReference и сохраняет exact validation/locks.
- FrameworkReference Microsoft.AspNetCore.App предоставляет Configuration/DI приложения; импорт фиксирует обязательные пакетные roots. EF Design/Tools/InMemory и Serilog сохранены для проверки combined graph, но hosting, SQL и InMemory database не используются.
- Разрешены закрытый SqlConnection, EF model/change tracker, ValidateScopes/ValidateOnBuild и локальный HttpMessageHandler. Не вызывать SaveChanges, Open, Execute, maintenance operations, native loading или host.
- Windows выполняет изолированные тесты; Linux RID собираются без исполнения. Конфликтующий direct PackageReference проверять отдельным внешним project через restore и ожидать отказ exact target.

using AgentBridge.Persistence.EfCore.Configuration;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Явный контракт текущей схемы для integration fixtures, проверяемый также без подключения к БД.</summary>
public static class IntegrationSchemaExpectations
{
    /// <summary>Полный состав собственных таблиц в ordinal порядке; history и таблицы host не включены.</summary>
    public static IReadOnlyList<string> Tables { get; } = Array.AsReadOnly<string>(
        ["CanonicalItems", "DialogCatalog", "DialogCatalogChanges", "DialogCatalogClocks", "DialogContexts",
         "DialogRecoveryOperations", "DialogSettings", "DialogTurns", "Dialogs", "ModelSteps"]);

    /// <summary>Точные ID всех migrations выбранного provider в порядке применения; не выводятся из actual assembly.</summary>
    public static IReadOnlyList<string> Migrations(DatabaseProvider provider) => Array.AsReadOnly<string>(provider switch
    {
        DatabaseProvider.SQLite =>
        [
            "20261003155233_InitialAgentBridgeSchema",
            "20261004074344_AddDurableToolAttempts",
            "20261004092118_AddDialogSettings",
            "20261011024150_AddCatalogAndDurableRecovery"
        ],
        DatabaseProvider.PostgreSql =>
        [
            "20261003155235_InitialAgentBridgeSchema",
            "20261004074347_AddDurableToolAttempts",
            "20261004092121_AddDialogSettings",
            "20261011024151_AddCatalogAndDurableRecovery"
        ],
        DatabaseProvider.SqlServer =>
        [
            "20261006060948_InitialAgentBridgeSchema",
            "20261011024148_AddCatalogAndDurableRecovery"
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    });
}

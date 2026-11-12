using System.Data;
using System.Linq.Expressions;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.Migrations.SqlServer;
using AgentBridge.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет SQL Server size translation и public bounded reader без БД.</summary>
public class SqlServerBoundedHistoryTests
{
    /// <summary>Все десять таблиц используют DATALENGTH и server-side bounds до materialization.</summary>
    [Fact]
    public void EveryTableTranslatesServerBounds()
    {
        // Arrange
        using AgentBridgeDbContext context = new SqlServerAgentBridgeDbContextFactory().CreateDbContext([]);

        // Act
        Verify<DialogRecord>(context, "OwnerId", "RuntimeJson");
        Verify<DialogTurnRecord>(context, "SettingsJson");
        Verify<CanonicalItemRecord>(context, "ContentJson");
        Verify<ModelStepRecord>(context, "ResponseOutputJson", "ResponseEnvelopeJson", "ResponseContinuationJson", "ResponseErrorMessage", "ToolAttemptsJson");
        Verify<DialogContextRecord>(context, "ResponseOutputJson", "ResponseEnvelopeJson", "ResponseContinuationJson", "SelectedModel");
        Verify<DialogSettingsRecord>(context, "Model", "Effort");
        Verify<DialogCatalogRecord>(context, "ProfileJson", "PolicyJson", "Title", "Snippet", "OwnerId", "SiteId", "AgentId");
        Verify<DialogCatalogClockRecord>(context, "Id");
        Verify<DialogCatalogChangeRecord>(context, "StateJson", "Kind");
        Verify<DialogRecoveryOperationRecord>(context, "ResultJson", "PairsJson", "PayloadHash", "Kind");

        // Assert
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>Явная policy подключает настоящий reader и отклоняет повторную регистрацию.</summary>
    [Fact]
    public async Task RegistrationUsesSharedReaderWithoutConnecting()
    {
        // Arrange
        ServiceCollection services = new();
        DialogHistoryLoadLimits limits = new(128, 1048576, 64);
        services.AddAgentBridgeSqlServerBoundedHistory(limits);
        services.AddAgentBridgeSqlServer();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = "Server=invalid.example;Database=synthetic;Encrypt=True";
        });
        services.AddAgentBridgePersistence();
        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = root.CreateAsyncScope();

        // Act
        IBoundedDialogReader reader = scope.ServiceProvider.GetRequiredService<IBoundedDialogReader>();

        // Assert
        Assert.Same(limits, reader.LoadLimits);
        Assert.Same(reader, scope.ServiceProvider.GetRequiredService<IDialogReader>());
        Assert.Equal(ConnectionState.Closed, scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().Database.GetDbConnection().State);
        Assert.Throws<InvalidOperationException>(() => services.AddAgentBridgeSqlServerBoundedHistory(limits));
    }

    /// <summary>Проверяет scalar projection и ограниченный payload query через настоящий EF translator.</summary>
    private static void Verify<T>(AgentBridgeDbContext context, params string[] columns) where T : class
    {
        Expression<Func<T, long>> size = new SqlServerHistoryRecordSizeProvider().Size<T>();
        Expression<Func<T, bool>> fits = Expression.Lambda<Func<T, bool>>(
            Expression.LessThanOrEqual(size.Body, Expression.Constant(8192L)), size.Parameters);
        string preflight = context.Set<T>().Select(size).Take(12).ToQueryString();
        string payload = context.Set<T>().Where(fits).Take(11).ToQueryString();
        Assert.Contains("TOP(", preflight);
        Assert.Contains("TOP(", payload);
        Assert.Contains("WHERE", payload);
        Assert.Contains("<=", payload);
        Assert.Contains("DATALENGTH(", preflight);
        Assert.Contains("DATALENGTH(", payload);
        foreach (string column in columns)
        {
            Assert.Contains($"[{column}]", preflight);
            Assert.Contains($"[{column}]", payload);
        }
    }
}

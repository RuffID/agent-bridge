using System.Data;
using System.Linq.Expressions;
using AgentBridge.Application.Models;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.Migrations.PostgreSql;
using AgentBridge.Persistence.Migrations.Sqlite;
using AgentBridge.Persistence.Migrations.SqlServer;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual catalogue query translation и generated operations; соединение и SQL не исполняются.</summary>
public class DialogCatalogProviderTests
{
    /// <summary>Production query применяет keyset/order/take на provider query до materialization, без canonical joins.</summary>
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Sqlite")]
    [InlineData("PostgreSql")]
    public async Task ProductionCatalogQueryIsBoundedAndTranslatable(string provider)
    {
        using AgentBridgeDbContext context = Create(provider);
        QueryProbe<DialogCatalogRecord> catalog = new(context);
        DialogCatalogQueries queries = new(catalog, new QueryProbe<DialogCatalogClockRecord>(context),
            new QueryProbe<DialogCatalogChangeRecord>(context), new QueryProbe<DialogRecoveryOperationRecord>(context));
        DialogCatalogCursor cursor = new(CatalogWriteFixture.SCOPE, CatalogWriteFixture.NOW,
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        await queries.ReadAsync(new string('A', 64), new(CatalogWriteFixture.SCOPE, cursor, 17,
            CatalogWriteFixture.NOW), TestContext.Current.CancellationToken);

        Assert.Equal(1, catalog.Calls);
        Assert.Equal(17, catalog.Limit);
        string sql = catalog.Sql!;
        Assert.Contains("DialogCatalog", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SortTimeUtc", sql, StringComparison.Ordinal);
        Assert.Contains("IdSortKey", sql, StringComparison.Ordinal);
        Assert.Contains(provider == "SqlServer" ? "TOP(" : "LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanonicalItems", sql, StringComparison.Ordinal);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>Новая migration сохраняет legacy данные без invented registration/timestamps и не содержит raw SQL.</summary>
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Sqlite")]
    [InlineData("PostgreSql")]
    public void CatalogMigrationKeepsLegacyRootsUnregistered(string provider)
    {
        using AgentBridgeDbContext context = Create(provider);
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        Migration migration = assembly.CreateMigration(Assert.Single(assembly.Migrations,
            item => item.Key.EndsWith("_AddCatalogAndDurableRecovery", StringComparison.Ordinal)).Value, context.Database.ProviderName!);
        CreateTableOperation[] tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(["DialogCatalog", "DialogCatalogChanges", "DialogCatalogClocks", "DialogRecoveryOperations"],
            tables.Select(table => table.Name).Order(StringComparer.Ordinal));
        AddColumnOperation registered = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), column => column.Name == "CatalogRegistered");
        Assert.Equal(false, registered.DefaultValue);
        Assert.True(Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), column => column.Name == "SavedAtUtc").IsNullable);
        Assert.True(Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), column => column.Name == "RuntimeJson").IsNullable);
        Assert.Equal(0L, Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(), column => column.Name == "RecoveryRevision").DefaultValue);
        CreateIndexOperation keyset = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>(),
            index => index.Table == "DialogCatalog");
        Assert.NotNull(keyset.IsDescending);
        Assert.Equal([false, true, true], keyset.IsDescending);
        Assert.Equal("DialogRecoveryOperations", Assert.Single(tables, table => table.ForeignKeys.Count != 0).Name);
        Assert.DoesNotContain(migration.UpOperations.Concat(migration.DownOperations), operation => operation is SqlOperation);
        Assert.Equal(4, migration.DownOperations.OfType<DropTableOperation>().Count());
        Assert.Equal(4, migration.DownOperations.OfType<DropColumnOperation>().Count());
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    private static AgentBridgeDbContext Create(string provider) => provider switch
    {
        "SqlServer" => new SqlServerAgentBridgeDbContextFactory().CreateDbContext([]),
        "Sqlite" => new SqliteAgentBridgeDbContextFactory().CreateDbContext([]),
        "PostgreSql" => new PostgreSqlAgentBridgeDbContextFactory().CreateDbContext([]),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    /// <inheritdoc/>
    private class QueryProbe<T>(AgentBridgeDbContext context) : IContextGetItemByPredicateRepository<T, AgentBridgeContextKey> where T : class
    {
        public int Calls { get; private set; }
        public int? Limit { get; private set; }
        public string? Sql { get; private set; }

        public Task<T?> GetItemByPredicateAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = false,
            Func<IQueryable<T>, IQueryable<T>>? include = null, CancellationToken ct = default) =>
            throw new NotSupportedException("Unexpected unbounded query.");

        public Task<List<T>> GetItemsByPredicateAsync(Expression<Func<T, bool>>? predicate = null, int skip = 0,
            int? take = null, bool asNoTracking = false, Func<IQueryable<T>, IQueryable<T>>? include = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            Limit = take;
            Assert.True(asNoTracking);
            Assert.Equal(0, skip);
            IQueryable<T> query = context.Set<T>().AsNoTracking().Where(predicate!);
            query = include!(query).Take(take!.Value);
            Sql = query.ToQueryString();
            return Task.FromResult(new List<T>());
        }
    }
}

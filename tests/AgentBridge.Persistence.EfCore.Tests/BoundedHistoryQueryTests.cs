using System.Data;
using System.Linq.Expressions;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using AgentBridge.Persistence.Migrations.SqlServer;
using AgentBridge.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual bounded query и SQL translation без connection; bounds проверяются до выдачи полной строки.</summary>
public class BoundedHistoryQueryTests
{
    /// <summary>SQL содержит server byte predicate/Take; scalar preflight не выбирает canonical payload/owned report.</summary>
    [Fact]
    public void EveryMappedTableTranslatesSizeAndPayloadBoundsIncludingBinaryOwner()
    {
        using AgentBridgeDbContext context = new SqlServerAgentBridgeDbContextFactory().CreateDbContext([]);
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
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>Проверяет обе server query формы, а не только размер уже materialized DTO.</summary>
    private static void Verify<T>(AgentBridgeDbContext context, params string[] columns) where T : class
    {
        Expression<Func<T, long>> size = new SqlServerHistoryRecordSizeProvider().Size<T>();
        string preflight = context.Set<T>().Select(size).Take(12).ToQueryString();
        Expression<Func<T, bool>> fits = Expression.Lambda<Func<T, bool>>(
            Expression.LessThanOrEqual(size.Body, Expression.Constant(8192L)), size.Parameters);
        string payload = context.Set<T>().Where(fits).Take(11).ToQueryString();
        Assert.Contains("TOP(", preflight);
        Assert.Contains("TOP(", payload);
        Assert.Contains("WHERE", payload);
        Assert.Contains("<=", payload);
        Assert.DoesNotContain("LEN(", preflight);
        foreach (string column in columns)
        {
            Assert.Contains($"DATALENGTH([", preflight);
            Assert.Contains($"[{column}]", preflight);
            Assert.Contains($"[{column}]", payload);
        }
    }

    /// <summary>Boundary/limit+1 records: overflow получает только bounded scalar rows и не загружает payload.</summary>
    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(1000, false)]
    public async Task RecordBoundaryStopsBeforePayload(int count, bool success)
    {
        List<CanonicalItemRecord> records = Enumerable.Range(1, count).Select(index => new CanonicalItemRecord
        {
            Sequence = index, ContentJson = "{\"type\":\"message\"}"
        }).ToList();
        FakeHistoryQueryRepository<CanonicalItemRecord> repository = new(records);
        HistoryLoadBudget budget = new(new(3, 4096, 1));
        PersistenceOperationGate gate = new(budget);
        HistoryQueryGuard guard = new(budget, new SqlServerHistoryRecordSizeProvider());
        using IDisposable operation = gate.Enter();
        if (success)
        {
            Assert.Equal(3, (await guard.ReadAsync(repository.Query(true).OrderBy(row => row.Sequence), TestContext.Current.CancellationToken)).Count);
            Assert.Equal(3, repository.PayloadRows);
            Assert.Contains("Take(3)", Assert.Single(repository.PayloadQueries).ToString());
        }
        else
        {
            await Assert.ThrowsAsync<DialogHistoryLoadLimitException>(() => guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken));
            Assert.Equal(0, repository.PayloadRows);
            Assert.Empty(repository.PayloadQueries);
        }

        Assert.Equal(Math.Min(count, 4), Assert.Single(repository.ScalarRows));
    }

    /// <summary>UTF-16/accounting учитывает все пробелы/NUL/Unicode; limit+1 запрещает full materialization.</summary>
    [Theory]
    [InlineData("ASCII", true)]
    [InlineData("ASCII", false)]
    [InlineData("Ж😀\0   ", true)]
    [InlineData("Ж😀\0   ", false)]
    public async Task ByteBoundaryPreservesTrailingSpacesAndUnicode(string text, bool atBoundary)
    {
        CanonicalItemRecord row = new() { ContentJson = text };
        long size = FakeHistoryQueryRepository<CanonicalItemRecord>.Size(row);
        FakeHistoryQueryRepository<CanonicalItemRecord> repository = new([row]);
        HistoryLoadBudget budget = new(new(1, atBoundary ? size : size - 1, 1));
        using IDisposable operation = new PersistenceOperationGate(budget).Enter();
        HistoryQueryGuard guard = new(budget, new SqlServerHistoryRecordSizeProvider());
        if (atBoundary)
        {
            Assert.Same(row, Assert.Single(await guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken)));
        }
        else
        {
            await Assert.ThrowsAsync<DialogHistoryLoadLimitException>(() => guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken));
            Assert.Empty(repository.PayloadQueries);
        }
    }

    /// <summary>Рост после scalar query не снимает byte/count bounds; сокращённый snapshot не возвращается.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RaceCannotRemovePayloadBounds(bool growBytes)
    {
        List<CanonicalItemRecord> rows = [new() { ContentJson = "{}" }];
        FakeHistoryQueryRepository<CanonicalItemRecord> repository = new(rows);
        repository.BeforeExecute = scalar =>
        {
            if (!scalar)
            {
                if (growBytes)
                {
                    rows[0].ContentJson = new string(' ', 1000000);
                }
                else
                {
                    rows.AddRange(Enumerable.Range(0, 1000).Select(_ => new CanonicalItemRecord { ContentJson = "{}" }));
                }
            }
        };
        HistoryLoadBudget budget = new(new(2, 4096, 1));
        using IDisposable operation = new PersistenceOperationGate(budget).Enter();
        HistoryQueryGuard guard = new(budget, new SqlServerHistoryRecordSizeProvider());
        await Assert.ThrowsAsync<DialogHistoryLoadConflictException>(() => guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken));
        Assert.InRange(repository.PayloadRows, 0, 2);
        Assert.Contains("Take(2)", Assert.Single(repository.PayloadQueries).ToString());
        Assert.Contains("<= 4096", repository.PayloadQueries[0].ToString());
    }

    /// <summary>Повторные reads расходуют общий record/query budget; release gate начинает независимую операцию.</summary>
    [Fact]
    public async Task OperationBudgetDoesNotResetBetweenQueries()
    {
        FakeHistoryQueryRepository<CanonicalItemRecord> repository = new([new() { ContentJson = "{}" }]);
        HistoryLoadBudget budget = new(new(1, 4096, 2));
        PersistenceOperationGate gate = new(budget);
        HistoryQueryGuard guard = new(budget, new SqlServerHistoryRecordSizeProvider());
        using (gate.Enter())
        {
            await guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<DialogHistoryLoadLimitException>(() => guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken));
            Assert.Equal(1, repository.PayloadRows);
        }

        using (gate.Enter())
        {
            Assert.Single(await guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken));
        }
    }

    /// <summary>Query limit/limit+1 прекращает I/O независимо от record budget, включая пустые запросы.</summary>
    [Fact]
    public async Task QueryLimitStopsBeforeNextPreflight()
    {
        FakeHistoryQueryRepository<CanonicalItemRecord> repository = new([]);
        HistoryLoadBudget budget = new(new(4, 4096, 1));
        HistoryQueryGuard guard = new(budget, new SqlServerHistoryRecordSizeProvider());
        using IDisposable operation = new PersistenceOperationGate(budget).Enter();
        Assert.Empty(await guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DialogHistoryLoadLimitException>(() => guard.ReadAsync(repository.Query(true), TestContext.Current.CancellationToken));
        Assert.Single(repository.ScalarQueries);
        Assert.Single(repository.PayloadQueries);
    }

    /// <summary>Caller cancel и ошибки I/O не превращаются в overflow/empty; следующая query не исполняется.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndErrorsRemainObservable(bool canceled)
    {
        FakeHistoryQueryRepository<CanonicalItemRecord> repository = new([new() { ContentJson = "{}" }]);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Exception primary = canceled ? new OperationCanceledException(caller.Token) : new IOException("isolated-io");
        repository.Error = primary;
        HistoryLoadBudget budget = new(new(1, 4096, 1));
        using IDisposable operation = new PersistenceOperationGate(budget).Enter();
        HistoryQueryGuard guard = new(budget, new SqlServerHistoryRecordSizeProvider());
        Assert.Same(primary, await Assert.ThrowsAnyAsync<Exception>(() => guard.ReadAsync(repository.Query(true), caller.Token)));
        Assert.Empty(repository.PayloadQueries);
    }

    /// <summary>Явная регистрация разрешает strict bounded port, не открывает соединение и не допускает скрытой смены limits.</summary>
    [Fact]
    public async Task RegistrationUsesSharedGateAndActualBoundedReader()
    {
        ServiceCollection services = new();
        DialogHistoryLoadLimits limits = new(128, 1048576, 64);
        services.AddAgentBridgeSqlServerBoundedHistory(limits);
        services.AddAgentBridgeSqlServer();
        services.Configure<DatabaseOptions>(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = "Server=never-open;Database=isolated;Integrated Security=true;Encrypt=false";
        });
        services.AddAgentBridgePersistence();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IBoundedDialogReader reader = scope.ServiceProvider.GetRequiredService<IBoundedDialogReader>();
        Assert.Same(limits, reader.LoadLimits);
        Assert.Same(reader, scope.ServiceProvider.GetRequiredService<IDialogReader>());
        Assert.Equal(ConnectionState.Closed, scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>().Database.GetDbConnection().State);
        Assert.Throws<InvalidOperationException>(() => services.AddAgentBridgeSqlServerBoundedHistory(limits));
    }
}

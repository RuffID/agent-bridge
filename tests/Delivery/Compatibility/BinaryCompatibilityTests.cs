using System.Data;
using System.Net;
using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.SqlServer;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.SqlServer.Providers;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.BinaryCompatibility.Tests;

/// <summary>Проверяет реальные DLL нового комплекта без соединения, SQL, host или сети.</summary>
public class BinaryCompatibilityTests
{
    /// <summary>Строгий DI разрешает SQL Server maintenance и общие repository/UoW на одном закрытом контексте.</summary>
    [Fact]
    public void SqlServerMaintenanceAndRepositoriesShareClosedScopedContext()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeSqlServer();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = "Server=never-open.invalid;Database=synthetic;Integrated Security=true;ConnectRetryCount=0";
        });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(options =>
        {
            options.SqlServerBackupDirectory = "/server/backups";
            options.BackupRetentionPeriod = TimeSpan.FromDays(30);
        }, MaintenanceExecutionMode.SingleInitializer);

        using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        root.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();
        AgentBridgeDbContext context = first.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
        IAppDbContext<AgentBridgeContextKey> adapter = first.ServiceProvider.GetRequiredService<IAppDbContext<AgentBridgeContextKey>>();
        IUnitOfWorkContext<AgentBridgeContextKey> uow = first.ServiceProvider.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>();
        IDatabaseMaintenance<AgentBridgeContextKey> maintenance = first.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.IsType<SqlConnection>(context.Database.GetDbConnection());
        Assert.Equal(new Version(7, 0, 0, 0), typeof(SqlConnection).Assembly.GetName().Version);
        Assert.Equal(new Version(10, 0, 12, 0), typeof(DbContext).Assembly.GetName().Version);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Same(context.Database, adapter.Database);
        Assert.Same(context.Database, uow.Database);
        Assert.IsType<SqlServerMaintenanceProvider<AgentBridgeContextKey>>(first.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>());
        Assert.Equal("sqlserver", maintenance.Capabilities.Provider);
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<AgentBridgeDbContext>());
        Assert.Throws<InvalidOperationException>(() => root.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>());
        Assert.NotNull(first.ServiceProvider.GetRequiredService<IRepositoryContext<AgentBridgeContextKey>>());
        Assert.NotNull(first.ServiceProvider.GetRequiredService<IContextGetItemByPredicateRepository<DialogRecord, AgentBridgeContextKey>>());
        Assert.NotNull(first.ServiceProvider.GetRequiredService<IContextDeleteItemRepository<DialogRecord, AgentBridgeContextKey>>());

        DialogRecord record = new() { Id = Guid.NewGuid(), OwnerId = "owner", CreatedAtUtc = DateTimeOffset.UtcNow };
        first.ServiceProvider.GetRequiredService<IContextCreateItemRepository<DialogRecord, AgentBridgeContextKey>>().Create(record);

        Assert.Equal(EntityState.Added, context.Entry(record).State);
        Assert.Same(context.Set<DialogRecord>().AsQueryable().Provider, first.ServiceProvider.GetRequiredService<IContextQueryRepository<DialogRecord, AgentBridgeContextKey>>().Query().Provider);
        uow.ClearChangeTracker();

        Assert.Empty(context.ChangeTracker.Entries());
        first.ServiceProvider.GetRequiredService<IContextUpdateItemRepository<DialogRecord, AgentBridgeContextKey>>().Update(record);

        Assert.Equal(EntityState.Modified, context.Entry(record).State);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>Новые HTTP DLL сохраняют успешный JSON, поток и ошибку с metadata на локальном handler.</summary>
    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    public async Task HttpJsonAndErrorContractsUseOnlyStubHandler(int status)
    {
        using StubHandler handler = new((HttpStatusCode)status);
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://never-requested.invalid/") };
        HttpApiClient client = new(http, NullLogger<HttpApiClient>.Instance, null);

        if (status == 200)
        {
            Dictionary<string, int>? result = await client.GetAsync<Dictionary<string, int>>("json", ct: TestContext.Current.CancellationToken);

            Assert.Equal(42, Assert.IsType<Dictionary<string, int>>(result)["value"]);
            await using HttpClientLibrary.Models.HttpStreamResponseResult stream = await client.SendStreamAsync(new LibraryHttpRequestOptions { Url = "stream" }, TestContext.Current.CancellationToken);
            using StreamReader reader = new(stream.Body);

            Assert.Equal("{\"value\":42}", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, handler.Requests);
        }
        else
        {
            HttpRequestFailedException error = await Assert.ThrowsAsync<HttpRequestFailedException>(() => client.GetAsync<object>("json", ct: TestContext.Current.CancellationToken));

            Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
            Assert.Equal(new Uri("https://never-requested.invalid/json"), error.Url);
            Assert.NotNull(error.ErrorResponse);
            Assert.DoesNotContain("value", error.Message);
            Assert.Equal(1, handler.Requests);
        }
    }

    /// <summary>Возвращает локальные ответы, не передавая запрос в сеть.</summary>
    private class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        /// <summary>Количество обращений к локальному handler.</summary>
        public int Requests { get; private set; }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"value\":42}", System.Text.Encoding.UTF8, "application/json") });
        }
    }
}

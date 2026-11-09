using System.Security.Cryptography;
using AgentBridge.Persistence.EfCore.Configuration;
using Docker.DotNet.Models;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Владеет временным каталогом коллекции и ленивым PostgreSQL-контейнером.</summary>
public class DatabaseIntegrationFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim startupGate = new(1, 1);
    private PostgreSqlContainer? container;
    private Task? startup;
    private PostgreSqlIntegrationTools? tools;
    private string? root;
    private bool disposed;

    /// <inheritdoc/>
    public ValueTask InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (root is not null)
        {
            throw new InvalidOperationException("Окружение коллекции уже инициализировано.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("Текущие реальные PostgreSQL проверки поддерживают Windows runner.");
        }

        root = Path.Combine(Path.GetTempPath(), "agentbridge-integration", "abverify_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        return ValueTask.CompletedTask;
    }

    /// <summary>Выделяет собственную БД; PostgreSQL использует один общий контейнер.</summary>
    /// <param name="provider">Фактический PostgreSQL.</param>
    /// <returns>Владелец БД без применения миграций; тест явно вызывает InitializeAsync.</returns>
    public async Task<IntegrationDatabase> CreateDatabaseAsync(DatabaseProvider provider)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (root is null)
        {
            throw new InvalidOperationException("Требуется инициализированный fixture коллекции.");
        }

        if (provider != DatabaseProvider.PostgreSql)
        {
            throw new ArgumentOutOfRangeException(nameof(provider));
        }

        await startupGate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            startup ??= StartPostgreSqlAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            startupGate.Release();
        }

        // Failed startup остаётся failed: повторный контейнер и скрытый retry не создаются.
        await startup;
        PostgreSqlContainer started = container ?? throw new InvalidOperationException("Контейнер не создан.");
        string connection = new NpgsqlConnectionStringBuilder(started.GetConnectionString())
        {
            Host = "127.0.0.1", SslMode = SslMode.Disable, Pooling = false
        }.ConnectionString;

        return new IntegrationDatabase(provider, root, connection, tools);
    }

    /// <summary>Проверяет настоящие утилиты и запускает только собственный контейнер с bounded readiness.</summary>
    private async Task StartPostgreSqlAsync(CancellationToken cancellationToken)
    {
        tools = PostgreSqlIntegrationTools.Discover();
        await tools.VerifyAsync(cancellationToken);

        string identity = "abverify_" + Guid.NewGuid().ToString("N");
        container = new PostgreSqlBuilder("postgres:18")
            .WithName(identity)
            .WithDatabase("postgres")
            .WithUsername(identity)
            .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)))
            .WithCleanUp(true)
            .WithCreateParameterModifier(ConfigureContainer)
            .Build();

        if (container.Hostname is not ("127.0.0.1" or "localhost"))
        {
            throw new InvalidOperationException("Требуется локальный Docker endpoint для loopback PostgreSQL.");
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        await container.StartAsync(deadline.Token);
    }

    /// <summary>Ограничивает публикацию loopback и хранение bounded tmpfs без внешних volumes.</summary>
    internal static void ConfigureContainer(CreateContainerParameters parameters)
    {
        HostConfig host = parameters.HostConfig
            ?? throw new InvalidOperationException("Testcontainers не подготовил HostConfig.");
        host.PortBindings = new Dictionary<string, IList<PortBinding>>
        {
            ["5432/tcp"] = [new PortBinding { HostIP = "127.0.0.1", HostPort = "0" }]
        };
        host.Tmpfs = new Dictionary<string, string>
        {
            ["/var/lib/postgresql"] = "rw,size=1073741824"
        };
        // PostgreSqlBuilder по умолчанию отключает durability; guard/recovery тесты сохраняют реальные commits.
        parameters.Cmd = ["postgres", "-c", "fsync=on", "-c", "full_page_writes=on", "-c", "synchronous_commit=on"];
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;

        disposed = true;
        List<Exception> errors = [];

        if (startup is not null)
        {
            try { await startup; }
            catch { /* Ошибка startup уже передана тесту; cleanup всё равно обязателен. */ }
        }

        if (container is not null)
        {
            try { await container.DisposeAsync(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        if (tools is not null)
        {
            try { await tools.DisposeAsync(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        if (root is not null)
        {
            // Удаляется только пустой root. Остатки failed cleanup сохраняются и означают ошибку.
            try { Directory.Delete(root); }
            catch (Exception exception) { errors.Add(exception); }
        }

        startupGate.Dispose();
        if (errors.Count != 0)
        {
            throw new AggregateException("Не удалось очистить интеграционное окружение.", errors);
        }
    }
}

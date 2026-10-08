using System.Security.Cryptography;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Владеет одним ленивым MSSQL-контейнером коллекции и всеми его БД/backup без внешних volumes.</summary>
public class SqlServerIntegrationFixture : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Func<CancellationToken, Task> start;
    private readonly Func<SqlServerIntegrationSettings> settings;
    private readonly Func<ValueTask> cleanup;
    private MsSqlContainer? container;
    private Task? startup;
    private bool disposed;
    private string? backupDirectory;

    /// <summary>Создаёт только владельца; Docker не проверяется и контейнер не строится.</summary>
    public SqlServerIntegrationFixture()
    {
        start = StartContainerAsync;
        settings = CreateSettings;
        cleanup = DisposeContainerAsync;
    }

    /// <summary>Подставляет границы lifecycle для изолированных проверок без Docker и SQL.</summary>
    internal SqlServerIntegrationFixture(Func<CancellationToken, Task> start,
        Func<SqlServerIntegrationSettings> settings, Func<ValueTask> cleanup)
    {
        this.start = start;
        this.settings = settings;
        this.cleanup = cleanup;
    }

    /// <summary>Запускает единственную попытку подготовки; каждый запрос получает новое уникальное имя БД.</summary>
    public async Task<SqlServerIntegrationSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            startup ??= PrepareAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        await startup;
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);

        return settings();
    }

    /// <summary>Ограничивает всю подготовку, включая pull/readiness/backup каталог, тремя минутами.</summary>
    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            await start(deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception)
        {
            // Исключения Docker/exec могут содержать команду sqlcmd с паролем: raw текст и InnerException не передаются.
            throw new InvalidOperationException("Не удалось подготовить временный MSSQL через локальный Docker Desktop " +
                "за три минуты (" + exception.GetType().Name + "). Проверьте Linux containers, образ и доступность Docker.");
        }
    }

    /// <summary>Запускает SQL Server и создаёт доступный backup каталог от штатного непривилегированного mssql.</summary>
    private async Task StartContainerAsync(CancellationToken cancellationToken)
    {
        string identity = "abverify_" + Guid.NewGuid().ToString("N");
        backupDirectory = "/var/opt/mssql/" + identity;
        container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04")
            .WithName(identity)
            .WithPassword("Ab1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)))
            .WithEnvironment("MSSQL_PID", "Developer")
            .WithDatabase("master")
            .WithLogger(NullLogger.Instance)
            .WithCleanUp(true)
            .WithCreateParameterModifier(ConfigureContainer)
            .Build();

        if (container.Hostname is not ("127.0.0.1" or "localhost"))
        {
            throw new InvalidOperationException("Требуется локальный Docker endpoint для loopback MSSQL.");
        }

        await container.StartAsync(cancellationToken);
        ExecResult mkdir = await container.ExecAsync(["mkdir", "-m", "700", backupDirectory], cancellationToken);
        if (mkdir.ExitCode != 0)
        {
            throw new InvalidOperationException("Не удалось создать серверный backup каталог.");
        }

        ExecResult access = await container.ExecAsync(
            ["sh", "-c", "test -d \"$1\" && test -w \"$1\" && test \"$(id -u)\" = 10001", "--", backupDirectory], cancellationToken);
        if (access.ExitCode != 0)
        {
            throw new InvalidOperationException("Backup каталог должен быть доступен пользователю mssql (10001).");
        }
    }

    /// <summary>Публикует только случайный loopback-порт; данные остаются в собственном контейнере.</summary>
    internal static void ConfigureContainer(CreateContainerParameters parameters)
    {
        HostConfig host = parameters.HostConfig
            ?? throw new InvalidOperationException("Testcontainers не подготовил HostConfig.");
        host.PortBindings = new Dictionary<string, IList<PortBinding>>
        {
            ["1433/tcp"] = [new PortBinding { HostIP = "127.0.0.1", HostPort = "0" }]
        };
    }

    /// <summary>Привязывает настройки к фактически опубликованному порту собственного контейнера.</summary>
    private SqlServerIntegrationSettings CreateSettings()
    {
        MsSqlContainer started = container ?? throw new InvalidOperationException("Контейнер MSSQL не создан.");
        string directory = backupDirectory ?? throw new InvalidOperationException("Backup каталог не подготовлен.");

        return SqlServerIntegrationSettings.FromContainer(started.GetConnectionString(), started.GetMappedPublicPort(1433),
            "abverify_" + Guid.NewGuid().ToString("N"), directory);
    }

    /// <summary>Удаляет контейнер с его БД/backup; токен отменённого теста очистку не отменяет.</summary>
    private async ValueTask DisposeContainerAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;

        disposed = true;
        if (startup is not null)
        {
            try { await startup; }
            catch { /* Ошибка подготовки уже передана тесту; ресурсы всё равно удаляются. */ }
        }

        try
        {
            await cleanup();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Не удалось удалить собственный MSSQL-контейнер (" +
                exception.GetType().Name + "); очистка БД и backup не подтверждена.");
        }
        finally
        {
            gate.Dispose();
        }
    }
}

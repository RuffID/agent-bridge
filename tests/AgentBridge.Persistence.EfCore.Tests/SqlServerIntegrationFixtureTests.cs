using AgentBridge.Persistence.EfCore.Tests.Integration;
using Docker.DotNet.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет MSSQL lifecycle и безопасные ошибки с подставными границами, без Docker и SQL.</summary>
public class SqlServerIntegrationFixtureTests
{
    /// <summary>Порт публикуется только на loopback без фиксированного порта или пользовательских volumes.</summary>
    [Fact]
    public void ContainerUsesRandomLoopbackPortWithoutMounts()
    {
        CreateContainerParameters parameters = new() { HostConfig = new HostConfig() };
        SqlServerIntegrationFixture.ConfigureContainer(parameters);

        KeyValuePair<string, IList<PortBinding>> port = Assert.Single(parameters.HostConfig.PortBindings);
        Assert.Equal("1433/tcp", port.Key);
        PortBinding binding = Assert.Single(port.Value);
        Assert.Equal("127.0.0.1", binding.HostIP);
        Assert.Equal("0", binding.HostPort);
        Assert.Null(parameters.HostConfig.Binds);
        Assert.Null(parameters.HostConfig.Mounts);
        Assert.Null(parameters.Volumes);
        Assert.Throws<InvalidOperationException>(() => SqlServerIntegrationFixture.ConfigureContainer(new()));
    }

    /// <summary>Конструирование/disposal без MSSQL запроса не запускает подготовку.</summary>
    [Fact]
    public async Task UnusedFixtureNeverStartsContainer()
    {
        int starts = 0;
        int cleanups = 0;
        SqlServerIntegrationFixture fixture = new(_ => { starts++; return Task.CompletedTask; }, Settings,
            () => { cleanups++; return ValueTask.CompletedTask; });

        await fixture.DisposeAsync();
        await fixture.DisposeAsync();

        Assert.Equal(0, starts);
        Assert.Equal(1, cleanups);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.GetSettingsAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Конкурентные запросы ожидают одну подготовку и получают независимые имена БД.</summary>
    [Fact]
    public async Task ConcurrentRequestsShareOneStartupAndOwnDatabases()
    {
        int starts = 0;
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using SqlServerIntegrationFixture fixture = new(_ => { starts++; return ready.Task; }, Settings,
            () => ValueTask.CompletedTask);
        Task<SqlServerIntegrationSettings> first = fixture.GetSettingsAsync(TestContext.Current.CancellationToken);
        Task<SqlServerIntegrationSettings> second = fixture.GetSettingsAsync(TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(1, starts);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            ready.SetResult();
            await Task.WhenAll(first, second);
        }

        Assert.NotEqual(first.Result.ConnectionString, second.Result.ConnectionString);
    }

    /// <summary>Ошибка подготовки не повторяется, не пропускается, не раскрывает секреты; cleanup выполняется.</summary>
    [Fact]
    public async Task FailedPreparationIsStickySanitizedAndCleaned()
    {
        int starts = 0;
        int cleanups = 0;
        SqlServerIntegrationFixture fixture = new(_ =>
        {
            starts++;
            return Task.FromException(new IOException("Password=secret-marker;Server=secret-marker"));
        }, Settings, () => { cleanups++; return ValueTask.CompletedTask; });

        try
        {
            InvalidOperationException first = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.GetSettingsAsync(TestContext.Current.CancellationToken));
            InvalidOperationException second = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.GetSettingsAsync(TestContext.Current.CancellationToken));
            Assert.Same(first, second);
            Assert.Contains("Docker Desktop", first.Message);
            Assert.DoesNotContain("secret-marker", first.ToString());
            Assert.Null(first.InnerException);
            Assert.Equal(1, starts);
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        Assert.Equal(1, cleanups);
    }

    /// <summary>Отмена подготовки передаётся тесту; независимый cleanup выполняется после отмены.</summary>
    [Fact]
    public async Task CancelledPreparationIsNotRetriedAndStillCleaned()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int starts = 0;
        int cleanups = 0;
        SqlServerIntegrationFixture fixture = new(ct =>
        {
            starts++;
            return Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }, Settings, () => { cleanups++; return ValueTask.CompletedTask; });
        Task<SqlServerIntegrationSettings> preparation = fixture.GetSettingsAsync(cancellation.Token);
        cancellation.Cancel();

        try
        {
            OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(() => preparation);
            Assert.Equal(cancellation.Token, error.CancellationToken);
            await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.GetSettingsAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, starts);
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        Assert.Equal(1, cleanups);
    }

    /// <summary>Disposal ждёт завершения подготовки перед очисткой; cancelled caller не отменяет cleanup.</summary>
    [Fact]
    public async Task DisposalAwaitsPreparationBeforeCleanup()
    {
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleaned = false;
        SqlServerIntegrationFixture fixture = new(_ => ready.Task, Settings,
            () => { cleaned = true; return ValueTask.CompletedTask; });
        Task<SqlServerIntegrationSettings> preparation = fixture.GetSettingsAsync(TestContext.Current.CancellationToken);
        Task disposal = fixture.DisposeAsync().AsTask();
        try
        {
            Assert.False(disposal.IsCompleted);
            Assert.False(cleaned);
        }
        finally
        {
            ready.SetResult();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => preparation);
            await disposal;
        }

        Assert.True(cleaned);
    }

    /// <summary>Ошибка очистки после ошибки подготовки остаётся видимой и не раскрывает raw сообщения.</summary>
    [Fact]
    public async Task CleanupFailureRemainsVisibleAfterStartupFailure()
    {
        SqlServerIntegrationFixture fixture = new(_ => Task.FromException(new IOException("secret-marker")), Settings,
            () => ValueTask.FromException(new IOException("secret-marker")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.GetSettingsAsync(TestContext.Current.CancellationToken));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DisposeAsync().AsTask());
        Assert.Contains("очистка БД и backup не подтверждена", error.Message);
        Assert.DoesNotContain("secret-marker", error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>MSSQL fact не содержит условного пропуска.</summary>
    [Fact]
    public void SqlServerFactNeverSkipsForMissingEnvironment()
    {
        Assert.Null(new SqlServerIntegrationFactAttribute().Skip);
    }

    /// <summary>Создаёт только локальную конфигурацию нового тестового имени, без DI или соединения.</summary>
    private static SqlServerIntegrationSettings Settings() => SqlServerIntegrationSettings.FromContainer(
        "User ID=sa;Password=synthetic", 15433, "abverify_" + Guid.NewGuid().ToString("N"), "/var/opt/mssql/backups");
}

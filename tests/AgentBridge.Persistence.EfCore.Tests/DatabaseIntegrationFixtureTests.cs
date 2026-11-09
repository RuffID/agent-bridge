using AgentBridge.Persistence.EfCore.Tests.Integration;
using AgentBridge.Persistence.EfCore.Configuration;
using Docker.DotNet.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет lifecycle fixture без Docker, процессов, SQL и открытия БД.</summary>
public class DatabaseIntegrationFixtureTests
{
    /// <summary>Docker metadata публикует только loopback random port и ограничивает временное хранилище.</summary>
    [Fact]
    public void ContainerConfigurationUsesLoopbackAndBoundedTmpfs()
    {
        CreateContainerParameters parameters = new() { HostConfig = new HostConfig() };
        DatabaseIntegrationFixture.ConfigureContainer(parameters);

        KeyValuePair<string, IList<PortBinding>> port = Assert.Single(parameters.HostConfig.PortBindings);
        Assert.Equal("5432/tcp", port.Key);
        PortBinding binding = Assert.Single(port.Value);
        Assert.Equal("127.0.0.1", binding.HostIP);
        Assert.Equal("0", binding.HostPort);
        Assert.NotNull(parameters.HostConfig.Tmpfs);
        KeyValuePair<string, string> tmpfs = Assert.Single(parameters.HostConfig.Tmpfs);
        Assert.Equal("/var/lib/postgresql", tmpfs.Key);
        Assert.Equal("rw,size=1073741824", tmpfs.Value);
        Assert.True(parameters.HostConfig.Binds is null || parameters.HostConfig.Binds.Count == 0);
        Assert.Equal(["postgres", "-c", "fsync=on", "-c", "full_page_writes=on", "-c", "synchronous_commit=on"], parameters.Cmd);
    }

    /// <summary>Неинициализированный fixture отказывает до подготовки PostgreSQL до создания ресурсов.</summary>
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task UninitializedFixtureRejectsDatabaseCreation(DatabaseProvider provider)
    {
        await using DatabaseIntegrationFixture fixture = new();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateDatabaseAsync(provider));
        Assert.Contains("инициализированный fixture", error.Message);
    }

    /// <summary>Закрытый fixture идемпотентно освобождается и не позволяет запускать новое окружение.</summary>
    [Fact]
    public async Task DisposedFixtureRejectsDatabaseCreation()
    {
        // Arrange
        DatabaseIntegrationFixture fixture = new();
        await fixture.DisposeAsync();

        // Act
        await fixture.DisposeAsync();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.CreateDatabaseAsync(DatabaseProvider.PostgreSql));
        Assert.Throws<ObjectDisposedException>(() => { _ = fixture.InitializeAsync(); });
    }

    /// <summary>Удалённые провайдеры отклоняются до запуска контейнера.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task RemovedProviderCannotStartContainer(int selected)
    {
        // Arrange
        await using DatabaseIntegrationFixture fixture = new();
        if (!OperatingSystem.IsWindows())
        {
            // Act
            InvalidOperationException platform = Assert.Throws<InvalidOperationException>(() => fixture.InitializeAsync());

            // Assert
            Assert.Contains("Windows", platform.Message);
            return;
        }

        await fixture.InitializeAsync();

        // Act
        ArgumentOutOfRangeException error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fixture.CreateDatabaseAsync((DatabaseProvider)selected));

        // Assert
        Assert.Equal("provider", error.ParamName);
    }
}

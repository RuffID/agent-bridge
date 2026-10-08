using AgentBridge.Persistence.EfCore.Tests.Integration;
using AgentBridge.Persistence.EfCore.Configuration;
using Docker.DotNet.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет lifecycle fixture без Docker, процессов, SQL и открытия SQLite.</summary>
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

    /// <summary>Неинициализированный fixture отказывает обоим провайдерам до создания ресурсов.</summary>
    [Theory]
    [InlineData(DatabaseProvider.SQLite)]
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
        DatabaseIntegrationFixture fixture = new();
        await fixture.DisposeAsync();
        await fixture.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.CreateDatabaseAsync(DatabaseProvider.PostgreSql));
        Assert.Throws<ObjectDisposedException>(() => { _ = fixture.InitializeAsync(); });
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.CreateSqlServerDatabaseAsync());
    }

    /// <summary>Неинициализированная коллекция отказывает MSSQL до построения контейнера.</summary>
    [Fact]
    public async Task UninitializedFixtureRejectsSqlServerCreation()
    {
        await using DatabaseIntegrationFixture fixture = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateSqlServerDatabaseAsync());
    }

    /// <summary>SQLite получает разные собственные каталоги без контейнера и удаляет их при disposal.</summary>
    [Fact]
    public async Task SqliteConfigurationOwnsSeparateDirectoriesWithoutDatabaseIo()
    {
        DatabaseIntegrationFixture fixture = new();
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<InvalidOperationException>(() => { _ = fixture.InitializeAsync(); });
            await fixture.DisposeAsync();
            return;
        }

        await fixture.InitializeAsync();
        string? root = null;
        try
        {
            await using IntegrationDatabase first = await fixture.CreateDatabaseAsync(DatabaseProvider.SQLite);
            await using IntegrationDatabase second = await fixture.CreateDatabaseAsync(DatabaseProvider.SQLite);
            root = Path.GetDirectoryName(first.DirectoryPath);

            Assert.NotEqual(first.DirectoryPath, second.DirectoryPath);
            Assert.Equal(root, Path.GetDirectoryName(second.DirectoryPath));
            Assert.True(Directory.Exists(first.BackupDirectory));
            Assert.True(Directory.Exists(second.BackupDirectory));
            Assert.False(File.Exists(Path.Combine(first.DirectoryPath, "source.db")));
            Assert.False(File.Exists(Path.Combine(second.DirectoryPath, "source.db")));
            Assert.Contains("source.db", first.ConnectionString);
            Assert.NotEqual(first.ConnectionString, second.ConnectionString);
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        Assert.False(Directory.Exists(root));
    }
}

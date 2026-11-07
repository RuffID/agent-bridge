using System.Reflection;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Responses;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Integration;
using AgentBridge.Persistence.SqlServer;
using AgentBridge.Sub2Api.Configuration;
using HttpClientLibrary.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Настоящая композиция facade без host, HTTP, SQL и разрешения scoped сервисов из root.</summary>
public class ConnectorSelectionTests
{
    /// <summary>Default и явный codex-lb сохраняют прежний gateway, options и scoped lifetime.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyDefaultAndExplicitCodexLb(bool explicitSelection)
    {
        // Arrange
        ServiceCollection services = Services();
        using HttpClient client = new();
        int factoryCalls = 0;
        if (explicitSelection)
        {
            services.AddAgentBridgeCodexLbConnector();
        }

        // Act
        services.AddAgentBridge(Config(false), _ => { factoryCalls++; return client; });
        using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        Assert.Equal(0, factoryCalls);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IModelGateway>());
        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();
        IModelGateway gateway = first.ServiceProvider.GetRequiredService<IModelGateway>();
        Assert.IsType<CodexLbModelGateway>(gateway);
        Assert.Same(gateway, first.ServiceProvider.GetRequiredService<IModelGateway>());
        Assert.NotSame(gateway, second.ServiceProvider.GetRequiredService<IModelGateway>());
        Assert.Equal(2, factoryCalls);
    }

    /// <summary>Sub2Api не читает CodexLb, не создаёт HTTP и не выдаёт успеха отсутствующих возможностей.</summary>
    [Fact]
    public async Task Sub2ApiBundleIsScopedAndExplicitlyUnsupportedWithoutCodexLb()
    {
        // Arrange
        ServiceCollection services = Services();
        services.AddAgentBridgeSub2ApiConnector();
        IConfiguration config = Config(true);
        DialogOwnerId owner = DialogOwnerId.From("owner");
        ModelAccess access = new("synthetic-key");
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), owner, Guid.NewGuid(), "agent");
        ModelRequest request = new("model", "medium", "instructions", [], []);
        int callbacks = 0;

        // Act
        services.AddAgentBridge(config, _ => throw new InvalidOperationException("HTTP factory must not run"));
        using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope scope = provider.CreateScope();
        IModelGateway gateway = scope.ServiceProvider.GetRequiredService<IModelGateway>();
        ServiceResult<ModelResponse> generation = await gateway.GenerateAsync(call, request, access,
            (_, _) => { callbacks++; return ValueTask.CompletedTask; }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(config.GetSection("CodexLb").Exists());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<CodexLbOptions>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(HttpApiClient));
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IModelSettingsReader>());
        Assert.Same(gateway, scope.ServiceProvider.GetRequiredService<IModelGateway>());
        AssertUnsupported(generation);
        AssertUnsupported(await gateway.CompactAsync(call, request, access, TestContext.Current.CancellationToken));
        AssertUnsupported(await scope.ServiceProvider.GetRequiredService<IModelCatalog>().ReadAsync(access, TestContext.Current.CancellationToken));
        AssertUnsupported(await scope.ServiceProvider.GetRequiredService<IModelAccessResolver>().ResolveAsync(owner, TestContext.Current.CancellationToken));
        IModelSettingsReader settings = scope.ServiceProvider.GetRequiredService<IModelSettingsReader>();
        AssertUnsupported(await settings.ReadAsync(owner, ct: TestContext.Current.CancellationToken));
        AssertUnsupported(await settings.ReadWithAccessAsync(owner, access, ct: TestContext.Current.CancellationToken));
        Assert.Equal(0, callbacks);
        using IServiceScope another = provider.CreateScope();
        Assert.NotSame(gateway, another.ServiceProvider.GetRequiredService<IModelGateway>());
        Assert.Equal("model", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Sub2ApiOptions>>().Value.Model);
    }

    /// <summary>Повтор выбора до/после facade идемпотентен; смена и несовместимые аргументы атомарно отклоняются.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatsAndConflictsDoNotMutateRegistrations(bool sub2Api)
    {
        // Arrange
        ServiceCollection services = Services();
        Select(services, sub2Api);
        ServiceDescriptor[] selected = services.ToArray();
        IConfiguration configuration = Config(sub2Api);
        Func<IServiceProvider, HttpClient> factory = _ => throw new InvalidOperationException("factory must not run");

        // Act
        Select(services, sub2Api);

        // Assert
        Assert.Equal(selected, services.ToArray());
        Assert.Throws<InvalidOperationException>(() => Select(services, !sub2Api));
        Assert.Equal(selected, services.ToArray());

        // Act
        services.AddAgentBridge(configuration, factory);
        ServiceDescriptor[] composed = services.ToArray();
        Select(services, sub2Api);
        services.AddAgentBridge(configuration, factory);

        // Assert
        Assert.Equal(composed, services.ToArray());
        Assert.Throws<InvalidOperationException>(() => Select(services, !sub2Api));
        Assert.Throws<InvalidOperationException>(() => services.AddAgentBridge(Config(sub2Api), factory));
        Assert.Throws<InvalidOperationException>(() => services.AddAgentBridge(configuration, _ => throw new InvalidOperationException()));
        Assert.Equal(composed, services.ToArray());
    }

    /// <summary>Неявный default также фиксирует выбор после facade.</summary>
    [Fact]
    public void ImplicitDefaultCannotChangeAfterFacade()
    {
        // Arrange
        ServiceCollection services = Services();
        services.AddAgentBridge(Config(false), _ => throw new InvalidOperationException());
        ServiceDescriptor[] before = services.ToArray();

        // Act
        Exception? error = Record.Exception(() => services.AddAgentBridgeSub2ApiConnector());
        services.AddAgentBridgeCodexLbConnector();

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(before, services.ToArray());
    }

    /// <summary>Прямые AddCodexLb порты нельзя случайно оставить внутри выбранного sub2api.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectCodexLbRegistrationsConflictBeforeIo(bool selectFirst)
    {
        // Arrange
        ServiceCollection services = Services();
        if (selectFirst)
        {
            services.AddAgentBridgeSub2ApiConnector();
        }

        services.AddCodexLbResponses(_ => throw new InvalidOperationException("factory must not run"));
        ServiceDescriptor[] before = services.ToArray();

        // Act
        Exception? error = Record.Exception(() =>
        {
            services.AddAgentBridgeSub2ApiConnector();
            services.AddAgentBridge(Config(true), _ => throw new InvalidOperationException());
        });

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(before, services.ToArray());
    }

    /// <summary>Direct регистрации codex-lb сохраняют прежнюю добавляющую семантику и сроки жизни.</summary>
    [Fact]
    public void DirectCodexLbCallsStillAppendScopedDescriptors()
    {
        // Arrange
        ServiceCollection services = new();
        Func<IServiceProvider, HttpClient> factory = _ => throw new InvalidOperationException();

        // Act
        services.AddCodexLbResponses(factory);
        services.AddCodexLbResponses(factory);

        // Assert
        Type[] ports = [typeof(IModelGateway), typeof(IModelCatalog), typeof(IModelAccessResolver), typeof(IModelSettingsReader), typeof(HttpApiClient)];
        foreach (Type port in ports)
        {
            ServiceDescriptor[] descriptors = services.Where(descriptor => descriptor.ServiceType == port).ToArray();
            Assert.Equal(2, descriptors.Length);
            Assert.All(descriptors, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
        }
    }

    /// <summary>Независимые custom порты и app HTTP сохраняются до и после обоих вариантов facade.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CustomOverridesAndOptionsPipelineRemainCompatible(bool sub2Api, bool before)
    {
        // Arrange
        ServiceCollection services = Services();
        Type[] ports = [typeof(IModelGateway), typeof(IModelCatalog), typeof(IModelAccessResolver), typeof(IModelSettingsReader)];
        Dictionary<Type, object> custom = ports.ToDictionary(type => type, type => DispatchProxy.Create(type, typeof(ContractProxy)));
        using HttpClient client = new();
        HttpApiClient http = new(client, Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpApiClient>.Instance);
        services.AddScoped(_ => http);
        if (before)
        {
            AddOverrides(services, custom);
        }

        Select(services, sub2Api);
        services.PostConfigure<Sub2ApiOptions>(options => options.Model = "overridden-model");

        // Act
        services.AddAgentBridge(Config(sub2Api), _ => throw new InvalidOperationException("factory must not run"));
        if (!before)
        {
            AddOverrides(services, custom);
        }

        using ServiceProvider provider = Build(services);
        using IServiceScope scope = provider.CreateScope();

        // Assert
        foreach (KeyValuePair<Type, object> pair in custom)
        {
            Assert.Same(pair.Value, scope.ServiceProvider.GetRequiredService(pair.Key));
        }

        Assert.Same(http, scope.ServiceProvider.GetRequiredService<HttpApiClient>());
        Assert.Equal("overridden-model", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Sub2ApiOptions>>().Value.Model);
    }

    /// <summary>Individual требует предварительный app source и не разрешает scoped source из root.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndividualSourceIsValidatedWithoutResolvingIt(bool hasSource)
    {
        // Arrange
        ServiceCollection services = Services();
        int sourceCreations = 0;
        if (hasSource)
        {
            services.AddScoped<IIndividualModelKeySource>(_ => { sourceCreations++; return new KeySource(); });
        }

        services.AddAgentBridgeSub2ApiConnector();
        IConfiguration config = Config(true, new() { ["Sub2Api:KeySource"] = "Individual", ["Sub2Api:SharedApiKey"] = null });

        // Act
        services.AddAgentBridge(config, _ => throw new InvalidOperationException());
        using ServiceProvider provider = Build(services);
        Exception? error = Record.Exception(() => provider.GetRequiredService<IStartupValidator>().Validate());

        // Assert
        Assert.Equal(0, sourceCreations);
        if (hasSource)
        {
            Assert.Null(error);
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IIndividualModelKeySource>());
            using IServiceScope scope = provider.CreateScope();
            Assert.IsType<KeySource>(scope.ServiceProvider.GetRequiredService<IIndividualModelKeySource>());
            Assert.Equal(1, sourceCreations);
        }
        else
        {
            Assert.IsType<OptionsValidationException>(error);
        }
    }

    /// <summary>Неисправные локальные options дают безопасный отказ до любых внешних операций.</summary>
    [Theory]
    [InlineData("Sub2Api:BaseAddress", "https://secret@host.invalid")]
    [InlineData("Sub2Api:KeySource", "unknown-secret")]
    [InlineData("Sub2Api:SharedApiKey", "bad secret")]
    [InlineData("Sub2Api:GenerationTimeout", "invalid-secret")]
    [InlineData("Sub2Api:CompactTimeout", "00:00:00")]
    public void InvalidSub2ApiOptionsFailSafely(string key, string value)
    {
        // Arrange
        ServiceCollection services = Services();
        services.AddAgentBridgeSub2ApiConnector();
        services.AddAgentBridge(Config(true, new() { [key] = value }), _ => throw new InvalidOperationException());
        using ServiceProvider provider = Build(services);

        // Act
        Exception? error = Record.Exception(() => provider.GetRequiredService<IStartupValidator>().Validate());

        // Assert
        Assert.IsType<OptionsValidationException>(error);
        Assert.DoesNotContain(value, error.Message);
        Assert.DoesNotContain("synthetic-key", error.ToString());
    }

    /// <summary>Выбирает public API без условных side effects в фабрике.</summary>
    private static void Select(IServiceCollection services, bool sub2Api)
    {
        if (sub2Api)
        {
            services.AddAgentBridgeSub2ApiConnector();
        }
        else
        {
            services.AddAgentBridgeCodexLbConnector();
        }
    }

    /// <summary>Подключает только app logging и disconnected SQL Server metadata, без соединения.</summary>
    private static ServiceCollection Services()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAgentBridgeSqlServer();
        return services;
    }

    /// <summary>Включает обе проверки graph/scopes без host.</summary>
    private static ServiceProvider Build(IServiceCollection services)
    {
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Синтетическая конфигурация содержит только выбранный transport section.</summary>
    private static IConfiguration Config(bool sub2Api, Dictionary<string, string?>? changes = null)
    {
        string section = "CodexLb";
        if (sub2Api)
        {
            section = "Sub2Api";
        }

        Dictionary<string, string?> values = new()
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "5", ["AgentBridge:Agent:InstructionsSource"] = "Configuration",
            ["AgentBridge:Agent:Instructions"] = "instructions", ["AgentBridge:Retention:RetentionPeriod"] = "14.00:00:00",
            ["AgentBridge:Retention:SoftContentLimitBytes"] = "10485760", ["AgentBridge:Compaction:TokenThreshold"] = "24000",
            ["AgentBridge:Compaction:InputTokenReserve"] = "0", ["AgentBridge:Compaction:MaxPasses"] = "3",
            [$"{section}:BaseAddress"] = "https://example.invalid/prefix", [$"{section}:Model"] = "model",
            [$"{section}:ReasoningEffort"] = "medium", [$"{section}:KeySource"] = "Shared",
            [$"{section}:SharedApiKey"] = "synthetic-key", [$"{section}:GenerationTimeout"] = "00:03:00",
            [$"{section}:CompactTimeout"] = "00:03:00", ["Database:Provider"] = "SqlServer",
            ["Database:ConnectionString"] = "Server=example.invalid;Database=synthetic;Integrated Security=True;Encrypt=True"
        };
        if (changes is not null)
        {
            foreach (KeyValuePair<string, string?> pair in changes)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>Регистрирует различимые контракты без выполнения методов.</summary>
    private static void AddOverrides(IServiceCollection services, Dictionary<Type, object> custom)
    {
        foreach (KeyValuePair<Type, object> pair in custom)
        {
            services.AddSingleton(pair.Key, pair.Value);
        }
    }

    /// <summary>Не допускает фиктивного успешного ответа подготовленного коннектора.</summary>
    private static void AssertUnsupported<T>(ServiceResult<T> result) where T : class
    {
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Null(result.Data);
    }

    /// <summary>Proxy используется исключительно как identity пользовательского override.</summary>
    public class ContractProxy : DispatchProxy
    {
        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            throw new InvalidOperationException("custom port must not run");
        }
    }

    /// <inheritdoc/>
    private class KeySource : IIndividualModelKeySource
    {
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<string?>("synthetic-individual-key");
        }
    }
}

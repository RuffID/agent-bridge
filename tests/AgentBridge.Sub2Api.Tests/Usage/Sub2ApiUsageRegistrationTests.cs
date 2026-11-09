using AgentBridge.Persistence.PostgreSql;
using System.Reflection;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Integration;
using AgentBridge.Sub2Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Публичное optional подключение usage через facade без host, сети и подключения к БД.</summary>
public class Sub2ApiUsageRegistrationTests
{
    /// <summary>Opt-in не меняет обязательную конфигурацию и не создаёт HTTP при регистрации/валидации/resolve gateway.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicCompositionIsOptionalAndLazy(bool auxiliary)
    {
        // Arrange
        ServiceCollection services = Services();
        services.AddAgentBridgeSub2ApiConnector();
        IConfiguration config = Config();
        using Sub2ApiCatalogFixture.HandlerDouble handler = new();
        handler.Respond = (_, _) => Task.FromResult(Sub2ApiCatalogFixture.Json("{\"mode\":\"unrestricted\"}"));
        using HttpClient client = new(handler);
        int factoryCalls = 0;

        // Act
        services.AddAgentBridge(config, _ =>
        {
            factoryCalls++;
            return client;
        });
        if (auxiliary)
        {
            services.AddAgentBridgeSub2ApiAuxiliary();
            ServiceDescriptor[] before = services.ToArray();
            services.AddAgentBridgeSub2ApiAuxiliary();

            // Assert
            Assert.Equal(before, services.ToArray());
        }

        // Act
        using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope scope = provider.CreateScope();
        IModelAuxiliaryGateway? gateway = scope.ServiceProvider.GetService<IModelAuxiliaryGateway>();

        // Assert
        Assert.Equal(0, factoryCalls);
        Assert.Equal(0, handler.Calls);
        Assert.False(config.GetSection("CodexLb").Exists());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<CodexLbOptions>));
        Assert.DoesNotContain(typeof(Sub2ApiOptions).Assembly.GetReferencedAssemblies(), assembly => assembly.Name == "AgentBridge.CodexLb");
        if (!auxiliary)
        {
            Assert.Null(gateway);
            return;
        }

        Assert.NotNull(gateway);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IModelAuxiliaryGateway>());
        Assert.Same(gateway, scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>());
        using IServiceScope second = provider.CreateScope();
        Assert.NotSame(gateway, second.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>());

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await gateway.ReadUsageAsync(new("selected-key"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(["Bearer selected-key"], handler.Authorizations);
        Assert.Equal(["https://gateway.invalid/prefix/v1/usage"], handler.Urls);
        Assert.False(handler.Disposed);

        // Arrange
        handler.Respond = (_, _) => Task.FromResult(Sub2ApiCatalogFixture.Json("{\"data\":[{\"b64_json\":\"aW1hZ2U=\"}]}"));

        // Act
        ServiceResult<ModelAuxiliaryResult> generated = await gateway.GenerateImageAsync(
            new() { Model = "chosen-image-model", Prompt = "synthetic prompt" }, new("selected-key"),
            TestContext.Current.CancellationToken);
        ServiceResult<ModelAuxiliaryResult> edited = await gateway.EditImageAsync(
            new() { Model = "chosen-image-model", Prompt = "synthetic prompt",
                Images = [new() { FileName = "input.png", Bytes = [1, 2, 3] }] }, new("selected-key"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(generated.Success);
        Assert.True(edited.Success);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(["https://gateway.invalid/prefix/v1/usage", "https://gateway.invalid/prefix/v1/images/generations",
            "https://gateway.invalid/prefix/v1/images/edits"], handler.Urls);
        Assert.Equal(["Bearer selected-key", "Bearer selected-key", "Bearer selected-key"], handler.Authorizations);
        Assert.False(handler.Disposed);
    }

    /// <summary>Неподдержанный upload отказывает до options, HTTP factory и проверки входных DTO.</summary>
    [Fact]
    public async Task UnsupportedMethodsNeverCreateHttp()
    {
        // Arrange
        ServiceCollection services = Services();
        services.AddAgentBridgeSub2ApiConnector();
        int factoryCalls = 0;
        services.AddAgentBridge(Config(), _ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("HTTP factory must not run");
        });
        services.AddAgentBridgeSub2ApiAuxiliary();
        using ServiceProvider provider = Build(services);
        using IServiceScope scope = provider.CreateScope();
        IModelAuxiliaryGateway gateway = scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>();
        using CancellationTokenSource canceled = new();
        canceled.Cancel();

        // Act
        ServiceResult<string> upload = await gateway.UploadFileAsync(null!, null!, canceled.Token);

        // Assert
        Assert.Equal(ServiceErrorType.Unsupported, upload.Error!.Type);
        Assert.Equal(0, factoryCalls);
    }

    /// <summary>Пользовательский port сохраняется при повторном optional подключении.</summary>
    [Fact]
    public void PreservesAppOverride()
    {
        // Arrange
        ServiceCollection services = Services();
        services.AddAgentBridgeSub2ApiConnector();
        IModelAuxiliaryGateway custom = DispatchProxy.Create<IModelAuxiliaryGateway, ConnectorSelectionTests.ContractProxy>();
        services.AddSingleton(custom);
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException("factory must not run"));

        // Act
        services.AddAgentBridgeSub2ApiAuxiliary();
        services.AddAgentBridgeSub2ApiAuxiliary();
        using ServiceProvider provider = Build(services);
        using IServiceScope scope = provider.CreateScope();

        // Assert
        Assert.Same(custom, scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IModelAuxiliaryGateway));
    }

    /// <summary>Без выбора Sub2Api подключение не меняет контейнер и не выбирает скрытый default.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsMissingOrCodexLbSelection(bool codexLb)
    {
        // Arrange
        ServiceCollection services = new();
        if (codexLb)
        {
            services.AddAgentBridgeCodexLbConnector();
        }

        ServiceDescriptor[] before = services.ToArray();

        // Act
        Exception? failure = Record.Exception(() => services.AddAgentBridgeSub2ApiAuxiliary());

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(before, services.ToArray());
    }

    /// <summary>Прямой CodexLb auxiliary не смешивается с Sub2Api до factory/HTTP.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsMixedBuiltInAuxiliary(bool selectFirst)
    {
        // Arrange
        ServiceCollection services = new();
        if (selectFirst)
        {
            services.AddAgentBridgeSub2ApiConnector();
        }

        services.AddCodexLbAuxiliary(new ConfigurationBuilder().Build());
        ServiceDescriptor[] before = services.ToArray();

        // Act
        Exception? failure = Record.Exception(() =>
        {
            if (selectFirst)
            {
                services.AddAgentBridgeSub2ApiAuxiliary();
            }
            else
            {
                services.AddAgentBridgeSub2ApiConnector();
            }
        });

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(before, services.ToArray());
    }

    /// <summary>Создаёт только logging и disconnected provider graph.</summary>
    private static ServiceCollection Services()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAgentBridgePostgreSql();

        return services;
    }

    /// <summary>Проверяет scopes/graph без запуска host.</summary>
    private static ServiceProvider Build(IServiceCollection services)
    {
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Не добавляет Auxiliary или фиктивный CodexLb раздел.</summary>
    private static IConfiguration Config()
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "5", ["AgentBridge:Agent:InstructionsSource"] = "Configuration",
            ["AgentBridge:Agent:Instructions"] = "instructions", ["AgentBridge:Retention:RetentionPeriod"] = "14.00:00:00",
            ["AgentBridge:Retention:SoftContentLimitBytes"] = "10485760", ["AgentBridge:Compaction:TokenThreshold"] = "24000",
            ["AgentBridge:Compaction:InputTokenReserve"] = "0", ["AgentBridge:Compaction:MaxPasses"] = "3",
            ["Sub2Api:BaseAddress"] = "https://gateway.invalid/prefix/v1/", ["Sub2Api:Model"] = "model",
            ["Sub2Api:ReasoningEffort"] = "medium", ["Sub2Api:KeySource"] = "Shared",
            ["Sub2Api:SharedApiKey"] = "synthetic-key", ["Sub2Api:GenerationTimeout"] = "00:00:30",
            ["Sub2Api:CompactTimeout"] = "00:00:30", ["Database:Provider"] = "PostgreSql",
            ["Database:ConnectionString"] = "Host=invalid.example;Database=synthetic"
        }).Build();
    }
}

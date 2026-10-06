using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Integration;
using AgentBridge.Persistence.EfCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentBridge.RuntimeProbe;

/// <summary>Actual facade, options, app logging и embedded BPE без операций хранения/транспорта.</summary>
public static class ProbeChecks
{
    /// <summary>Проверяет positive composition, scopes и два словаря; malformed configuration отклоняется.</summary>
    public static async Task<object> RunAsync(string databaseProvider)
    {
        using ProbeLoggerProvider sink = new();
        using ILoggerFactory logger = LoggerFactory.Create(builder => builder.AddProvider(sink));
        using BlockingHandler handler = new();
        using HttpClient client = new(handler);
        int factoryCalls = 0;
        Func<IServiceProvider, HttpClient> factory = _ => { factoryCalls++; return client; };
        ServiceCollection services = new();
        services.AddSingleton(logger);
        IConfiguration configuration = Configuration(databaseProvider);
        services.AddAgentBridge(configuration, factory);
        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        root.GetRequiredService<IStartupValidator>().Validate();
        if (factoryCalls != 0) throw new InvalidOperationException("Startup invoked HTTP factory.");
        await using AsyncServiceScope first = root.CreateAsyncScope();
        await using AsyncServiceScope second = root.CreateAsyncScope();
        AgentBridgeDbContext context = first.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
        if (ReferenceEquals(context, second.ServiceProvider.GetRequiredService<AgentBridgeDbContext>()))
            throw new InvalidOperationException("DbContext escaped its scope.");
        string expectedProvider = databaseProvider switch
        {
            "SqlServer" => "Microsoft.EntityFrameworkCore.SqlServer",
            "Sqlite" => "Microsoft.EntityFrameworkCore.Sqlite",
            "PostgreSql" => "Npgsql.EntityFrameworkCore.PostgreSQL",
            _ => throw new ArgumentException("Unknown provider.")
        };
        if (context.Database.ProviderName != expectedProvider) throw new InvalidOperationException("Wrong provider.");
        _ = first.ServiceProvider.GetRequiredService<AgentRunner>();
        _ = first.ServiceProvider.GetRequiredService<AgentSettingsService>();
        _ = first.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        _ = first.ServiceProvider.GetRequiredService<ContextCompactor>();
        _ = first.ServiceProvider.GetRequiredService<IDialogReader>();
        if (!ReferenceEquals(logger, root.GetRequiredService<ILoggerFactory>())) throw new InvalidOperationException("App logger replaced.");
        first.ServiceProvider.GetRequiredService<ILogger<AgentRunner>>().LogInformation("Runtime probe marker");
        if (sink.Messages != 1) throw new InvalidOperationException("App logging did not receive marker.");
        List<object> counts = [];
        foreach ((string model, string encoding) in new[] { ("gpt-4.1", "o200k_base"), ("gpt-4", "cl100k_base") })
        {
            ServiceResult<ContextTokenCount> result = await first.ServiceProvider.GetRequiredService<IContextTokenCounter>()
                .CountAsync(new ModelRequest(model, null, "Hello world", [], []));
            if (!result.Success || result.Data is not ContextTokenCount count || count.Encoding != encoding
                || count.KnownTokens != 2 || count.EstimatedInputTokens is null || count.HasOpaqueContent)
                throw new InvalidOperationException("Embedded BPE result mismatch.");
            counts.Add(new { model, count.Encoding, count.KnownTokens, count.EstimatedInputTokens });
        }
        bool rejected = false;
        ServiceCollection invalid = new();
        invalid.AddSingleton(logger);
        try
        {
            invalid.AddAgentBridge(Configuration(databaseProvider, omitMaxSteps: true), factory);
            await using ServiceProvider invalidRoot = invalid.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            invalidRoot.GetRequiredService<IStartupValidator>().Validate();
        }
        catch (OptionsValidationException) { rejected = true; }
        if (!rejected || handler.Calls != 0) throw new InvalidOperationException("Invalid options accepted or HTTP attempted.");
        return new { expectedProvider, scopesDistinct = true, appLoggerPreserved = true, messages = sink.Messages,
            invalidConfigurationRejected = rejected, httpFactoryCalls = factoryCalls, httpCalls = handler.Calls, counts };
    }

    /// <summary>Создаёт полный синтетический app config; connection string никогда не используется для подключения.</summary>
    private static IConfiguration Configuration(string provider, bool omitMaxSteps = false)
    {
        Dictionary<string, string?> values = new()
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "5", ["AgentBridge:Agent:InstructionsSource"] = "Configuration", ["AgentBridge:Agent:Instructions"] = "instructions",
            ["AgentBridge:Retention:RetentionPeriod"] = "14.00:00:00", ["AgentBridge:Retention:SoftContentLimitBytes"] = "10485760",
            ["AgentBridge:Compaction:TokenThreshold"] = "24000", ["AgentBridge:Compaction:InputTokenReserve"] = "0", ["AgentBridge:Compaction:MaxPasses"] = "3",
            ["CodexLb:BaseAddress"] = "https://example.invalid", ["CodexLb:Model"] = "gpt-4.1", ["CodexLb:ReasoningEffort"] = "medium",
            ["CodexLb:KeySource"] = "Shared", ["CodexLb:SharedApiKey"] = "synthetic-key", ["CodexLb:GenerationTimeout"] = "00:03:00", ["CodexLb:CompactTimeout"] = "00:03:00",
            ["Database:Provider"] = provider, ["Database:ConnectionString"] = provider switch
            {
                "SqlServer" => "Server=example.invalid;Database=synthetic;Encrypt=True",
                "Sqlite" => "Data Source=never-opened.db",
                "PostgreSql" => "Host=example.invalid;Database=synthetic;Username=synthetic",
                _ => throw new ArgumentException("Unknown provider.")
            }
        };
        if (omitMaxSteps) values.Remove("AgentBridge:Agent:MaxToolSteps");
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}

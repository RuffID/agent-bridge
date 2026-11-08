using System.Security.Cryptography;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Integration;
using AgentBridge.Persistence.PostgreSql;
using AgentBridgeConsole;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.LiveAcceptance;

/// <summary>Две явно запускаемые фазы live-приёмки без host, maintenance или автоматического восстановления.</summary>
internal static class Program
{
    /// <summary>Принимает фазу, явный каталог конфигурации консоли и путь эксклюзивного безопасного журнала.</summary>
    public static async Task<int> Main(string[] args)
    {
        AcceptanceJournal? journal = null;
        try
        {
            AcceptanceJournal.Require(args.Length == 3, "arguments_required");
            AcceptanceJournal.Require(args[0] is "json" or "sse", "phase_invalid");
            bool streaming = args[0] == "sse";
            IConfigurationRoot configuration = ConsoleConfiguration.Load(Path.GetFullPath(args[1]), false,
                new EnvironmentVariablesConfigurationSource { Prefix = "ABC_" });
            using IDisposable configurationLifetime = (IDisposable)configuration;
            string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                configuration.AsEnumerable().OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray())));
            journal = new AcceptanceJournal(Path.GetFullPath(args[2]), streaming, hash);
            string runtimeHash = HashRuntime();
            if (streaming)
            {
                AcceptanceJournal.Require(journal.Evidence.RuntimeHash == runtimeHash, "runtime_changed");
            }

            journal.Evidence.RuntimeHash = runtimeHash;
            journal.Save();
            await RunAsync(configuration, journal, streaming);
            Console.WriteLine($"Phase={journal.Evidence.Phase}; generations={journal.Evidence.Generations}; " +
                $"catalogs={journal.Evidence.Catalogs}; probe={journal.Evidence.ProbeExecutions}");
            return 0;
        }
        catch (Exception error)
        {
            // Даже неожиданные driver/config/HTTP исключения не выводятся целиком.
            string type = error.GetType().Name;
            if (journal is not null)
            {
                journal.Evidence.FailureType = type;
                if (error is AcceptanceCheckException check)
                {
                    journal.Evidence.FailureCode = check.Code;
                }

                journal.Evidence.Phase = "failed";
                journal.Save();
            }

            Console.Error.WriteLine($"Acceptance stopped; type={type}. No retry is permitted.");
            return 1;
        }
        finally
        {
            journal?.Dispose();
        }
    }

    /// <summary>Создаёт новый DI/HTTP lifetime для каждой фазы и читает только public catalog API.</summary>
    private static async Task RunAsync(IConfiguration configuration, AcceptanceJournal journal, bool streaming)
    {
        ConsoleIdentity identity = ConsoleIdentity.Read(configuration);
        AcceptanceJournal.Require(configuration["Sub2Api:KeySource"] == "Shared", "key_source_changed");
        string model = configuration["Sub2Api:Model"] ?? throw new InvalidOperationException("model_missing");
        string effort = configuration["Sub2Api:ReasoningEffort"] ?? throw new InvalidOperationException("effort_missing");
        Uri endpoint = new(configuration["Sub2Api:BaseAddress"] ?? throw new InvalidOperationException("endpoint_missing"));
        using AcceptanceHttpHandler handler = new(journal, endpoint, model, effort, streaming);
        using HttpClient http = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        ServiceCollection registrations = new();
        registrations.AddLogging();
        registrations.AddSingleton(identity);
        registrations.AddSingleton(journal);
        registrations.AddTransient<ProbeTool>();
        registrations.AddSingleton<IDialogCatalogProjector, ConsoleCatalogProjector>();
        registrations.AddAgentBridgeTool<AcceptanceProbe, ProbeToolValidator>(ProbeTool.ToolDefinition);
        registrations.AddAgentBridgePostgreSql();
        registrations.AddAgentBridgeSub2ApiConnector();
        registrations.AddAgentBridge(configuration, _ => http);
        await using ServiceProvider root = registrations.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(10));
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        CancellationToken ct = deadline.Token;
        if (!streaming)
        {
            await CreateAsync(services, identity, journal, ct);
        }

        DialogSnapshot before = await ReadAsync(services, identity, journal, ct);
        IReadOnlyList<CanonicalModelItem> previous = before.Turns.SelectMany(turn => turn.Items).ToArray();
        if (streaming)
        {
            AcceptanceJournal.Require(before.Turns.Count == 1, "restart_turn_count");
            AcceptanceJournal.Require(AcceptanceJournal.Hash([JsonSerializer.SerializeToElement(before.Turns[0])])
                == journal.Evidence.FirstTurnHash, "restart_history_changed");
            journal.Evidence.Checks["RestartHistoryUnchanged"] = true;
        }
        else
        {
            AcceptanceJournal.Require(before.Turns.Count == 0, "new_dialog_not_empty");
        }

        string text = "Проверка инструмента: вызови console_probe ровно один раз с пустым объектом. " +
            "После результата ответь только его executionId. Никаких других вызовов.";
        if (streaming)
        {
            text = "Продолжаем прежний диалог после перезапуска приложения. Не вызывай инструменты. " +
                "Ответь только executionId уже сохранённого результата console_probe из предыдущего обращения.";
        }

        CanonicalModelItem input = new(JsonSerializer.SerializeToElement(new
        {
            type = "message", role = "user", content = new[] { new { type = "input_text", text } }
        }));
        handler.RequiredPrefix = previous.Select(item => item.Content).Append(input.Content).ToArray();
        ApplicationCallContext call = new(DialogId.From(journal.Evidence.DialogId), identity.Scope.OwnerId,
            Guid.NewGuid(), identity.Scope.AgentId);
        AgentRunRequest request = new(call, [input], [ProbeTool.ToolDefinition.Name],
            new ToolExecutionLimits(4, 1, 1, TimeSpan.FromMinutes(10)),
            new DialogRunOptions(identity.Scope, identity.Profile, TimeSpan.FromMinutes(15)));
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null;
        if (streaming)
        {
            callback = (update, token) =>
            {
                token.ThrowIfCancellationRequested();
                journal.Evidence.StreamUpdates++;
                return ValueTask.CompletedTask;
            };
        }

        AgentRunResult result = await services.GetRequiredService<AgentRunner>().RunAsync(request, callback, ct);
        journal.Evidence.Checks["Completed_" + PhaseName(streaming)] = result.Status == AgentRunStatus.Completed;
        journal.Evidence.Checks["TerminalSaved_" + PhaseName(streaming)] = result.TerminalSaved;
        journal.Evidence.FailureCode = result.Error?.Type.ToString();
        journal.Save();
        AcceptanceJournal.Require(result.Status == AgentRunStatus.Completed && result.TerminalSaved, "run_not_completed");
        AcceptanceJournal.Require(result.LastResponse?.Status == ModelResponseStatus.Completed, "model_not_completed");
        ContextBudgetAssessment budget = result.LastBudgetAssessment ?? throw new InvalidOperationException("budget_missing");
        journal.Evidence.DeclaredInputWindows.Add(budget.DeclaredInputContextWindow);
        journal.Evidence.RequiresServerValidation.Add(budget.RequiresServerValidation);
        journal.Evidence.Checks["BudgetChecked_" + PhaseName(streaming)] = true;

        DialogSnapshot after = await ReadAsync(services, identity, journal, ct);
        VerifyHistory(after, result, journal, streaming);
        ServiceResult<ModelResponse> compact = await services.GetRequiredService<IModelGateway>().CompactAsync(call,
            new ModelRequest(model, effort, string.Empty, [input], []),
            new ModelAccess(configuration["Sub2Api:SharedApiKey"] ?? throw new InvalidOperationException("key_missing")), ct);
        AcceptanceJournal.Require(!compact.Success && compact.Error?.Type == ServiceErrorType.Unsupported, "compact_not_unsupported");
        journal.Evidence.Checks["CompactUnsupported"] = true;
        journal.Evidence.Checks["FullContextWithoutContinuation"] = true;
        journal.Evidence.Phase = "json-complete";
        AcceptanceJournal.Require(journal.Evidence.Catalogs == ExpectedCatalogs(streaming), "catalog_count_unexpected");
        if (streaming)
        {
            AcceptanceJournal.Require(journal.Evidence.StreamUpdates > 0, "stream_updates_missing");
            AcceptanceJournal.Require(journal.Evidence.Catalogs == 2, "catalog_count_unexpected");
            journal.Evidence.Phase = "sse-complete";
        }

        journal.Save();
    }

    /// <summary>Даёт фиксированный безопасный суффикс проверки.</summary>
    private static string PhaseName(bool streaming)
    {
        if (streaming)
        {
            return "sse";
        }

        return "json";
    }

    /// <summary>Фиксирует DLL runtime, чтобы второй процесс использовал тот же комплект.</summary>
    private static string HashRuntime()
    {
        string[] hashes = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Order(StringComparer.Ordinal).Select(path =>
            {
                return Path.GetFileName(path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            }).ToArray();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(hashes)));
    }

    /// <summary>Каждая фаза читает ровно один manifest в штатном settings path.</summary>
    private static int ExpectedCatalogs(bool streaming)
    {
        if (streaming)
        {
            return 2;
        }

        return 1;
    }

    /// <summary>Создаёт только новый тестовый диалог; схема и прежние диалоги не изменяются.</summary>
    private static async Task CreateAsync(IServiceProvider services, ConsoleIdentity identity,
        AcceptanceJournal journal, CancellationToken ct)
    {
        DateTimeOffset now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        DialogRetentionOptions retention = services.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value;
        journal.Evidence.DialogId = Guid.NewGuid();
        journal.Save();
        DialogCatalogCreate create = new(DialogId.From(journal.Evidence.DialogId), identity.Scope, now,
            retention.CalculateExpiresAtUtc(now), identity.Profile,
            new DialogProjectionPolicy(ConsoleCatalogProjector.PROJECTOR_KEY, 1, 80, 160, 1048576));
        ServiceResult<DialogCatalogState> result = await services.GetRequiredService<IDialogCatalogCreator>().CreateAsync(create, ct);
        AcceptanceJournal.Require(result.Success, "dialog_create_failed");
    }

    /// <summary>Требует Ready и отсутствие compact/recovery перед продолжением и после terminal save.</summary>
    private static async Task<DialogSnapshot> ReadAsync(IServiceProvider services, ConsoleIdentity identity,
        AcceptanceJournal journal, CancellationToken ct)
    {
        DialogAccess access = new(DialogId.From(journal.Evidence.DialogId), identity.Scope.OwnerId,
            services.GetRequiredService<TimeProvider>().GetUtcNow());
        ServiceResult<DialogSnapshot> read = await services.GetRequiredService<IDialogReader>()
            .ReadCatalogAsync(new DialogCatalogAccess(access, identity.Scope, identity.Profile), ct);
        AcceptanceJournal.Require(read.Success, "dialog_read_failed");
        DialogSnapshot snapshot = read.Data!;
        AcceptanceJournal.Require(snapshot.Catalog is not null && identity.Matches(snapshot.Catalog), "identity_changed");
        AcceptanceJournal.Require(snapshot.Continuation?.Readiness == DialogReadiness.Ready, "dialog_not_ready");
        AcceptanceJournal.Require(snapshot.ActiveContext is null, "unexpected_compact");
        AcceptanceJournal.Require(snapshot.Continuation!.RecoveryRevision == 0, "unexpected_recovery");
        return snapshot;
    }

    /// <summary>Сверяет canonical tool пару, durable попытку, ответ и отправленный полный input без вывода payload.</summary>
    private static void VerifyHistory(DialogSnapshot snapshot, AgentRunResult result,
        AcceptanceJournal journal, bool streaming)
    {
        int expectedTurns = 1;
        if (streaming)
        {
            expectedTurns = 2;
        }

        AcceptanceJournal.Require(snapshot.Turns.Count == expectedTurns, "saved_turn_count");
        StoredDialogTurn first = snapshot.Turns[0];
        JsonElement[] outputs = first.Items.Select(item => item.Content)
            .Where(item => item.GetProperty("type").GetString() == "function_call_output").ToArray();
        AcceptanceJournal.Require(outputs.Length == 1 && journal.Evidence.ProbeExecutions == 1, "probe_count");
        JsonElement output = outputs[0];
        JsonElement[] calls = first.Items.Select(item => item.Content)
            .Where(item => item.GetProperty("type").GetString() == "function_call").ToArray();
        AcceptanceJournal.Require(calls.Length == 1, "call_count");
        AcceptanceJournal.Require(calls[0].GetProperty("name").GetString() == ProbeTool.ToolDefinition.Name, "tool_changed");
        AcceptanceJournal.Require(calls[0].GetProperty("call_id").GetString() == output.GetProperty("call_id").GetString(), "tool_pair_changed");
        StoredToolAttempt[] attempts = first.ModelSteps.SelectMany(step => step.ToolAttempts).ToArray();
        AcceptanceJournal.Require(attempts.Length == 1 && attempts[0].State == ToolAttemptState.Succeeded, "tool_not_saved");
        using JsonDocument probe = JsonDocument.Parse(output.GetProperty("output").GetString()!);
        string executionId = probe.RootElement.GetProperty("executionId").GetString()!;
        AcceptanceJournal.Require(Guid.TryParse(executionId, out _), "probe_result_invalid");
        string visible = string.Join("", result.LastResponse!.Output.Select(item => VisibleText.ReadMessage(item.Content)));
        AcceptanceJournal.Require(visible.Contains(executionId, StringComparison.OrdinalIgnoreCase), "saved_probe_not_recalled");
        CanonicalModelItem[] allItems = snapshot.Turns.SelectMany(turn => turn.Items).ToArray();
        string inputHash = AcceptanceJournal.Hash(allItems.SkipLast(result.LastResponse.Output.Count).Select(item => item.Content));
        AcceptanceJournal.Require(inputHash == journal.Evidence.InputHashes.Last(), "sent_history_differs");
        if (!streaming)
        {
            journal.Evidence.FirstTurnHash = AcceptanceJournal.Hash([JsonSerializer.SerializeToElement(first)]);
        }
        else
        {
            AcceptanceJournal.Require(AcceptanceJournal.Hash([JsonSerializer.SerializeToElement(first)])
                == journal.Evidence.FirstTurnHash, "saved_first_turn_changed");
            AcceptanceJournal.Require(snapshot.Turns[1].ModelSteps.All(step => step.ToolAttempts.Count == 0), "tool_replayed");
        }

        StoredModelStep lastStep = snapshot.Turns.Last().ModelSteps.Last();
        AcceptanceJournal.Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(lastStep.Response),
            JsonSerializer.SerializeToElement(result.LastResponse)), "saved_response_changed");
        AcceptanceJournal.Require(snapshot.Turns.SelectMany(turn => turn.ModelSteps)
            .All(step => step.Response.ConnectorProvenance?.ConnectorId == "sub2api"), "saved_provenance_missing");
        journal.Evidence.Checks["SavedHistoryAndToolPair_" + PhaseName(streaming)] = true;
    }
}

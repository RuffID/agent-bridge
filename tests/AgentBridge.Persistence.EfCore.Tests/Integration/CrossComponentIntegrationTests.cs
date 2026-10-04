using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AgentBridge.Persistence.EfCore.Tests.Integration.CrossComponentFixture;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Адресные проверки actual runner + HTTP adapter + BPE + durable storage без сети и hosting.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class CrossComponentIntegrationTests
{
    /// <summary>JSON/SSE сохраняют полные repeated pairs; Started виден handler, restart не повторяет side effects.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, false)]
    [InlineData(DatabaseProvider.SQLite, true)]
    [InlineData(DatabaseProvider.PostgreSql, false)]
    [InlineData(DatabaseProvider.PostgreSql, true)]
    public async Task RepeatedPairsRoundTripThroughActualTransportAndRestart(DatabaseProvider provider, bool stream)
    {
        await using IntegrationDatabase database = new(provider);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        Enqueue(fixture, stream, Function());
        Enqueue(fixture, stream, Function());
        Enqueue(fixture, stream, Message("Готово 😀", "assistant"));
        fixture.Handler.AfterCatalog = () => fixture.Keys.Key = "changed-after-resolve";
        AgentRunResult result = await fixture.RunAsync(stream);
        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(2, fixture.Actions);
        Assert.Equal(1, fixture.ProviderCalls);
        Assert.Equal(1, fixture.Keys.Calls);
        Assert.All(fixture.Handler.Requests, request => Assert.Equal(INDIVIDUAL, request.Key));
        JsonElement lastInput = fixture.Handler.Requests.Last().Body!.Value.GetProperty("input");
        Assert.Equal(2, lastInput.EnumerateArray().Count(item => item.GetProperty("type").GetString() == "function_call"));
        Assert.Equal(2, lastInput.EnumerateArray().Count(item => item.GetProperty("type").GetString() == "function_call_output"));
        Assert.Equal("developer", lastInput[0].GetProperty("role").GetString());
        Assert.All(fixture.Handler.Requests.Where(request => request.Body is not null), request =>
        {
            Assert.Equal("fixed instructions", request.Body!.Value.GetProperty("instructions").GetString());
            Assert.False(request.Body.Value.GetProperty("store").GetBoolean());
        });
        fixture.BuildRoot();
        DialogSnapshot saved = await fixture.ReadAsync();
        Assert.Equal(NOW.AddHours(37), saved.ExpiresAtUtc);
        StoredDialogTurn turn = Assert.Single(saved.Turns);
        Assert.Equal(3, turn.ModelSteps.Count);
        Assert.Equal(3, turn.ModelSteps.Select(step => step.StepId).Distinct().Count());
        Assert.All(turn.ModelSteps.Take(2), step => Assert.Equal(ToolAttemptState.Succeeded, Assert.Single(step.ToolAttempts).State));
        Assert.Equal("{\"nested\":[1,2]}", turn.ModelSteps[0].Response.Envelope!.Content.GetProperty("future").GetRawText());
        Assert.Equal(2, turn.Items.Count(item => Type(item) == "function_call_output"));
        Assert.Equal("gpt-5", turn.Settings!.Model);
        Assert.Equal("medium", turn.Settings.Effort);
        int requests = fixture.Handler.Requests.Count;
        Assert.Equal(AgentRunStatus.Interrupted, (await fixture.RunAsync(stream)).Status);
        Assert.Equal(requests, fixture.Handler.Requests.Count);
        Assert.Equal(2, fixture.Actions);
        Assert.DoesNotContain(INDIVIDUAL, await database.FingerprintAsync());
        Assert.DoesNotContain(SHARED, await database.FingerprintAsync());
    }

    /// <summary>EOF после partial arguments сохраняет call, запрещает handler/replay/следующий input без fake output.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task PartialSsePersistsAndBlocksNextRequest(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        const string PARTIAL = "data: {\"type\":\"response.output_item.added\",\"output_index\":0,\"item\":{\"type\":\"function_call\",\"id\":\"fc\",\"call_id\":\"same\",\"name\":\"action\",\"arguments\":\"\"}}\n\n"
            + "data: {\"type\":\"response.function_call_arguments.delta\",\"output_index\":0,\"item_id\":\"fc\",\"delta\":\"{\\\"id\\\":\"}\n\n";
        fixture.Handler.Responses.Enqueue(("/prefix/v1/responses", PARTIAL, true));
        AgentRunResult result = await fixture.RunAsync(stream: true);
        Assert.Equal(AgentRunStatus.Incomplete, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(0, fixture.Actions);
        fixture.BuildRoot();
        DialogSnapshot saved = await fixture.ReadAsync();
        Assert.Equal("{\"id\":", saved.Turns[0].ModelSteps[0].Response.Output[0].Content.GetProperty("arguments").GetString());
        Assert.Empty(saved.Turns[0].ModelSteps[0].ToolAttempts);
        Assert.DoesNotContain(saved.Turns[0].Items, item => Type(item) == "function_call_output");
        int requests = fixture.Handler.Requests.Count;
        Assert.Equal(AgentRunStatus.Interrupted, (await fixture.RunAsync()).Status);
        Assert.Equal(requests, fixture.Handler.Requests.Count);
        AgentRunResult next = await fixture.RunAsync(call: Next(fixture));
        Assert.Equal(ServiceErrorType.Conflict, next.Error!.Type);
        Assert.Single(fixture.Handler.Requests, request => request.Path.EndsWith("/responses", StringComparison.Ordinal));
        Assert.Single((await fixture.ReadAsync()).Turns);
        Assert.Equal(0, fixture.Actions);
    }

    /// <summary>Порог actual BPE запускает compact только terminal prefix; opaque окно сохраняется, generation запрещена, backup сохраняет новую схему.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task OpaqueCompactAndNewMetadataSurviveActualBackupRestore(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        await fixture.SelectAsync("gpt-5", "high");
        Enqueue(fixture, false, Function());
        Enqueue(fixture, false, Message(string.Join(' ', Enumerable.Repeat("история", 400)), "assistant"));
        Assert.Equal(AgentRunStatus.Completed, (await fixture.RunAsync()).Status);
        DialogSnapshot before = await fixture.ReadAsync();
        fixture.BuildRoot(threshold: 100);
        const string COMPACT = "{\"object\":\"response.compaction\",\"id\":\"compact-not-generation-anchor\",\"model\":\"actual-compact-server\",\"future\":[1,2],\"output\":[{\"type\":\"compaction\",\"encrypted_content\":\"opaque-stage23\",\"future\":{\"keep\":true}}]}";
        fixture.Handler.Responses.Enqueue(("/prefix/v1/responses/compact", COMPACT, false));
        AgentRunResult result = await fixture.RunAsync(call: Next(fixture));
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.True(result.TerminalSaved);
        Assert.Equal(2, fixture.ProviderCalls);
        Assert.Equal(1, fixture.Actions);
        Assert.Equal(2, fixture.Handler.Requests.Count(request => request.Path == "/prefix/v1/responses"));
        JsonElement compactRequest = Assert.Single(fixture.Handler.Requests, request => request.Path.EndsWith("/compact", StringComparison.Ordinal)).Body!.Value;
        Assert.False(compactRequest.TryGetProperty("tools", out _));
        Assert.Equal(before.Turns[0].Items.Select(item => item.Content.GetRawText()),
            compactRequest.GetProperty("input").EnumerateArray().Select(item => item.GetRawText()));
        fixture.BuildRoot();
        DialogSnapshot saved = await fixture.ReadAsync();
        Assert.Equal(1, saved.ActiveContext!.Version);
        Assert.Equal(1, saved.ActiveContext.ThroughTurnSequence);
        Assert.Equal("gpt-5", saved.ActiveContext.SelectedModel);
        Assert.Equal("opaque-stage23", saved.ActiveContext.Items[0].Content.GetProperty("encrypted_content").GetString());
        Assert.Null(saved.ActiveContext.Compaction.Continuation);
        Assert.Equal(before.ExpiresAtUtc, saved.ExpiresAtUtc);
        Assert.Equal(before.Turns[0].Items.Select(item => item.Content.GetRawText()), saved.Turns[0].Items.Select(item => item.Content.GetRawText()));
        Assert.Equal("high", saved.Selection!.Effort);
        Assert.All(saved.Turns, turn => Assert.NotNull(turn.Settings));
        await VerifyBackupAsync(database, fixture, saved);
    }

    /// <summary>Принятый text compact используется следующим actual запросом вместе с provider/new input, без envelope или anchor.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task TextCompactContinuesWithExactlyOneWindowProviderAndCurrentTurn(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        Enqueue(fixture, false, Message(string.Join(' ', Enumerable.Repeat("история", 400)), "assistant"));
        Assert.Equal(AgentRunStatus.Completed, (await fixture.RunAsync()).Status);
        DialogSnapshot before = await fixture.ReadAsync();
        fixture.BuildRoot(threshold: 250);
        fixture.Handler.Responses.Enqueue(("/prefix/v1/responses/compact", JsonSerializer.Serialize(new
        {
            @object = "response.compaction", id = "compact-only", model = "actual-compact-server",
            output = new[] { Message("short window", "assistant").Content }
        }), false));
        Enqueue(fixture, true, Message("after compact", "assistant"));
        int keys = fixture.Keys.Calls;
        fixture.Handler.AfterCatalog = () => fixture.Keys.Key = "changed-during-compact-run";
        AgentRunResult result = await fixture.RunAsync(stream: true, call: Next(fixture));
        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(keys + 1, fixture.Keys.Calls);
        Assert.Equal(2, fixture.ProviderCalls);
        JsonElement sent = fixture.Handler.Requests.Last().Body!.Value;
        Assert.Equal(new[] { Message("provider text", "developer").Content.GetRawText(), Message("short window", "assistant").Content.GetRawText(), Message("new input").Content.GetRawText() },
            sent.GetProperty("input").EnumerateArray().Select(item => item.GetRawText()));
        Assert.Single(sent.GetProperty("tools").EnumerateArray());
        Assert.Equal("fixed instructions", sent.GetProperty("instructions").GetString());
        Assert.False(sent.TryGetProperty("previous_response_id", out _));
        Assert.All(fixture.Handler.Requests, request => Assert.Equal(INDIVIDUAL, request.Key));
        DialogSnapshot saved = await fixture.ReadAsync();
        Assert.Equal(1, saved.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(1, saved.ActiveContext.Version);
        Assert.Equal(before.Turns[0].Items.Select(item => item.Content.GetRawText()), saved.Turns[0].Items.Select(item => item.Content.GetRawText()));
        Assert.Equal(before.ExpiresAtUtc, saved.ExpiresAtUtc);
        Assert.Equal("gpt-5", saved.ActiveContext.SelectedModel);
        Assert.Null(saved.ActiveContext.Compaction.Continuation);
    }

    /// <summary>Actual BPE полного prepared payload допускает equality и отклоняет один лишний токен по input_context_window.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualFullBudgetEqualityAndOneTokenExcess(bool excess)
    {
        await using IntegrationDatabase database = new(DatabaseProvider.SQLite);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        fixture.BuildRoot(threshold: 1);
        long estimate;
        await using (AsyncServiceScope scope = fixture.Root.CreateAsyncScope())
        {
            ModelRequest prepared = new("gpt-5", "medium", "fixed instructions",
                [Message("provider text", "developer"), Message("new input")], [TOOL]);
            ServiceResult<ContextTokenCount> count = await scope.ServiceProvider.GetRequiredService<IContextTokenCounter>().CountAsync(prepared);
            Assert.True(count.Success);
            estimate = count.Data!.EstimatedInputTokens!.Value;
        }
        fixture.Handler.InputWindow = checked((int)estimate + 10 - (excess ? 1 : 0));
        if (!excess) Enqueue(fixture, false, Message("fits", "assistant"));
        AgentRunResult result = await fixture.RunAsync();
        Assert.Equal(excess ? AgentRunStatus.Failed : AgentRunStatus.Completed, result.Status);
        Assert.True(result.TerminalSaved);
        if (excess)
        {
            Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
            Assert.DoesNotContain(fixture.Handler.Requests, request => request.Body is not null);
        }
        Assert.Equal(fixture.Handler.InputWindow, (await fixture.ReadAsync()).Turns[0].Settings!.InputContextWindow);
    }

    /// <summary>Independent settings update во время handler не меняет active model/effort/access; новый run видит выбор.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task ActiveRunPinsSettingsAndAccessWhileNextRunUsesSavedSelection(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        await fixture.SelectAsync("gpt-4.1", "low");
        fixture.Handler.Requests.Clear();
        fixture.Keys.Calls = 0;
        fixture.Action = async (_, _) =>
        {
            fixture.Keys.Key = "stage23-new-key";
            await fixture.SelectAsync("gpt-5", "low");
            return ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { changed = true })));
        };
        Enqueue(fixture, false, Function());
        Enqueue(fixture, false, Message("done", "assistant"));
        Assert.Equal(AgentRunStatus.Completed, (await fixture.RunAsync(effort: "high")).Status);
        Assert.Equal(2, fixture.Keys.Calls); // Один run resolver и отдельный settings use case.
        foreach ((string path, string? key, JsonElement? body) in fixture.Handler.Requests.Where(request => request.Body is not null))
        {
            Assert.Equal("/prefix/v1/responses", path);
            Assert.Equal(INDIVIDUAL, key);
            Assert.Equal("gpt-4.1", body!.Value.GetProperty("model").GetString());
            Assert.Equal("high", body.Value.GetProperty("reasoning").GetProperty("effort").GetString());
        }
        DialogSnapshot saved = await fixture.ReadAsync();
        Assert.Equal("gpt-4.1", saved.Turns[0].Settings!.Model);
        Assert.Equal("high", saved.Turns[0].Settings!.Effort);
        Assert.Equal(8000, saved.Turns[0].Settings!.TokenThreshold);
        Assert.Equal(10000, saved.Turns[0].Settings!.InputContextWindow);
        Assert.Equal("gpt-5", saved.Selection!.Model);
        Assert.Equal("low", saved.Selection.Effort);
        fixture.BuildRoot();
        Enqueue(fixture, false, Message("next", "assistant"));
        Assert.Equal(AgentRunStatus.Completed, (await fixture.RunAsync(call: Next(fixture))).Status);
        Assert.Equal("stage23-new-key", fixture.Handler.Requests.Last().Key);
        Assert.Equal("low", fixture.Handler.Requests.Last().Body!.Value.GetProperty("reasoning").GetProperty("effort").GetString());
        await using AsyncServiceScope scope = fixture.Root.CreateAsyncScope();
        DialogStatus status = (await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().GetStatusAsync(fixture.Call)).Data!;
        Assert.Equal("gpt-5", status.SelectedModel);
        Assert.Equal("actual-server-model", status.ServerModel);
        Assert.Equal(NOW.AddHours(37), status.ExpiresAtUtc);
    }

    /// <summary>Null допускает shared; source/HTTP/invalid key и неизвестный/малый input budget не разрешают fallback или Begin.</summary>
    [DatabaseIntegrationTheory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("source")]
    [InlineData("http")]
    [InlineData("missing-budget")]
    [InlineData("small-budget")]
    [InlineData("exact-model")]
    [InlineData("exact-effort")]
    public async Task AccessAndCatalogFailuresStopBeforeDurableBegin(string boundary)
    {
        await using IntegrationDatabase database = new(DatabaseProvider.SQLite);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        switch (boundary)
        {
            case "null": fixture.Keys.Key = null; break;
            case "empty": fixture.Keys.Key = string.Empty; break;
            case "source": fixture.Keys.Failure = new IOException("synthetic source failure"); break;
            case "http": fixture.Handler.CatalogStatus = HttpStatusCode.Unauthorized; break;
            case "missing-budget": fixture.Handler.InputWindow = null; break;
            case "small-budget": fixture.Handler.InputWindow = 8009; break;
        }
        if (boundary == "source")
            Assert.Same(fixture.Keys.Failure, await Assert.ThrowsAsync<IOException>(() => fixture.RunAsync()));
        else if (boundary == "null")
        {
            Enqueue(fixture, false, Message("done", "assistant"));
            Assert.Equal(AgentRunStatus.Completed, (await fixture.RunAsync()).Status);
            Assert.All(fixture.Handler.Requests, request => Assert.Equal(SHARED, request.Key));
        }
        else
        {
            AgentRunResult result;
            if (boundary == "exact-model")
            {
                await using AsyncServiceScope scope = fixture.Root.CreateAsyncScope();
                result = await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(
                    new(fixture.Call, [Message("input")], [], new(1, 1, 1, TimeSpan.FromMinutes(1)), model: "GPT-5"));
            }
            else result = await fixture.RunAsync(effort: boundary == "exact-effort" ? "High" : null);
            Assert.Equal(AgentRunStatus.Failed, result.Status);
            Assert.NotNull(result.Error);
            Assert.DoesNotContain("stage23-private-payload", result.Error.Message);
            Assert.All(fixture.Handler.Requests, request => Assert.Equal(INDIVIDUAL, request.Key));
        }
        Assert.Equal(1, fixture.Keys.Calls);
        Assert.Equal(0, fixture.Actions);
        if (boundary != "null")
        {
            Assert.Empty((await fixture.ReadAsync()).Turns);
            Assert.DoesNotContain(fixture.Handler.Requests, request => request.Body is not null);
        }
    }

    /// <summary>Started виден до ожидания; cancel/expiry cleanup не допускают revival и повторного side effect после restart.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite, false)]
    [InlineData(DatabaseProvider.SQLite, true)]
    [InlineData(DatabaseProvider.PostgreSql, false)]
    [InlineData(DatabaseProvider.PostgreSql, true)]
    public async Task CancellationAndConcurrentCleanupDoNotRepeatActions(DatabaseProvider provider, bool cleanup)
    {
        await using IntegrationDatabase database = new(provider);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        await fixture.SelectAsync("gpt-5", "high");
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource cancellation = new();
        fixture.Action = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(20));
            return ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { sideEffect = "confirmed" })));
        };
        Enqueue(fixture, true, Function());
        Task<AgentRunResult> running = fixture.RunAsync(stream: true, cancellationToken: cancellation.Token);
        AgentRunResult result;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.False(running.IsCompleted);
            if (cleanup)
            {
                fixture.Time.Now = NOW.AddHours(37); // Настроенная equality, не искусственно shortened expiry.
                await using AsyncServiceScope scope = fixture.Root.CreateAsyncScope();
                ExpiredDialogCleanupResult deleted = await scope.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>().CleanupAsync(5);
                Assert.Equal(1, deleted.DeletedCount);
            }
            cancellation.Cancel();
            release.TrySetResult();
            result = await running;
        }
        finally { release.TrySetResult(); await running; }
        Assert.Equal(1, fixture.Actions);
        fixture.BuildRoot();
        int requests = fixture.Handler.Requests.Count;
        AgentRunResult restarted = await fixture.RunAsync();
        Assert.Equal(requests, fixture.Handler.Requests.Count);
        Assert.Equal(1, fixture.Actions);
        if (cleanup)
        {
            Assert.Equal(ServiceErrorType.NotFound, result.Error!.Type);
            Assert.False(result.TerminalSaved);
            Assert.Single(result.LastTools!.Outputs);
            Assert.Equal(ServiceErrorType.NotFound, restarted.Error!.Type);
            foreach (string table in new[] { "Dialogs", "DialogTurns", "ModelSteps", "CanonicalItems", "DialogContexts", "DialogSettings" })
                Assert.Equal(0L, Convert.ToInt64(Assert.Single(await database.QueryAsync("SELECT count(*) AS n FROM \"" + table + "\""))["n"]));
        }
        else
        {
            Assert.Equal(AgentRunStatus.Canceled, result.Status);
            Assert.True(result.TerminalSaved);
            DialogSnapshot saved = await fixture.ReadAsync();
            Assert.Equal(ToolAttemptState.Succeeded, Assert.Single(saved.Turns[0].ModelSteps[0].ToolAttempts).State);
            Assert.Single(saved.Turns[0].Items, item => Type(item) == "function_call_output");
            Assert.Equal(AgentRunStatus.Interrupted, restarted.Status);
        }
    }

    /// <summary>Потеря acknowledgement после actual outcome commit сохраняет journal/output, но не разрешает продолжение или replay.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task LostOutcomeAcknowledgementDoesNotRepeatConfirmedAction(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await using CrossComponentFixture fixture = new(database);
        await fixture.InitializeAsync();
        fixture.BuildRoot(customize: services =>
        {
            services.AddScoped<DialogToolAttemptUnitOfWork>();
            services.AddScoped<IDialogToolAttemptWriter, LostOutcomeAcknowledgement>();
        });
        Enqueue(fixture, false, Function());
        IOException failure = await Assert.ThrowsAsync<IOException>(() => fixture.RunAsync());
        Assert.Equal("synthetic lost outcome acknowledgement", failure.Message);
        Assert.Equal(1, fixture.Actions);
        int requests = fixture.Handler.Requests.Count;
        fixture.BuildRoot();
        DialogSnapshot saved = await fixture.ReadAsync();
        Assert.Equal(DialogTurnStatus.InProgress, saved.Turns[0].Status);
        Assert.Equal(ToolAttemptState.Succeeded, saved.Turns[0].ModelSteps[0].ToolAttempts[0].State);
        Assert.Single(saved.Turns[0].Items, item => Type(item) == "function_call_output");
        Assert.Equal(AgentRunStatus.Interrupted, (await fixture.RunAsync()).Status);
        Assert.Equal(requests, fixture.Handler.Requests.Count);
        Assert.Equal(1, fixture.Actions);
    }

    /// <inheritdoc/>
    private class LostOutcomeAcknowledgement(DialogToolAttemptUnitOfWork inner) : IDialogToolAttemptWriter
    {
        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> StartAsync(DialogAccess access, DialogWriteToken expected,
            ToolExecutionIdentity identity, CancellationToken cancellationToken = default) => inner.StartAsync(access, expected, identity, cancellationToken);

        /// <inheritdoc/>
        public async Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(DialogAccess access, DialogWriteToken expected,
            Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken = default)
        {
            ServiceResult<DialogWriteToken> committed = await inner.SaveOutcomesAsync(access, expected, turnId, stepId, batch, cancellationToken);
            Assert.True(committed.Success, committed.Error?.Message);
            throw new IOException("synthetic lost outcome acknowledgement");
        }
    }

    /// <summary>Ожидаемый ответ добавляется ровно один раз, лишний HTTP падает в handler.</summary>
    private static void Enqueue(CrossComponentFixture fixture, bool stream, params CanonicalModelItem[] items)
    {
        string response = Response(items);
        fixture.Handler.Responses.Enqueue(("/prefix/v1/responses", stream ? Sse(response) : response, stream));
    }
    /// <summary>Формирует новый turn того же владельца и диалога.</summary>
    private static ApplicationCallContext Next(CrossComponentFixture fixture) => new(fixture.Call.DialogId, fixture.Call.OwnerId, Guid.NewGuid(), fixture.Call.AgentId);

    /// <summary>Actual native backup/pg_dump и restore сравнивают всю схему/данные, затем читают новые metadata через production ports.</summary>
    private static async Task VerifyBackupAsync(IntegrationDatabase database, CrossComponentFixture fixture, DialogSnapshot expected)
    {
        string before = await database.FingerprintAsync();
        LocalBackupArtifact artifact;
        await using (AsyncServiceScope scope = database.Root.CreateAsyncScope())
        {
            IDatabaseMaintenanceProvider<AgentBridgeContextKey> backup = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>();
            using MaintenanceBudget budget = new(TimeSpan.FromSeconds(45), default);
            DatabaseInspection inspection = await backup.InspectAsync(budget);
            DatabaseBackupReceipt receipt = await backup.BackupAsync(Guid.NewGuid(), inspection.TargetIdentity, budget);
            artifact = Assert.IsType<LocalBackupArtifact>(receipt.Artifact);
        }
        await using (FileStream bytes = File.OpenRead(artifact.Path))
        {
            Assert.Equal(artifact.Length, bytes.Length);
            Assert.Equal(artifact.Sha256, Convert.ToHexString(await SHA256.HashDataAsync(bytes)), ignoreCase: true);
        }
        string connection = await database.RestoreAsync(artifact);
        Assert.NotEqual(database.ConnectionString, connection);
        Assert.Equal(before, await database.FingerprintAsync(connection));
        ServiceProvider restored = fixture.BuildRoot(connection: connection);
        DialogSnapshot read = await fixture.ReadAsync(restored);
        Assert.Equal(expected.ContentBytes, read.ContentBytes);
        Assert.Equal(expected.Selection!.Version, read.Selection!.Version);
        Assert.Equal(expected.Selection.Effort, read.Selection.Effort);
        Assert.Equal(expected.ActiveContext!.SelectedModel, read.ActiveContext!.SelectedModel);
        Assert.Equal(expected.ActiveContext.Compaction.Envelope!.Content.GetRawText(), read.ActiveContext.Compaction.Envelope!.Content.GetRawText());
        Assert.Equal(ToolAttemptState.Succeeded, read.Turns[0].ModelSteps[0].ToolAttempts[0].State);
        Assert.Equal(expected.Turns[0].Settings!.InputContextWindow, read.Turns[0].Settings!.InputContextWindow);
        int calls = fixture.Handler.Requests.Count;
        Assert.Equal(AgentRunStatus.Interrupted, (await fixture.RunAsync()).Status);
        Assert.Equal(calls, fixture.Handler.Requests.Count);
        Assert.Equal(1, fixture.Actions);
    }
}

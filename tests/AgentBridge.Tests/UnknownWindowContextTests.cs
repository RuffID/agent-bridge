using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Tokenization;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Existing compactor/status с неизвестным окном; только in-memory ports, без transport/persistence/restart.</summary>
public class UnknownWindowContextTests
{
    /// <summary>Подтверждённое окно применяется после save вместе с хвостом; Unsupported не сохраняет кандидата.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownWindowPreservesExistingCompactAcceptance(bool unsupported)
    {
        // Arrange
        Fixture fixture = new();
        fixture.UnsupportedCompact = unsupported;
        ContextCompactor compactor = new(new([]), fixture, fixture, fixture, TimeProvider.System,
            new Frozen<ContextCompactionOptions>(new() { MaxPasses = 1, BudgetPolicy = fixture.Settings.BudgetPolicy }));
        ModelRequest input = new("gpt-5", "medium", "instructions", [Text("tail")], []);

        // Act
        ContextCompactionResult result = (await compactor.CompactAsync(fixture.Call, fixture.Dialog, input, fixture.Settings,
            new("synthetic-key"), TestContext.Current.CancellationToken)).Data!;

        // Assert
        Assert.Single(fixture.CompactInputs);
        Assert.Equal("history-history-history", fixture.CompactInputs[0].Input.Single().Content.GetProperty("content").GetString());
        Assert.Null(fixture.CompactInputs[0].Continuation);
        Assert.Equal("instructions", fixture.CompactInputs[0].Instructions);
        Assert.Equal("history-history-history", fixture.Dialog.Turns.Single().Items.Single().Content.GetProperty("content").GetString());
        if (unsupported)
        {
            Assert.Equal(ContextCompactionStatus.Failed, result.Status);
            Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
            Assert.Equal(0, fixture.Saves);
            Assert.Null(result.ActiveContext);
        }
        else
        {
            Assert.Equal(ContextCompactionStatus.TargetReached, result.Status);
            Assert.Equal(1, fixture.Saves);
            Assert.Equal("c", result.ActiveContext!.Items.Single().Content.GetProperty("content").GetString());
            Assert.Equal(new[] { "c", "tail" }, result.PreparedRequest.Input
                .Select(item => item.Content.GetProperty("content").GetString()).ToArray());
            Assert.Equal(1, result.ActiveContext.ThroughTurnSequence);
        }
    }

    /// <summary>Статус при неизвестном окне не подставляет budget и не выполняет compact/write.</summary>
    [Fact]
    public async Task StatusReportsUnknownWindowWithoutRejectingKnownTextCount()
    {
        // Arrange
        Fixture fixture = new();
        AgentSettingsService service = new(fixture, fixture, fixture, new(new ContextTokenCounter(), []), fixture,
            TimeProvider.System, new Frozen<DialogRetentionOptions>(new()),
            new Frozen<ContextCompactionOptions>(new() { BudgetPolicy = fixture.Settings.BudgetPolicy }),
            new Frozen<AgentOptions>(new()));

        // Act
        DialogStatus result = (await service.GetStatusAsync(fixture.Call, TestContext.Current.CancellationToken)).Data!;

        // Assert
        Assert.Null(result.ContextError);
        Assert.Null(result.Settings!.Model.Model.InputContextWindow);
        Assert.NotNull(result.ContextSize!.EstimatedInputTokens);
        Assert.Equal(0, fixture.Saves);
        Assert.Empty(fixture.CompactInputs);
    }

    /// <summary>Создаёт обычный canonical item.</summary>
    private static CanonicalModelItem Text(string text)
    {
        return new(JsonSerializer.SerializeToElement(new { type = "message", role = "user", content = text }));
    }

    /// <summary>Изолированные порты наблюдают только соответствующие им операции.</summary>
    private class Fixture : IContextTokenCounter, IModelGateway, IDialogContextWriter, IDialogReader,
        IDialogSettingsWriter, IModelSettingsReader
    {
        internal ApplicationCallContext Call { get; } = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        internal ModelSettingsSnapshot Settings { get; } = new(new("gpt-5", true, null, null, null, ["medium"], null, [],
            true, false, false, false, false), "medium", 10, 100, ContextBudgetPolicy.ServerValidationWithUnknownWindow);
        internal DialogSnapshot Dialog { get; }
        internal List<ModelRequest> CompactInputs { get; } = [];
        internal bool UnsupportedCompact { get; set; }
        internal int Saves { get; private set; }

        /// <summary>Задаёт завершённую исходную историю.</summary>
        internal Fixture()
        {
            Dialog = new(new(Call.DialogId, Guid.NewGuid(), 1), Call.OwnerId, DateTimeOffset.UtcNow.AddHours(-1), null, 1,
                [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Text("history-history-history")], [])], null);
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            long size = request.Input.Sum(item => item.Content.GetProperty("content").GetString()!.Length);
            return Task.FromResult(ServiceResult<ContextTokenCount>.Ok(new("fixture", size, size, false)));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(0, Saves);
            CompactInputs.Add(request);
            if (UnsupportedCompact)
            {
                return Task.FromResult(ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Unsupported, "Compact недоступен.")));
            }

            return Task.FromResult(ServiceResult<ModelResponse>.Ok(ModelResponse.Completed([Text("c")])));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Compact не заменяется generation.");
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected,
            long throughTurnSequence, ModelResponse compacted, CancellationToken cancellationToken = default)
        {
            Assert.Equal(ModelResponseStatus.Completed, compacted.Status);
            Saves++;
            return Task.FromResult(ServiceResult<DialogWriteToken>.Ok(new(expected.DialogId, expected.IncarnationId, expected.Revision + 1)));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<DialogSnapshot>.Ok(Dialog));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null,
            string? effort = null, CancellationToken ct = default)
        {
            return Task.FromResult(ServiceResult<ModelSettingsSnapshot>.Ok(Settings));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogModelSelection>> SaveAsync(DialogAccess access, DialogWriteToken expected,
            long expectedVersion, ModelSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Чтение статуса не меняет selection.");
        }
    }

    /// <summary>Фиксирует локальные options без контейнера/host.</summary>
    private class Frozen<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        /// <inheritdoc/>
        public T Value => value;
        /// <inheritdoc/>
        public T Get(string? name)
        {
            return value;
        }
    }
}

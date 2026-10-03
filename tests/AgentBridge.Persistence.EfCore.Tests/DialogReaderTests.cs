using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Защищённый public read path с заглушками base repositories, без БД/хоста и write UoW.</summary>
public class DialogReaderTests
{
    /// <summary>Все lifecycle и полный JSON переживают mapping; история до prefix и незавершённые turns сохраняются.</summary>
    [Theory]
    [InlineData(ModelResponseStatus.Completed)]
    [InlineData(ModelResponseStatus.Incomplete)]
    [InlineData(ModelResponseStatus.Failed)]
    [InlineData(ModelResponseStatus.Canceled)]
    public async Task FullSnapshotPreservesOrderReportsAndHistoryAcrossCompaction(ModelResponseStatus status)
    {
        Fixture fixture = new();
        Guid firstTurn = Guid.NewGuid();
        Guid secondTurn = Guid.NewGuid();
        Guid firstStep = Guid.NewGuid();
        Guid secondStep = Guid.NewGuid();
        fixture.Turns.Records.AddRange([
            new() { DialogId = fixture.Dialog.Id, Id = secondTurn, Sequence = 2, Status = DialogTurnStatus.InProgress },
            new() { DialogId = Guid.NewGuid(), Id = firstTurn, Sequence = 1 },
            new() { DialogId = fixture.Dialog.Id, Id = firstTurn, Sequence = 1, Status = DialogTurnStatus.Failed }]);
        const string TOOL_JSON = "{\"type\":\"function_call_output\",\"call_id\":\"call-1\",\"output\":\"данные\",\"future\":[null,true]}";
        const string REASONING_JSON = "{\"type\":\"reasoning\",\"encrypted_content\":\"opaque\",\"unknown\":123456789012345678901234567890}";
        fixture.Items.Records.AddRange([
            new() { DialogId = fixture.Dialog.Id, TurnId = firstTurn, Sequence = 2, ContentJson = TOOL_JSON },
            new() { DialogId = Guid.NewGuid(), TurnId = firstTurn, Sequence = 1, ContentJson = "{\"foreign\":true}" },
            new() { DialogId = fixture.Dialog.Id, TurnId = secondTurn, Sequence = 1, ContentJson = "{\"new\":true}" },
            new() { DialogId = fixture.Dialog.Id, TurnId = firstTurn, Sequence = 1, ContentJson = REASONING_JSON }]);
        ModelResponse original = Response(status);
        fixture.Steps.Records.AddRange([
            new() { DialogId = fixture.Dialog.Id, TurnId = firstTurn, Id = secondStep, Sequence = 2, Response = ModelResponseRecord.FromModelResponse(ModelResponse.Completed([])) },
            new() { DialogId = fixture.Dialog.Id, TurnId = firstTurn, Id = firstStep, Sequence = 1, Response = ModelResponseRecord.FromModelResponse(original) },
            new() { DialogId = Guid.NewGuid(), TurnId = firstTurn, Id = firstStep, Sequence = 1, Response = new() { FormatVersion = 99 } },
            new() { DialogId = fixture.Dialog.Id, TurnId = secondTurn, Id = firstStep, Sequence = 1, Response = ModelResponseRecord.FromModelResponse(ModelResponse.Incomplete([])) }]);
        fixture.Contexts.Records.AddRange([
            new() { DialogId = fixture.Dialog.Id, Version = 1, ThroughTurnSequence = 0, Compaction = ModelResponseRecord.FromModelResponse(ModelResponse.Completed([])) },
            new() { DialogId = Guid.NewGuid(), Version = 99, Compaction = new() { FormatVersion = 99 } },
            new() { DialogId = fixture.Dialog.Id, Version = 2, ThroughTurnSequence = 1, Compaction = ModelResponseRecord.FromModelResponse(Response(ModelResponseStatus.Completed)) }]);

        using CancellationTokenSource source = new();
        ServiceResult<DialogSnapshot> result = await fixture.Reader.ReadAsync(fixture.Access(), source.Token);
        Assert.True(result.Success);
        DialogSnapshot snapshot = Assert.IsType<DialogSnapshot>(result.Data);
        Assert.Equal(fixture.Dialog.Id, snapshot.Token.DialogId.Value);
        Assert.Equal(fixture.Dialog.IncarnationId, snapshot.Token.IncarnationId);
        Assert.Equal(fixture.Dialog.Revision, snapshot.Token.Revision);
        Assert.Equal(fixture.Dialog.OwnerId, snapshot.OwnerId.Value);
        Assert.Equal(fixture.Dialog.CreatedAtUtc, snapshot.CreatedAtUtc);
        Assert.Equal(fixture.Dialog.ExpiresAtUtc, snapshot.ExpiresAtUtc);
        Assert.Equal(fixture.Dialog.ContentBytes, snapshot.ContentBytes);
        Assert.Equal([firstTurn, secondTurn], snapshot.Turns.Select(turn => turn.Id));
        Assert.Equal([1L, 2L], snapshot.Turns.Select(turn => turn.Sequence));
        Assert.Equal(DialogTurnStatus.Failed, snapshot.Turns[0].Status);
        Assert.Equal(DialogTurnStatus.InProgress, snapshot.Turns[1].Status);
        Assert.Equal(REASONING_JSON, snapshot.Turns[0].Items[0].Content.GetRawText());
        Assert.Equal(TOOL_JSON, snapshot.Turns[0].Items[1].Content.GetRawText());
        Assert.True(Assert.Single(snapshot.Turns[1].Items).Content.GetProperty("new").GetBoolean());
        Assert.Equal([firstStep, secondStep], snapshot.Turns[0].ModelSteps.Select(step => step.StepId));
        AssertResponse(original, snapshot.Turns[0].ModelSteps[0].Response);
        Assert.Equal(firstStep, Assert.Single(snapshot.Turns[1].ModelSteps).StepId);
        StoredDialogContext active = Assert.IsType<StoredDialogContext>(snapshot.ActiveContext);
        Assert.Equal(2, active.Version);
        Assert.Equal(1, active.ThroughTurnSequence);
        AssertResponse(Response(ModelResponseStatus.Completed), active.Compaction);
        Assert.Equal(3, fixture.Contexts.Records.Count);
        Assert.Equal(1, fixture.Items.ReadCalls);
        Assert.Equal(1, fixture.Steps.ReadCalls);
        Assert.Equal(2, fixture.Dialogs.ReadCalls);
        Assert.Equal(source.Token, fixture.Contexts.LastCancellationToken);
        Assert.True(fixture.Turns.LastAsNoTracking);
        Assert.True(fixture.Items.LastAsNoTracking);
        Assert.True(fixture.Steps.LastAsNoTracking);
        Assert.True(fixture.Contexts.LastAsNoTracking);

        fixture.Items.Records[3].ContentJson = "{}";
        fixture.Steps.Records[1].Response.OutputJson = "[]";
        fixture.Contexts.Records[2].Compaction.EnvelopeJson = null;
        fixture.Dialog.OwnerId = "changed";
        Assert.Equal(REASONING_JSON, snapshot.Turns[0].Items[0].Content.GetRawText());
        AssertResponse(original, snapshot.Turns[0].ModelSteps[0].Response);
        Assert.NotNull(active.Compaction.Envelope);
        Assert.Equal(" User:Б ", snapshot.OwnerId.Value);
    }

    /// <summary>Пустая история/отсутствие compact допустимы, истёкший диалог читается до физического удаления.</summary>
    [Fact]
    public async Task EmptyExpiredDialogReturnsMetadataWithoutInventingContext()
    {
        Fixture fixture = new();
        ServiceResult<DialogSnapshot> result = await fixture.Reader.ReadAsync(fixture.Access(nowUtc: fixture.Dialog.ExpiresAtUtc));
        Assert.True(result.Success);
        DialogSnapshot snapshot = Assert.IsType<DialogSnapshot>(result.Data);
        Assert.True(snapshot.IsExpired(fixture.Dialog.ExpiresAtUtc));
        Assert.False(snapshot.IsExpired(fixture.Dialog.ExpiresAtUtc.AddTicks(-1)));
        Assert.Empty(snapshot.Turns);
        Assert.Null(snapshot.ActiveContext);
    }

    /// <summary>Отсутствие/ordinal несовпадение владельца не возвращают данные, не читают детей и не ставят изменения.</summary>
    [Theory]
    [InlineData(null, ServiceErrorType.NotFound)]
    [InlineData("user:Б", ServiceErrorType.Forbidden)]
    [InlineData(" User:б ", ServiceErrorType.Forbidden)]
    [InlineData("User:Б", ServiceErrorType.Forbidden)]
    public async Task ExpectedDenialDoesNotReadChildrenOrStageData(string? owner, ServiceErrorType expected)
    {
        Fixture fixture = new();
        if (owner is null)
        {
            fixture.Dialogs.Records.Clear();
        }
        ServiceResult<DialogSnapshot> result = await fixture.Reader.ReadAsync(fixture.Access(owner ?? fixture.Dialog.OwnerId));
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(expected, result.Error!.Type);
        Assert.Equal(0, fixture.Turns.ReadCalls + fixture.Items.ReadCalls + fixture.Steps.ReadCalls + fixture.Contexts.ReadCalls);
        Assert.Empty(fixture.Dialogs.Created);
        Assert.Empty(fixture.Dialogs.Updated);
        Assert.Empty(fixture.Dialogs.Deleted);
        Assert.Null(fixture.Dialogs.CreatedRange);
        Assert.Equal(owner is null ? 0 : 1, fixture.Dialogs.Records.Count);
    }

    /// <summary>Caller cancellation распространяется исходным токеном до чтения и из base repository.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationIsNotConvertedToExpectedFailure(bool duringRead)
    {
        Fixture fixture = new();
        using CancellationTokenSource source = new();
        source.Cancel();
        if (duringRead)
        {
            fixture.Items.ReadException = new OperationCanceledException(source.Token);
        }
        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Reader.ReadAsync(fixture.Access(), duringRead ? CancellationToken.None : source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(duringRead ? 1 : 0, fixture.Dialogs.ReadCalls);
        Assert.Equal(0, fixture.Contexts.ReadCalls);
    }

    /// <summary>Неожиданная ошибка base repository не маскируется NotFound или успешным пустым снимком.</summary>
    [Fact]
    public async Task RepositoryFailurePropagatesAndStopsSubsequentReads()
    {
        Fixture fixture = new();
        InvalidOperationException expected = new("synthetic repository failure");
        fixture.Turns.ReadException = expected;
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reader.ReadAsync(fixture.Access())));
        Assert.Equal(0, fixture.Items.ReadCalls + fixture.Steps.ReadCalls + fixture.Contexts.ReadCalls);
    }

    /// <summary>Повреждённый формат отчёта и JSON item отклоняются, а не пропускаются из истории.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptCanonicalDataFailsFast(bool corruptReport)
    {
        Fixture fixture = new();
        Guid turnId = Guid.NewGuid();
        fixture.Turns.Records.Add(new() { DialogId = fixture.Dialog.Id, Id = turnId, Sequence = 1 });
        if (corruptReport)
        {
            fixture.Steps.Records.Add(new() { DialogId = fixture.Dialog.Id, TurnId = turnId, Id = Guid.NewGuid(), Sequence = 1, Response = new() { FormatVersion = 99 } });
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reader.ReadAsync(fixture.Access()));
        }
        else
        {
            fixture.Items.Records.Add(new() { DialogId = fixture.Dialog.Id, TurnId = turnId, Sequence = 1, ContentJson = "not-json" });
            await Assert.ThrowsAnyAsync<JsonException>(() => fixture.Reader.ReadAsync(fixture.Access()));
        }
    }

    /// <summary>Повторная проверка root отказывает при удалении, смене владельца, новой жизни или версии без retry.</summary>
    [Theory]
    [InlineData(0, ServiceErrorType.NotFound)]
    [InlineData(1, ServiceErrorType.Forbidden)]
    [InlineData(2, ServiceErrorType.Conflict)]
    [InlineData(3, ServiceErrorType.Conflict)]
    public async Task ChangedRootRejectsMixedSnapshot(int interleaving, ServiceErrorType expected)
    {
        Fixture fixture = new();
        Guid oldIncarnation = fixture.Dialog.IncarnationId;
        fixture.Contexts.BeforeRead = () =>
        {
            switch (interleaving)
            {
                case 0: fixture.Dialogs.Records.Clear(); break;
                case 1:
                    fixture.Dialogs.Records[0] = new DialogRecord
                    {
                        Id = fixture.Dialog.Id, OwnerId = "other-owner", IncarnationId = Guid.NewGuid(), Revision = 0
                    };
                    break;
                case 2: fixture.Dialog.IncarnationId = Guid.NewGuid(); break;
                case 3: fixture.Dialog.Revision++; break;
            }
        };
        fixture.Items.BeforeRead = () => fixture.Items.Records.Add(new()
        {
            DialogId = fixture.Dialog.Id, TurnId = Guid.NewGuid(), Sequence = 1, ContentJson = "{\"foreign\":true}"
        });
        ServiceResult<DialogSnapshot> result = await fixture.Reader.ReadAsync(fixture.Access());
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(expected, result.Error!.Type);
        Assert.Equal(2, fixture.Dialogs.ReadCalls);
        Assert.Equal(1, fixture.Turns.ReadCalls);
        Assert.Equal(1, fixture.Items.ReadCalls);
        Assert.Equal(1, fixture.Steps.ReadCalls);
        Assert.Equal(1, fixture.Contexts.ReadCalls);
        Assert.Empty(fixture.Dialogs.Created);
        if (interleaving == 2)
        {
            Assert.NotEqual(oldIncarnation, fixture.Dialog.IncarnationId);
        }
    }

    /// <summary>При стабильном root orphan items/steps не отбрасываются молча из снимка.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrphanDataFailsFast(bool orphanStep)
    {
        Fixture fixture = new();
        if (orphanStep)
        {
            fixture.Steps.Records.Add(new() { DialogId = fixture.Dialog.Id, TurnId = Guid.NewGuid(), Id = Guid.NewGuid(), Sequence = 1 });
        }
        else
        {
            fixture.Items.Records.Add(new() { DialogId = fixture.Dialog.Id, TurnId = Guid.NewGuid(), Sequence = 1, ContentJson = "{}" });
        }
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reader.ReadAsync(fixture.Access()));
        Assert.Contains("без родительского", error.Message);
    }

    /// <summary>Повреждённый принятый compact не выдаётся за допустимое активное окно.</summary>
    [Fact]
    public async Task NonCompletedAcceptedCompactionFailsFast()
    {
        Fixture fixture = new();
        fixture.Contexts.Records.Add(new() { DialogId = fixture.Dialog.Id, Version = 1, Compaction = ModelResponseRecord.FromModelResponse(ModelResponse.Incomplete([])) });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reader.ReadAsync(fixture.Access()));
    }

    /// <summary>Создаёт полный синтетический отчёт с unknown/opaque полями без секретов.</summary>
    private static ModelResponse Response(ModelResponseStatus status)
    {
        using JsonDocument output = JsonDocument.Parse("{\"type\":\"reasoning\",\"encrypted_content\":\"opaque\",\"future\":[null,true]}");
        using JsonDocument envelope = JsonDocument.Parse("{\"id\":\"response-1\",\"usage\":{\"total_tokens\":42},\"future\":{\"x\":1}}");
        using JsonDocument continuation = JsonDocument.Parse("{\"upstream_owner\":\"owner-1\",\"opaque\":{\"cursor\":\"next\"}}");
        CanonicalModelItem[] items = [new(output.RootElement)];
        CanonicalModelEnvelope fullEnvelope = new(envelope.RootElement);
        ModelContinuation fullContinuation = new(continuation.RootElement);
        return status switch
        {
            ModelResponseStatus.Completed => ModelResponse.Completed(items, fullEnvelope, fullContinuation),
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete(items, fullEnvelope, fullContinuation),
            ModelResponseStatus.Canceled => ModelResponse.Canceled(items, fullEnvelope, fullContinuation),
            ModelResponseStatus.Failed => ModelResponse.Failed(items, new ServiceError(ServiceErrorType.Rejected, "Безопасный отказ."), fullEnvelope, fullContinuation),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    /// <summary>Сравнивает весь отчёт, не только видимый текст или lifecycle.</summary>
    private static void AssertResponse(ModelResponse expected, ModelResponse actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Output.Select(item => item.Content.GetRawText()), actual.Output.Select(item => item.Content.GetRawText()));
        Assert.Equal(expected.Envelope!.Content.GetRawText(), actual.Envelope!.Content.GetRawText());
        Assert.Equal(expected.Continuation!.Content.GetRawText(), actual.Continuation!.Content.GetRawText());
        Assert.Equal(expected.Error?.Type, actual.Error?.Type);
        Assert.Equal(expected.Error?.Message, actual.Error?.Message);
    }

    /// <summary>Собирает read adapter из изолированных точных базовых контрактов, без DbContext.</summary>
    private class Fixture
    {
        public FakeBaseRepository<DialogRecord> Dialogs { get; } = new();
        public FakeBaseRepository<DialogTurnRecord> Turns { get; } = new();
        public FakeBaseRepository<CanonicalItemRecord> Items { get; } = new();
        public FakeBaseRepository<ModelStepRecord> Steps { get; } = new();
        public FakeBaseRepository<DialogContextRecord> Contexts { get; } = new();
        public DialogRecord Dialog { get; } = new()
        {
            Id = Guid.NewGuid(), IncarnationId = Guid.NewGuid(), Revision = 17, OwnerId = " User:Б ",
            CreatedAtUtc = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero),
            ExpiresAtUtc = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), ContentBytes = 4096
        };
        public DialogReader Reader { get; }

        /// <summary>Создаёт один набор строк для последовательных read-запросов.</summary>
        public Fixture()
        {
            Dialogs.Records.Add(Dialog);
            Reader = new(new DialogRecordQueries(new FakeDialogByIdRepository(Dialogs), Dialogs),
                new(Turns), new(Items), new(Steps), new(Contexts));
        }

        /// <summary>Фиксирует владельца и явное время; системные часы не используются.</summary>
        public DialogAccess Access(string? owner = null, DateTimeOffset? nowUtc = null) =>
            new(DialogId.From(Dialog.Id), DialogOwnerId.From(owner ?? Dialog.OwnerId), nowUtc ?? Dialog.CreatedAtUtc);
    }
}

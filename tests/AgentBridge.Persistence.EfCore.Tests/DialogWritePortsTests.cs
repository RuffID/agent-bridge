using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Production write paths на base repository/transaction fakes: атомарное согласование, отказы и полный payload.</summary>
public class DialogWritePortsTests
{
    /// <summary>Повторное создание существующего ID возвращает Conflict; не перезаписывает владельца или срок.</summary>
    [Fact]
    public async Task ExistingDialogIsNotOverwritten()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        ServiceResult<DialogWriteToken> duplicate = await fixture.Creator.CreateAsync(token.DialogId, DialogOwnerId.From("other"),
            FakeWriteFixture.NOW, FakeWriteFixture.NOW.AddDays(7), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, duplicate.Error!.Type);
        Assert.Null(duplicate.Data);
        Assert.Equal(" User:Б ", Assert.Single(fixture.Roots.Persisted).OwnerId);
        Assert.Equal(FakeWriteFixture.NOW.AddDays(1), fixture.Roots.Persisted[0].ExpiresAtUtc);
    }

    /// <summary>Явное удаление разрешено после expiry; owner/token проверяются и partial failure не удаляет историю.</summary>
    [Fact]
    public async Task ExplicitDeletionChecksOwnerAndPreservesDataOnFailure()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        token = (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token, Guid.NewGuid(), [Item("{\"x\":1}")], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Forbidden, (await fixture.Deletion.DeleteAsync(FakeWriteFixture.Access(token, owner: "other"), token, cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        fixture.BeforeSave = () => throw new IOException("delete failure");
        await Assert.ThrowsAsync<IOException>(() => fixture.Deletion.DeleteAsync(FakeWriteFixture.Access(token), token, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(fixture.Roots.Persisted);
        Assert.Single(fixture.Turns.Persisted);
        Assert.Single(fixture.Items.Persisted);
        fixture.BeforeSave = null;
        Assert.True((await fixture.Deletion.DeleteAsync(FakeWriteFixture.Access(token, FakeWriteFixture.NOW.AddDays(2)), token, cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Roots.Persisted);
        Assert.Empty(fixture.Items.Persisted);
    }

    /// <summary>Повреждённая доменная история не маскируется Conflict и не модифицируется.</summary>
    [Fact]
    public async Task CorruptStoredHistoryFailsBeforeSave()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        Guid turn = Guid.NewGuid();
        token = (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token, turn, [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        fixture.Turns.Persisted[0].Sequence = 2;
        fixture.Session.Events.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Writer.AppendAsync(FakeWriteFixture.Access(token), token, turn, [], [], cancellationToken: TestContext.Current.CancellationToken));
        Assert.DoesNotContain("save", fixture.Session.Events);
        Assert.Equal(2, fixture.Turns.Persisted[0].Sequence);
        Assert.Equal(token.Revision, fixture.Roots.Persisted[0].Revision);
    }

    /// <summary>Loader отклоняет context-only mismatch до staging/save; корректные штатные времена разрешают следующий compact.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextOnlyChronologyIsValidatedBeforeWrite(bool corrupt)
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        DateTimeOffset contextTime = FakeWriteFixture.NOW.AddMinutes(1);
        token = (await fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token, contextTime), token, 0,
            Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken)).Data!;
        DialogRecord root = Assert.Single(fixture.Roots.Persisted);
        Assert.Equal(contextTime, root.LastChangedAtUtc);
        Assert.Equal(contextTime, Assert.Single(fixture.Contexts.Persisted).CreatedAtUtc);
        if (corrupt) { root.LastChangedAtUtc = FakeWriteFixture.NOW.AddMinutes(2); }
        fixture.Session.Events.Clear();
        Task<ServiceResult<DialogWriteToken>> write = fixture.ContextWriter.SaveAsync(
            FakeWriteFixture.Access(token, FakeWriteFixture.NOW.AddMinutes(3)), token, 0, Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken);
        if (corrupt)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => write);
            Assert.Equal(["begin", "rollback", "dispose", "clear"], fixture.Session.Events);
            Assert.Equal(token.Revision, root.Revision);
            Assert.Equal(FakeWriteFixture.NOW.AddMinutes(2), root.LastChangedAtUtc);
            Assert.Equal(contextTime, Assert.Single(fixture.Contexts.Persisted).CreatedAtUtc);
            Assert.Empty(fixture.Roots.Repository.Updated);
            Assert.Empty(fixture.Contexts.Repository.Created);
        }
        else
        {
            Assert.True((await write).Success);
            Assert.Equal(2, fixture.Roots.Persisted[0].Revision);
            Assert.Equal(FakeWriteFixture.NOW.AddMinutes(3), fixture.Roots.Persisted[0].LastChangedAtUtc);
            Assert.Equal(2, fixture.Contexts.Persisted.Count);
        }
        Assert.Empty(fixture.Turns.Persisted);
        Assert.Equal(FakeWriteFixture.NOW.AddDays(1), fixture.Roots.Persisted[0].ExpiresAtUtc);
        Assert.Equal(" User:Б ", fixture.Roots.Persisted[0].OwnerId);
    }

    /// <summary>Begin/append/finish сохраняют input, все lifecycle reports и независимые envelope/continuation.</summary>
    [Theory]
    [InlineData(ModelResponseStatus.Completed)]
    [InlineData(ModelResponseStatus.Incomplete)]
    [InlineData(ModelResponseStatus.Failed)]
    [InlineData(ModelResponseStatus.Canceled)]
    public async Task TurnRoundTripPreservesEveryPayloadAndFixedExpiry(ModelResponseStatus status)
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken original = await fixture.CreateAsync();
        Guid turn = Guid.NewGuid();
        CanonicalModelItem input = Item("{\"type\":\"message\",\"text\":\"Привет\"}");
        DialogWriteToken started = (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(original), original, turn, [input], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        ModelResponse response = Response(status);
        Guid step = Guid.NewGuid();
        DialogWriteToken appended = (await fixture.Writer.AppendAsync(FakeWriteFixture.Access(started), started, turn,
            response.Output, [new StoredModelStep(step, response)], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        ServiceResult<DialogWriteToken> finished = await fixture.Writer.FinishAsync(FakeWriteFixture.Access(appended), appended,
            turn, DialogTurnStatus.Completed, [], [], cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(finished.Success);
        Assert.Equal(3, finished.Data!.Revision);
        Assert.Equal(original.IncarnationId, finished.Data.IncarnationId);
        DialogRecord root = Assert.Single(fixture.Roots.Persisted);
        Assert.Equal(FakeWriteFixture.NOW.AddDays(1), root.ExpiresAtUtc);
        Assert.Equal(" User:Б ", root.OwnerId);
        Assert.Equal(DialogTurnStatus.Completed, Assert.Single(fixture.Turns.Persisted).Status);
        Assert.Equal([1L, 2L], fixture.Items.Persisted.Select(row => row.Sequence));
        ModelResponse stored = Assert.Single(fixture.Steps.Persisted).Response.ToModelResponse();
        Assert.Equal(status, stored.Status);
        Assert.Equal(response.Envelope!.Content.GetRawText(), stored.Envelope!.Content.GetRawText());
        Assert.Equal(response.Continuation!.Content.GetRawText(), stored.Continuation!.Content.GetRawText());
        Assert.True(JsonElement.DeepEquals(response.Output[0].Content, stored.Output[0].Content));
        Assert.Equal(response.Error?.Message, stored.Error?.Message);
        ModelResponseRecord report = fixture.Steps.Persisted[0].Response;
        long expectedBytes = fixture.Items.Persisted.Sum(row => Encoding.UTF8.GetByteCount(row.ContentJson)) +
            new[] { report.OutputJson, report.EnvelopeJson, report.ContinuationJson, report.ErrorMessage }
                .Where(value => value is not null).Sum(value => Encoding.UTF8.GetByteCount(value!));
        Assert.Equal(expectedBytes, root.ContentBytes);
        Assert.Empty(fixture.Roots.Repository.Records);
    }

    /// <summary>Ранние guards не читают детей и не сохраняют; identity/owner не нормализуются.</summary>
    [Theory]
    [InlineData("missing", ServiceErrorType.NotFound)]
    [InlineData("owner", ServiceErrorType.Forbidden)]
    [InlineData("id", ServiceErrorType.Conflict)]
    [InlineData("incarnation", ServiceErrorType.Conflict)]
    [InlineData("revision", ServiceErrorType.Conflict)]
    [InlineData("expiry", ServiceErrorType.Expired)]
    public async Task GuardsRejectWithoutReadingChildren(string change, ServiceErrorType error)
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        DialogAccess access = FakeWriteFixture.Access(token);
        if (change == "missing") { fixture.Roots.Persisted.Clear(); }
        if (change == "owner") { access = FakeWriteFixture.Access(token, owner: "User:Б"); }
        if (change == "id") { access = new(DialogId.From(Guid.NewGuid()), access.OwnerId, access.NowUtc); }
        if (change == "incarnation") { token = new(token.DialogId, Guid.NewGuid(), token.Revision); }
        if (change == "revision") { token = new(token.DialogId, token.IncarnationId, token.Revision + 1); }
        if (change == "expiry")
        {
            access = FakeWriteFixture.Access(token, FakeWriteFixture.NOW.AddDays(1));
            token = new(token.DialogId, token.IncarnationId, token.Revision + 1);
        }
        fixture.Session.Events.Clear();
        ServiceResult<DialogWriteToken> result = await fixture.Writer.BeginAsync(access, token, Guid.NewGuid(), [], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(error, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(0, fixture.Turns.Repository.ReadCalls);
        Assert.DoesNotContain("save", fixture.Session.Events);
        Assert.Empty(fixture.Turns.Persisted);
    }

    /// <summary>После удаления и пересоздания старый результат не восстанавливает прежнюю историю.</summary>
    [Fact]
    public async Task DeleteAndRecreateNeverRefreshesAnOldToken()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken old = await fixture.CreateAsync();
        Assert.True((await fixture.Deletion.DeleteAsync(FakeWriteFixture.Access(old), old, cancellationToken: TestContext.Current.CancellationToken)).Success);
        DialogWriteToken fresh = (await fixture.Creator.CreateAsync(old.DialogId, FakeWriteFixture.Access(old).OwnerId,
            FakeWriteFixture.NOW, FakeWriteFixture.NOW.AddDays(2), cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.NotEqual(old.IncarnationId, fresh.IncarnationId);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(old), old, Guid.NewGuid(), [], cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Empty(fixture.Turns.Persisted);
        Assert.Equal(0, fixture.Roots.Persisted[0].Revision);
    }

    /// <summary>Partial staging/save failure не меняет committed root/items/turns.</summary>
    [Fact]
    public async Task SaveFailureRollsBackTheWholeTurn()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        fixture.BeforeSave = () => throw new IOException("synthetic save failure");
        await Assert.ThrowsAsync<IOException>(() => fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token,
            Guid.NewGuid(), [Item("{\"text\":\"данные\"}")], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Roots.Persisted[0].Revision);
        Assert.Equal(0, fixture.Roots.Persisted[0].ContentBytes);
        Assert.Empty(fixture.Items.Persisted);
        Assert.Empty(fixture.Turns.Persisted);
        Assert.Empty(fixture.Roots.Repository.Updated);
    }

    /// <summary>Смена root после проверки и до save даёт Conflict без записи детей или retry.</summary>
    [Theory]
    [InlineData("delete")]
    [InlineData("revision")]
    [InlineData("incarnation")]
    [InlineData("owner")]
    public async Task ConcurrentRootChangeRollsBackChildren(string change)
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        fixture.Session.Events.Clear();
        fixture.BeforeSave = () =>
        {
            if (change == "delete") { fixture.Roots.Persisted.Clear(); }
            if (change == "revision") { fixture.Roots.Persisted[0].Revision++; }
            if (change == "incarnation") { fixture.Roots.Persisted[0].IncarnationId = Guid.NewGuid(); }
            if (change == "owner") { fixture.Roots.Persisted[0].OwnerId = "other"; }
        };
        ServiceResult<DialogWriteToken> result = await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token,
            Guid.NewGuid(), [Item("{\"text\":\"late\"}")], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Empty(fixture.Turns.Persisted);
        Assert.Empty(fixture.Items.Persisted);
        Assert.Equal(["begin", "save", "rollback", "dispose", "clear"], fixture.Session.Events);
    }

    /// <summary>Compact сохраняет все версии, допускает пустой/тот же prefix и отклоняет незавершённый prefix.</summary>
    [Fact]
    public async Task ContextAcceptsOnlyTerminalPrefixWithoutLosingHistory()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        Guid turn = Guid.NewGuid();
        token = (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token, turn, [Item("{\"text\":\"input\"}")], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token), token, 1, Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        token = (await fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token), token, 0, Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken)).Data!;
        token = (await fixture.Writer.FinishAsync(FakeWriteFixture.Access(token), token, turn, DialogTurnStatus.Completed, [], [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        token = (await fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token), token, 1, Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken)).Data!;
        token = (await fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token), token, 1, Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal([1L, 2L, 3L], fixture.Contexts.Persisted.Select(row => row.Version));
        Assert.Equal([0L, 1L, 1L], fixture.Contexts.Persisted.Select(row => row.ThroughTurnSequence));
        Assert.Single(fixture.Items.Persisted);
        Assert.Single(fixture.Turns.Persisted);
        Assert.Equal(ServiceErrorType.Validation, (await fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token), token, 0, Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.Validation, (await fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token), token, 1, Response(ModelResponseStatus.Incomplete), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        fixture.BeforeSave = () => throw new IOException("failed compact save");
        await Assert.ThrowsAsync<IOException>(() => fixture.ContextWriter.SaveAsync(FakeWriteFixture.Access(token), token, 1, Response(ModelResponseStatus.Completed), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, fixture.Contexts.Persisted.Count);
        Assert.Equal(token.Revision, fixture.Roots.Persisted[0].Revision);
    }

    /// <summary>Старый кандидат очистки не удаляет свежую версию; точная граница expiry разрешает удаление.</summary>
    [Fact]
    public async Task ExpiredDeletionRechecksTokenAndExactExpiry()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Deletion.DeleteAsync(token, FakeWriteFixture.NOW.AddDays(1).AddTicks(-1), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        DialogWriteToken next = (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token, Guid.NewGuid(), [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Deletion.DeleteAsync(token, FakeWriteFixture.NOW.AddDays(1), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        Assert.True((await fixture.Deletion.DeleteAsync(next, FakeWriteFixture.NOW.AddDays(1), cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Roots.Persisted);
        Assert.Empty(fixture.Turns.Persisted);
        Assert.Equal(ServiceErrorType.NotFound, (await fixture.Deletion.DeleteAsync(next, FakeWriteFixture.NOW.AddDays(1), cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Повторные ID/terminal completion не меняют данные; child ID остаются локальны своему родителю.</summary>
    [Fact]
    public async Task DuplicateStepsAndFinishedTurnsAreRejected()
    {
        FakeWriteFixture fixture = new();
        DialogWriteToken token = await fixture.CreateAsync();
        Guid turn = Guid.NewGuid();
        token = (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token, turn, [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token), token, turn, [], cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        Guid stepId = Guid.NewGuid();
        StoredModelStep step = new(stepId, Response(ModelResponseStatus.Failed));
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.AppendAsync(FakeWriteFixture.Access(token), token, turn, [], [step, step], cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        token = (await fixture.Writer.AppendAsync(FakeWriteFixture.Access(token), token, turn, [], [step], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.AppendAsync(FakeWriteFixture.Access(token), token, turn, [], [step], cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        token = (await fixture.Writer.FinishAsync(FakeWriteFixture.Access(token), token, turn, DialogTurnStatus.Failed, [], [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.AppendAsync(FakeWriteFixture.Access(token), token, turn, [], [], cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.FinishAsync(FakeWriteFixture.Access(token), token, turn, DialogTurnStatus.Completed, [], [], cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.NotFound, (await fixture.Writer.AppendAsync(FakeWriteFixture.Access(token), token, Guid.NewGuid(), [], [], cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
        DialogWriteToken other = await fixture.CreateAsync();
        other = (await fixture.Writer.BeginAsync(FakeWriteFixture.Access(other), other, turn, [], cancellationToken: TestContext.Current.CancellationToken)).Data!;
        Assert.True((await fixture.Writer.AppendAsync(FakeWriteFixture.Access(other), other, turn, [], [step], cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Equal(2, fixture.Steps.Persisted.Count);
    }

    /// <summary>Создаёт полный синтетический payload без ключей/сетевых вызовов.</summary>
    private static ModelResponse Response(ModelResponseStatus status)
    {
        CanonicalModelItem output = Item("{\"type\":\"function_call_output\",\"call_id\":\"c1\",\"output\":\"результат\",\"unknown\":42}");
        using JsonDocument envelope = JsonDocument.Parse("{\"id\":\"r1\",\"opaque\":\"abc\"}");
        using JsonDocument continuation = JsonDocument.Parse("{\"owner\":\"upstream\",\"cursor\":\"next\"}");
        CanonicalModelEnvelope full = new(envelope.RootElement);
        ModelContinuation next = new(continuation.RootElement);
        return status switch
        {
            ModelResponseStatus.Completed => ModelResponse.Completed([output], full, next),
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete([output], full, next),
            ModelResponseStatus.Canceled => ModelResponse.Canceled([output], full, next),
            ModelResponseStatus.Failed => ModelResponse.Failed([output], new ServiceError(ServiceErrorType.Rejected, "Отказ."), full, next),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    /// <summary>Фиксирует независимый канонический JSON.</summary>
    private static CanonicalModelItem Item(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(document.RootElement);
    }
}

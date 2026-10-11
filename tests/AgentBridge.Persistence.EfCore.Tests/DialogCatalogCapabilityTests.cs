using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual public catalogue/feed/lifecycle с transactional doubles; SQL atomicity этими тестами не подтверждается.</summary>
public class DialogCatalogCapabilityTests
{
    /// <summary>Create атомарно фиксирует empty projection/root/profile/change; user/assistant metadata только из saved items.</summary>
    [Fact]
    public async Task SavedQuestionAndAssistantMetadataAreAtomicAndNeverIncludeTools()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState empty = await fixture.CreateAsync();
        Assert.Equal("Новый чат", empty.Text.Title);
        Assert.Null(empty.LastSavedMessageAtUtc);
        Assert.Empty(empty.Text.Snippet);
        Assert.Equal(DialogReadiness.Ready, empty.Readiness);
        Assert.Single(fixture.Committed.Roots);
        Assert.Single(fixture.Committed.Catalog);
        Assert.Single(fixture.Committed.Changes);
        Assert.True(empty.Profile.Matches(CatalogWriteFixture.PROFILE));

        DialogRunState begun = await fixture.BeginAsync(empty.Token, question: "  Мой\n вопрос 😀 ");
        DialogSnapshot original = await fixture.SnapshotAsync(begun.Token);
        Assert.Equal("Мой вопрос 😀", original.Catalog!.Text.Title);
        Assert.Equal(CatalogWriteFixture.NOW, original.Catalog.LastSavedMessageAtUtc);
        Assert.DoesNotContain("PRIVATE", original.Catalog.Text.Snippet);
        Assert.Equal(CatalogWriteFixture.NOW, Assert.Single(fixture.Committed.Items).SavedAtUtc);

        fixture.Time.Now = CatalogWriteFixture.NOW.AddSeconds(1);
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([CatalogWriteFixture.Assistant(" Видимый\nответ "),
            new(JsonSerializer.SerializeToElement(new { type = "reasoning", private_data = "PRIVATE" }))]));
        DialogWriteToken appended = (await fixture.Writer.AppendAsync(new DialogRunWriteAccess(fixture.Access(begun.Token), begun.Lease),
            begun.Token, begun.Lease.TurnId, step.Response.Output, [step], TestContext.Current.CancellationToken)).Data!;
        fixture.Time.Now = CatalogWriteFixture.NOW.AddSeconds(2);
        DialogContinuationState finished = (await fixture.Lifecycle.FinalizeAsync(new(fixture.Access(appended), appended, begun.Lease,
            DialogTurnStatus.Canceled), TestContext.Current.CancellationToken)).Data!;
        fixture.Reload();
        DialogCatalogState state = (await fixture.CatalogReader.ReadStateAsync(fixture.Access(finished.Token), TestContext.Current.CancellationToken)).Data!;
        Assert.Equal("Мой вопрос 😀", state.Text.Title);
        Assert.Equal("Видимый ответ", state.Text.Snippet);
        Assert.Equal(CatalogWriteFixture.NOW.AddSeconds(1), state.LastSavedMessageAtUtc);
        Assert.Null(fixture.Committed.Items.Last().SavedAtUtc);
        Assert.Equal(DialogReadiness.Ready, finished.Readiness);
        Assert.Equal(DialogTurnStatus.Canceled, finished.Terminal);
        Assert.NotNull(Assert.Single(fixture.Committed.Turns).SettingsJson);
    }

    /// <summary>Один bounded slice, scope isolation, DESC RFC UUID tie-break и progress cursor; snapshots не читаются.</summary>
    [Fact]
    public async Task KeysetReadsAreBoundedStableScopedAndIndependentOfHistory()
    {
        CatalogWriteFixture fixture = new();
        Guid lower = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        Guid upper = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
        await fixture.CreateAsync(lower);
        await fixture.CreateAsync(upper);
        DialogCatalogScope other = CatalogWriteFixture.SCOPE with { SiteId = "other" };
        Assert.True((await fixture.Creator.CreateAsync(new(DialogId.From(Guid.NewGuid()), other, CatalogWriteFixture.NOW,
            null, CatalogWriteFixture.PROFILE, CatalogWriteFixture.POLICY), TestContext.Current.CancellationToken)).Success);
        fixture.Reload();
        int canonicalReads = fixture.CanonicalReads;
        int catalogReads = fixture.CatalogQueries.ReadCalls;
        DialogCatalogSlice first = (await fixture.CatalogReader.ReadAsync(new(CatalogWriteFixture.SCOPE, null, 1,
            CatalogWriteFixture.NOW), TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(upper, Assert.Single(first.Candidates).Token.DialogId.Value);
        Assert.Equal(1, fixture.CatalogQueries.LastTake);
        Assert.Equal(catalogReads + 1, fixture.CatalogQueries.ReadCalls);
        Assert.Equal(canonicalReads, fixture.CanonicalReads);
        Assert.NotNull(first.Next);

        DialogCatalogSlice second = (await fixture.CatalogReader.ReadAsync(new(CatalogWriteFixture.SCOPE, first.Next, 1,
            CatalogWriteFixture.NOW), TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(lower, Assert.Single(second.Candidates).Token.DialogId.Value);
        ServiceResult<DialogCatalogSlice> mismatch = await fixture.CatalogReader.ReadAsync(new(other, first.Next, 1,
            CatalogWriteFixture.NOW), TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Validation, mismatch.Error!.Type);
        Assert.Equal(ServiceErrorType.Validation, (await fixture.CatalogReader.ReadAsync(new(CatalogWriteFixture.SCOPE, null, 257,
            CatalogWriteFixture.NOW), TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Projection refusal/unknown schema/save failure не создают canonical либо частичную projection.</summary>
    [Theory]
    [InlineData("policy")]
    [InlineData("schema")]
    [InlineData("projector")]
    [InlineData("save")]
    public async Task FailuresRollbackCanonicalProjectionAndFeed(string failure)
    {
        CatalogWriteFixture fixture = new();
        if (failure == "policy")
        {
            ServiceResult<DialogCatalogState> rejected = await fixture.Creator.CreateAsync(new(DialogId.From(Guid.NewGuid()),
                CatalogWriteFixture.SCOPE, CatalogWriteFixture.NOW, null, CatalogWriteFixture.PROFILE,
                CatalogWriteFixture.POLICY with { Version = 2 }), TestContext.Current.CancellationToken);
            Assert.Equal(ServiceErrorType.Unsupported, rejected.Error!.Type);
            Assert.Empty(fixture.Committed.Roots);
            Assert.Empty(fixture.Committed.Catalog);
            Assert.Empty(fixture.Committed.Changes);
            return;
        }

        DialogCatalogState state = await fixture.CreateAsync();
        string before = JsonSerializer.Serialize(fixture.Committed);
        if (failure == "projector") fixture.Projection.Refuse = true;
        if (failure == "save") fixture.BeforeSave = () => throw new IOException("save failure");
        DialogRunBegin begin = new(fixture.Access(state.Token), state.Token, Guid.NewGuid(),
            [CatalogWriteFixture.Question("question", failure == "schema" ? 2 : 1)],
            new("gpt-5", "high", 900, 10, 1000), TimeSpan.FromMinutes(1), CatalogWriteFixture.SCOPE, CatalogWriteFixture.PROFILE);
        if (failure == "save")
            await Assert.ThrowsAsync<IOException>(() => fixture.Lifecycle.BeginAsync(begin, TestContext.Current.CancellationToken));
        else
            Assert.Equal(ServiceErrorType.Unsupported, (await fixture.Lifecycle.BeginAsync(begin, TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Committed));
    }

    /// <summary>Unknown commit до/после apply перечитывается новым instance по прежнему ID, без rollback fiction.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateUnknownCommitIsResolvedByAuthoritativeCompactRead(bool applied)
    {
        CatalogWriteFixture fixture = new();
        Guid id = Guid.NewGuid();
        IOException primary = new("unknown acknowledgement");
        if (applied) fixture.AfterCommit = () => throw primary;
        else fixture.BeforeCommit = () => throw primary;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => fixture.Creator.CreateAsync(new(DialogId.From(id),
            CatalogWriteFixture.SCOPE, CatalogWriteFixture.NOW, null, CatalogWriteFixture.PROFILE, CatalogWriteFixture.POLICY),
            TestContext.Current.CancellationToken)));

        CatalogWriteFixture next = new(fixture.Committed);
        next.Reload();
        ServiceResult<DialogCatalogState> read = await next.CatalogReader.ReadStateAsync(new(DialogId.From(id),
            CatalogWriteFixture.SCOPE.OwnerId, CatalogWriteFixture.NOW), TestContext.Current.CancellationToken);
        Assert.Equal(applied, read.Success);
        if (applied) Assert.Equal(DialogReadiness.Ready, read.Data!.Readiness);
        else Assert.Equal(ServiceErrorType.NotFound, read.Error!.Type);
    }

    /// <summary>Active delete запрещён; fence не создаёт новый root, tombstone доминирует поздний writer/recreate.</summary>
    [Fact]
    public async Task DeleteFeedTombstoneCannotBeUndoneByLateWriterOrSameIdCreate()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState empty = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(empty.Token);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Deletion.DeleteAsync(fixture.Access(run.Token), run.Token,
            TestContext.Current.CancellationToken)).Error!.Type);
        fixture.Time.Now = run.Lease.DeadlineUtc;
        DialogContinuationState fenced = (await fixture.Lifecycle.FenceAsync(new(fixture.Access(run.Token), run.Token, run.Lease, Guid.NewGuid()),
            TestContext.Current.CancellationToken)).Data!;
        Assert.True((await fixture.Deletion.DeleteAsync(fixture.Access(fenced.Token), fenced.Token,
            TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Committed.Roots);
        Assert.Empty(fixture.Committed.Items);
        Assert.True(Assert.Single(fixture.Committed.Catalog).Deleted);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Creator.CreateAsync(new(empty.Token.DialogId, CatalogWriteFixture.SCOPE,
            fixture.Time.Now, null, CatalogWriteFixture.PROFILE, CatalogWriteFixture.POLICY), TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.NotFound, (await fixture.Writer.AppendAsync(new DialogRunWriteAccess(fixture.Access(run.Token), run.Lease),
            run.Token, run.Lease.TurnId, [], [], TestContext.Current.CancellationToken)).Error!.Type);
        fixture.Reload();
        DialogCatalogChangeSlice feed = (await fixture.Feed.ReadChangesAsync(new(CatalogWriteFixture.SCOPE, 0, 256,
            fixture.Time.Now), TestContext.Current.CancellationToken)).Data!;
        Assert.Equal("delete", feed.Changes.Last().Kind);
        Assert.True(feed.Changes.Last().State.Deleted);
        Assert.Empty(feed.Changes.Last().State.Text.Title);
        Assert.Equal(fixture.Committed.Changes.Count, feed.NextCheckpoint);
    }

    /// <summary>Expiry equality исключает текст до physical cleanup; cleanup atomically пишет expiry tombstone.</summary>
    [Fact]
    public async Task ExpiryIsIndependentOfActivityAndHidesTextBeforeCleanup()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState state = await fixture.CreateAsync(expires: CatalogWriteFixture.NOW.AddSeconds(5));
        fixture.Time.Now = CatalogWriteFixture.NOW.AddSeconds(5);
        fixture.Reload();
        Assert.Empty((await fixture.CatalogReader.ReadAsync(new(CatalogWriteFixture.SCOPE, null, 4, fixture.Time.Now),
            TestContext.Current.CancellationToken)).Data!.Candidates);
        DialogCatalogState expired = (await fixture.CatalogReader.ReadStateAsync(fixture.Access(state.Token),
            TestContext.Current.CancellationToken)).Data!;
        Assert.Empty(expired.Text.Title);
        Assert.Equal(ServiceErrorType.Expired, (await fixture.Reader.ReadCatalogAsync(fixture.FullAccess(state.Token),
            TestContext.Current.CancellationToken)).Error!.Type);
        Assert.True((await fixture.ExpiryDeletion.DeleteAsync(state.Token, fixture.Time.Now, TestContext.Current.CancellationToken)).Success);
        Assert.Equal("expiry", fixture.Committed.Changes.Last().Kind);
    }

    /// <summary>Feed gap/unknown checkpoint явно ResetRequired, а не успешная пустая страница.</summary>
    [Fact]
    public async Task FeedRejectsGapsAndUnknownCheckpoints()
    {
        CatalogWriteFixture fixture = new();
        await fixture.CreateAsync();
        await fixture.CreateAsync();
        fixture.Committed.Changes.RemoveAt(0);
        fixture.Reload();
        ServiceResult<DialogCatalogChangeSlice> gap = await fixture.Feed.ReadChangesAsync(new(CatalogWriteFixture.SCOPE, 0, 1,
            CatalogWriteFixture.NOW), TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Unsupported, gap.Error!.Type);
        Assert.Equal("catalog_reset_required", gap.Error.Message);
        Assert.Equal(ServiceErrorType.Unsupported, (await fixture.Feed.ReadChangesAsync(new(CatalogWriteFixture.SCOPE, 100, 1,
            CatalogWriteFixture.NOW), TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Unicode scalars/normalization и literal wildcard symbols проверяются pure production helper.</summary>
    [Fact]
    public void TitleNormalizationDoesNotSplitSurrogatesOrTreatSearchAsWildcard()
    {
        Assert.Equal("a 😀", DialogCatalogText.Limit(" a\t\n 😀 z", 3));
        string title = "е\u0301 %_[]";
        string key = DialogCatalogText.SearchKey(title);
        Assert.Equal("É %_[]", DialogCatalogText.SearchKey("e\u0301 %_[]"));
        Assert.Contains("%_[]", key, StringComparison.Ordinal);
        Assert.Equal(key, DialogCatalogText.SearchKey(title.Normalize()));
    }
}

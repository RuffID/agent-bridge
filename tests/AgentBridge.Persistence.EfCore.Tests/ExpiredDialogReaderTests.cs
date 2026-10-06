using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Ограниченные кандидаты очистки через точный base predicate API, без запуска удаления.</summary>
public class ExpiredDialogReaderTests
{
    /// <summary>Expiry равен now включается; порядок expiry/ID применяется до take, token сохраняется полностью.</summary>
    [Fact]
    public async Task CandidatesApplyUtcBoundaryAndOrderingBeforeLimit()
    {
        DateTimeOffset now = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        FakeBaseRepository<DialogRecord> repository = new();
        DialogRecord first = Record("00000000-0000-0000-0000-000000000001", now.AddTicks(-1));
        DialogRecord second = Record("00000000-0000-0000-0000-000000000002", now);
        repository.Records.AddRange([
            Record("00000000-0000-0000-0000-000000000004", now.AddTicks(1)),
            Record("00000000-0000-0000-0000-000000000003", now), second, first]);
        ExpiredDialogReader reader = new(new DialogRecordQueries(new FakeDialogByIdRepository(repository), repository), new());
        using CancellationTokenSource source = new();
        ServiceResult<IReadOnlyList<DialogWriteToken>> result = await reader.ReadAsync(now, 2, source.Token);
        Assert.True(result.Success);
        IReadOnlyList<DialogWriteToken> candidates = Assert.IsAssignableFrom<IReadOnlyList<DialogWriteToken>>(result.Data);
        Assert.Equal([first.Id, second.Id], candidates.Select(token => token.DialogId.Value));
        Assert.Equal(first.IncarnationId, candidates[0].IncarnationId);
        Assert.Equal(first.Revision, candidates[0].Revision);
        Assert.Equal(second.IncarnationId, candidates[1].IncarnationId);
        Assert.Equal(second.Revision, candidates[1].Revision);
        Assert.Equal(2, repository.LastTake);
        Assert.True(repository.LastAsNoTracking);
        Assert.Equal(source.Token, repository.LastCancellationToken);
        Assert.Equal(4, repository.Records.Count);
        Assert.Empty(repository.Deleted);
    }

    /// <summary>Отсутствие кандидатов — успешная пустая неизменяемая выборка.</summary>
    [Fact]
    public async Task EmptyCandidatesAreSuccessful()
    {
        FakeBaseRepository<DialogRecord> repository = new();
        ExpiredDialogReader reader = new(new DialogRecordQueries(new FakeDialogByIdRepository(repository), repository), new());
        Assert.Empty((await reader.ReadAsync(DateTimeOffset.UnixEpoch, 1, cancellationToken: TestContext.Current.CancellationToken)).Data!);
    }

    /// <summary>Неположительный limit не превращается в неограниченное чтение base API.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidLimitFailsBeforeBaseRead(int limit)
    {
        FakeBaseRepository<DialogRecord> repository = new();
        ExpiredDialogReader reader = new(new DialogRecordQueries(new FakeDialogByIdRepository(repository), repository), new());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(DateTimeOffset.UnixEpoch, limit, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, repository.ReadCalls);
    }

    /// <summary>Ненулевое смещение UTC отклоняется до base read, не теряя точности expiry.</summary>
    [Fact]
    public async Task NonUtcFailsBeforeBaseRead()
    {
        FakeBaseRepository<DialogRecord> repository = new();
        ExpiredDialogReader reader = new(new DialogRecordQueries(new FakeDialogByIdRepository(repository), repository), new());
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(1)), 1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, repository.ReadCalls);
    }

    /// <summary>Отмена до выборки не превращается в успешный пустой список.</summary>
    [Fact]
    public async Task CallerCancellationPropagates()
    {
        FakeBaseRepository<DialogRecord> repository = new();
        ExpiredDialogReader reader = new(new DialogRecordQueries(new FakeDialogByIdRepository(repository), repository), new());
        using CancellationTokenSource source = new();
        source.Cancel();
        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(() => reader.ReadAsync(DateTimeOffset.UnixEpoch, 1, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(0, repository.ReadCalls);
    }

    /// <summary>Создаёт синтетический сохраняемый token кандидата.</summary>
    private static DialogRecord Record(string id, DateTimeOffset expiresAtUtc) => new()
    {
        Id = Guid.Parse(id), IncarnationId = Guid.NewGuid(), Revision = 17, ExpiresAtUtc = expiresAtUtc
    };
}

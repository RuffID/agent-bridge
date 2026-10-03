using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Порядок и отказ технического scope на fake transaction без БД, hosting и фоновых задач.</summary>
public class UnitOfWorkScopeTests
{
    /// <summary>Успех появляется только после save, commit, dispose и очистки.</summary>
    [Fact]
    public async Task SuccessWaitsForCommitAndCleanup()
    {
        FakeUnitOfWorkSession session = new();
        UnitOfWorkScope scope = new(session, new());
        ServiceResult result = await scope.ExecuteAsync(_ => Task.FromResult(ServiceResult.Ok()), default);
        Assert.True(result.Success);
        Assert.Equal(["begin", "save", "commit", "dispose", "clear"], session.Events);
    }

    /// <summary>Ожидаемый отказ не сохраняет даже ошибочно staged state.</summary>
    [Fact]
    public async Task ExpectedFailureRollsBackWithoutSave()
    {
        FakeUnitOfWorkSession session = new();
        ServiceError error = new(ServiceErrorType.Forbidden, "Отказ.");
        ServiceResult result = await new UnitOfWorkScope(session, new()).ExecuteAsync(_ => Task.FromResult(ServiceResult.Fail(error)), default);
        Assert.Same(error, result.Error);
        Assert.Equal(["begin", "rollback", "dispose", "clear"], session.Events);
    }

    /// <summary>Неожиданные begin/action/save failures остаются исключениями; автоматического retry нет.</summary>
    [Theory]
    [InlineData("begin")]
    [InlineData("action")]
    [InlineData("save")]
    public async Task UnexpectedFailureIsNotConflict(string phase)
    {
        FakeUnitOfWorkSession session = new();
        InvalidOperationException error = new("synthetic");
        if (phase == "begin") { session.BeginError = error; }
        if (phase == "save") { session.OnSave = () => throw error; }
        UnitOfWorkScope scope = new(session, new());
        Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ExecuteAsync(_ =>
            phase == "action" ? throw error : Task.FromResult(ServiceResult.Ok()), default));
        Assert.Same(error, actual);
        Assert.DoesNotContain("commit", session.Events);
        Assert.Equal(1, session.Events.Count(item => item == "begin"));
        Assert.Equal(phase == "begin" ? "begin" : "clear", session.Events[^1]);
    }

    /// <summary>Отмена save не отменяет обязательный rollback/cleanup.</summary>
    [Fact]
    public async Task CancellationStillRollsBack()
    {
        using CancellationTokenSource cancellation = new();
        FakeUnitOfWorkSession session = new() { OnSave = () => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); } };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new UnitOfWorkScope(session, new()).ExecuteAsync(
            _ => Task.FromResult(ServiceResult.Ok()), cancellation.Token));
        Assert.Equal(["begin", "save", "rollback", "dispose", "clear"], session.Events);
    }

    /// <summary>Неизвестный commit либо cleanup запрещает повторное использование scoped-контекста.</summary>
    [Theory]
    [InlineData("commit")]
    [InlineData("rollback")]
    [InlineData("dispose")]
    [InlineData("clear")]
    public async Task UnknownOutcomePoisonsScope(string phase)
    {
        FakeUnitOfWorkSession session = new();
        InvalidOperationException error = new("synthetic");
        if (phase == "commit") { session.Transaction.CommitError = error; }
        if (phase == "rollback") { session.Transaction.RollbackError = error; }
        if (phase == "dispose") { session.Transaction.DisposeError = error; }
        if (phase == "clear") { session.ClearError = error; }
        UnitOfWorkScope scope = new(session, new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ExecuteAsync(_ => Task.FromResult(
            phase == "rollback" ? ServiceResult.Fail(new ServiceError(ServiceErrorType.Conflict, "Отказ.")) : ServiceResult.Ok()), default));
        int calls = session.Events.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ExecuteAsync(_ => Task.FromResult(ServiceResult.Ok()), default));
        Assert.Equal(calls, session.Events.Count);
    }

    /// <summary>Первичная ошибка сохраняется вместе с вторичными cleanup exceptions.</summary>
    [Fact]
    public async Task PartialFailurePreservesAllExceptions()
    {
        FakeUnitOfWorkSession session = new();
        InvalidOperationException primary = new("primary");
        session.OnSave = () => throw primary;
        session.Transaction.RollbackError = new IOException("rollback");
        session.Transaction.DisposeError = new IOException("dispose");
        AggregateException actual = await Assert.ThrowsAsync<AggregateException>(() => new UnitOfWorkScope(session, new()).ExecuteAsync(
            _ => Task.FromResult(ServiceResult.Ok()), default));
        Assert.Same(primary, actual.InnerExceptions[0]);
        Assert.Equal(3, actual.InnerExceptions.Count);
        Assert.Equal("clear", session.Events[^1]);
    }

    /// <summary>Root CAS failure является Conflict; неизвестный concurrency failure без root evidence остаётся исключением.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyProvenRootConcurrencyIsExpected(bool hasRoot)
    {
        using AgentBridgeDbContext metadata = new(new DbContextOptionsBuilder<AgentBridgeDbContext>()
            .UseSqlite("Data Source=never-open.db").Options);
        DbUpdateConcurrencyException error = hasRoot
            ? new FakeRootConcurrencyException(metadata.Entry(new DialogRecord()))
            : new DbUpdateConcurrencyException("synthetic");
        FakeUnitOfWorkSession session = new() { OnSave = () => throw error };
        Task<ServiceResult> operation = new UnitOfWorkScope(session, new()).ExecuteAsync(_ => Task.FromResult(ServiceResult.Ok()), default);
        if (hasRoot) { Assert.Equal(ServiceErrorType.Conflict, (await operation).Error!.Type); }
        else { Assert.Same(error, await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => operation)); }
        Assert.Equal(["begin", "save", "rollback", "dispose", "clear"], session.Events);
    }

    /// <summary>Управляемое незавершённое await отклоняет вторую write/read operation без параллельных tasks.</summary>
    [Fact]
    public async Task SharedGateRejectsReadAndWriteWhileFirstOperationIsPending()
    {
        TaskCompletionSource completion = new();
        FakeUnitOfWorkSession session = new() { OnSave = () => completion.Task };
        PersistenceOperationGate gate = new();
        UnitOfWorkScope scope = new(session, gate);
        Task<ServiceResult> first = scope.ExecuteAsync(_ => Task.FromResult(ServiceResult.Ok()), default);
        Assert.False(first.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ExecuteAsync(_ => Task.FromResult(ServiceResult.Ok()), default));
        FakeBaseRepository<DialogRecord> rows = new();
        ExpiredDialogReader reader = new(new DialogRecordQueries(new FakeDialogByIdRepository(rows), rows), gate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(DateTimeOffset.UnixEpoch, 1));
        Assert.Equal(0, rows.ReadCalls);
        completion.SetResult();
        Assert.True((await first).Success);
        Assert.True((await reader.ReadAsync(DateTimeOffset.UnixEpoch, 1)).Success);
    }
}

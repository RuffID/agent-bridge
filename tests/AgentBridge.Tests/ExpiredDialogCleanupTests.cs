using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Public cleanup/DI с изолированными портами; реальные транзакции и cascade эти doubles не доказывают.</summary>
public class ExpiredDialogCleanupTests
{
    /// <summary>Регистрация без I/O сохраняет custom clock; один read и каждый delete имеют отдельный закрытый scope и fresh UTC.</summary>
    [Fact]
    public async Task OneBoundedBatchUsesFreshTimeAndIndependentAwaitedScopes()
    {
        Probe probe = new(3);
        await using ServiceProvider root = Root(probe);
        Assert.Equal(0, probe.Created);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        ExpiredDialogCleanupResult result = await cleanup.CleanupAsync(2, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ExpiredDialogCleanupStatus.Completed, result.Status);
        Assert.True(result.CandidatesRead);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(1, probe.Reads);
        Assert.Equal(3, probe.Created);
        Assert.Equal(probe.Created, probe.Disposed);
        Assert.Equal(new[] { 1, 2, 3 }, probe.Operations.Select(operation => operation.Scope));
        Assert.Equal(new[] { probe.Start, probe.Start.AddSeconds(1), probe.Start.AddSeconds(2) }, probe.Operations.Select(operation => operation.Now));
        Assert.Same(result, cleanup.LastResult);
        Assert.Single(probe.Tokens.Skip(result.Candidates.Count));
    }

    /// <summary>Ожидаемый отказ не стирает соседние успехи и не refresh/retry исходный token.</summary>
    [Fact]
    public async Task ExpectedFailureContinuesBatchAndKeepsOriginalErrorAndTokens()
    {
        Probe probe = new(3);
        ServiceError error = new(ServiceErrorType.Conflict, "Кандидат изменился.");
        probe.Delete = (token, _) => Task.FromResult(ReferenceEquals(token, probe.Tokens[1]) ? ServiceResult.Fail(error) : ServiceResult.Ok());
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanupResult result = await caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>().CleanupAsync(3, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ExpiredDialogCleanupStatus.Partial, result.Status);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(new[] { ExpiredDialogDeletionStatus.Deleted, ExpiredDialogDeletionStatus.Failed, ExpiredDialogDeletionStatus.Deleted }, result.Candidates.Select(candidate => candidate.Status));
        Assert.Same(error, result.Candidates[1].Error);
        Assert.Null(result.Error);
        Assert.Equal(3, probe.Deletes);
        Assert.Equal(1, probe.Reads);
        Assert.Equal(probe.Tokens, result.Candidates.Select(candidate => candidate.Token));
    }

    /// <summary>Отказ чтения и успешный пустой пакет различаются; ни один не вызывает delete.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadFailureAndEmptyBatchAreExplicit(bool failed)
    {
        Probe probe = new(0);
        ServiceError error = new(ServiceErrorType.Rejected, "Чтение отклонено.");
        if (failed) probe.Read = (_, _) => Task.FromResult(ServiceResult<IReadOnlyList<DialogWriteToken>>.Fail(error));
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanupResult result = await caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>().CleanupAsync(1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(failed ? ExpiredDialogCleanupStatus.Failed : ExpiredDialogCleanupStatus.Completed, result.Status);
        Assert.Equal(!failed, result.CandidatesRead);
        Assert.Same(failed ? error : null, result.Error);
        Assert.Empty(result.Candidates);
        Assert.Equal(0, probe.Deletes);
        Assert.Equal(1, probe.Disposed);
    }

    /// <summary>Исходная отмена не создаёт scopes; отмена после success не теряет Deleted и останавливает следующий кандидат.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationPreservesConfirmedDeletion(bool beforeRead)
    {
        Probe probe = new(2);
        using CancellationTokenSource cancellation = new();
        if (beforeRead) cancellation.Cancel();
        else probe.Delete = (_, _) => { cancellation.Cancel(); return Task.FromResult(ServiceResult.Ok()); };
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanupResult result = await caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>().CleanupAsync(2, cancellation.Token);
        Assert.Equal(ExpiredDialogCleanupStatus.Canceled, result.Status);
        Assert.Equal(beforeRead ? 0 : 1, result.DeletedCount);
        Assert.Equal(beforeRead ? 0 : 1, probe.Deletes);
        if (beforeRead) { Assert.False(result.CandidatesRead); Assert.Empty(result.Candidates); Assert.Equal(0, probe.Created); }
        else Assert.Equal(new[] { ExpiredDialogDeletionStatus.Deleted, ExpiredDialogDeletionStatus.NotAttempted }, result.Candidates.Select(candidate => candidate.Status));
        Assert.Equal(probe.Created, probe.Disposed);
    }

    /// <summary>Отмена во время порта даёт Unknown, ожидание закончено и следующий кандидат не вызывается.</summary>
    [Fact]
    public async Task CancellationDuringDeletionKeepsPartialUnknownAndNotAttempted()
    {
        Probe probe = new(3);
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Delete = async (token, ct) =>
        {
            if (ReferenceEquals(token, probe.Tokens[0])) return ServiceResult.Ok();
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException();
        };
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        Task<ExpiredDialogCleanupResult> running = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>().CleanupAsync(3, cancellation.Token);
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken); }
        finally { cancellation.Cancel(); await running; }
        ExpiredDialogCleanupResult result = await running;
        Assert.Equal(ExpiredDialogCleanupStatus.Canceled, result.Status);
        Assert.Equal(new[] { ExpiredDialogDeletionStatus.Deleted, ExpiredDialogDeletionStatus.Unknown, ExpiredDialogDeletionStatus.NotAttempted }, result.Candidates.Select(candidate => candidate.Status));
        Assert.Equal(2, probe.Deletes);
        Assert.Equal(probe.Created, probe.Disposed);
    }

    /// <summary>Исключение прерывает пакет и распространяется без raw message в LastResult; повторный вызов имеет новый snapshot.</summary>
    [Fact]
    public async Task UnexpectedFailurePropagatesAndLastResultIsImmutable()
    {
        Probe probe = new(3);
        IOException primary = new("synthetic-secret");
        probe.Delete = (token, _) => ReferenceEquals(token, probe.Tokens[1]) ? Task.FromException<ServiceResult>(primary) : Task.FromResult(ServiceResult.Ok());
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => cleanup.CleanupAsync(3, cancellationToken: TestContext.Current.CancellationToken)));
        ExpiredDialogCleanupResult result = cleanup.LastResult!;
        Assert.Equal(ExpiredDialogCleanupStatus.Interrupted, result.Status);
        Assert.Equal(new[] { ExpiredDialogDeletionStatus.Deleted, ExpiredDialogDeletionStatus.Unknown, ExpiredDialogDeletionStatus.NotAttempted }, result.Candidates.Select(candidate => candidate.Status));
        Assert.Equal(2, probe.Deletes);
        Assert.Equal(probe.Created, probe.Disposed);
        Assert.DoesNotContain("synthetic-secret", System.Text.Json.JsonSerializer.Serialize(result));
        probe.Delete = (_, _) => Task.FromResult(ServiceResult.Ok());
        Assert.Equal(ExpiredDialogCleanupStatus.Completed, (await cleanup.CleanupAsync(1, cancellationToken: TestContext.Current.CancellationToken)).Status);
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(2, probe.Reads);
    }

    /// <summary>Ошибка освобождения не стирает подтверждённый success/expected отказ; batch останавливается.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeFailureKeepsAcknowledgedPortOutcome(bool failed)
    {
        Probe probe = new(2) { FailDisposeAt = 2 };
        ServiceError expected = new(ServiceErrorType.Conflict, "Неактуален.");
        if (failed) probe.Delete = (_, _) => Task.FromResult(ServiceResult.Fail(expected));
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        await Assert.ThrowsAsync<IOException>(() => cleanup.CleanupAsync(2, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ExpiredDialogCleanupStatus.Interrupted, cleanup.LastResult!.Status);
        Assert.Equal(failed ? ExpiredDialogDeletionStatus.Failed : ExpiredDialogDeletionStatus.Deleted, cleanup.LastResult.Candidates[0].Status);
        Assert.Same(failed ? expected : null, cleanup.LastResult.Candidates[0].Error);
        Assert.Equal(ExpiredDialogDeletionStatus.NotAttempted, cleanup.LastResult.Candidates[1].Status);
        Assert.Equal(1, probe.Deletes);
    }

    /// <summary>Primary и Dispose failures сохраняются вместе; aggregate отмены не маскирует cleanup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrimaryAndCleanupFailuresAreBothObservable(bool cancel)
    {
        Probe probe = new(1) { FailDisposeAt = 2 };
        using CancellationTokenSource cancellation = new();
        Exception primary = cancel ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("primary");
        probe.Delete = (_, _) => { if (cancel) cancellation.Cancel(); return Task.FromException<ServiceResult>(primary); };
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => cleanup.CleanupAsync(1, cancellation.Token));
        Assert.Same(primary, error.InnerExceptions[0]);
        Assert.IsType<IOException>(error.InnerExceptions[1]);
        Assert.Equal(ExpiredDialogCleanupStatus.Interrupted, cleanup.LastResult!.Status);
        Assert.Equal(ExpiredDialogDeletionStatus.Unknown, cleanup.LastResult.Candidates[0].Status);
    }

    /// <summary>OCE из DisposeAsync при уже отменённом caller не маскируется как обычная отмена и не стирает success.</summary>
    [Fact]
    public async Task DisposeCancellationIsAnUnexpectedCleanupFailure()
    {
        using CancellationTokenSource cancellation = new();
        Probe probe = new(2) { FailDisposeAt = 2, DisposeError = new OperationCanceledException(cancellation.Token) };
        probe.Delete = (_, _) => { cancellation.Cancel(); return Task.FromResult(ServiceResult.Ok()); };
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        Assert.Same(probe.DisposeError, await Assert.ThrowsAsync<OperationCanceledException>(() => cleanup.CleanupAsync(2, cancellation.Token)));
        Assert.Equal(ExpiredDialogCleanupStatus.Interrupted, cleanup.LastResult!.Status);
        Assert.Equal(1, cleanup.LastResult.DeletedCount);
        Assert.Equal(ExpiredDialogDeletionStatus.NotAttempted, cleanup.LastResult.Candidates[1].Status);
    }

    /// <summary>Отмена источника без caller cancellation распространяется; read cleanup не разрешает начать deletion.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadOrReadScopeFailureDoesNotDelete(bool dispose)
    {
        Probe probe = new(1);
        if (dispose) probe.FailDisposeAt = 1;
        else probe.Read = (_, _) => Task.FromException<ServiceResult<IReadOnlyList<DialogWriteToken>>>(new OperationCanceledException());
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        if (dispose) await Assert.ThrowsAsync<IOException>(() => cleanup.CleanupAsync(1, cancellationToken: TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<OperationCanceledException>(() => cleanup.CleanupAsync(1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ExpiredDialogCleanupStatus.Interrupted, cleanup.LastResult!.Status);
        Assert.Equal(dispose, cleanup.LastResult.CandidatesRead);
        Assert.All(cleanup.LastResult.Candidates, candidate => Assert.Equal(ExpiredDialogDeletionStatus.NotAttempted, candidate.Status));
        Assert.Equal(0, probe.Deletes);
        Assert.Equal(1, probe.Disposed);
    }

    /// <summary>Некорректный limit и нарушающий limit/unique IDs reader fail-fast до удаления.</summary>
    [Theory]
    [InlineData("limit")]
    [InlineData("oversize")]
    [InlineData("duplicate")]
    public async Task InvalidBoundsAndReaderContractDoNotDelete(string kind)
    {
        Probe probe = new(2);
        if (kind != "limit") probe.Read = (_, _) => Task.FromResult(ServiceResult<IReadOnlyList<DialogWriteToken>>.Ok(
            kind == "duplicate" ? new[] { probe.Tokens[0], probe.Tokens[0] } : probe.Tokens));
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        if (kind == "limit") await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cleanup.CleanupAsync(0, cancellationToken: TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => cleanup.CleanupAsync(kind == "duplicate" ? 2 : 1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, probe.Deletes);
        Assert.Equal(kind == "limit" ? 0 : 1, probe.Reads);
    }

    /// <summary>Параллельный вызов одного экземпляра отклоняется без второго read и без потери отчёта первого.</summary>
    [Fact]
    public async Task ConcurrentCallIsRejectedAndAllStartedWorkIsAwaited()
    {
        Probe probe = new(1);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Delete = async (_, _) => { entered.TrySetResult(); await release.Task; return ServiceResult.Ok(); };
        await using ServiceProvider root = Root(probe);
        using IServiceScope caller = root.CreateScope();
        ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        Task<ExpiredDialogCleanupResult> running = cleanup.CleanupAsync(1, cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => cleanup.CleanupAsync(1, cancellationToken: TestContext.Current.CancellationToken));
        }
        finally { release.TrySetResult(); await running; }
        Assert.Same(await running, cleanup.LastResult);
        Assert.Equal(1, probe.Reads);
        Assert.Equal(probe.Created, probe.Disposed);
    }

    /// <summary>Строит actual DI регистрацию с custom clock и изолированными scoped портами.</summary>
    private static ServiceProvider Root(Probe probe)
    {
        ServiceCollection services = new();
        services.AddSingleton(probe);
        services.AddSingleton<TimeProvider>(probe.Clock);
        services.AddAgentBridgeDialogCleanup();
        services.AddAgentBridgeDialogCleanup();
        services.AddScoped<Ports>();
        services.AddScoped<IExpiredDialogReader>(sp => sp.GetRequiredService<Ports>());
        services.AddScoped<IExpiredDialogDeletion>(sp => sp.GetRequiredService<Ports>());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Наблюдает границы операций и задаёт контролируемые отказы без БД.</summary>
    private class Probe(int count)
    {
        public readonly DateTimeOffset Start = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        public readonly Clock Clock = new();
        public readonly DialogWriteToken[] Tokens = Enumerable.Range(0, count).Select(_ => new DialogWriteToken(DialogId.From(Guid.NewGuid()), Guid.NewGuid(), 0)).ToArray();
        public readonly List<(int Scope, DateTimeOffset Now)> Operations = [];
        public int Created, Disposed, Reads, Deletes, FailDisposeAt;
        public Exception DisposeError = new IOException("cleanup");
        public Func<int, CancellationToken, Task<ServiceResult<IReadOnlyList<DialogWriteToken>>>>? Read;
        public Func<DialogWriteToken, CancellationToken, Task<ServiceResult>>? Delete;
    }

    /// <summary>Управляемое UTC-время для проверки свежего чтения каждого порта.</summary>
    private class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <inheritdoc cref="IExpiredDialogReader"/>
    private class Ports(Probe probe) : IExpiredDialogReader, IExpiredDialogDeletion, IAsyncDisposable
    {
        private readonly int id = ++probe.Created;
        private bool disposed;

        /// <inheritdoc/>
        public Task<ServiceResult<IReadOnlyList<DialogWriteToken>>> ReadAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken = default)
        {
            Observe(nowUtc);
            probe.Reads++;
            return probe.Read?.Invoke(limit, cancellationToken) ?? Task.FromResult(ServiceResult<IReadOnlyList<DialogWriteToken>>.Ok(probe.Tokens.Take(limit).ToArray()));
        }

        /// <inheritdoc/>
        public Task<ServiceResult> DeleteAsync(DialogWriteToken expected, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            Observe(nowUtc);
            probe.Deletes++;
            return probe.Delete?.Invoke(expected, cancellationToken) ?? Task.FromResult(ServiceResult.Ok());
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            if (disposed) return ValueTask.CompletedTask;
            disposed = true;
            probe.Disposed++;
            return id == probe.FailDisposeAt ? ValueTask.FromException(probe.DisposeError) : ValueTask.CompletedTask;
        }

        /// <summary>Проверяет завершение предыдущего scope до нового чтения или удаления.</summary>
        private void Observe(DateTimeOffset now)
        {
            Assert.Equal(id - 1, probe.Disposed);
            Assert.Equal(id, probe.Created);
            Assert.Equal(TimeSpan.Zero, now.Offset);
            probe.Operations.Add((id, now));
            probe.Clock.Now = now.AddSeconds(1);
        }
    }
}

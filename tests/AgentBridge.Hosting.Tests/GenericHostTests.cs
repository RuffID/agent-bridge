using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Responses;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Hosting.Tests;

/// <summary>Настоящий Generic Host с facade; shutdown bridge принадлежит только тестовому приложению.</summary>
public class GenericHostTests
{
    /// <summary>Composition/start/stop не запускают HTTP, storage, cleanup, migration или maintenance.</summary>
    [Fact]
    public async Task StartAndStopArePassiveWithoutAppWorker()
    {
        HostingFixture fixture = new();
        try
        {
            Assert.NotNull(fixture.Host.Services.GetRequiredService<IStartupValidator>());
            Assert.Empty(fixture.Host.Services.GetServices<IHostedService>());
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.False(fixture.Host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
            await fixture.Host.StopAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.True(fixture.Host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.IsCancellationRequested);
            Assert.Equal(0, fixture.HttpFactories);
            Assert.Equal(0, fixture.Handler.Calls);
            Assert.Empty(fixture.Keys);
            Assert.Empty(fixture.Roots);
        }
        finally { await fixture.CleanupAsync(); }
    }

    /// <summary>Build не валидирует options: именно Host.StartAsync вызывает зарегистрированный IStartupValidator.</summary>
    [Theory]
    [InlineData("AgentBridge:Agent:MaxToolSteps", "0")]
    [InlineData("AgentBridge:Agent:InstructionsSource", "invalid")]
    [InlineData("AgentBridge:Agent:Instructions", null)]
    [InlineData("AgentBridge:Retention:RetentionPeriod", "00:00:00")]
    [InlineData("AgentBridge:Retention:SoftContentLimitBytes", "-1")]
    [InlineData("AgentBridge:Compaction:TokenThreshold", "0")]
    [InlineData("AgentBridge:Compaction:InputTokenReserve", "-1")]
    [InlineData("AgentBridge:Compaction:MaxPasses", "invalid")]
    [InlineData("CodexLb:BaseAddress", "https://user:synthetic-secret@example.invalid")]
    [InlineData("CodexLb:Model", null)]
    [InlineData("CodexLb:ReasoningEffort", null)]
    [InlineData("CodexLb:KeySource", "invalid")]
    [InlineData("CodexLb:SharedApiKey", null)]
    [InlineData("CodexLb:GenerationTimeout", "00:00:00")]
    [InlineData("CodexLb:CompactTimeout", "invalid")]
    [InlineData("Database:Provider", "invalid")]
    [InlineData("Database:ConnectionString", null)]
    public async Task StartupValidationRejectsInvalidOptionsBeforeAppWork(string path, string? value)
    {
        HostingFixture fixture = new(new() { [path] = value }, worker: true);
        Exception? expected = null;
        try
        {
            Assert.NotNull(fixture.Host.Services.GetRequiredService<IStartupValidator>());
            OptionsValidationException error = await Assert.ThrowsAsync<OptionsValidationException>(() =>
                fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken));
            expected = error;
            Assert.Contains(path.Split(':').Last(), error.Message);
            Assert.DoesNotContain("synthetic-secret", error.ToString());
            Assert.DoesNotContain("synthetic-password", error.ToString());
            Assert.Null(fixture.Worker!.ExecuteTask);
            Assert.Empty(fixture.Keys);
            Assert.Empty(fixture.Roots);
            Assert.Equal(0, fixture.HttpFactories);
            Assert.Equal(0, fixture.Handler.Calls);
        }
        finally { await fixture.CleanupAsync(expected); }
    }

    /// <summary>Независимые invalid options агрегируются настоящим startup validator без потери отдельных причин.</summary>
    [Fact]
    public async Task StartupValidationAggregatesIndependentFailures()
    {
        HostingFixture fixture = new(new()
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "0", ["CodexLb:GenerationTimeout"] = "00:00:00",
            ["Database:ConnectionString"] = null
        }, worker: true);
        try
        {
            AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken));
            Assert.Equal(3, error.Flatten().InnerExceptions.Count);
            Assert.All(error.Flatten().InnerExceptions, cause => Assert.IsType<OptionsValidationException>(cause));
            Assert.Null(fixture.Worker!.ExecuteTask);
            Assert.Equal(0, fixture.Handler.Calls);
            Assert.Empty(fixture.Roots);
        }
        finally { await fixture.CleanupAsync(); }
    }

    /// <summary>Individual без source отклоняется при actual StartAsync, до фабрики или работы hosted service.</summary>
    [Fact]
    public async Task IndividualModeRequiresAppSourceOnHostStartup()
    {
        HostingFixture fixture = new(new() { ["CodexLb:KeySource"] = "Individual" }, worker: true, source: false);
        try
        {
            OptionsValidationException error = await Assert.ThrowsAsync<OptionsValidationException>(() => fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken));
            Assert.Contains(nameof(IIndividualModelKeySource), error.Message);
            Assert.Null(fixture.Worker!.ExecuteTask);
            Assert.Equal(0, fixture.HttpFactories);
            Assert.Equal(0, fixture.Handler.Calls);
        }
        finally { await fixture.CleanupAsync(); }
    }

    /// <summary>Обычные scopes используют независимые source/reader; disposal принадлежит app container.</summary>
    [Fact]
    public async Task SyncScopesHaveIndependentSourcesAndReleaseThem()
    {
        HostingFixture fixture = new();
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.Throws<InvalidOperationException>(() => fixture.Host.Services.GetRequiredService<AgentRunner>());
            using (IServiceScope first = fixture.Host.Services.CreateScope())
            using (IServiceScope second = fixture.Host.Services.CreateScope())
            {
                IModelSettingsReader a = first.ServiceProvider.GetRequiredService<IModelSettingsReader>();
                IModelSettingsReader b = second.ServiceProvider.GetRequiredService<IModelSettingsReader>();
                Assert.Same(a, first.ServiceProvider.GetRequiredService<IModelSettingsReader>());
                Assert.NotSame(a, b);
                Assert.True((await a.ReadAsync(fixture.Call.OwnerId, ct: TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken)).Success);
                Assert.True((await b.ReadAsync(fixture.Call.OwnerId, ct: TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken)).Success);
                Assert.Equal(2, fixture.Keys.Count);
                Assert.NotSame(fixture.Keys[0], fixture.Keys[1]);
                Assert.All(fixture.Keys, key => { Assert.Equal(1, key.Calls); Assert.False(key.Disposed); });
            }
            Assert.All(fixture.Keys, key => { Assert.True(key.Disposed); Assert.False(key.AsyncDisposed); });
            Assert.Equal(2, fixture.Handler.Calls);
            Assert.All(fixture.Handler.Bodies, body => Assert.True(body.Disposed));
            Assert.False(fixture.Handler.Disposed);
        }
        finally { await fixture.CleanupAsync(); }
    }

    /// <summary>Actual runner/read/UoW/DbContext scoped; host stop не закрывает scope, созданный приложением.</summary>
    [Fact]
    public async Task AsyncScopesKeepRealScopedGraphAndAwaitDisposal()
    {
        HostingFixture fixture = new();
        AsyncServiceScope first = fixture.Host.Services.CreateAsyncScope();
        AsyncServiceScope second = fixture.Host.Services.CreateAsyncScope();
        bool disposed = false;
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            AgentRunner a = first.ServiceProvider.GetRequiredService<AgentRunner>();
            AgentRunner b = second.ServiceProvider.GetRequiredService<AgentRunner>();
            Assert.Same(a, first.ServiceProvider.GetRequiredService<AgentRunner>());
            Assert.NotSame(a, b);
            Assert.IsType<DialogReader>(first.ServiceProvider.GetRequiredService<IDialogReader>());
            Assert.IsType<DialogTurnUnitOfWork>(first.ServiceProvider.GetRequiredService<IDialogTurnWriter>());
            Assert.IsType<CodexLbModelGateway>(first.ServiceProvider.GetRequiredService<IModelGateway>());
            AgentBridgeDbContext context = first.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
            Assert.NotSame(context, second.ServiceProvider.GetRequiredService<AgentBridgeDbContext>());
            Assert.NotSame(first.ServiceProvider.GetRequiredService<IDialogTurnWriter>(), second.ServiceProvider.GetRequiredService<IDialogTurnWriter>());
            Assert.NotSame(first.ServiceProvider.GetRequiredService<UnitOfWorkScope>(), second.ServiceProvider.GetRequiredService<UnitOfWorkScope>());

            await fixture.Host.StopAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.All(fixture.Keys, key => Assert.False(key.Disposed));
            await first.DisposeAsync().AsTask().WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await second.DisposeAsync().AsTask().WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            disposed = true;
            Assert.All(fixture.Keys, key => Assert.True(key.AsyncDisposed));
            Assert.All(fixture.Roots, root => Assert.True(root.Disposed));
            Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Entries().ToArray());
            Assert.Equal(0, fixture.Handler.Calls);
        }
        finally
        {
            if (!disposed)
            {
                fixture.KeyDisposeRelease.TrySetResult();
                try { await HostingFixture.AwaitCleanupAsync(first.DisposeAsync().AsTask()); }
                finally
                {
                    try { await HostingFixture.AwaitCleanupAsync(second.DisposeAsync().AsTask()); }
                    finally { await fixture.CleanupAsync(); }
                }
            }
            else await fixture.CleanupAsync();
        }
    }

    /// <summary>Остановка app worker отменяет actual runner HTTP и ожидает transport плюс async scope.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostStopCancelsRealRunnerAndWaitsForOperationAndScope(bool body)
    {
        HostingFixture fixture = new(worker: true) { HoldKeyDisposal = true };
        fixture.Handler.BlockSend = !body;
        fixture.Handler.BlockBody = body;
        Task? stop = null;
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await fixture.Handler.Entered.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            stop = fixture.Host.StopAsync(TestContext.Current.CancellationToken);
            await fixture.Handler.Canceled.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.True(fixture.Handler.ObservedToken.IsCancellationRequested);
            Assert.False(stop.IsCompleted);
            fixture.Handler.Release.TrySetResult();
            await fixture.KeyDisposeEntered.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.False(stop.IsCompleted);
            Assert.Equal(AgentRunStatus.Canceled, fixture.Worker!.Result!.Status);
            fixture.KeyDisposeRelease.TrySetResult();
            await stop.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await fixture.Worker.ExecuteTask!.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.Null(fixture.Worker.Failure);
            Assert.All(fixture.Keys, key => Assert.True(key.AsyncDisposed));
            Assert.All(fixture.Roots, root => { Assert.Equal(2, root.Reads); Assert.True(root.Disposed); });
            Assert.Equal(1, fixture.Handler.Calls);
            Assert.All(fixture.Handler.Bodies, stream => Assert.True(stream.Disposed));
            Assert.False(fixture.Handler.Disposed);
        }
        finally
        {
            try { await fixture.CleanupAsync(); }
            finally { if (stop is not null) await HostingFixture.AwaitCleanupAsync(stop); }
        }
    }

    /// <summary>Без app bridge host lifetime не отменяет активный actual pipeline и не освобождает app scope.</summary>
    [Fact]
    public async Task HostStopDoesNotAutomaticallyCancelApplicationOperation()
    {
        HostingFixture fixture = new();
        fixture.Handler.BlockSend = true;
        using CancellationTokenSource operationToken = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        AsyncServiceScope scope = fixture.Host.Services.CreateAsyncScope();
        Task? operation = null;
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            operation = scope.ServiceProvider.GetRequiredService<IModelSettingsReader>().ReadAsync(fixture.Call.OwnerId, ct: operationToken.Token);
            await fixture.Handler.Entered.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await fixture.Host.StopAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.False(operation.IsCompleted);
            Assert.False(fixture.Handler.ObservedToken.IsCancellationRequested);
            Assert.False(Assert.Single(fixture.Keys).Disposed);

            operationToken.Cancel();
            await fixture.Handler.Canceled.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            fixture.Handler.Release.TrySetResult();
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken));
            Assert.Equal(operationToken.Token, error.CancellationToken);
        }
        finally
        {
            fixture.Handler.Release.TrySetResult();
            operationToken.Cancel();
            try
            {
                if (operation is not null)
                {
                    try { await HostingFixture.AwaitCleanupAsync(operation); }
                    catch (OperationCanceledException) when (operation.IsCanceled) { }
                }
            }
            finally
            {
                try { await HostingFixture.AwaitCleanupAsync(scope.DisposeAsync().AsTask()); }
                finally { await fixture.CleanupAsync(); }
            }
        }
    }

    /// <summary>Actual HTTP read failure и body/scope cleanup остаются наблюдаемы через runner, host logger и app StopAsync.</summary>
    [Fact]
    public async Task RunnerHttpFailureKeepsPrimaryAndSecondaryErrorsDuringShutdown()
    {
        HostingFixture fixture = new(worker: true);
        IOException primary = new("synthetic read failure");
        InvalidOperationException bodyCleanup = new("synthetic body cleanup");
        InvalidOperationException scopeCleanup = new("synthetic app scope cleanup");
        fixture.Handler.ReadFailure = primary;
        fixture.Handler.DisposeFailure = bodyCleanup;
        fixture.KeyDisposalFailure = scopeCleanup;
        Exception? expected = null;
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await fixture.Handler.Entered.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            fixture.Handler.Release.TrySetResult();
            await fixture.Worker!.Finished.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            expected = fixture.Worker.Failure;
            AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => fixture.Host.StopAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken));
            Assert.Same(expected, error);
            Assert.Same(primary, error.InnerExceptions[0]);
            Assert.Same(scopeCleanup, error.InnerExceptions[1]);
            IReadOnlyList<Exception> secondary = Assert.IsAssignableFrom<IReadOnlyList<Exception>>(primary.Data["HttpClientLibrary.CleanupExceptions"]);
            Assert.Contains(secondary, cause => ReferenceEquals(cause, bodyCleanup));
            Assert.Same(error, await Assert.ThrowsAsync<AggregateException>(() => fixture.Worker.ExecuteTask!.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken)));
            await fixture.Logger.ErrorLogged.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.Contains(fixture.Logger.Errors, cause => ReferenceEquals(cause, error));
            Assert.All(fixture.Roots, root => Assert.True(root.Disposed));
            Assert.All(fixture.Keys, key => Assert.True(key.AsyncDisposed));
            Assert.All(fixture.Handler.Bodies, stream => Assert.True(stream.Disposed));
            Assert.Equal(1, fixture.Handler.Calls);
        }
        finally { await fixture.CleanupAsync(expected); }
    }

    /// <summary>Actual AgentRunScope сохраняет read+scope failures до HTTP и до write UoW.</summary>
    [Fact]
    public async Task RunnerReadFailureKeepsShortScopeCleanupError()
    {
        HostingFixture fixture = new(worker: true);
        InvalidOperationException primary = new("synthetic repository failure");
        InvalidOperationException secondary = new("synthetic read scope cleanup");
        fixture.ReadFailure = primary;
        fixture.ReadDisposalFailure = secondary;
        Exception? expected = null;
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await fixture.Worker!.Finished.Task.WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            expected = fixture.Worker.Failure;
            AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => fixture.Host.StopAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken));
            Assert.Same(expected, error);
            Assert.Same(primary, error.InnerExceptions[0]);
            Assert.Same(secondary, error.InnerExceptions[1]);
            Assert.Equal(0, fixture.Handler.Calls);
            Assert.True(Assert.Single(fixture.Roots, root => root.Reads > 0).Disposed);
        }
        finally { await fixture.CleanupAsync(expected); }
    }

    /// <summary>Borrowed client/handler/logger переживают catalog, scope disposal, host stop и host DisposeAsync.</summary>
    [Fact]
    public async Task BorrowedResourcesRemainOwnedByApplication()
    {
        HostingFixture fixture = new();
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await using (AsyncServiceScope scope = fixture.Host.Services.CreateAsyncScope())
            {
                Assert.True((await scope.ServiceProvider.GetRequiredService<IModelSettingsReader>().ReadAsync(fixture.Call.OwnerId, ct: TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken)).Success);
            }
            await fixture.Host.StopAsync(TestContext.Current.CancellationToken).WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            await ((IAsyncDisposable)fixture.Host).DisposeAsync().AsTask().WaitAsync(HostingFixture.BUDGET, TestContext.Current.CancellationToken);
            Assert.False(fixture.Handler.Disposed);
            Assert.False(fixture.Logger.Disposed);
            fixture.Client.CancelPendingRequests();
            Assert.NotNull(fixture.Logger.CreateLogger("after-host-dispose"));
            Assert.True(Assert.Single(fixture.Handler.Bodies).Disposed);
        }
        finally { await fixture.CleanupAsync(); }
    }
}

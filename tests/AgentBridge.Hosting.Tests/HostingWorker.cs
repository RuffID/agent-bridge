using System.Runtime.ExceptionServices;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentBridge.Hosting.Tests;

/// <summary>Bridge тестового приложения: связывает stoppingToken с actual AgentRunner и наблюдает ошибки при StopAsync.</summary>
/// <remarks>AgentBridge не регистрирует этот worker и не предоставляет такую shutdown-политику.</remarks>
internal class HostingWorker(IServiceScopeFactory scopes, HostingFixture fixture) : BackgroundService
{
    internal readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal AgentRunResult? Result;
    internal Exception? Failure;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using CancellationTokenSource operation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, fixture.Abort.Token);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        Exception? failure = null;
        try
        {
            AgentRunRequest request = new(fixture.Call, [], [], new(5, 5, 1, HostingFixture.BUDGET));
            Result = await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(request, cancellationToken: operation.Token);
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { await scope.DisposeAsync().AsTask().WaitAsync(HostingFixture.BUDGET); }
            catch (Exception cleanup)
            {
                failure = failure is null ? cleanup : new AggregateException("Ошибка сценария и scope приложения.", failure, cleanup);
            }
            Failure = failure;
            Finished.TrySetResult();
        }

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <inheritdoc/>
    /// <remarks>Стандартный BackgroundService.StopAsync не обещает вернуть первичную ошибку ExecuteTask; это политика приложения.</remarks>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (Failure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

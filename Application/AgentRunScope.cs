using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Application;

/// <summary>Ожидает отдельный async scope и сохраняет primary+cleanup ошибки чтения либо короткой записи.</summary>
internal static class AgentRunScope
{
    /// <summary>Не оставляет начатый scope; отдельно сообщает cleanup-only origin, сохраняя исходное исключение.</summary>
    public static async Task<T> ExecuteAsync<T>(IServiceScopeFactory scopes, Func<IServiceProvider, Task<T>> operation,
        Action<Exception>? onCleanupFailure = null)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        Exception? primary = null;
        try { return await operation(scope.ServiceProvider); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { await scope.DisposeAsync(); }
            catch (Exception cleanup) when (primary is not null)
            {
                throw new AggregateException("Ошибка операции и освобождения её scope.", primary, cleanup);
            }
            catch (Exception cleanup)
            {
                onCleanupFailure?.Invoke(cleanup);
                throw;
            }
        }
    }
}

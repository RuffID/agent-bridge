using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Application;

/// <summary>Ожидает отдельный async scope и сохраняет primary+cleanup ошибки чтения либо короткой записи.</summary>
internal static class AgentRunScope
{
    /// <summary>Не оставляет начатый scope и не подменяет исходную ошибку исключением DisposeAsync.</summary>
    public static async Task<T> ExecuteAsync<T>(IServiceScopeFactory scopes, Func<IServiceProvider, Task<T>> operation)
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
        }
    }
}

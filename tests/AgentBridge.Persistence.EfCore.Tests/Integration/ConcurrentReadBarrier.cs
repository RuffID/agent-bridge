using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Согласует две первые реальные root-выборки разных контекстов; завершение ограничено по времени.</summary>
public class ConcurrentReadBarrier : DbCommandInterceptor
{
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int reads;
    /// <inheritdoc/>
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FROM \"Dialogs\"", StringComparison.Ordinal) && Interlocked.Increment(ref reads) <= 2)
        {
            if (Volatile.Read(ref reads) == 2) { ready.TrySetResult(); }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        return result;
    }
}

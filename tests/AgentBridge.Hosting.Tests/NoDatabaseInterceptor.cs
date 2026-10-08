using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentBridge.Hosting.Tests;

/// <summary>Fail-fast запрещает даже попытку открыть БД в isolated hosting-проекте.</summary>
internal class NoDatabaseInterceptor : DbConnectionInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        throw new InvalidOperationException("Hosting-тест не разрешает database I/O.");

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Hosting-тест не разрешает database I/O.");
}

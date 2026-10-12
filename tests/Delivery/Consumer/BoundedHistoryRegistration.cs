using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Compile-only вызовы actual B03 API; host задаёт все limits и авторизует scope/profile отдельно.</summary>
public static class BoundedHistoryRegistration
{
    /// <summary>Подключает политику вместе с существующим SqlServer/persistence/facade; I/O не выполняется.</summary>
    public static IServiceCollection AddLoadPolicy(this IServiceCollection services,
        int maxRecords, long maxRecordBytes, int maxQueries) =>
        services.AddAgentBridgeSqlServerBoundedHistory(new(maxRecords, maxRecordBytes, maxQueries));

    /// <summary>Читает полную разрешённую историю или возвращает safe Rejected/history_load_limit_exceeded.</summary>
    public static Task<ServiceResult<DialogSnapshot>> ReadAuthorizedHistoryAsync(IBoundedDialogReader reader,
        DialogCatalogAccess authorizedAccess, CancellationToken cancellationToken) =>
        reader.ReadCatalogAsync(authorizedAccess, cancellationToken);

    /// <summary>Требует bounded generation в actual runner; не запускается compile-only проверкой.</summary>
    public static Task<AgentRunResult> RunAuthorizedTurnAsync(AgentRunner runner,
        AgentRunRequest authorizedRequest, CancellationToken cancellationToken) =>
        runner.RunBoundedAsync(authorizedRequest, cancellationToken: cancellationToken);
}

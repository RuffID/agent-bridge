using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Bounded compact metadata без чтения полной истории.</summary>
public interface IDialogCatalogReader
{
    /// <summary>Bounded compact metadata без чтения полной истории.</summary>
    Task<ServiceResult<DialogCatalogSlice>> ReadAsync(DialogCatalogRead request, CancellationToken cancellationToken = default);
    /// <summary>Authoritative metadata прежнего ID, включая tombstone; expired текст скрывается.</summary>
    Task<ServiceResult<DialogCatalogState>> ReadStateAsync(DialogAccess access, CancellationToken cancellationToken = default);
}

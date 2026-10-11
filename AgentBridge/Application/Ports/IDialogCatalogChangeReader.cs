using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Bounded durable change feed; неизвестный checkpoint/gap означает ResetRequired.</summary>
public interface IDialogCatalogChangeReader
{
    /// <summary>Bounded durable change feed; неизвестный checkpoint/gap означает ResetRequired.</summary>
    Task<ServiceResult<DialogCatalogChangeSlice>> ReadChangesAsync(DialogCatalogChangeRead request, CancellationToken cancellationToken = default);
}

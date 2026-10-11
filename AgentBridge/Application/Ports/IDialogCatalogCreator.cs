using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Регистрация root/profile/projection одной transaction.</summary>
public interface IDialogCatalogCreator
{
    /// <summary>Регистрация root/profile/projection одной transaction.</summary>
    Task<ServiceResult<DialogCatalogState>> CreateAsync(DialogCatalogCreate request, CancellationToken cancellationToken = default);
}

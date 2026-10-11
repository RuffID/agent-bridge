using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Авторизованная host-проверка завершения исполнителя; не устанавливает outcome инструмента.</summary>
public interface IDialogRunQuiescenceVerifier
{
    /// <summary>Проверяет evidence для exact incarnation/turn/lease/epoch вне storage transaction.</summary>
    Task<ServiceResult> VerifyAsync(DialogRunFence request, CancellationToken cancellationToken = default);
}

using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Короткая атомарная граница явного удаления диалога и всех зависимых состояний.</summary>
public interface IDialogDeletion
{
    /// <summary>Проверяет существование, access/token ID, владельца и incarnation/revision; удаляет также просроченный диалог.</summary>
    /// <remarks>Удаление инвалидирует все прежние tokens; отказ не меняет данные. Старые результаты не восстанавливают историю.</remarks>
    Task<ServiceResult> DeleteAsync(DialogAccess access, DialogWriteToken expected, CancellationToken cancellationToken = default);
}

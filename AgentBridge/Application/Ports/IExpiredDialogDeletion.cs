using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Короткая атомарная операция системной очистки одного кандидата вместе с зависимыми данными.</summary>
public interface IExpiredDialogDeletion
{
    /// <summary>Повторно проверяет существование, incarnation/revision и истечение в момент удаления;
    /// изменившийся или заново созданный кандидат не удаляется. Расписанием и правом очистки владеет приложение.</summary>
    Task<ServiceResult> DeleteAsync(DialogWriteToken expected, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

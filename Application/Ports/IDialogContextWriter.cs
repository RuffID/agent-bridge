using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Короткая атомарная граница принятия нового рабочего окна после внешнего compact.</summary>
public interface IDialogContextWriter
{
    /// <summary>Проверяет существование, access/token ID, владельца, срок, incarnation/revision и terminal prefix;
    /// принимает следующую версию окна и сохраняет историю. Отказ оставляет прежнее окно актуальным.</summary>
    /// <remarks>Prefix не откатывается и не включает InProgress/дыры. Compaction должен иметь статус Completed;
    /// метаданные prefix не дают права пропустить отдельные элементы. Полный envelope сохраняется отдельно от input-items.
    /// Сеть завершена до вызова; будущий адаптер использует общий сценарный scope/UoW.</remarks>
    Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, CancellationToken cancellationToken = default);
}

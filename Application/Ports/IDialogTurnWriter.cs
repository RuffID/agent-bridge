using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Ports;

/// <summary>Короткие атомарные изменения одного обращения; ни один метод не удерживает транзакцию между вызовами.</summary>
/// <remarks>Каждая операция повторно проверяет существование, совпадение access/token ID, владельца,
/// nowUtc &lt; ExpiresAtUtc, сохраняемые incarnation/revision и состояние обращения в одной границе с записью.
/// Отказ не меняет данные. Успех возвращает следующую версию. Поздняя запись не создаёт отсутствующий диалог.
/// Будущий адаптер использует общий сценарный scope/UoW; EF-сессия не удерживается во время сети.</remarks>
public interface IDialogTurnWriter
{
    /// <summary>Атомарно начинает обращение с порядком начала и сохраняет его исходный input.</summary>
    Task<ServiceResult<DialogWriteToken>> BeginAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, IReadOnlyList<CanonicalModelItem> input, CancellationToken cancellationToken = default);
    /// <summary>Добавляет новые канонические элементы текущего обращения без признания terminal completion.</summary>
    Task<ServiceResult<DialogWriteToken>> AppendAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> modelSteps,
        CancellationToken cancellationToken = default);
    /// <summary>Добавляет ещё не сохранённые элементы и фиксирует конечный статус; InProgress и повторное завершение запрещены.</summary>
    Task<ServiceResult<DialogWriteToken>> FinishAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, DialogTurnStatus status, IReadOnlyList<CanonicalModelItem> newItems, IReadOnlyList<StoredModelStep> modelSteps,
        CancellationToken cancellationToken = default);
}

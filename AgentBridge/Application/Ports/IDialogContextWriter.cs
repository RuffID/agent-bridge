using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Короткая атомарная граница принятия нового рабочего окна после внешнего compact.</summary>
public interface IDialogContextWriter
{
    /// <summary>Epoch-aware compact; учитывает recovery revision через root CAS.</summary>
    Task<ServiceResult<DialogWriteToken>> SaveWithModelAsync(DialogRunWriteAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, string selectedModel, CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new(ServiceErrorType.Unsupported, "run_fencing_unsupported")));
    /// <summary>Сохраняет provenance выбранной модели отдельно от canonical данных; legacy writer явно отказывается.</summary>
    Task<ServiceResult<DialogWriteToken>> SaveWithModelAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, string selectedModel,
        CancellationToken cancellationToken = default) => Task.FromResult(ServiceResult<DialogWriteToken>.Fail(
            new(ServiceErrorType.Unsupported, "Writer не поддерживает provenance модели compact.")));
    /// <summary>Проверяет существование, access/token ID, владельца, срок, incarnation/revision и terminal prefix;
    /// принимает следующую версию окна и сохраняет историю. Отказ оставляет прежнее окно актуальным.</summary>
    /// <remarks>Prefix не откатывается и не включает InProgress/дыры. Compaction должен иметь статус Completed;
    /// метаданные prefix не дают права пропустить отдельные элементы. Полный envelope сохраняется отдельно от input-items.
    /// Сеть завершена до вызова; EF-адаптер использует общий сценарный scope/UoW.</remarks>
    Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, CancellationToken cancellationToken = default);
}

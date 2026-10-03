using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Явный перевод доменных отказов и принятой версии в независимый прикладной результат.</summary>
internal static class DialogWriteResults
{
    /// <summary>Сохраняет смысл ожидаемого отказа; неизвестный enum не маскируется.</summary>
    public static ServiceError Error(DialogMutationResult result) => result switch
    {
        DialogMutationResult.OwnerMismatch => new(ServiceErrorType.Forbidden, "Владелец не совпадает."),
        DialogMutationResult.Deleted or DialogMutationResult.TurnNotFound => new(ServiceErrorType.NotFound, "Состояние не найдено."),
        DialogMutationResult.Expired => new(ServiceErrorType.Expired, "Срок диалога истёк."),
        DialogMutationResult.StaleOperation or DialogMutationResult.DuplicateTurn or
            DialogMutationResult.TurnAlreadyFinished or DialogMutationResult.UnfinishedContextRange =>
            new(ServiceErrorType.Conflict, "Изменение несовместимо с текущим состоянием диалога."),
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };

    /// <summary>Переносит только разрешённые изменяемые метаданные после успешного доменного изменения.</summary>
    public static DialogWriteToken Apply(DialogRecord root, Dialog dialog, long addedBytes)
    {
        long bytes = checked(root.ContentBytes + addedBytes);
        root.Revision = dialog.Revision;
        root.LastChangedAtUtc = dialog.LastChangedAtUtc;
        root.ContentBytes = bytes;
        return new DialogWriteToken(dialog.Id, root.IncarnationId, root.Revision);
    }
}

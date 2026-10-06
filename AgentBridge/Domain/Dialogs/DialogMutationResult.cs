namespace AgentBridge.Domain.Dialogs;

/// <summary>Независимый от HTTP и прикладного слоя результат проверки доменной операции.</summary>
public enum DialogMutationResult
{
    /// <summary>Операция выполнена.</summary>
    Success,
    /// <summary>Переданная идентичность не совпадает с владельцем.</summary>
    OwnerMismatch,
    /// <summary>Диалог уже удалён.</summary>
    Deleted,
    /// <summary>Срок доступности диалога истёк.</summary>
    Expired,
    /// <summary>Снимок относится к другой жизни диалога или устаревшей версии.</summary>
    StaleOperation,
    /// <summary>Обращение с указанной идентичностью уже существует.</summary>
    DuplicateTurn,
    /// <summary>Обращение отсутствует в этом диалоге.</summary>
    TurnNotFound,
    /// <summary>Обращение уже имеет конечный статус.</summary>
    TurnAlreadyFinished,
    /// <summary>В покрываемый префикс контекста попало выполняющееся обращение.</summary>
    UnfinishedContextRange
}

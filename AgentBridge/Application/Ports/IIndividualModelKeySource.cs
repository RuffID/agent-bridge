using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Ports;

/// <summary>Источник индивидуального ключа приложения; не является подтверждением прав владельца.</summary>
public interface IIndividualModelKeySource
{
    /// <summary>Возвращает ключ без нормализации; только null означает отсутствие. Ошибка источника не разрешает общий ключ.</summary>
    Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default);
}

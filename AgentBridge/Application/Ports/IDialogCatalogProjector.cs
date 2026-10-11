using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Чистый synchronous projector приложения, привязанный к exact key/version, без I/O.</summary>
public interface IDialogCatalogProjector
{
    /// <summary>Чистый synchronous projector приложения, привязанный к exact key/version, без I/O.</summary>
    string Key { get; }
    /// <summary>Поддерживаемая exact версия.</summary>
    int Version { get; }
    /// <summary>Проецирует только разрешённый текст принимаемого saved сообщения.</summary>
    ServiceResult<DialogCatalogTextUpdate> Project(SavedDialogMessage message, DialogCatalogTextState current, DialogProjectionPolicy policy);
}

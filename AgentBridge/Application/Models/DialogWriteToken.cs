using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Условие записи по сохраняемой идентичности жизни и версии, полученным от хранилища.</summary>
/// <remarks>Не конвертируется из DialogStateVersion. Сам тип не обеспечивает persistence, rehydration или атомарность.</remarks>
public class DialogWriteToken
{
    /// <summary>Фиксирует прочитанные значения; новая жизнь того же ID должна иметь другой сохраняемый incarnation.</summary>
    public DialogWriteToken(DialogId dialogId, Guid incarnationId, long revision)
    {
        ArgumentNullException.ThrowIfNull(dialogId);
        if (incarnationId == Guid.Empty)
        {
            throw new ArgumentException("Идентичность жизни обязательна.", nameof(incarnationId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        DialogId = dialogId;
        IncarnationId = incarnationId;
        Revision = revision;
    }

    /// <summary>Идентичность диалога.</summary>
    public DialogId DialogId { get; }
    /// <summary>Сохраняемая идентичность жизни, общая после загрузки разными экземплярами.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Ожидаемая версия; обновление возвращает новый token.</summary>
    public long Revision { get; }
}

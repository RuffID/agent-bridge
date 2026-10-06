namespace AgentBridge.Domain.Dialogs;

/// <summary>Неизменяемый снимок актуальности перед выполнением внешней операции.</summary>
/// <remarks>
/// Снимок действует для жизни объекта в памяти. Повторная загрузка и persistent concurrency не реализованы.
/// Не является блокировкой или авторизацией пользователя; хранилище должно атомарно проверять актуальность.
/// </remarks>
public class DialogStateVersion
{
    private readonly Guid _lifetimeId;

    /// <summary>Создаёт снимок только внутри границы доменного агрегата.</summary>
    internal DialogStateVersion(DialogId dialogId, Guid lifetimeId, long revision)
    {
        DialogId = dialogId;
        _lifetimeId = lifetimeId;
        Revision = revision;
    }

    /// <summary>Идентичность диалога, к которому относится операция.</summary>
    public DialogId DialogId { get; }
    /// <summary>Версия всего агрегата на момент получения снимка.</summary>
    public long Revision { get; }

    /// <summary>Проверяет жизнь диалога и его версию внутри агрегата.</summary>
    internal bool Matches(Guid lifetimeId, long revision) => _lifetimeId == lifetimeId && Revision == revision;
}

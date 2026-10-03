namespace AgentBridge.Domain.Dialogs;

/// <inheritdoc/>
/// <remarks>Непустая идентичность диалога, независимая от транспорта и хранилища.</remarks>
public class DialogId : IEquatable<DialogId>
{
    /// <summary>Фиксирует проверенное значение идентичности.</summary>
    private DialogId(Guid value) => Value = value;

    /// <summary>Значение идентификатора, предоставленное приложением.</summary>
    public Guid Value { get; }

    /// <summary>Создаёт идентификатор из непустого значения.</summary>
    public static DialogId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор диалога не должен быть пустым.", nameof(value));
        }

        return new DialogId(value);
    }

    /// <inheritdoc/>
    public bool Equals(DialogId? other) => other is not null && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DialogId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();
}

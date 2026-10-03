namespace AgentBridge.Domain.Dialogs;

/// <inheritdoc/>
/// <remarks>Непустая идентичность владельца в пространстве идентификаторов приложения.</remarks>
public class DialogOwnerId : IEquatable<DialogOwnerId>
{
    /// <summary>Фиксирует проверенную идентичность владельца.</summary>
    private DialogOwnerId(string value) => Value = value;

    /// <summary>Исходное значение без нормализации и привязки к Telegram или WPF.</summary>
    public string Value { get; }

    /// <summary>Создаёт идентичность владельца без изменения регистра или пробелов.</summary>
    public static DialogOwnerId From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new DialogOwnerId(value);
    }

    /// <inheritdoc/>
    public bool Equals(DialogOwnerId? other) => other is not null && StringComparer.Ordinal.Equals(Value, other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DialogOwnerId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
}

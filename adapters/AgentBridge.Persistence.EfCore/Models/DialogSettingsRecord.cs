using EFCoreLibrary.Abstractions.Entity;
using AgentBridge.Application.Models;

namespace AgentBridge.Persistence.EfCore.Models;

/// <inheritdoc/>
/// <remarks>Отдельная строка settings; её concurrency не инвалидирует revision текущего хода.</remarks>
public class DialogSettingsRecord : IEntity<Guid>
{
    /// <inheritdoc/>
    public Guid Id { get; set; }
    /// <summary>Независимая CAS версия выбора.</summary>
    public long Version { get; set; }
    /// <summary>Точный selected model, не server model.</summary>
    public string Model { get; set; } = string.Empty;
    /// <summary>Сохранённый effort.</summary>
    public string Effort { get; set; } = string.Empty;
    /// <summary>Возвращает независимый проверенный snapshot.</summary>
    public DialogModelSelection ToSelection() => new(Version, Model, Effort);
}

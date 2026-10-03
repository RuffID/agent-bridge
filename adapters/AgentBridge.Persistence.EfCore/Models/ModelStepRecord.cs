using EFCoreLibrary.Abstractions.Entity;

namespace AgentBridge.Persistence.EfCore.Models;

/// <inheritdoc/>
/// <remarks>Отдельный результат шага модели; envelope не становится item следующего input.</remarks>
public class ModelStepRecord : IEntity<Guid>
{
    /// <inheritdoc/>
    public Guid Id { get; set; }
    /// <summary>Диалог родительского обращения.</summary>
    public Guid DialogId { get; set; }
    /// <summary>Обязательное родительское обращение.</summary>
    public Guid TurnId { get; set; }
    /// <summary>Стабильный порядок выполнения шагов внутри обращения, начиная с единицы.</summary>
    public long Sequence { get; set; }
    /// <summary>Полный lifecycle-отчёт; чувствительные данные не логируются.</summary>
    public ModelResponseRecord Response { get; set; } = new();
}

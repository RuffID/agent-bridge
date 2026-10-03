using AgentBridge.Persistence.EfCore.Models;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Подготовленный пакет Infrastructure; не передаётся в Application и не подтверждает сохранение.</summary>
/// <param name="Items">Канонические items.</param>
/// <param name="Steps">Полные отчёты модели.</param>
/// <param name="AddedBytes">Размер новых текстовых колонок.</param>
public record PreparedTurnContent(IReadOnlyList<CanonicalItemRecord> Items, IReadOnlyList<ModelStepRecord> Steps, long AddedBytes);

using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace AgentBridge.Persistence.EfCore.Repositories;

/// <summary>Делегирует staging строк базовым репозиториям; не сохраняет и не подтверждает прикладную операцию.</summary>
/// <remarks>Используется только инфраструктурой сценария после проверок и доменного изменения.
/// Все операции участвуют в общей scoped session; сохранение и rollback принадлежат общему scope этапа 10.</remarks>
public class RecordStaging<TEntity>(
    IContextCreateItemRepository<TEntity, AgentBridgeContextKey> creator,
    IContextUpdateItemRepository<TEntity, AgentBridgeContextKey> updater,
    IContextDeleteItemRepository<TEntity, AgentBridgeContextKey> deleter) where TEntity : class
{
    /// <summary>Ставит одну строку на создание без сохранения.</summary>
    public void StageCreate(TEntity record) => creator.Create(record);
    /// <summary>Ставит набор строк на создание без сохранения.</summary>
    public void StageCreateRange(IEnumerable<TEntity> records) => creator.CreateRange(records);
    /// <summary>Ставит подготовленное сценарием изменение строки без сохранения.</summary>
    public void StageUpdate(TEntity record) => updater.Update(record);
    /// <summary>Ставит подготовленные сценарием изменения строк без сохранения.</summary>
    public void StageUpdateRange(IEnumerable<TEntity> records) => updater.UpdateRange(records);
    /// <summary>Ставит существующую строку на удаление без сохранения.</summary>
    public void StageDelete(TEntity record) => deleter.Delete(record);
    /// <summary>Ставит существующие строки на удаление без сохранения.</summary>
    public void StageDeleteRange(IEnumerable<TEntity> records) => deleter.DeleteRange(records);
}

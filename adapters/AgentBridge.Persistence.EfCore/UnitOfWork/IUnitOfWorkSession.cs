using Microsoft.EntityFrameworkCore.Storage;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Техническая Infrastructure-граница одной EFCoreLibrary session, без репозиториев и бизнес-решений.</summary>
public interface IUnitOfWorkSession
{
    /// <summary>Открывает сериализуемую transaction; внешние transaction и retry strategy запрещены.</summary>
    Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken);
    /// <summary>Сохраняет staged изменения общего контекста.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    /// <summary>Удаляет tracked state после завершения операции.</summary>
    void Clear();
}

using System.Runtime.ExceptionServices;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Общая техническая граница сценариев: transaction, save, rollback и очистка; делегаты принадлежат только Infrastructure.</summary>
/// <remarks>Не передавать сюда модель/инструмент/внешний I/O. Не использовать base staging вне этого scope.
/// Успех возвращается только после commit и cleanup. Неизвестный commit или cleanup блокирует scoped gate.</remarks>
public class UnitOfWorkScope(IUnitOfWorkSession session, PersistenceOperationGate gate,
    IEnumerable<IAgentBridgeDatabaseProvider> databaseProviders)
{
    /// <summary>Сохраняет прежний конструктор изолированного scope; provider-specific PK classification требует модулей.</summary>
    public UnitOfWorkScope(IUnitOfWorkSession session, PersistenceOperationGate gate) : this(session, gate, []) { }

    /// <summary>Выполняет короткую операцию с данными; распознаёт только доказанный root concurrency conflict.</summary>
    public Task<ServiceResult<T>> ExecuteAsync<T>(Func<CancellationToken, Task<ServiceResult<T>>> action,
        CancellationToken cancellationToken, bool creatingDialog = false, bool writingSettings = false) where T : class =>
        ExecuteCoreAsync(action, result => result.Success, ServiceResult<T>.Fail, cancellationToken, creatingDialog, writingSettings);

    /// <summary>Выполняет короткую команду без данных.</summary>
    public Task<ServiceResult> ExecuteAsync(Func<CancellationToken, Task<ServiceResult>> action, CancellationToken cancellationToken) =>
        ExecuteCoreAsync(action, result => result.Success, ServiceResult.Fail, cancellationToken, false, false);

    /// <summary>Владеет transaction до dispose и сохраняет первичную и вторичные ошибки без подмены Conflict.</summary>
    private async Task<T> ExecuteCoreAsync<T>(Func<CancellationToken, Task<T>> action, Func<T, bool> succeeded,
        Func<ServiceError, T> conflict, CancellationToken cancellationToken, bool creatingDialog, bool writingSettings)
    {
        using IDisposable lease = gate.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        IDbContextTransaction? transaction = null;
        T result = default!;
        List<Exception> failures = [];
        bool committed = false;
        bool commitStarted = false;
        try
        {
            transaction = await session.BeginAsync(cancellationToken);
            result = await action(cancellationToken);
            if (succeeded(result))
            {
                try
                {
                    await session.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException error) when (IsRootConflict(error, creatingDialog) || IsCatalogConflict(error) || writingSettings && IsSettingsConflict(error))
                {
                    result = conflict(new ServiceError(ServiceErrorType.Conflict, "Диалог изменён конкурентной операцией."));
                }
                if (succeeded(result))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    commitStarted = true;
                    await transaction.CommitAsync(cancellationToken);
                    committed = true;
                }
            }
        }
        catch (Exception error)
        {
            failures.Add(error);
            if (commitStarted || transaction is null)
            {
                gate.Poison();
            }
        }
        if (transaction is not null)
        {
            if (!committed)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                catch (Exception error) { failures.Add(error); gate.Poison(); }
            }
            try { await transaction.DisposeAsync(); }
            catch (Exception error) { failures.Add(error); gate.Poison(); }
        }
        // Если Begin не выдал принадлежащую нам transaction, не трогаем чужой tracker/transaction.
        if (transaction is not null)
        {
            try { session.Clear(); }
            catch (Exception error) { failures.Add(error); gate.Poison(); }
        }
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1)
        {
            throw new AggregateException("Ошибка операции и её завершения; scope больше не пригоден.", failures);
        }
        return result;
    }

    /// <summary>Не классифицирует driver/network/serialization failures и нарушения дочерних ограничений как stale token.</summary>
    private bool IsRootConflict(DbUpdateException error, bool creatingDialog)
    {
        if (error.Entries.Count != 1 || error.Entries[0].Entity is not DialogRecord)
        {
            return false;
        }
        if (!creatingDialog)
        {
            return error is DbUpdateConcurrencyException;
        }
        return databaseProviders.Any(provider => provider.IsPrimaryKeyViolation(error, "Dialogs", "PK_Dialogs"));
    }

    /// <summary>Распознаёт только CAS/PK конкретной строки выбора, без маскировки FK/driver/serialization ошибок.</summary>
    private bool IsSettingsConflict(DbUpdateException error)
    {
        if (error.Entries.Count != 1 || error.Entries[0].Entity is not DialogSettingsRecord) return false;
        return error is DbUpdateConcurrencyException || databaseProviders.Any(provider =>
            provider.IsPrimaryKeyViolation(error, "DialogSettings", "PK_DialogSettings"));
    }

    /// <summary>Только exact catalog/clock CAS либо их PK race; чужие FK/driver/commit errors не превращаются в Conflict.</summary>
    private bool IsCatalogConflict(DbUpdateException error)
    {
        if (error.Entries.Count != 1) return false;
        string? table = error.Entries[0].Entity switch
        {
            DialogCatalogRecord => "DialogCatalog",
            DialogCatalogClockRecord => "DialogCatalogClocks",
            DialogCatalogChangeRecord => "DialogCatalogChanges",
            DialogRecoveryOperationRecord => "DialogRecoveryOperations",
            _ => null
        };

        return table is not null && (error is DbUpdateConcurrencyException ||
            databaseProviders.Any(provider => provider.IsPrimaryKeyViolation(error, table, "PK_" + table)));
    }
}

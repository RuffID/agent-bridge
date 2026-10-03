using System.Data.Common;
using EFCoreLibrary.Maintenance;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <inheritdoc cref="IDatabaseMaintenanceProvider{TContextKey}"/>
/// <remarks>Только наблюдаемые fake операции; connection, SQL, native backup и процессы отсутствуют.</remarks>
public class FakeMaintenanceBoundary : IDatabaseMaintenanceProvider<AgentBridgeContextKey>, IRelationalMigrationOperations<AgentBridgeContextKey>
{
    /// <summary>Наблюдаемый порядок вызовов.</summary>
    public List<string> Calls { get; } = [];
    /// <summary>Подставное существование БД.</summary>
    public bool Exists { get; set; } = true;
    /// <summary>Подставная идентичность цели.</summary>
    public string Target { get; set; } = "synthetic-target";
    /// <summary>Неисполненные подставные migrations.</summary>
    public IReadOnlyList<string> Pending { get; set; } = ["synthetic-migration"];
    /// <summary>Управляемая ошибка или задержка inspection.</summary>
    public Func<MaintenanceBudget, Task>? InspectionAction { get; set; }
    /// <summary>Управляемая ошибка создания или collision.</summary>
    public Func<MaintenanceBudget, Task>? CreationAction { get; set; }
    /// <summary>Управляемый отказ backup/native/process boundary.</summary>
    public Func<MaintenanceBudget, Task>? BackupAction { get; set; }
    /// <summary>Управляемый partial failure migration.</summary>
    public Func<CancellationToken, Task>? MigrationAction { get; set; }
    /// <summary>Управляемое повреждение receipt.</summary>
    public Func<DatabaseBackupReceipt, DatabaseBackupReceipt>? ChangeReceipt { get; set; }
    /// <summary>Признак удержания подставной EF-сессии.</summary>
    public bool Pinned { get; private set; }
    /// <summary>Неизвестное завершение очистки сессии.</summary>
    public bool FailCleanup { get; set; }
    /// <summary>Имитирует pending после незавершённого обновления.</summary>
    public bool KeepPending { get; set; }

    /// <inheritdoc/>
    public DatabaseMaintenanceCapabilities Capabilities => new("sqlite", "sqlite database", "file main only", false, false);
    /// <inheritdoc/>
    public string ProviderName => "fake-never-connect";
    /// <inheritdoc/>
    public DbConnection Connection => throw new InvalidOperationException("Fake boundary запрещает доступ к connection.");
    /// <inheritdoc/>
    public bool HasTransaction => false;

    /// <inheritdoc/>
    public async Task<DatabaseInspection> InspectAsync(MaintenanceBudget budget)
    {
        Calls.Add("inspect");
        if (InspectionAction is not null) await InspectionAction(budget);
        budget.Check();
        return new DatabaseInspection(Exists, Target, []);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(MaintenanceBudget budget)
    {
        Calls.Add("create");
        if (CreationAction is not null) await CreationAction(budget);
        budget.Check();
        Exists = true;
    }

    /// <inheritdoc/>
    public async Task<DatabaseBackupReceipt> BackupAsync(Guid operationId, string targetIdentity, MaintenanceBudget budget)
    {
        Calls.Add("backup");
        if (!Pinned) throw new InvalidOperationException("Backup требует удержанную fake session.");
        if (BackupAction is not null) await BackupAction(budget);
        budget.Check();
        DatabaseBackupReceipt receipt = new(operationId, targetIdentity, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            new LocalBackupArtifact(Path.GetFullPath("fake-never-created-backup"), 42, new string('A', 64)),
            Capabilities.Provider, Capabilities.Format, Capabilities.Scope);
        return ChangeReceipt?.Invoke(receipt) ?? receipt;
    }

    /// <inheritdoc/>
    public Task<IAsyncDisposable> PinAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add("pin");
        Pinned = true;
        return Task.FromResult<IAsyncDisposable>(new Pin(this));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> PendingAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add("pending");
        if (!Pinned) throw new InvalidOperationException("Pending требует удержанную fake session.");
        return Task.FromResult(Pending);
    }

    /// <inheritdoc/>
    public async Task MigrateAsync(CancellationToken ct)
    {
        Calls.Add("migrate");
        if (!Pinned) throw new InvalidOperationException("Migration требует удержанную fake session.");
        if (MigrationAction is not null) await MigrationAction(ct);
        ct.ThrowIfCancellationRequested();
        if (!KeepPending) Pending = [];
    }

    /// <inheritdoc/>
    private class Pin(FakeMaintenanceBoundary owner) : IAsyncDisposable
    {
        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            owner.Calls.Add("unpin");
            owner.Pinned = false;
            if (owner.FailCleanup) throw new MaintenanceException(MaintenanceError.CleanupUnconfirmed);
            return ValueTask.CompletedTask;
        }
    }
}

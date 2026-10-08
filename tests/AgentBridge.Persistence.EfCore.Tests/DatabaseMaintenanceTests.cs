using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.EfCore;
using EFCoreLibrary.Maintenance.Processes;
using EFCoreLibrary.Maintenance.Sqlite.Abstractions;
using EFCoreLibrary.Maintenance.Sqlite.Backup;
using EFCoreLibrary.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Явный API AgentBridge с настоящим библиотечным coordinator и fake provider/migration boundaries.</summary>
public class DatabaseMaintenanceTests
{
    /// <summary>Public AB registration проецирует actual process/native primary и secondary без готовой fake ошибки.</summary>
    [Theory][InlineData("process")][InlineData("native-unknown")][InlineData("native-code")]
    public async Task ActualBackupCleanupProjectsSafePrimary(string path)
    {
        ProcessBoundary process = new();
        BackupProcessRunner runner = new(process);
        BackupSession session = new(path == "native-unknown");
        FakeMaintenanceBoundary fake = new()
        {
            BackupAction = async budget =>
            {
                if (path == "process") await runner.RunAsync(new ProcessCommand(Path.GetFullPath("never-executed"), [], new Dictionary<string, string>()), budget, TimeSpan.FromMilliseconds(10));
                else await SqliteBackupStepper.CopyAsync(session, budget);
            }
        };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        try
        {
            MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken));
            Assert.Equal(path == "native-code" ? MaintenanceError.BackupNotConfirmed : MaintenanceError.CleanupUnconfirmed, error.Code);
            Assert.Equal(MaintenanceError.BackupFailed, error.PrimaryError);
            Assert.DoesNotContain("synthetic-secret", error.ToString());
            Assert.Null(error.InnerException);
            Assert.DoesNotContain("migrate", fake.Calls);
            Assert.Single(fake.Calls, call => call == "backup");
            Assert.Equal("unpin", fake.Calls.Last());
            if (path == "process") { Assert.Equal(1, process.Starts); Assert.Equal(1, runner.UnconfirmedCount); Assert.Equal(0, process.Disposals); }
            else { Assert.Equal(1, session.Steps); Assert.Equal(1, session.Finishes); }
            using IServiceScope next = root.CreateScope();
            if (path != "native-code")
            {
                string[] calls = fake.Calls.ToArray();
                Assert.Equal(MaintenanceError.GatePoisoned, (await Assert.ThrowsAsync<MaintenanceException>(() => Service(next).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken))).Code);
                Assert.Equal(calls, fake.Calls);
            }
            else await Service(next).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken);
        }
        finally
        {
            process.StopConfirmed = true;
            await runner.RetryCleanupAsync(Timeout);
        }
    }

    /// <inheritdoc cref="IBackupProcessFactory"/>
    private class ProcessBoundary : IBackupProcessFactory, IBackupProcessHandle
    {
        private Task? completion;
        public int Starts { get; private set; }
        public int Disposals { get; private set; }
        public bool StopConfirmed { get; set; }
        /// <inheritdoc/>
        public IBackupProcessHandle Start(ProcessCommand command) { Starts++; completion = Task.FromException(new IOException("synthetic-secret-pipe")); return this; }
        /// <inheritdoc/>
        public Task Completion => completion ?? throw new InvalidOperationException("Process не начат.");
        /// <inheritdoc/>
        public bool HasExited => StopConfirmed;
        /// <inheritdoc/>
        public ProcessResult Result => throw new InvalidOperationException("Неуспешный process не имеет результата.");
        /// <inheritdoc/>
        public void RequestStop() { }
        /// <inheritdoc/>
        public void Dispose() { Assert.True(StopConfirmed); Disposals++; }
    }

    /// <inheritdoc/>
    private class BackupSession(bool unknown) : ISqliteBackupSession
    {
        public int Steps { get; private set; }
        public int Finishes { get; private set; }
        /// <inheritdoc/>
        public int Step(int pages) { Steps++; return 10; }
        /// <inheritdoc/>
        public int Finish() { Finishes++; if (unknown) throw new IOException("synthetic-secret-finish"); return 10; }
    }

    /// <summary>Actual coordinator и EF lease сохраняют primary после ошибки CloseConnectionAsync.</summary>
    [Theory]
    [InlineData("typed", false)][InlineData("typed", true)]
    [InlineData("caller", false)][InlineData("caller", true)]
    [InlineData("deadline", false)][InlineData("deadline", true)]
    [InlineData("success", false)][InlineData("success", true)]
    public async Task ActualPinCleanupPreservesPrimary(string failure, bool cleanupFails)
    {
        using CancellationTokenSource caller = new();
        using FakePinnedConnection connection = new() { FailClose = cleanupFails };
        using DbContext context = new(new DbContextOptionsBuilder()
            .UseSqlite(connection).Options);
        EfMigrationOperations<AgentBridgeContextKey> migrations = new(
            new EfDbContextAdapter<DbContext, AgentBridgeContextKey>(context));
        FakeMaintenanceBoundary fake = new() { PinFactory = migrations.PinAsync, Pending = failure == "success" ? [] : ["pending"] };
        if (failure == "typed") fake.ChangeReceipt = receipt => receipt with { TargetIdentity = "wrong" };
        if (failure is "caller" or "deadline") fake.BackupAction = async budget =>
        {
            if (failure == "caller") caller.Cancel();
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, budget.Token);
        };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        Exception? error = await Record.ExceptionAsync(() => Service(scope).UpdateExistingAsync(
            failure == "deadline" ? TimeSpan.FromSeconds(1) : Timeout, caller.Token));
        MaintenanceError? primary = failure switch
        {
            "typed" => MaintenanceError.BackupNotConfirmed,
            "caller" => MaintenanceError.Cancelled,
            "deadline" => MaintenanceError.DeadlineExceeded,
            _ => null
        };
        if (cleanupFails)
        {
            MaintenanceException cleanup = Assert.IsType<MaintenanceException>(error);
            Assert.Equal(MaintenanceError.CleanupUnconfirmed, cleanup.Code);
            Assert.Equal(primary, cleanup.PrimaryError);
        }
        else if (failure == "caller") Assert.Equal(caller.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        else if (primary.HasValue) Assert.Equal(primary, Assert.IsType<MaintenanceException>(error).Code);
        else Assert.Null(error);
        Assert.Equal(1, connection.Closes);
        Assert.DoesNotContain("migrate", fake.Calls);
        Assert.DoesNotContain("synthetic-secret", error?.Message ?? "");
        if (cleanupFails)
        {
            Exception cause = Assert.IsAssignableFrom<Exception>(error!.InnerException);
            if (primary.HasValue) cause = Assert.IsType<AggregateException>(cause).InnerExceptions[1];
            Assert.Equal(MaintenanceError.CleanupUnconfirmed, Assert.IsType<MaintenanceException>(cause).Code);
            Assert.IsType<IOException>(cause.InnerException);
        }
        else if (failure == "deadline") Assert.IsAssignableFrom<OperationCanceledException>(error!.InnerException);
        else Assert.Null(error?.InnerException);
        connection.FailClose = false;
        fake.BackupAction = null;
        if (cleanupFails)
        {
            string[] calls = fake.Calls.ToArray();
            using IServiceScope next = root.CreateScope();
            Assert.Equal(MaintenanceError.GatePoisoned, (await Assert.ThrowsAsync<MaintenanceException>(() => Service(next).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken))).Code);
            Assert.Equal(calls, fake.Calls);
        }
        else await Service(scope).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken);
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Отказ pending после успешных CREATE/binding блокирует waiter и новый DI scope до provider I/O.</summary>
    [Theory]
    [InlineData("typed")]
    [InlineData("raw")]
    [InlineData("caller")]
    [InlineData("deadline")]
    public async Task InitializationDiscoveryFailureBlocksWaitingAndFreshScopes(string failure)
    {
        using CancellationTokenSource caller = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeMaintenanceBoundary fake = new() { Exists = false };
        bool failed = false;
        fake.PendingAction = async token =>
        {
            if (failed) return;
            failed = true;
            Assert.True(fake.Pinned);
            entered.TrySetResult();
            await release.Task;
            if (failure == "caller") { caller.Cancel(); token.ThrowIfCancellationRequested(); }
            if (failure == "deadline") await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);
            if (failure == "raw") throw new InvalidOperationException("synthetic-secret-driver");
            throw new MaintenanceException(MaintenanceError.ConnectionFailed);
        };
        MaintenanceTestLogger logger = new();
        using ServiceProvider root = Root(fake, logger);
        using IServiceScope firstScope = root.CreateScope();
        using IServiceScope waiterScope = root.CreateScope();
        Task<DatabaseMaintenanceResult> first = Service(firstScope).InitializeNewAsync(
            failure == "deadline" ? TimeSpan.FromSeconds(1) : Timeout, caller.Token);
        Task<DatabaseInspection>? waiter = null;
        try
        {
            await entered.Task.WaitAsync(Timeout, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(["inspect", "create", "inspect", "pin", "inspect", "pending"], fake.Calls);
            waiter = Service(waiterScope).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken);
            Assert.False(waiter.IsCompleted);
            release.TrySetResult();
            Exception? error = await Record.ExceptionAsync(() => first);
            MaintenanceError expected = failure == "caller" ? MaintenanceError.Cancelled :
                failure == "deadline" ? MaintenanceError.DeadlineExceeded : MaintenanceError.ConnectionFailed;
            if (failure == "caller") Assert.Equal(caller.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            else Assert.Equal(expected, Assert.IsType<MaintenanceException>(error).Code);
            Assert.DoesNotContain("synthetic-secret", error!.Message);
            if (failure == "raw") Assert.IsType<InvalidOperationException>(error.InnerException);
            MaintenanceException blocked = await Assert.ThrowsAsync<MaintenanceException>(() => waiter);
            Assert.Equal(MaintenanceError.GatePoisoned, blocked.Code);
            Assert.False(fake.Pinned);
            Assert.Equal(["inspect", "create", "inspect", "pin", "inspect", "pending", "unpin"], fake.Calls);
            string[] calls = fake.Calls.ToArray();
            using IServiceScope fresh = root.CreateScope();
            Assert.Equal(MaintenanceError.GatePoisoned,
                (await Assert.ThrowsAsync<MaintenanceException>(() => Service(fresh).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken))).Code);
            Assert.Equal(calls, fake.Calls);
            IReadOnlyDictionary<string, object?> diagnostic = logger.Events.First();
            Assert.Equal(MaintenanceStage.Discovery, diagnostic["Stage"]);
            Assert.Equal(expected, diagnostic["Code"]);
            Assert.All(logger.Exceptions, Assert.Null);
            Assert.DoesNotContain("synthetic-secret", string.Join(" ", logger.Events.SelectMany(item => item.Values)));
        }
        finally
        {
            release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (waiter is not null) await Record.ExceptionAsync(() => waiter);
        }
    }

    /// <summary>Успешные режимы обслуживания оставляют общий gate доступным новому scope.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SuccessfulChangeAllowsFreshScope(bool initialize, bool pending)
    {
        FakeMaintenanceBoundary fake = new() { Exists = !initialize, Pending = pending ? ["synthetic-migration"] : [] };
        using ServiceProvider root = Root(fake);
        using (IServiceScope first = root.CreateScope())
        {
            DatabaseMaintenanceResult result = initialize ? await Service(first).InitializeNewAsync(Timeout, ct: TestContext.Current.CancellationToken) : await Service(first).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken);
            Assert.Equal(initialize ? MaintenanceOutcome.Initialized : pending ? MaintenanceOutcome.Migrated : MaintenanceOutcome.Unchanged, result.Outcome);
        }
        using IServiceScope fresh = root.CreateScope();
        Assert.True((await Service(fresh).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken)).Exists);
        Assert.False(fake.Pinned);
    }

    /// <summary>Ранний отказ initialization до CREATE не запрещает последующее явное обслуживание.</summary>
    [Fact]
    public async Task PreCreateFailureAllowsFreshScope()
    {
        FakeMaintenanceBoundary fake = new() { Exists = false, InspectionAction = _ => throw new MaintenanceException(MaintenanceError.PermissionDenied) };
        using ServiceProvider root = Root(fake);
        using (IServiceScope first = root.CreateScope())
            Assert.Equal(MaintenanceError.PermissionDenied,
                (await Assert.ThrowsAsync<MaintenanceException>(() => Service(first).InitializeNewAsync(Timeout, ct: TestContext.Current.CancellationToken))).Code);
        Assert.Equal(["inspect"], fake.Calls);
        fake.InspectionAction = null;
        using IServiceScope fresh = root.CreateScope();
        Assert.Equal(MaintenanceOutcome.Initialized, (await Service(fresh).InitializeNewAsync(Timeout, ct: TestContext.Current.CancellationToken)).Outcome);
    }

    /// <summary>Inspection возвращает pending без backup, create или migrate.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InspectDoesNotChangeDatabase(bool exists)
    {
        FakeMaintenanceBoundary fake = new() { Exists = exists };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseInspection inspection = await Service(scope).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken);
        Assert.Equal(exists, inspection.Exists);
        Assert.Equal(exists ? new[] { "synthetic-migration" } : [], inspection.PendingMigrations);
        Assert.Equal(exists ? new[] { "inspect", "pin", "inspect", "pending", "unpin" } : ["inspect"], fake.Calls);
        Assert.False(fake.Pinned);
    }

    /// <summary>Перед pending migrations подтверждается backup и повторно проверяется target; session освобождается.</summary>
    [Fact]
    public async Task ExistingDatabaseBacksUpBeforeMigration()
    {
        FakeMaintenanceBoundary fake = new();
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseMaintenanceResult result = await Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken);
        Assert.Equal(MaintenanceOutcome.Migrated, result.Outcome);
        Assert.Equal(["synthetic-migration"], result.AppliedMigrations);
        Assert.NotNull(result.Backup);
        Assert.Equal(fake.Target, result.Backup.TargetIdentity);
        Assert.Equal(fake.Capabilities.Scope, result.Backup.Scope);
        Assert.Equal(["inspect", "pin", "inspect", "pending", "backup", "inspect", "migrate", "pending", "unpin"], fake.Calls);
        Assert.False(fake.Pinned);
    }

    /// <summary>Без изменений результат Unchanged не содержит фиктивный backup.</summary>
    [Fact]
    public async Task NoPendingMeansNoBackup()
    {
        FakeMaintenanceBoundary fake = new() { Pending = [] };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseMaintenanceResult result = await Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken);
        Assert.Equal(MaintenanceOutcome.Unchanged, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Empty(result.AppliedMigrations);
        Assert.Equal(["inspect", "pin", "inspect", "pending", "unpin"], fake.Calls);
    }

    /// <summary>Первая установка выбирается отдельно и не создаёт фиктивную копию отсутствующей БД.</summary>
    [Fact]
    public async Task NewDatabaseIsInitializedExplicitly()
    {
        FakeMaintenanceBoundary fake = new() { Exists = false };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseMaintenanceResult result = await Service(scope).InitializeNewAsync(Timeout, ct: TestContext.Current.CancellationToken);
        Assert.Equal(MaintenanceOutcome.Initialized, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Equal(["inspect", "create", "inspect", "pin", "inspect", "pending", "inspect", "migrate", "pending", "unpin"], fake.Calls);
    }

    /// <summary>Оба режима отклоняют неверное существование цели без переключения на другой режим.</summary>
    [Theory]
    [InlineData(false, MaintenanceError.DatabaseMissing)]
    [InlineData(true, MaintenanceError.DatabaseAlreadyExists)]
    public async Task ModesDoNotFallback(bool initialize, MaintenanceError expected)
    {
        FakeMaintenanceBoundary fake = new() { Exists = initialize };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => initialize
            ? Service(scope).InitializeNewAsync(Timeout, ct: TestContext.Current.CancellationToken) : Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(expected, error.Code);
        Assert.Equal(["inspect"], fake.Calls);
    }

    /// <summary>Ошибки доступа и подключения сохраняют safe code и не разрешают CREATE.</summary>
    [Theory]
    [InlineData(MaintenanceError.AuthenticationFailed)]
    [InlineData(MaintenanceError.PermissionDenied)]
    [InlineData(MaintenanceError.ConnectionFailed)]
    public async Task ConnectionFailureNeverMeansMissing(MaintenanceError code)
    {
        FakeMaintenanceBoundary fake = new() { InspectionAction = _ => throw new MaintenanceException(code) };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InitializeNewAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code);
        Assert.Equal(["inspect"], fake.Calls);
    }

    /// <summary>Неподтверждённый receipt любого происхождения запрещает migration.</summary>
    [Theory]
    [InlineData("operation")]
    [InlineData("target")]
    [InlineData("provider")]
    [InlineData("format")]
    [InlineData("scope")]
    [InlineData("artifact")]
    [InlineData("time")]
    public async Task InvalidReceiptStopsBeforeMigration(string field)
    {
        FakeMaintenanceBoundary fake = new()
        {
            ChangeReceipt = receipt => field switch
            {
                "operation" => receipt with { OperationId = Guid.NewGuid() },
                "target" => receipt with { TargetIdentity = "other-target" },
                "provider" => receipt with { Provider = "other-provider" },
                "format" => receipt with { Format = "other-format" },
                "scope" => receipt with { Scope = "other-scope" },
                "artifact" => receipt with { Artifact = new LocalBackupArtifact("relative", 0, "invalid") },
                "time" => receipt with { StartedAtUtc = DateTimeOffset.UnixEpoch },
                _ => throw new InvalidOperationException("Неизвестный test case.")
            }
        };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.BackupNotConfirmed, error.Code);
        Assert.DoesNotContain("migrate", fake.Calls);
        Assert.False(fake.Pinned);
    }

    /// <summary>Provider backup failure или collision не продолжают migration; исходная причина доступна в InnerException.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackupFailureOrNameCollisionStopsMigration(bool collision)
    {
        FakeMaintenanceBoundary fake = new()
        {
            BackupAction = _ => collision ? throw new IOException("synthetic-secret-collision")
                : throw new MaintenanceException(MaintenanceError.BackupFailed)
        };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.BackupFailed, error.Code);
        Assert.DoesNotContain("synthetic-secret", error.Message);
        if (collision) Assert.IsType<IOException>(error.InnerException);
        else Assert.Null(error.InnerException);
        Assert.DoesNotContain("migrate", fake.Calls);
        Assert.Equal("unpin", fake.Calls.Last());
    }

    /// <summary>Смена target после backup останавливает схему без попытки обновить новую цель.</summary>
    [Fact]
    public async Task ChangedTargetAfterBackupStopsMigration()
    {
        FakeMaintenanceBoundary fake = new();
        fake.ChangeReceipt = receipt => { fake.Target = "changed-target"; return receipt; };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.TargetMismatch, error.Code);
        Assert.DoesNotContain("migrate", fake.Calls);
    }

    /// <summary>Неизвестный исход migration, verification или cleanup блокирует другие scope того же root.</summary>
    [Theory]
    [InlineData("migration", MaintenanceError.MigrationFailed)]
    [InlineData("verification", MaintenanceError.MigrationFailed)]
    [InlineData("cleanup", MaintenanceError.CleanupUnconfirmed)]
    [InlineData("backup-cleanup", MaintenanceError.CleanupUnconfirmed)]
    public async Task PartialFailurePoisonsSharedGate(string failure, MaintenanceError expected)
    {
        FakeMaintenanceBoundary fake = new();
        if (failure == "migration") fake.MigrationAction = _ => throw new InvalidOperationException("synthetic-secret-driver");
        if (failure == "verification") fake.KeepPending = true;
        if (failure == "cleanup") fake.FailCleanup = true;
        if (failure == "backup-cleanup") fake.BackupAction = _ => throw new MaintenanceException(MaintenanceError.CleanupUnconfirmed, MaintenanceError.BackupFailed);
        using ServiceProvider root = Root(fake);
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(first).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(expected, error.Code);
        Assert.DoesNotContain("synthetic-secret", error.Message);
        if (failure == "migration") Assert.IsType<InvalidOperationException>(error.InnerException);
        if (failure == "backup-cleanup") Assert.Equal(MaintenanceError.BackupFailed, error.PrimaryError);
        int calls = fake.Calls.Count;
        MaintenanceException blocked = await Assert.ThrowsAsync<MaintenanceException>(() => Service(second).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.GatePoisoned, blocked.Code);
        Assert.Equal(calls, fake.Calls.Count);
    }

    /// <summary>CREATE collision явно завершается ошибкой без backup/migrate и без retry.</summary>
    [Fact]
    public async Task InitializationCollisionDoesNotContinue()
    {
        FakeMaintenanceBoundary fake = new() { Exists = false, CreationAction = _ => throw new MaintenanceException(MaintenanceError.DatabaseAlreadyExists) };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InitializeNewAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.DatabaseAlreadyExists, error.Code);
        Assert.Equal(["inspect", "create"], fake.Calls);
        MaintenanceException blocked = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.GatePoisoned, blocked.Code);
    }

    /// <summary>Отмена до gate или во время backup возвращает исходный caller token без migration.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationIsPreserved(bool duringBackup)
    {
        using CancellationTokenSource caller = new();
        FakeMaintenanceBoundary fake = new();
        if (duringBackup) fake.BackupAction = budget => { caller.Cancel(); budget.Check(); return Task.CompletedTask; };
        else caller.Cancel();
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(scope).UpdateExistingAsync(Timeout, caller.Token));
        Assert.Equal(caller.Token, error.CancellationToken);
        Assert.DoesNotContain("migrate", fake.Calls);
        Assert.False(fake.Pinned);
        if (!duringBackup) Assert.Empty(fake.Calls);
    }

    /// <summary>Typed backup failure не заменяется одновременно сработавшей отменой.</summary>
    [Fact]
    public async Task TypedFailureWinsOverConcurrentCancellation()
    {
        using CancellationTokenSource caller = new();
        FakeMaintenanceBoundary fake = new() { BackupAction = _ => { caller.Cancel(); throw new MaintenanceException(MaintenanceError.BackupFailed); } };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout, caller.Token));
        Assert.Equal(MaintenanceError.BackupFailed, error.Code);
    }

    /// <summary>Собственный deadline отличается от отмены вызывающего кода.</summary>
    [Fact]
    public async Task DeadlineIsReportedWithoutCallerCancellation()
    {
        FakeMaintenanceBoundary fake = new() { InspectionAction = budget => Task.Delay(System.Threading.Timeout.Infinite, budget.Token) };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InspectAsync(TimeSpan.FromMilliseconds(25), ct: TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.DeadlineExceeded, error.Code);
        Assert.Equal(["inspect"], fake.Calls);
    }

    /// <summary>Второй scope ждёт общий gate; отменённый waiter не начинает provider I/O.</summary>
    [Fact]
    public async Task GateSerializesScopesAndCancellationStopsWaiter()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeMaintenanceBoundary firstFake = new() { InspectionAction = async budget => { entered.TrySetResult(); await release.Task.WaitAsync(budget.Token); } };
        FakeMaintenanceBoundary secondFake = new();
        ServiceCollection services = DatabaseMaintenanceRegistrationTests.Services(DatabaseProvider.SQLite);
        services.AddScoped<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>(_ => firstFake);
        services.AddScoped<IRelationalMigrationOperations<AgentBridgeContextKey>>(_ => firstFake);
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();
        // Отдельная fake session для второго coordinator сохраняет общий root gate.
        DatabaseMaintenance<AgentBridgeContextKey> secondService = new(secondFake, secondFake,
            second.ServiceProvider.GetRequiredService<SingleInitializerGate>(), second.ServiceProvider.GetRequiredService<ILogger<DatabaseMaintenance<AgentBridgeContextKey>>>());
        Task<DatabaseInspection> active = Service(first).InspectAsync(Timeout, ct: TestContext.Current.CancellationToken);
        using CancellationTokenSource waiter = new();
        Task<DatabaseInspection>? waiting = null;
        try
        {
            await entered.Task.WaitAsync(Timeout, cancellationToken: TestContext.Current.CancellationToken);
            waiting = secondService.InspectAsync(Timeout, waiter.Token);
            Assert.False(waiting.IsCompleted);
            Assert.Empty(secondFake.Calls);
            waiter.Cancel();
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.Equal(waiter.Token, error.CancellationToken);
            Assert.Empty(secondFake.Calls);
        }
        finally
        {
            release.TrySetResult();
            waiter.Cancel();
            await Record.ExceptionAsync(() => active);
            if (waiting is not null) await Record.ExceptionAsync(() => waiting);
        }
        await active;
        await secondService.InspectAsync(Timeout, ct: TestContext.Current.CancellationToken);
        Assert.Contains("inspect", secondFake.Calls);
    }

    /// <summary>Стадии и safe code доступны через logger приложения без raw exception или секретов.</summary>
    [Fact]
    public async Task DiagnosticsPreserveSafeStagesAndErrors()
    {
        MaintenanceTestLogger logger = new();
        FakeMaintenanceBoundary fake = new() { BackupAction = _ => throw new InvalidOperationException("synthetic-secret-driver") };
        using ServiceProvider root = Root(fake, logger);
        using IServiceScope scope = root.CreateScope();
        await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken));
        IReadOnlyDictionary<string, object?> failure = Assert.Single(logger.Events);
        Assert.Equal(MaintenanceStage.Backup, failure["Stage"]);
        Assert.Equal(MaintenanceError.BackupFailed, failure["Code"]);
        Assert.IsType<Guid>(failure["OperationId"]);
        Assert.All(logger.Exceptions, Assert.Null);
        Assert.DoesNotContain("synthetic-secret", string.Join(" ", failure.Values));
        fake.BackupAction = null;
        await Service(scope).UpdateExistingAsync(Timeout, ct: TestContext.Current.CancellationToken);
        Assert.Equal(MaintenanceStage.Verification, logger.Events.Last()["Stage"]);
    }

    /// <summary>Создаёт production registrations и заменяет только технические I/O-границы.</summary>
    private static ServiceProvider Root(FakeMaintenanceBoundary fake, MaintenanceTestLogger? logger = null)
    {
        ServiceCollection services = DatabaseMaintenanceRegistrationTests.Services(DatabaseProvider.SQLite);
        services.RemoveAll<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>();
        services.RemoveAll<IRelationalMigrationOperations<AgentBridgeContextKey>>();
        services.AddScoped<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>(_ => fake);
        services.AddScoped<IRelationalMigrationOperations<AgentBridgeContextKey>>(_ => fake);
        if (logger is not null) services.AddLogging(builder => builder.AddProvider(logger));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Разрешает только явно вызываемый библиотечный API выбранного scope.</summary>
    private static IDatabaseMaintenance<AgentBridgeContextKey> Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
}

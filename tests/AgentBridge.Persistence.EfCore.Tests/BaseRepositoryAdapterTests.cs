using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверки делегирования актуальному base API и полных родительских predicates без EF provider.</summary>
public class BaseRepositoryAdapterTests
{
    /// <summary>Все типы строк делегируют одиночный и пакетный CRUD без изменения исходных данных заглушки.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryRecordTypeDelegatesStagingOnly(int recordType)
    {
        switch (recordType)
        {
            case 0: AssertStaging(new DialogRecord()); break;
            case 1: AssertStaging(new DialogTurnRecord()); break;
            case 2: AssertStaging(new CanonicalItemRecord()); break;
            case 3: AssertStaging(new ModelStepRecord()); break;
            case 4: AssertStaging(new DialogContextRecord()); break;
            default: throw new ArgumentOutOfRangeException(nameof(recordType));
        }
    }

    /// <summary>Локальный turn ID не выбирает одноимённое обращение чужого диалога; tracking передаётся явно.</summary>
    [Fact]
    public async Task TurnLookupIncludesParentAndPropagatesTrackingAndCancellation()
    {
        Guid dialogId = Guid.NewGuid();
        Guid turnId = Guid.NewGuid();
        FakeBaseRepository<DialogTurnRecord> repository = new();
        DialogTurnRecord selected = new() { DialogId = dialogId, Id = turnId };
        repository.Records.AddRange([new() { DialogId = Guid.NewGuid(), Id = turnId }, selected]);
        TurnRecordQueries adapter = new(repository);
        using CancellationTokenSource source = new();
        Assert.Same(selected, await adapter.FindAsync(dialogId, turnId, source.Token, trackChanges: true));
        Assert.False(repository.LastAsNoTracking);
        Assert.Equal(source.Token, repository.LastCancellationToken);
        Assert.Null(await adapter.FindAsync(dialogId, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(repository.LastAsNoTracking);
    }

    /// <summary>Шаг требует обоих родителей: одинаковые ID шагов/обращений в других ветках не совпадают.</summary>
    [Fact]
    public async Task StepLookupIncludesDialogTurnAndStep()
    {
        Guid dialogId = Guid.NewGuid();
        Guid turnId = Guid.NewGuid();
        Guid stepId = Guid.NewGuid();
        FakeBaseRepository<ModelStepRecord> repository = new();
        ModelStepRecord selected = new() { DialogId = dialogId, TurnId = turnId, Id = stepId };
        repository.Records.AddRange([
            new() { DialogId = Guid.NewGuid(), TurnId = turnId, Id = stepId },
            new() { DialogId = dialogId, TurnId = Guid.NewGuid(), Id = stepId },
            new() { DialogId = dialogId, TurnId = turnId, Id = Guid.NewGuid() }, selected]);
        ModelStepRecordQueries adapter = new(repository);
        Assert.Same(selected, await adapter.FindAsync(dialogId, turnId, stepId, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(repository.LastAsNoTracking);
        Assert.Null(await adapter.FindAsync(Guid.NewGuid(), turnId, stepId, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Items фильтруются обоими родителями и сортируются по Sequence вместо порядка входных строк.</summary>
    [Fact]
    public async Task ItemTurnReadFiltersBothParentsBeforeStableOrdering()
    {
        Guid dialogId = Guid.NewGuid();
        Guid turnId = Guid.NewGuid();
        FakeBaseRepository<CanonicalItemRecord> repository = new();
        CanonicalItemRecord first = new() { DialogId = dialogId, TurnId = turnId, Sequence = 1 };
        CanonicalItemRecord second = new() { DialogId = dialogId, TurnId = turnId, Sequence = 2 };
        repository.Records.AddRange([second,
            new() { DialogId = Guid.NewGuid(), TurnId = turnId, Sequence = 1 },
            new() { DialogId = dialogId, TurnId = Guid.NewGuid(), Sequence = 1 }, first]);
        Assert.Equal([first, second], await new ItemRecordQueries(repository).ReadTurnAsync(dialogId, turnId, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(repository.LastAsNoTracking);
    }

    /// <summary>Активна максимальная Version только заданного диалога; все предыдущие версии остаются доступны.</summary>
    [Fact]
    public async Task ContextSelectionUsesVersionInsteadOfDateAndRetainsPriorVersions()
    {
        Guid dialogId = Guid.NewGuid();
        FakeBaseRepository<DialogContextRecord> repository = new();
        DateTimeOffset now = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        DialogContextRecord old = new() { DialogId = dialogId, Version = 1, CreatedAtUtc = now.AddHours(1) };
        DialogContextRecord active = new() { DialogId = dialogId, Version = 2, CreatedAtUtc = now };
        repository.Records.AddRange([old, new() { DialogId = Guid.NewGuid(), Version = 99 }, active]);
        ContextRecordQueries adapter = new(repository);
        Assert.Same(active, await adapter.ReadActiveAsync(dialogId, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal([old, active], await adapter.ReadAsync(dialogId, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, repository.Records.Count);
        Assert.Null(await adapter.ReadActiveAsync(Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(repository.LastAsNoTracking);
    }

    /// <summary>Глобальный ID диалога читается через baseById, с управляемым tracking.</summary>
    [Fact]
    public async Task DialogLookupDelegatesIdAndTracking()
    {
        FakeBaseRepository<DialogRecord> repository = new();
        DialogRecord selected = new() { Id = Guid.NewGuid() };
        repository.Records.AddRange([new() { Id = Guid.NewGuid() }, selected]);
        DialogRecordQueries adapter = new(new FakeDialogByIdRepository(repository), repository);
        Assert.Same(selected, await adapter.FindAsync(selected.Id, trackChanges: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(repository.LastAsNoTracking);
        Assert.Null(await adapter.FindAsync(Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(repository.LastAsNoTracking);
    }

    /// <summary>Проверяет точное делегирование без собственной CRUD-реализации или сохранения.</summary>
    private static void AssertStaging<TEntity>(TEntity record) where TEntity : class
    {
        FakeBaseRepository<TEntity> repository = new();
        repository.Records.Add(record);
        TEntity[] range = [record];
        RecordStaging<TEntity> adapter = new(repository, repository, repository);
        adapter.StageCreate(record);
        adapter.StageCreateRange(range);
        adapter.StageUpdate(record);
        adapter.StageUpdateRange(range);
        adapter.StageDelete(record);
        adapter.StageDeleteRange(range);
        Assert.Same(record, Assert.Single(repository.Created));
        Assert.Same(record, Assert.Single(repository.Updated));
        Assert.Same(record, Assert.Single(repository.Deleted));
        Assert.Same(range, repository.CreatedRange);
        Assert.Same(range, repository.UpdatedRange);
        Assert.Same(range, repository.DeletedRange);
        Assert.Same(record, Assert.Single(repository.Records));
        Assert.Equal(0, repository.ReadCalls);
    }
}

using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Изолированное хранилище committed/staged копий для проверки production write ports, без имитации provider SQL.</summary>
internal class FakeWriteFixture
{
    public static readonly DateTimeOffset NOW = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
    public Store<DialogRecord> Roots { get; } = new(row => row.Id.ToString());
    public Store<DialogTurnRecord> Turns { get; } = new(row => $"{row.DialogId}/{row.Id}");
    public Store<CanonicalItemRecord> Items { get; } = new(row => $"{row.DialogId}/{row.TurnId}/{row.Sequence}");
    public Store<ModelStepRecord> Steps { get; } = new(row => $"{row.DialogId}/{row.TurnId}/{row.Id}");
    public Store<DialogContextRecord> Contexts { get; } = new(row => $"{row.DialogId}/{row.Version}");
    public FakeUnitOfWorkSession Session { get; } = new();
    public PersistenceOperationGate Gate { get; } = new();
    public DialogCreationUnitOfWork Creator { get; }
    public DialogTurnUnitOfWork Writer { get; }
    public DialogContextUnitOfWork ContextWriter { get; }
    public DialogDeletionUnitOfWork Deletion { get; }
    public Action? BeforeSave { get; set; }
    private Dictionary<Guid, DialogRecord> _initial = [];

    /// <summary>Подключает настоящие сценарии к exact base API fakes и отделённой committed памяти.</summary>
    public FakeWriteFixture()
    {
        UnitOfWorkScope scope = new(Session, Gate);
        DialogRecordQueries roots = new(new FakeDialogByIdRepository(Roots.Repository), Roots.Repository);
        DialogWriteGuard guard = new(roots);
        DialogStateLoader state = new(new(Turns.Repository), new(Contexts.Repository));
        TurnContentStaging content = new(new(Items.Repository), new(Steps.Repository), Items.Staging(), Steps.Staging());
        Creator = new(scope, roots, Roots.Staging());
        Writer = new(scope, guard, state, content, Roots.Staging(), Turns.Staging(), new(Turns.Repository));
        ContextWriter = new(scope, guard, state, Roots.Staging(), Contexts.Staging());
        Deletion = new(scope, guard, roots, Roots.Staging());
        Session.OnBegin = () =>
        {
            Roots.Begin(); Turns.Begin(); Items.Begin(); Steps.Begin(); Contexts.Begin();
            _initial = Roots.Persisted.ToDictionary(row => row.Id, row => Clone(row));
        };
        Session.OnSave = () =>
        {
            BeforeSave?.Invoke();
            foreach (DialogRecord changed in Roots.Repository.Updated.Concat(Roots.Repository.Deleted))
            {
                DialogRecord? current = Roots.Persisted.SingleOrDefault(row => row.Id == changed.Id);
                DialogRecord expected = _initial[changed.Id];
                if (current is null || current.Revision != expected.Revision || current.IncarnationId != expected.IncarnationId ||
                    current.OwnerId != expected.OwnerId || current.ExpiresAtUtc != expected.ExpiresAtUtc)
                {
                    using AgentBridgeDbContext metadata = new(new DbContextOptionsBuilder<AgentBridgeDbContext>()
                        .UseSqlite("Data Source=never-open.db").Options);
                    throw new FakeRootConcurrencyException(metadata.Entry(changed));
                }
            }
            return Task.CompletedTask;
        };
        Session.Transaction.OnCommit = () =>
        {
            Guid[] deleted = Roots.Repository.Deleted.Select(row => row.Id).ToArray();
            Roots.Commit(); Turns.Commit(); Items.Commit(); Steps.Commit(); Contexts.Commit();
            Turns.Persisted.RemoveAll(row => deleted.Contains(row.DialogId));
            Items.Persisted.RemoveAll(row => deleted.Contains(row.DialogId));
            Steps.Persisted.RemoveAll(row => deleted.Contains(row.DialogId));
            Contexts.Persisted.RemoveAll(row => deleted.Contains(row.DialogId));
        };
        Session.OnClear = () => { Roots.Clear(); Turns.Clear(); Items.Clear(); Steps.Clear(); Contexts.Clear(); };
    }

    /// <summary>Создаёт диалог production port, а не обходными DTO setters.</summary>
    public async Task<DialogWriteToken> CreateAsync()
    {
        return (await Creator.CreateAsync(DialogId.From(Guid.NewGuid()), DialogOwnerId.From(" User:Б "), NOW, NOW.AddDays(1))).Data!;
    }

    /// <summary>Фиксирует явные идентичности и UTC без часов.</summary>
    public static DialogAccess Access(DialogWriteToken token, DateTimeOffset? now = null, string owner = " User:Б ") =>
        new(token.DialogId, DialogOwnerId.From(owner), now ?? NOW);

    /// <summary>Копирует только тестовые persistence DTO, независимо от исходной mutable строки.</summary>
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    /// <summary>Отделяет committed rows от fake tracker; commit вызывается только после успешного fake save.</summary>
    internal class Store<T>(Func<T, string> key) where T : class
    {
        public List<T> Persisted { get; } = [];
        public FakeBaseRepository<T> Repository { get; } = new();
        /// <summary>Создаёт base staging adapter.</summary>
        public RecordStaging<T> Staging() => new(Repository, Repository, Repository);
        /// <summary>Создаёт рабочие копии.</summary>
        public void Begin()
        {
            Repository.Records.Clear();
            Repository.Records.AddRange(Persisted.Select(Clone));
        }
        /// <summary>Применяет подготовленные fake операции к committed памяти.</summary>
        public void Commit()
        {
            foreach (T deleted in Repository.Deleted.Concat(Repository.DeletedRange ?? []))
            {
                Persisted.RemoveAll(row => key(row) == key(deleted));
            }
            foreach (T updated in Repository.Updated.Concat(Repository.UpdatedRange ?? []))
            {
                int index = Persisted.FindIndex(row => key(row) == key(updated));
                if (index < 0) { throw new InvalidOperationException("Missing fake row."); }
                Persisted[index] = Clone(updated);
            }
            Persisted.AddRange(Repository.Created.Concat(Repository.CreatedRange ?? []).Select(Clone));
        }
        /// <summary>Очищает fake tracker после любого исхода.</summary>
        public void Clear()
        {
            Repository.Records.Clear();
            Repository.ClearStaging();
        }
    }
}

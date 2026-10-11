using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual public catalogue/lifecycle/recovery/write ports поверх isolated base repositories; без provider execution.</summary>
internal class CatalogWriteFixture
{
    internal static readonly DateTimeOffset NOW = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    internal static readonly DialogCatalogScope SCOPE = new(DialogOwnerId.From("owner"), "site", "agent");
    internal static readonly CatalogAccessProfile PROFILE = new("profile", 1, JsonSerializer.SerializeToElement(new { permission = "payroll", consent = true }));
    internal static readonly DialogProjectionPolicy POLICY = new("test-question", 1, 40, 30, 16384);
    public Database Committed { get; }
    public Clock Time { get; } = new();
    public FakeUnitOfWorkSession Session { get; } = new();
    public PersistenceOperationGate Gate { get; } = new();
    public IDialogCatalogCreator Creator { get; }
    public IDialogCatalogReader CatalogReader { get; }
    public IDialogCatalogChangeReader Feed { get; }
    public IDialogContinuationReader Continuation { get; }
    public IDialogRunLifecycle Lifecycle { get; }
    public IDialogRecovery Recovery { get; }
    public IDialogTurnWriter Writer { get; }
    public IDialogToolAttemptWriter Attempts { get; }
    public IDialogContextWriter ContextWriter { get; }
    public IDialogDeletion Deletion { get; }
    public IExpiredDialogDeletion ExpiryDeletion { get; }
    public IDialogReader Reader { get; }
    public FakeBaseRepository<DialogCatalogRecord> CatalogQueries => _catalog.Repository;
    public FakeBaseRepository<DialogCatalogChangeRecord> FeedQueries => _changes.Repository;
    public int CanonicalReads => _items.Repository.ReadCalls;
    public Action? BeforeSave { get; set; }
    public Action? BeforeCommit { get; set; }
    public Action? AfterCommit { get; set; }
    public Projector Projection { get; } = new();
    private readonly Store<DialogRecord> _roots;
    private readonly Store<DialogTurnRecord> _turns;
    private readonly Store<CanonicalItemRecord> _items;
    private readonly Store<ModelStepRecord> _steps;
    private readonly Store<DialogContextRecord> _contexts;
    private readonly Store<DialogSettingsRecord> _settings;
    private readonly Store<DialogCatalogRecord> _catalog;
    private readonly Store<DialogCatalogClockRecord> _clock;
    private readonly Store<DialogCatalogChangeRecord> _changes;
    private readonly Store<DialogRecoveryOperationRecord> _operations;

    public CatalogWriteFixture(Database? committed = null, IDialogRecoveryEvidenceResolver? evidenceResolver = null,
        IDialogRunQuiescenceVerifier? quiescenceVerifier = null)
    {
        Committed = committed ?? new();
        _roots = new(Committed.Roots, row => row.Id.ToString());
        _turns = new(Committed.Turns, row => $"{row.DialogId}/{row.Id}");
        _items = new(Committed.Items, row => $"{row.DialogId}/{row.TurnId}/{row.Sequence}");
        _steps = new(Committed.Steps, row => $"{row.DialogId}/{row.TurnId}/{row.Id}");
        _contexts = new(Committed.Contexts, row => $"{row.DialogId}/{row.Version}");
        _settings = new(Committed.Settings, row => row.Id.ToString());
        _catalog = new(Committed.Catalog, row => row.Id.ToString());
        _clock = new(Committed.Clocks, row => row.Id);
        _changes = new(Committed.Changes, row => $"{row.ScopeKey}/{row.Sequence}");
        _operations = new(Committed.Operations, row => $"{row.DialogId}/{row.Id}");
        DialogRetentionPolicy retention = new(new() { RetentionPeriod = TimeSpan.FromDays(1) });
        UnitOfWorkScope scope = new(Session, Gate);
        DialogRecordQueries roots = new(new FakeDialogByIdRepository(_roots.Repository), _roots.Repository, retention);
        TurnRecordQueries turnQueries = new(_turns.Repository);
        DialogWriteGuard guard = new(roots, retention);
        DialogStateLoader loader = new(turnQueries, new(_contexts.Repository), retention);
        TurnContentStaging content = new(new(_items.Repository), new(_steps.Repository), _items.Staging(), _steps.Staging());
        DialogCatalogQueries queries = new(_catalog.Repository, _clock.Repository, _changes.Repository, _operations.Repository);
        DialogCatalogStaging staging = new(queries, _catalog.Staging(), _clock.Staging(), _changes.Staging(), [Projection]);
        Creator = new DialogCatalogCreationUnitOfWork(scope, roots, queries, _roots.Staging(), staging);
        DialogCatalogReader reader = new(queries, roots, Gate);
        CatalogReader = reader;
        Feed = reader;
        Continuation = reader;
        DialogLifecycleUnitOfWork lifecycle = new(scope, guard, roots, loader, content, _roots.Staging(),
            _turns.Staging(), queries, staging, _operations.Staging(), Time, turnQueries, evidenceResolver, quiescenceVerifier);
        Lifecycle = lifecycle;
        Recovery = lifecycle;
        Writer = new DialogTurnUnitOfWork(scope, guard, loader, content, _roots.Staging(), _turns.Staging(), turnQueries, staging);
        Attempts = new DialogToolAttemptUnitOfWork(scope, guard, loader, new(_steps.Repository), content,
            _steps.Staging(), _roots.Staging(), staging);
        ContextWriter = new DialogContextUnitOfWork(scope, guard, loader, _roots.Staging(), _contexts.Staging(), staging);
        DialogDeletionUnitOfWork deletion = new(scope, guard, roots, _roots.Staging(), retention, staging);
        Deletion = deletion;
        ExpiryDeletion = deletion;
        Reader = new DialogReader(roots, turnQueries, new(_items.Repository), new(_steps.Repository), new(_contexts.Repository),
            Gate, new(new FakeSettingsByIdRepository(_settings.Repository)), retention, queries);
        Dictionary<Guid, DialogRecord> initial = [];
        Dictionary<string, long> clocks = [];
        Session.OnBegin = () =>
        {
            Reload();
            initial = Committed.Roots.ToDictionary(row => row.Id, Clone);
            clocks = Committed.Clocks.ToDictionary(row => row.Id, row => row.Sequence);
        };
        Session.OnSave = () =>
        {
            BeforeSave?.Invoke();
            foreach (DialogRecord changed in _roots.Repository.Updated.Concat(_roots.Repository.Deleted))
            {
                DialogRecord expected = initial[changed.Id];
                DialogRecord? actual = Committed.Roots.SingleOrDefault(row => row.Id == changed.Id);
                if (actual is null || actual.Revision != expected.Revision || actual.RuntimeJson != expected.RuntimeJson ||
                    actual.IncarnationId != expected.IncarnationId)
                {
                    using AgentBridgeDbContext model = Metadata();
                    throw new FakeRootConcurrencyException(model.Entry(changed));
                }
            }

            foreach (DialogCatalogClockRecord changed in _clock.Repository.Updated)
            {
                if (Committed.Clocks.Single(row => row.Id == changed.Id).Sequence != clocks[changed.Id])
                {
                    using AgentBridgeDbContext model = Metadata();
                    throw new FakeRootConcurrencyException(model.Entry(changed));
                }
            }

            return Task.CompletedTask;
        };
        Session.Transaction.OnCommit = () =>
        {
            BeforeCommit?.Invoke();
            Guid[] deleted = _roots.Repository.Deleted.Select(row => row.Id).ToArray();
            _roots.Commit(); _turns.Commit(); _items.Commit(); _steps.Commit(); _contexts.Commit();
            _settings.Commit(); _catalog.Commit(); _clock.Commit(); _changes.Commit(); _operations.Commit();
            Committed.Turns.RemoveAll(row => deleted.Contains(row.DialogId));
            Committed.Items.RemoveAll(row => deleted.Contains(row.DialogId));
            Committed.Steps.RemoveAll(row => deleted.Contains(row.DialogId));
            Committed.Contexts.RemoveAll(row => deleted.Contains(row.DialogId));
            Committed.Settings.RemoveAll(row => deleted.Contains(row.Id));
            Committed.Operations.RemoveAll(row => deleted.Contains(row.DialogId));
            AfterCommit?.Invoke();
        };
        Session.OnClear = () =>
        {
            _roots.Clear(); _turns.Clear(); _items.Clear(); _steps.Clear(); _contexts.Clear();
            _settings.Clear(); _catalog.Clear(); _clock.Clear(); _changes.Clear(); _operations.Clear();
        };
    }

    public void Reload()
    {
        _roots.Reload(); _turns.Reload(); _items.Reload(); _steps.Reload(); _contexts.Reload();
        _settings.Reload(); _catalog.Reload(); _clock.Reload(); _changes.Reload(); _operations.Reload();
    }

    public async Task<DialogCatalogState> CreateAsync(Guid? id = null, DateTimeOffset? expires = null) =>
        (await Creator.CreateAsync(new(DialogId.From(id ?? Guid.NewGuid()), SCOPE, NOW, expires, PROFILE, POLICY),
            TestContext.Current.CancellationToken)).Data!;

    public DialogAccess Access(DialogWriteToken token) => new(token.DialogId, SCOPE.OwnerId, Time.Now);
    public DialogCatalogAccess FullAccess(DialogWriteToken token) => new(Access(token), SCOPE, PROFILE);
    public async Task<DialogSnapshot> SnapshotAsync(DialogWriteToken token)
    {
        Reload();
        ServiceResult<DialogSnapshot> result = await Reader.ReadCatalogAsync(FullAccess(token), TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Error?.Message);
        return result.Data!;
    }

    public async Task<DialogRunState> BeginAsync(DialogWriteToken token, Guid? turn = null, string question = "Первый вопрос") =>
        (await Lifecycle.BeginAsync(new(Access(token), token, turn ?? Guid.NewGuid(), [Question(question)],
            new TurnModelSettings("gpt-5", "high", 900, 10, 1000), TimeSpan.FromMinutes(1), SCOPE, PROFILE),
            TestContext.Current.CancellationToken)).Data!;

    public static CanonicalModelItem Question(string text, int version = 1) => new(JsonSerializer.SerializeToElement(new
    {
        type = "message", role = "user", content = JsonSerializer.Serialize(new { version, question = text, selection = "PRIVATE", pageContext = "PRIVATE" })
    }));
    public static CanonicalModelItem Assistant(string text) => new(JsonSerializer.SerializeToElement(new
    {
        type = "message", role = "assistant", content = new[] { new { type = "output_text", text } }
    }));
    public static CanonicalModelItem Call(string id, string name = "action") => new(JsonSerializer.SerializeToElement(new
    {
        type = "function_call", call_id = id, name, arguments = "{}"
    }));
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static AgentBridgeDbContext Metadata() => new(new DbContextOptionsBuilder<AgentBridgeDbContext>().UseSqlite("Data Source=never-open.db").Options);

    internal class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = NOW;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal class Database
    {
        public List<DialogRecord> Roots { get; } = [];
        public List<DialogTurnRecord> Turns { get; } = [];
        public List<CanonicalItemRecord> Items { get; } = [];
        public List<ModelStepRecord> Steps { get; } = [];
        public List<DialogContextRecord> Contexts { get; } = [];
        public List<DialogSettingsRecord> Settings { get; } = [];
        public List<DialogCatalogRecord> Catalog { get; } = [];
        public List<DialogCatalogClockRecord> Clocks { get; } = [];
        public List<DialogCatalogChangeRecord> Changes { get; } = [];
        public List<DialogRecoveryOperationRecord> Operations { get; } = [];
    }

    private class Store<T>(List<T> committed, Func<T, string> key) where T : class
    {
        public FakeBaseRepository<T> Repository { get; } = new();
        public RecordStaging<T> Staging() => new(Repository, Repository, Repository);
        public void Reload()
        {
            Repository.Records.Clear();
            Repository.Records.AddRange(committed.Select(Clone));
        }

        public void Commit()
        {
            foreach (T row in Repository.Deleted.Concat(Repository.DeletedRange ?? []))
                committed.RemoveAll(item => key(item) == key(row));
            foreach (T row in Repository.Updated.Concat(Repository.UpdatedRange ?? []))
                committed[committed.FindIndex(item => key(item) == key(row))] = Clone(row);
            foreach (T row in Repository.Created.Concat(Repository.CreatedRange ?? []))
            {
                if (committed.Any(item => key(item) == key(row))) throw new InvalidOperationException("Duplicate fake PK.");
                committed.Add(Clone(row));
            }
        }

        public void Clear()
        {
            Repository.Records.Clear();
            Repository.ClearStaging();
        }
    }

    /// <inheritdoc/>
    internal class Projector : IDialogCatalogProjector
    {
        public string Key => "test-question";
        public int Version => 1;
        public bool Refuse { get; set; }
        public ServiceResult<DialogCatalogTextUpdate> Project(SavedDialogMessage message, DialogCatalogTextState current, DialogProjectionPolicy policy)
        {
            if (Refuse) return ServiceResult<DialogCatalogTextUpdate>.Fail(new(ServiceErrorType.Unsupported, "test_refusal"));
            string text;
            if (message.Role == "user")
            {
                using JsonDocument envelope = JsonDocument.Parse(message.Message.Content.GetProperty("content").GetString()!);
                if (envelope.RootElement.GetProperty("version").GetInt32() != 1)
                    return ServiceResult<DialogCatalogTextUpdate>.Fail(new(ServiceErrorType.Unsupported, "test_envelope_unknown"));
                text = envelope.RootElement.GetProperty("question").GetString()!;
                if (string.IsNullOrWhiteSpace(text))
                    return ServiceResult<DialogCatalogTextUpdate>.Fail(new(ServiceErrorType.Validation, "test_question_empty"));
            }
            else
            {
                text = string.Join(" ", message.Message.Content.GetProperty("content").EnumerateArray()
                    .Where(item => item.GetProperty("type").GetString() == "output_text")
                    .Select(item => item.GetProperty("text").GetString()));
            }

            string title = current.HasSavedQuestion || message.Role != "user" ? current.Title :
                DialogCatalogText.Limit(text, policy.MaxTitleScalars);
            return ServiceResult<DialogCatalogTextUpdate>.Ok(new(new(title, DialogCatalogText.SearchKey(title),
                DialogCatalogText.Limit(text, policy.MaxSnippetScalars), current.HasSavedQuestion || message.Role == "user")));
        }
    }
}

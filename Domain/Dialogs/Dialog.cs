using System.Collections.ObjectModel;

namespace AgentBridge.Domain.Dialogs;

/// <summary>Корень диалога, защищающий владение, срок доступности, порядок обращений и актуальность контекста.</summary>
/// <remarks>Время передаётся явно. Тип не читает часы, настройки, HTTP или БД и не обеспечивает потокобезопасность.</remarks>
public class Dialog
{
    private readonly Guid _lifetimeId = Guid.NewGuid();
    private readonly List<DialogTurn> _turns = [];
    private readonly List<DialogContextState> _contextStates = [];
    private readonly ReadOnlyCollection<DialogTurn> _turnsView;
    private readonly ReadOnlyCollection<DialogContextState> _contextStatesView;
    private DateTimeOffset _lastChangedAtUtc;

    /// <summary>Создаёт начальное состояние после проверки фабрикой.</summary>
    private Dialog(DialogId id, DialogOwnerId ownerId, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        Id = id;
        OwnerId = ownerId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        _lastChangedAtUtc = createdAtUtc;
        _turnsView = _turns.AsReadOnly();
        _contextStatesView = _contextStates.AsReadOnly();
    }

    /// <summary>Неизменяемая идентичность диалога.</summary>
    public DialogId Id { get; }
    /// <summary>Неизменяемый владелец; проверку прав текущего пользователя выполняет приложение.</summary>
    public DialogOwnerId OwnerId { get; }
    /// <summary>Время создания в UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; }
    /// <summary>Фиксированное время истечения, рассчитанное из настроек при создании.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }
    /// <summary>Версия агрегата; каждое успешное изменение делает предыдущие снимки устаревшими.</summary>
    public long Revision { get; private set; }
    /// <summary>Время последнего принятого изменения; сохраняется вместе с revision.</summary>
    public DateTimeOffset LastChangedAtUtc => _lastChangedAtUtc;
    /// <summary>Признак явного доменного удаления; физическое удаление выполняется хранилищем.</summary>
    public bool IsDeleted { get; private set; }
    /// <summary>Обращения в порядке начала, доступные только для чтения.</summary>
    public IReadOnlyList<DialogTurn> Turns => _turnsView;
    /// <summary>Принятые версии контекста; compact не удаляет обращения.</summary>
    public IReadOnlyList<DialogContextState> ContextStates => _contextStatesView;
    /// <summary>Последний принятый контекст либо отсутствие контекста.</summary>
    public DialogContextState? ActiveContext => _contextStates.Count == 0 ? null : _contextStates[^1];

    /// <summary>Создаёт диалог с уже вычисленным из конфигурации сроком; не содержит пробного периода.</summary>
    public static Dialog Create(DialogId id, DialogOwnerId ownerId, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(ownerId);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Срок истечения должен быть позже создания.");
        }

        return new Dialog(id, ownerId, createdAtUtc, expiresAtUtc);
    }

    /// <summary>Восстанавливает живой сохранённый агрегат, проверяя всю хронологию и историю без проигрывания mutations.</summary>
    /// <remarks>Удалённые агрегаты не восстанавливаются. Локальный lifetime остаётся новым для этого экземпляра;
    /// сохраняемый incarnation проверяется внешней атомарной границей до вызова фабрики.</remarks>
    public static Dialog Restore(DialogId id, DialogOwnerId ownerId, DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc, long revision, DateTimeOffset lastChangedAtUtc,
        IEnumerable<DialogTurnSnapshot> turns, IEnumerable<DialogContextSnapshot> contexts)
    {
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(contexts);
        Dialog dialog = Create(id, ownerId, createdAtUtc, expiresAtUtc);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        EnsureUtc(lastChangedAtUtc, nameof(lastChangedAtUtc));
        if (lastChangedAtUtc < createdAtUtc || lastChangedAtUtc >= expiresAtUtc)
        {
            throw new ArgumentException("Время сохранённого изменения вне жизни диалога.");
        }

        HashSet<Guid> ids = [];
        DateTimeOffset previousStart = createdAtUtc;
        long minimumRevision = 0;
        foreach (DialogTurnSnapshot turn in turns)
        {
            ArgumentNullException.ThrowIfNull(turn);
            EnsureUtc(turn.StartedAtUtc, nameof(turns));
            if (turn.FinishedAtUtc is DateTimeOffset finish)
            {
                EnsureUtc(finish, nameof(turns));
            }
            if (turn.Id == Guid.Empty || !ids.Add(turn.Id) || turn.Sequence != dialog._turns.Count + 1L ||
                !Enum.IsDefined(turn.Status) || turn.StartedAtUtc < previousStart || turn.StartedAtUtc > lastChangedAtUtc ||
                (turn.Status == DialogTurnStatus.InProgress) != (turn.FinishedAtUtc is null) ||
                turn.FinishedAtUtc < turn.StartedAtUtc || turn.FinishedAtUtc > lastChangedAtUtc)
            {
                throw new ArgumentException("Некорректное сохранённое обращение.");
            }
            dialog._turns.Add(new DialogTurn(turn.Id, turn.Sequence, turn.StartedAtUtc, turn.Status, turn.FinishedAtUtc));
            previousStart = turn.StartedAtUtc;
            minimumRevision = checked(minimumRevision + (turn.Status == DialogTurnStatus.InProgress ? 1 : 2));
        }
        DateTimeOffset previousContext = createdAtUtc;
        foreach (DialogContextSnapshot context in contexts)
        {
            ArgumentNullException.ThrowIfNull(context);
            EnsureUtc(context.CreatedAtUtc, nameof(contexts));
            if (context.Version != dialog._contextStates.Count + 1L ||
                context.ThroughTurnSequence < (dialog.ActiveContext?.ThroughTurnSequence ?? 0) ||
                context.ThroughTurnSequence > dialog._turns.Count || context.CreatedAtUtc < previousContext ||
                context.CreatedAtUtc > lastChangedAtUtc ||
                dialog._turns.Take((int)context.ThroughTurnSequence).Any(turn =>
                    turn.FinishedAtUtc is null || turn.FinishedAtUtc > context.CreatedAtUtc))
            {
                throw new ArgumentException("Некорректная сохранённая версия контекста.");
            }
            dialog._contextStates.Add(new DialogContextState(context.Version, context.ThroughTurnSequence, context.CreatedAtUtc));
            previousContext = context.CreatedAtUtc;
            minimumRevision = checked(minimumRevision + 1);
        }
        if (revision < minimumRevision || (revision == 0 && lastChangedAtUtc != createdAtUtc) ||
            (revision > minimumRevision && dialog._turns.Count == 0))
        {
            throw new ArgumentException("Версия не соответствует сохранённым изменениям.");
        }
        dialog.Revision = revision;
        dialog._lastChangedAtUtc = lastChangedAtUtc;
        return dialog;
    }

    /// <summary>Принимает промежуточные данные выполняющегося обращения и инвалидирует прежний snapshot.</summary>
    public DialogMutationResult TryAppendTurn(DialogOwnerId ownerId, DialogStateVersion version, Guid turnId, DateTimeOffset nowUtc)
    {
        DialogMutationResult result = CheckVersion(ownerId, version, nowUtc);
        if (result != DialogMutationResult.Success)
        {
            return result;
        }
        DialogTurn? turn = _turns.Find(item => item.Id == turnId);
        if (turn is null)
        {
            return DialogMutationResult.TurnNotFound;
        }
        if (turn.Status != DialogTurnStatus.InProgress)
        {
            return DialogMutationResult.TurnAlreadyFinished;
        }
        CommitRevision(NextRevision(nowUtc), nowUtc);
        return DialogMutationResult.Success;
    }

    /// <summary>Проверяет истечение, включая точное равенство времени сроку.</summary>
    public bool IsExpired(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        return nowUtc >= ExpiresAtUtc;
    }

    /// <summary>Проверяет доступность продолжения для указанного владельца.</summary>
    public bool IsAvailable(DialogOwnerId ownerId, DateTimeOffset nowUtc) => CheckAvailability(ownerId, nowUtc) == DialogMutationResult.Success;

    /// <summary>Получает снимок для последующей проверки актуальности внешнего результата.</summary>
    public DialogMutationResult TryCaptureVersion(DialogOwnerId ownerId, DateTimeOffset nowUtc, out DialogStateVersion? version)
    {
        version = null;
        DialogMutationResult result = CheckAvailability(ownerId, nowUtc);
        if (result != DialogMutationResult.Success)
        {
            return result;
        }

        version = new DialogStateVersion(Id, _lifetimeId, Revision);
        return DialogMutationResult.Success;
    }

    /// <summary>Начинает новое обращение и возвращает снимок уже изменённого диалога.</summary>
    public DialogMutationResult TryBeginTurn(DialogOwnerId ownerId, Guid turnId, DateTimeOffset nowUtc, out DialogStateVersion? version)
    {
        version = null;
        if (turnId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор обращения не должен быть пустым.", nameof(turnId));
        }

        DialogMutationResult result = CheckAvailability(ownerId, nowUtc);
        if (result != DialogMutationResult.Success)
        {
            return result;
        }

        if (_turns.Any(turn => turn.Id == turnId))
        {
            return DialogMutationResult.DuplicateTurn;
        }

        long revision = NextRevision(nowUtc);
        long sequence = checked((long)_turns.Count + 1);
        DialogTurn turn = new(turnId, sequence, nowUtc, DialogTurnStatus.InProgress, null);
        _turns.Add(turn);
        CommitRevision(revision, nowUtc);
        version = new DialogStateVersion(Id, _lifetimeId, Revision);
        return DialogMutationResult.Success;
    }

    /// <summary>Принимает конечный статус только для актуального снимка и незавершённого обращения.</summary>
    /// <remarks>Подтверждение terminal completion относится к адаптеру; наличие текста не является таким подтверждением.</remarks>
    public DialogMutationResult TryFinishTurn(DialogOwnerId ownerId, DialogStateVersion version, Guid turnId, DialogTurnStatus status, DateTimeOffset nowUtc)
    {
        if (!Enum.IsDefined(status) || status == DialogTurnStatus.InProgress)
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Необходим конечный статус обращения.");
        }

        DialogMutationResult result = CheckVersion(ownerId, version, nowUtc);
        if (result != DialogMutationResult.Success)
        {
            return result;
        }

        int index = _turns.FindIndex(turn => turn.Id == turnId);
        if (index < 0)
        {
            return DialogMutationResult.TurnNotFound;
        }

        DialogTurn turn = _turns[index];
        if (turn.Status != DialogTurnStatus.InProgress)
        {
            return DialogMutationResult.TurnAlreadyFinished;
        }

        long revision = NextRevision(nowUtc);
        _turns[index] = new DialogTurn(turn.Id, turn.Sequence, turn.StartedAtUtc, status, nowUtc);
        CommitRevision(revision, nowUtc);
        return DialogMutationResult.Success;
    }

    /// <summary>Принимает следующую версию контекста для префикса завершённых обращений без удаления истории.</summary>
    /// <remarks>
    /// Ноль означает отсутствие покрытых обращений. Все обращения префикса должны иметь конечный статус.
    /// Метод вызывается после успешного compact; повторные проходы могут покрывать тот же префикс.
    /// Метаданные не определяют cutoff отдельных событий Responses и не заменяют сохранение их содержимого.
    /// </remarks>
    public DialogMutationResult TryApplyContext(DialogOwnerId ownerId, DialogStateVersion version, long throughTurnSequence, DateTimeOffset nowUtc)
    {
        DialogMutationResult result = CheckVersion(ownerId, version, nowUtc);
        if (result != DialogMutationResult.Success)
        {
            return result;
        }

        if (throughTurnSequence < 0 || throughTurnSequence > _turns.Count || throughTurnSequence < (ActiveContext?.ThroughTurnSequence ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(throughTurnSequence), "Контекст должен покрывать существующий префикс без возврата назад.");
        }

        if (_turns.Take((int)throughTurnSequence).Any(turn => turn.Status == DialogTurnStatus.InProgress))
        {
            return DialogMutationResult.UnfinishedContextRange;
        }

        long revision = NextRevision(nowUtc);
        long contextVersion = checked((ActiveContext?.Version ?? 0) + 1);
        DialogContextState context = new(contextVersion, throughTurnSequence, nowUtc);
        _contextStates.Add(context);
        CommitRevision(revision, nowUtc);
        return DialogMutationResult.Success;
    }

    /// <summary>Делает диалог и производные состояния недоступными; допускает очистку после истечения.</summary>
    public DialogMutationResult TryDelete(DialogOwnerId ownerId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        EnsureUtc(nowUtc, nameof(nowUtc));
        if (!OwnerId.Equals(ownerId))
        {
            return DialogMutationResult.OwnerMismatch;
        }

        if (IsDeleted)
        {
            return DialogMutationResult.Deleted;
        }

        long revision = NextRevision(nowUtc);
        _turns.Clear();
        _contextStates.Clear();
        IsDeleted = true;
        CommitRevision(revision, nowUtc);
        return DialogMutationResult.Success;
    }

    /// <summary>Проверяет доступность и принадлежность снимка текущему состоянию.</summary>
    private DialogMutationResult CheckVersion(DialogOwnerId ownerId, DialogStateVersion version, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(version);
        DialogMutationResult result = CheckAvailability(ownerId, nowUtc);
        if (result != DialogMutationResult.Success)
        {
            return result;
        }

        return Id.Equals(version.DialogId) && version.Matches(_lifetimeId, Revision)
            ? DialogMutationResult.Success
            : DialogMutationResult.StaleOperation;
    }

    /// <summary>Проверяет владельца, удаление и срок без изменения агрегата.</summary>
    private DialogMutationResult CheckAvailability(DialogOwnerId ownerId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        EnsureUtc(nowUtc, nameof(nowUtc));
        if (!OwnerId.Equals(ownerId))
        {
            return DialogMutationResult.OwnerMismatch;
        }

        if (IsDeleted)
        {
            return DialogMutationResult.Deleted;
        }

        return IsExpired(nowUtc) ? DialogMutationResult.Expired : DialogMutationResult.Success;
    }

    /// <summary>Проверяет хронологию и переполнение до изменения состояния.</summary>
    private long NextRevision(DateTimeOffset nowUtc)
    {
        if (nowUtc < _lastChangedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUtc), "Время изменения не должно предшествовать состоянию диалога.");
        }

        return checked(Revision + 1);
    }

    /// <summary>Фиксирует уже проверенную версию успешной операции.</summary>
    private void CommitRevision(long revision, DateTimeOffset nowUtc)
    {
        Revision = revision;
        _lastChangedAtUtc = nowUtc;
    }

    /// <summary>Отклоняет время с ненулевым смещением.</summary>
    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время должно быть задано в UTC.", parameterName);
        }
    }
}

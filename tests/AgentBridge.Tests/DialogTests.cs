using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Изолированные проверки доменного агрегата через публичные операции с управляемым временем.</summary>
public class DialogTests
{
    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From("tenant/user-42");

    /// <summary>Настроенный нестандартный период фиксируется при создании, новые настройки влияют только на будущие диалоги.</summary>
    [Fact]
    public void ConfiguredExpirationIsFixedAcrossTurnsCompactionAndSettingsChanges()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(_ => { }, options => { options.RetentionPeriod = TimeSpan.FromHours(36); options.SoftContentLimitBytes = 10_485_760; });
        using ServiceProvider provider = services.BuildServiceProvider();
        DialogRetentionOptions options = provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value;
        Dialog dialog = Dialog.Create(DialogId.From(Guid.NewGuid()), OWNER, CREATED_AT_UTC, options.CalculateExpiresAtUtc(CREATED_AT_UTC));
        options.RetentionPeriod = TimeSpan.FromDays(21);
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC.AddHours(1));
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, turnId, DialogTurnStatus.Completed, CREATED_AT_UTC.AddHours(2)));
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC.AddHours(2)), 1, CREATED_AT_UTC.AddHours(3)));
        Begin(dialog, Guid.NewGuid(), CREATED_AT_UTC.AddHours(4));
        Assert.Equal(CREATED_AT_UTC.AddHours(36), dialog.ExpiresAtUtc);
        Dialog future = Dialog.Create(DialogId.From(Guid.NewGuid()), OWNER, CREATED_AT_UTC, options.CalculateExpiresAtUtc(CREATED_AT_UTC));
        Assert.Equal(CREATED_AT_UTC.AddDays(21), future.ExpiresAtUtc);
    }

    /// <summary>До границы диалог доступен, на самой границе и после неё все записи результатов отклоняются.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExactExpirationBoundaryRejectsNewTurnsAndLateResults(long ticksAfterExpiration)
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        Assert.True(dialog.IsAvailable(OWNER, dialog.ExpiresAtUtc!.Value.AddTicks(-1)));
        DateTimeOffset expired = dialog.ExpiresAtUtc!.Value.AddTicks(ticksAfterExpiration);
        Assert.True(dialog.IsExpired(expired));
        Assert.False(dialog.IsAvailable(OWNER, expired));
        Assert.Equal(DialogMutationResult.Expired, dialog.TryCaptureVersion(OWNER, expired, out DialogStateVersion? missing));
        Assert.Null(missing);
        Assert.Equal(DialogMutationResult.Expired, dialog.TryBeginTurn(OWNER, Guid.NewGuid(), expired, out missing));
        Assert.Null(missing);
        Assert.Equal(DialogMutationResult.Expired, dialog.TryFinishTurn(OWNER, version, turnId, DialogTurnStatus.Completed, expired));
        Assert.Equal(DialogMutationResult.Expired, dialog.TryApplyContext(OWNER, version, 1, expired));
        Assert.Equal(1, dialog.Revision);
        Assert.Equal(DialogTurnStatus.InProgress, Assert.Single(dialog.Turns).Status);
        Assert.Empty(dialog.ContextStates);
    }

    /// <summary>Другой владелец не может начать обращение, получить снимок, записать результат, compact или удалить диалог.</summary>
    [Fact]
    public void ForeignOwnerCannotMutateOrCaptureDialog()
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        DialogOwnerId foreign = DialogOwnerId.From("tenant/User-42");
        Assert.False(dialog.IsAvailable(foreign, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.OwnerMismatch, dialog.TryCaptureVersion(foreign, CREATED_AT_UTC, out DialogStateVersion? missing));
        Assert.Null(missing);
        Assert.Equal(DialogMutationResult.OwnerMismatch, dialog.TryBeginTurn(foreign, Guid.NewGuid(), CREATED_AT_UTC, out missing));
        Assert.Null(missing);
        Assert.Equal(DialogMutationResult.OwnerMismatch, dialog.TryFinishTurn(foreign, version, turnId, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.OwnerMismatch, dialog.TryApplyContext(foreign, version, 1, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.OwnerMismatch, dialog.TryDelete(foreign, CREATED_AT_UTC));
        Assert.Equal(1, dialog.Revision);
        Assert.False(dialog.IsDeleted);
    }

    /// <summary>Порядок задаётся началом обращений даже при одинаковом времени и обратном порядке завершения.</summary>
    [Fact]
    public void TurnOrderDoesNotDependOnCompletionOrderOrTimestamp()
    {
        Dialog dialog = Create();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Begin(dialog, first, CREATED_AT_UTC);
        DialogStateVersion version = Begin(dialog, second, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, second, DialogTurnStatus.Incomplete, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, Capture(dialog, CREATED_AT_UTC), first, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(new[] { first, second }, dialog.Turns.Select(turn => turn.Id));
        Assert.Equal(new long[] { 1, 2 }, dialog.Turns.Select(turn => turn.Sequence));
        Assert.Equal(DialogTurnStatus.Incomplete, dialog.Turns[1].Status);
    }

    /// <summary>Каждый конечный статус фиксируется один раз, старый дочерний снимок не изменяется.</summary>
    [Theory]
    [InlineData(DialogTurnStatus.Completed)]
    [InlineData(DialogTurnStatus.Failed)]
    [InlineData(DialogTurnStatus.Canceled)]
    [InlineData(DialogTurnStatus.Incomplete)]
    public void TerminalStatusCannotBeRewritten(DialogTurnStatus status)
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        DialogTurn old = Assert.Single(dialog.Turns);
        DateTimeOffset finished = CREATED_AT_UTC.AddMinutes(1);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, turnId, status, finished));
        Assert.Equal(status, dialog.Turns[0].Status);
        Assert.Equal(finished, dialog.Turns[0].FinishedAtUtc);
        Assert.Equal(DialogTurnStatus.InProgress, old.Status);
        Assert.Null(old.FinishedAtUtc);
        Assert.Equal(DialogMutationResult.TurnAlreadyFinished, dialog.TryFinishTurn(OWNER, Capture(dialog, finished), turnId, DialogTurnStatus.Completed, finished));
        Assert.Equal(2, dialog.Revision);
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, finished), 1, finished));
        Assert.Equal(1, dialog.ActiveContext!.ThroughTurnSequence);
    }

    /// <summary>Некорректный конечный статус не меняет обращение и версию.</summary>
    [Theory]
    [InlineData(DialogTurnStatus.InProgress)]
    [InlineData((DialogTurnStatus)99)]
    public void InvalidTerminalStatusFailsWithoutMutation(DialogTurnStatus status)
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TryFinishTurn(OWNER, version, turnId, status, CREATED_AT_UTC));
        Assert.Equal(1, dialog.Revision);
        Assert.Equal(DialogTurnStatus.InProgress, dialog.Turns[0].Status);
    }

    /// <summary>Ошибки идентичности обращения не создают повторов и не изменяют чужое обращение.</summary>
    [Fact]
    public void DuplicateAndMissingTurnDoNotMutateState()
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.DuplicateTurn, dialog.TryBeginTurn(OWNER, turnId, CREATED_AT_UTC, out DialogStateVersion? missing));
        Assert.Null(missing);
        Assert.Equal(DialogMutationResult.TurnNotFound, dialog.TryFinishTurn(OWNER, version, Guid.NewGuid(), DialogTurnStatus.Failed, CREATED_AT_UTC));
        Assert.Throws<ArgumentException>(() => dialog.TryBeginTurn(OWNER, Guid.Empty, CREATED_AT_UTC, out _));
        Assert.Single(dialog.Turns);
        Assert.Equal(1, dialog.Revision);
    }

    /// <summary>Повторные compact одного префикса дают новые версии, новые обращения остаются после покрытой части.</summary>
    [Fact]
    public void ContextVersionsPreserveHistoryAndAdvanceCoverage()
    {
        Dialog dialog = Create();
        Guid first = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, first, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, first, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), 1, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), 1, CREATED_AT_UTC));
        Guid second = Guid.NewGuid();
        version = Begin(dialog, second, CREATED_AT_UTC);
        Assert.Equal(1, dialog.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, second, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), 2, CREATED_AT_UTC));
        Assert.Equal(new long[] { 1, 2, 3 }, dialog.ContextStates.Select(context => context.Version));
        Assert.Equal(new long[] { 1, 1, 2 }, dialog.ContextStates.Select(context => context.ThroughTurnSequence));
        Assert.Same(dialog.ContextStates[2], dialog.ActiveContext);
        Assert.Equal(2, dialog.Turns.Count);
    }

    /// <summary>Compact не может покрыть отсутствующие обращения или вернуть покрытие истории назад.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void InvalidContextCoveragePreservesPreviousContext(long sequence)
    {
        Dialog dialog = Create();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, first, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, first, DialogTurnStatus.Completed, CREATED_AT_UTC));
        version = Begin(dialog, second, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, second, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), 2, CREATED_AT_UTC));
        DialogContextState? original = dialog.ActiveContext;
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), sequence, CREATED_AT_UTC));
        Assert.Same(original, dialog.ActiveContext);
        Assert.Single(dialog.ContextStates);
        Assert.Equal(5, dialog.Revision);
    }

    /// <summary>Изменение контекста делает предыдущие результаты compact и обращения устаревшими.</summary>
    [Fact]
    public void ChangedContextRejectsOldOperationWithoutOverwritingActiveState()
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, version, 0, CREATED_AT_UTC));
        DialogContextState? active = dialog.ActiveContext;
        Assert.Equal(DialogMutationResult.StaleOperation, dialog.TryApplyContext(OWNER, version, 0, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.StaleOperation, dialog.TryFinishTurn(OWNER, version, turnId, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Same(active, dialog.ActiveContext);
        Assert.Equal(2, dialog.Revision);
    }

    /// <summary>Любое успешное изменение обращения делает предыдущий снимок устаревшим.</summary>
    [Fact]
    public void TurnChangesInvalidatePreviouslyCapturedVersion()
    {
        Dialog dialog = Create();
        DialogStateVersion empty = Capture(dialog, CREATED_AT_UTC);
        Guid first = Guid.NewGuid();
        DialogStateVersion firstVersion = Begin(dialog, first, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.StaleOperation, dialog.TryFinishTurn(OWNER, empty, first, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Guid second = Guid.NewGuid();
        DialogStateVersion secondVersion = Begin(dialog, second, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.StaleOperation, dialog.TryFinishTurn(OWNER, firstVersion, first, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, secondVersion, second, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.StaleOperation, dialog.TryApplyContext(OWNER, secondVersion, 2, CREATED_AT_UTC));
    }

    /// <summary>Снимки не принимаются другим диалогом или заново созданным объектом с тем же публичным ID.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperationCannotCrossDialogLifetimes(bool reuseId)
    {
        Dialog original = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(original, turnId, CREATED_AT_UTC);
        Dialog other = Dialog.Create(reuseId ? original.Id : DialogId.From(Guid.NewGuid()), OWNER, CREATED_AT_UTC, original.ExpiresAtUtc);
        Begin(other, turnId, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.StaleOperation, other.TryFinishTurn(OWNER, version, turnId, DialogTurnStatus.Completed, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.StaleOperation, other.TryApplyContext(OWNER, version, 1, CREATED_AT_UTC));
        Assert.Equal(DialogTurnStatus.InProgress, other.Turns[0].Status);
    }

    /// <summary>Явное удаление и очистка истёкшего диалога удаляют производные состояния и отклоняют поздние результаты.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletedDialogCannotBeRevivedByLateTurnOrCompact(bool expired)
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, version, 0, CREATED_AT_UTC));
        version = Capture(dialog, CREATED_AT_UTC);
        DateTimeOffset deletionTime = expired ? dialog.ExpiresAtUtc!.Value : CREATED_AT_UTC.AddMinutes(1);
        Assert.Equal(DialogMutationResult.Success, dialog.TryDelete(OWNER, deletionTime));
        Assert.True(dialog.IsDeleted);
        Assert.Empty(dialog.Turns);
        Assert.Empty(dialog.ContextStates);
        Assert.Null(dialog.ActiveContext);
        Assert.False(dialog.IsAvailable(OWNER, deletionTime));
        Assert.Equal(DialogMutationResult.Deleted, dialog.TryFinishTurn(OWNER, version, turnId, DialogTurnStatus.Completed, deletionTime));
        Assert.Equal(DialogMutationResult.Deleted, dialog.TryApplyContext(OWNER, version, 1, deletionTime));
        Assert.Equal(DialogMutationResult.Deleted, dialog.TryBeginTurn(OWNER, Guid.NewGuid(), deletionTime, out DialogStateVersion? missing));
        Assert.Null(missing);
        Assert.Equal(DialogMutationResult.Deleted, dialog.TryCaptureVersion(OWNER, deletionTime, out missing));
        Assert.Null(missing);
        Assert.Equal(DialogMutationResult.Deleted, dialog.TryDelete(OWNER, deletionTime));
        Assert.Equal(3, dialog.Revision);
    }

    /// <summary>Создание нового независимого диалога не удаляет предыдущий и не наследует его контекст.</summary>
    [Fact]
    public void NewDialogDoesNotChangeExistingDialog()
    {
        Dialog first = Create();
        Begin(first, Guid.NewGuid(), CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, first.TryApplyContext(OWNER, Capture(first, CREATED_AT_UTC), 0, CREATED_AT_UTC));
        Dialog second = Create();
        Assert.NotEqual(first.Id, second.Id);
        Assert.Empty(second.Turns);
        Assert.Null(second.ActiveContext);
        Assert.False(first.IsDeleted);
        Assert.Single(first.Turns);
        Assert.Single(first.ContextStates);
    }

    /// <summary>Коллекции защищены и при приведении к изменяемому интерфейсу.</summary>
    [Fact]
    public void CollectionViewsCannotBypassRootInvariants()
    {
        Dialog dialog = Create();
        Begin(dialog, Guid.NewGuid(), CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), 0, CREATED_AT_UTC));
        IList<DialogTurn> turns = Assert.IsAssignableFrom<IList<DialogTurn>>(dialog.Turns);
        IList<DialogContextState> states = Assert.IsAssignableFrom<IList<DialogContextState>>(dialog.ContextStates);
        Assert.Throws<NotSupportedException>(() => turns.Clear());
        Assert.Throws<NotSupportedException>(() => turns.Add(turns[0]));
        Assert.Throws<NotSupportedException>(() => turns[0] = turns[0]);
        Assert.Throws<NotSupportedException>(() => states.Clear());
        Assert.Throws<NotSupportedException>(() => states.Add(states[0]));
        Assert.Throws<NotSupportedException>(() => states[0] = states[0]);
        Assert.Single(dialog.Turns);
        Assert.Single(dialog.ContextStates);
    }

    /// <summary>Пустые идентичности отклоняются; допустимые значения сравниваются без нормализации.</summary>
    [Fact]
    public void IdentityValuesPreserveApplicationSemantics()
    {
        Assert.Throws<ArgumentException>(() => DialogId.From(Guid.Empty));
        Assert.Throws<ArgumentNullException>(() => DialogOwnerId.From(null!));
        Assert.Throws<ArgumentException>(() => DialogOwnerId.From(" "));
        Guid id = Guid.NewGuid();
        Assert.Equal(DialogId.From(id), DialogId.From(id));
        Assert.Equal(DialogOwnerId.From("Пользователь/42"), DialogOwnerId.From("Пользователь/42"));
        Assert.NotEqual(OWNER, DialogOwnerId.From("TENANT/user-42"));
        Assert.Equal(" user ", DialogOwnerId.From(" user ").Value);
    }

    /// <summary>Создание отклоняет неверные даты, UTC и отсутствующие идентичности.</summary>
    [Fact]
    public void CreationRejectsInvalidDatesAndIdentities()
    {
        DialogId id = DialogId.From(Guid.NewGuid());
        Assert.Throws<ArgumentNullException>(() => Dialog.Create(null!, OWNER, CREATED_AT_UTC, CREATED_AT_UTC.AddHours(1)));
        Assert.Throws<ArgumentNullException>(() => Dialog.Create(id, null!, CREATED_AT_UTC, CREATED_AT_UTC.AddHours(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Dialog.Create(id, OWNER, CREATED_AT_UTC, CREATED_AT_UTC));
        Assert.Throws<ArgumentOutOfRangeException>(() => Dialog.Create(id, OWNER, CREATED_AT_UTC, CREATED_AT_UTC.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Dialog.Create(id, OWNER, CREATED_AT_UTC.ToOffset(TimeSpan.FromHours(7)), CREATED_AT_UTC.AddHours(1)));
        Assert.Throws<ArgumentException>(() => Dialog.Create(id, OWNER, CREATED_AT_UTC, CREATED_AT_UTC.AddHours(1).ToOffset(TimeSpan.FromHours(7))));
    }

    /// <summary>Все доменные операции отклоняют не-UTC время без изменений.</summary>
    [Fact]
    public void OperationsRejectNonUtcTimeWithoutMutation()
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DialogStateVersion version = Begin(dialog, turnId, CREATED_AT_UTC);
        DateTimeOffset nonUtc = CREATED_AT_UTC.ToOffset(TimeSpan.FromHours(7));
        Assert.Throws<ArgumentException>(() => dialog.IsExpired(nonUtc));
        Assert.Throws<ArgumentException>(() => dialog.IsAvailable(OWNER, nonUtc));
        Assert.Throws<ArgumentException>(() => dialog.TryCaptureVersion(OWNER, nonUtc, out _));
        Assert.Throws<ArgumentException>(() => dialog.TryBeginTurn(OWNER, Guid.NewGuid(), nonUtc, out _));
        Assert.Throws<ArgumentException>(() => dialog.TryFinishTurn(OWNER, version, turnId, DialogTurnStatus.Completed, nonUtc));
        Assert.Throws<ArgumentException>(() => dialog.TryApplyContext(OWNER, version, 1, nonUtc));
        Assert.Throws<ArgumentException>(() => dialog.TryDelete(OWNER, nonUtc));
        Assert.Equal(1, dialog.Revision);
    }

    /// <summary>Время записи не может предшествовать последнему изменению агрегата.</summary>
    [Fact]
    public void BackwardMutationTimeDoesNotLeavePartialChanges()
    {
        Dialog dialog = Create();
        Guid turnId = Guid.NewGuid();
        DateTimeOffset started = CREATED_AT_UTC.AddMinutes(1);
        DialogStateVersion version = Begin(dialog, turnId, started);
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TryBeginTurn(OWNER, Guid.NewGuid(), CREATED_AT_UTC, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TryFinishTurn(OWNER, version, turnId, DialogTurnStatus.Failed, CREATED_AT_UTC));
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TryApplyContext(OWNER, version, 0, CREATED_AT_UTC));
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TryDelete(OWNER, CREATED_AT_UTC));
        Assert.Equal(1, dialog.Revision);
        Assert.Single(dialog.Turns);
        Assert.Empty(dialog.ContextStates);
        Assert.False(dialog.IsDeleted);
    }

    /// <summary>Выполняющееся обращение и незавершённая дыра внутри префикса не считаются покрытыми compact.</summary>
    [Fact]
    public void ContextCannotCoverRunningTurnOrGapInTerminalPrefix()
    {
        Dialog dialog = Create();
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), 0, CREATED_AT_UTC));
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Begin(dialog, first, CREATED_AT_UTC);
        DialogStateVersion version = Begin(dialog, second, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, second, DialogTurnStatus.Completed, CREATED_AT_UTC));
        version = Capture(dialog, CREATED_AT_UTC);
        Assert.Equal(DialogMutationResult.UnfinishedContextRange, dialog.TryApplyContext(OWNER, version, 1, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.UnfinishedContextRange, dialog.TryApplyContext(OWNER, version, 2, CREATED_AT_UTC));
        Assert.Equal(0, dialog.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(4, dialog.Revision);
        Assert.Equal(DialogMutationResult.Success, dialog.TryFinishTurn(OWNER, version, first, DialogTurnStatus.Incomplete, CREATED_AT_UTC));
        Assert.Equal(DialogMutationResult.Success, dialog.TryApplyContext(OWNER, Capture(dialog, CREATED_AT_UTC), 2, CREATED_AT_UTC));
        Assert.Equal(2, dialog.ActiveContext.ThroughTurnSequence);
    }

    /// <summary>Создаёт допустимый диалог без options, HTTP и хранения.</summary>
    private static Dialog Create() => Dialog.Create(DialogId.From(Guid.NewGuid()), OWNER, CREATED_AT_UTC, CREATED_AT_UTC.AddHours(36));

    /// <summary>Начинает обращение через тот же публичный контракт, что использует приложение.</summary>
    private static DialogStateVersion Begin(Dialog dialog, Guid turnId, DateTimeOffset nowUtc)
    {
        Assert.Equal(DialogMutationResult.Success, dialog.TryBeginTurn(OWNER, turnId, nowUtc, out DialogStateVersion? version));
        return Assert.IsType<DialogStateVersion>(version);
    }

    /// <summary>Получает актуальный снимок без доступа к закрытому состоянию.</summary>
    private static DialogStateVersion Capture(Dialog dialog, DateTimeOffset nowUtc)
    {
        Assert.Equal(DialogMutationResult.Success, dialog.TryCaptureVersion(OWNER, nowUtc, out DialogStateVersion? version));
        return Assert.IsType<DialogStateVersion>(version);
    }
}

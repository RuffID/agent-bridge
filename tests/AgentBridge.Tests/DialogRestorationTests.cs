using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Валидирующее production-восстановление и продолжение через защищённый Domain API.</summary>
public class DialogRestorationTests
{
    private static readonly DateTimeOffset CREATED = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From("owner");

    /// <summary>Без возможной последней append mutation время root совпадает с известным изменением; входные списки не меняются.</summary>
    [Theory]
    [InlineData("context-only")]
    [InlineData("begin")]
    [InlineData("terminal")]
    [InlineData("terminal-append")]
    public void LastChangedWithoutCorrespondingMutationIsRejected(string state)
    {
        List<DialogTurnSnapshot> turns = [];
        List<DialogContextSnapshot> contexts = [];
        long revision = 1;
        DateTimeOffset latest = CREATED.AddMinutes(1);
        if (state == "context-only") { contexts.Add(new(1, 0, latest)); }
        else
        {
            bool terminal = state != "begin";
            turns.Add(new(Guid.NewGuid(), 1, CREATED, terminal ? DialogTurnStatus.Completed : DialogTurnStatus.InProgress,
                terminal ? latest : null));
            if (!terminal) { latest = CREATED; }
            revision = terminal ? (state == "terminal-append" ? 3 : 2) : 1;
        }
        DialogTurnSnapshot[] originalTurns = turns.ToArray();
        DialogContextSnapshot[] originalContexts = contexts.ToArray();
        DateTimeOffset changed = state == "context-only" ? CREATED.AddMinutes(2) : latest.AddTicks(1);
        Assert.Throws<ArgumentException>(() => Dialog.Restore(DialogId.From(Guid.NewGuid()), OWNER, CREATED,
            CREATED.AddDays(1), revision, changed, turns, contexts));
        Assert.Equal(originalTurns, turns);
        Assert.Equal(originalContexts, contexts);
    }

    /// <summary>Каждая штатная стадия, включая append после compact и одинаковые времена, допускает точный round-trip.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(6, true)]
    public void MutationStagesRoundTrip(int stage, bool sameTime)
    {
        Dialog original = Dialog.Create(DialogId.From(Guid.NewGuid()), OWNER, CREATED, CREATED.AddDays(1));
        Guid turn = Guid.NewGuid();
        DateTimeOffset Now(int step) => sameTime ? CREATED : CREATED.AddMinutes(step);
        if (stage >= 1) { Assert.Equal(DialogMutationResult.Success, original.TryBeginTurn(OWNER, turn, Now(1), out _)); }
        if (stage >= 2)
        {
            original.TryCaptureVersion(OWNER, Now(2), out DialogStateVersion? version);
            Assert.Equal(DialogMutationResult.Success, original.TryApplyContext(OWNER, version!, 0, Now(2)));
        }
        if (stage >= 3)
        {
            original.TryCaptureVersion(OWNER, Now(3), out DialogStateVersion? version);
            Assert.Equal(DialogMutationResult.Success, original.TryAppendTurn(OWNER, version!, turn, Now(3)));
        }
        if (stage >= 4)
        {
            original.TryCaptureVersion(OWNER, Now(4), out DialogStateVersion? version);
            Assert.Equal(DialogMutationResult.Success, original.TryFinishTurn(OWNER, version!, turn, DialogTurnStatus.Completed, Now(4)));
        }
        foreach (int step in Enumerable.Range(5, Math.Max(0, stage - 4)))
        {
            original.TryCaptureVersion(OWNER, Now(step), out DialogStateVersion? version);
            Assert.Equal(DialogMutationResult.Success, original.TryApplyContext(OWNER, version!, 1, Now(step)));
        }
        original.TryCaptureVersion(OWNER, Now(stage), out DialogStateVersion? old);
        Dialog restored = Dialog.Restore(original.Id, OWNER, CREATED, original.ExpiresAtUtc, original.Revision,
            original.LastChangedAtUtc,
            original.Turns.Select(item => new DialogTurnSnapshot(item.Id, item.Sequence, item.StartedAtUtc, item.Status, item.FinishedAtUtc)),
            original.ContextStates.Select(item => new DialogContextSnapshot(item.Version, item.ThroughTurnSequence, item.CreatedAtUtc)));
        Assert.Equal(stage, restored.Revision);
        Assert.Equal(original.LastChangedAtUtc, restored.LastChangedAtUtc);
        Assert.Equal(original.ExpiresAtUtc, restored.ExpiresAtUtc);
        Assert.Equal(original.OwnerId, restored.OwnerId);
        Assert.Equal(original.Turns.Select(item => item.Status), restored.Turns.Select(item => item.Status));
        Assert.Equal(original.ContextStates.Select(item => item.CreatedAtUtc), restored.ContextStates.Select(item => item.CreatedAtUtc));
        Assert.Equal(DialogMutationResult.StaleOperation, restored.TryApplyContext(OWNER, old!, 0, Now(stage)));
        Assert.Equal(DialogMutationResult.OwnerMismatch, restored.TryCaptureVersion(DialogOwnerId.From("other"), Now(stage), out _));
        Assert.Equal(DialogMutationResult.Expired, restored.TryCaptureVersion(OWNER, original.ExpiresAtUtc!.Value, out _));
    }

    /// <summary>Context-only compact допускает повторные версии и одинаковое время без изменения срока.</summary>
    [Fact]
    public void ContextOnlyRoundTripKeepsLatestKnownMutation()
    {
        Dialog original = Dialog.Create(DialogId.From(Guid.NewGuid()), OWNER, CREATED, CREATED.AddDays(1));
        foreach (DateTimeOffset now in new[] { CREATED.AddMinutes(1), CREATED.AddMinutes(1), CREATED.AddMinutes(2) })
        {
            original.TryCaptureVersion(OWNER, now, out DialogStateVersion? version);
            Assert.Equal(DialogMutationResult.Success, original.TryApplyContext(OWNER, version!, 0, now));
        }
        Dialog restored = Dialog.Restore(original.Id, OWNER, CREATED, original.ExpiresAtUtc, original.Revision,
            original.LastChangedAtUtc, [], original.ContextStates.Select(item => new DialogContextSnapshot(item.Version, 0, item.CreatedAtUtc)));
        Assert.Empty(restored.Turns);
        Assert.Equal(3, restored.Revision);
        Assert.Equal(3, restored.ActiveContext!.Version);
        Assert.Equal(CREATED.AddMinutes(2), restored.LastChangedAtUtc);
    }

    /// <summary>Предельная revision восстанавливается без переполнения; новая mutation отклоняется до изменения.</summary>
    [Fact]
    public void MaximumAppendRevisionFailsBeforeMutation()
    {
        Dialog dialog = Dialog.Restore(DialogId.From(Guid.NewGuid()), OWNER, CREATED, CREATED.AddDays(1), long.MaxValue,
            CREATED.AddMinutes(2), [new(Guid.NewGuid(), 1, CREATED, DialogTurnStatus.InProgress, null)], []);
        dialog.TryCaptureVersion(OWNER, CREATED.AddMinutes(3), out DialogStateVersion? version);
        Assert.Throws<OverflowException>(() => dialog.TryAppendTurn(OWNER, version!, dialog.Turns[0].Id, CREATED.AddMinutes(3)));
        Assert.Equal(long.MaxValue, dialog.Revision);
        Assert.Equal(CREATED.AddMinutes(2), dialog.LastChangedAtUtc);
        Assert.Single(dialog.Turns);
    }

    /// <summary>UTC, фиксированные даты и revision проверяются также на внешнем context-only snapshot.</summary>
    [Theory]
    [InlineData("created-offset")]
    [InlineData("expiry-offset")]
    [InlineData("changed-offset")]
    [InlineData("context-offset")]
    [InlineData("expiry-at-creation")]
    [InlineData("changed-before-creation")]
    [InlineData("changed-at-expiry")]
    [InlineData("negative-revision")]
    [InlineData("extra-revision-without-turn")]
    public void ContextOnlyRestoreRejectsDateAndRevisionBounds(string corruption)
    {
        DateTimeOffset created = CREATED;
        DateTimeOffset expiry = CREATED.AddDays(1);
        DateTimeOffset changed = CREATED.AddMinutes(1);
        DateTimeOffset context = changed;
        long revision = 1;
        if (corruption == "created-offset") { created = created.ToOffset(TimeSpan.FromHours(1)); }
        if (corruption == "expiry-offset") { expiry = expiry.ToOffset(TimeSpan.FromHours(1)); }
        if (corruption == "changed-offset") { changed = changed.ToOffset(TimeSpan.FromHours(1)); }
        if (corruption == "context-offset") { context = context.ToOffset(TimeSpan.FromHours(1)); }
        if (corruption == "expiry-at-creation") { expiry = created; }
        if (corruption == "changed-before-creation") { changed = created.AddTicks(-1); }
        if (corruption == "changed-at-expiry") { changed = expiry; }
        if (corruption == "negative-revision") { revision = -1; }
        if (corruption == "extra-revision-without-turn") { revision = 2; }
        Assert.ThrowsAny<ArgumentException>(() => Dialog.Restore(DialogId.From(Guid.NewGuid()), OWNER, created,
            expiry, revision, changed, [], [new(1, 0, context)]));
    }

    /// <summary>Реальные revision/хронология/контексты сохраняются; старый локальный lifetime не переносится.</summary>
    [Fact]
    public void RestorePreservesHistoryAndRejectsOtherInstanceSnapshot()
    {
        Dialog original = Dialog.Create(DialogId.From(Guid.NewGuid()), OWNER, CREATED, CREATED.AddDays(1));
        Guid turn = Guid.NewGuid();
        original.TryBeginTurn(OWNER, turn, CREATED.AddMinutes(1), out DialogStateVersion? started);
        original.TryAppendTurn(OWNER, started!, turn, CREATED.AddMinutes(2));
        original.TryCaptureVersion(OWNER, CREATED.AddMinutes(3), out DialogStateVersion? version);
        original.TryFinishTurn(OWNER, version!, turn, DialogTurnStatus.Completed, CREATED.AddMinutes(3));
        original.TryCaptureVersion(OWNER, CREATED.AddMinutes(4), out version);
        original.TryApplyContext(OWNER, version!, 1, CREATED.AddMinutes(4));
        original.TryCaptureVersion(OWNER, CREATED.AddMinutes(4), out version);
        Dialog restored = Dialog.Restore(original.Id, original.OwnerId, original.CreatedAtUtc, original.ExpiresAtUtc,
            original.Revision, original.LastChangedAtUtc,
            original.Turns.Select(item => new DialogTurnSnapshot(item.Id, item.Sequence, item.StartedAtUtc, item.Status, item.FinishedAtUtc)),
            original.ContextStates.Select(item => new DialogContextSnapshot(item.Version, item.ThroughTurnSequence, item.CreatedAtUtc)));
        Assert.Equal(4, restored.Revision);
        Assert.Equal(original.LastChangedAtUtc, restored.LastChangedAtUtc);
        Assert.Equal(original.ExpiresAtUtc, restored.ExpiresAtUtc);
        Assert.Equal(1, restored.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(DialogMutationResult.StaleOperation, restored.TryApplyContext(OWNER, version!, 1, CREATED.AddMinutes(5)));
        Assert.Equal(DialogMutationResult.Success, restored.TryCaptureVersion(OWNER, CREATED.AddMinutes(5), out DialogStateVersion? fresh));
        Assert.Equal(DialogMutationResult.Success, restored.TryApplyContext(OWNER, fresh!, 1, CREATED.AddMinutes(5)));
        Assert.Equal(5, restored.Revision);
        Assert.Equal(2, restored.ContextStates.Count);
    }

    /// <summary>Повреждённый снимок отклоняется до выдачи агрегата; restore не исправляет порядок и terminal prefix.</summary>
    [Theory]
    [InlineData("gap")]
    [InlineData("duplicate")]
    [InlineData("status")]
    [InlineData("finish")]
    [InlineData("time")]
    [InlineData("offset")]
    [InlineData("revision")]
    [InlineData("context-gap")]
    [InlineData("unfinished-prefix")]
    [InlineData("late-finish")]
    [InlineData("negative-prefix")]
    [InlineData("rollback-prefix")]
    public void InvalidSnapshotIsRejected(string corruption)
    {
        Guid id = Guid.NewGuid();
        List<DialogTurnSnapshot> turns = [new(id, 1, CREATED, DialogTurnStatus.Completed, CREATED.AddMinutes(2))];
        List<DialogContextSnapshot> contexts = [new(1, 1, CREATED.AddMinutes(3))];
        long revision = 3;
        if (corruption == "gap") { turns[0] = turns[0] with { Sequence = 2 }; }
        if (corruption == "duplicate") { turns.Add(turns[0] with { Sequence = 2 }); }
        if (corruption == "status") { turns[0] = turns[0] with { Status = (DialogTurnStatus)99 }; }
        if (corruption == "finish") { turns[0] = turns[0] with { FinishedAtUtc = null }; }
        if (corruption == "time") { turns[0] = turns[0] with { StartedAtUtc = CREATED.AddTicks(-1) }; }
        if (corruption == "offset") { turns[0] = turns[0] with { StartedAtUtc = CREATED.ToOffset(TimeSpan.FromHours(1)) }; }
        if (corruption == "revision") { revision = 1; }
        if (corruption == "context-gap") { contexts[0] = contexts[0] with { Version = 2 }; }
        if (corruption == "unfinished-prefix") { turns[0] = turns[0] with { Status = DialogTurnStatus.InProgress, FinishedAtUtc = null }; }
        if (corruption == "late-finish") { contexts[0] = contexts[0] with { CreatedAtUtc = CREATED.AddMinutes(1) }; }
        if (corruption == "negative-prefix") { contexts[0] = contexts[0] with { ThroughTurnSequence = -1 }; }
        if (corruption == "rollback-prefix") { contexts.Add(new(2, 0, CREATED.AddMinutes(4))); revision = 4; }
        Assert.ThrowsAny<ArgumentException>(() => Dialog.Restore(DialogId.From(Guid.NewGuid()), OWNER, CREATED,
            CREATED.AddDays(1), revision, CREATED.AddMinutes(4), turns, contexts));
    }

    /// <summary>Промежуточные данные меняют версию только активного обращения; срок не продлевается.</summary>
    [Fact]
    public void AppendProtectsVersionTurnAndExpiry()
    {
        Dialog dialog = Dialog.Create(DialogId.From(Guid.NewGuid()), OWNER, CREATED, CREATED.AddDays(1));
        Guid turn = Guid.NewGuid();
        dialog.TryBeginTurn(OWNER, turn, CREATED, out DialogStateVersion? version);
        Assert.Equal(DialogMutationResult.TurnNotFound, dialog.TryAppendTurn(OWNER, version!, Guid.NewGuid(), CREATED));
        Assert.Equal(1, dialog.Revision);
        Assert.Equal(DialogMutationResult.Expired, dialog.TryAppendTurn(OWNER, version!, turn, CREATED.AddDays(1)));
        Assert.Equal(DialogMutationResult.Success, dialog.TryAppendTurn(OWNER, version!, turn, CREATED));
        Assert.Equal(DialogMutationResult.StaleOperation, dialog.TryAppendTurn(OWNER, version!, turn, CREATED));
        dialog.TryCaptureVersion(OWNER, CREATED, out version);
        dialog.TryFinishTurn(OWNER, version!, turn, DialogTurnStatus.Failed, CREATED);
        dialog.TryCaptureVersion(OWNER, CREATED, out version);
        Assert.Equal(DialogMutationResult.TurnAlreadyFinished, dialog.TryAppendTurn(OWNER, version!, turn, CREATED));
        Assert.Equal(3, dialog.Revision);
    }
}

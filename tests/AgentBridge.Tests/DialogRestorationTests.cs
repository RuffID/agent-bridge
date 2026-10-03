using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Валидирующее production-восстановление и продолжение через защищённый Domain API.</summary>
public class DialogRestorationTests
{
    private static readonly DateTimeOffset CREATED = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DialogOwnerId OWNER = DialogOwnerId.From("owner");

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

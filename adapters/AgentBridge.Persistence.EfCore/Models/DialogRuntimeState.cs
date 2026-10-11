using System.Text.Json;
using System.Text;
using AgentBridge.Application.Models;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Versioned bounded root lifecycle; не копия истории и не app memory lock.</summary>
internal class DialogRuntimeState
{
    public int FormatVersion { get; set; } = 1;
    public long Epoch { get; set; }
    public long RecoveryRevision { get; set; }
    public Guid? TurnId { get; set; }
    public DialogTurnStatus? Terminal { get; set; }
    public bool Finalized { get; set; } = true;
    public Guid LeaseId { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }
    public List<Call> Calls { get; set; } = [];

    public DialogReadiness Readiness => LeaseId != Guid.Empty ? DialogReadiness.Active :
        Finalized && Calls.All(call => call.OutputItemIndex is not null || call.ResolutionJson is not null)
            ? DialogReadiness.Ready : DialogReadiness.RecoveryRequired;

    public static DialogRuntimeState Read(DialogRecord root)
    {
        if (root.RuntimeJson is null || Encoding.UTF8.GetByteCount(root.RuntimeJson) > 2097152)
            throw new InvalidOperationException("Lifecycle byte budget exceeded.");

        DialogRuntimeState value = JsonSerializer.Deserialize<DialogRuntimeState>(root.RuntimeJson ??
            throw new InvalidOperationException("Registered root не содержит lifecycle.")) ??
            throw new InvalidOperationException("Отсутствует lifecycle.");
        if (value.FormatVersion != 1 || value.Calls.Count > 1024 || value.Epoch < 0 || value.RecoveryRevision < 0 ||
            value.Calls.Select(call => (call.TurnId, call.StepId, call.OutputIndex)).Distinct().Count() != value.Calls.Count)
            throw new InvalidOperationException("Неизвестный либо повреждённый lifecycle.");

        return value;
    }

    public void Save(DialogRecord root)
    {
        string json = JsonSerializer.Serialize(this);
        if (Encoding.UTF8.GetByteCount(json) > 2097152)
            throw new InvalidOperationException("Lifecycle byte budget exceeded.");

        root.RuntimeJson = json;
    }
    public DialogRunLease? Lease(DialogRecord root) => LeaseId == Guid.Empty ? null :
        new(root.IncarnationId, TurnId!.Value, Epoch, LeaseId, DeadlineUtc!.Value);
    public bool Matches(DialogRecord root, DialogRunLease lease, DateTimeOffset nowUtc) =>
        lease.IncarnationId == root.IncarnationId && lease.Epoch == Epoch && lease.LeaseId == LeaseId &&
        lease.TurnId == TurnId && LeaseId != Guid.Empty && DeadlineUtc > nowUtc;

    public DialogContinuationState ToState(DialogRecord root) => new(
        new(AgentBridge.Domain.Dialogs.DialogId.From(root.Id), root.IncarnationId, root.Revision),
        Readiness, TurnId, Terminal, Finalized, Epoch, RecoveryRevision, Lease(root),
        Calls.Where(call => call.OutputItemIndex is null && call.ResolutionJson is null).Select(call => call.Position(Finalized)));

    internal class Call
    {
        public Guid TurnId { get; set; }
        public Guid StepId { get; set; }
        public int OutputIndex { get; set; }
        public int ItemIndex { get; set; }
        public string CallId { get; set; } = string.Empty;
        public ToolAttemptState State { get; set; } = ToolAttemptState.NotStarted;
        public int? OutputItemIndex { get; set; }
        public string? ResolutionJson { get; set; }
        public Guid? RecoveryId { get; set; }
        public long ResolutionRevision { get; set; }
        public DialogRecoveryKind? ResolutionKind { get; set; }
        public string? EvidenceReference { get; set; }
        public DialogPendingCall Position(bool finalized = false) => new(TurnId, StepId, OutputIndex, ItemIndex, CallId,
            finalized && State == ToolAttemptState.Started ? ToolAttemptState.Unknown : State);
    }
}

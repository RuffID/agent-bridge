using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.BinaryConsumer;

/// <summary>Compile-only проверки новых public DLL ports; методы не исполняются.</summary>
public static class AssistantDialogCapabilities
{
    /// <summary>Явная регистрация передаёт immutable scope/profile/policy без claims или Ledger dependencies.</summary>
    public static Task<ServiceResult<DialogCatalogState>> RegisterAsync(IDialogCatalogCreator creator,
        DialogCatalogCreate request, CancellationToken cancellationToken) => creator.CreateAsync(request, cancellationToken);

    /// <summary>Full-profile history gate и authoritative recovery lookup используют фактические сигнатуры DLL.</summary>
    public static async Task<ServiceResult<DialogRecoveryResult>> ReadRecoveryAsync(IDialogReader history,
        IDialogRecovery recovery, DialogCatalogAccess access, Guid recoveryId, CancellationToken cancellationToken)
    {
        ServiceResult<DialogSnapshot> snapshot = await history.ReadCatalogAsync(access, cancellationToken);
        if (!snapshot.Success) return ServiceResult<DialogRecoveryResult>.Fail(snapshot.Error!);

        return await recovery.ReadAsync(access.Access, recoveryId, cancellationToken);
    }

    /// <summary>Epoch-aware append требует accepted lease, затем terminal принимается через lifecycle.</summary>
    public static async Task<ServiceResult<DialogContinuationState>> SaveAndFinalizeAsync(IDialogTurnWriter writer,
        IDialogRunLifecycle lifecycle, DialogRunWriteAccess access, DialogWriteToken token, StoredModelStep step,
        DialogTurnStatus status, CancellationToken cancellationToken)
    {
        ServiceResult<DialogWriteToken> appended = await writer.AppendAsync(access, token, access.Lease.TurnId,
            step.Response.Output, [step], cancellationToken);
        if (!appended.Success) return ServiceResult<DialogContinuationState>.Fail(appended.Error!);

        return await lifecycle.FinalizeAsync(new(access.Access, appended.Data!, access.Lease, status), cancellationToken);
    }
}

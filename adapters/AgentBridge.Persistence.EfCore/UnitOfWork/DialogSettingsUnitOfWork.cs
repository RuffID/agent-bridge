using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class DialogSettingsUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard,
    SettingsRecordQueries settings, RecordStaging<DialogSettingsRecord> staging) : IDialogSettingsWriter
{
    /// <inheritdoc/>
    public Task<ServiceResult<DialogModelSelection>> SaveAsync(DialogAccess access, DialogWriteToken expected,
        long expectedVersion, ModelSettingsSnapshot selected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ServiceResult<ModelSettingsSnapshot> valid = ModelSelectionValidator.Validate(new([selected.Model]), selected.Model.Id,
            selected.ReasoningEffort, selected.TokenThreshold, selected.InputTokenReserve);
        if (!valid.Success) return Task.FromResult(ServiceResult<DialogModelSelection>.Fail(valid.Error!));
        return scope.ExecuteAsync<DialogModelSelection>(async ct =>
        {
            ServiceResult<DialogRecord> root = await guard.LoadAsync(access, expected, ct);
            if (!root.Success) return ServiceResult<DialogModelSelection>.Fail(root.Error!);
            DialogSettingsRecord? record = await settings.FindAsync(access.DialogId.Value, ct, true);
            if ((record?.Version ?? 0) != expectedVersion)
                return ServiceResult<DialogModelSelection>.Fail(new(ServiceErrorType.Conflict, "Версия настроек устарела."));
            bool create = record is null;
            record ??= new() { Id = access.DialogId.Value };
            record.Version = checked(expectedVersion + 1);
            record.Model = selected.Model.Id;
            record.Effort = selected.ReasoningEffort;
            if (create) staging.StageCreate(record); else staging.StageUpdate(record);
            return ServiceResult<DialogModelSelection>.Ok(record.ToSelection());
        }, cancellationToken, writingSettings: true);
    }
}

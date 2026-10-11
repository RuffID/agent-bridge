using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class DialogCatalogCreationUnitOfWork(UnitOfWorkScope scope, DialogRecordQueries roots,
    DialogCatalogQueries queries, RecordStaging<DialogRecord> dialogs, DialogCatalogStaging catalog) : IDialogCatalogCreator
{
    /// <inheritdoc/>
    public Task<ServiceResult<DialogCatalogState>> CreateAsync(DialogCatalogCreate request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ServiceError? invalid = catalog.Validate(request.Policy);
        if (invalid is not null) return Task.FromResult(ServiceResult<DialogCatalogState>.Fail(invalid));
        if (!ValidScope(request.Scope) || request.Profile.Key.Length > 128 ||
            request.CreatedAtUtc.Offset != TimeSpan.Zero || request.ExpiresAtUtc?.Offset != null && request.ExpiresAtUtc.Value.Offset != TimeSpan.Zero ||
            request.ExpiresAtUtc <= request.CreatedAtUtc ||
            Encoding.UTF8.GetByteCount(request.Profile.Data.GetRawText()) > 8192)
            return Task.FromResult(ServiceResult<DialogCatalogState>.Fail(new(ServiceErrorType.Validation, "catalog_profile_or_scope_budget")));
        Dialog dialog = Dialog.Create(request.DialogId, request.Scope.OwnerId, request.CreatedAtUtc, request.ExpiresAtUtc);
        return scope.ExecuteAsync<DialogCatalogState>(async ct =>
        {
            if (await roots.FindAsync(dialog.Id.Value, ct) is not null || await queries.FindAsync(dialog.Id.Value, ct) is not null)
                return ServiceResult<DialogCatalogState>.Fail(new(ServiceErrorType.Conflict, "dialog_id_already_used"));
            DialogRecord root = new()
            {
                Id = dialog.Id.Value, IncarnationId = Guid.NewGuid(), OwnerId = dialog.OwnerId.Value,
                CreatedAtUtc = dialog.CreatedAtUtc, ExpiresAtUtc = dialog.ExpiresAtUtc ?? DateTimeOffset.MaxValue,
                LastChangedAtUtc = dialog.CreatedAtUtc, CatalogRegistered = true, RuntimeJson = JsonSerializer.Serialize(new DialogRuntimeState())
            };
            DialogCatalogRecord metadata = await catalog.CreateAsync(root, request, ct);
            dialogs.StageCreate(root);
            return ServiceResult<DialogCatalogState>.Ok(DialogCatalogStaging.State(metadata, request.CreatedAtUtc));
        }, cancellationToken, creatingDialog: true);
    }

    internal static bool ValidScope(DialogCatalogScope scope) => scope is not null && scope.OwnerId is not null &&
        scope.OwnerId.Value.Length <= 512 && !string.IsNullOrWhiteSpace(scope.SiteId) && scope.SiteId.Length <= 128 &&
        !string.IsNullOrWhiteSpace(scope.AgentId) && scope.AgentId.Length <= 128;
}

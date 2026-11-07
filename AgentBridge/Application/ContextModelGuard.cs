using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.Application;

/// <summary>Проверяет смену модели по provenance активного окна и непокрытой истории, не очищая canonical данные.</summary>
public class ContextModelGuard(IContextContentInspector inspector, IEnumerable<IContextModelCompatibility> validators)
{
    private readonly IContextModelCompatibility? _validator = validators.SingleOrDefault();

    /// <summary>Для opaque/неопределённого содержимого другой или неизвестной selected модели требует подтверждение приложения.</summary>
    public async Task<ServiceResult> CheckAsync(ApplicationCallContext call, DialogSnapshot dialog,
        ModelSettingsSnapshot target, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(ContextModelSource Source, ModelConnectorProvenance? Provenance)> sources = Sources(dialog);
        foreach ((ContextModelSource source, ModelConnectorProvenance? _) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.Items.Count == 0 || source.SelectedModel == target.Model.Id)
            {
                continue;
            }

            if (!inspector.HasOpaqueContent(source.Items, cancellationToken))
            {
                continue;
            }

            if (_validator is null)
            {
                return ServiceResult.Fail(new(ServiceErrorType.Unsupported,
                    "Совместимость сохранённого непрозрачного контекста с выбранной моделью не подтверждена."));
            }

            ServiceResult compatible = await _validator.CheckAsync(call, source, target, cancellationToken);
            if (!compatible.Success)
            {
                return compatible;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return ServiceResult.Ok();
    }

    /// <summary>Проверяет каждый opaque источник независимо от model ID и model-compatibility приложения.</summary>
    public ServiceResult CheckConnector(DialogSnapshot dialog, ModelConnectorProvenance target,
        CancellationToken cancellationToken = default)
    {
        foreach ((ContextModelSource source, ModelConnectorProvenance? provenance) in Sources(dialog))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!inspector.HasOpaqueContent(source.Items, cancellationToken))
            {
                continue;
            }

            ServiceError? error = target.ValidateSource(provenance);
            if (error is not null)
            {
                return ServiceResult.Fail(error);
            }
        }

        return ServiceResult.Ok();
    }

    /// <summary>Новые opaque вклады без сохранённого происхождения не получают доверие от соседней истории.</summary>
    public ServiceResult CheckUnboundInput(IEnumerable<CanonicalModelItem> items, CancellationToken cancellationToken = default)
    {
        if (inspector.HasOpaqueContent(items, cancellationToken))
        {
            return ServiceResult.Fail(new(ServiceErrorType.Unsupported, "Происхождение нового непрозрачного input не подтверждено."));
        }

        return ServiceResult.Ok();
    }

    /// <summary>Разделяет occurrences outputs и residual input/tools без глобальной дедупликации.</summary>
    private static IReadOnlyList<(ContextModelSource Source, ModelConnectorProvenance? Provenance)> Sources(DialogSnapshot dialog)
    {
        List<(ContextModelSource Source, ModelConnectorProvenance? Provenance)> sources = [];
        if (dialog.EffectiveContext is StoredDialogContext context)
        {
            sources.Add((new(context.SelectedModel, ServerModel(context.Compaction), context.Items),
                context.Compaction.ConnectorProvenance));
        }

        long through = dialog.EffectiveContext?.ThroughTurnSequence ?? 0;
        foreach (StoredDialogTurn turn in dialog.EffectiveTurns.Where(turn => turn.Sequence > through))
        {
            // Удаляется ровно по одному последнему occurrence каждого сохранённого output, без дедупликации истории.
            List<CanonicalModelItem> remaining = [.. turn.Items];
            foreach (StoredModelStep step in turn.ModelSteps.Reverse())
            {
                foreach (CanonicalModelItem item in step.Response.Output.Reverse())
                {
                    int index = remaining.FindLastIndex(candidate => JsonElement.DeepEquals(candidate.Content, item.Content));
                    if (index >= 0)
                    {
                        remaining.RemoveAt(index);
                    }
                }
            }

            foreach (StoredModelStep step in turn.ModelSteps)
            {
                sources.Add((new(turn.Settings?.Model, ServerModel(step.Response), step.Response.Output),
                    step.Response.ConnectorProvenance));
            }

            sources.Add((new(turn.Settings?.Model, null, remaining), null));
        }

        return sources;
    }

    /// <summary>Возвращает только отдельно объявленное server model, без envelope/headers/usage.</summary>
    internal static string? ServerModel(ModelResponse response) => response.Envelope?.Content is JsonElement envelope
        && envelope.TryGetProperty("model", out JsonElement model) && model.ValueKind == JsonValueKind.String
        ? model.GetString() : null;
}

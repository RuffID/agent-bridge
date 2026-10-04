using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Подготавливает добавление канонических items и полных model reports через base CRUD внутри turn UoW.</summary>
public class TurnContentStaging(ItemRecordQueries items, ModelStepRecordQueries steps,
    RecordStaging<CanonicalItemRecord> itemStaging, RecordStaging<ModelStepRecord> stepStaging)
{
    /// <summary>Проверяет локальные step ID до staging; возвращает отказ либо добавленный размер через подготовленный пакет.</summary>
    public async Task<ServiceResult<PreparedTurnContent>> PrepareAsync(Guid dialogId, Guid turnId,
        IReadOnlyList<CanonicalModelItem> newItems, IReadOnlyList<StoredModelStep> newSteps, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newItems);
        ArgumentNullException.ThrowIfNull(newSteps);
        List<CanonicalItemRecord> existingItems = await items.ReadTurnAsync(dialogId, turnId, cancellationToken);
        List<ModelStepRecord> existingSteps = await steps.ReadTurnAsync(dialogId, turnId, cancellationToken);
        if (existingItems.Where((item, index) => item.Sequence != index + 1L).Any() ||
            existingSteps.Where((step, index) => step.Sequence != index + 1L).Any())
        {
            throw new InvalidOperationException("Повреждён порядок содержимого обращения.");
        }
        HashSet<Guid> stepIds = existingSteps.Select(step => step.Id).ToHashSet();
        List<CanonicalItemRecord> preparedItems = [];
        List<ModelStepRecord> preparedSteps = [];
        long size = 0;
        foreach (CanonicalModelItem item in newItems)
        {
            ArgumentNullException.ThrowIfNull(item);
            string json = item.Content.GetRawText();
            preparedItems.Add(new CanonicalItemRecord
            {
                DialogId = dialogId, TurnId = turnId,
                Sequence = checked(existingItems.Count + preparedItems.Count + 1L), ContentJson = json
            });
            size = checked(size + StoredContentSize.Of(json));
        }
        foreach (StoredModelStep step in newSteps)
        {
            ArgumentNullException.ThrowIfNull(step);
            if (step.ToolAttempts.Count != 0)
                return ServiceResult<PreparedTurnContent>.Fail(new(ServiceErrorType.Validation,
                    "Журнал изменяется только через специализированный сценарий попыток."));
            if (!stepIds.Add(step.StepId))
            {
                return ServiceResult<PreparedTurnContent>.Fail(new ServiceError(ServiceErrorType.Conflict, "Шаг модели уже сохранён в обращении."));
            }
            ModelResponseRecord response = ModelResponseRecord.FromModelResponse(step.Response);
            preparedSteps.Add(new ModelStepRecord
            {
                DialogId = dialogId, TurnId = turnId, Id = step.StepId,
                Sequence = checked(existingSteps.Count + preparedSteps.Count + 1L), Response = response
            });
            size = checked(size + StoredContentSize.Of(response));
        }
        return ServiceResult<PreparedTurnContent>.Ok(new PreparedTurnContent(preparedItems, preparedSteps, size));
    }

    /// <summary>Ставит уже подготовленный пакет без самостоятельного save.</summary>
    public void Stage(PreparedTurnContent content)
    {
        itemStaging.StageCreateRange(content.Items);
        stepStaging.StageCreateRange(content.Steps);
    }
}

namespace AgentBridge.Application.Models;

/// <summary>Отдельный сохранённый шаг модели с полным lifecycle-отчётом, envelope и продолжением.</summary>
public class StoredModelStep
{
    /// <summary>Связывает результат конкретного шага с обращением через коллекцию StoredDialogTurn.ModelSteps.</summary>
    public StoredModelStep(Guid stepId, ModelResponse response)
    {
        if (stepId == Guid.Empty)
        {
            throw new ArgumentException("Идентичность шага обязательна.", nameof(stepId));
        }
        ArgumentNullException.ThrowIfNull(response);
        StepId = stepId;
        Response = response;
    }

    /// <summary>Идентичность одного шага модели внутри обращения.</summary>
    public Guid StepId { get; }
    /// <summary>Полный результат шага; envelope не является элементом следующего input.</summary>
    public ModelResponse Response { get; }
}

using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Отдельный сохранённый шаг модели с полным lifecycle-отчётом, envelope и продолжением.</summary>
public class StoredModelStep
{
    /// <summary>Связывает результат конкретного шага с обращением через коллекцию StoredDialogTurn.ModelSteps.</summary>
    public StoredModelStep(Guid stepId, ModelResponse response, IEnumerable<StoredToolAttempt>? toolAttempts = null)
    {
        if (stepId == Guid.Empty)
        {
            throw new ArgumentException("Идентичность шага обязательна.", nameof(stepId));
        }
        ArgumentNullException.ThrowIfNull(response);
        StepId = stepId;
        Response = response;
        ToolAttempts = toolAttempts is null ? Array.Empty<StoredToolAttempt>() : ContractSnapshot.Copy(toolAttempts);
        if (ToolAttempts.Select(attempt => attempt.OutputIndex).Distinct().Count() != ToolAttempts.Count)
            throw new ArgumentException("Позиции попыток не должны повторяться.", nameof(toolAttempts));
        foreach (StoredToolAttempt attempt in ToolAttempts)
            if (attempt.OutputIndex >= response.Output.Count ||
                !response.Output[attempt.OutputIndex].Content.TryGetProperty("type", out JsonElement type) ||
                type.ValueKind != JsonValueKind.String || type.GetString() != "function_call")
                throw new ArgumentException("Попытка должна ссылаться на исходный function_call.", nameof(toolAttempts));
    }

    /// <summary>Идентичность одного шага модели внутри обращения.</summary>
    public Guid StepId { get; }
    /// <summary>Полный результат шага; envelope не является элементом следующего input.</summary>
    public ModelResponse Response { get; }
    /// <summary>Явные durable попытки; пустой legacy журнал не разрешает replay существующего turn.</summary>
    public IReadOnlyList<StoredToolAttempt> ToolAttempts { get; }
}

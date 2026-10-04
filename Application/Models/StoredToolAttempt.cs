namespace AgentBridge.Application.Models;

/// <summary>Явный журнал внутри parent-aware StoredModelStep; не является canonical элементом или envelope.</summary>
public class StoredToolAttempt
{
    /// <summary>Фиксирует исходную позицию output, выбранного агента и подтверждённость состояния.</summary>
    public StoredToolAttempt(int outputIndex, string agentId, ToolAttemptState state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outputIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        OutputIndex = outputIndex;
        AgentId = agentId;
        State = state;
    }

    /// <summary>Позиция function_call во всём исходном output шага.</summary>
    public int OutputIndex { get; }
    /// <summary>Агент попытки; не определяет владение всем диалогом.</summary>
    public string AgentId { get; }
    /// <summary>Состояние durable попытки.</summary>
    public ToolAttemptState State { get; }
}

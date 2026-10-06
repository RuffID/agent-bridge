namespace AgentBridge.Application.Models;

/// <summary>Идентичность попытки, пригодная для durable журнала этапа20; call_id не является её уникальным ключом.</summary>
public class ToolExecutionIdentity
{
    internal ToolExecutionIdentity(ApplicationCallContext call, Guid incarnationId, Guid stepId, int outputIndex)
    {
        Call = call;
        IncarnationId = incarnationId;
        StepId = stepId;
        OutputIndex = outputIndex;
    }

    /// <summary>Фиксированные owner/dialog/turn/agent.</summary>
    public ApplicationCallContext Call { get; }
    /// <summary>Идентичность жизни диалога из исходного storage token.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Идентичность сохранённого шага модели.</summary>
    public Guid StepId { get; }
    /// <summary>Исходная позиция function_call во всём output, включая opaque/unknown items.</summary>
    public int OutputIndex { get; }
}

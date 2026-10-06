namespace AgentBridge.Application.Models;

/// <summary>Явный input одного нового turn; права на агента и provider selection принадлежат приложению.</summary>
public class AgentRunRequest
{
    /// <summary>Копирует input/selection; model/effort null используют saved dialog selection, затем defaults reader приложения.</summary>
    public AgentRunRequest(ApplicationCallContext call, IEnumerable<CanonicalModelItem> input,
        IEnumerable<string> selectedToolNames, ToolExecutionLimits toolLimits, string? model = null,
        string? effort = null, string? instructions = null, ModelRequestParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(toolLimits);
        Call = call;
        Input = ContractSnapshot.Copy(input);
        SelectedToolNames = ContractSnapshot.Copy(selectedToolNames);
        ToolLimits = toolLimits;
        Model = model;
        Effort = effort;
        Instructions = instructions;
        Parameters = parameters;
    }

    /// <summary>Принадлежность нового обращения; существующий TurnId не переисполняется.</summary>
    public ApplicationCallContext Call { get; }
    /// <summary>Только новое ещё не сохранённое содержимое.</summary>
    public IReadOnlyList<CanonicalModelItem> Input { get; }
    /// <summary>Exact names приложения; не являются авторизацией handler.</summary>
    public IReadOnlyList<string> SelectedToolNames { get; }
    /// <summary>Ограничения инструментов; MaxSteps дополнительно ограничен AgentOptions.</summary>
    public ToolExecutionLimits ToolLimits { get; }
    /// <summary>Явная модель либо выбор конфигурации.</summary>
    public string? Model { get; }
    /// <summary>Явное усилие либо выбор конфигурации.</summary>
    public string? Effort { get; }
    /// <summary>Инструкции обращения; обязательны в режиме PerRequest, в Configuration null использует AgentOptions.</summary>
    public string? Instructions { get; }
    /// <summary>Canonical generation controls без server continuation.</summary>
    public ModelRequestParameters? Parameters { get; }
}

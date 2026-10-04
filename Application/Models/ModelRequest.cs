namespace AgentBridge.Application.Models;

/// <summary>Снимок подготовленного входа модели для генерации, compact и подсчёта полного бюджета.</summary>
public class ModelRequest
{
    /// <summary>Копирует вход и инструменты; выбор допустимой модели и effort проверяется будущим сценарием.</summary>
    public ModelRequest(string model, string? reasoningEffort, string instructions,
        IEnumerable<CanonicalModelItem> input, IEnumerable<ModelToolDefinition> tools, ModelContinuation? continuation = null,
        ModelRequestParameters? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(instructions);
        if (reasoningEffort is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reasoningEffort);
        }
        Model = model;
        ReasoningEffort = reasoningEffort;
        Instructions = instructions;
        Input = ContractSnapshot.Copy(input);
        Tools = ContractSnapshot.Copy(tools);
        Continuation = continuation;
        Parameters = parameters;
    }

    /// <summary>Зафиксированная выбранная модель.</summary>
    public string Model { get; }
    /// <summary>Выбранное усилие либо отсутствие явного значения.</summary>
    public string? ReasoningEffort { get; }
    /// <summary>Полные инструкции агента.</summary>
    public string Instructions { get; }
    /// <summary>Упорядоченные сообщения, инструменты и opaque-элементы подготовленного входа.</summary>
    public IReadOnlyList<CanonicalModelItem> Input { get; }
    /// <summary>Полные описания инструментов, учитываемые при подсчёте.</summary>
    public IReadOnlyList<ModelToolDefinition> Tools { get; }
    /// <summary>Зафиксированные метаданные продолжения только этого диалога и upstream-владения.</summary>
    public ModelContinuation? Continuation { get; }
    /// <summary>Независимые дополнительные контроли; неизвестную поддержку адаптер отклоняет явно.</summary>
    public ModelRequestParameters? Parameters { get; }
}

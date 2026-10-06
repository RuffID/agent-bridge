using AgentBridge.Application.Models;

namespace AgentBridge.Application.Ports;

/// <summary>Отдельный scope приложения на invocation; освобождается только после завершения handler.</summary>
public interface IToolHandlerScope : IAsyncDisposable
{
    /// <summary>Зафиксированное описание регистрации.</summary>
    ModelToolDefinition Definition { get; }
    /// <summary>Обязательный validator схемы и прав.</summary>
    IToolInvocationValidator Validator { get; }
    /// <summary>Отдельный scoped обработчик приложения.</summary>
    IToolHandler Handler { get; }
}

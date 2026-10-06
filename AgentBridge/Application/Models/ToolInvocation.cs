using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Нормализованный вызов инструмента с идентичностью связи результата и полными аргументами.</summary>
public class ToolInvocation
{
    /// <summary>Фиксирует параметры после протокольного разбора; разрешения и схема проверяются приложением.</summary>
    public ToolInvocation(ApplicationCallContext call, string callId, string name, JsonElement arguments)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Call = call;
        CallId = callId;
        Name = name;
        Arguments = ContractSnapshot.Json(arguments);
    }

    /// <summary>Идентичности пользователя и обращения для проверки доступа.</summary>
    public ApplicationCallContext Call { get; }
    /// <summary>Идентичность вызова, которую будущий mapping сохраняет в результате инструмента.</summary>
    public string CallId { get; }
    /// <summary>Имя зарегистрированного инструмента.</summary>
    public string Name { get; }
    /// <summary>Полные аргументы без потери дополнительных полей.</summary>
    public JsonElement Arguments { get; }
}

using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Неизменяемый JSON снимок usage или image результата; чувствительные данные не предназначены для логирования.</summary>
public class ModelAuxiliaryResult
{
    /// <summary>Отделяет результат от времени жизни transport document.</summary>
    public ModelAuxiliaryResult(JsonElement content) => Content = content.Clone();

    /// <summary>Пользовательский результат без transport error envelope.</summary>
    public JsonElement Content { get; }
}

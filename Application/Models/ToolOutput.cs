using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Полезные данные инструмента; ошибку возвращают через ServiceResult.Fail.</summary>
public class ToolOutput
{
    /// <summary>Фиксирует независимое JSON-значение, включая допустимый JSON null.</summary>
    public ToolOutput(JsonElement content) => Content = ContractSnapshot.Json(content);

    /// <summary>Полный результат приложения; protocol mapping выполняется позже.</summary>
    public JsonElement Content { get; }
}

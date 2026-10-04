using System.Text.Json;

namespace AgentBridge.CodexLb.Responses.Models;

/// <summary>Содержит подготовленный независимый JSON transport body, без публичного wire DTO в ядре.</summary>
internal class ResponseRequestBody(JsonElement content)
{
    /// <summary>Полный объект запроса.</summary>
    internal JsonElement Content { get; } = content;
}

using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.BinaryConsumer;

/// <inheritdoc/>
/// <remarks>Пример read-only инструмента; реальные данные и права предоставляет приложение.</remarks>
public class AccountSummaryTool(IAccountSummarySource source, AccountSummaryValidator validator) : IToolHandler
{
    /// <summary>Полная схема пустого объекта; этот exact name выбирается в примере run.</summary>
    public static ModelToolDefinition ToolDefinition { get; } = new("account_summary", "Прочитать разрешённую сводку пользователя.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object", properties = new { }, required = Array.Empty<string>(), additionalProperties = false
        }), strict: true);

    /// <inheritdoc/>
    public ModelToolDefinition Definition => ToolDefinition;

    /// <inheritdoc/>
    public async Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ServiceResult allowed = await validator.ValidateAsync(Definition, invocation, cancellationToken);
        if (!allowed.Success) return ServiceResult<ToolOutput>.Fail(allowed.Error!);
        return await source.ReadAuthorizedAsync(invocation.Call, cancellationToken);
    }
}

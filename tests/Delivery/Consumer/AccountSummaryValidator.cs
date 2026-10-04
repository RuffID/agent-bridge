using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.BinaryConsumer;

/// <inheritdoc/>
/// <remarks>Проверяет всю простую схему без параметров и передаёт текущую авторизацию приложению.</remarks>
public class AccountSummaryValidator(IAccountSummarySource source) : IToolInvocationValidator
{
    /// <inheritdoc/>
    public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (definition.Name != AccountSummaryTool.ToolDefinition.Name || invocation.Name != definition.Name ||
            invocation.Arguments.ValueKind != JsonValueKind.Object || invocation.Arguments.EnumerateObject().Any())
            return Task.FromResult(ServiceResult.Fail(new(ServiceErrorType.Validation, "Ожидается инструмент сводки без параметров.")));
        return source.AuthorizeAsync(invocation.Call, cancellationToken);
    }
}

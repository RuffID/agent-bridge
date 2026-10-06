using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Обязательная проверка полной схемы аргументов и текущих прав приложением до бизнес-действия.</summary>
public interface IToolInvocationValidator
{
    /// <summary>Проверяет схему, пользователя, агента и доступ к бизнес-объекту; не выполняет побочных действий.</summary>
    Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation,
        CancellationToken cancellationToken = default);
}

using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Обработчик зарегистрированного инструмента, принадлежащий подключающему приложению.</summary>
public interface IToolHandler
{
    /// <summary>Описание инструмента и полная схема его параметров.</summary>
    ModelToolDefinition Definition { get; }
    /// <summary>Проверяет имя, аргументы и права перед действием; не выполняет автоматический повтор неоднозначного сбоя.</summary>
    /// <remarks>Caller cancellation распространяется с исходным токеном; неожиданные исключения не маскируются успехом.
    /// Fail означает подтверждённый отказ; при неоднозначном исходе бросить исключение либо вернуть Timeout.
    /// Обработчик обязан соблюдать cancellation; внешние действия не входят в транзакцию AgentBridge.</remarks>
    Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default);
}

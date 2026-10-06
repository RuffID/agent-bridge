using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Необязательная в этапе19 граница durable начала попытки; интеграция и реализация относятся к этапу20.</summary>
/// <remarks>Исполняется после validator и до handler, пока handler scope жив. Отказ или исключение блокирует действие;
/// checkpoint обязан создавать отдельный scope/UoW на invocation и завершать короткую транзакцию до возврата;
/// экземпляр может вызываться параллельно и не должен держать общий DbContext. Null checkpoint не защищает restart.</remarks>
public interface IToolExecutionCheckpoint
{
    /// <summary>Фиксирует начало именно этой identity или запрещает recovery/repeat; не запускает бизнес-действие.</summary>
    Task<ServiceResult> BeforeExecuteAsync(ToolExecutionIdentity identity, ToolInvocation invocation,
        CancellationToken cancellationToken = default);
}

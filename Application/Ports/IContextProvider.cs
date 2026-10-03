using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Зарегистрированный приложением источник разрешённого бизнес-контекста.</summary>
public interface IContextProvider
{
    /// <summary>Проверяет доступ внутри поставщика и возвращает только разрешённые данные либо ожидаемый отказ.</summary>
    Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default);
}

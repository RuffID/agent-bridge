using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Подсчёт подготовленного запроса с явным различением подтверждённой и оценочной кодировок.</summary>
public interface IContextTokenCounter
{
    /// <summary>Без явно настроенного оценочного словаря неизвестная кодировка — Unsupported; приблизительность обязательна в результате.</summary>
    Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default);
}

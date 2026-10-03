using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Подсчёт всего подготовленного запроса по проверенной кодировке модели.</summary>
public interface IContextTokenCounter
{
    /// <summary>Разделяет известные токены и полный оценочный бюджет; неизвестная кодировка — Unsupported.</summary>
    Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default);
}

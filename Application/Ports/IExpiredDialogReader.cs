using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Ограниченная выборка кандидатов системной очистки; не открывает изменяющий UoW.</summary>
public interface IExpiredDialogReader
{
    /// <summary>Читает не более положительного limit кандидатов с ExpiresAtUtc &lt;= nowUtc; пустая выборка — успех.</summary>
    Task<ServiceResult<IReadOnlyList<DialogWriteToken>>> ReadAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken = default);
}

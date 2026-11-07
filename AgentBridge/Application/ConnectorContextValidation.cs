using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.Application;

/// <summary>Проверяет локальные metadata полного запроса до создания HTTP-клиента.</summary>
public static class ConnectorContextValidation
{
    /// <summary>Opaque input требует происхождение; переданный marker всегда проверяется даже для текста.</summary>
    public static ServiceError? Check(ModelRequest request, ModelConnectorProvenance target,
        IContextContentInspector inspector, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ConnectorProvenance is null && !inspector.HasOpaqueContent(request.Input, cancellationToken))
        {
            return null;
        }

        return target.ValidateSource(request.ConnectorProvenance);
    }
}

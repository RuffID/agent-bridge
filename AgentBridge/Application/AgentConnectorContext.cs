using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.Application;

/// <summary>Фиксирует локальное происхождение одного run и проверяет историю до разрешения подготовленного запроса.</summary>
internal class AgentConnectorContext(ContextModelGuard guard, IModelGateway gateway, ApplicationCallContext call, ModelAccess access)
{
    private readonly ModelConnectorProvenance? _provenance = (gateway as IModelContextGateway)?.GetContextProvenance(call, access);

    /// <summary>Проверяет каждый сохранённый источник, включая компактное окно без continuation.</summary>
    internal ServiceResult CheckStored(DialogSnapshot dialog, CancellationToken cancellationToken)
    {
        if (_provenance is null)
        {
            return ServiceResult.Ok();
        }

        return guard.CheckConnector(dialog, _provenance, cancellationToken);
    }

    /// <summary>Не позволяет неизвестным opaque provider/input получить доверие от соседнего окна.</summary>
    internal ServiceResult CheckNew(IEnumerable<CanonicalModelItem> items, CancellationToken cancellationToken)
    {
        if (_provenance is null)
        {
            return ServiceResult.Ok();
        }

        return guard.CheckUnboundInput(items, cancellationToken);
    }

    /// <summary>Отмечает подготовленный запрос только после проверки его источников.</summary>
    internal ModelRequest Prepare(ModelRequest request)
    {
        if (_provenance is null)
        {
            return request;
        }

        return request.WithConnectorProvenance(_provenance);
    }

    /// <summary>Сохраняет происхождение partial данных при неожиданном завершении gateway.</summary>
    internal ModelResponse Bind(ModelResponse response)
    {
        if (_provenance is null)
        {
            return response;
        }

        return response.WithConnectorProvenance(_provenance);
    }
}

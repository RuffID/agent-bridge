using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Приложение подтверждает совместимость opaque-состояния с новым выбором по доказуемому контракту.</summary>
public interface IContextModelCompatibility
{
    /// <summary>Получает отдельно selected/server provenance; отказ сохраняет историю и не разрешает отправку.</summary>
    Task<ServiceResult> CheckAsync(ApplicationCallContext call, ContextModelSource source,
        ModelSettingsSnapshot target, CancellationToken cancellationToken = default);
}

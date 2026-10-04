using AgentBridge.Application.Ports;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.BinaryConsumer;

/// <inheritdoc/>
/// <remarks>Пример адаптера к secret store приложения. Delegate не нормализует ключ и не скрывает отказ источника.</remarks>
public class IndividualKeySource(Func<DialogOwnerId, CancellationToken, Task<string?>> readKey) : IIndividualModelKeySource
{
    /// <inheritdoc/>
    public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(readKey);
        ct.ThrowIfCancellationRequested();
        return readKey(ownerId, ct); // Только null разрешает shared key; пустая строка не является отсутствием.
    }
}

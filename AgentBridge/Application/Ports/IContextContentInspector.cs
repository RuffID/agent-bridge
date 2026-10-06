using AgentBridge.Application.Models;

namespace AgentBridge.Application.Ports;

/// <summary>Классифицирует canonical содержимое независимо от model mapping или числа токенов.</summary>
public interface IContextContentInspector
{
    /// <summary>Возвращает неопределённость opaque/unknown полей без tokenizer, сети или удаления данных.</summary>
    bool HasOpaqueContent(IEnumerable<CanonicalModelItem> items, CancellationToken cancellationToken = default);
}

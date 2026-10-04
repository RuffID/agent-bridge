using AgentBridge.Application.Models;

namespace AgentBridge.Application.Ports;

/// <summary>Описания инструментов и создание независимого scope одного вызова; регистрация не даёт права исполнения.</summary>
public interface IToolRegistry
{
    /// <summary>Независимый упорядоченный список полных описаний.</summary>
    IReadOnlyList<ModelToolDefinition> Definitions { get; }
    /// <summary>Открывает новый scope handler/validator; null означает неизвестное exact имя.</summary>
    IToolHandlerScope? OpenScope(string name);
}

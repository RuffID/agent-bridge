using AgentBridge.Application.Models;

namespace AgentBridge.Application.Ports;

/// <summary>Полный reader с явной библиотечной политикой materialization для всего persistence provider generation.</summary>
/// <remarks>Методы унаследованы от IDialogReader. Возвращают полную историю либо semantic overflow без обрезки.
/// Регистрация защищает также queries внутренних runner/write/lifecycle/recovery/compact операций.</remarks>
public interface IBoundedDialogReader : IDialogReader
{
    /// <summary>Frozen явные пределы operation gate; token budget и retention не подменяют эту политику.</summary>
    DialogHistoryLoadLimits LoadLimits { get; }
}

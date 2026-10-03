namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Общий scoped gate для read/write ports; отклоняет параллельное использование и неизвестный исход cleanup.</summary>
public class PersistenceOperationGate
{
    private int _active;
    private bool _poisoned;

    /// <summary>Занимает контекст без очереди и повторов; вызывающий обязан освободить lease.</summary>
    public IDisposable Enter()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            throw new InvalidOperationException("Один persistence scope нельзя использовать параллельно или вложенно.");
        }
        if (_poisoned)
        {
            Volatile.Write(ref _active, 0);
            throw new InvalidOperationException("Исход предыдущей операции неизвестен; необходим новый persistence scope.");
        }
        return new Lease(this);
    }

    /// <summary>Запрещает дальнейшие операции этого scope после неизвестного исхода.</summary>
    public void Poison() => _poisoned = true;

    /// <summary>Освобождает занятость без управления контекстом или транзакцией.</summary>
    private class Lease(PersistenceOperationGate owner) : IDisposable
    {
        private PersistenceOperationGate? _owner = owner;
        /// <inheritdoc/>
        public void Dispose()
        {
            PersistenceOperationGate? current = Interlocked.Exchange(ref _owner, null);
            if (current is not null)
            {
                Volatile.Write(ref current._active, 0);
            }
        }
    }
}

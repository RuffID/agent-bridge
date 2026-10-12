using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Persistence.EfCore.Reading;

/// <summary>Scoped учёт полных строк и query pairs внутри общего read/write gate.</summary>
public class HistoryLoadBudget(DialogHistoryLoadLimits limits)
{
    private bool _active;
    private int _records;
    private int _queries;

    /// <summary>Пределы frozen provider generation; defaults отсутствуют.</summary>
    public DialogHistoryLoadLimits Limits { get; } = limits;

    /// <summary>Начинает новую библиотечную операцию; вложенные операции запрещены.</summary>
    public void Begin()
    {
        if (_active)
        {
            throw new InvalidOperationException("history_load_nested_operation");
        }

        _records = 0;
        _queries = 0;
        _active = true;
    }

    /// <summary>Завершает учёт независимо от успеха; gate отдельно сохраняет poisoning.</summary>
    public void End() => _active = false;

    /// <summary>Резервирует одну query pair до I/O и возвращает остаток полных строк.</summary>
    public int Query()
    {
        if (!_active)
        {
            throw new InvalidOperationException("history_load_operation_required");
        }

        if (_queries >= Limits.MaxQueries)
        {
            throw new DialogHistoryLoadLimitException();
        }

        _queries++;
        return Limits.MaxRecords - _records;
    }

    /// <summary>Принимает число уже ограниченных полных строк без возможности превышения общего предела.</summary>
    public void Accept(int count)
    {
        if (count < 0 || count > Limits.MaxRecords - _records)
        {
            throw new InvalidOperationException("history_load_record_contract");
        }

        _records += count;
    }
}

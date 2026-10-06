using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Immutable отчёт одного вызова очистки, включая частичные результаты до отмены или исключения.</summary>
public class ExpiredDialogCleanupResult
{
    internal ExpiredDialogCleanupResult(ExpiredDialogCleanupStatus status, int limit, bool candidatesRead,
        IEnumerable<ExpiredDialogDeletionResult> candidates, ServiceError? error)
    {
        Status = status;
        Limit = limit;
        CandidatesRead = candidatesRead;
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Error = error;
    }

    /// <summary>Итог пакета; при Interrupted вызывающий получает exception и читает LastResult.</summary>
    public ExpiredDialogCleanupStatus Status { get; }
    /// <summary>Явная максимальная длина единственной выборки этого вызова.</summary>
    public int Limit { get; }
    /// <summary>Получен успешный snapshot кандидатов; пустой snapshot отличается от отказа чтения.</summary>
    public bool CandidatesRead { get; }
    /// <summary>Кандидаты исходного пакета в исходном порядке и их индивидуальные исходы.</summary>
    public IReadOnlyList<ExpiredDialogDeletionResult> Candidates { get; }
    /// <summary>Число подтверждённых удалений; Unknown не учитывается.</summary>
    public int DeletedCount => Candidates.Count(candidate => candidate.Status == ExpiredDialogDeletionStatus.Deleted);
    /// <summary>Ожидаемый отказ чтения; отказы удаления находятся в индивидуальных результатах.</summary>
    public ServiceError? Error { get; }
}

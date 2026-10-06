using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Application;

/// <summary>Явная очистка одного ограниченного пакета в отдельных коротких scopes без scheduler или retry.</summary>
/// <remarks>Приложение авторизует вызов и задаёт расписание. Неожиданные исключения распространяются;
/// LastResult сохраняет частичный отчёт. Один экземпляр не допускает параллельных вызовов.</remarks>
public class ExpiredDialogCleanup(IServiceScopeFactory scopes, TimeProvider time)
{
    private int running;

    /// <summary>Последний завершённый или прерванный отчёт; raw exceptions и содержимое диалогов отсутствуют.</summary>
    public ExpiredDialogCleanupResult? LastResult { get; private set; }

    /// <summary>Читает один пакет и последовательно удаляет кандидатов; partial/canceled не являются успехом.</summary>
    public async Task<ExpiredDialogCleanupResult> CleanupAsync(int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            throw new InvalidOperationException("Очистка этого экземпляра уже выполняется.");
        List<ExpiredDialogDeletionResult> candidates = [];
        bool candidatesRead = false;
        ServiceError? readError = null;
        OperationCanceledException? operationCancellation = null;
        LastResult = null;
        try
        {
            CheckCancellation();
            await ExecuteAsync(async provider =>
            {
                IExpiredDialogReader reader = provider.GetRequiredService<IExpiredDialogReader>();
                CheckCancellation();
                ServiceResult<IReadOnlyList<DialogWriteToken>> read = await reader.ReadAsync(time.GetUtcNow(), limit, cancellationToken);
                if (!read.Success) { readError = read.Error!; return false; }
                DialogWriteToken[] snapshot = read.Data!.ToArray();
                if (snapshot.Length > limit || snapshot.Any(token => token is null) ||
                    snapshot.Select(token => token.DialogId.Value).Distinct().Count() != snapshot.Length)
                    throw new InvalidOperationException("Порт чтения нарушил границы пакета очистки.");
                candidates.AddRange(snapshot.Select(token => new ExpiredDialogDeletionResult(token, ExpiredDialogDeletionStatus.NotAttempted)));
                candidatesRead = true;
                return true;
            });
            if (readError is not null) return Report(ExpiredDialogCleanupStatus.Failed);
            foreach (int index in Enumerable.Range(0, candidates.Count))
            {
                CheckCancellation();
                await ExecuteAsync(async provider =>
                {
                    IExpiredDialogDeletion deletion = provider.GetRequiredService<IExpiredDialogDeletion>();
                    CheckCancellation();
                    DateTimeOffset nowUtc = time.GetUtcNow();
                    DialogWriteToken token = candidates[index].Token;
                    candidates[index] = new(token, ExpiredDialogDeletionStatus.Unknown);
                    ServiceResult result = await deletion.DeleteAsync(token, nowUtc, cancellationToken);
                    candidates[index] = result.Success
                        ? new(token, ExpiredDialogDeletionStatus.Deleted)
                        : new(token, ExpiredDialogDeletionStatus.Failed, result.Error!);
                    return result;
                });
            }
            CheckCancellation();
            return Report(candidates.Any(candidate => candidate.Status == ExpiredDialogDeletionStatus.Failed)
                ? ExpiredDialogCleanupStatus.Partial : ExpiredDialogCleanupStatus.Completed);
        }
        catch (OperationCanceledException error) when (ReferenceEquals(error, operationCancellation))
        {
            return Report(ExpiredDialogCleanupStatus.Canceled);
        }
        catch
        {
            Report(ExpiredDialogCleanupStatus.Interrupted);
            throw;
        }
        finally { Volatile.Write(ref running, 0); }

        ExpiredDialogCleanupResult Report(ExpiredDialogCleanupStatus status) =>
            LastResult = new(status, limit, candidatesRead, candidates, readError);

        void CheckCancellation()
        {
            if (!cancellationToken.IsCancellationRequested) return;
            operationCancellation = new OperationCanceledException(cancellationToken);
            throw operationCancellation;
        }

        Task<T> ExecuteAsync<T>(Func<IServiceProvider, Task<T>> operation) =>
            AgentRunScope.ExecuteAsync(scopes, async provider =>
            {
                try { return await operation(provider); }
                catch (OperationCanceledException error) when (cancellationToken.IsCancellationRequested)
                {
                    operationCancellation = error;
                    throw;
                }
            });
    }
}

using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Отчёт сценария с последним принятым состоянием, включая частичный успех нескольких проходов.</summary>
public class ContextCompactionResult
{
    /// <summary>Фиксирует состояние после последнего успешного save, не выдавая snapshot за разрешение записи.</summary>
    public ContextCompactionResult(ContextCompactionStatus status, DialogWriteToken token,
        StoredDialogContext? activeContext, ModelRequest preparedRequest, ContextTokenCount? count,
        int passes, ServiceError? error = null, ModelResponse? lastResponse = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(preparedRequest);
        if (!Enum.IsDefined(status)) { throw new ArgumentOutOfRangeException(nameof(status)); }
        ArgumentOutOfRangeException.ThrowIfNegative(passes);
        if ((status == ContextCompactionStatus.Failed) != (error is not null))
        {
            throw new ArgumentException("Ошибка обязательна только для неуспешного отчёта.", nameof(error));
        }
        Status = status;
        Token = token;
        ActiveContext = activeContext;
        PreparedRequest = preparedRequest;
        Count = count;
        Passes = passes;
        Error = error;
        LastResponse = lastResponse;
    }

    /// <summary>Причина остановки; UnknownBudget не разрешает генерацию.</summary>
    public ContextCompactionStatus Status { get; }
    /// <summary>Исходный token либо token последнего успешного save.</summary>
    public DialogWriteToken Token { get; }
    /// <summary>Последнее принятое окно; кандидат при отказе сюда не попадает.</summary>
    public StoredDialogContext? ActiveContext { get; }
    /// <summary>Полный запрос с принятым окном и неизменными transient вкладами.</summary>
    public ModelRequest PreparedRequest { get; }
    /// <summary>Оценка полного принятого запроса; null при ошибке исходного подсчёта.</summary>
    public ContextTokenCount? Count { get; }
    /// <summary>Число выполненных вызовов compact, включая последний неуспешный.</summary>
    public int Passes { get; }
    /// <summary>Ожидаемая ошибка без исходного payload; сохраняется исходный объект ошибки.</summary>
    public ServiceError? Error { get; }
    /// <summary>Последний lifecycle-отчёт compact, включая непринятый partial output; чувствительные данные.</summary>
    public ModelResponse? LastResponse { get; }
}

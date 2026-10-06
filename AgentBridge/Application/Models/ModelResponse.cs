using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Отчёт lifecycle, сохраняющий канонический output даже при неполном, ошибочном или отменённом исходе.</summary>
/// <remarks>ServiceResult.Ok с этим отчётом подтверждает получение отчёта, а не успешное завершение модели.</remarks>
public class ModelResponse
{
    private ModelResponse(ModelResponseStatus status, IEnumerable<CanonicalModelItem> output, ServiceError? error,
        CanonicalModelEnvelope? envelope, ModelContinuation? continuation)
    {
        Status = status;
        Output = ContractSnapshot.Copy(output);
        Error = error;
        Envelope = envelope;
        Continuation = continuation;
    }

    /// <summary>Авторитетный исход независимо от полученных потоковых фрагментов.</summary>
    public ModelResponseStatus Status { get; }
    /// <summary>Полный известный output в исходном порядке.</summary>
    public IReadOnlyList<CanonicalModelItem> Output { get; }
    /// <summary>Явный ожидаемый отказ только при Failed; не содержит неожиданные исключения.</summary>
    public ServiceError? Error { get; }
    /// <summary>Полный envelope, когда получен; null при обрыве до получения полного объекта.</summary>
    public CanonicalModelEnvelope? Envelope { get; }
    /// <summary>Снимок продолжения для того же диалога/upstream-владения.</summary>
    public ModelContinuation? Continuation { get; }
    /// <summary>Создаётся адаптером только после подтверждённого terminal completion.</summary>
    public static ModelResponse Completed(IEnumerable<CanonicalModelItem> output, CanonicalModelEnvelope? envelope = null,
        ModelContinuation? continuation = null) => new(ModelResponseStatus.Completed, output, null, envelope, continuation);
    /// <summary>Сохраняет неполный output, в том числе EOF без подтверждения завершения.</summary>
    public static ModelResponse Incomplete(IEnumerable<CanonicalModelItem> output, CanonicalModelEnvelope? envelope = null,
        ModelContinuation? continuation = null) => new(ModelResponseStatus.Incomplete, output, null, envelope, continuation);
    /// <summary>Сохраняет явный отказ и уже полученные данные в отчёте lifecycle.</summary>
    public static ModelResponse Failed(IEnumerable<CanonicalModelItem> output, ServiceError error,
        CanonicalModelEnvelope? envelope = null, ModelContinuation? continuation = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(ModelResponseStatus.Failed, output, error, envelope, continuation);
    }
    /// <summary>Сохраняет известный output при наблюдавшейся caller cancellation.</summary>
    public static ModelResponse Canceled(IEnumerable<CanonicalModelItem> output, CanonicalModelEnvelope? envelope = null,
        ModelContinuation? continuation = null) => new(ModelResponseStatus.Canceled, output, null, envelope, continuation);
}

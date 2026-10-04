using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application;

/// <summary>Готовит полный упорядоченный запрос из разрешённых вкладов и прочитанного состояния диалога.</summary>
/// <remarks>Не читает и не изменяет хранилище, не вызывает модель, инструменты, tokenizer или compact.
/// Snapshot не является разрешением записи или транзакционным снимком.</remarks>
public class ContextBuilder
{
    private readonly IReadOnlyList<IContextProvider> _providers;

    /// <summary>Фиксирует выбранных приложением провайдеров в порядке их последовательного вызова.</summary>
    /// <remarks>Приложение отвечает за права и срок scope; сборщик не владеет провайдерами.</remarks>
    public ContextBuilder(IEnumerable<IContextProvider> providers) => _providers = ContractSnapshot.Copy(providers);

    /// <summary>Проверяет доступность snapshot и собирает запрос без потери canonical данных и ролей.</summary>
    /// <param name="call">Actual идентичности текущего вызова, передаваемые провайдерам приложения.</param>
    /// <param name="dialog">Полное состояние, ранее полученное через защищённый IDialogReader.</param>
    /// <param name="newRequest">Инструкции, настройки, инструменты и только ещё не сохранённые Input этого шага.</param>
    /// <param name="nowUtc">Явное UTC проверки фиксированного срока; время системных часов не читается.</param>
    /// <param name="cancellationToken">Исходная отмена вызывающей стороны для всех провайдеров.</param>
    /// <returns>Полный ModelRequest либо безопасный отказ без частичного запроса.</returns>
    public async Task<ServiceResult<ModelRequest>> BuildAsync(ApplicationCallContext call, DialogSnapshot dialog,
        ModelRequest newRequest, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(newRequest);
        ContractSnapshot.Utc(nowUtc);
        cancellationToken.ThrowIfCancellationRequested();

        ServiceError? error = ValidateDialog(call, dialog, nowUtc, cancellationToken);
        if (error is not null)
        {
            return ServiceResult<ModelRequest>.Fail(error);
        }

        List<CanonicalModelItem> input = [];
        ContextRequest providerRequest = new(call, newRequest.Input);
        foreach (IContextProvider provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ServiceResult<ContextContribution> contribution = await provider.GetContextAsync(providerRequest, cancellationToken);
            if (!contribution.Success)
            {
                return ServiceResult<ModelRequest>.Fail(contribution.Error
                    ?? throw new InvalidOperationException("Отказ провайдера должен содержать ошибку."));
            }
            cancellationToken.ThrowIfCancellationRequested();
            input.AddRange((contribution.Data
                ?? throw new InvalidOperationException("Успех провайдера должен содержать вклад.")).Items);
        }

        long through = dialog.ActiveContext?.ThroughTurnSequence ?? 0;
        if (dialog.ActiveContext is not null)
        {
            input.AddRange(dialog.ActiveContext.Items);
        }
        foreach (StoredDialogTurn turn in dialog.Turns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (turn.Sequence > through)
            {
                input.AddRange(turn.Items);
            }
        }
        input.AddRange(newRequest.Input);

        error = ValidateFunctionPairs(input, cancellationToken);
        if (error is not null)
        {
            return ServiceResult<ModelRequest>.Fail(error);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return ServiceResult<ModelRequest>.Ok(new(newRequest.Model, newRequest.ReasoningEffort,
            newRequest.Instructions, input, newRequest.Tools, newRequest.Continuation, newRequest.Parameters));
    }

    /// <summary>Проверяет владельца, диалог, срок и непрерывность terminal prefix без изменения snapshot.</summary>
    private static ServiceError? ValidateDialog(ApplicationCallContext call, DialogSnapshot dialog, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!dialog.OwnerId.Equals(call.OwnerId))
        {
            return new(ServiceErrorType.Forbidden, "Диалог недоступен этому владельцу.");
        }
        if (!dialog.Token.DialogId.Equals(call.DialogId))
        {
            return new(ServiceErrorType.Conflict, "Снимок относится к другому диалогу.");
        }
        if (dialog.IsExpired(nowUtc))
        {
            return new(ServiceErrorType.Expired, "Срок доступности диалога истёк.");
        }

        long through = dialog.ActiveContext?.ThroughTurnSequence ?? 0;
        if (dialog.ExpiresAtUtc <= dialog.CreatedAtUtc || through > dialog.Turns.Count ||
            dialog.ActiveContext is not null && dialog.ActiveContext.Compaction.Status != ModelResponseStatus.Completed)
        {
            return new(ServiceErrorType.Conflict, "Состояние контекста диалога некорректно.");
        }
        long sequence = 0;
        HashSet<Guid> ids = [];
        foreach (StoredDialogTurn turn in dialog.Turns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (turn.Sequence != ++sequence || !ids.Add(turn.Id) ||
                turn.Sequence <= through && turn.Status == DialogTurnStatus.InProgress)
            {
                return new(ServiceErrorType.Conflict, "История не подтверждает непрерывное покрытие контекста.");
            }
        }
        return null;
    }

    /// <summary>Проверяет только известные внешние пары функций, не раскрывая opaque-содержимое.</summary>
    internal static ServiceError? ValidateFunctionPairs(IEnumerable<CanonicalModelItem> items, CancellationToken cancellationToken)
    {
        Dictionary<string, int> pending = new(StringComparer.Ordinal);
        foreach (CanonicalModelItem item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement content = item.Content;
            if (!content.TryGetProperty("type", out JsonElement type) || type.ValueKind != JsonValueKind.String ||
                type.GetString() is not ("function_call" or "function_call_output"))
            {
                continue;
            }
            if (!content.TryGetProperty("call_id", out JsonElement id) || id.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(id.GetString()))
            {
                return new(ServiceErrorType.Validation, "Известная пара функции не содержит корректную идентичность.");
            }
            string callId = id.GetString() ?? throw new InvalidOperationException("Проверенная идентичность функции обязательна.");
            pending.TryGetValue(callId, out int unmatched);
            if (type.GetString() == "function_call")
            {
                pending[callId] = checked(unmatched + 1);
            }
            else if (unmatched == 0)
            {
                return new(ServiceErrorType.Validation, "Результат функции не имеет предшествующего вызова.");
            }
            else if (unmatched == 1)
            {
                pending.Remove(callId);
            }
            else
            {
                pending[callId] = unmatched - 1;
            }
        }
        return pending.Count == 0 ? null : new(ServiceErrorType.Conflict, "Вызов функции ещё не имеет результата.");
    }
}

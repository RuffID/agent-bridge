using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application;

/// <summary>Проверяет сохранение исходных FIFO-occurrences известных пар в новом compact окне.</summary>
internal static class CompactFunctionPairInspector
{
    /// <summary>Не допускает подмену пары при сохранённом балансе; скрытые opaque вызовы не восстанавливает.</summary>
    internal static ServiceError? Validate(IReadOnlyList<CanonicalModelItem> original,
        IReadOnlyList<CanonicalModelItem> candidate, CancellationToken cancellationToken)
    {
        ServiceError? error = ContextBuilder.ValidateFunctionPairs(candidate, cancellationToken);
        if (error is not null) { return error; }
        List<(int CallIndex, CanonicalModelItem Call, CanonicalModelItem Output)> pairs = Pairs(original, cancellationToken);
        List<(int CallIndex, CanonicalModelItem Call, CanonicalModelItem Output)> retained = Pairs(candidate, cancellationToken);
        List<int> matchedIndices = [];
        int nextOutput = 0;
        List<(int CandidateCall, int OriginalCall)> matchedCalls = [];
        foreach ((int candidateCall, CanonicalModelItem call, CanonicalModelItem output) in retained)
        {
            bool matched = false;
            while (nextOutput < pairs.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (int callIndex, CanonicalModelItem sourceCall, CanonicalModelItem sourceOutput) = pairs[nextOutput++];
                if (JsonElement.DeepEquals(call.Content, sourceCall.Content)
                    && JsonElement.DeepEquals(output.Content, sourceOutput.Content))
                {
                    matchedCalls.Add((candidateCall, callIndex));
                    matchedIndices.Add(nextOutput - 1);
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                return new(ServiceErrorType.Rejected, "Compact не сохранил исходную связь вызова и результата функции.");
            }
        }
        // Первый и последний ordered matching должны совпасть: первый найденный источник не доказывает uniqueness.
        int previousOutput = pairs.Count - 1;
        for (int index = retained.Count - 1; index >= 0; index--)
        {
            (int _, CanonicalModelItem call, CanonicalModelItem output) = retained[index];
            while (previousOutput >= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (int _, CanonicalModelItem sourceCall, CanonicalModelItem sourceOutput) = pairs[previousOutput];
                if (JsonElement.DeepEquals(call.Content, sourceCall.Content)
                    && JsonElement.DeepEquals(output.Content, sourceOutput.Content)) { break; }
                previousOutput--;
            }
            if (previousOutput != matchedIndices[index])
            {
                return new(ServiceErrorType.Rejected, "Compact не позволяет однозначно определить исходную пару функции.");
            }
            previousOutput--;
        }
        int previousCall = -1;
        foreach ((int _, int originalCall) in matchedCalls.OrderBy(pair => pair.CandidateCall))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (originalCall <= previousCall)
            {
                return new(ServiceErrorType.Rejected, "Compact изменил порядок исходных вызовов функции.");
            }
            previousCall = originalCall;
        }
        return null;
    }

    /// <summary>Сопоставляет каждый известный результат с первым ещё не закрытым вызовом того же ID.</summary>
    private static List<(int CallIndex, CanonicalModelItem Call, CanonicalModelItem Output)> Pairs(
        IReadOnlyList<CanonicalModelItem> items, CancellationToken cancellationToken)
    {
        Dictionary<string, Queue<int>> pending = new(StringComparer.Ordinal);
        List<(int CallIndex, CanonicalModelItem Call, CanonicalModelItem Output)> result = [];
        for (int index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement content = items[index].Content;
            if (!content.TryGetProperty("type", out JsonElement type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() is not ("function_call" or "function_call_output")) { continue; }
            // Обе последовательности уже прошли ValidateFunctionPairs; missing/unmatched здесь невозможны.
            string id = content.GetProperty("call_id").GetString()!;
            if (type.GetString() == "function_call")
            {
                if (!pending.TryGetValue(id, out Queue<int>? calls)) { pending.Add(id, calls = new()); }
                calls.Enqueue(index);
            }
            else
            {
                int callIndex = pending[id].Dequeue();
                result.Add((callIndex, items[callIndex], items[index]));
            }
        }
        return result;
    }
}

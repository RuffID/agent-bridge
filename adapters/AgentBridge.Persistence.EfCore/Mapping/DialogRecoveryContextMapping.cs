using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Persistence.EfCore.Models;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Effective pairs по original positions; originals/terminal/confirmed JSON не переписываются.</summary>
internal static class DialogRecoveryContextMapping
{
    public static IReadOnlyList<StoredDialogTurn> Apply(IReadOnlyList<StoredDialogTurn> history,
        IReadOnlyList<DialogRecoveryOperationRecord> operations)
    {
        if (operations.Count > 4096) throw new InvalidOperationException("Recovery history budget exceeded.");
        Dictionary<Guid, List<DialogRuntimeState.Call>> byTurn = [];
        foreach (DialogRecoveryOperationRecord operation in operations)
        {
            List<DialogRuntimeState.Call> calls = JsonSerializer.Deserialize<List<DialogRuntimeState.Call>>(operation.PairsJson) ??
                throw new InvalidOperationException("Recovery pairs missing.");
            if (calls.Count is < 1 or > 1024 || calls.Any(call => call.TurnId != calls[0].TurnId))
                throw new InvalidOperationException("Recovery pair budget/turn mismatch.");
            byTurn[calls[0].TurnId] = calls;
        }

        List<StoredDialogTurn> effective = [];
        foreach (StoredDialogTurn turn in history)
        {
            if (!byTurn.TryGetValue(turn.Id, out List<DialogRuntimeState.Call>? calls))
            {
                effective.Add(turn);
                continue;
            }

            Dictionary<int, CanonicalModelItem> outputs = [];
            HashSet<int> skip = [];
            foreach (DialogRuntimeState.Call call in calls)
            {
                StoredModelStep step = turn.ModelSteps.Single(item => item.StepId == call.StepId);
                if (call.ItemIndex < 0 || call.ItemIndex >= turn.Items.Count || call.OutputIndex < 0 ||
                    call.OutputIndex >= step.Response.Output.Count ||
                    turn.Items[call.ItemIndex].Content.GetRawText() != step.Response.Output[call.OutputIndex].Content.GetRawText())
                    throw new InvalidOperationException("Recovery original position mismatch.");

                CanonicalModelItem output;
                if (call.OutputItemIndex is int index)
                {
                    if (index <= call.ItemIndex || index >= turn.Items.Count || !skip.Add(index))
                        throw new InvalidOperationException("Recovery confirmed position mismatch.");
                    output = turn.Items[index];
                }
                else
                {
                    using JsonDocument document = JsonDocument.Parse(call.ResolutionJson ??
                        throw new InvalidOperationException("Recovery unresolved position."));
                    output = new(document.RootElement);
                }

                if (output.Content.GetProperty("type").GetString() != "function_call_output" ||
                    output.Content.GetProperty("call_id").GetString() != call.CallId)
                    throw new InvalidOperationException("Recovery protocol output mismatch.");
                outputs.Add(call.ItemIndex, output);
            }

            List<CanonicalModelItem> items = [];
            int position = 0;
            foreach (CanonicalModelItem item in turn.Items)
            {
                if (!skip.Contains(position))
                {
                    items.Add(item);
                    if (outputs.TryGetValue(position, out CanonicalModelItem? output)) items.Add(output);
                }

                position++;
            }

            effective.Add(new(turn.Id, turn.Sequence, turn.Status, items, turn.ModelSteps, turn.Settings));
        }

        if (byTurn.Keys.Any(id => history.All(turn => turn.Id != id)))
            throw new InvalidOperationException("Recovery orphan turn.");
        return effective.AsReadOnly();
    }
}

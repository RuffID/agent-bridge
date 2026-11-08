using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridgeConsole;

namespace AgentBridge.LiveAcceptance;

/// <inheritdoc/>
/// <remarks>Резервирует единственное безопасное выполнение; повтор и исполнение после restart запрещены.</remarks>
internal class AcceptanceProbe(ProbeTool probe, AcceptanceJournal journal) : IToolHandler
{
    public ModelToolDefinition Definition => ProbeTool.ToolDefinition;

    /// <inheritdoc/>
    public async Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        AcceptanceJournal.Require(journal.Evidence.Phase == "json-started", "probe_after_restart");
        AcceptanceJournal.Require(journal.Evidence.ProbeExecutions == 0, "probe_repeated");
        journal.Evidence.ProbeExecutions++;
        journal.Save();
        return await probe.ExecuteAsync(invocation, cancellationToken);
    }
}

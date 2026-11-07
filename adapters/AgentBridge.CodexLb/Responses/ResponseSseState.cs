using AgentBridge.Application.Models;
using AgentBridge.Responses;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Привязывает общий накопитель к прежним codex-lb continuation и typed errors.</summary>
internal class ResponseSseState(ModelContinuation? previous, string binding)
    : ResponseSseAccumulator(
        (body, headers) => ResponseContinuationMapper.Create(previous, body, headers, binding),
        error => ResponseErrorReader.Read(error, null))
{
}

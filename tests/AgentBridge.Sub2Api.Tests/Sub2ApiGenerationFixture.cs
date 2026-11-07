using System.Net;
using System.Text;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Existing composition с actual HTTP pipeline и локальным наблюдаемым потоком.</summary>
internal class Sub2ApiGenerationFixture(TimeSpan? timeout = null) : Sub2ApiCatalogFixture(timeout: timeout)
{
    internal const string COMPLETED = """{"id":"resp_echo","model":"requested-echo","status":"completed","output":[{"type":"reasoning","encrypted_content":"opaque-result","future":true},{"type":"function_call","call_id":"c","name":"tool","arguments":"{}"}]}""";
    internal ApplicationCallContext Call { get; } = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("synthetic-owner"), Guid.NewGuid(), "agent");
    internal IModelGateway Gateway => Scope.ServiceProvider.GetRequiredService<IModelGateway>();
    internal ModelAccess Access { get; } = new(INDIVIDUAL_KEY);
    internal ModelRequest Request { get; } = new("public-model", "medium", "instructions", [], []);

    /// <summary>Один actual HTTP response, без внешнего handler и без повторных запросов.</summary>
    internal Sub2ApiTestStream Reply(string text, string mediaType = "text/event-stream", int fragment = 7,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes(text), fragment);
        Handler.Respond = (_, _) =>
        {
            HttpResponseMessage response = new(status) { Content = new StreamContent(stream) };
            response.Content.Headers.ContentType = new(mediaType);
            response.Headers.Add("x-codex-turn-state", "private-server-state");
            return Task.FromResult(response);
        };
        return stream;
    }

    /// <summary>Создаёт корректно закрытый SSE frame.</summary>
    internal static string Frame(string json)
    {
        return "data: " + json + "\n\n";
    }

    /// <summary>Полный terminal response с encrypted output, без доказательства actual upstream model.</summary>
    internal static string Terminal()
    {
        return Frame("{\"type\":\"response.completed\",\"response\":" + COMPLETED + "}");
    }

    /// <summary>Текстовый partial output, который обязан пережить EOF/отмену/timeout.</summary>
    internal static string Partial()
    {
        return Frame("""{"type":"response.output_item.added","output_index":0,"item":{"id":"m","type":"message","content":[{"type":"output_text","text":""}],"future":17}}""")
            + Frame("""{"type":"response.output_text.delta","output_index":0,"item_id":"m","content_index":0,"delta":"hello"}""");
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.Persistence.EfCore.Tests.Sub2Api;

/// <summary>Локальные manifest и Responses, без delegating handler или сетевого fallback.</summary>
internal class Sub2ApiRunTransport : HttpMessageHandler
{
    private const string MANIFEST = """{"models":[{"slug":"gpt-5","supported_in_api":true,"supported_reasoning_levels":[{"effort":"medium"}]}]}""";
    internal Queue<Func<CancellationToken, Task<HttpResponseMessage>>> Replies { get; } = new();
    internal List<JsonElement> Bodies { get; } = [];
    internal List<string> Authorizations { get; } = [];
    internal List<string> Urls { get; } = [];
    internal List<Body> Responses { get; } = [];
    internal int CatalogCalls { get; private set; }

    /// <summary>Добавляет canonical lifecycle-ответ, optionally завернутый в SSE terminal event.</summary>
    internal void Enqueue(IEnumerable<CanonicalModelItem> output, string status = "completed", bool sse = false)
    {
        string json = JsonSerializer.Serialize(new { id = "response-synthetic", status, output = output.Select(item => item.Content) });
        if (sse)
        {
            json = "data: " + JsonSerializer.Serialize(new { type = "response." + status, response = JsonSerializer.Deserialize<JsonElement>(json) }) + "\n\n";
        }

        string mediaType = "application/json";
        if (sse)
        {
            mediaType = "text/event-stream";
        }

        Replies.Enqueue(_ => Task.FromResult(Json(json, HttpStatusCode.OK, mediaType)));
    }

    /// <summary>Создаёт наблюдаемый локальный response content.</summary>
    internal HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK, string mediaType = "application/json")
    {
        Body body = new(json, mediaType);
        Responses.Add(body);
        return new(status) { Content = body };
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Urls.Add(request.RequestUri!.AbsoluteUri);
        Authorizations.Add(request.Headers.Authorization!.ToString());
        if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/models", StringComparison.Ordinal))
        {
            CatalogCalls++;
            return Json(MANIFEST);
        }

        if (request.Method != HttpMethod.Post || !request.RequestUri.AbsolutePath.EndsWith("/responses", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unexpected transport operation.");
        }

        using JsonDocument document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        Bodies.Add(document.RootElement.Clone());
        return await Replies.Dequeue()(cancellationToken);
    }

    /// <summary>Фиксирует освобождение каждого model/catalog response.</summary>
    internal class Body(string text, string mediaType) : StringContent(text, Encoding.UTF8, mediaType)
    {
        internal bool Disposed { get; private set; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}

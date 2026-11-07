using System.Net;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Responses;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Проверяет linked механизмы внутри Sub2Api DLL через actual HTTP library; не реализует sub2api gateway.</summary>
public class SharedResponsesTests
{
    /// <summary>W03/W04/W06: JSON/compact writers сохраняют полный input, opaque поля и пары инструментов без anchor.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommonJsonMechanismsPreserveFullCanonicalContext(bool compact)
    {
        // Arrange
        CanonicalModelItem[] input =
        [
            Item("""{"type":"reasoning","encrypted_content":"OPAQUE_EXAMPLE","unknown":{"a":1}}"""),
            Item("""{"type":"function_call","call_id":"call_example","name":"read_example","arguments":"{}"}"""),
            Item("""{"type":"function_call_output","call_id":"call_example","output":"{\"total\":42}"}""")
        ];
        ModelRequest request = new("example-model", "low", "instructions", input, []);
        ServiceResult<ResponseRequestBody> prepared;
        if (compact)
        {
            prepared = CompactRequestWriter.Write(request);
        }
        else
        {
            prepared = ResponseRequestWriter.Write(request, null);
        }

        Assert.True(prepared.Success);
        using Handler handler = new();
        using HttpClient client = new(handler);
        HttpApiClient http = new(client, NullLogger<HttpApiClient>.Instance);

        // Act
        HttpResponseResult<JsonElement> response = await http.SendWithResponseAsync<JsonElement>(new LibraryHttpRequestOptions
        {
            Method = HttpMethod.Post, Url = "https://example.invalid/responses", Body = prepared.Data!.Content
        }, TestContext.Current.CancellationToken);
        List<CanonicalModelItem> output = CanonicalOutputReader.Read(response.Body, out bool hasOutput);

        // Assert
        Assert.Equal(1, handler.Calls);
        Assert.False(handler.Body.GetProperty("store").GetBoolean());
        Assert.False(handler.Body.TryGetProperty("previous_response_id", out _));
        Assert.False(handler.Body.TryGetProperty("max_output_tokens", out _));
        Assert.Equal("example-model", handler.Body.GetProperty("model").GetString());
        Assert.Equal("low", handler.Body.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(input.Select(item => item.Content)), handler.Body.GetProperty("input")));
        Assert.True(hasOutput);
        Assert.Equal("OPAQUE_OUTPUT", Assert.Single(output).Content.GetProperty("encrypted_content").GetString());
    }

    /// <summary>W05: actual owned stream, BOM/multiline и незакрытый EOF используют ту же реализацию framing.</summary>
    [Fact]
    public async Task CommonSseMechanismReadsActualOwnedHttpStream()
    {
        // Arrange
        using Handler handler = new()
        {
            Text = "\uFEFF:keepalive\r\nevent: response.completed\r\ndata: {\"type\":\r\ndata: \"response.completed\"}\r\n\r\ndata: unfinished",
            MediaType = "text/event-stream"
        };
        using HttpClient client = new(handler);
        HttpApiClient http = new(client, NullLogger<HttpApiClient>.Instance);
        List<(string? Event, string Data)> events = [];

        // Act
        await using (HttpStreamResponseResult response = await http.SendStreamAsync(new LibraryHttpRequestOptions
        {
            Method = HttpMethod.Post, Url = "https://example.invalid/responses", Body = new { stream = true }
        }, TestContext.Current.CancellationToken))
        {
            await foreach ((string? name, string data) in SseEventReader.ReadAsync(response.Body, TestContext.Current.CancellationToken))
            {
                events.Add((name, data));
            }

            Assert.True(response.Body.CanRead);
        }

        // Assert
        Assert.Equal(1, handler.Calls);
        Assert.Equal(("response.completed", "{\"type\":\n\"response.completed\"}"), Assert.Single(events));
        Assert.True(handler.Content!.Disposed);
    }

    /// <summary>Создаёт независимый canonical item из синтетического образца.</summary>
    private static CanonicalModelItem Item(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new CanonicalModelItem(document.RootElement);
    }

    /// <summary>Локальная transport-граница без сокетов и скрытых повторов.</summary>
    private class Handler : HttpMessageHandler
    {
        internal string Text = """{"output":[{"type":"reasoning","encrypted_content":"OPAQUE_OUTPUT","future":true}]}""";
        internal string MediaType = "application/json";
        internal JsonElement Body;
        internal int Calls;
        internal Content? Content;

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Body = body.RootElement.Clone();
            Content = new Content(Text);
            Content.Headers.ContentType = new(MediaType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = Content };
        }
    }

    /// <summary>Наблюдает освобождение content actual HTTP pipeline.</summary>
    private class Content(string value) : StringContent(value)
    {
        internal bool Disposed;

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}

using System.Security.Cryptography;
using System.Text.Json;

namespace AgentBridge.LiveAcceptance;

/// <summary>Ограничивает фактические отправки существующего HTTP транспорта без собственного API клиента.</summary>
internal class AcceptanceHttpHandler(AcceptanceJournal journal, Uri endpoint, string model,
    string effort, bool streaming) : DelegatingHandler(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
{
    private bool catalogSent;

    public IReadOnlyList<JsonElement> RequiredPrefix { get; set; } = [];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri uri = request.RequestUri ?? throw new InvalidOperationException("request_uri_missing");
        AcceptanceJournal.Require(uri.GetLeftPart(UriPartial.Authority) == endpoint.GetLeftPart(UriPartial.Authority), "origin_changed");
        AcceptanceJournal.Require(uri.Query.Length == 0, "unexpected_query");
        string prefix = endpoint.AbsolutePath.TrimEnd('/');
        if (prefix.EndsWith("/v1", StringComparison.Ordinal))
        {
            prefix = prefix[..^3];
        }

        bool generation = request.Method == HttpMethod.Post && uri.AbsolutePath == prefix + "/v1/responses";
        bool catalog = request.Method == HttpMethod.Get && uri.AbsolutePath == prefix + "/backend-api/codex/models";
        AcceptanceJournal.Require(generation || catalog, "route_not_approved");
        if (generation)
        {
            AcceptanceJournal.Require(journal.Evidence.Generations < 10, "generation_limit");
            byte[] bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement body = document.RootElement;
            AcceptanceJournal.Require(!body.TryGetProperty("previous_response_id", out _), "continuation_sent");
            AcceptanceJournal.Require(body.GetProperty("model").GetString() == model, "model_changed");
            AcceptanceJournal.Require(body.GetProperty("reasoning").GetProperty("effort").GetString() == effort, "effort_changed");
            AcceptanceJournal.Require(body.GetProperty("stream").GetBoolean() == streaming, "stream_mode_changed");
            JsonElement[] input = body.GetProperty("input").EnumerateArray().ToArray();
            AcceptanceJournal.Require(input.Length >= RequiredPrefix.Count, "history_truncated");
            int position = 0;
            foreach (JsonElement expected in RequiredPrefix)
            {
                AcceptanceJournal.Require(JsonElement.DeepEquals(input[position++], expected), "history_changed");
            }

            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            AcceptanceJournal.Require(!journal.Evidence.RequestHashes.Contains(hash), "request_repeated");
            journal.Evidence.RequestHashes.Add(hash);
            journal.Evidence.InputHashes.Add(AcceptanceJournal.Hash(input));
            journal.Evidence.Generations++;
        }
        else
        {
            AcceptanceJournal.Require(!catalogSent, "catalog_repeated");
            AcceptanceJournal.Require(journal.Evidence.Catalogs < 2, "catalog_limit");
            catalogSent = true;
            journal.Evidence.Catalogs++;
        }

        journal.Save();
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        journal.Evidence.HttpStatuses.Add((int)response.StatusCode);
        string format = response.Content.Headers.ContentType?.MediaType ?? "missing";
        journal.Evidence.ResponseFormats.Add(format);
        journal.Save();
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new AcceptanceCheckException("http_rejected");
        }

        string expectedFormat = "application/json";
        if (generation && streaming)
        {
            expectedFormat = "text/event-stream";
        }

        if (format != expectedFormat)
        {
            response.Dispose();
            throw new AcceptanceCheckException("response_format_unexpected");
        }

        return response;
    }
}

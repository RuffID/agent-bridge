using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверки полного формата persistence DTO через сериализацию, без обещания restart на настоящей БД.</summary>
public class PersistencePayloadTests
{
    /// <summary>Все lifecycle сохраняют порядок output, tool result, opaque и unknown поля отдельно от envelope.</summary>
    [Theory]
    [InlineData(ModelResponseStatus.Completed)]
    [InlineData(ModelResponseStatus.Incomplete)]
    [InlineData(ModelResponseStatus.Failed)]
    [InlineData(ModelResponseStatus.Canceled)]
    public void FullResponseSurvivesSerializationWithLifecycleAndUnknownFields(ModelResponseStatus status)
    {
        using JsonDocument item = JsonDocument.Parse("{\"type\":\"reasoning\",\"encrypted_content\":\"opaque\",\"unknown\":{\"large\":123456789012345678901234567890}}");
        using JsonDocument tool = JsonDocument.Parse("{\"type\":\"function_call_output\",\"call_id\":\"call-1\",\"output\":\"данные\",\"future\":[null,true]}");
        using JsonDocument envelopeJson = JsonDocument.Parse("{\"id\":\"response-1\",\"usage\":{\"total_tokens\":42},\"future\":{\"x\":1}}");
        using JsonDocument continuationJson = JsonDocument.Parse("{\"upstream_owner\":\"owner-1\",\"opaque\":{\"cursor\":\"next\"}}");
        CanonicalModelItem[] items = [new(item.RootElement), new(tool.RootElement)];
        CanonicalModelEnvelope envelope = new(envelopeJson.RootElement);
        ModelContinuation continuation = new(continuationJson.RootElement);
        ServiceError error = new(ServiceErrorType.Rejected, "Безопасный отказ.");
        ModelResponse original = status switch
        {
            ModelResponseStatus.Completed => ModelResponse.Completed(items, envelope, continuation),
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete(items, envelope, continuation),
            ModelResponseStatus.Failed => ModelResponse.Failed(items, error, envelope, continuation),
            ModelResponseStatus.Canceled => ModelResponse.Canceled(items, envelope, continuation),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        ModelResponseRecord persisted = ModelResponseRecord.FromModelResponse(original);
        ModelResponseRecord reloaded = JsonSerializer.Deserialize<ModelResponseRecord>(JsonSerializer.Serialize(persisted))!;
        ModelResponse restored = reloaded.ToModelResponse();
        Assert.Equal(status, restored.Status);
        Assert.Equal(2, restored.Output.Count);
        Assert.Equal("123456789012345678901234567890", restored.Output[0].Content.GetProperty("unknown").GetProperty("large").GetRawText());
        Assert.Equal("call-1", restored.Output[1].Content.GetProperty("call_id").GetString());
        Assert.Equal("данные", restored.Output[1].Content.GetProperty("output").GetString());
        Assert.Equal(envelope.Content.GetRawText(), restored.Envelope!.Content.GetRawText());
        Assert.Equal(continuation.Content.GetRawText(), restored.Continuation!.Content.GetRawText());
        Assert.Equal(original.Error?.Type, restored.Error?.Type);
        Assert.Equal(original.Error?.Message, restored.Error?.Message);
        Assert.DoesNotContain(restored.Output, output => output.Content.TryGetProperty("usage", out _));
    }

    /// <summary>Отсутствующий полный envelope при обрыве не заменяется вымышленным результатом.</summary>
    [Fact]
    public void IncompleteReportWithoutEnvelopeRemainsIncomplete()
    {
        ModelResponse restored = ModelResponseRecord.FromModelResponse(ModelResponse.Incomplete([])).ToModelResponse();
        Assert.Equal(ModelResponseStatus.Incomplete, restored.Status);
        Assert.Empty(restored.Output);
        Assert.Null(restored.Envelope);
        Assert.Null(restored.Continuation);
    }

    /// <summary>Повреждённый формат отклоняется явно: нет частичного восстановления и fallback в Completed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void InvalidPersistedResponseFailsFast(int corruption)
    {
        ModelResponseRecord record = new();
        switch (corruption)
        {
            case 0: record.FormatVersion = 2; break;
            case 1: record.Status = (ModelResponseStatus)99; break;
            case 2: record.Status = ModelResponseStatus.Failed; break;
            case 3: record.ErrorType = ServiceErrorType.Rejected; break;
            case 4: record.OutputJson = "{}"; break;
            case 5: record.OutputJson = "[null]"; break;
            case 6: record.EnvelopeJson = "[]"; break;
            case 7: record.ContinuationJson = "null"; break;
        }
        if (corruption < 5)
        {
            Assert.Throws<InvalidOperationException>(() => record.ToModelResponse());
        }
        else
        {
            Assert.Throws<ArgumentException>(() => record.ToModelResponse());
        }
    }

    /// <summary>Повторная материализация DTO сохраняет token и фиксированные даты, не используя Domain lifetime.</summary>
    [Fact]
    public void SerializedDialogRetainsIncarnationRevisionOwnerAndFixedExpiry()
    {
        DateTimeOffset created = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        DialogRecord original = new()
        {
            Id = Guid.NewGuid(), IncarnationId = Guid.NewGuid(), Revision = 17, OwnerId = " User:Б ",
            CreatedAtUtc = created, ExpiresAtUtc = created.AddHours(36), LastChangedAtUtc = created.AddHours(1), ContentBytes = 4096
        };
        DialogRecord reloaded = JsonSerializer.Deserialize<DialogRecord>(JsonSerializer.Serialize(original))!;
        DialogWriteToken token = new(DialogId.From(reloaded.Id), reloaded.IncarnationId, reloaded.Revision);
        Assert.Equal(original.IncarnationId, token.IncarnationId);
        Assert.Equal(original.Revision, token.Revision);
        Assert.Equal(original.Id, token.DialogId.Value);
        Assert.Equal(original.OwnerId, reloaded.OwnerId);
        Assert.Equal(original.ExpiresAtUtc, reloaded.ExpiresAtUtc);
        Assert.Equal(original.ContentBytes, reloaded.ContentBytes);
        reloaded.IncarnationId = Guid.NewGuid();
        Assert.NotEqual(token.IncarnationId, reloaded.IncarnationId);
    }
}

using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Persistence.EfCore.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Единственный новый формат отчётов отклоняет прежние и повреждённые metadata без fallback.</summary>
public class ConnectorPayloadTests
{
    /// <summary>Старый raw envelope, null и неполные metadata не получают вымышленное происхождение.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("{\"id\":\"old-response\",\"output\":[]}")]
    [InlineData("{\"envelope\":null,\"connector\":\"sub2api\",\"binding\":null}")]
    [InlineData("{\"envelope\":null,\"connector\":\"sub2api\",\"binding\":\"invalid\"}")]
    [InlineData("{\"envelope\":null,\"connector\":null,\"binding\":null,\"extra\":1}")]
    [InlineData("{\"envelope\":{},\"connector\":null,\"binding\":null}")]
    public void LegacyAndInvalidMetadataAreRejected(string? json)
    {
        // Arrange
        ModelResponseRecord record = ModelResponseRecord.FromModelResponse(ModelResponse.Completed([]));
        record.EnvelopeJson = json;

        // Act
        Exception? error = Record.Exception(() => record.ToModelResponse());

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(json, record.EnvelopeJson);
    }

    /// <summary>Canonical envelope сохраняет исходные пробелы, escape sequences и отсутствие transport anchor.</summary>
    [Fact]
    public void EnvelopeTextAndProvenanceRoundTripIndependently()
    {
        // Arrange
        const string JSON = "{ \"future\" : \"\\u0430\", \"number\" : 1.00 }";
        using JsonDocument document = JsonDocument.Parse(JSON);
        ModelConnectorProvenance provenance = new("sub2api", new string('B', 64));
        ModelResponse original = ModelResponse.Completed([], new(document.RootElement)).WithConnectorProvenance(provenance);

        // Act
        ModelResponse restored = ModelResponseRecord.FromModelResponse(original).ToModelResponse();

        // Assert
        Assert.Equal(JSON, restored.Envelope!.Content.GetRawText());
        Assert.Equal(provenance.ConnectorId, restored.ConnectorProvenance!.ConnectorId);
        Assert.Equal(provenance.Binding, restored.ConnectorProvenance.Binding);
        Assert.Null(restored.Continuation);
        Assert.False(original.Envelope!.Content.TryGetProperty("connector", out _));
    }
}

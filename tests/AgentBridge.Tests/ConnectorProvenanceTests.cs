using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Tokenization;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Проверяет отдельное происхождение окна и каждого непокрытого источника.</summary>
public class ConnectorProvenanceTests
{
    /// <summary>Равенство модели и отсутствие continuation не разрешают неизвестный или чужой compact.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("codex-lb", ServiceErrorType.Unsupported)]
    [InlineData("sub2api", null)]
    public void CompactWithoutContinuationRequiresOwnProvenance(string? connector, ServiceErrorType? expected)
    {
        // Arrange
        ModelConnectorProvenance target = new("sub2api", new string('A', 64));
        ModelResponse compact = ModelResponse.Completed([Opaque()]);
        if (connector is not null)
        {
            compact = compact.WithConnectorProvenance(new(connector, target.Binding));
        }

        DialogSnapshot dialog = Snapshot([], new(1, 0, compact, "same-model"));
        ContextModelGuard guard = new(new ContextTokenCounter(), []);

        // Act
        ServiceResult result = guard.CheckConnector(dialog, target, TestContext.Current.CancellationToken);

        // Assert
        if (connector is null)
        {
            Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        }
        else
        {
            Assert.Equal(expected is null, result.Success);
            Assert.Equal(expected, result.Error?.Type);
        }

        Assert.Same(compact, dialog.ActiveContext!.Compaction);
        Assert.Null(compact.Continuation);
    }

    /// <summary>Родное окно не легализует отдельный чужой output или неизвестный duplicate input.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryOccurrenceAndOutputIsChecked(bool duplicateResidual)
    {
        // Arrange
        ModelConnectorProvenance target = new("sub2api", new string('A', 64));
        CanonicalModelItem item = Opaque();
        ModelResponse own = ModelResponse.Completed([item]).WithConnectorProvenance(target);
        ModelResponse other = ModelResponse.Completed([item]).WithConnectorProvenance(new("codex-lb", target.Binding));
        ModelResponse output = other;
        CanonicalModelItem[] input = [item];
        if (duplicateResidual)
        {
            output = own;
            input = [item, item];
        }

        StoredDialogTurn turn = new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, input,
            [new(Guid.NewGuid(), output)], new("same-model", "medium", 10, 1, 1000));
        DialogSnapshot dialog = Snapshot([turn], new(1, 0, own, "same-model"));
        ContextModelGuard guard = new(new ContextTokenCounter(), []);

        // Act
        ServiceResult result = guard.CheckConnector(dialog, target, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(input.Length, turn.Items.Count);
        Assert.Same(output, Assert.Single(turn.ModelSteps).Response);
    }

    /// <summary>Каждый фактор владения меняет binding, новый TurnId сохраняет возможность продолжить диалог.</summary>
    [Theory]
    [InlineData("dialog")]
    [InlineData("owner")]
    [InlineData("agent")]
    [InlineData("endpoint")]
    [InlineData("key")]
    [InlineData("turn")]
    public void BindingTracksOwnershipExceptTurn(string changed)
    {
        // Arrange
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        ModelAccess access = new("synthetic-key");
        string endpoint = "https://gateway.invalid/v1/responses";
        ModelConnectorProvenance original = ModelConnectorProvenance.Create("sub2api", call, access, endpoint);
        ApplicationCallContext next = new(call.DialogId, call.OwnerId, Guid.NewGuid(), call.AgentId);
        switch (changed)
        {
            case "dialog": next = new(DialogId.From(Guid.NewGuid()), call.OwnerId, call.TurnId, call.AgentId); break;
            case "owner": next = new(call.DialogId, DialogOwnerId.From("other"), call.TurnId, call.AgentId); break;
            case "agent": next = new(call.DialogId, call.OwnerId, call.TurnId, "other"); break;
            case "endpoint": endpoint += "/other"; break;
            case "key": access = new("other-key"); break;
        }

        // Act
        ModelConnectorProvenance candidate = ModelConnectorProvenance.Create("sub2api", next, access, endpoint);
        ServiceError? error = original.ValidateSource(candidate);

        // Assert
        if (changed == "turn")
        {
            Assert.Null(error);
            Assert.Equal(original.Binding, candidate.Binding);
        }
        else
        {
            Assert.Equal(ServiceErrorType.Conflict, error!.Type);
            Assert.NotEqual(original.Binding, candidate.Binding);
        }
    }

    /// <summary>Текст не требует метки; непрозрачное поле в новом input отвергается без потери данных.</summary>
    [Fact]
    public void PlainTextIsAllowedButUnknownInputIsNotTrusted()
    {
        // Arrange
        ContextModelGuard guard = new(new ContextTokenCounter(), []);
        CanonicalModelItem plain = Item("""{"type":"message","role":"user","content":"hello"}""");

        // Act
        ServiceResult text = guard.CheckUnboundInput([plain], TestContext.Current.CancellationToken);
        ServiceResult opaque = guard.CheckUnboundInput([Opaque()], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(text.Success);
        Assert.Equal(ServiceErrorType.Unsupported, opaque.Error!.Type);
    }

    /// <summary>Создаёт snapshot с одинаковой моделью и без I/O.</summary>
    private static DialogSnapshot Snapshot(IEnumerable<StoredDialogTurn> turns, StoredDialogContext? compact)
    {
        return new(new(DialogId.From(Guid.NewGuid()), Guid.NewGuid(), 0), DialogOwnerId.From("owner"),
            DateTimeOffset.UtcNow, null, 0, turns, compact);
    }

    /// <summary>Opaque item остаётся независимым canonical JSON.</summary>
    private static CanonicalModelItem Opaque()
    {
        return Item("""{"type":"reasoning","encrypted_content":"opaque"}""");
    }

    /// <summary>Создаёт независимый JSON snapshot.</summary>
    private static CanonicalModelItem Item(string json)
    {
        return new(JsonSerializer.Deserialize<JsonElement>(json));
    }
}

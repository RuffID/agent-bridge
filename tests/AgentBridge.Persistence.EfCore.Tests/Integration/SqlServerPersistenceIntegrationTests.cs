using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Проверяет настоящие MSSQL migration/append/context и новый DI root в собственном контейнере.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class SqlServerPersistenceIntegrationTests(DatabaseIntegrationFixture environment)
{
    /// <summary>Первая установка и публичные короткие записи сохраняют историю; stale root и expiry запрещают запись.</summary>
    [SqlServerIntegrationFact]
    public async Task InitializeAppendContextRestartAndExpiry()
    {
        await using SqlServerIntegrationDatabase database = await environment.CreateSqlServerDatabaseAsync();
        Assert.Equal(MaintenanceOutcome.Initialized, (await database.InitializeNewAsync(TestContext.Current.CancellationToken)).Outcome);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DialogOwnerId owner = DialogOwnerId.From(" User:Б ");
        DialogId id = DialogId.From(Guid.NewGuid());
        DialogAccess access = new(id, owner, now);
        DialogWriteToken token;
        Guid turn = Guid.NewGuid();
        ModelResponse response = PersistenceIntegrationTests.Response(ModelResponseStatus.Completed);
        using (IServiceScope scope = database.Root.CreateScope())
        {
            token = PersistenceIntegrationTests.Success(await scope.ServiceProvider.GetRequiredService<IDialogCreator>()
                .CreateAsync(id, owner, now, now.AddHours(1)));
            DialogWriteToken stale = token;
            IDialogTurnWriter writer = scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
            token = PersistenceIntegrationTests.Success(await writer.BeginAsync(access, token, turn,
                [PersistenceIntegrationTests.Item("{\"text\":\"Привет 😀\"}")]));
            Assert.Equal(ServiceErrorType.Conflict, (await writer.AppendAsync(access, stale, turn, [], [])).Error!.Type);
            token = PersistenceIntegrationTests.Success(await writer.AppendAsync(access, token, turn, response.Output,
                [new(Guid.NewGuid(), response)]));
            token = PersistenceIntegrationTests.Success(await writer.FinishAsync(access, token, turn, DialogTurnStatus.Completed, [], []));
            token = PersistenceIntegrationTests.Success(await scope.ServiceProvider.GetRequiredService<IDialogContextWriter>()
                .SaveAsync(access, token, 1, response));
        }
        await using ServiceProvider restarted = database.BuildRoot();
        using IServiceScope readScope = restarted.CreateScope();
        IDialogReader reader = readScope.ServiceProvider.GetRequiredService<IDialogReader>();
        ServiceResult<DialogSnapshot> read = await reader.ReadAsync(access);
        Assert.True(read.Success, read.Error?.Message);
        Assert.Equal(token.DialogId, read.Data!.Token.DialogId);
        Assert.Equal(token.IncarnationId, read.Data.Token.IncarnationId);
        Assert.Equal(token.Revision, read.Data.Token.Revision);
        Assert.Equal(now.AddHours(1), read.Data.ExpiresAtUtc);
        Assert.Equal(2, Assert.Single(read.Data.Turns).Items.Count);
        Assert.Single(read.Data.Turns[0].ModelSteps);
        Assert.True(JsonElement.DeepEquals(response.Envelope!.Content, read.Data.Turns[0].ModelSteps[0].Response.Envelope!.Content));
        Assert.True(JsonElement.DeepEquals(response.Continuation!.Content, read.Data.Turns[0].ModelSteps[0].Response.Continuation!.Content));
        Assert.Equal(1, read.Data.ActiveContext!.ThroughTurnSequence);
        Assert.True(JsonElement.DeepEquals(response.Output[0].Content, read.Data.ActiveContext.Compaction.Output[0].Content));
        Assert.Equal(ServiceErrorType.Forbidden, (await reader.ReadAsync(new(id, DialogOwnerId.From(" user:Б "), now))).Error!.Type);
        ServiceResult<DialogWriteToken> expired = await readScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
            .BeginAsync(new(id, owner, now.AddHours(1)), token, Guid.NewGuid(), []);
        Assert.Equal(ServiceErrorType.Expired, expired.Error!.Type);
        Assert.Single((await reader.ReadAsync(access)).Data!.Turns);
        string[] ordinalOwners = ["Owner", "owner", "Owner ", "Owner\0", "Owner\0\0", "Owner\ud800", "Owner\ud801"];
        foreach (string value in ordinalOwners)
        {
            DialogOwnerId exactOwner = DialogOwnerId.From(value);
            DialogId exactId = DialogId.From(Guid.NewGuid());
            DialogAccess exactAccess = new(exactId, exactOwner, now);
            using IServiceScope ownerScope = restarted.CreateScope();
            DialogWriteToken exact = PersistenceIntegrationTests.Success(await ownerScope.ServiceProvider.GetRequiredService<IDialogCreator>()
                .CreateAsync(exactId, exactOwner, now, now.AddHours(1)));
            foreach (string other in ordinalOwners.Where(other => !StringComparer.Ordinal.Equals(value, other)))
            {
                DialogAccess mismatch = new(exactId, DialogOwnerId.From(other), now);
                Assert.Equal(ServiceErrorType.Forbidden, (await ownerScope.ServiceProvider.GetRequiredService<IDialogReader>()
                    .ReadAsync(mismatch)).Error!.Type);
                Assert.Equal(ServiceErrorType.Forbidden, (await ownerScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
                    .BeginAsync(mismatch, exact, Guid.NewGuid(), [])).Error!.Type);
            }
            PersistenceIntegrationTests.Success(await ownerScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>()
                .BeginAsync(exactAccess, exact, Guid.NewGuid(), []));
            ServiceResult<DialogSnapshot> roundTrip = await ownerScope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(exactAccess);
            Assert.True(roundTrip.Success, roundTrip.Error?.Message);
            Assert.Equal(value, roundTrip.Data!.OwnerId.Value);
            Assert.Equal(1, roundTrip.Data.Token.Revision);
            Assert.Single(roundTrip.Data.Turns);
        }
    }
}

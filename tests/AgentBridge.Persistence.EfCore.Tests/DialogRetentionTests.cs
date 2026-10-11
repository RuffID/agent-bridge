using AgentBridge.Application.Models;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Текущая политика для старых строк; настоящие guards и UoW без подключения к БД.</summary>
public class DialogRetentionTests
{
    /// <summary>Отключённый срок разрешает запись после legacy expiry и запрещает автоматическое удаление.</summary>
    [Fact]
    public async Task UnlimitedPolicyOverridesLegacyExpiry()
    {
        FakeWriteFixture fixture = new(new());
        DialogWriteToken token = await fixture.CreateAsync();
        DateTimeOffset now = FakeWriteFixture.NOW.AddDays(2);
        Assert.True((await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token, now), token, Guid.NewGuid(), [], TestContext.Current.CancellationToken)).Success);
        DialogWriteToken current = new(token.DialogId, token.IncarnationId, fixture.Roots.Persisted.Single().Revision);
        Assert.False((await fixture.Deletion.DeleteAsync(current, now, TestContext.Current.CancellationToken)).Success);
        Assert.Single(fixture.Roots.Persisted);
    }

    /// <summary>Сокращение периода удаляет ранее созданную строку, даже если её legacy expiry ещё не наступил.</summary>
    [Fact]
    public async Task ShorterPolicyDeletesExistingDialogAtBoundary()
    {
        FakeWriteFixture fixture = new(new() { RetentionPeriod = TimeSpan.FromHours(3) });
        DialogWriteToken token = await fixture.CreateAsync();
        DateTimeOffset now = FakeWriteFixture.NOW.AddHours(3);
        Assert.False((await fixture.Writer.BeginAsync(FakeWriteFixture.Access(token, now), token, Guid.NewGuid(), [], TestContext.Current.CancellationToken)).Success);
        Assert.True((await fixture.Deletion.DeleteAsync(token, now, TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Roots.Persisted);
    }

    /// <summary>Бессрочная legacy политика возвращает null; bounded cleanup query учитывает только registered expiry.</summary>
    [Fact]
    public async Task UnlimitedReaderHasNoExpiryOrCleanupCandidates()
    {
        FakeBaseRepository<DialogRecord> roots = new();
        DialogRecord root = new() { Id = Guid.NewGuid(), IncarnationId = Guid.NewGuid(), OwnerId = "owner",
            CreatedAtUtc = FakeWriteFixture.NOW, ExpiresAtUtc = FakeWriteFixture.NOW.AddDays(1) };
        roots.Records.Add(root);
        DialogRetentionPolicy policy = new(new());
        DialogRecordQueries query = new(new FakeDialogByIdRepository(roots), roots, policy);
        FakeBaseRepository<DialogSettingsRecord> settings = new();
        DialogReader reader = new(query, new(new FakeBaseRepository<DialogTurnRecord>()),
            new(new FakeBaseRepository<CanonicalItemRecord>()), new(new FakeBaseRepository<ModelStepRecord>()),
            new(new FakeBaseRepository<DialogContextRecord>()), new(), new(new FakeSettingsByIdRepository(settings)), policy);
        DialogSnapshot snapshot = (await reader.ReadAsync(new(DialogId.From(root.Id), DialogOwnerId.From("owner"),
            FakeWriteFixture.NOW.AddDays(2)), TestContext.Current.CancellationToken)).Data!;
        Assert.Null(snapshot.ExpiresAtUtc);
        Assert.False(snapshot.IsExpired(FakeWriteFixture.NOW.AddYears(100)));
        int reads = roots.ReadCalls;
        Assert.Empty(await query.ReadExpiredAsync(FakeWriteFixture.NOW.AddDays(2), 32, TestContext.Current.CancellationToken));
        Assert.Equal(reads + 1, roots.ReadCalls);
        Assert.Equal(32, roots.LastTake);
    }
}

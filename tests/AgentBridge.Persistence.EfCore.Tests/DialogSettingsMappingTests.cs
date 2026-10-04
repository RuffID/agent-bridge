using AgentBridge.Application.Models;
using AgentBridge.Persistence.EfCore.Mapping;
using AgentBridge.Persistence.EfCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Изолированная metadata/JSON проверка settings21, без подключения к providers.</summary>
public class DialogSettingsMappingTests
{
    /// <summary>Версия выбора independent CAS, cascade FK и nullable historical snapshot совпадают для обоих providers.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentSettingsHaveConcurrencyAndCascadeMetadata(bool postgres)
    {
        DbContextOptionsBuilder<AgentBridgeDbContext> options = new();
        if (postgres) options.UseNpgsql("Host=invalid.example;Database=synthetic"); else options.UseSqlite("Data Source=never-open.db");
        using AgentBridgeDbContext context = new(options.Options);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType settings = model.FindEntityType(typeof(DialogSettingsRecord))!;
        Assert.True(settings.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Equal("Id", Assert.Single(settings.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(settings.GetForeignKeys()).DeleteBehavior);
        Assert.True(model.FindEntityType(typeof(DialogTurnRecord))!.FindProperty("SettingsJson")!.IsNullable);
        Assert.True(model.FindEntityType(typeof(DialogContextRecord))!.FindProperty("SelectedModel")!.IsNullable);
    }

    /// <summary>Snapshot JSON сохраняет все primitive лимиты и не добавляет secret/canonical поля; null остаётся legacy.</summary>
    [Fact]
    public void TurnSnapshotRoundTripsPrimitiveLimits()
    {
        TurnModelSettings original = new("gpt-5", "high", 32000, 4096, 200000);
        string json = TurnSettingsMapping.Write(original)!;
        TurnModelSettings saved = TurnSettingsMapping.Read(json)!;
        Assert.Equal(original.Model, saved.Model);
        Assert.Equal(original.Effort, saved.Effort);
        Assert.Equal(original.TokenThreshold, saved.TokenThreshold);
        Assert.Equal(original.InputTokenReserve, saved.InputTokenReserve);
        Assert.Equal(original.InputContextWindow, saved.InputContextWindow);
        Assert.Null(TurnSettingsMapping.Read(null));
        Assert.Null(TurnSettingsMapping.Write(null));
        Assert.DoesNotContain("ApiKey", json);
    }

    /// <summary>Unknown/missing version не маскируется defaults.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":99,\"settings\":{}}")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"settings\":null}")]
    public void InvalidSnapshotFailsFast(string json) => Assert.Throws<InvalidOperationException>(() => TurnSettingsMapping.Read(json));
}

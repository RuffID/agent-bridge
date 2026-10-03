using AgentBridge.Persistence.EfCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверки модели двух настоящих providers без подключения, SQL, migrations или имитации БД.</summary>
public class PersistenceModelTests
{
    /// <summary>Фиксированные поля защищены EF metadata, а token хранит incarnation и revision независимо от Domain lifetime.</summary>
    [Theory]
    [InlineData(false, "BINARY")]
    [InlineData(true, "C")]
    public void DialogHasPersistentTokenFixedFieldsAndReadIndexes(bool postgres, string collation)
    {
        using AgentBridgeDbContext context = CreateContext(postgres);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType dialog = model.FindEntityType(typeof(DialogRecord))!;
        Assert.Equal(["Id"], dialog.FindPrimaryKey()!.Properties.Select(property => property.Name));
        foreach (string name in new[] { "IncarnationId", "OwnerId", "CreatedAtUtc", "ExpiresAtUtc" })
        {
            Assert.Equal(PropertySaveBehavior.Throw, dialog.FindProperty(name)!.GetAfterSaveBehavior());
        }
        foreach (string name in new[] { "IncarnationId", "Revision", "OwnerId", "ExpiresAtUtc" })
        {
            Assert.True(dialog.FindProperty(name)!.IsConcurrencyToken);
        }
        Assert.Equal(collation, dialog.FindProperty("OwnerId")!.GetCollation());
        Assert.DoesNotContain(dialog.GetIndexes(), index => index.Properties.Any(property => property.Name == "OwnerId"));
        AssertIndex(dialog, false, "ExpiresAtUtc", "Id");
        Assert.Contains(dialog.GetCheckConstraints(), check => check.Sql == "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        Assert.Equal(5, model.GetEntityTypes().Count());
        Assert.DoesNotContain(model.GetEntityTypes(), type => type.ClrType.Namespace?.StartsWith("AgentBridge.Domain", StringComparison.Ordinal) == true);
    }

    /// <summary>Локальные ID обращений и шагов не становятся глобально уникальными; FK несут того же родителя.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParentKeysOrderingAndCascadeCoverAllDependentRows(bool postgres)
    {
        using AgentBridgeDbContext context = CreateContext(postgres);
        IModel model = context.Model;
        IEntityType turn = model.FindEntityType(typeof(DialogTurnRecord))!;
        IEntityType item = model.FindEntityType(typeof(CanonicalItemRecord))!;
        IEntityType step = model.FindEntityType(typeof(ModelStepRecord))!;
        IEntityType compact = model.FindEntityType(typeof(DialogContextRecord))!;
        Assert.Equal(["DialogId", "Id"], turn.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(["DialogId", "TurnId", "Sequence"], item.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(["DialogId", "TurnId", "Id"], step.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(["DialogId", "Version"], compact.FindPrimaryKey()!.Properties.Select(property => property.Name));
        AssertIndex(turn, true, "DialogId", "Sequence");
        AssertIndex(step, true, "DialogId", "TurnId", "Sequence");
        foreach (IEntityType child in new[] { item, step })
        {
            IForeignKey foreignKey = Assert.Single(child.GetForeignKeys());
            Assert.Equal(["DialogId", "TurnId"], foreignKey.Properties.Select(property => property.Name));
            Assert.Equal(typeof(DialogTurnRecord), foreignKey.PrincipalEntityType.ClrType);
        }
        foreach (IEntityType child in new[] { turn, item, step, compact })
        {
            IForeignKey foreignKey = Assert.Single(child.GetForeignKeys());
            Assert.True(foreignKey.IsRequired);
            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        }
        Assert.DoesNotContain(turn.GetIndexes(), index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0].Name == "Id");
        Assert.DoesNotContain(step.GetIndexes(), index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0].Name == "Id");
    }

    /// <summary>Envelope/continuation/output хранятся отдельно от истории; compact допускает только Completed в metadata.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullResponseColumnsAndCompletedCompactAreMapped(bool postgres)
    {
        using AgentBridgeDbContext context = CreateContext(postgres);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType step = model.FindEntityType(typeof(ModelStepRecord))!;
        IEntityType compact = model.FindEntityType(typeof(DialogContextRecord))!;
        foreach (IEntityType type in new[] { step, compact })
        {
            IComplexProperty response = Assert.Single(type.GetComplexProperties());
            Assert.False(response.IsNullable);
            Assert.Equal(7, response.ComplexType.GetProperties().Count());
            foreach (string name in new[] { "OutputJson", "EnvelopeJson", "ContinuationJson", "ErrorMessage" })
            {
                Assert.Equal("text", response.ComplexType.FindProperty(name)!.GetColumnType());
            }
            Assert.False(response.ComplexType.FindProperty("OutputJson")!.IsNullable);
            Assert.True(response.ComplexType.FindProperty("EnvelopeJson")!.IsNullable);
            Assert.Contains(type.GetCheckConstraints(), check => check.Name?.EndsWith("ResponseError", StringComparison.Ordinal) == true);
        }
        Assert.Contains(compact.GetCheckConstraints(), check => check.Sql == "\"ResponseStatus\" = 0");
        Assert.Contains(compact.GetCheckConstraints(), check => check.Sql == "\"ThroughTurnSequence\" >= 0");
        IEntityType item = model.FindEntityType(typeof(CanonicalItemRecord))!;
        Assert.False(item.FindProperty("ContentJson")!.IsNullable);
        Assert.Equal("text", item.FindProperty("ContentJson")!.GetColumnType());
        Assert.DoesNotContain(model.GetEntityTypes().SelectMany(type => type.GetProperties()),
            property => property.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("ModelAccess", StringComparison.Ordinal));
    }

    /// <summary>Целочисленное время обоих providers сохраняет ticks, UTC и порядок на границе истечения.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimestampConversionIsExactSortableAndRejectsNonUtc(bool postgres)
    {
        using AgentBridgeDbContext context = CreateContext(postgres);
        IProperty property = context.Model.FindEntityType(typeof(DialogRecord))!.FindProperty("ExpiresAtUtc")!;
        ValueConverter converter = property.GetTypeMapping().Converter!;
        DateTimeOffset before = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset expires = before.AddTicks(1);
        Assert.Equal(typeof(long), converter.ProviderClrType);
        Assert.Equal(expires, converter.ConvertFromProvider(converter.ConvertToProvider(expires)));
        Assert.True((long)converter.ConvertToProvider(before)! < (long)converter.ConvertToProvider(expires)!);
        Assert.Throws<ArgumentException>(() => converter.ConvertToProvider(expires.ToOffset(TimeSpan.FromHours(7))));
        IProperty nullable = context.Model.FindEntityType(typeof(DialogTurnRecord))!.FindProperty("FinishedAtUtc")!;
        Assert.Null(nullable.GetTypeMapping().Converter!.ConvertToProvider(null));
    }

    /// <summary>Создаёт только options и контекст; ни одно соединение не открывается.</summary>
    private static AgentBridgeDbContext CreateContext(bool postgres)
    {
        DbContextOptionsBuilder<AgentBridgeDbContext> options = new();
        if (postgres)
        {
            options.UseNpgsql("Host=invalid.example;Database=synthetic;Username=synthetic;Password=synthetic");
        }
        else
        {
            options.UseSqlite("Data Source=metadata-only-never-open.db");
        }
        return new AgentBridgeDbContext(options.Options);
    }

    /// <summary>Проверяет индекс по порядку колонок, а не только наличие каждой колонки.</summary>
    private static void AssertIndex(IEntityType type, bool unique, params string[] names) =>
        Assert.Contains(type.GetIndexes(), index => index.IsUnique == unique && index.Properties.Select(property => property.Name).SequenceEqual(names));
}

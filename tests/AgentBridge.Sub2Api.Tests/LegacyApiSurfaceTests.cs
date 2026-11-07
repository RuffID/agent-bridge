using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Сравнивает текущий API с переносимым текстовым эталоном; не является old-consumer ABI verifier.</summary>
public class LegacyApiSurfaceTests
{
    /// <summary>Старые declaring types/assembly identity, методы, конструкторы, accessors и generic/default контракты сохранены.</summary>
    [Theory]
    [InlineData("AgentBridge")]
    [InlineData("AgentBridge.CodexLb")]
    [InlineData("AgentBridge.Integration")]
    public void LegacyPublicAndProtectedSurfaceIsPreserved(string assembly)
    {
        // Arrange
        string newAssembly = Path.Combine(AppContext.BaseDirectory, assembly + ".dll");
        HashSet<string> expected = LegacyApiBaseline.ReadSignatures(assembly);

        // Act
        HashSet<string> actual = PeSurfaceReader.Read(newAssembly);
        string[] missing = expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        // Assert
        Assert.NotEmpty(expected);
        Assert.True(missing.Length == 0,
            $"Missing legacy API entries: {missing.Length}.{Environment.NewLine}" + string.Join(Environment.NewLine, missing.Take(20)));
    }
}

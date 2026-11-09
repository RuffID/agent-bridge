using AgentBridge.Sub2Api.Tests;
using Xunit;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Сравнивает новую поставку с frozen consumers и semantic API старого комплекта.</summary>
public class FrozenConsumerAbiTests(ITestOutputHelper output)
{
    /// <summary>Все прежние public/protected signatures, identities, defaults и constraints сохраняются в actual kit.</summary>
    [Theory]
    [InlineData("PostgreSql", "win-x64")]
    public void DeliveredPublicSurfacePreservesPinnedBaseline(string provider, string rid)
    {
        // Arrange
        AbiEvidence.VerifyBaseline();
        string kit = AbiEvidence.CurrentKit(provider, rid);

        // Act
        Dictionary<string, string[]> missing = new(StringComparer.Ordinal);
        foreach (string name in new[] { "AgentBridge", "AgentBridge.CodexLb", "AgentBridge.Integration" })
        {
            HashSet<string> expected = LegacyApiBaseline.ReadSignatures(name);
            Assert.NotEmpty(expected);
            HashSet<string> actual = PeSurfaceReader.Read(Path.Combine(kit, "lib", name + ".dll"));
            missing[name] = expected.Except(actual, StringComparer.Ordinal).ToArray();
        }

        // Assert
        Assert.All(missing, pair => Assert.True(pair.Value.Length == 0,
            pair.Key + ": " + string.Join(Environment.NewLine, pair.Value.Take(20))));
    }
}

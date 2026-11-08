using AgentBridge.Sub2Api.Tests;
using Xunit;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Сравнивает новую поставку с frozen consumers и semantic API старого комплекта.</summary>
public class FrozenConsumerAbiTests(ITestOutputHelper output)
{
    /// <summary>Обе SQL Server baseline DLL разрешают все references на трёх RID того же провайдера.</summary>
    [Theory]
    [InlineData("SqlServer", "win-x64", "original")]
    [InlineData("SqlServer", "linux-x64", "original")]
    [InlineData("SqlServer", "linux-arm64", "original")]
    [InlineData("SqlServer", "win-x64", "surface")]
    [InlineData("SqlServer", "linux-x64", "surface")]
    [InlineData("SqlServer", "linux-arm64", "surface")]
    public void EveryFrozenReferenceResolves(string provider, string rid, string consumer)
    {
        // Arrange
        string baseline = AbiEvidence.VerifyBaseline();
        string kit = AbiEvidence.CurrentKit(provider, rid);
        byte[] frozen = File.ReadAllBytes(Path.Combine(baseline, "frozen-binaries", consumer, "AgentBridge.BinaryConsumer.dll"));
        FrozenReferenceVerifier verifier = new(frozen, AbiEvidence.References(kit));

        // Act
        (int types, int members, int specifications) = verifier.Verify(frozen);

        // Assert
        Assert.True(types > 0);
        Assert.True(members > 0);
        Assert.True(specifications > 0);
        output.WriteLine($"{provider}/{rid}/{consumer}: TypeRef={types}, MemberRef={members}, MethodSpec={specifications}");
    }

    /// <summary>Все прежние public/protected signatures, identities, defaults и constraints сохраняются в actual kit.</summary>
    [Theory]
    [InlineData("SqlServer", "win-x64")]
    [InlineData("SqlServer", "linux-x64")]
    [InlineData("SqlServer", "linux-arm64")]
    [InlineData("Sqlite", "win-x64")]
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

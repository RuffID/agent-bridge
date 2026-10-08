using Xunit;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Различающие контроли native classifier; синтетические заголовки не являются runtime evidence.</summary>
public class NativeAssetMetadataTests
{
    /// <summary>Настоящий ELF asset принимается только для своей архитектуры; повреждённый class/endian/magic отклоняется.</summary>
    [Theory]
    [InlineData("linux-x64", "linux-arm64")]
    public void ElfRejectsWrongArchitectureAndMalformedHeaders(string rid, string otherRid)
    {
        string root = Environment.GetEnvironmentVariable("AGENTBRIDGE_DELIVERY_ROOT") ?? throw new InvalidOperationException("Нужен каталог поставки.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, "SqlServer", rid, "native", rid, "libmsalruntime.so"));
        NativeAssetMetadata.Validate(bytes, rid);
        Assert.Throws<InvalidDataException>(() => NativeAssetMetadata.Validate(bytes, otherRid));
        Assert.Throws<InvalidDataException>(() => NativeAssetMetadata.Validate(bytes[..20], rid));
        foreach (int position in new[] { 0, 4, 5, 6, 18 })
        {
            byte[] corrupted = (byte[])bytes.Clone();
            corrupted[position] = 0;
            Assert.Throws<InvalidDataException>(() => NativeAssetMetadata.Validate(corrupted, rid));
        }
    }

    /// <summary>Managed PE и ELF не принимаются как Windows native, Windows PE не принимается как Linux asset.</summary>
    [Fact]
    public void NativeRejectsManagedAndMixedOperatingSystems()
    {
        string root = Environment.GetEnvironmentVariable("AGENTBRIDGE_DELIVERY_ROOT") ?? throw new InvalidOperationException("Нужен каталог поставки.");
        byte[] managed = File.ReadAllBytes(Path.Combine(root, "SqlServer", "win-x64", "lib", "AgentBridge.dll"));
        byte[] windows = File.ReadAllBytes(Path.Combine(root, "SqlServer", "win-x64", "native", "win-x64", "Microsoft.Data.SqlClient.SNI.dll"));
        byte[] linux = File.ReadAllBytes(Path.Combine(root, "SqlServer", "linux-x64", "native", "linux-x64", "libmsalruntime.so"));
        NativeAssetMetadata.Validate(windows, "win-x64");
        byte[] wrongMachine = (byte[])windows.Clone();
        int coffOffset = BitConverter.ToInt32(wrongMachine, 0x3c);
        wrongMachine[coffOffset + 4] = 0;
        Assert.Throws<InvalidDataException>(() => NativeAssetMetadata.Validate(wrongMachine, "win-x64"));
        Assert.Throws<InvalidDataException>(() => NativeAssetMetadata.Validate(managed, "win-x64"));
        Assert.Throws<InvalidDataException>(() => NativeAssetMetadata.Validate(windows, "linux-x64"));
        Assert.Throws<BadImageFormatException>(() => NativeAssetMetadata.Validate(linux, "win-x64"));
        Assert.Throws<ArgumentException>(() => NativeAssetMetadata.Validate(linux, "unknown"));
    }
}

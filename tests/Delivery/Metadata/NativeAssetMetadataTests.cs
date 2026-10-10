using Xunit;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Различающие контроли native classifier; синтетические заголовки не являются runtime evidence.</summary>
public class NativeAssetMetadataTests
{
    /// <summary>Синтетический ELF header принимается только для своей архитектуры; повреждённый class/endian/magic отклоняется.</summary>
    [Theory]
    [InlineData("linux-x64", "linux-arm64")]
    [InlineData("linux-arm64", "linux-x64")]
    public void ElfRejectsWrongArchitectureAndMalformedHeaders(string rid, string otherRid)
    {
        byte[] bytes = ElfHeader(rid);
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
        string root = DeliveryTestPaths.SdkRoot;
        byte[] managed = File.ReadAllBytes(Path.Combine(root, "SqlServer", "win-x64", "lib", "AgentBridge.dll"));
        byte[] windows = File.ReadAllBytes(Path.Combine(root, "SqlServer", "win-x64", "native", "win-x64", "Microsoft.Data.SqlClient.SNI.dll"));
        byte[] linux = ElfHeader("linux-x64");
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

    /// <summary>Создаёт header для negative controls; SqlClient 7 не поставляет прежний libmsalruntime.so.</summary>
    private static byte[] ElfHeader(string rid)
    {
        byte[] bytes = new byte[64];
        new byte[] { 0x7f, 0x45, 0x4c, 0x46, 2, 1, 1 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18, 2), rid == "linux-arm64" ? (ushort)183 : (ushort)62);

        return bytes;
    }
}

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

    /// <summary>Managed PE и ELF не принимаются как Windows native; неизвестный RID отклоняется.</summary>
    [Fact]
    public void NativeRejectsManagedAndMixedOperatingSystems()
    {
        // Arrange
        string root = DeliveryTestPaths.SdkRoot;

        // Act
        byte[] managed = File.ReadAllBytes(Path.Combine(root, "PostgreSql", "win-x64", "lib", "AgentBridge.dll"));
        byte[] linux = ElfHeader("linux-x64");

        // Assert
        Assert.Throws<InvalidDataException>(() => NativeAssetMetadata.Validate(managed, "win-x64"));
        Assert.Throws<BadImageFormatException>(() => NativeAssetMetadata.Validate(linux, "win-x64"));
        Assert.Throws<ArgumentException>(() => NativeAssetMetadata.Validate(linux, "unknown"));
    }

    /// <summary>Создаёт синтетический ELF header для negative controls.</summary>
    private static byte[] ElfHeader(string rid)
    {
        byte[] bytes = new byte[64];
        new byte[] { 0x7f, 0x45, 0x4c, 0x46, 2, 1, 1 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18, 2), rid == "linux-arm64" ? (ushort)183 : (ushort)62);

        return bytes;
    }
}

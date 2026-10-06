using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Проверяет формат и архитектуру native asset без загрузки кода.</summary>
internal static class NativeAssetMetadata
{
    /// <summary>Отклоняет чужой RID, managed PE, усечённый файл и несовместимые ELF class/endian/machine.</summary>
    public static void Validate(byte[] bytes, string rid)
    {
        if (rid == "win-x64")
        {
            using MemoryStream stream = new(bytes, writable: false);
            using PEReader pe = new(stream);
            if (pe.HasMetadata || pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            {
                throw new InvalidDataException("Ожидается native PE AMD64 без managed metadata.");
            }
            return;
        }

        ushort machine = rid switch
        {
            "linux-x64" => 62,
            "linux-arm64" => 183,
            _ => throw new ArgumentException("Неизвестный RID.", nameof(rid))
        };
        if (bytes.Length < 64 || !bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46 }) ||
            bytes[4] != 2 || bytes[5] != 1 || bytes[6] != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(18, 2)) != machine)
        {
            throw new InvalidDataException("Ожидается ELF64 little-endian выбранной архитектуры.");
        }
    }
}

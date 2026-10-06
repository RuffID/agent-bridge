using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Сохраняет ordinal идентичность владельца в SQL Server без строкового padding, нормализации и потери UTF-16 code units.</summary>
internal class OrdinalOwnerConverter() : ValueConverter<string, byte[]>(owner => Encode(owner), bytes => Decode(bytes))
{
    /// <summary>Кодирует каждый UTF-16 code unit явно в little-endian, включая непарные суррогаты.</summary>
    private static byte[] Encode(string owner)
    {
        byte[] bytes = new byte[checked(owner.Length * 2)];
        for (int index = 0; index < owner.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2, 2), owner[index]);
        }
        return bytes;
    }

    /// <summary>Восстанавливает исходную строку; нечётная длина означает повреждённые данные.</summary>
    private static string Decode(byte[] bytes)
    {
        if (bytes.Length % 2 != 0)
        {
            throw new InvalidOperationException("Повреждённая ordinal идентичность владельца.");
        }
        char[] characters = new char[bytes.Length / 2];
        for (int index = 0; index < characters.Length; index++)
        {
            characters[index] = (char)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2, 2));
        }
        return new string(characters);
    }
}

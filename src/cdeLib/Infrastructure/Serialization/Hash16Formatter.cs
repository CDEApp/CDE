using cdeLib.Entities;
using MessagePack;
using MessagePack.Formatters;

namespace cdeLib.Infrastructure.Serialization;

/// <summary>
/// Custom MessagePack formatter for Hash16 that treats zero values as optional/null.
/// This allows non-nullable Hash16 struct (16 bytes) instead of Hash16? (24 bytes)
/// while maintaining backward compatibility with existing catalogs.
/// </summary>
public sealed class Hash16Formatter : IMessagePackFormatter<Hash16>
{
    public void Serialize(ref MessagePackWriter writer, Hash16 value, MessagePackSerializerOptions options)
    {
        // If hash is not set (zero/default), serialize as nil to save space
        if (!value.IsSet)
        {
            writer.WriteNil();
            return;
        }

        // Serialize as an array of 2 ulongs
        writer.WriteArrayHeader(2);
        writer.Write(value.HashA);
        writer.Write(value.HashB);
    }

    public Hash16 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        // Handle nil (missing/null hash from old catalogs)
        if (reader.TryReadNil())
        {
            return default; // Returns Hash16 with HashA=0, HashB=0 (IsSet=false)
        }

        // Read array of 2 ulongs
        var count = reader.ReadArrayHeader();
        if (count != 2)
        {
            throw new MessagePackSerializationException($"Invalid Hash16 array length: {count}, expected 2");
        }

        var hashA = reader.ReadUInt64();
        var hashB = reader.ReadUInt64();

        return new Hash16 { HashA = hashA, HashB = hashB };
    }
}

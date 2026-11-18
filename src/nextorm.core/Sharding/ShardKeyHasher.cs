using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace NextORM.Core;

/// <summary>Encodes shard-key tuples canonically and hashes them with a fixed, non-cryptographic basis.</summary>
internal static class ShardKeyHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong FixedSeed = 0xD6E8FEB86659FD93UL;
    private const ulong Prime = 1099511628211UL;

    internal static byte[] GetCanonicalBytes(IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, values.Count);
        stream.Write(header);

        for (var index = 0; index < values.Count; index++)
        {
            var (tag, payload) = Encode(values[index]);
            stream.WriteByte(tag);
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            stream.Write(header);
            stream.Write(payload);
        }

        return stream.ToArray();
    }

    internal static ulong Hash(IReadOnlyList<object?> values)
        => HashBytes(GetCanonicalBytes(values));

    internal static ulong HashBytes(ReadOnlySpan<byte> bytes)
    {
        var hash = OffsetBasis ^ FixedSeed;
        foreach (var value in bytes)
        {
            hash ^= value;
            hash = unchecked(hash * Prime);
        }
        return hash;
    }

    private static (byte Tag, byte[] Payload) Encode(object? value)
    {
        Span<byte> buffer = stackalloc byte[16];
        switch (value)
        {
            case null:
                return (0, []);
            case int number:
                BinaryPrimitives.WriteInt32LittleEndian(buffer, number);
                return (1, buffer[..sizeof(int)].ToArray());
            case long number:
                BinaryPrimitives.WriteInt64LittleEndian(buffer, number);
                return (2, buffer[..sizeof(long)].ToArray());
            case short number:
                BinaryPrimitives.WriteInt16LittleEndian(buffer, number);
                return (3, buffer[..sizeof(short)].ToArray());
            case byte number:
                buffer[0] = number;
                return (4, [buffer[0]]);
            case Guid guid:
                return (5, Encoding.ASCII.GetBytes(guid.ToString("N", CultureInfo.InvariantCulture)));
            case string text:
                return (6, Encoding.UTF8.GetBytes(text));
            case bool boolean:
                buffer[0] = boolean ? (byte)1 : (byte)0;
                return (7, [buffer[0]]);
            case DateTime dateTime:
                BinaryPrimitives.WriteInt64LittleEndian(buffer, dateTime.Ticks);
                buffer[sizeof(long)] = (byte)dateTime.Kind;
                return (8, buffer[..(sizeof(long) + 1)].ToArray());
            case DateTimeOffset dateTimeOffset:
                BinaryPrimitives.WriteInt64LittleEndian(buffer, dateTimeOffset.Ticks);
                BinaryPrimitives.WriteInt64LittleEndian(buffer[sizeof(long)..], dateTimeOffset.Offset.Ticks);
                return (9, buffer.ToArray());
            case decimal decimalValue:
                var bits = decimal.GetBits(decimalValue);
                for (var index = 0; index < bits.Length; index++)
                    BinaryPrimitives.WriteInt32LittleEndian(buffer[(index * sizeof(int))..], bits[index]);
                return (10, buffer.ToArray());
            default:
                throw new ArgumentException(
                    $"Shard-key value type '{value.GetType()}' is not supported.", nameof(value));
        }
    }
}

using System.Globalization;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// The typed per-column formatters invoked from the compiled CSV column plan. Every method takes the
/// concrete CLR type, so the write path never boxes a value or falls back to <c>object</c>; the calling
/// expression tree is compiled once per query and dispatches straight into the matching method.
/// </summary>
internal static class CsvValueFormatter
{
    public static void WriteBoolean(CsvRowBuffer buffer, bool value)
    {
        if (buffer.Policy.ValueTransform is { } transform)
        {
            buffer.WriteField(transform(value));
            return;
        }

        buffer.WriteEscapedField(value ? "true"u8 : "false"u8);
    }

    public static void WriteByte(CsvRowBuffer buffer, byte value) => WriteFormatted(buffer, value);

    public static void WriteInt16(CsvRowBuffer buffer, short value) => WriteFormatted(buffer, value);

    public static void WriteInt32(CsvRowBuffer buffer, int value) => WriteFormatted(buffer, value);

    public static void WriteUInt32(CsvRowBuffer buffer, uint value) => WriteFormatted(buffer, value);

    public static void WriteInt64(CsvRowBuffer buffer, long value) => WriteFormatted(buffer, value);

    public static void WriteUInt64(CsvRowBuffer buffer, ulong value) => WriteFormatted(buffer, value);

    public static void WriteSingle(CsvRowBuffer buffer, float value) => WriteFormatted(buffer, value);

    public static void WriteDouble(CsvRowBuffer buffer, double value) => WriteFormatted(buffer, value);

    public static void WriteDecimal(CsvRowBuffer buffer, decimal value) => WriteFormatted(buffer, value);

    public static void WriteGuid(CsvRowBuffer buffer, Guid value) => WriteFormatted(buffer, value, "D");

    public static void WriteDateTime(CsvRowBuffer buffer, DateTime value) => WriteFormatted(buffer, value, "O");

    public static void WriteDateTimeOffset(CsvRowBuffer buffer, DateTimeOffset value) => WriteFormatted(buffer, value, "O");

    public static void WriteTimeSpan(CsvRowBuffer buffer, TimeSpan value) => WriteFormatted(buffer, value, "c");

    public static void WriteString(CsvRowBuffer buffer, string? value) => buffer.WriteStringFieldWithPolicy(value);

    public static void WriteBytes(CsvRowBuffer buffer, byte[]? value) => buffer.WriteBytesField(value);

    internal static void WriteFormatted<T>(CsvRowBuffer buffer, T value, ReadOnlySpan<char> format = default)
        where T : struct, IUtf8SpanFormattable
    {
        if (buffer.Policy.ValueTransform is { } transform)
        {
            buffer.WriteField(transform(value));
            return;
        }

        Span<byte> scratch = stackalloc byte[96];
        if (!value.TryFormat(scratch, out var written, format, CultureInfo.InvariantCulture))
            throw new InvalidOperationException(
                $"The value of type {typeof(T).Name} did not fit the {scratch.Length}-byte CSV scratch buffer.");

        buffer.WriteEscapedField(scratch[..written]);
    }
}

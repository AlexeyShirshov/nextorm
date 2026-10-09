using System.Buffers;
using System.Data.Common;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// SQL Server native JSON streaming: the eligibility predicate for the database-side <c>FOR JSON</c>
/// fast-path and the bounded UTF-16 to UTF-8 pump that copies the single document column to a
/// caller-owned destination.
/// </summary>
/// <remarks>
/// This type is provider-neutral but only ever engaged when the dialect reports
/// <see cref="ISqlDialect.SupportsForJson"/> and the request is otherwise eligible. The pump owns only
/// fixed-size rented buffers, so its memory use is independent of the document size, and it never
/// flushes or disposes the destination. Output is logically equivalent to the managed writer; the
/// database and System.Text.Json differ only lexically (for example SQL Server leaves non-ASCII and
/// HTML-sensitive characters unescaped), which the streaming documentation calls out.
/// </remarks>
internal static class JsonNativeStream
{
    // Fixed pump buffers: a document of any size is copied in these chunks, so app-owned memory does not
    // grow with the document. Rented from the shared pool and always returned.
    private const int CharBufferSize = 4096;
    private const int ByteBufferSize = 4096;

    private static readonly byte[] EmptyArray = "[]"u8.ToArray();

    /// <summary>
    /// Whether a validated shape plan may be served by the database-side <c>FOR JSON</c> document
    /// transport instead of managed row-by-row serialization. The predicate is deliberately narrow: any
    /// condition that could change null, type, alias or framing semantics returns <see langword="false"/>
    /// so the caller falls back to the managed writer before execution (never a retry after a native
    /// failure).
    /// </summary>
    /// <param name="plan">The frozen managed shape plan.</param>
    /// <param name="options">The requested container options.</param>
    /// <returns><see langword="true"/> only when every native condition holds.</returns>
    internal static bool IsEligible(JsonShapePlan plan, JsonStreamOptions options)
    {
        // A scalar projection streams a bare JSON array of values, but FOR JSON always wraps columns in
        // objects; a recursive shape is reconstructed by managed presence/alias logic.
        if (plan.IsScalar || plan.Shape is not null)
            return false;

        // The native document is a bare array produced by FOR JSON PATH with no root wrapper, no
        // indentation and alias-equal property names; every other option needs the managed writer.
        if (options.Mode != JsonStreamMode.Array
            || options.Root is not null
            || options.WriteIndented
            || options.PropertyNamingPolicy is not null)
            return false;

        var columns = plan.Columns;
        if (columns.Length == 0)
            return false;

        for (var i = 0; i < columns.Length; i++)
        {
            var column = columns[i];

            // *OrDefault projections substitute default(T) for SQL NULL in the managed writer, which the
            // database cannot reproduce; keep them managed.
            if (column.DefaultOnNull)
                return false;

            // FOR JSON PATH treats a dot in a column alias as a nested path, so only a plain identifier
            // is a proven property name. Anything else stays managed.
            if (!IsSimpleAlias(column.Name))
                return false;

            // A numeric enum is classified as a number over its underlying integral type; the declared
            // type still exposes the enum, so it is excluded (D178 keeps every enum managed).
            if (column.DeclaredType is { } declared && (Nullable.GetUnderlyingType(declared) ?? declared).IsEnum)
                return false;

            // Only a proven direct pass-through may be served natively: the managed writer reads the
            // runtime provider field type and narrows/converts it, whereas FOR JSON emits the raw database
            // value. A raw accessor or a wider provider binding would therefore diverge from managed.
            if (!column.IsDirectPassThrough)
                return false;

            if (column.ProviderType is { } providerType && providerType != column.ValueType)
                return false;

            var admitted = column.Kind switch
            {
                JsonWriteKind.String => column.ValueType == typeof(string),
                JsonWriteKind.Boolean => column.ValueType == typeof(bool),
                JsonWriteKind.Number => column.ValueType == typeof(short) || column.ValueType == typeof(int) || column.ValueType == typeof(long),
                _ => false,
            };

            if (!admitted)
                return false;
        }

        return true;
    }

    /// <summary>
    /// True when <paramref name="name"/> is a plain ASCII identifier that FOR JSON PATH emits verbatim
    /// as a property name (no path separators or other special characters).
    /// </summary>
    private static bool IsSimpleAlias(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var first = name[0];
        if (!(first == '_' || (first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z')))
            return false;

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!(c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Copies the reader's single native <c>FOR JSON</c> document column to <paramref name="output"/> in
    /// bounded character chunks, transcoding UTF-16 to UTF-8. An empty result set (SQL Server emits no
    /// row) or a SQL NULL document becomes the empty array <c>[]</c>.
    /// </summary>
    /// <param name="reader">The open, positioned native document reader.</param>
    /// <param name="output">The caller-owned destination; it is never flushed or disposed.</param>
    internal static void WriteDocument(DbDataReader reader, Stream output)
    {
        ValidateReader(reader);

        var charBuffer = ArrayPool<char>.Shared.Rent(CharBufferSize);
        var byteBuffer = ArrayPool<byte>.Shared.Rent(ByteBufferSize);
        try
        {
            var encoder = Encoding.UTF8.GetEncoder();
            var wroteAny = false;
            // SQL Server splits a large FOR JSON document across multiple result rows of chunk size
            // (for example 2033 characters); every row is a contiguous part of the one document and
            // must be concatenated in order. A surrogate pair may straddle a row boundary, so the
            // encoder is never flushed between rows.
            while (reader.Read())
            {
                if (reader.IsDBNull(0))
                    continue;

                long offset = 0;
                while (true)
                {
                    var charsRead = reader.GetChars(0, offset, charBuffer, 0, charBuffer.Length);
                    if (charsRead <= 0)
                        break;

                    offset += charsRead;
                    wroteAny = true;
                    Transcode(encoder, charBuffer.AsSpan(0, (int)charsRead), byteBuffer, output);
                }
            }

            FlushEncoder(encoder, byteBuffer, output);
            if (!wroteAny)
                output.Write(EmptyArray);
        }
        finally
        {
            // Clear before returning: the rented buffers held document characters that must not leak to a
            // later renter of the shared pool.
            ArrayPool<char>.Shared.Return(charBuffer, clearArray: true);
            ArrayPool<byte>.Shared.Return(byteBuffer, clearArray: true);
        }
    }

    /// <summary>
    /// Asynchronous counterpart of <see cref="WriteDocument"/>: writes each transcoded chunk with
    /// <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> and observes the token
    /// between chunks.
    /// </summary>
    /// <param name="reader">The open, positioned native document reader.</param>
    /// <param name="output">The caller-owned destination; it is never flushed or disposed.</param>
    /// <param name="cancellationToken">A token observed between chunks and while writing.</param>
    /// <returns>A task that completes when the whole document has been copied.</returns>
    internal static async Task WriteDocumentAsync(DbDataReader reader, Stream output, CancellationToken cancellationToken)
    {
        ValidateReader(reader);

        var charBuffer = ArrayPool<char>.Shared.Rent(CharBufferSize);
        var byteBuffer = ArrayPool<byte>.Shared.Rent(ByteBufferSize);
        try
        {
            var encoder = Encoding.UTF8.GetEncoder();
            var wroteAny = false;
            // See WriteDocument: a large document is split across reader rows that form one document.
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (reader.IsDBNull(0))
                    continue;

                long offset = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var charsRead = reader.GetChars(0, offset, charBuffer, 0, charBuffer.Length);
                    if (charsRead <= 0)
                        break;

                    offset += charsRead;
                    wroteAny = true;
                    var charPos = 0;
                    while (charPos < charsRead)
                    {
                        encoder.Convert(charBuffer.AsSpan(charPos, (int)charsRead - charPos), byteBuffer, flush: false, out var charsUsed, out var bytesUsed, out var completed);
                        if (bytesUsed > 0)
                            await output.WriteAsync(byteBuffer.AsMemory(0, bytesUsed), cancellationToken).ConfigureAwait(false);

                        charPos += charsUsed;
                        if (completed)
                            break;

                        if (charsUsed == 0 && bytesUsed == 0)
                            throw new InvalidOperationException("JSON streaming native transport could not make progress while transcoding UTF-16 to UTF-8.");
                    }
                }
            }

            encoder.Convert(ReadOnlySpan<char>.Empty, byteBuffer, flush: true, out _, out var remaining, out _);
            if (remaining > 0)
                await output.WriteAsync(byteBuffer.AsMemory(0, remaining), cancellationToken).ConfigureAwait(false);

            if (!wroteAny)
                await output.WriteAsync(EmptyArray, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Clear before returning: see WriteDocument.
            ArrayPool<char>.Shared.Return(charBuffer, clearArray: true);
            ArrayPool<byte>.Shared.Return(byteBuffer, clearArray: true);
        }
    }

    private static void ValidateReader(DbDataReader reader)
    {
        if (reader.FieldCount != 1 || reader.GetFieldType(0) != typeof(string))
        {
            var fieldType = reader.FieldCount > 0 ? reader.GetFieldType(0).FullName : "none";
            throw new NotSupportedException(
                $"JSON streaming validation [reader-binding]: the native FOR JSON transport expected one string document column, but the reader reported {reader.FieldCount} column(s) with first field type '{fieldType}'.");
        }
    }

    // Encoder.Convert with flush=false may leave a trailing high surrogate pending; a later chunk (or the
    // final flush) completes it. An actual unpaired surrogate is replaced with U+FFFD exactly as
    // System.Text.Json does, so the emitted UTF-8 is always valid.
    private static void Transcode(Encoder encoder, ReadOnlySpan<char> chars, byte[] byteBuffer, Stream output)
    {
        Span<byte> bytes = byteBuffer;
        while (true)
        {
            encoder.Convert(chars, bytes, flush: false, out var charsUsed, out var bytesUsed, out var completed);
            if (bytesUsed > 0)
                output.Write(byteBuffer, 0, bytesUsed);

            chars = chars[charsUsed..];
            if (completed)
                return;

            if (charsUsed == 0 && bytesUsed == 0)
                throw new InvalidOperationException("JSON streaming native transport could not make progress while transcoding UTF-16 to UTF-8.");
        }
    }

    private static void FlushEncoder(Encoder encoder, byte[] byteBuffer, Stream output)
    {
        Span<byte> bytes = byteBuffer;
        encoder.Convert(ReadOnlySpan<char>.Empty, bytes, flush: true, out _, out var bytesUsed, out _);
        if (bytesUsed > 0)
            output.Write(byteBuffer, 0, bytesUsed);
    }

}

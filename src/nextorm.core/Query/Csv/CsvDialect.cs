using System.Text;

namespace NextORM.Core;

/// <summary>
/// The resolved CSV dialect of a single <c>WriteCsv</c> call: the delimiter and the pre-computed
/// checks used by the RFC 4180 escaping rules. A field is quoted only when it contains the delimiter,
/// a double quote, CR or LF.
/// </summary>
internal sealed class CsvDialect
{
    private const byte Quote = (byte)'"';
    private const byte CarriageReturn = (byte)'\r';
    private const byte LineFeed = (byte)'\n';

    private readonly byte[] _delimiterUtf8;

    public CsvDialect(char delimiter)
    {
        Delimiter = delimiter;
        // Delimiters are single UTF-16 code units; encode the one code unit to compare it against the
        // UTF-8 bytes the fields are written as.
        _delimiterUtf8 = Encoding.UTF8.GetBytes(new[] { delimiter });
    }

    /// <summary>The configured field delimiter.</summary>
    public char Delimiter { get; }

    /// <summary>The delimiter encoded as UTF-8, used to write it between fields and to detect it in a field.</summary>
    public ReadOnlySpan<byte> DelimiterUtf8 => _delimiterUtf8;

    /// <summary>Whether a formatted byte field must be quoted to stay a single CSV field.</summary>
    public bool NeedsQuoting(ReadOnlySpan<byte> field)
    {
        foreach (var value in field)
        {
            if (value == Quote || value == CarriageReturn || value == LineFeed)
                return true;
        }

        return ContainsDelimiter(field);
    }

    /// <summary>Whether a character field must be quoted to stay a single CSV field.</summary>
    public bool NeedsQuoting(ReadOnlySpan<char> field)
    {
        foreach (var value in field)
        {
            if (value == '"' || value == '\r' || value == '\n' || value == Delimiter)
                return true;
        }

        return false;
    }

    private bool ContainsDelimiter(ReadOnlySpan<byte> field)
    {
        var delimiter = _delimiterUtf8;
        if (delimiter.Length == 1)
            return field.IndexOf(delimiter[0]) >= 0;

        for (var i = 0; i + delimiter.Length <= field.Length; i++)
        {
            var matches = true;
            for (var j = 0; j < delimiter.Length; j++)
            {
                if (field[i + j] != delimiter[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return true;
        }

        return false;
    }
}

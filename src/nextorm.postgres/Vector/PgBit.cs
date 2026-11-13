namespace NextORM.Postgres;

/// <summary>
/// A PostgreSQL <c>bit</c> string: a fixed-length sequence of bits, the CLR representation of
/// pgvector's binary vectors used with the Hamming (<c>&lt;~&gt;</c>) and Jaccard (<c>&lt;%&gt;</c>)
/// distance operators.
/// </summary>
public readonly struct PgBit : IEquatable<PgBit>
{
    private readonly bool[]? _bits;

    /// <summary>Creates a bit string from its bits, most significant (leftmost) first.</summary>
    /// <param name="bits">The bits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bits"/> is null.</exception>
    public PgBit(IEnumerable<bool> bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        _bits = bits.ToArray();
    }

    /// <summary>Creates a bit string from PostgreSQL's binary text form, such as <c>10110</c>.</summary>
    /// <param name="bits">A string containing only <c>0</c> and <c>1</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bits"/> is null.</exception>
    /// <exception cref="FormatException">The text contains a character other than <c>0</c> or <c>1</c>.</exception>
    public PgBit(string bits) => _bits = ParseBits(bits);

    /// <summary>Gets the number of bits.</summary>
    public int Length => _bits?.Length ?? 0;

    /// <summary>Gets a bit by its zero-based index, leftmost first.</summary>
    public bool this[int index] => (_bits ?? Array.Empty<bool>())[index];

    /// <summary>Returns a defensive copy of the bits.</summary>
    public bool[] ToArray() => _bits is null ? Array.Empty<bool>() : (bool[])_bits.Clone();

    /// <summary>Formats the value using PostgreSQL's binary text representation, such as <c>10110</c>.</summary>
    public override string ToString()
    {
        var bits = _bits ?? Array.Empty<bool>();
        var characters = new char[bits.Length];
        for (var i = 0; i < bits.Length; i++) characters[i] = bits[i] ? '1' : '0';
        return new string(characters);
    }

    /// <summary>Parses PostgreSQL's binary text representation (a string of <c>0</c> and <c>1</c>).</summary>
    /// <param name="text">The text to parse.</param>
    /// <exception cref="FormatException">The text contains a character other than <c>0</c> or <c>1</c>.</exception>
    public static PgBit Parse(string text) => new(ParseBits(text));

    private static bool[] ParseBits(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bits = new bool[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            bits[i] = text[i] switch
            {
                '0' => false,
                '1' => true,
                _ => throw new FormatException("A PostgreSQL bit string may contain only '0' and '1'.")
            };
        }
        return bits;
    }

    /// <inheritdoc />
    public bool Equals(PgBit other) => (_bits ?? Array.Empty<bool>()).AsSpan().SequenceEqual(other._bits ?? Array.Empty<bool>());
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PgBit other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var bit in _bits ?? Array.Empty<bool>()) hash.Add(bit);
        return hash.ToHashCode();
    }
}

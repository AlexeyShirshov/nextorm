using System.Globalization;
using System.Text;

namespace NextORM.Postgres;

/// <summary>A PostgreSQL <c>halfvec</c> (pgvector half-precision) value backed by finite coordinates.</summary>
public readonly struct PgHalfVector : IEquatable<PgHalfVector>
{
    private readonly Half[]? _values;

    /// <summary>Creates a half vector from half-precision coordinates.</summary>
    /// <param name="values">The coordinates; all values must be finite.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">A coordinate is NaN or infinity.</exception>
    public PgHalfVector(IEnumerable<Half> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values.ToArray();
        if (_values.Any(static value => !Half.IsFinite(value)))
            throw new ArgumentException("Vector coordinates must be finite.", nameof(values));
    }

    /// <summary>Creates a half vector from single-precision coordinates, rounding each one to half precision.</summary>
    /// <param name="values">The coordinates; all values must be finite.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">A coordinate is NaN, infinity, or outside the half-precision range.</exception>
    public PgHalfVector(IEnumerable<float> values)
        : this(values is null ? throw new ArgumentNullException(nameof(values)) : ConvertAll(values))
    {
    }

    /// <summary>Gets the vector dimensions.</summary>
    public int Dimensions => _values?.Length ?? 0;

    /// <summary>Gets a coordinate by its zero-based index.</summary>
    public Half this[int index] => (_values ?? Array.Empty<Half>())[index];

    /// <summary>Returns a defensive copy of the coordinates.</summary>
    public Half[] ToArray() => _values is null ? Array.Empty<Half>() : (Half[])_values.Clone();

    /// <summary>Formats the value using pgvector's text representation.</summary>
    public override string ToString()
    {
        var values = _values ?? Array.Empty<Half>();
        var result = new StringBuilder("[");
        for (var i = 0; i < values.Length; i++)
        {
            if (i != 0) result.Append(',');
            result.Append(values[i].ToString(CultureInfo.InvariantCulture));
        }
        return result.Append(']').ToString();
    }

    /// <summary>Parses pgvector's bracketed text representation.</summary>
    /// <param name="text">A value such as <c>[1,2.5]</c>.</param>
    /// <exception cref="FormatException">The text is not a bracketed list of finite half-precision numbers.</exception>
    public static PgHalfVector Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
            throw new FormatException("A pgvector half vector must be enclosed in brackets.");
        var content = text.AsSpan(1, text.Length - 2);
        if (content.IsEmpty) return new PgHalfVector(Array.Empty<Half>());
        var parts = content.ToString().Split(',');
        var values = new Half[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!Half.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !Half.IsFinite(values[i]))
                throw new FormatException("A pgvector half vector must contain finite half-precision numbers.");
        return new PgHalfVector(values);
    }

    private static Half[] ConvertAll(IEnumerable<float> values)
    {
        var converted = values.Select(static value => (Half)value).ToArray();
        if (converted.Any(static value => !Half.IsFinite(value)))
            throw new ArgumentException("Vector coordinates must be finite and within the half-precision range.", nameof(values));
        return converted;
    }

    /// <inheritdoc />
    public bool Equals(PgHalfVector other) => (_values ?? Array.Empty<Half>()).AsSpan().SequenceEqual(other._values ?? Array.Empty<Half>());
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PgHalfVector other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _values ?? Array.Empty<Half>()) hash.Add(value);
        return hash.ToHashCode();
    }
}

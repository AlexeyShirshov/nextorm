using System.Globalization;
using System.Text;

namespace NextORM.Postgres;

/// <summary>A PostgreSQL <c>vector</c> value backed by finite single-precision coordinates.</summary>
public readonly struct PgVector : IEquatable<PgVector>
{
    private readonly float[]? _values;

    /// <summary>Creates a vector from its coordinates.</summary>
    /// <param name="values">The coordinates; all values must be finite.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">A coordinate is NaN or infinity.</exception>
    public PgVector(IEnumerable<float> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values.ToArray();
        if (_values.Any(static value => !float.IsFinite(value)))
            throw new ArgumentException("Vector coordinates must be finite.", nameof(values));
    }

    /// <summary>Gets the vector dimensions.</summary>
    public int Dimensions => _values?.Length ?? 0;

    /// <summary>Gets a coordinate by its zero-based index.</summary>
    public float this[int index] => (_values ?? Array.Empty<float>())[index];

    /// <summary>Returns a defensive copy of the coordinates.</summary>
    public float[] ToArray() => _values is null ? Array.Empty<float>() : (float[])_values.Clone();

    /// <summary>Formats the value using pgvector's text representation.</summary>
    public override string ToString()
    {
        var values = _values ?? Array.Empty<float>();
        var result = new StringBuilder("[");
        for (var i = 0; i < values.Length; i++)
        {
            if (i != 0) result.Append(',');
            result.Append(values[i].ToString("R", CultureInfo.InvariantCulture));
        }
        return result.Append(']').ToString();
    }

    /// <summary>Parses pgvector's bracketed text representation.</summary>
    /// <param name="text">A value such as <c>[1,2.5]</c>.</param>
    public static PgVector Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
            throw new FormatException("A pgvector value must be enclosed in brackets.");
        var content = text.AsSpan(1, text.Length - 2);
        if (content.IsEmpty) return new PgVector(Array.Empty<float>());
        var parts = content.ToString().Split(',');
        var values = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !float.IsFinite(values[i]))
                throw new FormatException("A pgvector value must contain finite single-precision numbers.");
        return new PgVector(values);
    }

    /// <inheritdoc />
    public bool Equals(PgVector other) => (_values ?? Array.Empty<float>()).AsSpan().SequenceEqual(other._values ?? Array.Empty<float>());
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PgVector other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _values ?? Array.Empty<float>()) hash.Add(value);
        return hash.ToHashCode();
    }
}

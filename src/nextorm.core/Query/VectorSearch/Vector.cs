using System.Globalization;
using System.Text;

namespace NextORM.Core;

/// <summary>A provider-independent vector backed by finite single-precision coordinates.</summary>
public sealed class Vector : IVectorValue
{
    private readonly float[] _coordinates;

    /// <summary>Creates a vector from its coordinates.</summary>
    /// <param name="coordinates">The coordinates; every coordinate must be finite.</param>
    /// <exception cref="ArgumentNullException"><paramref name="coordinates"/> is null.</exception>
    /// <exception cref="ArgumentException">A coordinate is NaN or infinity.</exception>
    public Vector(IEnumerable<float> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        _coordinates = coordinates.ToArray();
        if (_coordinates.Any(static value => !float.IsFinite(value)))
            throw new ArgumentException("Vector coordinates must be finite.", nameof(coordinates));
    }

    /// <summary>Gets the number of coordinates.</summary>
    public int Dimensions => _coordinates.Length;

    /// <summary>Gets a coordinate by its zero-based index.</summary>
    public float this[int index] => _coordinates[index];

    /// <summary>Returns a defensive copy of the coordinates.</summary>
    public float[] ToArray() => (float[])_coordinates.Clone();

    /// <summary>Formats the coordinates using pgvector's dense vector text representation.</summary>
    public override string ToString()
    {
        var result = new StringBuilder("[");
        for (var i = 0; i < _coordinates.Length; i++)
        {
            if (i != 0) result.Append(',');
            result.Append(_coordinates[i].ToString("R", CultureInfo.InvariantCulture));
        }
        return result.Append(']').ToString();
    }

    /// <summary>Parses a bracketed list of finite single-precision coordinates.</summary>
    /// <param name="text">A value such as <c>[1,2.5]</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">The value is not a bracketed list of finite numbers.</exception>
    public static Vector Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
            throw new FormatException("A vector value must be enclosed in brackets.");

        var content = text.AsSpan(1, text.Length - 2);
        if (content.IsEmpty)
            return new Vector(Array.Empty<float>());

        var parts = content.ToString().Split(',');
        var values = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])
                || !float.IsFinite(values[i]))
                throw new FormatException("A vector value must contain finite single-precision numbers.");
        }

        return new Vector(values);
    }
}

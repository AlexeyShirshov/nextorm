using System.Globalization;
using System.Text;

namespace NextORM.Core;

/// <summary>A provider-independent vector backed by finite single-precision coordinates.</summary>
public sealed class Vector : IVectorValue, IEquatable<Vector>
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

    /// <summary>Determines whether this vector has the same coordinates as another vector.</summary>
    /// <param name="other">The vector to compare with this vector, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if both vectors have the same dimension and each corresponding coordinate
    /// has the same exact single-precision value; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(Vector? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null || _coordinates.Length != other._coordinates.Length)
            return false;

        for (var i = 0; i < _coordinates.Length; i++)
        {
            if (_coordinates[i] != other._coordinates[i])
                return false;
        }

        return true;
    }

    /// <summary>Determines whether the specified object is a vector with the same exact coordinates.</summary>
    /// <param name="obj">The object to compare with this vector.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="obj"/> is a <see cref="Vector"/> with the same dimension
    /// and exact coordinate values; otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj) => obj is Vector other && Equals(other);

    /// <summary>Returns a hash code for this vector's exact coordinate values.</summary>
    /// <returns>A process-local hash code consistent with vector equality.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_coordinates.Length);
        for (var i = 0; i < _coordinates.Length; i++)
        {
            var coordinate = _coordinates[i];
            hash.Add(coordinate == 0f ? 0f : coordinate);
        }

        return hash.ToHashCode();
    }

    /// <summary>Determines whether two vectors have the same exact coordinates.</summary>
    /// <param name="left">The first vector, or <see langword="null"/>.</param>
    /// <param name="right">The second vector, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if both operands are null or both vectors have the same dimension and exact
    /// coordinate values; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator ==(Vector? left, Vector? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>Determines whether two vectors have different exact coordinates.</summary>
    /// <param name="left">The first vector, or <see langword="null"/>.</param>
    /// <param name="right">The second vector, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if exactly one operand is null or the vectors have different dimensions or
    /// coordinate values; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator !=(Vector? left, Vector? right) => !(left == right);

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

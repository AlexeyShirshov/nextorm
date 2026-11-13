using System.Globalization;
using System.Text;

namespace NextORM.Postgres;

/// <summary>
/// A PostgreSQL <c>sparsevec</c> (pgvector sparse) value: a fixed number of dimensions with only the
/// non-zero coordinates stored, as sorted zero-based indices and their finite single-precision values.
/// </summary>
public readonly struct PgSparseVector : IEquatable<PgSparseVector>
{
    private readonly int[]? _indices;
    private readonly float[]? _values;
    private readonly int _dimensions;

    /// <summary>Creates a sparse vector from a dense coordinate list, dropping zero coordinates.</summary>
    /// <param name="dense">The dense coordinates; every non-zero value must be finite.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dense"/> is null.</exception>
    /// <exception cref="ArgumentException">A non-zero coordinate is NaN or infinity.</exception>
    public PgSparseVector(IEnumerable<float> dense)
    {
        ArgumentNullException.ThrowIfNull(dense);
        var values = dense.ToArray();
        var indices = new List<int>();
        var nonZero = new List<float>();
        for (var i = 0; i < values.Length; i++)
        {
            if (!float.IsFinite(values[i]))
                throw new ArgumentException("Sparse vector values must be finite.", nameof(dense));
            if (values[i] == 0f) continue;
            indices.Add(i);
            nonZero.Add(values[i]);
        }
        _dimensions = values.Length;
        _indices = indices.ToArray();
        _values = nonZero.ToArray();
    }

    /// <summary>Creates a sparse vector from its zero-based indices and values.</summary>
    /// <param name="dimensions">The number of dimensions; a positive value for a storable vector.</param>
    /// <param name="indices">The zero-based, strictly ascending indices of the non-zero coordinates.</param>
    /// <param name="values">The finite non-zero coordinate values, aligned with <paramref name="indices"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensions"/> is negative.</exception>
    /// <exception cref="ArgumentException">The index and value counts differ, the indices are not strictly ascending, an index is outside the vector, or a value is not a finite non-zero number.</exception>
    public PgSparseVector(int dimensions, IEnumerable<int> indices, IEnumerable<float> values)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimensions);
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(values);

        var indexArray = indices.ToArray();
        var valueArray = values.ToArray();
        if (indexArray.Length != valueArray.Length)
            throw new ArgumentException("The indices and values must have the same length.", nameof(values));
        for (var i = 0; i < indexArray.Length; i++)
        {
            if (indexArray[i] < 0 || indexArray[i] >= dimensions)
                throw new ArgumentException($"The index {indexArray[i]} at position {i} is outside the {dimensions}-dimensional vector.", nameof(indices));
            if (i > 0 && indexArray[i] <= indexArray[i - 1])
                throw new ArgumentException("The indices must be unique and strictly ascending.", nameof(indices));
            if (!float.IsFinite(valueArray[i]) || valueArray[i] == 0f)
                throw new ArgumentException("The sparse vector values must be finite and non-zero.", nameof(values));
        }

        _dimensions = dimensions;
        _indices = indexArray;
        _values = valueArray;
    }

    /// <summary>Gets the number of dimensions, including the coordinates that are not stored.</summary>
    public int Dimensions => _dimensions;

    /// <summary>Gets the number of stored (non-zero) coordinates.</summary>
    public int Count => _indices?.Length ?? 0;

    /// <summary>Gets the zero-based indices of the stored coordinates, ascending.</summary>
    public ReadOnlyMemory<int> Indices => _indices ?? ReadOnlyMemory<int>.Empty;

    /// <summary>Gets the values of the stored coordinates, aligned with <see cref="Indices"/>.</summary>
    public ReadOnlyMemory<float> Values => _values ?? ReadOnlyMemory<float>.Empty;

    /// <summary>Expands the value into a dense coordinate array of <see cref="Dimensions"/> length.</summary>
    public float[] ToArray()
    {
        var result = new float[_dimensions];
        var indices = _indices ?? Array.Empty<int>();
        var values = _values ?? Array.Empty<float>();
        for (var i = 0; i < indices.Length; i++) result[indices[i]] = values[i];
        return result;
    }

    /// <summary>Formats the value using pgvector's text representation, such as <c>{1:1.5,3:2}/5</c>.</summary>
    public override string ToString()
    {
        var indices = _indices ?? Array.Empty<int>();
        var values = _values ?? Array.Empty<float>();
        var result = new StringBuilder("{");
        for (var i = 0; i < indices.Length; i++)
        {
            if (i != 0) result.Append(',');
            result.Append((indices[i] + 1).ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(values[i].ToString("R", CultureInfo.InvariantCulture));
        }
        return result.Append("}/").Append(_dimensions.ToString(CultureInfo.InvariantCulture)).ToString();
    }

    /// <summary>Parses pgvector's sparse text representation, such as <c>{1:1.5,3:2}/5</c> (one-based indices).</summary>
    /// <param name="text">The text to parse.</param>
    /// <exception cref="FormatException">The text is not a valid sparse vector.</exception>
    public static PgSparseVector Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length < 4 || text[0] != '{')
            throw new FormatException("A pgvector sparse vector must have the form {index:value,...}/dimensions.");

        var separator = text.LastIndexOf('/');
        if (separator < 2 || text[separator - 1] != '}')
            throw new FormatException("A pgvector sparse vector must have the form {index:value,...}/dimensions.");
        if (!int.TryParse(text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var dimensions) || dimensions < 0)
            throw new FormatException("A pgvector sparse vector must end with a non-negative dimension count.");

        var content = text.AsSpan(1, separator - 2);
        var indices = new List<int>();
        var values = new List<float>();
        if (!content.IsEmpty)
        {
            foreach (var element in content.ToString().Split(','))
            {
                var colon = element.IndexOf(':');
                if (colon <= 0 || !int.TryParse(element.AsSpan(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 1)
                    throw new FormatException("A pgvector sparse vector element must have the form index:value with a one-based index.");
                if (!float.TryParse(element.AsSpan(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
                    throw new FormatException("A pgvector sparse vector must contain finite numbers.");
                indices.Add(index - 1);
                values.Add(value);
            }
        }

        try
        {
            return new PgSparseVector(dimensions, indices, values);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("A pgvector sparse vector is malformed.", exception);
        }
    }

    /// <inheritdoc />
    public bool Equals(PgSparseVector other)
        => _dimensions == other._dimensions
            && (_indices ?? Array.Empty<int>()).AsSpan().SequenceEqual(other._indices ?? Array.Empty<int>())
            && (_values ?? Array.Empty<float>()).AsSpan().SequenceEqual(other._values ?? Array.Empty<float>());
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PgSparseVector other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_dimensions);
        foreach (var index in _indices ?? Array.Empty<int>()) hash.Add(index);
        foreach (var value in _values ?? Array.Empty<float>()) hash.Add(value);
        return hash.ToHashCode();
    }
}

namespace NextORM.Core;

/// <summary>Ordered collection shape tokens without the evaluated parameter values.</summary>
internal sealed class InValuesShape : IEquatable<InValuesShape>
{
    private readonly int[] _tokens;
    private readonly int _hash;

    internal InValuesShape(List<int> tokens)
    {
        _tokens = tokens.ToArray();
        var hash = new XxHash32();
        foreach (var token in _tokens)
            hash.Add(token);
        _hash = hash.ToHashCode();
    }

    public bool Equals(InValuesShape? other)
        => ReferenceEquals(this, other)
        || (other is not null && _tokens.AsSpan().SequenceEqual(other._tokens));

    public override bool Equals(object? obj) => Equals(obj as InValuesShape);

    public override int GetHashCode() => _hash;
}

namespace NextORM.Core;

/// <summary>
/// Immutable structural identity of a collection-valued projection slot graph (#249 / T353), stored as an
/// ordered list of canonical slot fingerprints. It participates in the plan key of the entity query the
/// carrier executes, so two projections that differ in a slot's path, CLR destination shape, nested
/// projection, adapter operation shape, inherited split/single mode or validation policy can never share a
/// plan entry. Captured values are deliberately absent: adapter lambdas are rendered with every constant
/// masked, and the values themselves stay in the per-execution reader the carrier compiles.
/// </summary>
internal readonly struct CollectionSlotShape : IEquatable<CollectionSlotShape>
{
    /// <summary>The empty shape: the command carries no collection-valued projection slot.</summary>
    public static readonly CollectionSlotShape Empty = new([]);

    private readonly string[]? _parts;

    /// <summary>Creates a shape from the ordered structural fingerprints.</summary>
    /// <param name="parts">The canonical fingerprints, in slot declaration order.</param>
    public CollectionSlotShape(string[] parts)
    {
        _parts = parts;
    }

    /// <summary>Whether this shape describes no collection-valued projection slot.</summary>
    public bool IsEmpty => _parts is not { Length: > 0 };

    /// <summary>Stable structural hash over the ordered fingerprints; <c>0</c> for the empty shape.</summary>
    public int StructuralHash
    {
        get
        {
            if (IsEmpty)
                return 0;

            unchecked
            {
                var hash = new XxHash32();
                for (var i = 0; i < _parts!.Length; i++)
                    hash.Add(_parts[i]);

                return hash.ToHashCode();
            }
        }
    }

    /// <inheritdoc/>
    public bool Equals(CollectionSlotShape other)
    {
        if (_parts is null || _parts.Length == 0)
            return other._parts is null || other._parts.Length == 0;

        if (other._parts is null || _parts.Length != other._parts.Length)
            return false;

        for (var i = 0; i < _parts.Length; i++)
        {
            if (!string.Equals(_parts[i], other._parts[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CollectionSlotShape other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StructuralHash;

    /// <summary>Feeds this shape into a running plan hash.</summary>
    /// <param name="hash">The running hash.</param>
    public void AddTo(ref XxHash32 hash)
    {
        if (_parts is null)
            return;

        for (var i = 0; i < _parts.Length; i++)
            hash.Add(_parts[i]);
    }

    /// <summary>
    /// Builds the shape of a recognized slot graph: the inherited eager-load mode (resolved once, so the
    /// split and single forms never collide) followed by every slot's structural fingerprint in declaration
    /// order. Nested slots contribute through their parent's fingerprint, so the whole graph is keyed.
    /// </summary>
    /// <param name="slots">The recognized slots, in projection order.</param>
    /// <param name="mode">The resolved eager-load mode inherited by the graph.</param>
    /// <returns>The structural shape of the slot graph.</returns>
    internal static CollectionSlotShape ForSlots(IReadOnlyList<CollectionSlotDescriptor> slots, EagerLoadMode mode)
    {
        var parts = new string[slots.Count + 1];
        parts[0] = "mode:" + mode;
        for (var i = 0; i < slots.Count; i++)
            parts[i + 1] = slots[i].Fingerprint;

        return new CollectionSlotShape(parts);
    }
}

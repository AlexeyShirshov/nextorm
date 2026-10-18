namespace NextORM.Core;

/// <summary>
/// Eager-load strategy state carried by a <see cref="QueryCommand"/>: the nullable per-command override
/// (a query's explicit <c>LoadWith</c> choice), the strategy resolved once during preparation, and the
/// structural identity of the eager-load graph used to key the plan cache.
/// </summary>
public partial class QueryCommand
{
    /// <summary>
    /// Per-command eager-load override: a concrete strategy set by an explicit <c>LoadWith(..., mode)</c>,
    /// or <see langword="null"/> to inherit the context default
    /// (<c>DataContextBuilder.UseEagerLoadMode</c>). It follows the same override/default pattern as
    /// <see cref="QuoteIdentifiers"/> and <see cref="KeywordCase"/>. A command with no eager loads leaves
    /// it <see langword="null"/> and carries no resolved strategy.
    /// </summary>
    public EagerLoadMode? EagerLoadMode
    {
        get => _description?.Options.EagerLoadMode;
        internal set
        {
            if (value == _description?.Options.EagerLoadMode) return;
            WriteOptions().EagerLoadMode = value;
        }
    }

    /// <summary>
    /// The executable strategy resolved for this command during preparation (the command override when
    /// present, otherwise the context default, with <see cref="EagerLoadMode.Auto"/> conservatively
    /// dispatched to <see cref="EagerLoadMode.SplitQuery"/>). <see cref="EagerLoadMode.Default"/> means
    /// the command has no eager-load strategy. It is part of the plan key so two commands that resolve to
    /// different strategies never share a cached plan.
    /// </summary>
    internal EagerLoadMode ResolvedEagerLoadMode
    {
        get => _preparation?.ResolvedEagerLoadMode ?? default;
        set
        {
            if (value == (_preparation?.ResolvedEagerLoadMode ?? default)) return;
            Preparation.ResolvedEagerLoadMode = value;
        }
    }

    /// <summary>
    /// The structural identity of this command's eager-load graph (ordered load paths, member and
    /// cardinality identity, parent/child linkage and child-shape identity). Empty when the command has
    /// no eager loads. It is part of the plan key so an otherwise identical parent command loaded with a
    /// different eager graph cannot reuse an incompatible plan.
    /// </summary>
    internal EagerLoadShape EagerLoadShape
    {
        get => _description?.Options.EagerLoadShape ?? default;
        set
        {
            if (value.Equals(_description?.Options.EagerLoadShape ?? default)) return;
            WriteOptions().EagerLoadShape = value;
        }
    }

    /// <summary>
    /// Whether one of this command's eager-load children requests a per-parent top-N (a child
    /// <c>Limit</c>/<c>Offset</c>/<c>Page</c>). Recorded when the parent command is built so
    /// <see cref="QueryPreparer"/> can reject the query during preparation — before any SQL is executed
    /// or a plan is inserted — instead of running the parent statement and failing in the child loader.
    /// It is command state, not a plan-key dimension: a rejected command never reaches the cache.
    /// </summary>
    internal bool HasPerParentEagerTopN
    {
        get => _description?.Options.HasPerParentEagerTopN ?? false;
        set
        {
            if (value == (_description?.Options.HasPerParentEagerTopN ?? false)) return;
            WriteOptions().HasPerParentEagerTopN = value;
        }
    }
}

/// <summary>
/// One centralized strategy-resolution path shared by preparation, execution dispatch and plan-key
/// construction. It is a plain resolver, not a strategy hierarchy: it maps an optional query override and
/// a context default to one executable <see cref="EagerLoadMode"/>, applies the conservative E1 automatic
/// rule (<see cref="EagerLoadMode.Auto"/> selects split loading), and fails closed on strategies that have
/// no implemented execution path.
/// </summary>
internal static class EagerLoadResolver
{
    /// <summary>
    /// Resolves the executable strategy: <paramref name="requested"/> wins when present, otherwise the
    /// context default applies. <see cref="EagerLoadMode.Auto"/> resolves to
    /// <see cref="EagerLoadMode.SplitQuery"/>; <see cref="EagerLoadMode.Correlated"/> is rejected because
    /// per-parent top-N / correlated execution is not implemented and must not be silently reinterpreted.
    /// </summary>
    /// <param name="requested">The query-level override, or <see langword="null"/> to inherit the context default.</param>
    /// <param name="contextDefault">The context-wide default; never <see cref="EagerLoadMode.Default"/> for a configured context.</param>
    /// <returns>The executable strategy.</returns>
    /// <exception cref="NotSupportedException"><paramref name="requested"/> or the context default is <see cref="EagerLoadMode.Correlated"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The effective value is an undefined numeric value.</exception>
    internal static EagerLoadMode Resolve(EagerLoadMode? requested, EagerLoadMode contextDefault)
    {
        var effective = requested ?? contextDefault;

        switch (effective)
        {
            case EagerLoadMode.SplitQuery:
            case EagerLoadMode.SingleQuery:
                return effective;
            case EagerLoadMode.Auto:
                // Conservative E1 dispatcher: every supported ordinary load stays on the split path. It
                // does not select Single automatically and does not erase a per-parent top-N requirement.
                return EagerLoadMode.SplitQuery;
            case EagerLoadMode.Correlated:
                throw CorrelatedNotSupported();
            case EagerLoadMode.Default:
                // "Default" is an inheritance sentinel, not an executable strategy; an absent choice maps
                // to the context default, which is already applied above. Treat a bare sentinel as split.
                return EagerLoadMode.SplitQuery;
            default:
                // The offending value is the query override when one was supplied, otherwise the context
                // default (effective == requested ?? contextDefault above), so attribute ParamName to the
                // actual source instead of always blaming the context default.
                throw new ArgumentOutOfRangeException(
                    requested is not null ? nameof(requested) : nameof(contextDefault), effective,
                    $"'{effective}' is not a defined EagerLoadMode value.");
        }
    }

    /// <summary>
    /// The <see cref="NotSupportedException"/> for an explicit <see cref="EagerLoadMode.Correlated"/>
    /// request, which has no implemented execution path. Shared so the preparation resolver and the
    /// <c>JoinInto</c> entry guard report the identical message.
    /// </summary>
    /// <returns>The rejection for an explicit Correlated request.</returns>
    internal static NotSupportedException CorrelatedNotSupported() =>
        new("EagerLoadMode.Correlated is not supported in this release: per-parent top-N / " +
            "correlated eager loading has no implemented execution path. Use SplitQuery or " +
            "SingleQuery, or remove the per-parent limit.");
}

/// <summary>
/// Immutable structural identity of an eager-load graph, stored as an ordered list of canonical
/// per-load fingerprints (parent/child types, collection member and key identity). It carries no public
/// surface: the plan key only needs value equality and a stable hash. Reference identity is never used.
/// </summary>
internal readonly struct EagerLoadShape : IEquatable<EagerLoadShape>
{
    /// <summary>The empty shape: the command has no eager loads.</summary>
    public static readonly EagerLoadShape Empty = new([]);

    private readonly string[] _parts;

    /// <summary>Creates a shape from the ordered per-load fingerprints.</summary>
    /// <param name="parts">The canonical fingerprints, in declaration order.</param>
    public EagerLoadShape(string[] parts)
    {
        _parts = parts;
    }

    /// <summary>Whether this shape describes no eager loads.</summary>
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
                for (var i = 0; i < _parts.Length; i++)
                    hash.Add(_parts[i]);

                return hash.ToHashCode();
            }
        }
    }

    /// <inheritdoc/>
    public bool Equals(EagerLoadShape other)
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
    public override bool Equals(object? obj) => obj is EagerLoadShape other && Equals(other);

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
}

namespace NextORM.Core;

/// <summary>
/// Per-join options applied while the join is declared, through the trailing
/// <c>Action&lt;JoinOptions&gt;</c> parameter of the <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/
/// <c>FullJoin</c>/<c>SemiJoin</c>/<c>AntiJoin</c>/<c>PasteJoin</c>/<c>CrossJoin</c>/<c>CrossApply</c>/
/// <c>OuterApply</c> and <c>JoinInto</c> overloads
/// (for example <c>a.Join(b, (x, y) =&gt; x.Id == y.Id, j =&gt; j.WithStrictness(JoinStrictness.Any))</c>).
/// The values are copied into the resulting join, so the options object is not retained. The raw
/// named-table (<c>TableAlias</c>) <c>CrossJoin</c>/<c>CrossApply</c>/<c>OuterApply</c> overloads take
/// no options: those joins cannot carry the modifiers described here.
/// </summary>
public sealed class JoinOptions
{
    internal JoinStrictness? Strictness { get; private set; }
    internal bool IsGlobal { get; private set; }
    internal string? JoinHint { get; private set; }
    internal IReadOnlyList<string>? TableHints { get; private set; }

    /// <summary>
    /// Applies a ClickHouse join modifier (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>) to this join, for
    /// example <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>. Requires a dialect that supports it
    /// (see <see cref="ISqlDialect.SupportsJoinStrictness"/>).
    /// </summary>
    /// <param name="strictness">The strictness modifier to apply to this join.</param>
    /// <returns>This instance, to allow chaining.</returns>
    public JoinOptions WithStrictness(JoinStrictness strictness)
    {
        Strictness = strictness;
        return this;
    }

    /// <summary>
    /// Marks this join as the ClickHouse <c>GLOBAL</c> variant (the right-hand side is resolved once
    /// and broadcast, for distributed queries), for example <c>j =&gt; j.Global()</c>. Requires a
    /// dialect that supports it (see <see cref="ISqlDialect.SupportsGlobalJoin"/>).
    /// </summary>
    /// <returns>This instance, to allow chaining.</returns>
    public JoinOptions Global()
    {
        IsGlobal = true;
        return this;
    }

    /// <summary>
    /// Attaches a provider-specific hint to this join, for example SQL Server
    /// <c>j =&gt; j.WithJoinHint("loop")</c> which renders <c>inner loop join</c>. On
    /// PostgreSQL/MySQL/MariaDB the hint is folded into the statement-level <c>/*+ ... */</c> comment
    /// (append the aliases yourself, e.g. <c>NestLoop(t1 t2)</c>); a dialect that supports neither form
    /// rejects the command with <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="hint">The join hint text; must be non-empty.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="hint"/> is null, empty or whitespace.</exception>
    public JoinOptions WithJoinHint(string hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            throw new ArgumentException("A join hint must be a non-empty string.", nameof(hint));

        JoinHint = hint;
        return this;
    }

    /// <summary>
    /// Attaches table-level hints to this join, for example SQL Server
    /// <c>j =&gt; j.WithJoinTableHint("nolock")</c> which renders a <c>WITH (nolock)</c> clause on that
    /// joined table only. This is the per-join counterpart of <see cref="FromOptions.WithTableHint"/> and
    /// is distinct from the optimizer join hint <see cref="WithJoinHint(string)"/>; unlike
    /// <see cref="EntityBuilder{TEntity}.WithTablesInScopeHint"/> it never leaks to the other physical
    /// tables. Hints are rendered verbatim, so only pass trusted values; null/blank entries are ignored,
    /// and a call that supplies only ignored entries leaves the hints unchanged. Requires a dialect that
    /// supports table hints (see <see cref="ISqlDialect.SupportsTableHints"/>) and a physical-table join
    /// source (an APPLY, derived-table, table-valued-function or XML/pivot join source is rejected).
    /// </summary>
    /// <param name="hints">The table hints to apply to the joined table (for example <c>nolock</c>).</param>
    /// <returns>This instance, to allow chaining.</returns>
    public JoinOptions WithJoinTableHint(params string[] hints)
    {
        // Normalize blank-only input to null: a call that supplies no usable hint must not clear hints
        // attached by an earlier call, mirroring FromOptions.WithTableHint.
        var filtered = hints is { Length: > 0 }
            ? hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray()
            : [];

        if (filtered.Length > 0)
            TableHints = filtered;

        return this;
    }
}

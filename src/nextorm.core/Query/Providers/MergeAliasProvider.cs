namespace NextORM.Core;

/// <summary>
/// An <see cref="IAliasProvider"/> that names the <c>MERGE</c> sources instead of the usual <c>t1</c>/<c>t2</c>:
/// the target row (<c>target</c>), the source relation (<c>source</c>) and, where the provider has one, the
/// proposed insert row (<c>excluded</c>). Used to render an <c>ON</c>/<c>WHEN ... AND</c> condition and an
/// explicit assignment right-hand side over the role rows.
/// </summary>
internal sealed class MergeAliasProvider : IAliasProvider
{
    /// <summary>The two-role instance used by a merge condition: <c>target</c> and <c>source</c>.</summary>
    public static readonly MergeAliasProvider Instance = new("target", "source", null);

    private readonly string? _target;
    private readonly string? _source;
    private readonly string? _excluded;

    /// <summary>Creates a provider with explicit role aliases.</summary>
    /// <param name="target">The SQL reference naming the target row.</param>
    /// <param name="source">The SQL reference naming the source relation, or <see langword="null"/> when the statement has no source relation.</param>
    /// <param name="excluded">The SQL reference naming the proposed insert row, or <see langword="null"/> when the provider has none.</param>
    public MergeAliasProvider(string? target, string? source, string? excluded)
    {
        _target = target;
        _source = source;
        _excluded = excluded;
    }

    /// <summary>Returns the target, source or excluded reference for the matching source index.</summary>
    /// <param name="idx">The zero-based source index.</param>
    /// <returns>The reference, or <see langword="null"/> for an index this statement does not have.</returns>
    public string? FindAlias(int idx) => idx switch
    {
        0 => _target,
        1 => _source,
        2 => _excluded,
        _ => null,
    };

    /// <summary>Returns the target reference; a MERGE assignment never allocates a new alias.</summary>
    /// <param name="from">The FROM source to register.</param>
    /// <returns>The target reference.</returns>
    public string GetNextAlias(FromExpression from) => _target ?? "target";

    /// <summary>Returns the source reference; a MERGE assignment never allocates a new alias.</summary>
    /// <param name="queryCommand">The nested query to register.</param>
    /// <returns>The source reference.</returns>
    public string GetNextAlias(QueryCommand queryCommand) => _source ?? "source";
}

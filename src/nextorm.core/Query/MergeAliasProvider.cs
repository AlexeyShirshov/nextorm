namespace NextORM.Core;

/// <summary>
/// An <see cref="IAliasProvider"/> that names the two <c>MERGE</c> sources <c>target</c> and
/// <c>source</c> instead of the usual <c>t1</c>/<c>t2</c>. Used only to render an
/// <c>ON</c>/<c>WHEN ... AND</c> condition over the <c>(target, source)</c> row.
/// </summary>
internal sealed class MergeAliasProvider : IAliasProvider
{
    /// <summary>The shared instance.</summary>
    public static readonly MergeAliasProvider Instance = new();

    private MergeAliasProvider()
    {
    }

    /// <summary>Returns <c>target</c> for index 0 and <c>source</c> for index 1.</summary>
    /// <param name="idx">The zero-based source index.</param>
    /// <returns>The alias, or <see langword="null"/> for an out-of-range index.</returns>
    public string? FindAlias(int idx) => idx switch
    {
        0 => "target",
        1 => "source",
        _ => null,
    };

    /// <summary>Returns the target alias; a MERGE condition never allocates a new source aliases.</summary>
    /// <param name="from">The FROM source to register.</param>
    /// <returns>The <c>target</c> alias.</returns>
    public string GetNextAlias(FromExpression from) => "target";

    /// <summary>Returns the source alias; a MERGE condition never allocates a new source alias.</summary>
    /// <param name="queryCommand">The nested query to register.</param>
    /// <returns>The <c>source</c> alias.</returns>
    public string GetNextAlias(QueryCommand queryCommand) => "source";
}

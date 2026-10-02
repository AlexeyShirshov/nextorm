namespace NextORM.Core;

/// <summary>
/// An immutable, reference-identified descriptor of an ordinary (non-recursive) common table
/// expression whose rows carry the projection shape <typeparamref name="TResult"/>.
/// <para>
/// Created from a query with <see cref="QueryCommand{TResult}.AsCte(string)"/> and read with
/// <see cref="DataContextExtensions.From{T}(IDataContext, Cte{T})"/>. The descriptor is a typed
/// projection source, not a mapped entity: typed member reads resolve to the defining query's output
/// columns, so a whole-entity <typeparamref name="TResult"/> does not imply a table mapping.
/// </para>
/// <para>
/// The descriptor holds the defining command and its declaration set internally; identity is by
/// reference, there is no structural or name equality. The defining <see cref="QueryCommand"/> is not
/// snapshotted, matching the existing CTE lifecycle contract.
/// </para>
/// <para>
/// Recursion is not part of this API — it declares an ordinary CTE only. The legacy
/// <c>With</c>/<c>WithRecursive</c>/<c>From(string)</c>/<c>From(CteDefinition)</c> string APIs are
/// unchanged and remain the way to read a CTE by name or to build a recursive CTE.
/// </para>
/// </summary>
/// <typeparam name="TResult">The row shape the CTE's defining <c>SELECT</c> projects.</typeparam>
public sealed class Cte<TResult>
{
    private readonly IReadOnlyList<CteDefinition> _definitions;

    internal Cte(QueryCommand<TResult> query, string name)
    {
        var definition = new CteDefinition(name, query) { TypedProjection = true };

        Query = query;
        Definition = definition;
        _definitions = BuildDefinitions(definition, query);
    }

    /// <summary>The defining command whose projection shape the typed source reads.</summary>
    internal QueryCommand<TResult> Query { get; }

    /// <summary>The single declaration this descriptor wraps.</summary>
    internal CteDefinition Definition { get; }

    /// <summary>
    /// The declaration set to attach to a command reading this CTE: the descriptor's own definition
    /// plus every reachable dependency, already flattened and ordered dependency-before-consumer.
    /// </summary>
    internal IReadOnlyList<CteDefinition> Definitions => _definitions;

    /// <summary>The name the common table expression is declared under and referenced by in <c>from</c>.</summary>
    public string Name => Definition.Name;

    // Seeds the closure with the descriptor's own declaration, then pulls in every declaration the
    // defining command (transitively) references by name. Declarations the defining command carries but
    // never references are omitted. Same-instance dependencies are deduplicated and a distinct
    // declaration under an already-used name is rejected by the hoister, matching the legacy CTE path.
    private static IReadOnlyList<CteDefinition> BuildDefinitions(CteDefinition definition, QueryCommand<TResult> query)
    {
        var candidates = new List<CteDefinition>(1 + (query.Ctes?.Count ?? 0)) { definition };
        if (query.Ctes is { Count: > 0 } carried)
            candidates.AddRange(carried);

        return CteHoister.Hoist(CteHoister.Reachable(candidates, definition))!;
    }
}

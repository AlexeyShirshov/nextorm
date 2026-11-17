using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Base type for the typed self-reference passed to a recursive CTE's step callback.
/// <para>
/// Only the recursive CTE factory creates references, and each is bound to the anchor's readable column
/// shape. A reference is identified by its non-public <see cref="Owner"/> token, not its declared
/// <see cref="Name"/>; references with the same name but different owners are not interchangeable.
/// </para>
/// </summary>
public abstract class CteReference
{
    private protected CteReference(string name, QueryCommand anchorShape, object owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(anchorShape);
        ArgumentNullException.ThrowIfNull(owner);

        Name = name;
        AnchorShape = anchorShape;
        Owner = owner;
    }

    /// <summary>The name the recursive common table expression is declared under and read from in the step.</summary>
    public string Name { get; }

    /// <summary>The anchor command whose projection describes the readable columns of the self-reference.</summary>
    internal QueryCommand AnchorShape { get; }

    /// <summary>Non-public identity of the recursive definition that owns this reference.</summary>
    internal object Owner { get; }
}

/// <summary>
/// Typed self-reference to a recursive common table expression. The step callback receives this instance
/// and reads the anchor's rows through
/// <see cref="DataContextExtensions.From{TResult}(IDataContext, CteReference{TResult})"/>. Use it only
/// while the factory runs that callback; a captured or foreign reference is rejected before a database
/// command is built.
/// </summary>
/// <typeparam name="TResult">The anchor/step row shape, identical to the recursive CTE's projection.</typeparam>
public sealed class CteReference<TResult> : CteReference
{
    internal CteReference(string name, QueryCommand anchorShape, object owner)
        : base(name, anchorShape, owner)
    {
    }
}

/// <summary>
/// A typed descriptor for a common table expression (CTE) whose rows have the projected shape
/// <typeparamref name="TResult"/>.
/// <para>
/// Create it from a query with <see cref="QueryCommand{TResult}.AsCte(string)"/> and read it with
/// <see cref="DataContextExtensions.From{T}(IDataContext, Cte{T})"/>. The CTE acts as a projection source,
/// not a mapped entity: member reads use the defining query's output columns, even when
/// <typeparamref name="TResult"/> is an entity type.
/// </para>
/// <para>
/// The descriptor is identified by object reference, not by its name or query structure. It retains the
/// defining <see cref="QueryCommand"/> rather than copying it.
/// </para>
/// <para>
/// This type creates ordinary, non-recursive CTEs. Use the existing
/// <c>With</c>/<c>WithRecursive</c>/<c>From(string)</c>/<c>From(CteDefinition)</c> APIs to read a CTE by
/// name or create a recursive CTE.
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

    private Cte(QueryCommand<TResult> query, CteDefinition definition)
    {
        Query = query;
        Definition = definition;
        _definitions = BuildDefinitions(definition, query);
    }

    /// <summary>
    /// Builds a recursive descriptor: invokes <paramref name="step"/> exactly once under a fresh owner
    /// token, assembles the body as <c>anchor UNION ALL step</c> and stores the recursive declaration.
    /// The callback is never re-invoked by preparation, generation or execution; a null callback throws
    /// <see cref="ArgumentNullException"/> and a null returned query throws
    /// <see cref="InvalidOperationException"/>, both before any database command is built.
    /// </summary>
    internal static Cte<TResult> CreateRecursive(
        QueryCommand<TResult> anchor,
        string name,
        Func<CteReference<TResult>, QueryCommand<TResult>> step,
        int? maxRecursion)
    {
        var owner = new object();
        var reference = new CteReference<TResult>(name, anchor, owner);

        // Marks the anchor command so a member read of the self-reference resolves the CTE's declared
        // output aliases rather than re-rendering the anchor body (a constant anchor would inline `1`
        // and render an unaddressable `t1.1`). See QueryCommand.TypedRecursiveAnchor.
        anchor.TypedRecursiveAnchor = reference;

        QueryCommand<TResult> stepQuery;
        RecursiveCteScope.Enter(owner);
        try
        {
            stepQuery = step(reference);
            if (stepQuery is null)
                throw new InvalidOperationException(
                    $"The recursive common table expression '{name}' step callback returned null; it must return the step query.");

            // Fail fast, before any database command is built: only a self-reference owned by THIS
            // definition may occur in the body. The walk covers the anchor too, so an anchor that
            // (illegally) reads a recursive reference is rejected as construction time as well.
            ValidateRecursiveOwners(name, owner, anchor, stepQuery);
        }
        finally
        {
            RecursiveCteScope.Exit(owner);
        }

        var body = anchor.UnionAll(stepQuery);
        var definition = new CteDefinition(name, body, true, maxRecursion)
        {
            TypedProjection = true,
            RecursiveReference = reference,
        };

        return new Cte<TResult>(body, definition);
    }

    /// <summary>
    /// Walks the whole query graph of a recursive definition's anchor and step and throws when it
    /// encounters a typed recursive self-reference whose owner token is not <paramref name="owner"/>.
    /// A reference from another recursive definition (a foreign/captured reference, even one whose
    /// defining scope is still active in a nested construction) and any reference inside the anchor are
    /// therefore rejected before a database command exists, not at preparation time. Nested recursive
    /// declarations carried by the graph are skipped: each was validated under its own owner when it was
    /// constructed, and its body is not governed by this definition's owner. The walk is fail-fast and
    /// diagnostic: it names the CTE, the branch/position and the offending source.
    /// </summary>
    private static void ValidateRecursiveOwners(string name, object owner, QueryCommand<TResult> anchor, QueryCommand<TResult> step)
    {
        var visited = new HashSet<QueryCommand>(ReferenceEqualityComparer.Instance);
        Walk(anchor, "anchor");
        Walk(step, "step");

        void Walk(QueryCommand cmd, string position)
        {
            if (!visited.Add(cmd))
                return;

            WalkFrom(cmd.From, position);

            if (cmd.Joins is { Length: > 0 } joins)
            {
                for (var i = 0; i < joins.Length; i++)
                    WalkFrom(joins[i].From, $"{position} join {i}");
            }

            if (cmd.UnionQuery is { } union)
                Walk(union, $"{position} set-operation");

            if (cmd.ReferencedQueries is { Count: > 0 } referenced)
            {
                for (var i = 0; i < referenced.Count; i++)
                    Walk(referenced[i], $"{position} subquery {i}");
            }

            if (cmd.Ctes is { Count: > 0 } ctes)
            {
                for (var i = 0; i < ctes.Count; i++)
                {
                    var declaration = ctes[i];
                    if (declaration.RecursiveReference is not null)
                        continue;

                    Walk(declaration.Mutation?.Source ?? declaration.Query, $"{position} declaration '{declaration.Name}'");
                }
            }
        }

        void WalkFrom(FromExpression? from, string position)
        {
            if (from is null)
                return;

            if (from.RecursiveOwner is { } fromOwner && !ReferenceEquals(fromOwner, owner))
                throw new InvalidOperationException(
                    $"The recursive common table expression '{name}' reads the self-reference '{from.Table}' in its {position}, but that reference belongs to a different recursive definition. A self-reference is only valid inside the AsRecursiveCte callback that received it, and never in the anchor.");

            if (from.SubQuery is { } subQuery)
                Walk(subQuery, position);

            if (from.ColumnShape is { } columnShape)
                Walk(columnShape, position);

            if (from.Pivot is { } pivot)
                WalkFrom(pivot.Inner, position);
        }
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
        var candidates = new List<CteDefinition>(2 + (query.Ctes?.Count ?? 0) + (query.UnionQuery?.Ctes?.Count ?? 0)) { definition };
        if (query.Ctes is { Count: > 0 } carried)
            candidates.AddRange(carried);
        // A recursive body is anchor UNION ALL step; declarations carried by the step query are
        // dependencies of the recursive definition, exactly like declarations carried by the anchor.
        if (query.UnionQuery?.Ctes is { Count: > 0 } stepCarried)
            candidates.AddRange(stepCarried);

        return CteHoister.Hoist(CteHoister.Reachable(candidates, definition))!;
    }
}

/// <summary>
/// Ambient owner scope of a recursive typed CTE. It is active only while the definition's step callback
/// runs (so a self-reference can be created) and while that definition's step is prepared (so a
/// captured/foreign reference embedded in the step is rejected). The scope is per-thread: query
/// construction and preparation are synchronous.
/// </summary>
internal static class RecursiveCteScope
{
    [ThreadStatic]
    private static HashSet<object>? _active;

    internal static void Enter(object owner)
        => (_active ??= new HashSet<object>(ReferenceEqualityComparer.Instance)).Add(owner);

    internal static void Exit(object owner)
    {
        if (_active is null)
            return;

        _active.Remove(owner);
        if (_active.Count == 0)
            _active = null;
    }

    internal static bool IsActive(object owner) => _active?.Contains(owner) ?? false;
}

using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Native route for a projection command's list terminals (I7/M3).
/// </summary>
/// <remarks>
/// <para>
/// A cached <c>GroupBy(...).Select(...)</c> (or <c>Where(...).Select(...)</c>) command still pays the
/// whole pre-preparation recipe on every call: the raw descriptor is fingerprinted, the stored
/// preparation is staged into the fresh command, the normalized plan key is rebuilt and the store is
/// interrogated. On a parameter-free shape that is the entire remaining overage. This route derives a
/// value-agnostic shape from the command's own graphs instead, finds the compiled holder the miss
/// published, binds the current closure values into its parameters and executes it — no fingerprint,
/// no staging, no key, no lookup.
/// </para>
/// <para>
/// Fail closed: the command must carry nothing beyond a source, a filter, a grouping key and a
/// projection. Every clause that the raw descriptor models (joins, CTEs, set operations, sorting,
/// paging, HAVING, DISTINCT, hints, samples, locks, components, naming/quoting overrides, eager and
/// collection-slot state, …) makes the command ineligible, as does an applicable global/builder filter.
/// Parameterization is bound per execution, never keyed.
/// </para>
/// </remarks>
internal static class NativeProjectionRoute
{
    /// <summary>The terminal discriminator of the shape header (a plan is never shared across terminals).</summary>
    private const int TerminalList = 2;

    /// <summary>
    /// Probes the native index for a synchronous list terminal and executes the stored holder on a hit.
    /// </summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="command">The freshly built projection command.</param>
    /// <param name="params">The terminal's positional arguments; a non-empty span keeps the legacy route.</param>
    /// <param name="probe">The probe result: the rows on a hit, or the shape to publish on a miss.</param>
    internal static void Run<TResult>(QueryCommand<TResult> command, ReadOnlySpan<object?> @params, out Probe<TResult> probe)
    {
        if (!TryResolve(command, @params, out var header))
        {
            probe = default;
            return;
        }

        var condition = command.Condition as LambdaExpression;
        var group = command.GroupBy;
        var projection = command.ProjectionExpression;

        if (QueryPlanStore.TryGetNative(header, condition, group, projection, out var candidate) && candidate is not null)
        {
            if (candidate.Holder is not DbPreparedQueryCommand<TResult> holder)
            {
                probe = default;
                return;
            }

            if (!ShapeIdentity.TryBind(candidate.Shape, header, candidate.Binder, holder.DbCommandParams, condition, group, projection))
            {
                probe = default;
                return;
            }

            probe = new Probe<TResult>(command.DataContext!.ToList<TResult>(holder, @params));
            return;
        }

        // Miss: capture the shape now, before preparation mutates the raw graph.
        var shape = ShapeIdentity.TryCreate(header, condition, group, projection);
        probe = shape is null ? default : new Probe<TResult>(header, shape);
    }

    /// <summary>
    /// Probes the native index for an asynchronous list terminal and executes the stored holder on a hit.
    /// </summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="command">The freshly built projection command.</param>
    /// <param name="params">The terminal's positional arguments; a non-empty array keeps the legacy route.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <param name="probe">The probe result: the pending rows on a hit, or the shape to publish on a miss.</param>
    internal static void RunAsync<TResult>(
        QueryCommand<TResult> command,
        object[] @params,
        CancellationToken cancellationToken,
        out Probe<TResult> probe)
    {
        if (!TryResolve(command, @params, out var header))
        {
            probe = default;
            return;
        }

        var condition = command.Condition as LambdaExpression;
        var group = command.GroupBy;
        var projection = command.ProjectionExpression;

        if (QueryPlanStore.TryGetNative(header, condition, group, projection, out var candidate) && candidate is not null)
        {
            if (candidate.Holder is not DbPreparedQueryCommand<TResult> holder)
            {
                probe = default;
                return;
            }

            if (!ShapeIdentity.TryBind(candidate.Shape, header, candidate.Binder, holder.DbCommandParams, condition, group, projection))
            {
                probe = default;
                return;
            }

            probe = new Probe<TResult>(command.DataContext!.ToListAsync<TResult>(holder, @params, cancellationToken));
            return;
        }

        var shape = ShapeIdentity.TryCreate(header, condition, group, projection);
        probe = shape is null ? default : new Probe<TResult>(header, shape);
    }

    /// <summary>
    /// Whether the command is a provable plain projection the native route may key, and the header
    /// identifying it.
    /// </summary>
    private static bool TryResolve<TResult>(QueryCommand<TResult> command, ReadOnlySpan<object?> @params, out ShapeHeader header)
    {
        header = default;

        // Explicit positional arguments are bound by the legacy path (they are not captures).
        if (!@params.IsEmpty)
            return false;

        if (command.DataContext is not DataContext context || !context.QueryCacheEnabled)
            return false;

        // A boxed wrapper command executes a runtime result type through QueryCommand<object?>; it has
        // its own holder shape and stays on the legacy route.
        if (command.BoxedResultType is not null)
            return false;

        // Mirrors the pre-preparation gates of the ordinary route (no prepared state, cacheable, no
        // caller-owned SQL, no side-effecting CTE or temporary-table materialisation, not the shared
        // Any/Count wrapper).
        if (command.IsPrepared
            || command.HasEverPrepared
            || !command.Cache
            || command.CustomData is RawSqlOverride
            || command.HasDataModifyingCte
            || command.HasTemporaryTableSource()
            || IsSharedAnyCommand(context, command))
        {
            return false;
        }

        if (!IsPlainProjection(command))
            return false;

        // A global/builder filter is injected only during preparation, so it changes the statement
        // without changing the raw shape. Metadata-only probe; no user filter function runs.
        if (QueryCommand.HasApplicableFiltersInCommandGraph(command, context))
            return false;

        if (command.Condition is not (null or LambdaExpression))
            return false;

        if (ResolveEntityType(command) is not { } entityType)
            return false;

        header = new ShapeHeader(
            entityType,
            typeof(TResult),
            context.GetType(),
            TerminalList,
            NativeContextStamp.For(context),
            NativeSourceStamp.For(command.From));
        return true;
    }

    /// <summary>
    /// The entity type the command reads from. A projection command created through <c>Select</c> does
    /// not carry it (the source type is only filled for a derived root), so the projection's own
    /// parameter is the authoritative source — the same resolution the filter probe uses.
    /// </summary>
    private static Type? ResolveEntityType(QueryCommand command)
        => command.EntityType
            ?? (command.ProjectionExpression is { Parameters: { Count: > 0 } parameters } ? parameters[0].Type : null);

    /// <summary>
    /// Whether the command is the one shared by the <c>Any</c>/<c>Count</c>/<c>All</c> terminals. The
    /// shared command is never keyed natively: it is replaced per call, so a native plan would be stale.
    /// </summary>
    private static bool IsSharedAnyCommand(DataContext context, QueryCommand command)
    {
        var anyCommand = context.AnyCommand;
        return anyCommand is { IsValueCreated: true } && ReferenceEquals(anyCommand.Value, command);
    }

    /// <summary>
    /// Whether the command carries exactly the state the native shape models: a plain table source, an
    /// optional filter, an optional grouping key and a projection.
    /// </summary>
    private static bool IsPlainProjection(QueryCommand command)
        => command.ProjectionExpression is not null
            && (command.EntityType is not null || command.ProjectionExpression.Parameters.Count > 0)
            && !command.IsDistinct
            && !command.Final
            && command.SampleRatio is null
            && command.SampleOffset == 0
            && !command.GroupByWithTotals
            && command.GroupingType == GroupingType.None
            && command.GroupingSets is not { Count: > 0 }
            && command.Settings is not { Count: > 0 }
            && command.Hints is null
            && command.Tag is null
            && command.TableHints is null
            && command.IndexHints is null
            && command.TablesInScopeHints is null
            && command.LimitBy is null
            && command.DistinctOn is null
            && command.ExtremeRow is null
            && command.TableSample is null
            && command.Temporal is null
            && command.RowLock is null
            && command.SourceLockClause is null
            && command.Having is null
            && command.PreWhere is null
            && command.Sorting is not { Length: > 0 }
            && command.ArrayJoins is not { Length: > 0 }
            && command.Windows is not { Count: > 0 }
            && command.Joins is not { Length: > 0 }
            && command.Ctes is not { Count: > 0 }
            && command.UnionType == UnionType.None
            && command.Paging.Limit == 0
            && command.Paging.Offset == 0
            && !command.Paging.HasWithTies
            && command.NavigationPaths is null or { Count: 0 }
            && !command.PerParentPaging
            && !command.PerParentPagingEmbedded
            && !command.HasPerParentEagerTopN
            && !command.DocumentMode
            && !command.IgnoreColumns
            && !command.SingleRow
            && !command.OneColumn
            && !command.DefaultOnEmpty
            && !command.SingleScalar
            && !command.IdentitySlotAliases
            && command.EagerLoadMode is null
            && command.PerParentPartition is null
            && command.CollectionSlotShape.IsEmpty
            && command.EagerLoadShape.IsEmpty
            && command.CollectionSlotCarrier is null
            && command.ComponentsState.IsEmpty
            && command.QuoteIdentifiers is null
            && command.NamingConvention is null
            && command.KeywordCase is null
            && command.CommandTimeout is null
            && IsPlainSource(command.From);

    /// <summary>Whether a source is a plain table/CTE-name source the raw descriptor can describe.</summary>
    private static bool IsPlainSource(FromExpression? from)
        => from is null
            || (from.SubQuery is null
                && from.ColumnShape is null
                && from.Pivot is null
                && from.XmlNodes is null
                && from.RawSqlSource is null
                && from.TempTable is null
                && from.TableFunction is null
                && from.TableExpressionOverride is null);

    /// <summary>
    /// The outcome of one probe: the materialized rows on a hit, or the shape to publish after the
    /// legacy route compiled the holder on a miss. A default value is "stay on the legacy route".
    /// </summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    internal readonly struct Probe<TResult>
    {
        private readonly ShapeHeader _header;
        private readonly ShapeKey? _shape;
        private readonly List<TResult>? _rows;
        private readonly Task<List<TResult>>? _pending;

        internal Probe(List<TResult> rows)
        {
            _header = default;
            _shape = null;
            _rows = rows;
            _pending = null;
        }

        internal Probe(Task<List<TResult>> pending)
        {
            _header = default;
            _shape = null;
            _rows = null;
            _pending = pending;
        }

        internal Probe(in ShapeHeader header, ShapeKey shape)
        {
            _header = header;
            _shape = shape;
            _rows = null;
            _pending = null;
        }

        /// <summary>Whether the route answered the call.</summary>
        internal bool Hit => _rows is not null || _pending is not null;

        /// <summary>The materialized rows on a synchronous hit.</summary>
        internal List<TResult>? Rows => _rows;

        /// <summary>The pending rows on an asynchronous hit.</summary>
        internal Task<List<TResult>>? Pending => _pending;

        /// <summary>
        /// Publishes the holder the legacy route compiled under the shape captured before preparation.
        /// A no-op for an ineligible command, a holder of another type, a parameter map that is not a
        /// provable bijection, or a preparation whose statement follows a value list instead of the plan
        /// key (the same shapes the raw recipe refuses to publish).
        /// </summary>
        /// <param name="command">The command the legacy route just prepared.</param>
        /// <param name="prepared">The holder the legacy route compiled.</param>
        internal void Publish(QueryCommand<TResult> command, IPreparedQueryCommand<TResult> prepared)
        {
            if (_shape is null || prepared is not DbPreparedQueryCommand<TResult> holder)
                return;

            // A value list rendered in a clause whose shape is not part of the key (IN/PREWHERE at any
            // depth) makes the statement follow the captured collection, so the plan is not reusable —
            // exactly the shapes the ordinary route keeps out of the cache.
            if (command.InValuesShapeHash != 0
                || command.PreWhereShapeHash != 0
                || command.HasTopLevelInValues
                || command.HasPreWhereInValues
                || command.HasUnkeyedTupleInValues
                || command.HasUnkeyedScalarInValues)
            {
                return;
            }

            var binder = NativeBinder.TryCreate(_shape, holder.DbCommandParams);
            if (binder is null)
                return;

            QueryPlanStore.SetNative(_shape, holder, binder);
        }

        /// <summary>Re-reads the header this probe used (diagnostics only).</summary>
        internal ShapeHeader Header => _header;
    }
}

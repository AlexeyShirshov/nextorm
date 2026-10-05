using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Iteration 14 proposal 4 (navigation/count allocation): the navigation expansion containers are
/// allocated lazily, a failed path resolution no longer allocates its member buffer, the wide-count
/// proximity probe is skipped for an absent/empty registry, and the three projection shapes tag the
/// materialized Int32 count column through one shared helper. The observable behavior — which Int32
/// columns are wide-count narrowed, which paths reuse a single join, and the
/// <c>Count()</c>/<c>LongCount()</c> distinction — is unchanged.
/// </summary>
[Collection("Query cache controls")]
public class Iteration14NavigationCountTests
{
    public Iteration14NavigationCountTests() => DataContextCache.Clear();

    private static readonly Type StateType =
        typeof(NavigationExpansion).GetNestedType("ExpansionState", BindingFlags.NonPublic)!;

    private static readonly PropertyInfo ListItemIndexer =
        typeof(IReadOnlyList<QueryCommand>).GetProperty("Item")!;

    private static readonly MethodInfo GetOrCreateMI = StateType.GetMethod(
        "GetOrCreate",
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        types: [typeof(ResolvedNavigationPath)],
        modifiers: null)!;

    private static readonly PropertyInfo JoinExpressionsPI =
        StateType.GetProperty("JoinExpressions", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly PropertyInfo NavigationPathsPI =
        StateType.GetProperty("NavigationPaths", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<D4Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<D4Owner>();
        ctx.From<D4Child>(b => b.HasOne(c => c.Parent, c => c.ParentId).HasOne(c => c.Owner, c => c.OwnerId));
        return ctx;
    }

    private static void Submit<T>(InMemoryDataContext ctx, QueryCommand<T> command)
        => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    // ---------------------------------------------------------------------------------------------
    // Lazy containers + negative-path allocation
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void FailedNavigationPath_DoesNotCreateContainers()
    {
        using var ctx = CreateContext();
        var command = ctx.From<D4Child>().Select(c => new { c.Id });
        var state = NewState(command, typeof(D4Child));

        // A freshly constructed state owns none of the three navigation containers.
        Container(state, "_byKey").Should().BeNull();
        Container(state, "_joins").Should().BeNull();
        Container(state, "_paths").Should().BeNull();

        // `it.Id` is a pure member chain but not a declared navigation: resolution fails and the
        // navigation containers stay unallocated.
        var root = Expression.Parameter(typeof(D4Child), "it");
        var idAccess = Expression.Property(root, nameof(D4Child.Id));
        NavigationExpansion.TryResolvePath(idAccess, root, state, out _).Should().BeFalse();
        Container(state, "_byKey").Should().BeNull();
        Container(state, "_joins").Should().BeNull();
        Container(state, "_paths").Should().BeNull();

        // Early negative exits: a non-PropertyInfo member and a chain rooted at another parameter.
        var holder = new Holder();
        var foreign = Expression.Field(Expression.Constant(holder), nameof(Holder.NotNavigation));
        var otherRoot = Expression.Parameter(typeof(D4Child), "other");
        var wrongRoot = Expression.Property(otherRoot, nameof(D4Child.Id));

        NavigationExpansion.TryResolvePath(foreign, root, state, out _).Should().BeFalse();
        NavigationExpansion.TryResolvePath(wrongRoot, root, state, out _).Should().BeFalse();

        // Warm the JIT, then prove the early negative exits allocate no member list. No fluent
        // assertion call is made inside the measured window (it would dominate the count).
        var before = GC.GetAllocatedBytesForCurrentThread();
        var foreignResult = NavigationExpansion.TryResolvePath(foreign, root, state, out _);
        var wrongRootResult = NavigationExpansion.TryResolvePath(wrongRoot, root, state, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        foreignResult.Should().BeFalse();
        wrongRootResult.Should().BeFalse();
        allocated.Should().Be(0, "the non-property / wrong-root exits must not allocate the member buffer");
    }

    [Fact]
    public void RepeatedNavigationPath_ReusesJoinAndPath()
    {
        using var ctx = CreateContext();
        var command = ctx.From<D4Child>().Select(c => new { c.Id });
        var state = NewState(command, typeof(D4Child));

        var root = Expression.Parameter(typeof(D4Child), "c");
        var parentPath = ResolvePath(Expression.Property(root, nameof(D4Child.Parent)), root, command);
        var ownerPath = ResolvePath(Expression.Property(root, nameof(D4Child.Owner)), root, command);

        // Resolving the same path twice must reuse the join and the recorded path entry; only the
        // distinct path adds a second one.
        var parentJoin = GetOrCreate(state, parentPath);
        var repeated = GetOrCreate(state, parentPath);
        var ownerJoin = GetOrCreate(state, ownerPath);

        ReferenceEquals(parentJoin, repeated).Should().BeTrue("the same path reuses its joined parameter");

        var joins = JoinList(state);
        joins.Should().HaveCount(2, "a repeated path must not duplicate a join");
        ReferenceEquals(joins[0].SourceParameter, parentJoin).Should().BeTrue("join order follows first resolution");
        ReferenceEquals(joins[1].SourceParameter, ownerJoin).Should().BeTrue();

        var paths = PathMap(state);
        paths.Count.Should().Be(2, "a repeated path must not duplicate a path entry");
        paths[parentJoin].Should().Be("D4Child.Parent");
        paths[ownerJoin].Should().Be("D4Child.Owner");
    }

    // ---------------------------------------------------------------------------------------------
    // Wide-count narrowing provenance
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void OrdinaryIntConversion_IsNotWideCountNarrowing()
    {
        using var ctx = CreateContext();

        // A non-empty registry is probed but only an index into a tagged wide-count command matches.
        var untagged = ctx.From<D4Child>().Select(c => c.Id);
        var tagged = ctx.From<D4Child>().Select(c => c.Id);
        tagged.IsWideNavigationCount = true;
        var registry = new FakeRegistry([untagged, tagged]);

        var parameter = Expression.Parameter(typeof(D4Child), "c");
        var ordinary = Expression.Convert(Expression.Property(parameter, nameof(D4Child.Big)), typeof(int));

        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(ordinary, registry)
            .Should().BeFalse("an ordinary long->int conversion carries no wide-count provenance");

        var command = ctx.From<D4Child>().Select(c => new { V = (int)c.Big });
        Submit(ctx, command);
        command.SelectList!.Single(c => c.PropertyName == "V").IsWideCountNarrowed.Should().BeFalse();
    }

    [Fact]
    public void NavigationCount_CheckedNarrowingIsPreserved()
    {
        using var ctx = CreateContext();
        var command = ctx.From<D4Parent>().Select(p => new { p.Id, C = p.Children.Count() });
        Submit(ctx, command);

        var column = command.SelectList!.Single(c => c.PropertyName == "C");
        column.PropertyType.Should().Be(typeof(int));
        column.IsWideCountNarrowed.Should().BeTrue("a navigation Count() projected as Int32 is a checked narrowing");
        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(column.Expression, command).Should().BeTrue();
    }

    [Fact]
    public void NavigationCount_ConvertCheckedIsDetected()
    {
        using var ctx = CreateContext();
        var tagged = ctx.From<D4Child>().Select(c => c.Id);
        tagged.IsWideNavigationCount = true;
        var registry = new FakeRegistry([tagged]);

        var scalar = WideIndex(0);

        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(
            Expression.ConvertChecked(scalar, typeof(int)), registry).Should().BeTrue();
        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(
            Expression.Convert(scalar, typeof(int)), registry).Should().BeTrue();
    }

    [Fact]
    public void NavigationLongCount_IsNotNarrowed()
    {
        using var ctx = CreateContext();
        var command = ctx.From<D4Parent>().Select(p => new { p.Id, L = p.Children.LongCount() });
        Submit(ctx, command);

        var column = command.SelectList!.Single(c => c.PropertyName == "L");
        column.PropertyType.Should().Be(typeof(long));
        column.IsWideCountNarrowed.Should().BeFalse();
        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(column.Expression, command)
            .Should().BeFalse("LongCount() is the 64-bit scalar itself, not a narrowing");
    }

    [Fact]
    public void EmptyRegistry_WithUnregisteredCandidate_DoesNotSkipProbe()
    {
        var narrowing = Expression.ConvertChecked(WideIndex(0), typeof(int));

        var nullRegistry = new FakeRegistry(null);
        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(narrowing, nullRegistry)
            .Should().BeFalse("an absent registry cannot hold a wide-count candidate");

        var emptyRegistry = new FakeRegistry([]);
        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(narrowing, emptyRegistry)
            .Should().BeFalse("an empty registry cannot hold a wide-count candidate");

        using var ctx = CreateContext();
        var untagged = ctx.From<D4Child>().Select(c => c.Id);
        var tagged = ctx.From<D4Child>().Select(c => c.Id);
        tagged.IsWideNavigationCount = true;

        // A non-empty registry is still probed: the untagged index 0 is rejected; the tagged
        // index 1 matches only when the marker points at it.
        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(
            Expression.ConvertChecked(WideIndex(0), typeof(int)),
            new FakeRegistry([untagged])).Should().BeFalse();
        CorrelatedQueryExpressionVisitor.ContainsNavigationCountNarrowing(
            Expression.ConvertChecked(WideIndex(1), typeof(int)),
            new FakeRegistry([untagged, tagged])).Should().BeTrue();
    }

    [Fact]
    public void SubQueryAndColumnShape_PreserveCountTags()
    {
        using var ctx = CreateContext();

        // Constructor/tuple projection.
        var tuple = ctx.From<D4Parent>().Select(p => new { p.Id, C = p.Children.Count() });
        Submit(ctx, tuple);
        tuple.SelectList!.Single(c => c.PropertyName == "C").IsWideCountNarrowed.Should().BeTrue();

        // Single-column projection (the shape a scalar subquery carries).
        var single = ctx.From<D4Parent>().Select(p => p.Children.Count());
        Submit(ctx, single);
        single.SelectList!.Single().IsWideCountNarrowed.Should().BeTrue();

        // Member-init projection.
        var init = ctx.From<D4Parent>().Select(p => new D4Shape { Id = p.Id, C = p.Children.Count() });
        Submit(ctx, init);
        init.SelectList!.Single(c => c.PropertyName == "C").IsWideCountNarrowed.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private static Expression WideIndex(int index)
    {
        var indexer = Expression.MakeIndex(
            Expression.Constant(Array.Empty<QueryCommand>(), typeof(IReadOnlyList<QueryCommand>)),
            ListItemIndexer,
            [Expression.Constant(index)]);
        // Mirror the production scalar placeholder: the registry index routed through object and typed
        // as the command's Int64 result, so an Int32 conversion can legitimately wrap it.
        return Expression.Convert(Expression.Convert(indexer, typeof(object)), typeof(long));
    }

    private static ResolvedNavigationPath ResolvePath(
        Expression expression, ParameterExpression root, QueryCommand command)
    {
        NavigationExpansion.TryResolvePath(expression, root, command, out var path).Should().BeTrue();
        return path;
    }

    /// <summary>Drives the private expansion state through its real <c>GetOrCreate</c> entry point.</summary>
    private static ParameterExpression GetOrCreate(object state, ResolvedNavigationPath path)
        => (ParameterExpression)GetOrCreateMI.Invoke(state, [path])!;

    private static JoinExpression[] JoinList(object state)
        => (JoinExpression[])JoinExpressionsPI.GetValue(state)!;

    private static IReadOnlyDictionary<ParameterExpression, string> PathMap(object state)
        => (IReadOnlyDictionary<ParameterExpression, string>)NavigationPathsPI.GetValue(state)!;

    private static object NewState(QueryCommand command, Type srcType)
        => Activator.CreateInstance(
            StateType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [command, srcType],
            culture: null)!;

    private static object? Container(object state, string name)
        => StateType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state);

    private sealed class Holder
    {
        // A public field gives `Expression.Field` a MemberExpression whose Member is not a PropertyInfo.
        public int NotNavigation = 1;
    }

    private sealed class FakeRegistry(IReadOnlyList<QueryCommand>? queries) : IQueryRegistry
    {
        public IReadOnlyList<QueryCommand> ReferencedQueries { get; } = queries!;

        public IReadOnlyList<Expression>? OuterReferences => null;

        public int AddCommand(QueryCommand cmd) => throw new NotSupportedException();

        public int AddOuterReference(Expression node) => throw new NotSupportedException();

        public QueryPlanEqualityComparer GetQueryPlanEqualityComparer() => throw new NotSupportedException();

        public ExpressionPlanEqualityComparer GetExpressionPlanEqualityComparer() => throw new NotSupportedException();

        public SelectExpressionPlanEqualityComparer GetSelectExpressionPlanEqualityComparer() => throw new NotSupportedException();

        public FromExpressionPlanEqualityComparer GetFromExpressionPlanEqualityComparer() => throw new NotSupportedException();

        public JoinExpressionPlanEqualityComparer GetJoinExpressionPlanEqualityComparer() => throw new NotSupportedException();

        public SortingExpressionPlanEqualityComparer GetSortingExpressionPlanEqualityComparer() => throw new NotSupportedException();
    }
}

[SqlTable("i14_d4_parent")]
public sealed class D4Parent
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public long Big { get; set; }

    public ICollection<D4Child> Children { get; set; } = new List<D4Child>();
}

[SqlTable("i14_d4_child")]
public sealed class D4Child
{
    public int Id { get; set; }

    public int ParentId { get; set; }

    public int OwnerId { get; set; }

    public long Big { get; set; }

    public D4Parent? Parent { get; set; }

    public D4Owner? Owner { get; set; }
}

[SqlTable("i14_d4_owner")]
public sealed class D4Owner
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

public sealed class D4Shape
{
    public int Id { get; set; }

    public int C { get; set; }
}

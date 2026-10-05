using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Iteration 14 proposal 2 (CTE lookup allocation): the data-modifying CTE detection
/// (<see cref="QueryCommand.HasDataModifyingCte"/>) must scan a prepared/hoisted, flat CTE list without
/// allocating a visited <see cref="HashSet{T}"/>, while keeping nested-DML detection, reference-identity
/// cycle termination and the call-local plan-cache bypass intact.
/// </summary>
[Collection("Query cache controls")]
public class Iteration14CteLookupTests
{
    public sealed class CteNumberRow
    {
        public int n { get; set; }
    }

    public sealed class CteIdRow
    {
        public int id { get; set; }
    }

    [Fact]
    public void CteWarmReuse_AllocatesZero()
    {
        using var ctx = new InMemoryDataContext();

        var command = ctx
            .With("recent", ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("recent")
            .Select(t => new { id = t["id"].AsInt });

        // Prepare (hoist) once so the CTE list is the flat, normalized shape the warm lookup sees.
        command.PrepareCommand(false, CancellationToken.None);

        // Measured directly: the per-lookup cost of the detection on an already-prepared flat CTE
        // command, which is exactly the allocation the old eager HashSet removed from this path.
        for (var i = 0; i < 100; i++)
            _ = command.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = command.HasDataModifyingCte;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0, "a warm, flat CTE lookup must not allocate");
    }

    [Fact]
    public void RecursiveCteWarmReuse_AllocatesZero()
    {
        using var ctx = new InMemoryDataContext();

        var anchor = ctx.From<SimpleEntity>().Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var command = ctx
            .WithRecursive("nums", anchor.UnionAll(step))
            .From("nums")
            .Select(t => new CteNumberRow { n = t["n"].AsInt });

        command.PrepareCommand(false, CancellationToken.None);

        for (var i = 0; i < 100; i++)
            _ = command.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = command.HasDataModifyingCte;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0, "a warm, flat recursive CTE lookup must not allocate");
    }

    [Fact]
    public void NestedMutation_DisablesReadReuse()
    {
        using var ctx = new DmlCteTestContext();
        ctx.PurgeQueryCache();

        // The DML CTE is declared on the body of the outer read CTE, so the top-level command's own
        // list has no mutation and only the recursive fallback finds it.
        var insert = ctx.CreateInsertBuilder<SimpleEntity>().Value(x => x.Id, 1).Returning(x => new { x.Id });
        var mutationScope = ctx.With("ins", insert);
        var inner = mutationScope.From("ins").Select(r => new { r.Id });
        var outer = ctx.With("o", inner).From("o").Select(t => t["Id"].AsInt);

        outer.HasDataModifyingCte.Should().BeTrue();

        var first = ctx.GetPreparedQueryCommand(outer, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(outer, createEnumerator: false, storeInCache: true, CancellationToken.None);

        ReferenceEquals(first, second).Should()
            .BeFalse("a nested data-modifying CTE must bypass the plan cache call-locally");
        outer.Cache.Should().BeTrue("the sticky Cache flag must never be cleared");
    }

    [Fact]
    public void CyclicNestedGraph_TerminatesAndFindsMutation()
    {
        using var ctx = new InMemoryDataContext();

        var a = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var b = ctx.From<SimpleEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });

        // a -> b -> a: a reference-identity cycle the traversal must terminate on.
        a.Ctes = new[] { new CteDefinition("b", b) };
        b.Ctes = new[] { new CteDefinition("a", a) };

        var root = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        root.Ctes = new[] { new CteDefinition("a", a) };

        root.HasDataModifyingCte.Should().BeFalse("a mutation-free declaration cycle detects nothing and terminates");
    }

    [Fact]
    public void CyclicNestedGraph_WithDeepMutation_TerminatesAndFindsMutation()
    {
        using var ctx = new DmlCteTestContext();

        var insert = ctx.CreateInsertBuilder<SimpleEntity>().Value(x => x.Id, 1).Returning(x => new { x.Id });
        var mutationShape = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var mutation = new CteDefinition("m", mutationShape, new CteMutation(insert.BuildMutationCommand()));

        var a = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var b = ctx.From<SimpleEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });

        // a -> b -> {m, a}: deep mutation plus a cycle back to the root's declaration.
        a.Ctes = new[] { new CteDefinition("b", b) };
        b.Ctes = new[] { mutation, new CteDefinition("a", a) };

        var root = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        root.Ctes = new[] { new CteDefinition("a", a) };

        root.HasDataModifyingCte.Should().BeTrue("a mutation nested below a declaration cycle must still be found");
    }

    [Fact]
    public void NestedReadCteWarmReuse_AllocatesZero()
    {
        using var ctx = new InMemoryDataContext();

        // Outer read CTE whose body itself declares a read CTE: after preparation the root list is
        // hoisted flat, but the nested body keeps its own declarations, which used to make the getter
        // enter the recursive HashSet path on every warm lookup.
        var command = BuildNestedReadTyped(ctx, "alloc_inner", "alloc_outer", threshold: 0);
        command.PrepareCommand(false, CancellationToken.None);
        command.HasDataModifyingCte.Should().BeFalse("a nested read CTE declares no data-modifying body");

        for (var i = 0; i < 100; i++)
            _ = command.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = command.HasDataModifyingCte;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0, "a warm, prepared nested-read CTE lookup must not allocate a traversal set");
    }

    [Fact]
    public void CteReuse_VariantMatrix()
    {
        using var readCtx = new InMemoryDataContext();

        BuildFlatRead(readCtx).HasDataModifyingCte.Should().BeFalse("read flat cold");
        var flatPrepared = BuildFlatRead(readCtx);
        flatPrepared.PrepareCommand(false, CancellationToken.None);
        flatPrepared.HasDataModifyingCte.Should().BeFalse("read flat prepared");

        BuildNestedReadTyped(readCtx, "mx_read_inner_cold", "mx_read_outer_cold", threshold: 0)
            .HasDataModifyingCte.Should().BeFalse("read nested cold");
        var nestedPrepared = BuildNestedReadTyped(readCtx, "mx_read_inner_prep", "mx_read_outer_prep", threshold: 0);
        nestedPrepared.PrepareCommand(false, CancellationToken.None);
        nestedPrepared.HasDataModifyingCte.Should().BeFalse("read nested prepared");

        using var mutationCtx = new DmlCteTestContext();
        mutationCtx.PurgeQueryCache();

        BuildFlatMutation(mutationCtx).HasDataModifyingCte.Should().BeTrue("mutation flat cold");
        var flatMutationPrepared = BuildFlatMutation(mutationCtx);
        flatMutationPrepared.PrepareCommand(false, CancellationToken.None);
        flatMutationPrepared.HasDataModifyingCte.Should().BeTrue("mutation flat prepared");

        BuildNestedMutation(mutationCtx).HasDataModifyingCte.Should().BeTrue("mutation nested cold");
        var nestedMutationPrepared = BuildNestedMutation(mutationCtx);
        nestedMutationPrepared.PrepareCommand(false, CancellationToken.None);
        nestedMutationPrepared.HasDataModifyingCte.Should()
            .BeTrue("a nested mutation must not become reusable merely because its command is prepared");
    }

    [Fact]
    public void NestedReadCte_PreservesSqlParametersAndPlanKey()
    {
        using var ctx = new DmlCteTestContext();
        ctx.PurgeQueryCache();

        const int threshold = 7;
        var outer = BuildNestedReadTyped(ctx, "read_inner", "read_outer", threshold);
        outer.HasDataModifyingCte.Should().BeFalse();

        var first = (DbPreparedQueryCommand<CteIdRow>)ctx.GetPreparedQueryCommand(outer, false, true, CancellationToken.None);
        var second = (DbPreparedQueryCommand<CteIdRow>)ctx.GetPreparedQueryCommand(outer, false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeTrue("a read-only nested CTE must keep the warm plan-cache instance");

        var sql = first.SqlStmt;
        sql.Should().NotBeNull();
        sql!.Should().Contain("read_inner").And.Contain("read_outer");
        sql.IndexOf("read_inner", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("read_outer", StringComparison.Ordinal),
                "a hoisted dependency must be declared before its consumer");

        var parameters = first.DbCommandParams.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle("the nested-read CTE captures exactly one value");
        parameters[0].Value.Should().Be(threshold);
        parameters[0].ParameterName.Should().Be("threshold");

        // Independent expected snapshot: prepare a structurally identical nested-read CTE through a
        // separate command instance (storeInCache: false, so it is a fresh plan) and compare the
        // parameter name/value/CLR-type/order against it. Comparing `first` against itself (or its own
        // cached `second`, which is the same instance) could never fail; this snapshot can.
        var expectedOuter = BuildNestedReadTyped(ctx, "read_inner_expected", "read_outer_expected", threshold);
        var expected = (DbPreparedQueryCommand<CteIdRow>)ctx.GetPreparedQueryCommand(expectedOuter, false, false, CancellationToken.None);
        var expectedParams = expected.DbCommandParams.Cast<DbParameter>().ToArray();

        parameters.Select(p => p.ParameterName)
            .Should().Equal(expectedParams.Select(p => p.ParameterName), "parameter names and order are provider-independent");
        parameters.Select(p => p.Value)
            .Should().Equal(expectedParams.Select(p => p.Value), "parameter values are provider-independent");
        parameters.Select(p => p.Value!.GetType())
            .Should().Equal(expectedParams.Select(p => p.Value!.GetType()), "parameter value CLR types are provider-independent");
    }

    [Fact]
    public void NestedReadCte_PreparedWarmPolicy_AllocatesZeroAndSharesPlan()
    {
        using var ctx = new DmlCteTestContext();
        ctx.PurgeQueryCache();

        var outer = BuildNestedReadTyped(ctx, "sync_inner", "sync_outer", threshold: 0);

        // This pins what a provider-free core test can actually prove: the warm prepared-command policy
        // shared by every terminal family (DataContext.GetPreparedQueryCommand -> QueryPlanner
        // .GetPreparedQueryCommand, whose only HasDataModifyingCte read is QueryPlanner.cs:559). It is
        // NOT a real sync/async terminal test: the in-memory provider cannot execute a nested-read CTE
        // (see E04-terminal-probe.log: terminal fails in the materializer), and DmlCteTestContext has no
        // reader. Both terminal families statically converge on the `QueryPlanner.cs:559` guard closure
        // (E01-paths.md); that row is returned as a DO->PLAN candidate instead of a proxy named SyncAsync.
        var warm = ctx.GetPreparedQueryCommand(outer, createEnumerator: false, storeInCache: true, CancellationToken.None);

        outer.IsPrepared.Should().BeTrue();
        outer.HasDataModifyingCte.Should().BeFalse();

        for (var i = 0; i < 100; i++)
            _ = outer.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = outer.HasDataModifyingCte;
        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0,
            "the shared warm lookup policy must allocate nothing");

        var again = ctx.GetPreparedQueryCommand(outer, createEnumerator: false, storeInCache: true, CancellationToken.None);
        ReferenceEquals(warm, again).Should().BeTrue("all terminal families share one warm plan identity");
    }

    [Fact]
    public void NestedReadCte_AcyclicSharedBody_ClassifiesAndReusesWarmPlan()
    {
        using var ctx = new DmlCteTestContext();
        ctx.PurgeQueryCache();

        // Diamond (acyclic): one shared inner read CTE reachable through two different outer bodies.
        // `bodyB.Ctes` reuses `bodyA`'s exact CteDefinition instance, so the same declaration is
        // reachable by two paths with no cycle.
        var innerBody = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var bodyA = ctx.With("dia_inner", innerBody).From("dia_inner").Select(t => new { id = t["id"].AsInt });
        var sharedInner = bodyA.Ctes![0];
        var bodyB = ctx.With("dia_inner", innerBody).From("dia_inner").Select(t => new { id = t["id"].AsInt });
        bodyB.Ctes = new[] { sharedInner };

        var root = ctx
            .With("dia_a", bodyA)
            .With("dia_b", bodyB)
            .From("dia_a")
            .Select(t => new { id = t["id"].AsInt });

        root.HasDataModifyingCte.Should().BeFalse("a shared read-only body has no reachable mutation (cold)");

        var first = ctx.GetPreparedQueryCommand(root, false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(root, false, true, CancellationToken.None);
        ReferenceEquals(first, second).Should().BeTrue("a read-only diamond must keep one warm plan identity");
        root.HasDataModifyingCte.Should().BeFalse("a shared read body stays non-mutating after hoist (prepared)");

        for (var i = 0; i < 100; i++)
            _ = root.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = root.HasDataModifyingCte;
        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0,
            "a warm prepared diamond lookup must not allocate a traversal set");
    }

    [Fact]
    public void NestedReadCte_PreparedClone_KeepsClassificationAndWarmAlloc()
    {
        using var ctx = new DmlCteTestContext();
        ctx.PurgeQueryCache();

        var prepared = BuildNestedReadTyped(ctx, "clone_inner", "clone_outer", threshold: 0);
        prepared.PrepareCommand(false, CancellationToken.None);
        prepared.IsPrepared.Should().BeTrue();

        // Both clone APIs must carry the prepared flag and the flattened CTE list so the shared-body
        // nested probe still short-circuits on the warm path.
        AssertPreparedCloneReadOnlyAndAllocFree(prepared.Clone(), "Clone()");
        AssertPreparedCloneReadOnlyAndAllocFree(prepared.CloneForCache(), "CloneForCache()");
    }

    [Fact]
    public void NestedMutation_PreparedClone_KeepsClassification()
    {
        using var ctx = new DmlCteTestContext();
        ctx.PurgeQueryCache();

        var prepared = BuildNestedMutation(ctx);
        prepared.PrepareCommand(false, CancellationToken.None);

        prepared.Clone().HasDataModifyingCte.Should().BeTrue("Clone() must preserve a nested mutation");
        prepared.CloneForCache().HasDataModifyingCte.Should().BeTrue("CloneForCache() must preserve a nested mutation");
    }

    private static void AssertPreparedCloneReadOnlyAndAllocFree(QueryCommand clone, string api)
    {
        clone.IsPrepared.Should().BeTrue($"{api} of a prepared command must keep the prepared flag");
        clone.HasDataModifyingCte.Should().BeFalse($"{api} must preserve the read-only classification");

        for (var i = 0; i < 100; i++)
            _ = clone.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = clone.HasDataModifyingCte;
        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0,
            $"{api} warm getter must not allocate a traversal set");
    }

    private static QueryCommand BuildFlatRead(IDataContext ctx)
        => ctx.With("flat_read", ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("flat_read")
            .Select(t => new { id = t["id"].AsInt });

    private static QueryCommand<CteIdRow> BuildNestedReadTyped(IDataContext ctx, string innerName, string outerName, int threshold)
    {
        var inner = ctx
            .With(innerName, ctx.From<SimpleEntity>().Where(x => x.Id > threshold).Select(x => new { x.Id }))
            .From(innerName)
            .Select(t => new { id = t["id"].AsInt });

        return ctx
            .With(outerName, inner)
            .From(outerName)
            .Select(t => new CteIdRow { id = t["id"].AsInt });
    }

    private static QueryCommand BuildFlatMutation(DmlCteTestContext ctx)
    {
        var insert = ctx.CreateInsertBuilder<SimpleEntity>().Value(x => x.Id, 1).Returning(x => new { x.Id });
        return ctx.With("flat_mut", insert).From("flat_mut").Select(r => new { r.Id });
    }

    private static QueryCommand BuildNestedMutation(DmlCteTestContext ctx)
    {
        var insert = ctx.CreateInsertBuilder<SimpleEntity>().Value(x => x.Id, 1).Returning(x => new { x.Id });
        var inner = ctx.With("nested_mut_inner", insert).From("nested_mut_inner").Select(r => new { r.Id });
        return ctx.With("nested_mut_outer", inner).From("nested_mut_outer").Select(t => t["Id"].AsInt);
    }

    // ------------------------------------------------------------------------------------------------
    // Provider-free DML-capable context: lets the planner gate run without a database, mirroring the
    // minimal fake context used by TypedCteTests.
    // ------------------------------------------------------------------------------------------------

    private sealed class DmlCteTestDialect : SqlDialectBase
    {
        internal static readonly DmlCteTestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }

        public override bool SupportsDataModifyingCtes => true;
    }

    private sealed class DmlCteTestContext : DataContext
    {
        private readonly FakeConnection _connection = new();

        public DmlCteTestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => DmlCteTestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => new FakeParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString) => _connection;
    }

    private sealed class FakeParameter(string name) : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class FakeConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeCommand();
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly FakeParameterCollection _parameters = new();

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = null!;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        [AllowNull]
        protected override DbTransaction DbTransaction { get; set; } = null!;

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbParameter CreateDbParameter() => new FakeParameter(string.Empty);

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value!);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string? value) => _items.Any(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string? parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string? parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string? parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string? parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
                Add(value);
            else
                _items[index] = value;
        }
    }
}

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #146 typed CTE surface: the ordinary slice-A API (<c>AsCte</c>/<c>Cte&lt;T&gt;</c>/<c>From(Cte&lt;T&gt;)</c>)
/// and the slice-B recursive API (<c>CteReference</c>/<c>AsRecursiveCte</c>/<c>From(CteReference&lt;T&gt;)</c>).
/// Renders SQL through a provider-free fake context (never opens a database) and pins the public surface
/// while keeping the legacy CTE surface intact.
/// </summary>
public class TypedCteTests
{
    [SqlTable("typed_cte_entity")]
    private sealed class TypedCteEntity
    {
        public long Id { get; set; }
        public int Total { get; set; }
    }

    [SqlTable("typed_cte_filtered")]
    private sealed class TypedCteFilteredEntity
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
    }

    // Dedicated to the nested-derived-table probe: configured with a lowercase physical column so the
    // property name and physical name differ only by case. A type used by no other test avoids the
    // process-wide metadata cache (the first registration without configuration would win).
    [SqlTable("typed_cte_nested")]
    private sealed class TypedCteNestedEntity
    {
        public long Id { get; set; }
    }

    private sealed record TypedCteDto(long Id, int Total);

    private sealed class TypedCteMemberInit
    {
        public long Id { get; set; }
        public int Total { get; set; }
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }

        // The typed-CTE tests only need tuple construction to render so the unsupported whole-row
        // System.Tuple shape reaches the fail-fast guard instead of a provider-capability error.
        public override ITupleRenderer? Tuple => TestTupleRenderer.Instance;
    }

    private sealed class TestTupleRenderer : ITupleRenderer
    {
        internal static readonly TestTupleRenderer Instance = new();

        public string RenderConstructor(IReadOnlyList<string> fields) => "ROW(" + string.Join(", ", fields) + ")";

        public string? RenderElement(string row, int oneBasedIndex) => "(" + row + ").f" + oneBasedIndex;
    }

    private sealed class TestContext : DataContext
    {
        private readonly FakeConnection _connection = new();

        public TestContext() : base(new DataContextBuilder())
        {
        }

        // Spy on the existing provider boundary (no new production seam): the fail-fast rejections must
        // never open the connection or execute a command.
        public int DbConnectionsOpened => _connection.OpenCount;

        public int DbCommandsExecuted => _connection.CommandsExecuted;

        public override ISqlDialect Dialect => TestDialect.Instance;

        // Only the SQL text is asserted; the planner mints parameters while building the command.
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
        public int OpenCount { get; private set; }

        public int CommandsExecuted { get; private set; }

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

        public override void Open() => OpenCount++;

        internal void RecordExecution() => CommandsExecuted++;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeCommand(this);
    }

    private sealed class FakeCommand(FakeConnection connection) : DbCommand
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

        public override int ExecuteNonQuery()
        {
            connection.RecordExecution();
            throw new NotSupportedException();
        }

        public override object ExecuteScalar()
        {
            connection.RecordExecution();
            throw new NotSupportedException();
        }

        public override void Prepare()
        {
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            connection.RecordExecution();
            throw new NotSupportedException();
        }
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

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        return Normalize(prepared.DbCommand.CommandText);
    }

    [Fact]
    public void TypedCte_ShouldRenderBareCteReferenceWithoutDerivedWrapper()
    {
        using var ctx = new TestContext();

        var recent = ctx.From<TypedCteEntity>()
            .Select(x => new { x.Id, x.Total })
            .AsCte("recent");

        recent.Name.Should().Be("recent");
        recent.Definitions.Should().ContainSingle().Which.Name.Should().Be("recent");

        var sql = SqlOf(ctx, ctx.From(recent).Select(r => r.Id));

        sql.Should().Contain("recent");
        // The typed source reads the CTE name directly (the CTE body's own "(select ...)" is expected):
        // a derived-table wrapper would instead render "from (select ...) as t1".
        sql.Should().Contain("from recent");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void TypedCte_WholeEntitySource_ShouldNotReapplyEntityFilterOnOuterRead()
    {
        using var ctx = new TestContext();

        var scoped = ctx.From<TypedCteFilteredEntity>(b => b.HasQueryFilter(e => e.TenantId == 7))
            .ToCommand()
            .AsCte("scoped");

        var sql = SqlOf(ctx, ctx.From(scoped).Select(r => r.Id));

        // The filter is applied once, inside the defining CTE body; the outer read of the CTE must not
        // treat the whole-entity projection as a mapped source and inject the filter again.
        sql.Should().Contain("typed_cte_filtered");
        sql.Should().Contain("select Id, TenantId from typed_cte_filtered");
        sql.Should().Contain("where TenantId = 7");
        sql.Should().NotContain("where t1.TenantId = 7");
    }

    [Fact]
    public void TypedCte_JoinSource_ShouldNotInjectEntityFilterIntoJoin()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteFilteredEntity>(b => b.HasQueryFilter(e => e.TenantId == 7))
            .ToCommand()
            .AsCte("l");
        var right = ctx.From<TypedCteFilteredEntity>(b => b.HasQueryFilter(e => e.TenantId == 7))
            .ToCommand()
            .AsCte("r");

        var sql = SqlOf(ctx, ctx.From(left)
            .Join(ctx.From(right), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        // No entity filter is injected into the join ON: each CTE body carries its filter once, and the
        // joined typed sources are treated as projection sources, not mapped entities.
        sql.Should().Contain("join r as 't2' on t1.Id = t2.Id");
        sql.Should().NotContain("t2.TenantId = 7");
    }

    [Fact]
    public void Reachable_ShouldIncludeRootAndReferencedOnly()
    {
        using var ctx = new TestContext();

        var a = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        var unused = ctx.From<TypedCteEntity>().Select(x => new { x.Total });
        var scope = ctx.With("a", a);
        var body = scope.From("a").Select(t => new { id = t["Id"].AsInt });

        // Candidates as a descriptor would see them: its own definition, the referenced 'a', and an
        // unreferenced sibling. Only the root and the reached declaration survive.
        var reachable = CteHoister.Reachable(
            [new CteDefinition("recent", body), scope.Ctes[0], new CteDefinition("unused", unused)],
            new CteDefinition("recent", body));

        reachable.Select(d => d.Name).Should().Equal("recent", "a");
    }

    [Fact]
    public void TypedCte_Surface_ShouldExposeSliceAAndSliceB()
    {
        var core = typeof(DataContextExtensions).Assembly;

        // Slice B public surface present: the non-generic abstract identity, the generic typed
        // self-reference, both AsRecursiveCte overloads and the typed From(CteReference<T>).
        var nonGenericReference = core.GetType("NextORM.Core.CteReference");
        nonGenericReference.Should().NotBeNull();
        nonGenericReference!.IsAbstract.Should().BeTrue();
        core.GetType("NextORM.Core.CteReference`1").Should().NotBeNull();

        var recursiveOverloads = typeof(QueryCommand<>).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "AsRecursiveCte")
            .ToList();
        recursiveOverloads.Should().HaveCount(2);
        recursiveOverloads.Should().Contain(m => m.GetParameters().Length == 2);
        var withLimit = recursiveOverloads.Single(m => m.GetParameters().Length == 3);
        withLimit.GetParameters()[2].ParameterType.Should().Be(typeof(int));
        withLimit.GetParameters()[2].HasDefaultValue.Should().BeFalse();

        typeof(DataContextExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Should().Contain(m => m.Name == "From" && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.IsGenericType
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(CteReference<>));

        // Slice A public surface present.
        typeof(QueryCommand<>).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Should().Contain(m => m.Name == "AsCte");
        typeof(DataContextExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Should().Contain(m => m.Name == "From" && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.IsGenericType
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Cte<>));

        // Legacy CTE surface unchanged.
        typeof(CteQuery).GetMethod("With", [typeof(string), typeof(QueryCommand)]).Should().NotBeNull();
        typeof(CteQuery).GetMethod("WithRecursive", [typeof(string), typeof(QueryCommand), typeof(int?)]).Should().NotBeNull();
        typeof(CteQuery).GetMethod("From", [typeof(string)]).Should().NotBeNull();
        typeof(CteQuery).GetMethod("From", [typeof(CteDefinition)]).Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------------------------
    // §6 Select forms: descriptor carries the definition and the typed read renders the bare CTE name.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TypedCte_ScalarForm_ShouldReadBareCte()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => x.Id).AsCte("s");

        var sql = SqlOf(ctx, ctx.From(cte).Select(r => r + 1));

        sql.Should().Contain("from s");
        sql.Should().NotContain("from (select");
        // Slice-A regression (#146-B): the bare scalar source parameter must render once as the
        // source's readable column, not be re-visited by the lambda walk (which produced "(Id + 1)Id").
        sql.Should().Contain("(Id + 1)");
        sql.Should().NotContain("(Id + 1)Id");
        sql.Should().NotContain("( + 1)");
    }

    [Fact]
    public void TypedCte_CtorDtoForm_ShouldReadBareCte()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new TypedCteDto(x.Id, x.Total)).AsCte("d");

        var sql = SqlOf(ctx, ctx.From(cte).Select(r => new TypedCteDto(r.Id, r.Total)));

        sql.Should().Contain("from d");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void TypedCte_MemberInitForm_ShouldReadBareCte()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new TypedCteMemberInit { Id = x.Id, Total = x.Total }).AsCte("mi");

        var sql = SqlOf(ctx, ctx.From(cte).Select(r => new TypedCteMemberInit { Id = r.Id, Total = r.Total }));

        sql.Should().Contain("from mi");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void TypedCte_WholeEntityForm_ShouldReadBareCte()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().ToCommand().AsCte("e");

        var sql = SqlOf(ctx, ctx.From(cte).Select(r => r.Id));

        sql.Should().Contain("from e");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void TypedCte_SystemTupleForm_ShouldCarryDefinition()
    {
        using var ctx = new TestContext();

        // System.Tuple construction needs a provider with a native tuple type, so the SQL rendering is
        // covered by the PostgreSQL/ClickHouse suites. Here the descriptor contract is pinned.
        var cte = ctx.From<TypedCteEntity>().Select(x => Tuple.Create(x.Id, x.Total)).AsCte("tp");

        cte.Definitions.Should().ContainSingle().Which.Name.Should().Be("tp");
    }

    [Fact]
    public void TypedCte_SystemTupleForm_ShouldFailFast()
    {
        using var ctx = new TestContext();

        // §6 and the code-smells review finding 65: a whole-row System.Tuple is projected as one opaque column, so a typed CTE
        // read cannot address it — an identity read renders an empty select list and an ItemN read the
        // row-operand-less "().f1". The read must fail fast instead of emitting broken SQL.
        var cte = ctx.From<TypedCteEntity>().Select(x => Tuple.Create(x.Id, x.Total)).AsCte("tp");

        var act = () => SqlOf(ctx, ctx.From(cte).Select(r => r.Item1));

        act.Should().Throw<NotSupportedException>().WithMessage("*System.Tuple*");
    }

    [Fact]
    public void TypedCte_JoinProjectionForm_ShouldReadBareCte()
    {
        using var ctx = new TestContext();

        // A bare join command's result is Projection<...> (IProjection); its identity shape is slot-tagged
        // by the defining query and read back through the typed CTE.
        var projection = ctx.From<TypedCteEntity>()
            .Join(ctx.From<TypedCteEntity>(), (a, b) => a.Id == b.Id)
            .ToCommand()
            .AsCte("p");

        var sql = SqlOf(ctx, ctx.From(projection).Select(x => x.Item1.Id));

        sql.Should().Contain("from p");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void TypedCte_ValueTupleForm_ShouldFailFast()
    {
        using var ctx = new TestContext();

        // §6: ValueTuple gets no new support just because the classifier recognises it. The typed read
        // must reject the unsupported shape rather than fall back to a wrong materialization.
        var cte = ctx.From<TypedCteEntity>().Select(x => new ValueTuple<long, int>(x.Id, x.Total)).AsCte("vt");

        var act = () => SqlOf(ctx, ctx.From(cte).Select(r => r.Item1));

        act.Should().Throw<NotSupportedException>().WithMessage("*ValueTuple*");
    }

    [Fact]
    public void TypedCte_AsCte_ShouldRejectNullOrEmptyName()
    {
        using var ctx = new TestContext();
        var q = ctx.From<TypedCteEntity>().Select(x => new { x.Id });

        ((Action)(() => q.AsCte(""))).Should().Throw<ArgumentException>();
        ((Action)(() => q.AsCte(null!))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TypedCte_Descriptor_ShouldExposeNameAndSingleDefinition()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("only");

        cte.Name.Should().Be("only");

        // Identity is by reference: two descriptors over the same command are distinct.
        var other = cte.Query.AsCte("only");
        other.Should().NotBeSameAs(cte);
    }

    // ---------------------------------------------------------------------------------------------
    // §7 dependency graph, dedup and ordering.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TypedCte_ReferencingCte_ShouldOrderDependencyBeforeConsumer()
    {
        using var ctx = new TestContext();

        var root = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("a");
        var consumer = ctx.From(root).Select(x => x.Id).AsCte("b");

        consumer.Definitions.Select(d => d.Name).Should().Equal("a", "b");

        var sql = SqlOf(ctx, ctx.From(consumer).Select(x => x + 1));

        sql.Should().Contain("with a as (");
        sql.Should().Contain(", b as (");
    }

    [Fact]
    public void TypedCte_UnionBody_ShouldRenderSetOperationAndReadBareName()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });
        var right = ctx.From<TypedCteEntity>().Where(x => x.Id < 9).Select(x => new { x.Id });

        var union = left.Union(right).AsCte("u");

        var sql = SqlOf(ctx, ctx.From(union).Select(x => x.Id));

        sql.Should().Contain("with u as (");
        sql.Should().Contain("union");
        sql.Should().Contain("from u");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void TypedCte_UnionBodyAsDependency_ShouldOrderBeforeConsumer()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });
        var right = ctx.From<TypedCteEntity>().Where(x => x.Id < 9).Select(x => new { x.Id });

        var union = left.UnionAll(right).AsCte("u");
        var consumer = ctx.From(union).Select(x => new { x.Id }).AsCte("c");

        // §7: a set-operation body is a dependency like any other; it is hoisted before its consumer.
        consumer.Definitions.Select(d => d.Name).Should().Equal("u", "c");

        var sql = SqlOf(ctx, ctx.From(consumer).Select(x => x.Id));

        sql.Should().Contain("with u as (");
        sql.Should().Contain(", c as (");
        sql.Should().Contain("union all");
        sql.Should().Contain("from u");
    }

    [Fact]
    public void TypedCte_UnionBody_ShouldHoistDependencyReferencedInABranch()
    {
        using var ctx = new TestContext();

        var dep = ctx.From<TypedCteEntity>().Where(x => x.Total > 0).Select(x => new { x.Id }).AsCte("dep");
        var left = ctx.From(dep).Select(x => new { x.Id });
        var right = ctx.From<TypedCteEntity>().Where(x => x.Id < 9).Select(x => new { x.Id });

        var union = left.Union(right).AsCte("u");

        // §7: traversing the set-operation branches pulls in a CTE referenced by an operand.
        union.Definitions.Select(d => d.Name).Should().Equal("dep", "u");

        var sql = SqlOf(ctx, ctx.From(union).Select(x => x.Id));

        sql.Should().Contain("with dep as (");
        sql.Should().Contain(", u as (");
    }

    [Fact]
    public void TypedCte_UnionAllBody_ShouldPreserveTypedCteBranch()
    {
        using var ctx = new TestContext();

        var dep = ctx.From<TypedCteEntity>().Where(x => x.Total > 0).Select(x => new { x.Id }).AsCte("dep");
        var left = ctx.From(dep).Select(x => new { x.Id });
        var right = ctx.From<TypedCteEntity>().Where(x => x.Id < 9).Select(x => new { x.Id });

        var union = left.UnionAll(right).AsCte("u");

        // Same seam as the Union case, for the UnionAll set operation: ResetPreparation clears _from,
        // and a typed-CTE first operand stores its ColumnShape there, so the clone must restore it.
        union.Definitions.Select(d => d.Name).Should().Equal("dep", "u");

        var sql = SqlOf(ctx, ctx.From(union).Select(x => x.Id));

        sql.Should().Contain("with dep as (");
        sql.Should().Contain(", u as (");
        sql.Should().Contain("union all");
        sql.Should().Contain("from dep");
    }

    [Fact]
    public void TypedCte_SelfReferencingDeclaration_ShouldRejectCycleBeforeDb()
    {
        using var ctx = new TestContext();

        // The descriptor API cannot express a back-edge through the fluent scopes (a name reference
        // must point at an already-declared scope, and re-declaring the same name is rejected as a
        // duplicate). A raw declaration set can form the self-edge, and the hoister must reject it
        // rather than loop or overflow.
        var body = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        var self = new CteDefinition("cyc", body);
        body.Ctes = new[] { self };

        var act = () => CteHoister.Hoist(new[] { self });

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void TypedCte_MutualDeclarationCycle_ShouldRejectBeforeDb()
    {
        using var ctx = new TestContext();

        var aBody = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        var bBody = ctx.From<TypedCteEntity>().Select(x => new { x.Total });
        var a = new CteDefinition("a", aBody);
        var b = new CteDefinition("b", bBody);
        aBody.Ctes = new[] { b };
        bBody.Ctes = new[] { a };

        var act = () => CteHoister.Hoist(new[] { a });

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void CteHoister_RecursiveCteNonSelfDependency_ShouldOrderDependencyBeforeConsumer()
    {
        using var ctx = new TestContext();

        // Only the matching self-edge of a recursive declaration is exempt from the cycle check. A
        // different declaration that a recursive CTE references must still be ordered before it;
        // exempting every edge out of a recursive declaration would emit the dependency after its
        // consumer (and hide a real cycle through that dependency).
        var depBody = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        var dep = new CteDefinition("dep", depBody);

        var recursiveBody = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        recursiveBody.From = new FromExpression("dep");
        var recursive = new CteDefinition("rec", recursiveBody, recursive: true);

        // 'dep' is declared after its consumer; the hoister must still move it first.
        var ordered = CteHoister.Hoist([recursive, dep]);

        ordered!.Select(d => d.Name).Should().Equal("dep", "rec");
    }

    [Fact]
    public void TypedCte_JoinOfTwoCtes_ShouldCarryBothDefinitionsInOrder()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Where(x => x.Id > 1).Select(x => new { x.Id }).AsCte("l");
        var right = ctx.From<TypedCteEntity>().Where(x => x.Id < 9).Select(x => new { x.Id }).AsCte("r");

        var sql = SqlOf(ctx, ctx.From(left)
            .Join(ctx.From(right), (a, b) => a.Id == b.Id)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id }));

        sql.Should().Contain("with l as (");
        sql.Should().Contain(", r as (");
        sql.Should().Contain("join r as");
    }

    [Fact]
    public void TypedCte_JoinOfTwoCtesOfSameClrType_ShouldBindBySourceSlot()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Total }).AsCte("l");
        var right = ctx.From<TypedCteEntity>().Where(x => x.Total > 2).Select(x => new { x.Id, x.Total }).AsCte("r");

        var sql = SqlOf(ctx, ctx.From(left)
            .Join(ctx.From(right), (a, b) => a.Id == b.Id)
            .Select(p => new { L = p.Item1.Total, R = p.Item2.Total }));

        // Same CLR projection type on both sides must bind by source slot: each CTE body keeps its own
        // predicate and the outer projection reads the matching ItemN column.
        sql.Should().Contain("with l as (select Id, Total from typed_cte_entity");
        sql.Should().Contain("), r as (select Id, Total from typed_cte_entity");
        sql.Should().Contain("join r as 't2' on t1.Id = t2.Id");
        sql.Should().Contain("select t1.Total, t2.Total");
    }

    [Fact]
    public void TypedCte_SelfJoinOfSameDescriptor_ShouldEmitDefinitionOnce()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("s");

        var sql = SqlOf(ctx, ctx.From(cte)
            .Join(ctx.From(cte), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        Occurrences(sql, "s as (").Should().Be(1);
        sql.Should().Contain("join s as 't2'");
    }

    [Fact]
    public void TypedCte_TwoDistinctDescriptorsWithSameName_ShouldRejectBeforeDb()
    {
        using var ctx = new TestContext();

        var first = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("dup");
        var second = ctx.From<TypedCteEntity>().Where(x => x.Id > 2).Select(x => new { x.Id }).AsCte("dup");

        var act = () => SqlOf(ctx, ctx.From(first)
            .Join(ctx.From(second), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        // Different declarations under one name are rejected by the existing Ordinal semantics, before
        // any DB round-trip.
        act.Should().Throw<InvalidOperationException>().WithMessage("*'dup'*");
    }

    // ---------------------------------------------------------------------------------------------
    // #159 direct Cte<T> join overloads: a descriptor passed straight to the seven operators must
    // behave exactly like converting it with the receiving context first (ctx.From(cte)).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TypedCte_DirectJoin_ShouldMatchConvertedForm()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Where(x => x.Id > 1).Select(x => new { x.Id }).AsCte("l");
        var right = ctx.From<TypedCteEntity>().Where(x => x.Id < 9).Select(x => new { x.Id }).AsCte("r");

        var direct = SqlOf(ctx, ctx.From(left)
            .Join(right, (a, b) => a.Id == b.Id)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id }));
        var converted = SqlOf(ctx, ctx.From(left)
            .Join(ctx.From(right), (a, b) => a.Id == b.Id)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id }));

        direct.Should().Be(converted);
        direct.Should().Contain("with l as (");
        direct.Should().Contain(", r as (");
        direct.Should().Contain("join r as");
    }

    [Theory]
    [InlineData("Join")]
    [InlineData("LeftJoin")]
    [InlineData("RightJoin")]
    [InlineData("FullJoin")]
    public void TypedCte_DirectConditionalOperators_ShouldMatchConvertedForm(string operation)
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("l");
        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        QueryCommand<long> Direct() => operation switch
        {
            "Join" => ctx.From(left).Join(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "LeftJoin" => ctx.From(left).LeftJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "RightJoin" => ctx.From(left).RightJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "FullJoin" => ctx.From(left).FullJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            _ => throw new NotSupportedException(),
        };
        QueryCommand<long> Converted() => operation switch
        {
            "Join" => ctx.From(left).Join(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "LeftJoin" => ctx.From(left).LeftJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "RightJoin" => ctx.From(left).RightJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            "FullJoin" => ctx.From(left).FullJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
            _ => throw new NotSupportedException(),
        };

        SqlOf(ctx, Direct()).Should().Be(SqlOf(ctx, Converted()));
    }

    [Fact]
    public void TypedCte_DirectCrossJoin_ShouldMatchConvertedForm()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("l");
        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        QueryCommand<long> Direct = ctx.From(left).CrossJoin(right).Select(p => p.Item1.Id);
        QueryCommand<long> Converted = ctx.From(left).CrossJoin(ctx.From(right)).Select(p => p.Item1.Id);

        SqlOf(ctx, Direct).Should().Be(SqlOf(ctx, Converted));
    }

    [Theory]
    [InlineData("CrossApply")]
    [InlineData("OuterApply")]
    public void TypedCte_DirectApply_UnsupportedDialect_ShouldMatchConvertedRejection(string operation)
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("l");
        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        QueryCommand<long> Direct = operation switch
        {
            "CrossApply" => ctx.From(left).CrossApply(right).Select(p => p.Item1.Id),
            _ => ctx.From(left).OuterApply(right).Select(p => p.Item1.Id),
        };
        QueryCommand<long> Converted = operation switch
        {
            "CrossApply" => ctx.From(left).CrossApply(ctx.From(right)).Select(p => p.Item1.Id),
            _ => ctx.From(left).OuterApply(ctx.From(right)).Select(p => p.Item1.Id),
        };

        // Unsupported APPLY must keep the exact full-form capability rejection.
        var directEx = Record.Exception(() => SqlOf(ctx, Direct));
        var convertedEx = Record.Exception(() => SqlOf(ctx, Converted));
        directEx.Should().BeOfType<NotSupportedException>();
        convertedEx.Should().BeOfType<NotSupportedException>();
        directEx!.Message.Should().Be(convertedEx!.Message);
    }

    [Fact]
    public void TypedCte_DirectJoin_WithExplicitOptions_ShouldMatchConvertedForm()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("l");
        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        var direct = SqlOf(ctx, ctx.From(left)
            .Join(right, (a, b) => a.Id == b.Id, o => o.SuppressCartesianWarning())
            .Select(p => p.Item1.Id));
        var converted = SqlOf(ctx, ctx.From(left)
            .Join(ctx.From(right), (a, b) => a.Id == b.Id, o => o.SuppressCartesianWarning())
            .Select(p => p.Item1.Id));

        direct.Should().Be(converted);
    }

    [Fact]
    public void TypedCte_DirectJoin_NullCte_ShouldFailBeforeSourceWork()
    {
        using var ctx = new TestContext();

        var other = ctx.From<TypedCteEntity>().ToCommand().AsCte("b");
        Cte<TypedCteEntity>? missing = null;

        var act = () => ctx.From(other).Join(missing!, (a, b) => a.Id == b.Id);

        act.Should().Throw<ArgumentNullException>().Where(e => e.ParamName == "cte");
    }

    [Fact]
    public void TypedCte_DirectJoin_NullPredicate_ShouldFailBeforeSourceWork()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().ToCommand().AsCte("a");
        var right = ctx.From<TypedCteEntity>().ToCommand().AsCte("b");
        System.Linq.Expressions.Expression<Func<TypedCteEntity, TypedCteEntity, bool>>? predicate = null;

        var act = () => ctx.From(left).Join(right, predicate!);

        act.Should().Throw<ArgumentNullException>().Where(e => e.ParamName == "joinCondition");
    }

    [Fact]
    public void TypedCte_DirectJoin_ShouldPreserveDependencyOrder()
    {
        using var ctx = new TestContext();

        var dependency = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("dep");
        var consumer = ctx.From(dependency).Select(x => new { x.Id }).AsCte("consumer");
        var root = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("root");

        var sql = SqlOf(ctx, ctx.From(root)
            .Join(consumer, (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        sql.Should().Contain("with root as (");
        sql.Should().Contain("dep as (");
        sql.Should().Contain("consumer as (");
        // Dependency-before-consumer: the transitive dependency is declared before its consumer.
        sql.IndexOf("dep as (", StringComparison.Ordinal).Should().BeLessThan(sql.IndexOf("consumer as (", StringComparison.Ordinal));
    }

    [Fact]
    public void TypedCte_DirectSelfJoin_ShouldEmitDefinitionOnce()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("s");

        var sql = SqlOf(ctx, ctx.From(cte)
            .Join(cte, (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        Occurrences(sql, "s as (").Should().Be(1);
        sql.Should().Contain("join s as 't2'");
    }

    [Fact]
    public void TypedCte_DirectJoin_ShouldNotMutateSharedCommandCacheFlag()
    {
        using var ctx = new TestContext();

        var left = ctx.From<TypedCteEntity>().ToCommand().AsCte("a");
        var right = ctx.From<TypedCteEntity>().ToCommand().AsCte("b");
        var command = ctx.From(left).Join(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id);

        command.Cache.Should().BeTrue();
        SqlOf(ctx, command);
        command.Cache.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // #159 TableAlias (named-table) receiver: the non-generic EntityBuilder Cte<T> overloads must
    // behave exactly like converting the descriptor with the receiving context first, including the
    // JoinSourceResolver path they route through.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Join")]
    [InlineData("LeftJoin")]
    [InlineData("RightJoin")]
    [InlineData("FullJoin")]
    public void TypedCte_TableAliasReceiver_DirectConditionalOperators_ShouldMatchConvertedForm(string operation)
    {
        using var ctx = new TestContext();

        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        QueryCommand<long> Direct() => operation switch
        {
            "Join" => ctx.From("typed_cte_entity").Join(right, (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            "LeftJoin" => ctx.From("typed_cte_entity").LeftJoin(right, (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            "RightJoin" => ctx.From("typed_cte_entity").RightJoin(right, (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            "FullJoin" => ctx.From("typed_cte_entity").FullJoin(right, (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            _ => throw new NotSupportedException(),
        };
        QueryCommand<long> Converted() => operation switch
        {
            "Join" => ctx.From("typed_cte_entity").Join(ctx.From(right), (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            "LeftJoin" => ctx.From("typed_cte_entity").LeftJoin(ctx.From(right), (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            "RightJoin" => ctx.From("typed_cte_entity").RightJoin(ctx.From(right), (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            "FullJoin" => ctx.From("typed_cte_entity").FullJoin(ctx.From(right), (t, b) => t.GetInt64("Id") == b.Id).Select(p => p.Item2.Id),
            _ => throw new NotSupportedException(),
        };

        var direct = SqlOf(ctx, Direct());
        direct.Should().Be(SqlOf(ctx, Converted()));
        direct.Should().Contain("r as (");
    }

    [Fact]
    public void TypedCte_TableAliasReceiver_DirectCrossJoin_ShouldMatchConvertedForm()
    {
        using var ctx = new TestContext();

        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        var direct = SqlOf(ctx, ctx.From("typed_cte_entity").CrossJoin(right).Select(p => p.Item2.Id));
        var converted = SqlOf(ctx, ctx.From("typed_cte_entity").CrossJoin(ctx.From(right)).Select(p => p.Item2.Id));

        direct.Should().Be(converted);
    }

    [Theory]
    [InlineData("CrossApply")]
    [InlineData("OuterApply")]
    public void TypedCte_TableAliasReceiver_DirectApply_UnsupportedDialect_ShouldMatchConvertedRejection(string operation)
    {
        using var ctx = new TestContext();

        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        QueryCommand<long> Direct() => operation switch
        {
            "CrossApply" => ctx.From("typed_cte_entity").CrossApply(right).Select(p => p.Item2.Id),
            _ => ctx.From("typed_cte_entity").OuterApply(right).Select(p => p.Item2.Id),
        };
        QueryCommand<long> Converted() => operation switch
        {
            "CrossApply" => ctx.From("typed_cte_entity").CrossApply(ctx.From(right)).Select(p => p.Item2.Id),
            _ => ctx.From("typed_cte_entity").OuterApply(ctx.From(right)).Select(p => p.Item2.Id),
        };

        var directEx = Record.Exception(() => SqlOf(ctx, Direct()));
        var convertedEx = Record.Exception(() => SqlOf(ctx, Converted()));
        directEx.Should().BeOfType<NotSupportedException>();
        convertedEx.Should().BeOfType<NotSupportedException>();
        directEx!.Message.Should().Be(convertedEx!.Message);
    }

    [Fact]
    public void TypedCte_TableAliasReceiver_NamedArguments_ShouldMatchPositional()
    {
        using var ctx = new TestContext();

        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        var positional = SqlOf(ctx, ctx.From("typed_cte_entity")
            .Join(right, (t, b) => t.GetInt64("Id") == b.Id)
            .Select(p => p.Item2.Id));
        var named = SqlOf(ctx, ctx.From("typed_cte_entity")
            .Join(cte: right, joinCondition: (t, b) => t.GetInt64("Id") == b.Id, options: null)
            .Select(p => p.Item2.Id));

        named.Should().Be(positional);
    }

    [Fact]
    public void TypedCte_TableAliasReceiver_NullCte_ShouldFailBeforeSourceWork()
    {
        using var ctx = new TestContext();

        Cte<TypedCteEntity>? missing = null;

        var act = () => ctx.From("typed_cte_entity").Join(missing!, (t, b) => t.GetInt64("Id") == b.Id);

        act.Should().Throw<ArgumentNullException>().Where(e => e.ParamName == "cte");
    }

    [Fact]
    public void TypedCte_TableAliasReceiver_AllFourteenOverloads_CompileAndBuild()
    {
        // R159-06 compile coverage: the non-generic EntityBuilder (TableAlias) receiver must expose all
        // 14 Cte<T> overloads - four conditional operators and CrossJoin/CrossApply/OuterApply, each in
        // the concise and explicit-options forms. Building (never rendering) each one pins the surface.
        using var ctx = new TestContext();

        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("r");

        ctx.From("typed_cte_entity").Join(right, (t, b) => t.GetInt64("Id") == b.Id).Should().NotBeNull();
        ctx.From("typed_cte_entity").Join(right, (t, b) => t.GetInt64("Id") == b.Id, options: null).Should().NotBeNull();
        ctx.From("typed_cte_entity").LeftJoin(right, (t, b) => t.GetInt64("Id") == b.Id).Should().NotBeNull();
        ctx.From("typed_cte_entity").LeftJoin(right, (t, b) => t.GetInt64("Id") == b.Id, options: null).Should().NotBeNull();
        ctx.From("typed_cte_entity").RightJoin(right, (t, b) => t.GetInt64("Id") == b.Id).Should().NotBeNull();
        ctx.From("typed_cte_entity").RightJoin(right, (t, b) => t.GetInt64("Id") == b.Id, options: null).Should().NotBeNull();
        ctx.From("typed_cte_entity").FullJoin(right, (t, b) => t.GetInt64("Id") == b.Id).Should().NotBeNull();
        ctx.From("typed_cte_entity").FullJoin(right, (t, b) => t.GetInt64("Id") == b.Id, options: null).Should().NotBeNull();
        ctx.From("typed_cte_entity").CrossJoin(right).Should().NotBeNull();
        ctx.From("typed_cte_entity").CrossJoin(right, options: null).Should().NotBeNull();
        ctx.From("typed_cte_entity").CrossApply(right).Should().NotBeNull();
        ctx.From("typed_cte_entity").CrossApply(right, options: null).Should().NotBeNull();
        ctx.From("typed_cte_entity").OuterApply(right).Should().NotBeNull();
        ctx.From("typed_cte_entity").OuterApply(right, options: null).Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------------------------
    // C3 regression pin: the TableAlias generic JoinCore resolves the joined builder's explicit
    // source through JoinSourceResolver. Plain mapped joins must keep falling back to entity
    // metadata; explicit sources (raw statement / derived query / configured table) must resolve
    // to that source instead of silently ignoring it.
    // ---------------------------------------------------------------------------------------------

    [SqlTable("typed_cte_ta_entity")]
    private sealed class TypedCteTableAliasEntity
    {
        public long Id { get; set; }
    }

    [SqlTableFunction("typed_cte_tvf")]
    private static IQueryable<TypedCteEntity> TypedCteRows() => throw new NotSupportedException();

    [Fact]
    public void TypedCte_TableAliasReceiver_PlainTypedJoin_ShouldUseEntityMetadata()
    {
        using var ctx = new TestContext();

        var sql = SqlOf(ctx, ctx.From("typed_cte_entity")
            .Join(ctx.From<TypedCteEntity>(), (t, b) => t.GetInt64("Id") == b.Id)
            .Select(p => p.Item2.Id));

        sql.Should().Contain("typed_cte_entity");
    }

    [Fact]
    public void TypedCte_TableAliasReceiver_ExplicitRawStatementSource_ShouldUseJoinedSource()
    {
        using var ctx = new TestContext();

        var bound = ctx.FromSql("select Id from typed_cte_raww").BindEntity<TypedCteEntity>(["Id"]);

        var cmd = ctx.From("typed_cte_entity")
            .Join(bound, (t, b) => t.GetInt64("Id") == b.Id)
            .Select(p => p.Item2.Id);

        // The in-memory test dialect cannot render a raw FROM source, so assert the resolved join source
        // structurally: the raw FromSql source must reach the join instead of falling back to metadata.
        cmd.PrepareCommand(false, CancellationToken.None);
        var join = cmd.Joins.Should().ContainSingle().Subject;
        join.From.Should().NotBeNull();
        join.From!.RawSqlSource.Should().NotBeNull();
        join.From!.RawSqlSource!.Sql.Should().Be("select Id from typed_cte_raww");
    }

    [Fact]
    public void TypedCte_TableAliasReceiver_DerivedQuerySource_ShouldUseJoinedSource()
    {
        using var ctx = new TestContext();

        var derived = ctx.From<TypedCteEntity>().Where(x => x.Id > 0).ToCommand();
        var bound = ctx.From(derived);

        var sql = SqlOf(ctx, ctx.From("typed_cte_entity")
            .Join(bound, (t, b) => t.GetInt64("Id") == b.Id)
            .Select(p => p.Item2.Id));

        // The derived query is honored as a subquery source rather than replaced by entity metadata.
        sql.Should().Contain("join (select");
    }

    [Fact]
    public void TypedCte_TableAliasReceiver_ConfiguredTableSource_ShouldUseEntityMetadata()
    {
        using var ctx = new TestContext();

        var bound = ctx.From<TypedCteTableAliasEntity>(b => b.Table("typed_cte_ta_override"));

        var sql = SqlOf(ctx, ctx.From("typed_cte_entity")
            .Join(bound, (t, b) => t.GetInt64("Id") == b.Id)
            .Select(p => p.Item2.Id));

        sql.Should().Contain("typed_cte_ta_override");
    }

    // E159-R2-TVF: the TableAlias receiver's generic JoinCore resolves the joined builder's explicit
    // `_from` through JoinSourceResolver, so a table-valued function source reaches the join instead of
    // being replaced by entity metadata (JoinSourceResolver.cs:11-14, EntityBuilder.cs:4478).
    [Fact]
    public void TypedCte_TableAliasReceiver_TableValuedFunctionSource_ShouldUseJoinedSource()
    {
        using var ctx = new TestContext();

        var tvf = ctx.FromTableFunction(() => TypedCteRows());

        var cmd = ctx.From("typed_cte_entity")
            .Join(tvf, (t, b) => t.GetInt64("Id") == b.Id)
            .Select(p => p.Item2.Id);

        cmd.PrepareCommand(false, CancellationToken.None);
        var join = cmd.Joins.Should().ContainSingle().Subject;
        join.From.Should().NotBeNull();
        join.From!.TableFunction.Should().NotBeNull();
        join.From!.TableFunction!.Name.Should().Be("typed_cte_tvf");
    }

    // E159-R2-SA: Semi/Anti on the TableAlias receiver stay on the metadata fallback
    // (`GetFrom(typeof(TJoinEntity))` - EntityBuilder.cs:4448,4462) and do not route the joined
    // builder's explicit source through JoinSourceResolver. A derived-query source used as the joined
    // builder is therefore ignored by Semi/Anti: the join reads the mapped table, not the subquery.
    [Fact]
    public void TypedCte_TableAliasReceiver_SemiAntiJoin_ShouldKeepEntityMetadataFallback()
    {
        using var ctx = new TestContext();

        var derived = ctx.From<TypedCteEntity>().Where(x => x.Id > 0).ToCommand();
        var bound = ctx.From(derived);

        var semi = ctx.From("typed_cte_entity")
            .SemiJoin(bound, (t, b) => t.GetInt64("Id") == b.Id)
            .Select(t => t.GetInt64("Id"));
        var anti = ctx.From("typed_cte_entity")
            .AntiJoin(bound, (t, b) => t.GetInt64("Id") == b.Id)
            .Select(t => t.GetInt64("Id"));

        semi.PrepareCommand(false, CancellationToken.None);
        anti.PrepareCommand(false, CancellationToken.None);

        foreach (var join in new[] { semi.Joins.Should().ContainSingle().Subject, anti.Joins.Should().ContainSingle().Subject })
        {
            join.From.Should().NotBeNull();
            join.From!.SubQuery.Should().BeNull();
            join.From!.TableFunction.Should().BeNull();
            join.From!.Table.Should().Be("typed_cte_entity");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // §9 / D5 lifecycle: cloning and shared command flags.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TypedCte_Clone_ShouldPreserveShape()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("c");

        var original = ctx.From(cte).Select(x => x.Id);
        var clone = (QueryCommand<long>)original.Clone();

        SqlOf(ctx, clone).Should().Be(SqlOf(ctx, original));
    }

    [Fact]
    public void TypedCte_Preparation_ShouldNotMutateSharedCommandFlags()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("c");
        var command = ctx.From(cte).Select(x => x.Id);

        command.Cache.Should().BeTrue();
        SqlOf(ctx, command);
        command.Cache.Should().BeTrue();
    }

    [Fact]
    public void TypedCte_SharedDescriptor_ShouldPrepareIndependentCommands()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("shared");

        var first = SqlOf(ctx, ctx.From(cte).Where(x => x.Id > 1).Select(x => x.Id));
        var second = SqlOf(ctx, ctx.From(cte).Where(x => x.Id < 9).Select(x => x.Id));

        first.Should().Contain("where (t1.Id > 1)");
        second.Should().Contain("where (t1.Id < 9)");
    }

    [Fact]
    public void TypedCte_ChainedSameProjectionType_ShouldReadThroughConsumerCte()
    {
        using var ctx = new TestContext();

        var inner = ctx.From<TypedCteEntity>().Select(x => new { x.Id, x.Total }).AsCte("i");
        var outer = ctx.From(inner).Select(o => new { o.Id, o.Total }).AsCte("o");

        var sql = SqlOf(ctx, ctx.From(outer).Select(x => x.Id));

        sql.Should().Contain("with i as (");
        sql.Should().Contain(", o as (");
        sql.Should().Contain("from i");
        sql.Should().Contain("from o");
    }

    [Fact]
    public void TypedCte_ChainedDifferingProjectionType_ShouldReadThroughConsumerCte()
    {
        using var ctx = new TestContext();

        var inner = ctx.From<TypedCteEntity>().Select(x => new { x.Id, x.Total }).AsCte("i");
        var outer = ctx.From(inner).Select(o => new { o.Id }).AsCte("o");

        var sql = SqlOf(ctx, ctx.From(outer).Select(x => x.Id));

        sql.Should().Contain("with i as (");
        sql.Should().Contain(", o as (");
        sql.Should().Contain("from i");
        sql.Should().Contain("from o");
    }

    [Fact]
    public void TypedCte_ChainedDtoProjectionType_ShouldReadThroughConsumerCte()
    {
        using var ctx = new TestContext();

        var inner = ctx.From<TypedCteEntity>().Select(x => new TypedCteDto(x.Id, x.Total)).AsCte("i");
        var outer = ctx.From(inner).Select(o => new TypedCteDto(o.Id, o.Total)).AsCte("o");

        var sql = SqlOf(ctx, ctx.From(outer).Select(x => x.Id));

        sql.Should().Contain("with i as (");
        sql.Should().Contain(", o as (");
        sql.Should().Contain("from i");
        sql.Should().Contain("from o");
    }

    // ---------------------------------------------------------------------------------------------
    // #146-A audit: preparation-reset operators must preserve the typed-CTE projection source.
    // ResetPreparation clears _from, which carries the ColumnShape marker; an operator that resets
    // after cloning must restore it, exactly like the set-operation fix.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TypedCte_Distinct_ShouldKeepProjectionSource()
    {
        using var ctx = new TestContext();
        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("d");

        var sql = SqlOf(ctx, ctx.From(cte).Select(x => x.Id).Distinct());

        sql.Should().Contain("distinct");
        sql.Should().Contain("from d");
    }

    [Fact]
    public void TypedCte_Paging_ShouldKeepProjectionSource()
    {
        using var ctx = new TestContext();
        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("p");

        var sql = SqlOf(ctx, ctx.From(cte).Select(x => x.Id).Limit(5));

        sql.Should().Contain("from p");
    }

    [Fact]
    public void TypedCte_OrderByIndex_ShouldKeepProjectionSource()
    {
        using var ctx = new TestContext();
        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("o");

        var sql = SqlOf(ctx, ctx.From(cte).Select(x => x.Id).OrderBy(1));

        sql.Should().Contain("from o");
        sql.Should().Contain("order by");
    }

    [Fact]
    public void TypedCte_OrderByExpression_ShouldKeepProjectionSource()
    {
        using var ctx = new TestContext();
        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("oe");

        var sql = SqlOf(ctx, ctx.From(cte).Select(x => x.Id).OrderBy(x => x));

        sql.Should().Contain("from oe");
    }

    [Fact]
    public void TypedCte_WithForJson_ShouldKeepProjectionSource()
    {
        using var ctx = new TestContext();
        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("j");

        var command = ctx.From(cte).Select(x => x.Id).WithForJson();

        command.From.Should().NotBeNull();
    }

    [Fact]
    public void TypedCte_WithForXml_ShouldKeepProjectionSource()
    {
        using var ctx = new TestContext();
        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("x");

        var command = ctx.From(cte).Select(x => x.Id).WithForXml();

        command.From.Should().NotBeNull();
    }

    [Fact]
    public void TypedCte_CloneWithoutUnion_ShouldKeepProjectionSource()
    {
        using var ctx = new TestContext();
        var cte = ctx.From<TypedCteEntity>().Select(x => new { x.Id }).AsCte("cu");
        var right = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        var union = ctx.From(cte).Select(x => new { x.Id }).UnionAll(right);

        var first = union.CloneWithoutUnion();

        // The in-memory set-operation path materialises the first operand on its own; the typed-CTE
        // projection source must survive the union removal or the operand cannot be prepared.
        first.From.Should().NotBeNull();
        SqlOf(ctx, first).Should().Contain("from cu");
    }

    [Fact]
    public void TypedCte_TupleWholeShapeRead_ShouldFailFast()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => Tuple.Create(x.Id, x.Total)).AsCte("tpw");

        // Audit candidate 3: the System.Tuple guard in PrepareColumns only runs when a Select is present
        // (`cmd._exp is not null`), so a whole-shape read (no Select) reaches the generic classification
        // path instead. It still fails fast here with QueryPreparationException and never emits invalid
        // SQL, so the bypass is benign; this test pins that fail-fast behaviour.
        var act = () => SqlOf(ctx, ctx.From(cte).ToCommand());

        act.Should().Throw<QueryPreparationException>();
    }

    [Fact]
    public void TypedCte_ComputedScalarBody_ShouldRenderReadableColumn()
    {
        using var ctx = new TestContext();

        // Audit candidate 4: an unaliased computed scalar body column has no PropertyName. The
        // declaration must alias it to a generated identifier and the whole-shape consumer must read
        // that identifier, not re-render the defining expression against the CTE (which has no column
        // named after the defining source).
        var cte = ctx.From<TypedCteEntity>().Select(x => x.Total * 2).AsCte("cs");

        var sql = SqlOf(ctx, ctx.From(cte).ToCommand());

        sql.Should().Contain("from cs");
        sql.Should().Contain("c0");
    }

    [Fact]
    public void Reachable_ShouldIgnorePhysicalTableNameMatchingSiblingCteName()
    {
        using var ctx = new TestContext();

        // Audit candidate 7: a plain mapped-entity body lists its physical table name; if that name
        // coincides with a sibling candidate declaration, the reachability closure must not drag the
        // unrelated declaration in (it would shadow the physical table in the emitted WITH).
        var body = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        var root = new CteDefinition("root", body);
        var sibling = new CteDefinition("typed_cte_entity", ctx.From<TypedCteEntity>().Select(x => new { x.Total }));

        var reachable = CteHoister.Reachable([root, sibling], root);

        reachable.Select(d => d.Name).Should().Equal("root");
    }

    [Fact]
    public void Reachable_OnPreparedBody_ShouldIgnorePhysicalTableNameMatchingSiblingCteName()
    {
        using var ctx = new TestContext();

        // The physical table name is materialized into From.Table only after the body is prepared, so
        // the same coincidence must not over-include for an already-executed body either.
        var body = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        SqlOf(ctx, body);
        body.From.Should().NotBeNull();

        var root = new CteDefinition("root", body);
        var sibling = new CteDefinition("typed_cte_entity", ctx.From<TypedCteEntity>().Select(x => new { x.Total }));

        var reachable = CteHoister.Reachable([root, sibling], root);

        reachable.Select(d => d.Name).Should().Equal("root");
    }

    [Fact]
    public void EnsureNoUnhoistedCtes_ShouldDetectDeclarationOnColumnShape()
    {
        using var ctx = new TestContext();

        // Audit candidate 9: a FromExpression.ColumnShape command may itself carry declarations; the
        // unhoisted-declaration walk must traverse it, otherwise a nested WITH could be rendered.
        var innerBody = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        var inner = new CteDefinition("inner", innerBody);

        var shape = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        shape.Ctes = [inner];

        var root = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        root.From = new FromExpression("shape_source", shape);

        var act = () => CteHoister.EnsureNoUnhoistedCtes(root, Array.Empty<CteDefinition>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*inner*");
    }

    [Fact]
    public void TypedCte_NestedDerivedTableInBody_ShouldNotInheritExactProjectionAliases()
    {
        using var ctx = new TestContext();

        // Audit candidate 5: the exact-projection-aliases flag scopes to a typed CTE declaration's own
        // select list. A nested derived table inside the body is an ordinary subquery and must keep the
        // default case-insensitive aliasing; otherwise it exposes the column under its property name
        // (`id as 'Id'`) while the enclosing body still references the physical name, a mismatch.
        var nested = ctx.From<TypedCteNestedEntity>(b => b.Property(x => x.Id).HasColumnName("id"))
            .Select(x => new { x.Id });
        var body = ctx.From(nested).Select(x => new { x.Id });
        var cte = body.AsCte("nested");

        var sql = SqlOf(ctx, ctx.From(cte).Select(x => x.Id));

        sql.Should().Contain("(select id from typed_cte_nested)");
        sql.Should().NotContain("id as 'Id'");
        sql.Should().Contain("from nested");
    }

    [Fact]
    public void CollectReferencedNames_ShouldTraverseColumnShapeSource()
    {
        using var ctx = new TestContext();

        // MF6c gap: a 3-level typed-CTE chain (producer -> middle -> consumer) whose middle source
        // references the producer only through a FromExpression.ColumnShape marker. The middle's own
        // table name is not a declaration, so the producer is reachable only by traversing the marker's
        // defining command. Dropping that traversal silently truncates the hoisted set to the middle.
        var producerBody = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        producerBody.From = new FromExpression("producer");
        var producer = new CteDefinition("producer", producerBody);

        var middleBody = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        middleBody.From = new FromExpression("middle_source", producerBody);
        var middle = new CteDefinition("middle", middleBody);

        var consumerBody = ctx.From<TypedCteEntity>().Select(x => new { x.Id });
        consumerBody.From = new FromExpression("middle");
        var consumer = new CteDefinition("consumer", consumerBody);

        // Reachable is the production caller of the private Walker.CollectReferencedNames closure.
        var reachable = CteHoister.Reachable([producer, middle, consumer], consumer);
        reachable.Select(d => d.Name).Should().Equal("producer", "middle", "consumer");
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    // ---------------------------------------------------------------------------------------------
    // #146 slice B: recursive typed CTE surface, callback lifecycle, owner scope and shape contract.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Recursive_ShouldReturnCteDescriptor_AndInvokeCallbackExactlyOnce()
    {
        using var ctx = new TestContext();
        var calls = 0;
        EntityBuilder<long>? selfBuilder = null;

        var cte = ctx.From<TypedCteEntity>().Select(x => x.Id).AsRecursiveCte("nums", self =>
        {
            calls++;
            selfBuilder = ctx.From(self);
            return ctx.From(self).Where(n => n < 3).Select(n => n + 1);
        });

        calls.Should().Be(1);
        selfBuilder.Should().NotBeNull();
        cte.Should().BeOfType<Cte<long>>();
        cte.Name.Should().Be("nums");
        cte.Definitions.Should().ContainSingle().Which.Name.Should().Be("nums");
        cte.Definitions[0].Recursive.Should().BeTrue();
    }

    [Fact]
    public void Recursive_ShouldRenderWithRecursiveAndUnionAll()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));

        var sql = SqlOf(ctx, ctx.From(cte).Select(n => n));

        sql.Should().Contain("with recursive");
        sql.Should().Contain("union all");
        sql.Should().Contain("from nums");

        // The scalar step read the self-reference as a bare parameter; it must render as the source's
        // one readable column and never be emitted twice by the lambda walk.
        sql.Should().Contain("(Id + 1)").And.NotContain("(Id + 1)Id");
    }

    [Fact]
    public void Recursive_SingleDeclarationWithoutNestedCtes_ShouldTakeFastPathAndRender()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));

        // Issue #200 L1: one declaration with no nested declaration is returned unchanged by the
        // single-item fast path...
        var definitions = cte.Definitions;
        CteHoister.Hoist(definitions).Should().BeSameAs(definitions);

        // ...and the recursive CTE still renders its WITH RECURSIVE / UNION ALL body.
        var sql = SqlOf(ctx, ctx.From(cte).Select(n => n));

        sql.Should().Contain("with recursive");
        sql.Should().Contain("union all");
        sql.Should().Contain("from nums");
    }

    [Fact]
    public void Recursive_MultipleSelfReferenceOccurrences_ShouldReferenceCteOutputNameForEachOccurrence()
    {
        using var ctx = new TestContext();

        // V12: the same self-reference is read at two positions of one step expression (the reference
        // value combined with itself). Standard SQL allows the recursive table to appear only once in
        // the step's FROM (a self-join of the recursive CTE is rejected by every engine), so multiple
        // occurrences means multiple member reads: both must render the CTE output alias `Id`.
        var cte = ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => new { x.Id })
            .AsRecursiveCte("nums", self => ctx.From(self)
                .Where(n => n.Id < 5)
                .Select(n => new { Id = n.Id + n.Id + 1 }));

        var sql = SqlOf(ctx, ctx.From(cte).Limit(20).Select(n => n.Id));

        sql.Should().Contain("with recursive");
        sql.Should().Contain("union all");
        sql.Should().Contain("from nums");
        // Each occurrence reads the declared output name, never an inlined anchor body/ordinal.
        sql.Should().Contain("Id + Id");
        sql.Should().NotContain("t1.1");
    }

    [Fact]
    public void Recursive_CapturedReferenceOutsideStep_ShouldThrowBeforeAnyCommand()
    {
        using var ctx = new TestContext();
        CteReference<long>? captured = null;

        _ = ctx.From<TypedCteEntity>().Select(x => x.Id).AsRecursiveCte("nums", self =>
        {
            captured = self;
            return ctx.From(self).Where(n => n < 3).Select(n => n + 1);
        });

        captured.Should().NotBeNull();
        var act = () => ctx.From(captured!);
        act.Should().Throw<InvalidOperationException>().WithMessage("*outside its defining step*");
    }

    [Fact]
    public void Recursive_ShapeMismatch_ShouldThrowBeforeAnyCommand()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>()
            .Select(x => new TypedCteMemberInit { Id = x.Id, Total = x.Total })
            .AsRecursiveCte("reordered", self =>
                ctx.From(self).Select(x => new TypedCteMemberInit { Total = x.Total, Id = x.Id }));

        var act = () => SqlOf(ctx, ctx.From(cte).Select(x => new { x.Id, x.Total }));
        act.Should().Throw<InvalidOperationException>().WithMessage("*reordered*");
    }

    [Fact]
    public void Recursive_ShouldNotEmitRecursionHint_WhenOmitted()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>().Select(x => x.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 3).Select(n => n + 1));

        cte.Definitions[0].MaxRecursion.Should().BeNull();

        var withHint = ctx.From<TypedCteEntity>().Select(x => x.Id)
            .AsRecursiveCte("nums2", self => ctx.From(self).Where(n => n < 3).Select(n => n + 1), 10);

        withHint.Definitions[0].MaxRecursion.Should().Be(10);
    }

    [Fact]
    public void Recursive_NullStep_ShouldThrowArgumentNullException()
    {
        using var ctx = new TestContext();
        var q = ctx.From<TypedCteEntity>().Select(x => x.Id);

        // AC2: a null callback is a caller error rejected by the argument guard before any owner/shape
        // work or command preparation. Both overloads share the same contract.
        ((Action)(() => q.AsRecursiveCte("nums", null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => q.AsRecursiveCte("nums", null!, 10))).Should().Throw<ArgumentNullException>();
    }

    // ---------------------------------------------------------------------------------------------
    // #146-B D5: callback lifecycle, owner scope, pre-DB rejection and shared-command hygiene.
    // Every rejection asserts the existing connection/command boundary was never opened or executed.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Recursive_Callback_ShouldRunOnceAcrossPreparationAndMultipleConsumers()
    {
        using var ctx = new TestContext();
        var calls = 0;

        var nums = ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id)
            .AsRecursiveCte("nums", self =>
            {
                calls++;
                return ctx.From(self).Where(n => n < 5).Select(n => n + 1);
            });

        calls.Should().Be(1);

        var first = SqlOf(ctx, ctx.From(nums).Select(n => n));
        var second = SqlOf(ctx, ctx.From(nums).Where(n => n > 1).Select(n => n));
        var third = SqlOf(ctx, ctx.From(nums).Select(n => n));

        first.Should().Contain("union all");
        second.Should().Contain("union all");
        third.Should().Contain("union all");
        calls.Should().Be(1, "preparation, generation and a second consumer must never re-invoke the step callback");
    }

    [Fact]
    public void Recursive_ShouldNotMutateSharedAnyCommand()
    {
        using var ctx = new TestContext();

        // Materialise the context-shared Any command (without executing: the fake boundary has no reader).
        var parent = ctx.From<TypedCteEntity>().ToCommand();
        var shared = EntityBuilderExtensions.GetAnyCommand(ctx, parent);
        ctx.AnyCommand!.Value.Should().BeSameAs(shared);
        shared.Cache.Should().BeTrue();
        var sharedFrom = shared.From;

        var nums = ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));
        SqlOf(ctx, ctx.From(nums).Where(n => n > 1).Select(n => n));

        ctx.AnyCommand!.Value.Should().BeSameAs(shared);
        shared.Cache.Should().BeTrue("preparing a recursive consumer must not flip the shared command's sticky flag");
        shared.From.Should().BeSameAs(sharedFrom);
    }

    [Fact]
    public void Recursive_ForeignReferenceSameName_ShouldThrowBeforeAnyCommand()
    {
        using var ctx = new TestContext();

        var act = () => ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id)
            .AsRecursiveCte("nums", outer =>
            {
                var innerAnchor = ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id);
                // Same declared name, different owner: name equality is not owner equality.
                var inner = innerAnchor.AsRecursiveCte("nums", _ =>
                    ctx.From(outer).Where(n => n < 3).Select(n => n + 1));
                return ctx.From(inner).Select(n => n);
            });

        act.Should().Throw<InvalidOperationException>().WithMessage("*in its step*different recursive definition*");
        ctx.DbConnectionsOpened.Should().Be(0);
        ctx.DbCommandsExecuted.Should().Be(0);
    }

    [Fact]
    public void Recursive_AnchorSelfAccess_ShouldThrowBeforeAnyCommand()
    {
        using var ctx = new TestContext();

        var act = () => ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id)
            .AsRecursiveCte("outer", outer =>
            {
                // The inner definition's anchor reads the outer self-reference; an anchor must never
                // carry one, even while the outer owner scope is still active.
                var innerAnchor = ctx.From(outer).Where(n => n < 3).Select(n => n + 1);
                var inner = innerAnchor.AsRecursiveCte("inner", _ =>
                    ctx.From<TypedCteEntity>().Where(x => x.Id == 1).Select(x => x.Id));
                return ctx.From(inner).Select(n => n);
            });

        act.Should().Throw<InvalidOperationException>().WithMessage("*in its anchor*different recursive definition*");
        ctx.DbConnectionsOpened.Should().Be(0);
        ctx.DbCommandsExecuted.Should().Be(0);
    }

    [Fact]
    public void Recursive_NullStepResult_ShouldThrowBeforeAnyCommand()
    {
        using var ctx = new TestContext();

        var act = () => ctx.From<TypedCteEntity>().Select(x => x.Id)
            .AsRecursiveCte("nullStep", _ => null!);

        act.Should().Throw<InvalidOperationException>().WithMessage("*nullStep*returned null*");
        ctx.DbConnectionsOpened.Should().Be(0);
        ctx.DbCommandsExecuted.Should().Be(0);
    }

    [Fact]
    public void Recursive_MemberSlotMismatch_ShouldNameCteBranchAndPosition()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>()
            .Select(x => new TypedCteMemberInit { Id = x.Id, Total = x.Total })
            .AsRecursiveCte("slot", self =>
                ctx.From(self).Select(x => new TypedCteMemberInit { Total = x.Total, Id = x.Id }));

        var act = () => SqlOf(ctx, ctx.From(cte).Select(x => new { x.Id, x.Total }));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'slot'*step position 0*self-reference member 'Total'*anchor names that column 'Id'*");
        ctx.DbConnectionsOpened.Should().Be(0);
        ctx.DbCommandsExecuted.Should().Be(0);
    }

    [Fact]
    public void Recursive_ShapeCountMismatch_ShouldNameCteBranchAndPosition()
    {
        using var ctx = new TestContext();

        var cte = ctx.From<TypedCteEntity>()
            .Select(x => new TypedCteMemberInit { Id = x.Id, Total = x.Total })
            .AsRecursiveCte("counted", self =>
                ctx.From(self).Select(x => new TypedCteMemberInit { Id = x.Id }));

        var act = () => SqlOf(ctx, ctx.From(cte).Select(x => new { x.Id, x.Total }));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'counted'*step position 0*anchor projects 2 column(s)*step projects 1*");
        ctx.DbConnectionsOpened.Should().Be(0);
        ctx.DbCommandsExecuted.Should().Be(0);
    }

    // The public AsRecursiveCte surface fixes TResult across anchor and step, so a per-leaf mismatch of
    // the declared CLR type or the bound provider type cannot be produced through it. The per-column
    // predicate is exercised directly over synthetic SelectExpression pairs instead, so a regression in
    // either comparison is caught. (Nullability is type-enforced by the preceding CLR equality check and
    // cannot be driven independently; it is recorded as a D8 coverage note.)
    [Fact]
    public void RecursiveColumn_DeclaredClrTypeMismatch_ShouldThrowWithPosition()
    {
        var anchor = new SelectExpression(typeof(int)) { PropertyName = "Id" };
        var step = new SelectExpression(typeof(long)) { PropertyName = "Id" };

        var act = () => QueryCommand.QueryPreparer.ValidateRecursiveColumn("clr", 2, anchor, step);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'clr'*step position 2*declared CLR type differs*");
    }

    [Fact]
    public void RecursiveColumn_BoundProviderTypeMismatch_ShouldThrowWithPosition()
    {
        var anchor = new SelectExpression(typeof(int)) { PropertyName = "Id", ProviderType = typeof(int) };
        var step = new SelectExpression(typeof(int)) { PropertyName = "Id", ProviderType = typeof(long) };

        var act = () => QueryCommand.QueryPreparer.ValidateRecursiveColumn("provider", 1, anchor, step);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'provider'*step position 1*bound provider type differs*");
    }

    // r2.2 branch closure: `anchor.PropertyName ?? anchor.OutputName` (QueryPreparer.cs:1012). A
    // positional/computed anchor column has no PropertyName, so the self-reference-slot diagnostic must
    // fall back to the internal OutputName instead of reporting a null name. The internal
    // ValidateRecursiveColumn seam drives the predicate directly, like the V10 tests above.
    [Fact]
    public void RecursiveColumn_OutputNameFallback_ShouldNameAnchorColumnInDiagnostic()
    {
        var anchor = new SelectExpression(typeof(int)) { OutputName = "Co" };
        var step = new SelectExpression(typeof(int))
        {
            PropertyName = "Id",
            Expression = System.Linq.Expressions.Expression.Property(
                System.Linq.Expressions.Expression.Parameter(typeof(TypedCteEntity), "x"),
                nameof(TypedCteEntity.Id)),
        };

        var act = () => QueryCommand.QueryPreparer.ValidateRecursiveColumn("outname", 0, anchor, step);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'outname'*step position 0*self-reference member 'Id'*anchor names that column 'Co'*");
    }

    // r2.2 branch closure: the provider-type diagnostic's anchor `<none>` arm
    // (QueryPreparer.cs:1028, `anchorProvider?.Name ?? "<none>"`). A column with neither a converter nor
    // a bound ProviderType reads as its CLR type, so the mismatch must render `<none>` for the anchor.
    [Fact]
    public void RecursiveColumn_AnchorProviderTypeMissing_ShouldReportNone()
    {
        var anchor = new SelectExpression(typeof(int)) { PropertyName = "Id" };
        var step = new SelectExpression(typeof(int)) { PropertyName = "Id", ProviderType = typeof(long) };

        var act = () => QueryCommand.QueryPreparer.ValidateRecursiveColumn("none-anchor", 3, anchor, step);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'none-anchor'*step position 3*bound provider type differs*anchor binds <none>*step binds Int64*");
    }

    // r2.2 branch closure: the symmetric step `<none>` arm (QueryPreparer.cs:1028).
    [Fact]
    public void RecursiveColumn_StepProviderTypeMissing_ShouldReportNone()
    {
        var anchor = new SelectExpression(typeof(int)) { PropertyName = "Id", ProviderType = typeof(int) };
        var step = new SelectExpression(typeof(int)) { PropertyName = "Id" };

        var act = () => QueryCommand.QueryPreparer.ValidateRecursiveColumn("none-step", 1, anchor, step);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'none-step'*step position 1*bound provider type differs*anchor binds Int32*step binds <none>*");
    }

    // r2.2 branch closure: `anchor.Converter?.ProviderType ?? anchor.ProviderType` and its step twin
    // (QueryPreparer.cs:1024-1025). When a converter is bound, the provider type comes from the
    // converter, not the column's ProviderType. StatusToStringConverter is a ValueConverter<,> whose
    // ProviderType is string; the two acts cover the anchor-side and step-side converter arms.
    [Fact]
    public void RecursiveColumn_ConverterProviderType_ShouldBeComparedInsteadOfColumnProviderType()
    {
        var anchorConverted = new SelectExpression(typeof(int))
            { PropertyName = "Id", ProviderType = typeof(int), Converter = new StatusToStringConverter() };
        var stepPlain = new SelectExpression(typeof(int)) { PropertyName = "Id", ProviderType = typeof(int) };

        var anchorAct = () => QueryCommand.QueryPreparer.ValidateRecursiveColumn("conv-anchor", 0, anchorConverted, stepPlain);
        anchorAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*anchor binds String*step binds Int32*");

        var anchorPlain = new SelectExpression(typeof(int)) { PropertyName = "Id", ProviderType = typeof(int) };
        var stepConverted = new SelectExpression(typeof(int))
            { PropertyName = "Id", ProviderType = typeof(int), Converter = new StatusToStringConverter() };

        var stepAct = () => QueryCommand.QueryPreparer.ValidateRecursiveColumn("conv-step", 0, anchorPlain, stepConverted);
        stepAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*anchor binds Int32*step binds String*");
    }
}

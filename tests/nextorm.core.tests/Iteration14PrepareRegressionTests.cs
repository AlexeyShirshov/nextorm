using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Iteration 14 proposal 3 (prepare-path fast paths): the relationship-free-root shortcut in
/// <see cref="NavigationExpansion"/> and the leaf-command shortcut around
/// <see cref="CteHoister.EnsureNoUnhoistedCtes"/> must be pure overhead reductions. Generation is
/// unchanged for a relationship-free root and a plain leaf SELECT, a relationship-bearing subquery is
/// still expanded, a hoisted subgraph declaration is still accepted and an unhoisted declaration below
/// a <c>SubQuery</c>/<c>ColumnShape</c> edge still fails fast.
/// <para>
/// Runs in the "Query cache controls" collection (serialized) because it clears the process-wide
/// <see cref="DataContextCache"/> to drive the expansion pass through its metadata-absent fallback.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class Iteration14PrepareRegressionTests
{
    public Iteration14PrepareRegressionTests() => DataContextCache.Clear();

    [SqlTable("d14_parent")]
    public sealed class NavParent
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public ICollection<NavChild> Children { get; set; } = new List<NavChild>();
    }

    [SqlTable("d14_child")]
    public sealed class NavChild
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public NavParent? Parent { get; set; }
    }

    // Two mappings over the same physical table: PlainRoot has no relationship (fast path), RelRoot
    // declares one (original metadata-resolving path). The relationship is never accessed, so the two
    // query shapes must render byte-for-byte identical SQL.
    [SqlTable("d14_root")]
    public sealed class PlainRoot
    {
        public int Id { get; set; }
    }

    [SqlTable("d14_root")]
    public sealed class RelRoot
    {
        public int Id { get; set; }

        public int ChildId { get; set; }

        public NavChild? Child { get; set; }
    }

    [Fact]
    public void RelationshipFreeRoot_PreservesSql()
    {
        using var ctx = new PrepareD3TestContext();
        ctx.From<NavChild>();

        var fastSql = SqlOf(ctx, ctx.From<PlainRoot>().Where(x => x.Id > 0).Select(x => new { x.Id }));
        var referenceSql = SqlOf(ctx, ctx.From<RelRoot>(b => b.HasOne(x => x.Child, x => x.ChildId))
            .Where(x => x.Id > 0)
            .Select(x => new { x.Id }));

        referenceSql.Should().Be(fastSql, "the relationship-free fast path must not change the rendered SQL");
        fastSql.Should().Contain("select");
        fastSql.Should().Contain("from");
    }

    [Fact]
    public void RelationshipFreeRoot_WithNavigatingSubquery_StillExpands()
    {
        using var ctx = new PrepareD3TestContext();
        ctx.From<NavParent>();
        ctx.From<NavChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));

        // The outer command's source type carries no relationship, but the derived-table subquery is a
        // command over a relationship-bearing entity: its own preparation must still inject the LEFT JOIN.
        var inner = ctx.From<NavChild>().Select(c => new { c.Id, Name = c.Parent!.Name });

        var sql = SqlOf(ctx, ctx.From(inner).Select(x => x.Id));

        sql.Should().Contain("left join d14_parent");
    }

    [Fact]
    public void LeafWithoutCte_PreservesPreparation()
    {
        using var ctx = new PrepareD3TestContext();

        var first = SqlOf(ctx, ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }));
        var second = SqlOf(ctx, ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }));

        second.Should().Be(first, "a leaf command still prepares deterministically");
        first.Should().Contain("select");
        first.Should().Contain("from");
    }

    [Fact]
    public void RootWithoutCte_SubgraphCte_IsValidated()
    {
        using var ctx = new PrepareD3TestContext();

        var body = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var sub = ctx.With("shared_subgraph", body).From("shared_subgraph").Select(t => new { id = t["id"].AsInt });

        // The declaration lives on the subgraph command; the root carries the same (hoisted) definition,
        // so the diagnostic must run for the SubQuery edge and accept it.
        var root = ctx.From(sub).Select(x => x.id);
        root.Ctes = sub.Ctes;

        var sql = SqlOf(ctx, root);

        sql.Should().Contain("shared_subgraph");
    }

    [Fact]
    public void RootWithoutCte_UnhoistedSubgraphCte_Throws()
    {
        using var ctx = new PrepareD3TestContext();

        var body = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var sub = ctx.With("shared_subgraph", body).From("shared_subgraph").Select(t => new { id = t["id"].AsInt });

        // The root carries no own declaration and the declaration lives only on the subgraph command
        // exposed through From.SubQuery. The unhoisted-declaration diagnostic must run for that edge and
        // reject it: this test fails if the leaf fast path wrongly skips EnsureNoUnhoistedCtes.
        var root = ctx.From(sub).Select(x => x.id);

        root.Ctes.Should().BeNullOrEmpty("the throw must come from the SubQuery edge, not the root's own list");

        Action act = () => SqlOf(ctx, root);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be hoisted*");
    }

    [Fact]
    public void NullableNavigationCountProjection_PreservesSql()
    {
        using var ctx = new PrepareD3TestContext();
        ctx.From<NavParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<NavChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));

        var nullable = SqlOf(ctx, ctx.From<NavParent>().Select(p => new { p.Id, C = (int?)p.Children.Count() }));
        var nonNullable = SqlOf(ctx, ctx.From<NavParent>().Select(p => new { p.Id, C = p.Children.Count() }));

        nullable.Should().Contain("count", "a nullable navigation-count projection must still render the aggregate");
        nullable.Should().Be(nonNullable,
            "lifting a navigation Count() to a nullable result must not change the rendered SQL");
    }

    [Fact]
    public void UnsupportedDialect_DataModifyingCte_Throws()
    {
        using var ctx = new PrepareD3TestContext();
        ctx.From<SimpleEntity>();

        var insert = ctx.CreateInsertBuilder<SimpleEntity>().Value(x => x.Id, 1).Returning(x => new { x.Id });

        Action act = () => ctx.With("ins", insert);

        act.Should().Throw<NotSupportedException>().WithMessage("*only supported by PostgreSQL*");
    }

    [Fact]
    public void UnhoistedSubQueryCte_StillThrows()
    {
        using var ctx = new PrepareD3TestContext();

        var inner = ctx.With("i", ctx.From<SimpleEntity>().Select(x => new { x.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });
        var root = ctx.From(inner).Select(x => x.id);

        Action act = () => SqlOf(ctx, root);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be hoisted*");
    }

    [Fact]
    public void UnhoistedColumnShapeCte_StillThrows()
    {
        using var ctx = new PrepareD3TestContext();

        var body = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var shape = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        shape.Ctes = [new CteDefinition("inner_d3", body)];

        var root = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        root.From = new FromExpression("shape_source", shape);

        Action act = () => SqlOf(ctx, root);

        act.Should().Throw<InvalidOperationException>().WithMessage("*inner_d3*");
    }

    // ------------------------------------------------------------------------------------------------
    // Provider-free SQL renderer, mirroring the minimal fake context used by TypedCteTests.
    // ------------------------------------------------------------------------------------------------

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        return Normalize(prepared.DbCommand.CommandText);
    }

    private sealed class PrepareD3Dialect : SqlDialectBase
    {
        internal static readonly PrepareD3Dialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class PrepareD3TestContext : DataContext
    {
        private readonly FakeConnection _connection = new();

        public PrepareD3TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => PrepareD3Dialect.Instance;

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

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Context-safety of the #126 flat-row-constructor gate: the permission that allows a row constructor as
/// a direct comparison operand is scoped visitor state. A rejected preparation must not poison a later
/// valid query, repeated preparation must be stable, the plan-cache flag on a <see cref="QueryCommand"/>
/// must stay untouched, and cached vs call-local preparation must classify identically.
/// </summary>
public class TupleContextSafetyTests
{
    [SqlTable("tuple_safety_entity")]
    private sealed class TupleSafetyEntity
    {
        [Key]
        public long Id { get; set; }

        public string? Name { get; set; }
    }

    private sealed class FlatTupleDialect : SqlDialectBase
    {
        internal static readonly FlatTupleDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }

        // Flat ANSI row constructor, no server-side element access: the MySQL/SQLite/SQL Server shape.
        public override ITupleRenderer? Tuple => FlatTupleRenderer.Instance;
    }

    private sealed class FlatTupleRenderer : ITupleRenderer
    {
        internal static readonly FlatTupleRenderer Instance = new();

        public string RenderConstructor(IReadOnlyList<string> fields) => "(" + string.Join(", ", fields) + ")";

        public string? RenderElement(string row, int oneBasedIndex) => null;
    }

    private sealed class FlatTupleTestContext : DataContext
    {
        private readonly FakeConnection _connection = new();

        public FlatTupleTestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => FlatTupleDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => new FakeParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString) => _connection;
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void Tuple_RejectedPreparation_ShouldNotPoisonFollowingValidQuery()
    {
        using var ctx = new FlatTupleTestContext();

        var rejected = ctx.From<TupleSafetyEntity>().Select(x => Tuple.Create(x.Id, x.Name));
        var act = () => SqlOf(ctx, rejected);
        act.Should().Throw<NotSupportedException>();

        // The scoped flag must be restored in a finally, so this valid comparison still renders.
        var sql = SqlOf(ctx, ctx.From<TupleSafetyEntity>()
            .Where(x => Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a"))
            .Select(x => new { x.Id }));

        sql.Should().Contain("(Id, Name) = (1, 'a')");
    }

    [Fact]
    public void Tuple_RepeatedRejectedPreparation_ShouldThrowEveryTimeAndKeepValidSqlStable()
    {
        using var ctx = new FlatTupleTestContext();

        var rejected = ctx.From<TupleSafetyEntity>().Select(x => Tuple.Create(x.Id, x.Name));
        for (var i = 0; i < 2; i++)
        {
            var act = () => SqlOf(ctx, rejected);
            act.Should().Throw<NotSupportedException>();
        }

        string Valid() => SqlOf(ctx, ctx.From<TupleSafetyEntity>()
            .Where(x => Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a"))
            .Select(x => new { x.Id }));

        Valid().Should().Be(Valid());
    }

    [Fact]
    public void Tuple_Preparation_ShouldNotDisablePlanCacheOnTheCommand()
    {
        using var ctx = new FlatTupleTestContext();

        var valid = ctx.From<TupleSafetyEntity>()
            .Where(x => Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a"))
            .Select(x => new { x.Id });

        ctx.GetPreparedQueryCommand(valid, false, true, CancellationToken.None);
        valid.Cache.Should().BeTrue("the row-constructor gate must not touch the sticky plan-cache flag");

        var rejected = ctx.From<TupleSafetyEntity>().Select(x => Tuple.Create(x.Id, x.Name));
        var act = () => ctx.GetPreparedQueryCommand(rejected, false, true, CancellationToken.None);
        act.Should().Throw<NotSupportedException>();
        rejected.Cache.Should().BeTrue("a rejected preparation must not leave the command uncacheable");
    }

    [Fact]
    public void Tuple_CachedAndCallLocalPreparation_ShouldRenderIdenticalSql()
    {
        using var ctx = new FlatTupleTestContext();

        string Prepare(bool storeInCache)
        {
            var cmd = ctx.From<TupleSafetyEntity>()
                .Where(x => Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a"))
                .Select(x => new IdShape { Id = x.Id });
            return ((DbPreparedQueryCommand<IdShape>)ctx.GetPreparedQueryCommand(cmd, false, storeInCache, CancellationToken.None))
                .DbCommand.CommandText;
        }

        Prepare(storeInCache: true).Should().Be(Prepare(storeInCache: false));
    }

    [Fact]
    public void Tuple_CapturedTupleOperandInComparison_ShouldThrowAtPreparation()
    {
        using var ctx = new FlatTupleTestContext();
        var local = Tuple.Create(1L, "a");

        var act = () => SqlOf(ctx, ctx.From<TupleSafetyEntity>()
            .Where(x => Tuple.Create(x.Id, x.Name) == local)
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().Which.Message
            .Should().Contain("FlatTupleDialect").And.Contain("inline row constructor");
    }

    [Fact]
    public void Tuple_NullOperandInComparison_ShouldThrowWithTheRealReason()
    {
        using var ctx = new FlatTupleTestContext();

        var act = () => SqlOf(ctx, ctx.From<TupleSafetyEntity>()
            .Where(x => Tuple.Create(x.Id, x.Name) == null)
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().Which.Message
            .Should().Contain("FlatTupleDialect").And.Contain("null");
    }

    [Fact]
    public void Tuple_RowComparisonInNonPredicatePosition_ShouldThrowAtPreparation()
    {
        using var ctx = new FlatTupleTestContext();

        var select = () => SqlOf(ctx, ctx.From<TupleSafetyEntity>()
            .Select(x => Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a")));
        select.Should().Throw<NotSupportedException>().Which.Message.Should().Contain("FlatTupleDialect");

        var orderBy = () => SqlOf(ctx, ctx.From<TupleSafetyEntity>()
            .OrderBy(x => Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a"))
            .Select(x => new { x.Id }));
        orderBy.Should().Throw<NotSupportedException>().Which.Message.Should().Contain("FlatTupleDialect");
    }

    [Fact]
    public void Tuple_ConvertWrappedConstructorOperand_ShouldRenderFlatRowComparison()
    {
        using var ctx = new FlatTupleTestContext();

        var sql = SqlOf(ctx, ctx.From<TupleSafetyEntity>()
            .Where(x => (object)Tuple.Create(x.Id, x.Name) == (object)Tuple.Create(1L, "a"))
            .Select(x => new { x.Id }));

        sql.Should().Contain("(Id, Name) = (1, 'a')");
    }

    [Fact]
    public void Tuple_ScopedFlag_ShouldBeRestoredAfterAComparisonOperand()
    {
        var options = new VisitorOptions(
            typeof(object), FlatTupleDialect.Instance, null!, 0, null, null!, null!, false, false, [], null);
        using var visitor = new WhereExpressionVisitor(options);

        var operand = ((Expression<Func<Tuple<long, string>>>)(() => Tuple.Create(1L, "a"))).Body;
        var subsequent = ((Expression<Func<Tuple<long, string>>>)(() => Tuple.Create(2L, "b"))).Body;

        visitor.VisitComparisonOperand(operand, null);
        visitor.IsDirectTupleComparisonOperand.Should().BeFalse(
            "the flat-row permission is scoped to one comparison operand" + " and restored in a finally");

        var act = () => visitor.Visit(subsequent);
        act.Should().Throw<NotSupportedException>(
            "a later non-comparison tuple position must not inherit the restored permission");
    }

    private sealed class IdShape
    {
        public long Id { get; set; }
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

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T304 (#304) A7/R10: the correlated reference-hop repair must be preparation-local. It registers the
/// joined navigation parameter as an outer reference of the *nested* command and must never mutate the
/// outer command - in particular not the context-shared <c>QueryCommand</c> that <c>Any()</c> reuses
/// (<see cref="QueryCache.AnyCommand"/>), whose sticky <c>Cache = false</c> flag would silently disable
/// the plan cache for the whole context.
///
/// <para>
/// A provider-free <see cref="DataContext"/> renders real SQL through the real planner, so the
/// visitor path under test runs without a database. It runs in the serialized "Query cache controls"
/// collection because it asserts plan-cache state.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class T304QueryCommandIsolationTests
{
    [Fact]
    public void SharedAnyCountAll_CommandStateRemainsUnchanged()
    {
        using var ctx = new ProviderFreeContext();
        RegisterNavigation(ctx);

        // The terminal family around the shared command: Any() reuses the context-shared QueryCommand
        // (GetAnyCommand), Count() prepares its own count() projection and the SQL ALL predicate its own
        // command. All three are prepared here, without executing, so the assertion is about state.
        var shared = EntityBuilderExtensions.GetAnyCommand(
            ctx, ctx.From<T304Parent>().Where(p => p.Id > 0).ToCommand());
        ctx.AnyCommand.Should().NotBeNull("the Any route must allocate the context-shared command");
        ctx.AnyCommand!.Value.Should().BeSameAs(shared);
        Prepare(ctx, shared, storeInCache: false);
        shared.Cache.Should().BeTrue("the shared Any command is cache-enabled by default");

        var sharedFrom = shared.From;
        var sharedOuterReferences = shared.OuterReferences;
        var sharedReferencedQueries = shared.ReferencedQueries;

        Prepare(ctx,
            ctx.From<T304Parent>().Where(p => p.Id > 0).SelectParent(e => SqlFunctions.Sql.count()),
            storeInCache: false);
        var childIds = ctx.From<T304Child>().Select(c => c.Id);
        Prepare(ctx,
            ctx.From<T304Parent>()
                .Where(p => p.Id == SqlFunctions.Sql.all(childIds))
                .ToCommand(),
            storeInCache: false);

        // The unit under test: every planned form of the correlated reference hop, hop 1 and hop 2+,
        // comparison/presence/projection, plus the nullable-foreign-key variant.
        Prepare(ctx, CorrelatedExistsCommand(ctx, hops: 1), storeInCache: false);
        Prepare(ctx, CorrelatedExistsCommand(ctx, hops: 2), storeInCache: false);
        Prepare(ctx, CorrelatedPresenceCommand(ctx), storeInCache: false);
        Prepare(ctx, CorrelatedProjectionCommand(ctx), storeInCache: false);
        Prepare(ctx, NullableReferenceExistsCommand(ctx), storeInCache: false);

        // The shared command is the same object with the same observable state: no sticky flag flip, no
        // re-parented FROM and no outer reference added to the outer command.
        ctx.AnyCommand!.Value.Should().BeSameAs(shared);
        shared.Cache.Should().BeTrue(
            "preparing correlated reference hops must not flip the shared command's sticky cache flag");
        shared.From.Should().BeSameAs(sharedFrom);
        shared.OuterReferences.Should().BeSameAs(sharedOuterReferences,
            "the joined parameter belongs to the nested command, not to the shared outer command");
        shared.ReferencedQueries.Should().BeSameAs(sharedReferencedQueries);
    }

    [Fact]
    public void ParameterRefresh_PreservesPlanIsolation()
    {
        using var ctx = new ProviderFreeContext();
        RegisterNavigation(ctx);

        // Two identically shaped commands built by two distinct closures: the same captured name and the
        // same CLR type, hence the same parameter name - the identity the repair must not merge.
        var first = Prepare(ctx, ReferenceHopCommand(ctx, "alpha"), storeInCache: false);
        var second = Prepare(ctx, ReferenceHopCommand(ctx, "beta"), storeInCache: false);

        first.Should().NotBeSameAs(second, "each uncached preparation owns its plan");
        SingleParameter(first).Should().Be("alpha");
        SingleParameter(second).Should().Be("beta");
        SingleParameter(first).Should().Be("alpha",
            "preparing the sibling plan must not refresh this plan's parameter");

        // The cached path rebinds the fresh closure value on the hit. The isolated pair above stays
        // untouched by it - a refresh is scoped to the command being prepared.
        var cached = Prepare(ctx, ReferenceHopCommand(ctx, "gamma"), storeInCache: true);
        var refreshed = Prepare(ctx, ReferenceHopCommand(ctx, "delta"), storeInCache: true);

        SingleParameter(cached).Should().Be("delta",
            "a cache hit refreshes the parameter from the fresh closure of the command being prepared");
        SingleParameter(refreshed).Should().Be("delta");
        SingleParameter(first).Should().Be("alpha", "the isolated pair keeps its own bound values");
        SingleParameter(second).Should().Be("beta");
    }

    private static void RegisterNavigation(IDataContext ctx)
    {
        ctx.From<T304Root>();
        ctx.From<T304Group>(b => b.HasOne(g => g.Root, g => g.RootId));
        ctx.From<T304Parent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        ctx.From<T304Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<T304NullableParent>(b => b.HasOne(p => p.Group, p => p.GroupId));
    }

    private static QueryCommand<int> ReferenceHopCommand(IDataContext ctx, string value)
    {
        var inner = ctx.From<T304Child>();
        return ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code && p.Group.Code == value)))
            .Select(p => p.Id);
    }

    private static QueryCommand<int> CorrelatedExistsCommand(IDataContext ctx, int hops)
    {
        var inner = ctx.From<T304Child>();
        if (hops == 1)
        {
            return ctx.From<T304Parent>()
                .Where(p => SqlFunctions.Sql.exists(inner.Where(
                    c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
                .Select(p => p.Id);
        }

        return ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id);
    }

    private static QueryCommand<int> CorrelatedPresenceCommand(IDataContext ctx)
    {
        var inner = ctx.From<T304Child>();
        return ctx.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group != null)))
            .Select(p => p.Id);
    }

    private static QueryCommand<string?> CorrelatedProjectionCommand(IDataContext ctx)
        => ctx.From<T304Parent>()
            .Select(p => ctx.From<T304Child>()
                .Where(c => c.ParentId == p.Id)
                .Select(c => p.Group!.Code)
                .FirstOrDefault());

    private static QueryCommand<int> NullableReferenceExistsCommand(IDataContext ctx)
    {
        var inner = ctx.From<T304Child>();
        return ctx.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .Select(p => p.Id);
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static object? SingleParameter<T>(DbPreparedQueryCommand<T> prepared)
    {
        var parameters = prepared.DbCommandParams.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle();
        return parameters[0].Value;
    }

    [SqlTable("t304_root")]
    public sealed class T304Root
    {
        public int Id { get; set; }

        public string? Label { get; set; }
    }

    [SqlTable("t304_group")]
    public sealed class T304Group
    {
        public int Id { get; set; }

        public string? Code { get; set; }

        public int RootId { get; set; }

        public T304Root? Root { get; set; }
    }

    [SqlTable("t304_parent")]
    public sealed class T304Parent
    {
        public int Id { get; set; }

        public int GroupId { get; set; }

        public T304Group? Group { get; set; }
    }

    [SqlTable("t304_nullable_parent")]
    public sealed class T304NullableParent
    {
        public int Id { get; set; }

        public int? GroupId { get; set; }

        public T304Group? Group { get; set; }
    }

    [SqlTable("t304_child")]
    public sealed class T304Child
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public string? GroupCode { get; set; }

        public T304Parent? Parent { get; set; }
    }

    // ------------------------------------------------------------------------------------------------
    // Provider-free SQL renderer (the same minimal fake boundary other core preparation tests use).
    // ------------------------------------------------------------------------------------------------

    private sealed class ProviderFreeDialect : SqlDialectBase
    {
        internal static readonly ProviderFreeDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class ProviderFreeContext : DataContext
    {
        private readonly FakeConnection _connection = new();

        public ProviderFreeContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => ProviderFreeDialect.Instance;

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

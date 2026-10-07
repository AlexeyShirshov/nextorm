using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #147: pins the explicit <c>Create…Builder</c> factory surface. The old implicit DML factory names
/// (<c>InsertInto</c>, …) must be gone from <see cref="DataContextExtensions"/>, the executing
/// <c>Update</c>/<c>UpdateAsync</c> pair must be intact, and every additive <c>CreateQueryBuilder*</c>
/// forwarder must delegate to the unchanged <c>From*</c> original with the same shape and SQL.
/// <para>
/// Runs in the "Query cache controls" collection (serialized, parallelization disabled) because the
/// callback-once cases clear the process-wide <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class CreateCommandBuilderFactoryTests
{
    [SqlTable("cqb_entity")]
    private sealed class CqbEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [SqlTable("cqb_callback")]
    private sealed class CallbackEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [SqlTable("cqb_callback_options")]
    private sealed class CallbackOptionsEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private interface ITvfRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        long Id { get; set; }
    }

    private static class Tvf
    {
        [SqlTableFunction("cqb_rows")]
        public static IQueryable<ITvfRow> AllRows() => throw new NotSupportedException();
    }

    /// <summary>Provider-free dialect: raw SQL FROM sources are enabled so the <c>FromSql</c> forms render.</summary>
    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override bool SupportsRawSqlSource => true;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        // The query planner mints parameters while building the command; only the SQL text is asserted.
        public override DbParameter CreateParam(string name, object? value) => new FakeParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString) => new FakeConnection();
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

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        return Normalize(prepared.DbCommand.CommandText);
    }

    /// <summary>Compile-time proof that the forwarder and the original return the same builder type.</summary>
    private static void AssertSameBuilderType<T>(EntityBuilder<T> viaForwarder, EntityBuilder<T> viaOriginal)
    {
        viaForwarder.Should().NotBeNull();
        viaOriginal.Should().NotBeNull();
    }

    // ---------------------------------------------------------------- surface contract ------------

    [Fact]
    public void OldDmlFactoryNames_ShouldBeAbsent()
    {
        var methods = typeof(DataContextExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);

        foreach (var old in new[] { "InsertInto", "BulkInsertInto", "DeleteFrom", "MergeInto", "Truncate", "UpdateJoin", "Batch" })
            methods.Should().NotContain(m => m.Name == old, $"the DML factory '{old}' was renamed to a Create…Builder form");
    }

    [Fact]
    public void NewDmlFactoryNames_ShouldBePresent()
    {
        var names = typeof(DataContextExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => m.Name)
            .ToHashSet();

        names.Should().Contain(new[]
        {
            "CreateInsertBuilder", "CreateBulkInsertBuilder", "CreateDeleteBuilder",
            "CreateUpdateBuilder", "CreateMergeBuilder", "CreateTruncateBuilder", "CreateUpdateJoinBuilder",
            "CreateSqliteFts5CommandBuilder",
        });
    }

    [Fact]
    public void BatchFactory_ShouldLiveOnBatchExtensions_AndNotRenderABatchDmlFactory()
    {
        typeof(BatchExtensions).GetMethod("CreateBatchBuilder", BindingFlags.Public | BindingFlags.Static)
            .Should().NotBeNull();

        typeof(BatchExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Should().NotContain(m => m.Name == "Batch");
    }

    [Fact]
    public void ExecutingUpdatePair_ShouldRemain_AndTheBuilderOverload_ShouldBeGone()
    {
        var updates = typeof(DataContextExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "Update")
            .ToArray();

        updates.Should().ContainSingle("only the executing Update(IDataContext, TEntity) overload remains");
        var parameters = updates[0].GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(IDataContext));
        parameters[1].ParameterType.Should().NotBe(typeof(Action<>));
        updates[0].ReturnType.Should().Be(typeof(int));

        var asyncUpdates = typeof(DataContextExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "UpdateAsync")
            .ToArray();
        asyncUpdates.Should().ContainSingle();
        asyncUpdates[0].ReturnType.Should().Be(typeof(Task<int>));
    }

    [Fact]
    public void NewQueryForwarders_ShouldExistWithTheirExpectedParameterCounts()
    {
        var methods = typeof(DataContextExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);

        int Count(string name, int parameterCount)
            => methods.Count(m => m.Name == name && m.GetParameters().Length == parameterCount);

        // CreateQueryBuilder: 9 overloads — five 2-arg and four 3-arg.
        Count("CreateQueryBuilder", 2).Should().Be(5);
        Count("CreateQueryBuilder", 3).Should().Be(4);
        Count("CreateQueryBuilderFromSql", 3).Should().Be(1);
        Count("CreateQueryBuilderFromTableFunction", 2).Should().Be(1);

        var totalForwarders = methods.Count(m => m.Name is "CreateQueryBuilder" or "CreateQueryBuilderFromSql" or "CreateQueryBuilderFromTableFunction");
        totalForwarders.Should().Be(11);
    }

    [Fact]
    public void DataContextCompositeInterface_ShouldBeUnchanged()
    {
        typeof(IDataContext).GetInterfaces().Should().BeEquivalentTo(new[]
        {
            typeof(IQueryExecutor),
            typeof(IQueryMaterializer),
            typeof(IQueryPlanner),
            typeof(IRowReaderFactory),
            typeof(IQueryCache),
            typeof(IContextEnvironment),
            typeof(IRawCommandExecutor),
            typeof(IAsyncDisposable),
            typeof(IDisposable),
        });

        // The factories are extension methods: none of them is a member of the composite interface.
        typeof(IDataContext).GetMethods().Should().NotContain(m => m.Name.StartsWith("CreateQueryBuilder", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- forwarder parity -------------

    [Fact]
    public void CreateQueryBuilder_ConfigOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilder<CqbEntity>(_ => { });
        var viaOriginal = ctx.From<CqbEntity>(_ => { });
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(x => new { x.Id }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(x => new { x.Id })));
    }

    [Fact]
    public void CreateQueryBuilder_OptionsConfigOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        Action<FromOptions> options = _ => { };

        var viaForwarder = ctx.CreateQueryBuilder<CqbEntity>(options, _ => { });
        var viaOriginal = ctx.From<CqbEntity>(options, _ => { });
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(x => new { x.Id }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(x => new { x.Id })));
    }

    [Fact]
    public void CreateQueryBuilder_TableNameOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilder("cqb_raw");
        var viaOriginal = ctx.From("cqb_raw");
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(t => new { Id = t["id"].AsInt }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(t => new { Id = t["id"].AsInt })));
    }

    [Fact]
    public void CreateQueryBuilder_TableNameOptionsOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilder("cqb_raw", _ => { });
        var viaOriginal = ctx.From("cqb_raw", _ => { });
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(t => new { Id = t["id"].AsInt }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(t => new { Id = t["id"].AsInt })));
    }

    [Fact]
    public void CreateQueryBuilder_QueryCommandOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        var inner = ctx.CreateQueryBuilder<CqbEntity>().Select(x => new { x.Id });

        var viaForwarder = ctx.CreateQueryBuilder(inner);
        var viaOriginal = ctx.From(inner);
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(x => new { x.Id }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(x => new { x.Id })));
    }

    [Fact]
    public void CreateQueryBuilder_QueryCommandOptionsOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        Action<FromOptions> options = _ => { };
        var inner = ctx.CreateQueryBuilder<CqbEntity>().Select(x => new { x.Id });

        var viaForwarder = ctx.CreateQueryBuilder(inner, options);
        var viaOriginal = ctx.From(inner, options);
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(x => new { x.Id }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(x => new { x.Id })));
    }

    [Fact]
    public void CreateQueryBuilder_EntityBuilderOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        var innerForwarder = ctx.CreateQueryBuilder<CqbEntity>();
        var innerOriginal = ctx.CreateQueryBuilder<CqbEntity>();

        var viaForwarder = ctx.CreateQueryBuilder(innerForwarder);
        var viaOriginal = ctx.From(innerOriginal);
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(x => new { x.Id }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(x => new { x.Id })));
    }

    [Fact]
    public void CreateQueryBuilder_EntityBuilderOptionsOverload_ShouldMatchFrom()
    {
        using IDataContext ctx = new TestContext();
        Action<FromOptions> options = _ => { };
        var innerForwarder = ctx.CreateQueryBuilder<CqbEntity>();
        var innerOriginal = ctx.CreateQueryBuilder<CqbEntity>();

        var viaForwarder = ctx.CreateQueryBuilder(innerForwarder, options);
        var viaOriginal = ctx.From(innerOriginal, options);
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(x => new { x.Id }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(x => new { x.Id })));
    }

    [Fact]
    public void CreateQueryBuilder_TempTableOverload_ShouldRejectTheSameWayAsFrom()
    {
        // The in-memory context is not an IBatchExecutor, so the lazy temporary-table source is rejected
        // by From(tempTable) before any SQL is emitted; the forwarder must reject identically.
        using IDataContext ctx = new InMemoryDataContext();
        var tempTable = ctx.CreateQueryBuilder<CqbEntity>().Select(x => new { x.Id }).AsTempTable();

        var viaForwarder = () => ctx.CreateQueryBuilder(tempTable);
        var viaOriginal = () => ctx.From(tempTable);

        viaForwarder.Should().Throw<NotSupportedException>().WithMessage("*cannot materialise a temporary table*");
        viaOriginal.Should().Throw<NotSupportedException>().WithMessage("*cannot materialise a temporary table*");
    }

    // ---------------------------------------------------------------- callback once ----------------

    [Fact]
    public void CreateQueryBuilder_ConfigCallback_ShouldRunExactlyOnce_AndRegisterTheSameMetadataAsFrom()
    {
        DataContextCache.Clear();
        using IDataContext ctx = new TestContext();
        var calls = 0;

        ctx.CreateQueryBuilder<CallbackEntity>(cfg =>
        {
            calls++;
            cfg.Property(x => x.Name!).HasColumnName("full_name");
        });

        calls.Should().Be(1);
        var viaForwarder = DataContextCache.Metadata[typeof(CallbackEntity)].Properties
            .Single(p => p.PropertyInfo.Name == nameof(CallbackEntity.Name)).ColumnName;
        viaForwarder.Should().Be("full_name");

        // The original registers the identical column mapping.
        DataContextCache.Clear();
        ctx.From<CallbackEntity>(cfg => cfg.Property(x => x.Name!).HasColumnName("full_name"));

        DataContextCache.Metadata[typeof(CallbackEntity)].Properties
            .Single(p => p.PropertyInfo.Name == nameof(CallbackEntity.Name)).ColumnName
            .Should().Be(viaForwarder);
    }

    [Fact]
    public void CreateQueryBuilder_OptionsConfigCallback_ShouldRunExactlyOnce()
    {
        DataContextCache.Clear();
        using IDataContext ctx = new TestContext();
        var calls = 0;
        Action<FromOptions> options = _ => { };

        ctx.CreateQueryBuilder<CallbackOptionsEntity>(options, cfg =>
        {
            calls++;
            cfg.Property(x => x.Name!).HasColumnName("opt_name");
        });

        calls.Should().Be(1);
        DataContextCache.Metadata[typeof(CallbackOptionsEntity)].Properties
            .Single(p => p.PropertyInfo.Name == nameof(CallbackOptionsEntity.Name)).ColumnName
            .Should().Be("opt_name");
    }

    // ---------------------------------------------------------------- FromSql params ----------------

    [Fact]
    public void CreateQueryBuilderFromSql_OmittedParameters_ShouldMatchFromSql()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilderFromSql("select 1 as Id");
        var viaOriginal = ctx.FromSql("select 1 as Id");

        SqlOf(ctx, viaForwarder.Select(t => new { Id = t["Id"].AsInt }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(t => new { Id = t["Id"].AsInt })));
    }

    [Fact]
    public void CreateQueryBuilderFromSql_ExplicitNullParameters_ShouldMatchFromSql()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilderFromSql("select 1 as Id", null);
        var viaOriginal = ctx.FromSql("select 1 as Id", null);

        SqlOf(ctx, viaForwarder.Select(t => new { Id = t["Id"].AsInt }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(t => new { Id = t["Id"].AsInt })));
    }

    [Fact]
    public void CreateQueryBuilderFromSql_EmptyParametersObject_ShouldMatchFromSql()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilderFromSql("select 1 as Id", new { });
        var viaOriginal = ctx.FromSql("select 1 as Id", new { });

        SqlOf(ctx, viaForwarder.Select(t => new { Id = t["Id"].AsInt }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(t => new { Id = t["Id"].AsInt })));
    }

    [Fact]
    public void CreateQueryBuilderFromSql_SingleNamedParameter_ShouldMatchFromSql()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilderFromSql("select @p as Id", new { p = 1 });
        var viaOriginal = ctx.FromSql("select @p as Id", new { p = 1 });

        SqlOf(ctx, viaForwarder.Select(t => new { Id = t["Id"].AsInt }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(t => new { Id = t["Id"].AsInt })));
    }

    [Fact]
    public void CreateQueryBuilderFromSql_CollidingParameterName_ShouldStillThrow()
    {
        using IDataContext ctx = new TestContext();
        var min = 1;

        var act = () => SqlOf(ctx, ctx
            .CreateQueryBuilderFromSql("select id from cqb_raw where id > @min", new { min })
            .Where(t => t["id"].AsInt > min)
            .Select(t => new { Id = t["id"].AsInt }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*two parameters named 'min'*");
    }

    // ---------------------------------------------------------------- table function ---------------

    [Fact]
    public void CreateQueryBuilderFromTableFunction_ShouldTranslateLikeFromTableFunction()
    {
        using IDataContext ctx = new TestContext();
        var viaForwarder = ctx.CreateQueryBuilderFromTableFunction(() => Tvf.AllRows());
        var viaOriginal = ctx.FromTableFunction(() => Tvf.AllRows());
        AssertSameBuilderType(viaForwarder, viaOriginal);

        SqlOf(ctx, viaForwarder.Select(r => new { r.Id }))
            .Should().Be(SqlOf(ctx, viaOriginal.Select(r => new { r.Id })));
        SqlOf(ctx, viaForwarder.Select(r => new { r.Id })).Should().Contain("cqb_rows()");
    }

    [Fact]
    public void CreateQueryBuilderFromTableFunction_InMemory_ShouldStillThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateQueryBuilderFromTableFunction(() => Tvf.AllRows())
            .Select(r => new { r.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    // ---------------------------------------------------------------- executing unchanged ----------

    [Fact]
    public void ExecutingUpdate_ShouldStillCompile_AndValidateLikeBefore()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.Update(new NoKeyEntity { Name = "a" });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecutingUpdateAsync_ShouldStillCompile_AndValidateLikeBefore()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.UpdateAsync(new NoKeyEntity { Name = "a" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ExecutingUpdate_OnMappedEntity_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();

        // A keyed entity passes the key validation and reaches the (read-only) in-memory execution,
        // proving the renamed surface did not accidentally turn Update into the builder factory.
        var act = () => ctx.Update(new CqbEntity { Id = 1, Name = "a" });

        act.Should().Throw<NotSupportedException>();
    }
}

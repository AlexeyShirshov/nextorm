using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Behavioural coverage for <c>ExecuteRaw</c>/<c>ExecuteRawAsync</c> against a real SQLite database:
/// scalar and name-mapped entity result sets, sequential result sets, parameter options, argument
/// validation, interception, transactions and reader release.
/// </summary>
/// <remarks>
/// Joins the "DataContextCache clear" collection because <see cref="RawMapping_DoesNotWriteSharedSelectListCache"/>
/// clears the process-wide <see cref="DataContextCache"/> and would otherwise race the plan-cache reuse
/// assertions in <c>InListCacheTests</c>.
/// </remarks>
[Collection("DataContextCache clear")]
public class RawCommandTests
{
    [SqlTable("raw_orders")]
    private sealed class RawOrder
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        // Deliberately absent from the raw result sets read by the tests.
        public string? Missing { get; set; }
    }

    // Never touched through LINQ: raw mapping must register its metadata on demand.
    [SqlTable("fresh_raw")]
    private sealed class RawFreshEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    // A parameterized constructor would make the materializer bind positionally; raw mapping rejects it.
    [SqlTable("raw_orders")]
    private sealed class RawCtorEntity
    {
        public RawCtorEntity(int id, string? name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; set; }
        public string? Name { get; set; }
    }

    // No writable properties: neither a scalar nor a mappable entity.
    private sealed class NotMappableRaw
    {
        public int Value => 1;
    }

    // Property X is mapped to column "y" and Y to "x": mapped column names must win over property names.
    [SqlTable("xy_raw")]
    private sealed class RawXyEntity
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("y")]
        public int X { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("x")]
        public int Y { get; set; }
    }

    private sealed class RecordingInterceptor : IQueryInterceptor
    {
        public List<string> Events { get; } = [];
        public DbCommand? LastCommand { get; private set; }

        public void CommandInitialized(CommandEventData eventData, DbCommand command)
        {
            Events.Add("initialized");
            LastCommand = command;
        }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            Events.Add("executing");
            LastCommand = command;
        }

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
        {
            Events.Add("executed");
            LastCommand = command;
        }

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
        {
            Events.Add("failed");
            LastCommand = command;
        }
    }

    private static (IDataContext Context, string Path) CreateDb(DataContextBuilder? builder = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-raw-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "create table simple_entity (id integer primary key);" +
                "create table raw_orders (id integer, name text);" +
                "create table fresh_raw (id integer, name text);" +
                "create table xy_raw (x integer, y integer);" +
                "create table reservation (id integer primary key, during_lower integer, during_upper integer);" +
                "insert into simple_entity (id) values (7);" +
                "insert into raw_orders (id, name) values (2, 'beta');" +
                "insert into raw_orders (id, name) values (1, 'alpha');" +
                "insert into fresh_raw (id, name) values (1, 'fresh');" +
                "insert into xy_raw (x, y) values (10, 20);" +
                "insert into reservation (id, during_lower, during_upper) values (1, 5, 9);";
            command.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", builder ?? new DataContextBuilder()), path);
    }

    // Direct tokenless expanded-params calls live in plain helpers: xUnit1051 rejects them inside a
    // test method even though the resolved overload has no CancellationToken, but the analyzers do
    // not scan this non-test method, so the expanded async syntax is still compiled and executed.
    private static Task<ProcedureResult> ExecuteRawExpandedAsync(DataContext ctx, string sql, ProcedureParameter parameter)
        => ctx.ExecuteRawAsync(sql, parameter);

    private static Task<ProcedureResult> ExecuteRawExpandedAsync(DataContext ctx, string sql, ProcedureParameter first, ProcedureParameter second)
        => ctx.ExecuteRawAsync(sql, first, second);

    private static Task<ProcedureResult> ExecuteRawExpandedAsync(IRawCommandExecutor ctx, string sql, ProcedureParameter parameter)
        => ctx.ExecuteRawAsync(sql, parameter);

    private static Task<ProcedureResult> ExecuteRawExpandedAsync(IRawCommandExecutor ctx, string sql, ProcedureParameter first, ProcedureParameter second)
        => ctx.ExecuteRawAsync(sql, first, second);

    [Fact]
    public void DdlWithNoResultSet_ReadThrows_AndDisposeIsIdempotent()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.ExecuteRaw("create table created_by_raw (id integer)");

            var act = () => result.Read<int>();

            act.Should().Throw<InvalidOperationException>();
            result.Dispose();
            result.Dispose();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ScalarRead_UsesParameterValue()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select @v as value", [new ProcedureParameter("v", 41)]);

            result.Read<int>().Should().Equal(41);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ScalarReadAsync_UsesParameterValue()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = await ctx.ExecuteRawAsync(
                "select @v as value",
                [new ProcedureParameter("v", "abc")],
                TestContext.Current.CancellationToken);

            var rows = new List<string>();
            await foreach (var row in result.ReadAsync<string>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal("abc");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void EntityRead_MapsByColumnName_InReaderOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            _ = ctx.From<RawOrder>();

            using var result = ctx.ExecuteRaw("select name, id from raw_orders order by id");

            var rows = result.Read<RawOrder>();

            rows.Should().HaveCount(2);
            rows[0].Id.Should().Be(1);
            rows[0].Name.Should().Be("alpha");
            rows[0].Missing.Should().BeNull();
            rows[1].Id.Should().Be(2);
            rows[1].Name.Should().Be("beta");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void EntityRead_NoMatchingColumns_Throws()
    {
        var (ctx, path) = CreateDb();
        try
        {
            _ = ctx.From<RawOrder>();
            using var result = ctx.ExecuteRaw("select 1 as unrelated");

            var act = () => result.Read<RawOrder>();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void MultipleResultSets_ReadSequentially_ThenThrows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

            result.Read<int>().Should().Equal(1);
            result.Read<int>().Should().Equal(2);

            var act = () => result.Read<int>();
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MultipleResultSets_ReadSequentiallyAsync_ThenThrows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = await ctx.ExecuteRawAsync(
                "select 1 as a; select 2 as b",
                [],
                TestContext.Current.CancellationToken);

            var first = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                first.Add(row);
            first.Should().Equal(1);

            var second = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                second.Add(row);
            second.Should().Equal(2);

            var act = async () =>
            {
                await foreach (var _ in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                {
                }
            };

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void DbTypeAndSize_AreAppliedToParameter()
    {
        var interceptor = new RecordingInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            using var result = ctx.ExecuteRaw(
                "select @p as value",
                [new ProcedureParameter("p", "abc", DbType: DbType.String, Size: 64)]);

            result.Read<string>().Should().Equal("abc");

            interceptor.LastCommand!.Parameters.Count.Should().Be(1);
            interceptor.LastCommand.Parameters[0].DbType.Should().Be(DbType.String);
            interceptor.LastCommand.Parameters[0].Size.Should().Be(64);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TypeName_OnSqlite_ThrowsArgumentException()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // SQLite emulates a table parameter with JSON and has no named table type, so a TypeName
            // (SQL Server only) is an invalid argument rather than an unsupported feature.
            var act = () => ctx.ExecuteRaw("select @p as value", [new ProcedureParameter("p", 1, TypeName: "MyTableType")]);

            act.Should().Throw<ArgumentException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void UnsupportedResultType_ThrowsNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a");

            var act = () => result.Read<NotMappableRaw>();

            act.Should().Throw<NotSupportedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void EntityNotUsedByLinq_IsRegisteredOnDemand()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // No ctx.From<RawFreshEntity>() here: raw mapping must resolve the mapping itself.
            using var result = ctx.ExecuteRaw("select name, id from fresh_raw");

            var rows = result.Read<RawFreshEntity>();

            rows.Should().HaveCount(1);
            rows[0].Id.Should().Be(1);
            rows[0].Name.Should().Be("fresh");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void EntityWithParameterizedConstructor_ThrowsNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select name, id from raw_orders");

            var act = () => result.Read<RawCtorEntity>();

            act.Should().Throw<NotSupportedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void RawMapping_DoesNotWriteSharedSelectListCache()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Start from a clean process-wide state, then read an entity through ExecuteRaw. The raw
            // path must not populate DataContextCache.SelectListCache: a raw entry lacks a PlanHashCode,
            // and a later LINQ query reusing it would fold a wrong ColumnsPlanHash into its plan key.
            DataContextCache.Clear();

            using (var result = ctx.ExecuteRaw("select id, name from raw_orders"))
                result.Read<RawOrder>();

            DataContextCache.SelectListCache.ContainsKey(typeof(RawOrder)).Should().BeFalse();

            // The LINQ query still maps correctly and builds its own projection.
            ctx.From<RawOrder>().OrderBy(x => x.Id).Select(x => x.Name).ToList().Should().Equal("alpha", "beta");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void MappedColumnName_TakesPrecedenceOverPropertyName()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Reader "x" must bind property Y (mapped to column "x"), and "y" must bind property X.
            using var result = ctx.ExecuteRaw("select x, y from xy_raw");

            var rows = result.Read<RawXyEntity>();

            rows.Should().ContainSingle();
            rows[0].Y.Should().Be(10);
            rows[0].X.Should().Be(20);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void DuplicateReaderColumnName_BindsFirstOccurrence()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select id, id + 100 as id, name from raw_orders where id = 1");

            var rows = result.Read<RawOrder>();

            rows.Should().ContainSingle();
            rows[0].Id.Should().Be(1);
            rows[0].Name.Should().Be("alpha");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void RangePairs_AreReorderedAdjacent_RegardlessOfReaderOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Reader order puts the upper bound first; the range materializer needs lower immediately
            // before its upper, so the raw projection must reorder the pair while keeping ordinals.
            using var result = ctx.ExecuteRaw("select during_upper, id, during_lower from reservation where id = 1");

            var rows = result.Read<RangeColumnsTests.ReservationEntity>();

            rows.Should().ContainSingle();
            rows[0].Id.Should().Be(1);
            rows[0].During.Lower.Should().Be(5);
            rows[0].During.Upper.Should().Be(9);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SameSql_TwoResultSetsWithSwappedColumnOrder_MapCorrectly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // The same command text, but the second result set returns the columns in the opposite
            // order: the mapper is keyed by the result shape, so the two sets must not share a mapper.
            using var result = ctx.ExecuteRaw(
                "select id, name from raw_orders where id = 1; select name, id from raw_orders where id = 2");

            var first = result.Read<RawOrder>();
            first.Should().ContainSingle();
            first[0].Id.Should().Be(1);
            first[0].Name.Should().Be("alpha");

            var second = result.Read<RawOrder>();
            second.Should().ContainSingle();
            second[0].Id.Should().Be(2);
            second[0].Name.Should().Be("beta");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SyntaxError_RaisesCommandFailed_AndContextStaysUsable()
    {
        var interceptor = new RecordingInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            var act = () => ctx.ExecuteRaw("selct 1");

            act.Should().Throw<SqliteException>();
            interceptor.Events.Should().Contain("failed");

            // The failed command must have been disposed, so the connection is usable again.
            ctx.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(7);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_PreCancelledToken_DisposesCleanly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var act = async () => await ctx.ExecuteRawAsync("select 1 as a", [], cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();

            ctx.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(7);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteRaw_OnConcreteContext_CompilesAndRuns()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        using var result = ctx.ExecuteRaw("select @v as value", [new ProcedureParameter("v", 5)]);

        result.Read<int>().Should().Equal(5);
    }

    [Fact]
    public void NoOutputParameters_NormalCommand()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a");
            result.Read<int>();

            result.OutputParameters.Should().BeEmpty();
            result.ReturnValue.Should().BeNull();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void AfterDispose_LinqQueryOnSameContextWorks()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.ExecuteRaw("select id from raw_orders");
            result.Read<int>().Should().HaveCount(2);
            result.Dispose();

            ctx.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(7);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Interceptor_IsRaisedForRawCommand()
    {
        var interceptor = new RecordingInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a");
            result.Read<int>();

            interceptor.Events.Should().Equal("initialized", "executing", "executed");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Transaction_RollbackUndoesRawInsert()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var transaction = ((ITransactionManager)ctx).BeginTransaction();

            using (ctx.ExecuteRaw("insert into simple_entity (id) values (99)"))
            {
            }

            transaction.Rollback();

            ctx.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(7);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void DdlThenSelect_AdvancesPastEmptyResultSet()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // The reader starts on the zero-column DDL set; navigation must advance to the SELECT.
            using var result = ctx.ExecuteRaw("create table created_after_ddl (id integer); select 7 as value");

            result.Read<int>().Should().Equal(7);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DdlThenSelectAsync_AdvancesPastEmptyResultSet()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = await ctx.ExecuteRawAsync(
                "create table created_after_ddl_async (id integer); select 7 as value",
                [],
                TestContext.Current.CancellationToken);

            var rows = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(7);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DdlWithNoResultSet_ReadAsyncThrows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = await ctx.ExecuteRawAsync(
                "create table created_by_raw_async (id integer)",
                [],
                TestContext.Current.CancellationToken);

            var act = async () =>
            {
                await foreach (var _ in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                {
                }
            };

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ArgumentValidation()
    {
        var (ctx, path) = CreateDb();
        try
        {
            Action nullSql = () => ctx.ExecuteRaw(null!);
            nullSql.Should().Throw<ArgumentException>();

            Action blankSql = () => ctx.ExecuteRaw("   ");
            blankSql.Should().Throw<ArgumentException>();

            Action nullParameters = () => ctx.ExecuteRaw("select 1", null!);
            nullParameters.Should().Throw<ArgumentNullException>();

            // Explicit normal-form cast: the params overload must not swallow the typed null.
            Action nullParametersTyped = () => ctx.ExecuteRaw("select 1", (IReadOnlyList<ProcedureParameter>?)null!);
            nullParametersTyped.Should().Throw<ArgumentNullException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteProcedure_OnSqlite_ThrowsNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var act = () => ctx.ExecuteProcedure("my_proc");

            act.Should().Throw<NotSupportedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteProcedureAsync_OnSqlite_ThrowsNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var act = async () => await ctx.ExecuteProcedureAsync(
                "my_proc",
                [new ProcedureParameter("p", 1)],
                TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteProcedure_WhitespaceName_ThrowsArgumentException()
    {
        var (ctx, path) = CreateDb();
        try
        {
            Action blank = () => ctx.ExecuteProcedure("   ");
            blank.Should().Throw<ArgumentException>();

            Action nullName = () => ctx.ExecuteProcedure(null!);
            nullName.Should().Throw<ArgumentException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteRaw_DefaultProcedureParameter_ThrowsArgumentException_AndContextStaysUsable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // default(ProcedureParameter) has a null Name: the descriptor is rejected before any
            // provider parameter is allocated, and the per-call command is disposed on the failed bind.
            Action nullName = () => ctx.ExecuteRaw("select @p as value", [default(ProcedureParameter)]);
            nullName.Should().Throw<ArgumentException>();

            Action blankName = () => ctx.ExecuteRaw("select @p as value", [new ProcedureParameter("  ", 1)]);
            blankName.Should().Throw<ArgumentException>();

            // The connection is usable again, so no command/reader was leaked.
            using var result = ctx.ExecuteRaw("select @v as value", [new ProcedureParameter("v", 1)]);
            result.Read<int>().Should().Equal(1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteRaw_ExpandedForm_SingleParameter_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Expanded params form: one ProcedureParameter element, no list.
            using var result = ctx.ExecuteRaw("select @v as value", new ProcedureParameter("v", 41));

            result.Read<int>().Should().Equal(41);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteRaw_ExpandedForm_TwoParameters_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw(
                "select @a + @b as value",
                new ProcedureParameter("a", 2),
                new ProcedureParameter("b", 3));

            result.Read<int>().Should().Equal(5);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteRaw_ExpandedForm_NoParameters_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as value");

            result.Read<int>().Should().Equal(1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_ExpandedForm_SingleParameter_NoToken_Executes()
    {
        DataContext ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        try
        {
            // Direct tokenless call on the concrete context: expanded params, no list, no token.
            using var result = await ExecuteRawExpandedAsync(ctx, "select @min as value", new ProcedureParameter("min", 0));

            var rows = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(0);
        }
        finally
        {
            ctx.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_ExpandedForm_TwoParameters_NoToken_Executes()
    {
        DataContext ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        try
        {
            using var result = await ExecuteRawExpandedAsync(
                ctx,
                "select @min as value union all select @max",
                new ProcedureParameter("min", 0),
                new ProcedureParameter("max", 10));

            var rows = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(0, 10);
        }
        finally
        {
            ctx.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_ExpandedForm_SingleParameter_RawExecutor_NoToken_Executes()
    {
        DataContext concrete = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        try
        {
            IRawCommandExecutor ctx = concrete;

            using var result = await ExecuteRawExpandedAsync(ctx, "select @min as value", new ProcedureParameter("min", 0));

            var rows = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(0);
        }
        finally
        {
            concrete.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_ExpandedForm_TwoParameters_RawExecutor_NoToken_Executes()
    {
        DataContext concrete = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        try
        {
            IRawCommandExecutor ctx = concrete;

            using var result = await ExecuteRawExpandedAsync(
                ctx,
                "select @min as value union all select @max",
                new ProcedureParameter("min", 0),
                new ProcedureParameter("max", 10));

            var rows = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(0, 10);
        }
        finally
        {
            concrete.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_ExpandedForm_NoParameters_NoToken_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            Func<string, IReadOnlyList<ProcedureParameter>, Task<ProcedureResult>> rawAsync = ctx.ExecuteRawAsync;
            using var result = await rawAsync("select 1 as value", []);

            var rows = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_ExplicitListWithToken_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Normal form with an explicitly typed list still binds the token overload.
            IReadOnlyList<ProcedureParameter> parameters = [new ProcedureParameter("v", "abc")];

            using var result = await ctx.ExecuteRawAsync(
                "select @v as value",
                parameters,
                TestContext.Current.CancellationToken);

            var rows = new List<string>();
            await foreach (var row in result.ReadAsync<string>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal("abc");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_TokenOnlyExtension_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // (sql, ct) with no parameter list binds the parameterless extension overload.
            using var result = await ctx.ExecuteRawAsync("select 42 as value", TestContext.Current.CancellationToken);

            var rows = new List<int>();
            await foreach (var row in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                rows.Add(row);

            rows.Should().Equal(42);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ExecuteProcedure_ExpandedForm_OnSqlite_ThrowsNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // SQLite has no stored procedures: the expanded form must bind and reach the gate.
            var act = () => ctx.ExecuteProcedure("my_proc", new ProcedureParameter("p", 1));

            act.Should().Throw<NotSupportedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteProcedureAsync_ExpandedForm_OnSqlite_ThrowsNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // SQLite has no stored procedures: the direct expanded async call must bind and reach the gate.
            var act = async () => await ctx.ExecuteProcedureAsync("my_proc", new ProcedureParameter("p", 1));

            await act.Should().ThrowAsync<NotSupportedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

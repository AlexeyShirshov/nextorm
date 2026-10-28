using System.Data.Common;
using System.Globalization;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// The 2022 arm is deliberately its own xunit collection: it runs as a separate serial invocation so
/// the 2022 container never competes with the 2025 one (the dispatch forbids two containers at once).
/// The container lives for the whole collection and is released once here — a per-test-instance
/// dispose would stop it between two facts of the same class.
/// </summary>
[CollectionDefinition("SqlServer2022", DisableParallelization = true)]
public sealed class T348SqlServer2022Collection : IDisposable
{
    public void Dispose() => SqlServer2022Container.DisposeContainerAsync().GetAwaiter().GetResult();
}

/// <summary>
/// T348 (#282) U1 live evidence on a real SQL Server 2022 (major 16) engine: <c>left_shift</c>
/// executes with a value, a negative operand and the typed-NULL branch, while every 17-only exemplar is
/// refused by name before SQL with zero executions. Run separately from the 2025 arm.
/// </summary>
[Collection("SqlServer2022")]
[Trait("Category", "T348SqlServer2022")]
public sealed class T348SqlServer2022FunctionsTests : IDisposable
{
    private const string FixtureTable = "t348_u1";

    private const string FixtureDdl =
        "if object_id('t348_u1') is not null drop table t348_u1; "
        + "create table t348_u1 (id int not null primary key, grp int null, value int null, label nvarchar(50) null); "
        + "insert into t348_u1 (id, grp, value, label) values "
        + "(1, 1, 2, N'a1b2c3'), (2, 1, 3, N'abc'), (3, 1, 4, null), "
        + "(4, 2, 2, N'x'), (5, 2, null, null), (6, 2, 3, N'y'), (7, 3, null, null)";

    private readonly string? _connectionString;
    private readonly int _major;

    public T348SqlServer2022FunctionsTests()
    {
        if (!SqlServer2022Container.IsAvailable)
        {
            if (ProviderSkipPolicy.IsOptedOut("sqlserver"))
                Assert.Skip($"Provider 'sqlserver' is explicitly opted out via {ProviderSkipPolicy.EnvironmentVariable}.");

            SqlServer2022Container.IsAvailable.Should().BeTrue(
                "the SQL Server 2022 container is unavailable ({0}); the live arm fails closed rather than skipping silently",
                SqlServer2022Container.Failure);
        }

        _connectionString = SqlServer2022Container.ConnectionString;
        _major = ReadMajor(_connectionString);
        Execute(_connectionString, FixtureDdl);
    }

    public void Dispose()
    {
        if (_connectionString is not null)
            Execute(_connectionString, $"if object_id('{FixtureTable}') is not null drop table {FixtureTable}");
    }

    [Fact]
    public void Real2022_ShouldExecuteLeftShiftAndRefuseEvery2025NameBeforeSql()
    {
        _major.Should().Be(16, "the 2022 arm must run on the 2022 image");
        _connectionString.Should().NotBeNull();

        using var ctx = new T348V16LiveContext(_connectionString!);
        var counter = new CommandCounter();
        ctx.AddInterceptor(counter);

        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { C = SqlFunctions.SqlServer.regexp_count(x.Label, "[0-9]") }),
            "The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: 16.");

        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .GroupBy(x => new { x.Grp })
                .Select(x => new { x.Grp, P = SqlFunctions.SqlServer.product(x.Value) }),
            "The PRODUCT function requires SQL Server major version 17 or later; declared major version: 16.");

        ShouldRefuse(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_split_to_table("a,b", ","))
                .Select(r => r.Value),
            "The REGEXP_SPLIT_TO_TABLE function requires SQL Server major version 17 or later; declared major version: 16.");

        // U3: the regex scalars, the regexp_matches TVF and the four fuzzy names refuse by name on the
        // real major-16 engine too; none of them reaches the server.
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { S = SqlFunctions.SqlServer.regexp_substr(x.Label, "[0-9]") }),
            "The REGEXP_SUBSTR function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { I = SqlFunctions.SqlServer.regexp_instr(x.Label, "[0-9]") }),
            "The REGEXP_INSTR function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_matches("a1", "[0-9]"))
                .Select(r => r.MatchValue),
            "The REGEXP_MATCHES function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { D = SqlFunctions.SqlServer.edit_distance(x.Label, "kitten") }),
            "The EDIT_DISTANCE function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { S = SqlFunctions.SqlServer.edit_distance_similarity(x.Label, "kitten") }),
            "The EDIT_DISTANCE_SIMILARITY function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { D = SqlFunctions.SqlServer.jaro_winkler_distance(x.Label, "martha") }),
            "The JARO_WINKLER_DISTANCE function requires SQL Server major version 17 or later; declared major version: 16.");
        ShouldRefuse(ctx, ctx.From<IT348U1Row>()
                .Select(x => new { S = SqlFunctions.SqlServer.jaro_winkler_similarity(x.Label, "martha") }),
            "The JARO_WINKLER_SIMILARITY function requires SQL Server major version 17 or later; declared major version: 16.");

        counter.Executions.Should().Be(0, "every refusal is raised before a command is dispatched against the real 2022 engine");

        var row = ctx.From<IT348U1Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Shift = SqlFunctions.SqlServer.left_shift((int?)1, 2),
                Negative = SqlFunctions.SqlServer.left_shift((int?)-8, 1),
                NullOperand = SqlFunctions.SqlServer.left_shift((int?)null, 1)
            })
            .Single();

        row.Shift.Should().Be(4);
        row.Negative.Should().Be(-16, "the negative operand shifts arithmetically on the real engine");
        row.NullOperand.Should().BeNull("the typed-NULL guard removes Msg 8116");
        ((object)row.Shift).Should().BeOfType<int>();
        counter.Executions.Should().BeGreaterThan(0, "the positive 2022 control executed a real command");

        T348Evidence.Write(
            $"T348LIVE-2022 major={_major} left_shift=4/-16/null refusals=10 executions_before_control=0 executions_total={counter.Executions} outcome=ok");
    }

    /// <summary>
    /// U2 on the real 2022 engine: the whole bit family executes over every operand shape and both
    /// conversions execute the closed map, so the 16-minimum gate is proven by a real major-16 server
    /// rather than by the declaration alone. The 2025-only refusals stay covered by the sibling fact.
    /// </summary>
    [Fact]
    public void Real2022_ShouldExecuteTheWholeBitFamilyAndBothConversions()
    {
        _major.Should().Be(16, "the 2022 arm must run on the 2022 image");

        using var ctx = new T348V16LiveContext(_connectionString!);

        var row = ctx.From<IT348U1Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                TinyL = SqlFunctions.SqlServer.left_shift((byte?)x.Value, 1),
                TinyR = SqlFunctions.SqlServer.right_shift((byte?)x.Value, 1),
                TinyC = SqlFunctions.SqlServer.bit_count((byte?)x.Value),
                TinyG = SqlFunctions.SqlServer.get_bit((byte?)x.Value, 1),
                TinyS = SqlFunctions.SqlServer.set_bit((byte?)x.Value, 0),
                TinyS3 = SqlFunctions.SqlServer.set_bit((byte?)x.Value, 0, false),
                TinyNull = SqlFunctions.SqlServer.left_shift((byte?)null, 1),

                SmallL = SqlFunctions.SqlServer.left_shift((short?)x.Value, 1),
                SmallNull = SqlFunctions.SqlServer.set_bit((short?)null, 1),

                IntL = SqlFunctions.SqlServer.left_shift(x.Value, 1),
                IntR = SqlFunctions.SqlServer.right_shift(x.Value, 1),
                IntC = SqlFunctions.SqlServer.bit_count(x.Value),
                IntG = SqlFunctions.SqlServer.get_bit(x.Value, 1),
                IntS = SqlFunctions.SqlServer.set_bit(x.Value, 0),
                IntNullShift = SqlFunctions.SqlServer.left_shift(x.Value, null),

                LongL = SqlFunctions.SqlServer.left_shift((long?)x.Id, 1),
                LongNull = SqlFunctions.SqlServer.bit_count((long?)null),

                RightNegative = SqlFunctions.SqlServer.right_shift((int?)-8, 1),

                CastInt = SqlFunctions.SqlServer.try_cast<int>("42"),
                CastBad = SqlFunctions.SqlServer.try_cast<int>("x"),
                CastGuid = SqlFunctions.SqlServer.try_cast<Guid>("6F9619FF-8B86-D011-B42D-00C04FC964FF"),
                ConvertDecimal = SqlFunctions.SqlServer.try_convert<decimal>("1.5"),
                ConvertBad = SqlFunctions.SqlServer.try_convert<decimal>("x"),
                ConvertStyled = SqlFunctions.SqlServer.try_convert<DateTime>("2024-01-02T03:04:05", 126),
                ConvertNullStyle = SqlFunctions.SqlServer.try_convert<int>("42", null)
            })
            .Single();

        row.TinyL.Should().Be((byte)4);
        row.TinyR.Should().Be((byte)1);
        row.TinyC.Should().Be(1L);
        row.TinyG.Should().BeTrue();
        row.TinyS.Should().Be((byte)3);
        row.TinyS3.Should().Be((byte)2);
        row.TinyNull.Should().BeNull();
        row.SmallL.Should().Be((short)4);
        row.SmallNull.Should().BeNull();
        row.IntL.Should().Be(4);
        row.IntR.Should().Be(1);
        row.IntC.Should().Be(1L);
        row.IntG.Should().BeTrue();
        row.IntS.Should().Be(3);
        row.IntNullShift.Should().BeNull();
        row.LongL.Should().Be(2L);
        row.LongNull.Should().BeNull();
        row.RightNegative.Should().Be(2147483644, "RIGHT_SHIFT is a logical shift on 2022 as well");
        row.CastInt.Should().Be(42);
        row.CastBad.Should().BeNull();
        row.CastGuid.Should().Be(Guid.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF"));
        row.ConvertDecimal.Should().Be(1.5m);
        row.ConvertBad.Should().BeNull();
        row.ConvertStyled.Should().Be(new DateTime(2024, 1, 2, 3, 4, 5));
        row.ConvertNullStyle.Should().BeNull("a NULL style yields NULL on 2022 too");

        ((object)row.TinyL!).Should().BeOfType<byte>();
        ((object)row.IntC!).Should().BeOfType<long>();
        ((object)row.IntG!).Should().BeOfType<bool>();
        ((object)row.LongL!).Should().BeOfType<long>();
        ((object)row.CastGuid!).Should().BeOfType<Guid>();

        T348Evidence.Write(
            "T348LIVE-2022 bit 4/1/1/true/3/2/null + shapes + logical-right-shift=2147483644 try_cast/try_convert 42/null/1.5/null/126/null-style-null outcome=ok");
    }

    private static void ShouldRefuse<T>(IDataContext ctx, QueryCommand<T> command, string message)
    {
        var act = () => ((DataContext)ctx).GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(message);
    }

    private static int ReadMajor(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select cast(serverproperty('ProductMajorVersion') as int)";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Counts the commands that actually reach execution.</summary>
    private sealed class CommandCounter : IQueryInterceptor
    {
        private int _executions;

        public int Executions => Volatile.Read(ref _executions);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executions);
    }
}

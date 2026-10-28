using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #252 live arm: the version-gated <c>array_except</c>. The server version is read first with raw
/// ADO.NET; below 25.9 the fact skips so the pinned <c>25.8-alpine</c> container stays green, and at
/// 25.9+ a context that <em>declares</em> that version renders the native <c>arrayExcept(...)</c> and
/// the returned rows are asserted (the SQL-shape gate lives in
/// <c>tests/nextorm.clickhouse.tests/Functions/ClickHouseArrayExceptVersionGateTests.cs</c>).
///
/// <para>
/// The declared context is a distinct nested subclass because a concrete context type owns one
/// immutable capability declaration — the shared unversioned <c>_sut</c> of every other ClickHouse
/// class must keep the pinned-baseline dialect. The class seeds its own <c>t351_array_except</c>
/// Memory table, so no assertion depends on another suite's seed layout.
/// </para>
///
/// <para>
/// The undeclared live refusal (the same 25.9 server, an undeclared context) is pinned by
/// <c>ClickHouseFunctionExecutionTests.ArrayExcept_ShouldRefuseBeforeTouchingTheServer</c>.
/// </para>
/// </summary>
public sealed class ClickHouseArrayExceptLiveTests : ProviderTestSuite
{
    [SqlTable("t351_array_except")]
    internal interface IT351ArrayExceptEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("nums")]
        long[] Nums { get; set; }

        [Column("other")]
        long[] Other { get; set; }

        [Column("nints")]
        long?[] NullableNums { get; set; }

        [Column("nother")]
        long?[] NullableOther { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static readonly Version ArrayExceptVersion = new(25, 9);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    /// <summary>The live server version, read through raw ADO.NET (never through the dialect under test).</summary>
    private static Version ServerVersion()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version()";
        var raw = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!;
        return Version.Parse(raw.Split(' ')[0]);
    }

    /// <summary>
    /// Creates the class-owned table with one marker row per documented case:
    /// <c>[1,2,3]</c> minus <c>[2]</c>, <c>[1,2,3,2]</c> minus <c>[2]</c>, and the nullable-element
    /// pair <c>[1,NULL,3]</c> minus <c>[2]</c> / minus <c>[NULL]</c>.
    /// </summary>
    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();

        foreach (var statement in new[]
                 {
                     "drop table if exists t351_array_except",
                     "create table t351_array_except (id Int32, nums Array(Int64), other Array(Int64), "
                     + "nints Array(Nullable(Int64)), nother Array(Nullable(Int64))) engine = Memory",
                     "insert into t351_array_except (id, nums, other, nints, nother) values "
                     + "(351, [1, 2, 3], [2], [], []), "
                     + "(352, [1, 2, 3, 2], [2], [], []), "
                     + "(353, [], [], [1, NULL, 3], [2]), "
                     + "(354, [], [], [1, NULL, 3], [NULL])"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void ArrayExcept_ShouldExecuteOnTheDeclaredServerVersion()
    {
        var version = ServerVersion();
        Assert.SkipWhen(
            version < ArrayExceptVersion,
            $"the live server is {version}; the native arrayExcept exists only from ClickHouse 25.9, "
            + "so this arm is not evidence on the pinned 25.8 baseline");

        Seed();

        using var ctx = new DeclaredArrayExceptContext(
            version, ClickHouseContainer.ConnectionString, new DataContextBuilder());

        var e = ctx.From<IT351ArrayExceptEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 351).Select(x => new
        {
            Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Other)
        }));
        sql.Should().Contain("arrayExcept(", "a declared 25.9+ context renders the native function");

        var two = ctx.From<IT351ArrayExceptEntity>()
            .Where(x => x.Id == 351)
            .Select(x => new { Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Other) })
            .First();
        two.Difference.Should().Equal(new long[] { 1L, 3L }, "[1,2,3] except [2] is [1,3]");

        var repeated = ctx.From<IT351ArrayExceptEntity>()
            .Where(x => x.Id == 352)
            .Select(x => new { Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Other) })
            .First();
        repeated.Difference.Should().Equal(new long[] { 1L, 3L }, "[1,2,3,2] except [2] is [1,3] — every occurrence is removed");

        // The native function takes exactly two arrays. The wider params signature must fail closed
        // before SQL, so the third-array form never reaches the server as NUMBER_OF_ARGUMENTS_DOESNT_MATCH.
        FluentActions.Invoking(() => ctx.From<IT351ArrayExceptEntity>()
                .Where(x => x.Id == 351)
                .Select(x => new
                {
                    Difference = SqlFunctions.ClickHouse.array_except(x.Nums, x.Other, x.Nums)
                })
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage(
                "The array_except function is not supported by this provider with 3 array argument(s): " +
                "ClickHouse's arrayExcept takes exactly two arrays.");
    }

    /// <summary>
    /// AC8 nullable semantics: the native <c>arrayExcept</c> keeps a NULL element when the right-hand
    /// array has no NULL, and removes it when the right-hand array carries NULL
    /// (<c>[1,NULL,3] except [NULL] = [1,3]</c> on 25.9.7.56). The nextorm projection must materialise
    /// exactly the server's answer as <c>long?[]</c> — the nullability of the elements does not change
    /// the spelling or the gate.
    /// </summary>
    [Fact]
    public void ArrayExcept_ShouldPreserveNativeNullableElementSemantics()
    {
        var version = ServerVersion();
        Assert.SkipWhen(
            version < ArrayExceptVersion,
            $"the live server is {version}; the native arrayExcept exists only from ClickHouse 25.9");

        Seed();

        using var ctx = new DeclaredArrayExceptContext(
            version, ClickHouseContainer.ConnectionString, new DataContextBuilder());

        var keptNull = ctx.From<IT351ArrayExceptEntity>()
            .Where(x => x.Id == 353)
            .Select(x => new
            {
                Difference = SqlFunctions.ClickHouse.array_except(x.NullableNums, x.NullableOther)
            })
            .First();
        keptNull.Difference.Should().Equal(
            new long?[] { 1L, null, 3L },
            "the server returns [1,NULL,3] for [1,NULL,3] except [2]: a NULL element is preserved");

        var removedNull = ctx.From<IT351ArrayExceptEntity>()
            .Where(x => x.Id == 354)
            .Select(x => new
            {
                Difference = SqlFunctions.ClickHouse.array_except(x.NullableNums, x.NullableOther)
            })
            .First();
        removedNull.Difference.Should().Equal(
            new long?[] { 1L, 3L },
            "the server returns [1,3] for [1,NULL,3] except [NULL]: a NULL element is removed");
    }

    /// <summary>
    /// One declaration per concrete context type: the version is caller-supplied and constant for the
    /// type, so the class declares it through this subclass (never through the shared context type).
    /// </summary>
    private sealed class DeclaredArrayExceptContext : ClickHouseDataContext
    {
        public DeclaredArrayExceptContext(
            Version serverVersion,
            string connectionString,
            DataContextBuilder optionsBuilder)
            : base(serverVersion, connectionString, optionsBuilder)
        {
        }
    }
}

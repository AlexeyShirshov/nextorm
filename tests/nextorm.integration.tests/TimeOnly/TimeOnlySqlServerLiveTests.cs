using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T340 (#340) SQL Server live matrix for a native <c>time</c> column projected as
/// <see cref="TimeOnly"/>. The table is created and seeded through raw T-SQL (never through the
/// mapper), so the assertions observe only what the reader actually returns.
/// <para>
/// SqlClient surfaces <c>time</c> as <see cref="TimeSpan"/>, which is why the same physical column is
/// also projected as <see cref="TimeSpan"/> (the T340 sibling control): the two projections must agree
/// tick for tick, and the <c>date</c>/<c>datetime2</c> columns are the frozen baseline controls.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class TimeOnlySqlServerLiveTests : ProviderTestSuite
{
    private const string Table = "t340_time_probe";

    /// <summary>00:00:00.0000000.</summary>
    private static readonly TimeSpan Midnight = TimeSpan.Zero;

    /// <summary>13:45:07.1234567.</summary>
    private static readonly TimeSpan Fractional7 = TimeSpan.FromTicks(495_071_234_567);

    /// <summary>23:59:59.9999999 — the last 100 ns tick <c>time(7)</c> can hold.</summary>
    private static readonly TimeSpan Maximum7 = TimeSpan.FromTicks(863_999_999_999);

    /// <summary>13:45:07.123 — the <c>time(3)</c> form of <see cref="Fractional7"/>.</summary>
    private static readonly TimeSpan Fractional3 = TimeSpan.FromTicks(495_071_230_000);

    /// <summary>23:59:59.999 — the last millisecond <c>time(3)</c> can hold.</summary>
    private static readonly TimeSpan Maximum3 = TimeSpan.FromTicks(863_999_990_000);

    private readonly ITestOutputHelper _output;

    public TimeOnlySqlServerLiveTests(ITestOutputHelper output) => _output = output;

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [SqlTable(Table)]
    public interface ITimeOnlyProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("t7")]
        TimeOnly? T7 { get; set; }

        [Column("t3")]
        TimeOnly? T3 { get; set; }

        [Column("tplain")]
        TimeOnly? TPlain { get; set; }

        [Column("d")]
        DateTime? Date { get; set; }

        [Column("dt2")]
        DateTime? DateTime2 { get; set; }
    }

    [SqlTable(Table)]
    public interface IDateOnlyProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("d")]
        DateOnly? D { get; set; }
    }

    [SqlTable(Table)]
    public interface ISpanProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("t7")]
        TimeSpan? T7 { get; set; }

        [Column("t3")]
        TimeSpan? T3 { get; set; }

        [Column("tplain")]
        TimeSpan? TPlain { get; set; }
    }

    /// <summary>AC1/AC2: a mapped <see cref="TimeOnly"/> projection returns the native ticks exactly.</summary>
    [Fact]
    public void MappedTimeOnly_ShouldReturnExactTicks()
    {
        CreateFixture();
        try
        {
            var rows = Context.From<ITimeOnlyProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.T7, x.T3, x.TPlain })
                .ToList();

            rows.Should().HaveCount(4);

            rows[0].T7.Should().Be(TimeOnly.FromTimeSpan(Midnight));
            rows[1].T7!.Value.Ticks.Should().Be(Fractional7.Ticks);
            rows[2].T7!.Value.Ticks.Should().Be(Maximum7.Ticks);
            rows[3].T7.Should().BeNull();

            rows[1].T3!.Value.Ticks.Should().Be(Fractional3.Ticks);
            rows[2].T3!.Value.Ticks.Should().Be(Maximum3.Ticks);

            rows[1].TPlain!.Value.Ticks.Should().Be(Fractional7.Ticks);
            rows[2].TPlain!.Value.Ticks.Should().Be(Maximum7.Ticks);
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC5/CC2: the scalar terminal of the same physical column agrees with the mapped row.</summary>
    [Fact]
    public void TopLevelScalar_ShouldReturnExactValue()
    {
        CreateFixture();
        try
        {
            var values = Context.From<ITimeOnlyProbe>()
                .OrderBy(x => x.Id)
                .Select(x => x.T7)
                .ToList();

            values.Should().HaveCount(4);
            values[0]!.Value.Ticks.Should().Be(Midnight.Ticks);
            values[1]!.Value.Ticks.Should().Be(Fractional7.Ticks);
            values[2]!.Value.Ticks.Should().Be(Maximum7.Ticks);
            values[3].Should().BeNull();
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>CC4: the unchanged <see cref="TimeSpan"/> projection of the same columns returns identical ticks.</summary>
    [Fact]
    public void TimeSpanControl_ShouldReturnIdenticalTicks()
    {
        CreateFixture();
        try
        {
            var rows = Context.From<ISpanProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.T7, x.T3, x.TPlain })
                .ToList();

            rows[0].T7!.Value.Ticks.Should().Be(Midnight.Ticks);
            rows[1].T7!.Value.Ticks.Should().Be(Fractional7.Ticks);
            rows[2].T7!.Value.Ticks.Should().Be(Maximum7.Ticks);
            rows[3].T7.Should().BeNull();

            rows[1].T3!.Value.Ticks.Should().Be(Fractional3.Ticks);
            rows[2].T3!.Value.Ticks.Should().Be(Maximum3.Ticks);

            rows[1].TPlain!.Value.Ticks.Should().Be(Fractional7.Ticks);
            rows[2].TPlain!.Value.Ticks.Should().Be(Maximum7.Ticks);
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>CC4: the frozen <c>DateTime</c> control columns keep their exact values.</summary>
    [Fact]
    public void DateControls_ShouldBeFrozen()
    {
        CreateFixture();
        try
        {
            var rows = Context.From<ITimeOnlyProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Date, x.DateTime2 })
                .ToList();

            rows[1].Date.Should().Be(new DateTime(2020, 3, 1));
            rows[1].DateTime2.Should().Be(new DateTime(2020, 3, 1, 1, 2, 3).AddTicks(1));
            rows[3].Date.Should().BeNull();
            rows[3].DateTime2.Should().BeNull();
            _output.WriteLine($"T340 SQL Server date control: {rows[1].Date:O} / {rows[1].DateTime2:O}");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>
    /// CC4 baseline: a <see cref="DateOnly"/> projection is <em>not</em> part of this task's contract.
    /// The fact records the current behaviour (exact value, or the rejection message) so a later change
    /// is a deliberate decision instead of an accident.
    /// </summary>
    [Fact]
    public void DateOnlyBaseline_ShouldBeRecorded()
    {
        CreateFixture();
        try
        {
            DateOnly? value = null;
            Exception? failure = null;
            try
            {
                value = Context.From<IDateOnlyProbe>()
                    .Where(x => x.Id == 2)
                    .Select(x => x.D)
                    .First();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (failure is null)
            {
                _output.WriteLine($"T340 SQL Server DateOnly baseline: supported ({value:O})");
                value.Should().Be(new DateOnly(2020, 3, 1));
            }
            else
            {
                _output.WriteLine($"T340 SQL Server DateOnly baseline: {failure.GetType().FullName}: {failure.Message}");
                failure.Message.Should().Contain("DateOnly",
                    "the frozen baseline rejects a DateOnly projection explicitly; the exact type and text live in E/date-controls.md");
            }
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC5: the prepared command and its plan are reused, and both executions agree.</summary>
    [Fact]
    public void PreparedCommand_ShouldBeReused()
    {
        CreateFixture();
        try
        {
            var context = (DataContext)Context;
            context.PurgeQueryCache();
            var holdersBefore = QueryPlanStore.Values.Count();

            var first = Prepare(context);
            var second = Prepare(context);

            second.Should().BeSameAs(first, "the same shape must be served from the plan store");
            QueryPlanStore.Values.Count().Should().Be(holdersBefore,
                "preparation must not grow the plan store on a cache hit");

            var rows = Context.From<ITimeOnlyProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.T7 })
                .ToList();
            var again = Context.From<ITimeOnlyProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.T7 })
                .ToList();

            again.Select(x => x.T7?.Ticks).Should().Equal(rows.Select(x => x.T7?.Ticks),
                "a repeated execution returns the identical values");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>
    /// AC5: the top-level scalar projection must be dispatched through the <c>ExecuteScalar</c> seam
    /// (<c>MapDelegate is null</c>). The failure message reports the observed dispatch so a wrong route
    /// is visible in the red log instead of being guessed.
    /// </summary>
    [Fact]
    public void ScalarTerminal_ShouldMatchMappedRows()
    {
        CreateFixture();
        try
        {
            var scalarPath = ScalarDispatchIsMapperFree();

            var value = Context.From<ITimeOnlyProbe>()
                .Where(x => x.Id == 2)
                .Select(x => x.T7)
                .First();

            value!.Value.Ticks.Should().Be(Fractional7.Ticks,
                $"scalar dispatch: MapDelegate-null={scalarPath}");
            scalarPath.Should().BeTrue(
                "a single-column projection is read by SqlCommand.ExecuteScalar, not by a compiled row mapper");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC6/CC6: the rendered SQL of the computed-time control query, captured before any change.</summary>
    [Fact]
    public void SqlControl_ShouldBeStable()
    {
        CreateFixture();
        try
        {
            var sql = SqlOf(Context, Context.From<ISpanProbe>()
                .Where(x => x.Id == 2)
                .Select(x => new { x.Id, Tf = SqlFunctions.SqlServer.timefromparts(13, 45, 7, 123, 3) }));

            _output.WriteLine("T340 SQL Server timefromparts control SQL:\n" + sql);
            sql.Should().Contain("timefromparts(", "the computed time keeps its T-SQL form");
            sql.Should().Contain("t340_time_probe", "the control query reads this task's own table");
        }
        finally
        {
            DropFixture();
        }
    }

    private static string SqlOf<TResult>(IDataContext ctx, QueryCommand<TResult> command)
        => ((DbPreparedQueryCommand<TResult>)((DataContext)ctx)
                .GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static object Prepare(DataContext context)
        => context.GetPreparedQueryCommand(
            context.From<ITimeOnlyProbe>().OrderBy(x => x.Id).Select(x => new { x.Id, x.T7 }),
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    private bool ScalarDispatchIsMapperFree()
    {
        var context = (DataContext)Context;
        var prepared = (DbPreparedQueryCommand<TimeOnly?>)context.GetPreparedQueryCommand(
            context.From<ITimeOnlyProbe>().Where(x => x.Id == 2).Select(x => x.T7),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);
        return prepared.MapDelegate is null;
    }

    private void CreateFixture()
    {
        Execute($"drop table if exists {Table}");
        Execute(
            $"create table {Table} (id bigint not null primary key, t7 time(7) null, t3 time(3) null, " +
            "tplain time null, d date null, dt2 datetime2(7) null)");

        Execute(
            $"insert into {Table} (id, t7, t3, tplain, d, dt2) values " +
            "(1, cast('00:00:00' as time(7)), cast('00:00:00' as time(3)), cast('00:00:00' as time), " +
            "cast('2020-02-29' as date), cast('2020-02-29T10:11:12.3456789' as datetime2(7)))");
        Execute(
            $"insert into {Table} (id, t7, t3, tplain, d, dt2) values " +
            "(2, cast('13:45:07.1234567' as time(7)), cast('13:45:07.123' as time(3)), cast('13:45:07.1234567' as time), " +
            "cast('2020-03-01' as date), cast('2020-03-01T01:02:03.0000001' as datetime2(7)))");
        Execute(
            $"insert into {Table} (id, t7, t3, tplain, d, dt2) values " +
            "(3, cast('23:59:59.9999999' as time(7)), cast('23:59:59.999' as time(3)), cast('23:59:59.9999999' as time), " +
            "cast('2020-03-02' as date), cast('2020-03-02T23:59:59.9999999' as datetime2(7)))");
        Execute(
            $"insert into {Table} (id, t7, t3, tplain, d, dt2) values (4, null, null, null, null, null)");
    }

    private void DropFixture() => Execute($"drop table if exists {Table}");

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}

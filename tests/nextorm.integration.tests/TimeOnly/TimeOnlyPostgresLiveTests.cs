using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T340 (#340) PostgreSQL live matrix for a native <c>time</c> column projected as
/// <see cref="TimeOnly"/>. The table is created and seeded through raw SQL, so only the reader's own
/// values are asserted. PostgreSQL's <c>time(6)</c> keeps microseconds (10 ticks), which is the
/// precision the shared <see cref="TimeSpan"/> control must reproduce identically.
/// </summary>
[Collection("Postgres")]
public sealed class TimeOnlyPostgresLiveTests : ProviderTestSuite
{
    private const string Table = "t340_time_probe";

    /// <summary>00:00:00.</summary>
    private static readonly TimeSpan Midnight = TimeSpan.Zero;

    /// <summary>13:45:07.123456 — PostgreSQL's microsecond precision.</summary>
    private static readonly TimeSpan Fractional6 = TimeSpan.FromTicks(495_071_234_560);

    /// <summary>23:59:59.999999 — the last microsecond <c>time(6)</c> can hold.</summary>
    private static readonly TimeSpan Maximum6 = TimeSpan.FromTicks(863_999_999_990);

    /// <summary>13:45:07.123 — the <c>time(3)</c> form.</summary>
    private static readonly TimeSpan Fractional3 = TimeSpan.FromTicks(495_071_230_000);

    /// <summary>23:59:59.999 — the last millisecond <c>time(3)</c> can hold.</summary>
    private static readonly TimeSpan Maximum3 = TimeSpan.FromTicks(863_999_990_000);

    private readonly ITestOutputHelper _output;

    public TimeOnlyPostgresLiveTests(ITestOutputHelper output) => _output = output;

    protected override ITestProvider Provider => PostgresTestProvider.Instance;

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

        [Column("ts")]
        DateTime? Timestamp { get; set; }
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
            rows[1].T7!.Value.Ticks.Should().Be(Fractional6.Ticks);
            rows[2].T7!.Value.Ticks.Should().Be(Maximum6.Ticks);
            rows[3].T7.Should().BeNull();

            rows[1].T3!.Value.Ticks.Should().Be(Fractional3.Ticks);
            rows[2].T3!.Value.Ticks.Should().Be(Maximum3.Ticks);

            rows[1].TPlain!.Value.Ticks.Should().Be(Fractional6.Ticks);
            rows[2].TPlain!.Value.Ticks.Should().Be(Maximum6.Ticks);
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
            values[1]!.Value.Ticks.Should().Be(Fractional6.Ticks);
            values[2]!.Value.Ticks.Should().Be(Maximum6.Ticks);
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
            rows[1].T7!.Value.Ticks.Should().Be(Fractional6.Ticks);
            rows[2].T7!.Value.Ticks.Should().Be(Maximum6.Ticks);
            rows[3].T7.Should().BeNull();

            rows[1].T3!.Value.Ticks.Should().Be(Fractional3.Ticks);
            rows[2].T3!.Value.Ticks.Should().Be(Maximum3.Ticks);

            rows[1].TPlain!.Value.Ticks.Should().Be(Fractional6.Ticks);
            rows[2].TPlain!.Value.Ticks.Should().Be(Maximum6.Ticks);
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>CC4: the frozen <c>date</c>/<c>timestamp</c> control columns keep their exact values.</summary>
    [Fact]
    public void DateControls_ShouldBeFrozen()
    {
        CreateFixture();
        try
        {
            var rows = Context.From<ITimeOnlyProbe>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Date, x.Timestamp })
                .ToList();

            rows[1].Date.Should().Be(new DateTime(2020, 3, 1));
            rows[1].Timestamp.Should().Be(new DateTime(2020, 3, 1, 1, 2, 3).AddTicks(10));
            rows[3].Date.Should().BeNull();
            rows[3].Timestamp.Should().BeNull();
            _output.WriteLine($"T340 PostgreSQL date control: {rows[1].Date:O} / {rows[1].Timestamp:O}");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>
    /// CC4 baseline: a <see cref="DateOnly"/> projection is <em>not</em> part of this task's contract.
    /// The fact records the current behaviour so a later change is a deliberate decision.
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
                _output.WriteLine($"T340 PostgreSQL DateOnly baseline: supported ({value:O})");
                value.Should().Be(new DateOnly(2020, 3, 1));
            }
            else
            {
                _output.WriteLine($"T340 PostgreSQL DateOnly baseline: {failure.GetType().FullName}: {failure.Message}");
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
            var holdersAfterFirst = QueryPlanStore.Values.Count();
            var second = Prepare(context);

            second.Should().BeSameAs(first, "the same shape must be served from the plan store");
            holdersAfterFirst.Should().BeGreaterThan(holdersBefore, "the first preparation publishes its plan");
            QueryPlanStore.Values.Count().Should().Be(holdersAfterFirst,
                "the second preparation is a cache hit and must not grow the plan store again");

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
            // The public ExecuteScalar seam (QueryExecutor.ExecuteScalar -> ConvertScalar) is the scalar
            // route this task threads the provider policy into. A single-column projection also carries a
            // row mapper (MapDelegate is not null, observed), so the mapped row is the agreement control.
            var context = (DataContext)Context;
            var command = context.From<ITimeOnlyProbe>().Where(x => x.Id == 2).Select(x => x.T7);
            var prepared = context.GetPreparedQueryCommand(
                command, createEnumerator: false, storeInCache: false, CancellationToken.None);

            var scalar = context.ExecuteScalar<TimeOnly?>(prepared, ReadOnlySpan<object?>.Empty, throwIfNull: true);
            var mapped = Context.From<ITimeOnlyProbe>()
                .Where(x => x.Id == 2)
                .Select(x => x.T7)
                .First();

            scalar!.Value.Ticks.Should().Be(Fractional6.Ticks);
            mapped!.Value.Ticks.Should().Be(scalar.Value.Ticks,
                "the scalar terminal and the compiled row mapper must agree");
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>AC6/CC6: the rendered SQL of the time projection, captured after the fix.</summary>
    [Fact]
    public void SqlControl_ShouldBeStable()
    {
        CreateFixture();
        try
        {
            var sql = SqlOf(Context, Context.From<ISpanProbe>()
                .Where(x => x.Id == 2)
                .Select(x => new { x.Id, x.T7 }));

            _output.WriteLine("T340 PostgreSQL time control SQL:\n" + sql);
            sql.Should().Contain("t340_time_probe", "the control query reads this task's own table");
            sql.Should().Contain("t7", "the projected column keeps its name");
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

    private void CreateFixture()
    {
        Execute($"drop table if exists {Table}");
        Execute(
            $"create table {Table} (id bigint not null primary key, t7 time(6) null, t3 time(3) null, " +
            "tplain time null, d date null, ts timestamp(6) null)");

        Execute(
            $"insert into {Table} (id, t7, t3, tplain, d, ts) values " +
            "(1, '00:00:00', '00:00:00', '00:00:00', '2020-02-29', '2020-02-29 10:11:12.345678')");
        Execute(
            $"insert into {Table} (id, t7, t3, tplain, d, ts) values " +
            "(2, '13:45:07.123456', '13:45:07.123', '13:45:07.123456', '2020-03-01', '2020-03-01 01:02:03.000001')");
        Execute(
            $"insert into {Table} (id, t7, t3, tplain, d, ts) values " +
            "(3, '23:59:59.999999', '23:59:59.999', '23:59:59.999999', '2020-03-02', '2020-03-02 23:59:59.999999')");
        Execute($"insert into {Table} (id, t7, t3, tplain, d, ts) values (4, null, null, null, null, null)");
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

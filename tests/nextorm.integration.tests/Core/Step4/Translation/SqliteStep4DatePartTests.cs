using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteStep4DatePartTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_dp_row (id integer primary key, dt datetime null);
        delete from s4_dp_row;
        insert into s4_dp_row (id, dt) values (1, '2023-04-05 06:07:08');
        """;

    public SqliteStep4DatePartTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void DatePart_Year_ShouldReturnYear()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1).Select(x => x.Dt!.Value.Year).Single();
        r.Should().Be(2023);
    }

    [Fact]
    public void DatePart_Month_ShouldReturnMonth()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1).Select(x => x.Dt!.Value.Month).Single();
        r.Should().Be(4);
    }

    [Fact]
    public void DatePart_Day_ShouldReturnDay()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1).Select(x => x.Dt!.Value.Day).Single();
        r.Should().Be(5);
    }

    [Fact]
    public void DatePart_Hour_ShouldReturnHour()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1).Select(x => x.Dt!.Value.Hour).Single();
        r.Should().Be(6);
    }

    [Fact]
    public void DatePart_Minute_ShouldReturnMinute()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1).Select(x => x.Dt!.Value.Minute).Single();
        r.Should().Be(7);
    }

    [Fact]
    public void DatePart_Second_ShouldReturnSecond()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1).Select(x => x.Dt!.Value.Second).Single();
        r.Should().Be(8);
    }

    [Fact]
    public void DatePart_DayOfWeek_ShouldReturnDow()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.extract("dow", x.Dt!.Value)).Single();
        r.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void DatePart_Isodow_ShouldReturnIsodow()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.extract("isodow", x.Dt!.Value)).Single();
        r.Should().BeInRange(1, 7);
    }

    [Fact]
    public void DatePart_Quarter_ShouldReturnQuarter()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.extract("quarter", x.Dt!.Value)).Single();
        r.Should().Be(2);
    }

    [Fact]
    public void DatePart_Week_ShouldReturnWeek()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.extract("week", x.Dt!.Value)).Single();
        r.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void DatePart_Doy_ShouldReturnDayOfYear()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.extract("doy", x.Dt!.Value)).Single();
        r.Should().Be(95);
    }

    [Fact]
    public void DateDiff_Day_ShouldReturnDayDelta()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("day", x.Dt!.Value, x.Dt!.Value.AddDays(3))).Single();
        r.Should().Be(3);
    }

    [Fact]
    public void DateDiff_Hour_ShouldReturnHourDelta()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("hour", x.Dt!.Value, x.Dt!.Value.AddHours(5))).Single();
        r.Should().Be(5);
    }

    [Fact]
    public void DateDiff_Month_ShouldReturnMonthDelta()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("month", x.Dt!.Value, x.Dt!.Value.AddMonths(2))).Single();
        r.Should().Be(2);
    }

    [Fact]
    public void DateDiff_Week_ShouldReturnWeekDelta()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("week", x.Dt!.Value, x.Dt!.Value.AddDays(14))).Single();
        r.Should().Be(2);
    }

    [Fact]
    public void DateAdd_Week_ShouldShiftByWeek()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("week", 2, x.Dt!.Value)).Single();
        r.Should().Be(new DateTime(2023, 4, 19, 6, 7, 8));
    }

    [Fact]
    public void DateAdd_Month_ShouldShiftByMonth()
    {
        var r = Context.From<S4DpRow>().Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("month", 1, x.Dt!.Value)).Single();
        r.Should().Be(new DateTime(2023, 5, 5, 6, 7, 8));
    }

    [SqlTable("s4_dp_row")]
    public sealed class S4DpRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("dt")]
        public DateTime? Dt { get; set; }
    }
}

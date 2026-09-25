using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public enum ProbeState
{
    Unknown,
    Active,
    Closed,
}

public sealed class DateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
{
    public override string? ConvertToProvider(DateTimeOffset model) => model.ToString("O");

    public override DateTimeOffset ConvertFromProvider(string? provider) => DateTimeOffset.Parse(provider!, System.Globalization.CultureInfo.InvariantCulture);
}

public abstract partial class CommonTestSuite
{
    [SqlTable("value_converter_probe")]
    public interface IValueConverterProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("state")]
        [ValueConverter(typeof(EnumToStringConverter<ProbeState>))]
        ProbeState State { get; set; }

        [Column("at")]
        [ValueConverter(typeof(DateTimeOffsetConverter))]
        DateTimeOffset? At { get; set; }
    }

    public sealed class ValueConverterProbe : IValueConverterProbe
    {
        public long Id { get; set; }
        public ProbeState State { get; set; }
        public DateTimeOffset? At { get; set; }
    }

    internal static string ProbeTextType(ISqlDialect dialect)
    {
        var name = dialect.GetType().Name;
        if (name.Contains("ClickHouse", StringComparison.Ordinal)) return "String";
        if (name.Contains("MySql", StringComparison.Ordinal) || name.Contains("MariaDb", StringComparison.Ordinal)) return "longtext";
        if (name.Contains("SqlServer", StringComparison.Ordinal)) return "nvarchar(max)";
        if (name.Contains("Postgres", StringComparison.Ordinal)) return "text";
        return "TEXT";
    }

    [Fact]
    public void ValueConverters_ShouldRoundTrip()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var textType = ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        ExecuteValueConverters(ctx, $"create table value_converter_probe (id bigint, state {textType}, at {textType}){engine}");

        try
        {
            var at = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(3));
            ctx.InsertInto<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 1, State = ProbeState.Active, At = at })
                .Insert();
            ctx.InsertInto<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 2, State = ProbeState.Closed, At = null })
                .Insert();

            var first = ctx.From<ValueConverterProbe>().Where(x => x.Id == 1).ToList().Single();
            first.State.Should().Be(ProbeState.Active);
            first.At.Should().Be(at);

            var second = ctx.From<ValueConverterProbe>().Where(x => x.Id == 2).ToList().Single();
            second.State.Should().Be(ProbeState.Closed);
            second.At.Should().BeNull();

            // A constant in a SET list is converted through the mapped property, like an INSERT value.
            ctx.Update<IValueConverterProbe>()
                .Set(x => x.State, ProbeState.Active)
                .Set(x => x.At, DateTimeOffset.UnixEpoch)
                .Where(x => x.Id == 2)
                .Update();

            var updated = ctx.From<ValueConverterProbe>().Where(x => x.Id == 2).ToList().Single();
            updated.State.Should().Be(ProbeState.Active);
            updated.At.Should().Be(DateTimeOffset.UnixEpoch);

            // Update-by-entity writes the same converted provider values.
            ctx.Update(new ValueConverterProbe { Id = 1, State = ProbeState.Closed, At = null });
            ctx.From<ValueConverterProbe>().Where(x => x.Id == 1).ToList().Single().State.Should().Be(ProbeState.Closed);
        }
        finally
        {
            ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        }
    }

    [Fact]
    public void ValueConverters_Queries_ShouldConvertConstantsAndProjections()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var textType = ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        ExecuteValueConverters(ctx, $"create table value_converter_probe (id bigint, state {textType}, at {textType}){engine}");

        try
        {
            var at = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(3));
            ctx.InsertInto<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 1, State = ProbeState.Active, At = at })
                .Insert();
            ctx.InsertInto<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 2, State = ProbeState.Closed, At = null })
                .Insert();

            // A constant in a comparison is converted like an insert value.
            ctx.From<ValueConverterProbe>().Where(x => x.State == ProbeState.Active).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(1);

            var active = ProbeState.Active;
            ctx.From<ValueConverterProbe>().Where(x => x.State == active).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(1);

            // A value list is converted element-wise.
            ctx.From<ValueConverterProbe>().Where(x => new[] { ProbeState.Active, ProbeState.Closed }.Contains(x.State)).ToList()
                .Should().HaveCount(2);

            // A converted constant that is not an enum.
            ctx.From<ValueConverterProbe>().Where(x => x.At == at).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(1);

            // A scalar projection materializes the model value, not the provider value.
            ctx.From<ValueConverterProbe>().Where(x => x.Id == 1).Select(x => x.State).ToList()
                .Should().Equal(ProbeState.Active);

            // An anonymous projection carries the same conversion.
            ctx.From<ValueConverterProbe>().Where(x => x.Id == 1).Select(x => new { x.State }).ToList()
                .Should().ContainSingle().Which.State.Should().Be(ProbeState.Active);

            // A compound parameter-free value (a + b) is converted once, as a single value.
            var one = 1;
            ctx.From<ValueConverterProbe>().Where(x => (int)x.State == one + 0).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(1);

            // A column-dependent CASE converts its branch values while leaving its test alone.
            ctx.From<ValueConverterProbe>().Where(x => x.State == (x.Id == 1 ? ProbeState.Active : ProbeState.Unknown)).ToList()
                .Should().ContainSingle().Which.Id.Should().Be(1);

            // A grouping key of a converted property materializes the model value.
            var groups = ctx.From<ValueConverterProbe>().GroupBy(x => x.State)
                .Select(x => new { x.State, Count = SqlFunctions.Sql.count() }).ToList();
            groups.Should().HaveCount(2);
            groups.Should().Contain(g => g.State == ProbeState.Active);
            groups.Should().Contain(g => g.State == ProbeState.Closed);
        }
        finally
        {
            ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        }
    }

    [Fact]
    public void ValueConverters_Returning_ShouldMaterializeConvertedValue()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var textType = ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        ExecuteValueConverters(ctx, $"create table value_converter_probe (id bigint, state {textType}, at {textType}){engine}");

        try
        {
            var row = ctx.InsertInto<ValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 10, State = ProbeState.Active, At = null })
                .Returning()
                .Single();

            row.State.Should().Be(ProbeState.Active);
        }
        finally
        {
            ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        }
    }

    [Fact]
    public void ValueConverters_CachedPlan_ShouldRefreshConditionalTestParam()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var textType = ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        ExecuteValueConverters(ctx, $"create table value_converter_probe (id bigint, state {textType}, at {textType}){engine}");

        try
        {
            ctx.InsertInto<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 1, State = ProbeState.Active, At = null })
                .Insert();
            ctx.InsertInto<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 2, State = ProbeState.Closed, At = null })
                .Insert();

            var threshold = 0L;
            var below = ctx.From<ValueConverterProbe>()
                .Where(x => x.State == (x.Id > threshold ? ProbeState.Closed : ProbeState.Active)).ToList();
            below.Should().ContainSingle().Which.Id.Should().Be(2);

            // Reusing the cached plan with a new captured value must refresh the test parameter as a raw
            // number; converting it through the State converter would bind a string and break the row.
            threshold = 10L;
            var above = ctx.From<ValueConverterProbe>()
                .Where(x => x.State == (x.Id > threshold ? ProbeState.Closed : ProbeState.Active)).ToList();
            above.Should().ContainSingle().Which.Id.Should().Be(1);
        }
        finally
        {
            ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        }
    }

    private static void ExecuteValueConverters(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

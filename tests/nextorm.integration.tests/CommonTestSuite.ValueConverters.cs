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

public sealed class ProbeStateConverter : ValueConverter<ProbeState, string>
{
    public override string? ConvertToProvider(ProbeState model) => model.ToString();

    public override ProbeState ConvertFromProvider(string? provider) => Enum.Parse<ProbeState>(provider!);
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
        [ValueConverter(typeof(ProbeStateConverter))]
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

    private static void ExecuteValueConverters(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class JsonNumericReaderBindingTests : ProviderTestSuite
{
    private const string Table = "json_numeric_reader_binding_probe";
    private const string ExpectedSixFields = """[{"Small":1,"Integer":2,"Big":3,"Real":4,"Double":5,"Numeric":6}]""";
    private const string ExpectedDecimalFields = """[{"Small":1,"Integer":2,"Big":3,"Numeric":6}]""";
    private const string ExpectedNullableScalar = "[1,null]";

    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    // These mappings vary the projected CLR target over the same physical fields without SQL casts,
    // leaving the reader's PostgreSQL numeric field types intact.
    [SqlTable(Table)]
    public interface IByteNumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        byte Small { get; set; }

        [Column("int_value")]
        byte Integer { get; set; }

        [Column("big_value")]
        byte Big { get; set; }

        [Column("real_value")]
        byte Real { get; set; }

        [Column("double_value")]
        byte Double { get; set; }

        [Column("numeric_value")]
        byte Numeric { get; set; }
    }

    [SqlTable(Table)]
    public interface IInt16NumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        short Small { get; set; }

        [Column("int_value")]
        short Integer { get; set; }

        [Column("big_value")]
        short Big { get; set; }

        [Column("real_value")]
        short Real { get; set; }

        [Column("double_value")]
        short Double { get; set; }

        [Column("numeric_value")]
        short Numeric { get; set; }
    }

    [SqlTable(Table)]
    public interface IInt32NumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        int Small { get; set; }

        [Column("int_value")]
        int Integer { get; set; }

        [Column("big_value")]
        int Big { get; set; }

        [Column("real_value")]
        int Real { get; set; }

        [Column("double_value")]
        int Double { get; set; }

        [Column("numeric_value")]
        int Numeric { get; set; }
    }

    [SqlTable(Table)]
    public interface IInt64NumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        long Small { get; set; }

        [Column("int_value")]
        long Integer { get; set; }

        [Column("big_value")]
        long Big { get; set; }

        [Column("real_value")]
        long Real { get; set; }

        [Column("double_value")]
        long Double { get; set; }

        [Column("numeric_value")]
        long Numeric { get; set; }
    }

    [SqlTable(Table)]
    public interface ISingleNumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        float Small { get; set; }

        [Column("int_value")]
        float Integer { get; set; }

        [Column("big_value")]
        float Big { get; set; }

        [Column("real_value")]
        float Real { get; set; }

        [Column("double_value")]
        float Double { get; set; }

        [Column("numeric_value")]
        float Numeric { get; set; }
    }

    [SqlTable(Table)]
    public interface IDoubleNumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        double Small { get; set; }

        [Column("int_value")]
        double Integer { get; set; }

        [Column("big_value")]
        double Big { get; set; }

        [Column("real_value")]
        double Real { get; set; }

        [Column("double_value")]
        double Double { get; set; }

        [Column("numeric_value")]
        double Numeric { get; set; }
    }

    [SqlTable(Table)]
    public interface IDecimalNumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        decimal Small { get; set; }

        [Column("int_value")]
        decimal Integer { get; set; }

        [Column("big_value")]
        decimal Big { get; set; }

        [Column("real_value")]
        decimal Real { get; set; }

        [Column("double_value")]
        decimal Double { get; set; }

        [Column("numeric_value")]
        decimal Numeric { get; set; }
    }

    [SqlTable(Table)]
    public interface INullableInt32NumericRow
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("small_value")]
        int? Value { get; set; }
    }

    [Fact]
    public void WriteJson_ByteTargetAcrossPostgresNumericFields_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<IByteNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric }),
            ExpectedSixFields));

    [Fact]
    public void WriteJson_Int16TargetAcrossPostgresNumericFields_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<IInt16NumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric }),
            ExpectedSixFields));

    [Fact]
    public void WriteJson_Int32TargetAcrossPostgresNumericFields_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<IInt32NumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric }),
            ExpectedSixFields));

    [Fact]
    public void WriteJson_Int64TargetAcrossPostgresNumericFields_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<IInt64NumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric }),
            ExpectedSixFields));

    [Fact]
    public void WriteJson_SingleTargetAcrossPostgresNumericFields_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<ISingleNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric }),
            ExpectedSixFields));

    [Fact]
    public void WriteJson_DoubleTargetAcrossPostgresNumericFields_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<IDoubleNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric }),
            ExpectedSixFields));

    [Fact]
    public void WriteJson_DecimalTargetForSupportedPostgresNumericFields_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<IDecimalNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Numeric }),
            ExpectedDecimalFields));

    [Fact]
    public void WriteJson_DecimalTargetForPostgresRealField_ShouldFailLikeMaterialization()
        => WithTable(context =>
        {
            var command = context.From<IDecimalNumericRow>().Where(x => x.Id == 1).Select(x => x.Real);
            AssertDecimalReadFails(command);
        });

    [Fact]
    public void WriteJson_DecimalTargetForPostgresDoubleField_ShouldFailLikeMaterialization()
        => WithTable(context =>
        {
            var command = context.From<IDecimalNumericRow>().Where(x => x.Id == 1).Select(x => x.Double);
            AssertDecimalReadFails(command);
        });

    [Fact]
    public void WriteJson_NullableScalar_ShouldMatchExpectedAndMaterialization()
        => WithTable(context => AssertJsonMatchesMaterialized(
            context.From<INullableInt32NumericRow>().OrderBy(x => x.Id).Select(x => x.Value),
            ExpectedNullableScalar));

    private void WithTable(Action<IDataContext> test)
    {
        var context = _sut.DataProvider;
        try
        {
            Execute(context, $"drop table if exists {Table}");
            Execute(
                context,
                $"create table {Table} (" +
                "id bigint primary key, " +
                "small_value smallint null, " +
                "int_value integer not null, " +
                "big_value bigint not null, " +
                "real_value real not null, " +
                "double_value double precision not null, " +
                "numeric_value numeric not null)");
            Execute(
                context,
                $"insert into {Table} " +
                "(id, small_value, int_value, big_value, real_value, double_value, numeric_value) values " +
                "(1, 1, 2, 3, 4, 5, 6), (2, null, 2, 3, 4, 5, 6)");

            test(context);
        }
        finally
        {
            Execute(context, $"drop table if exists {Table}");
        }
    }

    private static void AssertJsonMatchesMaterialized<TResult>(QueryCommand<TResult> command, string expected)
    {
        JsonSerializer.Serialize(command.ToList()).Should().Be(expected);
        using var buffer = new MemoryStream();
        command.WriteJson(buffer);

        Encoding.UTF8.GetString(buffer.ToArray()).Should().Be(expected);
    }

    private static void AssertDecimalReadFails(QueryCommand<decimal> command)
    {
        FluentActions.Invoking(() =>
        {
            _ = command.ToList();
        })
            .Should().Throw<InvalidCastException>();
        FluentActions.Invoking(() =>
        {
            using var buffer = new MemoryStream();
            command.WriteJson(buffer);
        }).Should().Throw<InvalidCastException>();
    }

    private static void Execute(IDataContext context, string sql)
    {
        ((DataContext)context).EnsureConnectionOpen();
        using var command = ((DataContext)context).CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}

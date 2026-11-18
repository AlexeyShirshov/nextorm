using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("ClickHouse")]
public sealed class ClickHouseJsonNumericReaderBindingTests : ProviderTestSuite
{
    private const string Table = "json_numeric_reader_binding_probe";
    private const string Expected = """[{"Small":1,"Integer":2,"Big":3,"Real":4,"Double":5,"Numeric":6}]""";

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void WriteJson_ByteTargetAcrossClickHouseNumericFields_ShouldMatchExpected()
        => WithTable(context => AssertJson(
            context.From<JsonNumericReaderBindingTests.IByteNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric })));

    [Fact]
    public void WriteJson_Int16TargetAcrossClickHouseNumericFields_ShouldMatchExpected()
        => WithTable(context => AssertJson(
            context.From<JsonNumericReaderBindingTests.IInt16NumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric })));

    [Fact]
    public void WriteJson_Int32TargetAcrossClickHouseNumericFields_ShouldMatchExpected()
        => WithTable(context => AssertJson(
            context.From<JsonNumericReaderBindingTests.IInt32NumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric })));

    [Fact]
    public void WriteJson_Int64TargetAcrossClickHouseNumericFields_ShouldMatchExpected()
        => WithTable(context => AssertJson(
            context.From<JsonNumericReaderBindingTests.IInt64NumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric })));

    [Fact]
    public void WriteJson_SingleTargetAcrossClickHouseNumericFields_ShouldMatchExpected()
        => WithTable(context => AssertJson(
            context.From<JsonNumericReaderBindingTests.ISingleNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric })));

    [Fact]
    public void WriteJson_DoubleTargetAcrossClickHouseNumericFields_ShouldMatchExpected()
        => WithTable(context => AssertJson(
            context.From<JsonNumericReaderBindingTests.IDoubleNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric })));

    [Fact]
    public void WriteJson_DecimalTargetAcrossClickHouseNumericFields_ShouldMatchExpected()
        => WithTable(context => AssertJson(
            context.From<JsonNumericReaderBindingTests.IDecimalNumericRow>().Where(x => x.Id == 1)
                .Select(x => new { x.Small, x.Integer, x.Big, x.Real, x.Double, x.Numeric }),
            """[{"Small":1,"Integer":2,"Big":3,"Real":4,"Double":5,"Numeric":6.00}]"""));

    private void WithTable(Action<IDataContext> test)
    {
        var context = _sut.DataProvider;
        try
        {
            Execute(context, $"drop table if exists {Table}");
            Execute(
                context,
                $"create table {Table} (" +
                "id Int64, " +
                "small_value UInt8, " +
                "int_value Int8, " +
                "big_value UInt16, " +
                "real_value UInt32, " +
                "double_value UInt64, " +
                "numeric_value Decimal64(2)) ENGINE = Memory");
            Execute(
                context,
                $"insert into {Table} " +
                "(id, small_value, int_value, big_value, real_value, double_value, numeric_value) " +
                "values (1, 1, 2, 3, 4, 5, 6)");

            test(context);
        }
        finally
        {
            Execute(context, $"drop table if exists {Table}");
        }
    }

    private static void AssertJson<TResult>(QueryCommand<TResult> command, string expected = Expected)
    {
        using var buffer = new MemoryStream();
        command.WriteJson(buffer);

        Encoding.UTF8.GetString(buffer.ToArray()).Should().Be(expected);
    }

    private static void Execute(IDataContext context, string sql)
    {
        ((DataContext)context).EnsureConnectionOpen();
        using var command = ((DataContext)context).CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}

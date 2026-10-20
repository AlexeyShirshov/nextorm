using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server specific surface, second sweep: the remaining T-SQL scalar/metadata functions
/// (identity, sequence, time-zone and compression forms), the three-part name qualification and
/// the temporal/format renderers that the first sweep did not reach.
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerExtendedFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Fact]
    public void IdentityAndSequenceFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                IdentIncr = SqlFunctions.SqlServer.ident_incr("complex_entity"),
                IdentSeed = SqlFunctions.SqlServer.ident_seed("complex_entity"),
                IndexCol = SqlFunctions.SqlServer.index_col("simple_entity", 1, 1),
                ObjectDefinition = SqlFunctions.SqlServer.object_definition(SqlFunctions.SqlServer.object_id("simple_entity")),
                ObjectSchema = SqlFunctions.SqlServer.object_schema_name(SqlFunctions.SqlServer.object_id("simple_entity")),
                Publishing = SqlFunctions.SqlServer.publishingservername(),
                OriginalDb = SqlFunctions.SqlServer.original_db_name(),
                Suser = SqlFunctions.SqlServer.suser_name()
            })
            .First();

        r.ObjectSchema.Should().Be("dbo");
        r.Publishing.Should().NotBeNull();
        r.OriginalDb.Should().NotBeNullOrEmpty();
        r.Suser.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CompressionAndTimeZoneFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Compressed = SqlFunctions.SqlServer.compress("abc"),
                RoundTrip = SqlFunctions.SqlServer.decompress(SqlFunctions.SqlServer.compress("abc")),
                Offset = SqlFunctions.SqlServer.todatetimeoffset(new DateTime(2023, 5, 17, 12, 0, 0), 120),
                Shifted = SqlFunctions.SqlServer.switchoffset(
                    SqlFunctions.SqlServer.todatetimeoffset(new DateTime(2023, 5, 17, 12, 0, 0), 120),
                    60),
                OffsetParts = SqlFunctions.SqlServer.datetimeoffsetfromparts(2023, 5, 17, 12, 0, 0, 0, 1, 0, 0)
            })
            .First();

        r.Compressed.Should().NotBeEmpty();
        r.RoundTrip.Should().Equal(System.Text.Encoding.UTF8.GetBytes("abc"));
        r.Offset!.Value.Offset.Should().Be(TimeSpan.FromMinutes(120));
        r.Shifted!.Value.Offset.Should().Be(TimeSpan.FromMinutes(60));
        r.OffsetParts!.Value.Offset.Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void ThreePartNames_ShouldQualify()
    {
        var server = _sut.SimpleEntity.WithServer(".").Select(x => x.Id).ToList();
        server.Should().HaveCount(10);

        var schema = _sut.ComplexEntity.WithSchema("dbo").Select(x => x.Id).ToList();
        schema.Should().HaveCount(3);

    }

    [Fact]
    public void TimeZoneFunctions_ShouldExecuteSeparately()
    {
        var id = _sut.SimpleEntity.Where(x => x.Id == 1).Select(x => SqlFunctions.SqlServer.current_timezone()).First();
        id.Should().NotBeNullOrEmpty();

        var name = _sut.SimpleEntity.Where(x => x.Id == 1).Select(x => SqlFunctions.SqlServer.current_timezone()).First();
        name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FormatMessageAndAnsiNulls_ShouldExecute()
    {
        var format = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Format = SqlFunctions.SqlServer.format(1234.5, "N2"),
                FormatCulture = SqlFunctions.SqlServer.format(1234.5, "N2", "en-US"),
                AnsiiNull = SqlFunctions.SqlServer.getansinull(),
                AnsiiNullDb = SqlFunctions.SqlServer.getansinull("master")
            })
            .First();

        format.Format.Should().Be("1,234.50");
        format.FormatCulture.Should().Be("1,234.50");
        format.AnsiiNull.Should().Be(1);
        format.AnsiiNullDb.Should().Be(1);
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests.T282;

/// <summary>
/// T282 (#282): native reconnaissance for the SQL Server <c>PARSE</c>/<c>TRY_PARSE</c> decimal surface.
/// The probes execute the exact frozen <c>CASE … PARSE(… USING N'en-US')</c> form, prove that the
/// culture is the literal <c>en-US</c> and not the session language, and pin the failure identity of
/// invalid text (<see cref="SqlException"/> for <c>PARSE</c>, NULL for <c>TRY_PARSE</c>).
/// </summary>
[Collection("T282 builtin catalog")]
public sealed class SqlServerParsingExecutionTests : IDisposable
{
    private readonly SqlServerDataContext? _context;

    public SqlServerParsingExecutionTests()
    {
        if (ProviderSkipPolicy.IsOptedOut("sqlserver"))
            Assert.Skip($"Provider 'sqlserver' is explicitly opted out: {SqlServerTestProvider.Instance.SkipReason}");

        SqlServerTestProvider.Instance.IsAvailable.Should().BeTrue(
            $"provider 'sqlserver' is unavailable ({SqlServerTestProvider.Instance.SkipReason})");

        _context = new SqlServerDataContext(SqlServerContainer.ConnectionString, new DataContextBuilder());
    }

    public void Dispose() => _context?.Dispose();

    private object? Scalar(string sql)
    {
        _context!.EnsureConnectionOpen();
        using var command = _context.CreateCommand(sql);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    /// <summary>D1: the frozen decimal <c>en-US</c> form and its failure identity on the live server.</summary>
    [Fact]
    public void T282_Recon_NativeDecimalParsing()
    {
        Scalar("select PARSE('1234.50' AS decimal(38,18) USING N'en-US')").Should().Be(1234.50m);
        Scalar("select TRY_PARSE('1234.50' AS decimal(38,18) USING N'en-US')").Should().Be(1234.50m);
        Scalar("select TRY_PARSE('bad' AS decimal(38,18) USING N'en-US')").Should().BeNull();
        Scalar("select CASE WHEN CAST(NULL AS nvarchar(4000)) IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE PARSE(CAST(NULL AS nvarchar(4000)) AS decimal(38,18) USING N'en-US') END").Should().BeNull();

        // The literal en-US culture wins over the session language.
        Scalar("set language German").Should().BeNull();
        Scalar("select PARSE('1234.50' AS decimal(38,18) USING N'en-US')").Should().Be(1234.50m);

        var invalid = () => Scalar("select PARSE('bad' AS decimal(38,18) USING N'en-US')");
        invalid.Should().Throw<SqlException>();
    }

    private void SeedParsingFixture()
    {
        foreach (var sql in new[]
        {
            "drop table if exists t282_parse",
            "create table t282_parse (id int not null primary key, txt nvarchar(100) null)",
            "insert into t282_parse (id, txt) values (1, N'1234.50'), (2, N'bad'), (3, null)"
        })
        {
            _context!.EnsureConnectionOpen();
            using var command = _context.CreateCommand(sql);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// D6: the public <see cref="SqlServerFunctions.parse"/> returns <see cref="decimal"/> values with
    /// the fixed <c>en-US</c> culture and NULL for a NULL argument or nullable column.
    /// </summary>
    [Fact]
    public void T282_Parse_FixedEnUsNullAndClrType()
    {
        SeedParsingFixture();

        var rows = _context!.From<T282ParseRow>().Where(x => x.Id != 2).OrderBy(x => x.Id).Select(x => new
        {
            x.Id,
            Parsed = SqlFunctions.SqlServer.parse(x.Text),
            Literal = SqlFunctions.SqlServer.parse("1234.50"),
            NullLiteral = SqlFunctions.SqlServer.parse(null)
        }).ToList();

        rows.Should().HaveCount(2);
        rows[0].Parsed.Should().Be(1234.50m);
        rows[0].Parsed.Should().BeOfType(typeof(decimal));
        rows[0].Literal.Should().Be(1234.50m);
        rows[0].NullLiteral.Should().BeNull();
        rows[1].Id.Should().Be(3);
        rows[1].Parsed.Should().BeNull();
    }

    /// <summary>D6: invalid decimal text raises the server error for <c>PARSE</c>, never a local fallback.</summary>
    [Fact]
    public void T282_Parse_InvalidTextThrows()
    {
        SeedParsingFixture();

        var act = () => _context!.From<T282ParseRow>()
            .Where(x => x.Id == 2)
            .Select(x => new { Parsed = SqlFunctions.SqlServer.parse(x.Text) })
            .ToList();

        act.Should().Throw<SqlException>();
    }

    /// <summary>D6: <c>TRY_PARSE</c> returns NULL for invalid text and the value for valid text.</summary>
    [Fact]
    public void T282_TryParse_InvalidTextReturnsNull()
    {
        SeedParsingFixture();

        var rows = _context!.From<T282ParseRow>().OrderBy(x => x.Id).Select(x => new
        {
            x.Id,
            Parsed = SqlFunctions.SqlServer.try_parse(x.Text)
        }).ToList();

        rows.Should().HaveCount(3);
        rows[0].Parsed.Should().Be(1234.50m);
        rows[1].Parsed.Should().BeNull();
        rows[2].Parsed.Should().BeNull();
        rows[0].Parsed.Should().BeOfType(typeof(decimal));
    }
}

/// <summary>The T282 probe table for the SQL Server decimal parsing live tests.</summary>
[SqlTable("t282_parse")]
public sealed class T282ParseRow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("txt")]
    public string? Text { get; set; }
}

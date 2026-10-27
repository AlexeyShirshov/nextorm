using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T348 (#282) U1: the four cross-seam exemplars (<c>regexp_count</c>, <c>left_shift</c>,
/// <c>product</c>, <c>regexp_split_to_table</c>), the version-only gate matrix and the refusal
/// diagnostics, all without a server. The declared major version is the only input.
/// </summary>
public class T348VersionedExemplarsSqlGenerationTests
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // A context type has one immutable declaration (the plan cache is keyed by it), so each declared
    // state gets its own type, exactly as the live T326 gate tests do.
    private sealed class V15Context(string connectionString)
        : SqlServerDataContext(new Version(15, 0), connectionString);

    private sealed class V16Context(string connectionString)
        : SqlServerDataContext(new Version(16, 0), connectionString);

    private sealed class V17Context(string connectionString)
        : SqlServerDataContext(new Version(17, 0), connectionString);

    private sealed class V18Context(string connectionString)
        : SqlServerDataContext(new Version(18, 0), connectionString);

    private sealed class V12AzureContext(string connectionString)
        : SqlServerDataContext(new Version(12, 0), SqlServerEngineEdition.SqlDatabase, connectionString);

    private sealed class AzureOnlyContext(string connectionString)
        : SqlServerDataContext(SqlServerEngineEdition.SqlDatabase, connectionString);

    private sealed class UnversionedContext(string connectionString)
        : SqlServerDataContext(connectionString, new DataContextBuilder());

    private static SqlServerDataContext Declared(int major) => major switch
    {
        15 => new V15Context(ConnectionString),
        16 => new V16Context(ConnectionString),
        17 => new V17Context(ConnectionString),
        18 => new V18Context(ConnectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(major), major, "No declared test context.")
    };

    private static SqlServerDataContext Declared(int major, SqlServerEngineEdition edition) =>
        major == 12 && edition == SqlServerEngineEdition.SqlDatabase
            ? new V12AzureContext(ConnectionString)
            : throw new ArgumentOutOfRangeException(nameof(major), major, "No declared test context.");

    private static SqlServerDataContext EditionOnly(SqlServerEngineEdition edition) =>
        edition == SqlServerEngineEdition.SqlDatabase
            ? new AzureOnlyContext(ConnectionString)
            : throw new ArgumentOutOfRangeException(nameof(edition), edition, "No edition-only test context.");

    private static SqlServerDataContext Unversioned() => new UnversionedContext(ConnectionString);

    [Theory]
    [InlineData(15, false, false, false)]
    [InlineData(16, false, true, false)]
    [InlineData(17, true, true, true)]
    [InlineData(18, true, true, true)]
    public void DeclaredMajor_ShouldOpenExactlyTheVersionGates(int major, bool functions2025, bool functions2022, bool product)
    {
        using var ctx = Declared(major);

        ctx.Dialect.SupportsSqlServer2025Functions.Should().Be(functions2025);
        ctx.Dialect.SupportsSqlServer2022Functions.Should().Be(functions2022);
        ctx.Dialect.SupportsProductAggregate.Should().Be(product);
    }

    [Fact]
    public void Unversioned_ShouldKeepEveryVersionGateClosed()
    {
        using var ctx = Unversioned();

        ctx.Dialect.SupportsSqlServer2025Functions.Should().BeFalse("an unversioned dialect assumes no version");
        ctx.Dialect.SupportsSqlServer2022Functions.Should().BeFalse("an unversioned dialect assumes no version");
        ctx.Dialect.SupportsProductAggregate.Should().BeFalse("PRODUCT follows the 2025 gate");
        ctx.Dialect.SupportsJson.Should().BeFalse("the native json gate was not touched");
    }

    [Fact]
    public void EditionOnly_ShouldNeverEnableTheVersionGatedNames()
    {
        using var ctx = EditionOnly(SqlServerEngineEdition.SqlDatabase);

        ctx.Dialect.SupportsSqlServer2025Functions.Should().BeFalse("no edition substitutes for a declared major version");
        ctx.Dialect.SupportsSqlServer2022Functions.Should().BeFalse("no edition substitutes for a declared major version");
        ctx.Dialect.SupportsProductAggregate.Should().BeFalse();
        ctx.Dialect.SupportsJson.Should().BeTrue("the #345 edition allowlist for the native json type is unchanged");
    }

    [Fact]
    public void AzureSqlDatabaseDeclaredAtTwelve_ShouldStillRefuseThe2025Names()
    {
        using var ctx = Declared(12, SqlServerEngineEdition.SqlDatabase);

        ctx.Dialect.SupportsJson.Should().BeTrue("the #345 edition allowlist for the native json type is unchanged");
        ctx.Dialect.SupportsSqlServer2025Functions.Should().BeFalse("major 12 cannot have the 2025 functions");
        ctx.Dialect.SupportsSqlServer2022Functions.Should().BeFalse("major 12 cannot have the 2022 functions");
        ctx.Dialect.SupportsProductAggregate.Should().BeFalse();
    }

    [Fact]
    public void DeclaredSeventeen_ShouldRenderTheFourExemplars()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var scalar = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            C = SqlFunctions.SqlServer.regexp_count(x.String, "[0-9]"),
            L = SqlFunctions.SqlServer.left_shift(x.Int, 1),
            P = SqlFunctions.SqlServer.product(x.Int)
        }));

        scalar.Should().Contain("regexp_count(somestring, N'[0-9]')");
        scalar.Should().Contain(
            "CASE WHEN nullableint IS NULL OR 1 IS NULL THEN CAST(NULL AS int) "
            + "ELSE left_shift(cast(nullableint as int), cast(1 as bigint)) END");
        scalar.Should().Contain("product(nullableint)");

        var tvf = SqlOf(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_split_to_table("a,b,c", ","))
            .Select(r => new { r.Value, r.Ordinal }));

        tvf.Should().Contain("regexp_split_to_table(N'a,b,c', N',')");
        tvf.Should().Contain("value");
        tvf.Should().Contain("ordinal");
    }

    [Fact]
    public void LeftShift_ShouldTypeBothArgumentsAndTheNullBranches()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var column = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(x.Int, 2) }));
        column.Should().Contain("left_shift(cast(nullableint as int), cast(2 as bigint))");
        column.Should().Contain("CASE WHEN nullableint IS NULL OR 2 IS NULL THEN CAST(NULL AS int)");

        var nullOperand = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(null, 1) }));
        nullOperand.Should().Contain("CASE WHEN CAST(NULL AS int) IS NULL OR 1 IS NULL THEN CAST(NULL AS int)");
        nullOperand.Should().Contain("left_shift(cast(CAST(NULL AS int) as int), cast(1 as bigint))");

        var nullShift = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(x.Int, null) }));
        nullShift.Should().Contain("CASE WHEN nullableint IS NULL OR CAST(NULL AS bigint) IS NULL THEN CAST(NULL AS int)");
        nullShift.Should().Contain("left_shift(cast(nullableint as int), cast(CAST(NULL AS bigint) as bigint))");
    }

    [Fact]
    public void DeclaredSixteen_ShouldPermitOnlyLeftShift()
    {
        using var ctx = Declared(16);
        var e = ctx.From<IComplexEntity>();

        var leftShift = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(x.Int, 1) }));
        leftShift.Should().Contain("left_shift(cast(nullableint as int), cast(1 as bigint))");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.regexp_count(x.String, "[0-9]") })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: 16.");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.product(x.Int) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The PRODUCT function requires SQL Server major version 17 or later; declared major version: 16.");

        FluentActions.Invoking(() => SqlOf(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_split_to_table("a,b", ","))
                .Select(r => r.Value)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The REGEXP_SPLIT_TO_TABLE function requires SQL Server major version 17 or later; declared major version: 16.");
    }

    [Fact]
    public void DeclaredFifteen_ShouldRefuseLeftShiftWithItsOwnMinimum()
    {
        using var ctx = Declared(15);
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(x.Int, 1) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The LEFT_SHIFT function requires SQL Server major version 16 or later; declared major version: 15.");
    }

    [Fact]
    public void Unversioned_ShouldRefuseEachExemplarByNameAndSayUnspecified()
    {
        using var ctx = Unversioned();
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.regexp_count(x.String, "p") })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: unspecified.");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(x.Int, 1) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The LEFT_SHIFT function requires SQL Server major version 16 or later; declared major version: unspecified.");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.product(x.Int) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The PRODUCT function requires SQL Server major version 17 or later; declared major version: unspecified.");

        FluentActions.Invoking(() => SqlOf(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_split_to_table("a", ","))
                .Select(r => r.Value)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The REGEXP_SPLIT_TO_TABLE function requires SQL Server major version 17 or later; declared major version: unspecified.");
    }

    [Fact]
    public void AzureSqlDatabaseAtTwelve_ShouldRefuseThe2025NamesWithTheDeclaredMajor()
    {
        using var ctx = Declared(12, SqlServerEngineEdition.SqlDatabase);
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.regexp_count(x.String, "p") })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: 12.");
    }

    [Fact]
    public void T348_Baseline_ShouldBeUnchanged()
    {
        using var ctx = Unversioned();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { P = SqlFunctions.SqlServer.patindex("%a%", x.String) }))
            .Should().Be("select patindex(N'%a%', somestring) as [P]\n from complex_entity\n where id = 1");

        var csv = "a,b";
        var separator = ",";
        SqlOf(ctx, ctx.From(() => SqlFunctions.SqlServer.string_split(csv, separator)).Select(x => x.Value))
            .Should().Be("select value\n from string_split(@csv, @separator) as [t1]");

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",")))
            .Should().Be("select string_agg(somestring, N',')\n from complex_entity");

        SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => x.Int, orderBy: () => x.Id)
        }))
            .Should().Be("select id, row_number() over (partition by nullableint order by id) as [rn]\n from complex_entity");

        SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Parsed = SqlFunctions.SqlServer.parse(x.String),
            ParsedNull = SqlFunctions.SqlServer.parse(null),
            Tried = SqlFunctions.SqlServer.try_parse(x.String),
            TriedNull = SqlFunctions.SqlServer.try_parse(null)
        }))
            .Should().Be(
                "select CASE WHEN somestring IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE PARSE(somestring AS decimal(38,18) USING N'en-US') END as [Parsed], "
                + "CASE WHEN CAST(NULL AS nvarchar(4000)) IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE PARSE(CAST(NULL AS nvarchar(4000)) AS decimal(38,18) USING N'en-US') END as [ParsedNull], "
                + "CASE WHEN somestring IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE TRY_PARSE(somestring AS decimal(38,18) USING N'en-US') END as [Tried], "
                + "CASE WHEN CAST(NULL AS nvarchar(4000)) IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE TRY_PARSE(CAST(NULL AS nvarchar(4000)) AS decimal(38,18) USING N'en-US') END as [TriedNull]\n"
                + " from complex_entity\n where id = 1");

        var crypto = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0)),
            Decoded = SqlFunctions.Sql.base64_decode("AAEC/f7/"),
            Random = SqlFunctions.Sql.crypt_gen_random(16)
        }));
        crypto.Should().Contain("BASE64_ENCODE(@norm_p0)");
        crypto.Should().Contain("BASE64_DECODE(N'AAEC/f7/')");
        crypto.Should().Contain("CASE WHEN 16 IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(16) END");
    }
}

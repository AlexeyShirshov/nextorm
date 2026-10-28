using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Reflection;
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

    private sealed class V14Context(string connectionString)
        : SqlServerDataContext(new Version(14, 0), connectionString);

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
        14 => new V14Context(ConnectionString),
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

        var nullOperand = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift((int?)null, 1) }));
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

    // ---------------------------------------------------------------------------------------------
    // U2 — bit family completion (four operand shapes over all five names)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The operand's own CLR type selects the rendered T-SQL type in every family member: the operand is
    /// cast to <c>tinyint</c>/<c>smallint</c>/<c>int</c>/<c>bigint</c>, the shift amount to <c>bigint</c>
    /// and the bit offset to <c>int</c>, and the NULL branch carries the result type (<c>bigint</c> for
    /// <c>BIT_COUNT</c>, <c>bit</c> for <c>GET_BIT</c>).
    /// </summary>
    [Fact]
    public void BitFamily_ShouldRenderTheOperandShapeOfEveryOverload()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var byteShape = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.SqlServer.left_shift((byte?)x.Int, 1),
            R = SqlFunctions.SqlServer.right_shift((byte?)x.Int, 1),
            C = SqlFunctions.SqlServer.bit_count((byte?)x.Int),
            G = SqlFunctions.SqlServer.get_bit((byte?)x.Int, 2),
            S = SqlFunctions.SqlServer.set_bit((byte?)x.Int, 2)
        }));

        byteShape.Should().Contain(
            "CASE WHEN cast(nullableint as tinyint) IS NULL OR 1 IS NULL THEN CAST(NULL AS tinyint) "
            + "ELSE left_shift(cast(cast(nullableint as tinyint) as tinyint), cast(1 as bigint)) END");
        byteShape.Should().Contain(
            "CASE WHEN cast(nullableint as tinyint) IS NULL OR 1 IS NULL THEN CAST(NULL AS tinyint) "
            + "ELSE right_shift(cast(cast(nullableint as tinyint) as tinyint), cast(1 as bigint)) END");
        byteShape.Should().Contain(
            "CASE WHEN cast(nullableint as tinyint) IS NULL THEN CAST(NULL AS bigint) "
            + "ELSE bit_count(cast(cast(nullableint as tinyint) as tinyint)) END");
        byteShape.Should().Contain(
            "CASE WHEN cast(nullableint as tinyint) IS NULL OR 2 IS NULL THEN CAST(NULL AS bit) "
            + "ELSE get_bit(cast(cast(nullableint as tinyint) as tinyint), cast(2 as int)) END");
        byteShape.Should().Contain(
            "CASE WHEN cast(nullableint as tinyint) IS NULL OR 2 IS NULL THEN CAST(NULL AS tinyint) "
            + "ELSE set_bit(cast(cast(nullableint as tinyint) as tinyint), cast(2 as int)) END");

        var shortShape = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.SqlServer.left_shift((short?)x.Int, 1),
            R = SqlFunctions.SqlServer.right_shift((short?)x.Int, 1),
            C = SqlFunctions.SqlServer.bit_count((short?)x.Int),
            G = SqlFunctions.SqlServer.get_bit((short?)x.Int, 2),
            S = SqlFunctions.SqlServer.set_bit((short?)x.Int, 2)
        }));

        shortShape.Should().Contain("cast(cast(nullableint as smallint) as smallint)");
        shortShape.Should().Contain("CAST(NULL AS smallint)");

        var intShape = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.SqlServer.left_shift(x.Int, 1),
            R = SqlFunctions.SqlServer.right_shift(x.Int, 1),
            C = SqlFunctions.SqlServer.bit_count(x.Int),
            G = SqlFunctions.SqlServer.get_bit(x.Int, 2),
            S = SqlFunctions.SqlServer.set_bit(x.Int, 2)
        }));

        intShape.Should().Contain("left_shift(cast(nullableint as int), cast(1 as bigint))");
        intShape.Should().Contain("right_shift(cast(nullableint as int), cast(1 as bigint))");
        intShape.Should().Contain("bit_count(cast(nullableint as int))");
        intShape.Should().Contain("get_bit(cast(nullableint as int), cast(2 as int))");
        intShape.Should().Contain("set_bit(cast(nullableint as int), cast(2 as int))");
        intShape.Should().Contain("CAST(NULL AS int)");

        // Id is a non-nullable long column, so the long overload is selected without a conversion node.
        var longShape = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.SqlServer.left_shift(x.Id, 1),
            R = SqlFunctions.SqlServer.right_shift(x.Id, 1),
            C = SqlFunctions.SqlServer.bit_count(x.Id),
            G = SqlFunctions.SqlServer.get_bit(x.Id, 2),
            S = SqlFunctions.SqlServer.set_bit(x.Id, 2)
        }));

        longShape.Should().Contain("left_shift(cast(id as bigint), cast(1 as bigint))");
        longShape.Should().Contain("right_shift(cast(id as bigint), cast(1 as bigint))");
        longShape.Should().Contain("bit_count(cast(id as bigint))");
        longShape.Should().Contain("get_bit(cast(id as bigint), cast(2 as int))");
        longShape.Should().Contain("set_bit(cast(id as bigint), cast(2 as int))");
        longShape.Should().Contain("CAST(NULL AS bigint)");
    }

    /// <summary>
    /// The three-argument <c>SET_BIT</c> types the value as <c>bit</c>; an omitted value keeps the native
    /// default (1) and an explicitly NULL value is guarded into a NULL result, not the default.
    /// </summary>
    [Fact]
    public void SetBit_ShouldDistinguishOmittedFromSuppliedAndNullValue()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            On = SqlFunctions.SqlServer.set_bit(x.Int, 2, true),
            Off = SqlFunctions.SqlServer.set_bit(x.Int, 2, false),
            Column = SqlFunctions.SqlServer.set_bit(x.Int, 2, x.Boolean),
            Null = SqlFunctions.SqlServer.set_bit(x.Int, 2, null),
            Omitted = SqlFunctions.SqlServer.set_bit(x.Int, 2)
        }));

        sql.Should().Contain("ELSE set_bit(cast(nullableint as int), cast(2 as int), cast(1 as bit)) END");
        sql.Should().Contain("ELSE set_bit(cast(nullableint as int), cast(2 as int), cast(0 as bit)) END");
        sql.Should().Contain("ELSE set_bit(cast(nullableint as int), cast(2 as int), cast(b as bit)) END");
        sql.Should().Contain("CASE WHEN nullableint IS NULL OR 2 IS NULL OR CAST(NULL AS bit) IS NULL THEN CAST(NULL AS int)");
        sql.Should().Contain("CASE WHEN nullableint IS NULL OR 2 IS NULL THEN CAST(NULL AS int) ELSE set_bit(cast(nullableint as int), cast(2 as int)) END");
    }

    /// <summary>
    /// Negative shifts and the edge offsets stay native calls: the renderer types the arguments but never
    /// rewrites the value, so an out-of-range offset is the engine's error, not a silent success.
    /// </summary>
    [Fact]
    public void BitFamily_ShouldLeaveNegativeShiftsAndEdgeOffsetsToTheEngine()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Negative = SqlFunctions.SqlServer.left_shift((int?)-8, 1),
            RightNegative = SqlFunctions.SqlServer.right_shift((int?)-8, 1),
            LongShift = SqlFunctions.SqlServer.left_shift(x.Id, 63),
            EdgeOffset = SqlFunctions.SqlServer.get_bit(x.Int, 31),
            WideOffset = SqlFunctions.SqlServer.get_bit(x.Id, 63)
        }));

        sql.Should().Contain("left_shift(cast(-8 as int), cast(1 as bigint))");
        sql.Should().Contain("right_shift(cast(-8 as int), cast(1 as bigint))");
        sql.Should().Contain("left_shift(cast(id as bigint), cast(63 as bigint))");
        sql.Should().Contain("get_bit(cast(nullableint as int), cast(31 as int))");
        sql.Should().Contain("get_bit(cast(id as bigint), cast(63 as int))");
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void DeclaredSixteenAndLater_ShouldPermitTheWholeBitFamily(int major)
    {
        using var ctx = Declared(major);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.SqlServer.left_shift(x.Int, 1),
            R = SqlFunctions.SqlServer.right_shift(x.Int, 1),
            C = SqlFunctions.SqlServer.bit_count(x.Int),
            G = SqlFunctions.SqlServer.get_bit(x.Int, 1),
            S = SqlFunctions.SqlServer.set_bit(x.Int, 1),
            S3 = SqlFunctions.SqlServer.set_bit(x.Int, 1, true)
        }));

        sql.Should().Contain("left_shift(cast(nullableint as int), cast(1 as bigint))");
        sql.Should().Contain("right_shift(cast(nullableint as int), cast(1 as bigint))");
        sql.Should().Contain("bit_count(cast(nullableint as int))");
        sql.Should().Contain("get_bit(cast(nullableint as int), cast(1 as int))");
        sql.Should().Contain("set_bit(cast(nullableint as int), cast(1 as int))");
        sql.Should().Contain("set_bit(cast(nullableint as int), cast(1 as int), cast(1 as bit))");
    }

    [Fact]
    public void DeclaredFifteen_ShouldRefuseEveryBitNameWithItsOwnMinimum()
    {
        using var ctx = Declared(15);
        var e = ctx.From<IComplexEntity>();

        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(x.Int, 1) }),
            "The LEFT_SHIFT function requires SQL Server major version 16 or later; declared major version: 15.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.right_shift(x.Int, 1) }),
            "The RIGHT_SHIFT function requires SQL Server major version 16 or later; declared major version: 15.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.bit_count(x.Int) }),
            "The BIT_COUNT function requires SQL Server major version 16 or later; declared major version: 15.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.get_bit(x.Int, 1) }),
            "The GET_BIT function requires SQL Server major version 16 or later; declared major version: 15.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.set_bit(x.Int, 1) }),
            "The SET_BIT function requires SQL Server major version 16 or later; declared major version: 15.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.set_bit(x.Int, 1, true) }),
            "The SET_BIT function requires SQL Server major version 16 or later; declared major version: 15.");
    }

    [Fact]
    public void UnversionedAndEditionOnly_ShouldRefuseEveryBitName()
    {
        using var unversioned = Unversioned();
        var e = unversioned.From<IComplexEntity>();

        AssertRefused(unversioned, e.Select(x => new { V = SqlFunctions.SqlServer.left_shift(x.Int, 1) }),
            "The LEFT_SHIFT function requires SQL Server major version 16 or later; declared major version: unspecified.");
        AssertRefused(unversioned, e.Select(x => new { V = SqlFunctions.SqlServer.right_shift(x.Int, 1) }),
            "The RIGHT_SHIFT function requires SQL Server major version 16 or later; declared major version: unspecified.");
        AssertRefused(unversioned, e.Select(x => new { V = SqlFunctions.SqlServer.bit_count(x.Int) }),
            "The BIT_COUNT function requires SQL Server major version 16 or later; declared major version: unspecified.");
        AssertRefused(unversioned, e.Select(x => new { V = SqlFunctions.SqlServer.get_bit(x.Int, 1) }),
            "The GET_BIT function requires SQL Server major version 16 or later; declared major version: unspecified.");
        AssertRefused(unversioned, e.Select(x => new { V = SqlFunctions.SqlServer.set_bit(x.Int, 1) }),
            "The SET_BIT function requires SQL Server major version 16 or later; declared major version: unspecified.");

        using var editionOnly = EditionOnly(SqlServerEngineEdition.SqlDatabase);
        var editionEntity = editionOnly.From<IComplexEntity>();

        AssertRefused(editionOnly, editionEntity.Select(x => new { V = SqlFunctions.SqlServer.bit_count(x.Int) }),
            "The BIT_COUNT function requires SQL Server major version 16 or later; declared major version: unspecified.");

        using var azure12 = Declared(12, SqlServerEngineEdition.SqlDatabase);
        var azureEntity = azure12.From<IComplexEntity>();

        AssertRefused(azure12, azureEntity.Select(x => new { V = SqlFunctions.SqlServer.get_bit(x.Int, 1) }),
            "The GET_BIT function requires SQL Server major version 16 or later; declared major version: 12.");
    }

    // ---------------------------------------------------------------------------------------------
    // U2 — TRY_CAST / TRY_CONVERT closed-map conversions
    // ---------------------------------------------------------------------------------------------

    /// <summary>The 13-entry closed destination map, read off one projection of both conversion names.</summary>
    [Fact]
    public void TryConversions_ShouldRenderTheClosedDestinationMap()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            B = SqlFunctions.SqlServer.try_cast<byte>(x.String),
            S = SqlFunctions.SqlServer.try_cast<short>(x.String),
            I = SqlFunctions.SqlServer.try_cast<int>(x.String),
            L = SqlFunctions.SqlServer.try_cast<long>(x.String),
            F = SqlFunctions.SqlServer.try_cast<float>(x.String),
            D = SqlFunctions.SqlServer.try_cast<double>(x.String),
            M = SqlFunctions.SqlServer.try_cast<decimal>(x.String),
            Bit = SqlFunctions.SqlServer.try_cast<bool>(x.String),
            G = SqlFunctions.SqlServer.try_cast<Guid>(x.String),
            Dt = SqlFunctions.SqlServer.try_cast<DateTime>(x.String),
            Dto = SqlFunctions.SqlServer.try_cast<DateTimeOffset>(x.String),
            Ts = SqlFunctions.SqlServer.try_cast<TimeSpan>(x.String),
            St = SqlFunctions.SqlServer.try_cast(x.String)
        }));

        sql.Should().Contain("try_cast(somestring as tinyint)");
        sql.Should().Contain("try_cast(somestring as smallint)");
        sql.Should().Contain("try_cast(somestring as int)");
        sql.Should().Contain("try_cast(somestring as bigint)");
        sql.Should().Contain("try_cast(somestring as real)");
        sql.Should().Contain("try_cast(somestring as float(53))");
        sql.Should().Contain("try_cast(somestring as decimal(38,10))");
        sql.Should().Contain("try_cast(somestring as bit)");
        sql.Should().Contain("try_cast(somestring as uniqueidentifier)");
        sql.Should().Contain("try_cast(somestring as datetime2(7))");
        sql.Should().Contain("try_cast(somestring as datetimeoffset(7))");
        sql.Should().Contain("try_cast(somestring as time(7))");
        sql.Should().Contain("try_cast(somestring as nvarchar(4000))");

        // No CLR-name fall-back: MakeTypeName's open `type.Name` arm must never reach the SQL.
        sql.Should().NotContain(" as Boolean");
        sql.Should().NotContain(" as Guid ");
        sql.Should().NotContain(" as DateTime ");
        sql.Should().NotContain(" as DateTime2");
        sql.Should().NotContain(" as TimeSpan");
        sql.Should().NotContain(" as String");
    }

    /// <summary>
    /// Omitted style, a supplied style and an explicitly NULL style are three distinct renderings; the
    /// NULL style is a typed NULL so the server keeps its native NULL-style semantics.
    /// </summary>
    [Fact]
    public void TryConvert_ShouldDistinguishOmittedSuppliedAndNullStyle()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            NoStyle = SqlFunctions.SqlServer.try_convert<int>(x.String),
            Style = SqlFunctions.SqlServer.try_convert<int>(x.String, 126),
            NullStyle = SqlFunctions.SqlServer.try_convert<int>(x.String, null),
            TextNoStyle = SqlFunctions.SqlServer.try_convert(x.String),
            TextStyle = SqlFunctions.SqlServer.try_convert(x.String, 1),
            TextNullStyle = SqlFunctions.SqlServer.try_convert(x.String, null)
        }));

        sql.Should().Contain("try_convert(int, somestring)");
        sql.Should().Contain("try_convert(int, somestring, 126)");
        sql.Should().Contain("try_convert(int, somestring, CAST(NULL AS int))");
        sql.Should().Contain("try_convert(nvarchar(4000), somestring)");
        sql.Should().Contain("try_convert(nvarchar(4000), somestring, 1)");
        sql.Should().Contain("try_convert(nvarchar(4000), somestring, CAST(NULL AS int))");
    }

    [Fact]
    public void TryConversions_ShouldRenderNullInputAndMalformedTextLiterals()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            NullInput = SqlFunctions.SqlServer.try_cast<int>((string?)null),
            Text = SqlFunctions.SqlServer.try_cast<int>("not-a-number"),
            Converted = SqlFunctions.SqlServer.try_convert<int>("42", 0)
        }));

        sql.Should().Contain("try_cast(null as int)");
        sql.Should().Contain("try_cast(N'not-a-number' as int)");
        sql.Should().Contain("try_convert(int, N'42', 0)");
    }

    /// <summary>
    /// The target is validated from the method's generic argument even when the call sits inside a
    /// constructor projection, so an unmapped target fails while the command is being prepared and never
    /// reaches the server.
    /// </summary>
    [Fact]
    public void TryConversions_ShouldRefuseAnUnmappedTargetBeforeTheCommand()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx,
                e.Select(x => new { V = SqlFunctions.SqlServer.try_cast<T348UnmappedTarget>(x.Int) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The TRY_CAST function cannot map CLR target 'T348UnmappedTarget' to a supported SQL Server conversion type.");

        FluentActions.Invoking(() => SqlOf(ctx,
                e.Select(x => new T348Projection(x.Id,
                    SqlFunctions.SqlServer.try_convert<T348UnmappedTarget>(x.Int, 1)))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The TRY_CONVERT function cannot map CLR target 'T348UnmappedTarget' to a supported SQL Server conversion type.");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void TryConversions_ShouldRenderInEverySqlServerDeclaration(int major)
    {
        using var ctx = Declared(major);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Cast = SqlFunctions.SqlServer.try_cast<int>(x.String),
            Convert = SqlFunctions.SqlServer.try_convert<decimal>(x.String, 0)
        }));

        sql.Should().Contain("try_cast(somestring as int)");
        sql.Should().Contain("try_convert(decimal(38,10), somestring, 0)");
    }

    [Fact]
    public void TryConversions_ShouldRenderOnUnversionedAndEditionOnlyDeclarations()
    {
        using var unversioned = Unversioned();
        var unversionedEntity = unversioned.From<IComplexEntity>();
        SqlOf(unversioned, unversionedEntity.Select(x => new
            {
                Cast = SqlFunctions.SqlServer.try_cast<Guid>(x.String),
                Convert = SqlFunctions.SqlServer.try_convert<DateTime>(x.String, null)
            }))
            .Should().Contain("try_cast(somestring as uniqueidentifier)")
            .And.Contain("try_convert(datetime2(7), somestring, CAST(NULL AS int))");

        using var editionOnly = EditionOnly(SqlServerEngineEdition.SqlDatabase);
        var editionEntity = editionOnly.From<IComplexEntity>();
        SqlOf(editionOnly, editionEntity.Select(x => new
            { Cast = SqlFunctions.SqlServer.try_cast<TimeSpan>(x.String) }))
            .Should().Contain("try_cast(somestring as time(7))");
    }

    /// <summary>
    /// The version-bound renderer is per-dialect state, so two differently declared contexts alive at the
    /// same time must answer from their own declaration only: a 17 context opening a 2025 name never
    /// warms it for a 16 context, and a 16 refusal never disables it on 17 (both orders are exercised).
    /// </summary>
    [Fact]
    public void TwoDeclarations_ShouldKeepTheirCapabilitiesSeparate()
    {
        using var seventeen = Declared(17);
        using var sixteen = Declared(16);

        seventeen.Dialect.SqlServerFunctions.Should().NotBeSameAs(sixteen.Dialect.SqlServerFunctions,
            "the renderer is constructed per dialect, so it cannot answer from another declaration");
        seventeen.Dialect.SqlServerFunctions!.Supports("regexp_count").Should().BeTrue();
        seventeen.Dialect.SqlServerFunctions.GetUnsupportedReason("regexp_count").Should().BeNull();
        sixteen.Dialect.SqlServerFunctions!.Supports("regexp_count").Should().BeFalse();
        sixteen.Dialect.SqlServerFunctions.GetUnsupportedReason("regexp_count")
            .Should().Be("The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: 16.");

        var e17 = seventeen.From<IComplexEntity>();
        var e16 = sixteen.From<IComplexEntity>();

        SqlOf(seventeen, e17.Select(x => new { C = SqlFunctions.SqlServer.regexp_count(x.String, "[0-9]") }))
            .Should().Contain("regexp_count(somestring, N'[0-9]')");

        // Rendering on 17 must not warm the name for the 16 context still alive beside it.
        AssertRefused(sixteen, e16.Select(x => new { C = SqlFunctions.SqlServer.regexp_count(x.String, "[0-9]") }),
            "The REGEXP_COUNT function requires SQL Server major version 17 or later; declared major version: 16.");

        // ... and the 16 context keeps rendering the family it does own, with its own operand shapes.
        SqlOf(sixteen, e16.Select(x => new { V = SqlFunctions.SqlServer.bit_count(x.Int) }))
            .Should().Contain("bit_count(cast(nullableint as int))");
        SqlOf(sixteen, e16.Select(x => new { V = SqlFunctions.SqlServer.set_bit(x.Int, 1, true) }))
            .Should().Contain("set_bit(cast(nullableint as int), cast(1 as int), cast(1 as bit))");

        // A fresh 17 context created after the 16 refusal still renders the 2025 name.
        using var seventeenAgain = Declared(17);
        var e17Again = seventeenAgain.From<IComplexEntity>();
        SqlOf(seventeenAgain, e17Again.Select(x => new { C = SqlFunctions.SqlServer.regexp_count(x.String, "[0-9]") }))
            .Should().Contain("regexp_count(somestring, N'[0-9]')");

        // The 16 minimum stays its own: a 15 context refuses SET_BIT while both wider contexts accept it.
        using var fifteen = Declared(15);
        var e15 = fifteen.From<IComplexEntity>();
        AssertRefused(fifteen, e15.Select(x => new { V = SqlFunctions.SqlServer.set_bit(x.Int, 1) }),
            "The SET_BIT function requires SQL Server major version 16 or later; declared major version: 15.");
    }

    // ---------------------------------------------------------------------------------------------
    // U3 — regex (scalars + TVF) and fuzzy matching
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Every shipped <c>regexp_substr</c>/<c>regexp_instr</c> overload renders its native prefix form,
    /// and an omitted optional argument is a different call shape from a supplied NULL: the NULL is a
    /// typed NULL, which the engine turns into a NULL result (probe <c>substr_null_start</c>,
    /// <c>instr_null_return</c>).
    /// </summary>
    [Fact]
    public void RegexScalars_ShouldRenderEveryOverloadAndPreserveOmittedVersusNull()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Sub2 = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+"),
            Sub3 = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+", 2),
            Sub4 = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+", 2, 1),
            Sub5 = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+", 2, 1, "i"),
            SubNullStart = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+", null),
            SubNullOccurrence = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+", 1, null),
            SubNullMatchType = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+", 1, 1, null),

            Instr2 = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+"),
            Instr3 = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 3),
            Instr4 = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 3, 2),
            Instr5 = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 3, 2, 1),
            Instr6 = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 3, 2, 1, "c"),
            InstrNullStart = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", null),
            InstrNullOccurrence = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 1, null),
            InstrNullReturn = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 1, 1, null),
            InstrNullMatchType = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 1, 1, 0, null)
        }));

        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+')");
        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+', 2)");
        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+', 2, 1)");
        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+', 2, 1, cast(N'i' as varchar(4000)))");
        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+', null)");
        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+', 1, null)");
        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+', 1, 1, cast(null as varchar(4000)))");

        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+')");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 3)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 3, 2)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 3, 2, 1)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 3, 2, 1, cast(N'c' as varchar(4000)))");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', null)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 1, null)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 1, 1, null)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 1, 1, 0, cast(null as varchar(4000)))");
    }

    /// <summary>
    /// Both <c>regexp_matches</c> TVF overloads render as a FROM source with their arguments, and the
    /// row interface carries the observed types and explicit physical column names.
    /// </summary>
    [Fact]
    public void RegexpMatches_ShouldRenderBothOverloadsAndTheRowShape()
    {
        using var ctx = Declared(17);

        var plain = SqlOf(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_matches("a1b2", "[0-9]"))
            .Select(r => new { r.MatchId, r.StartPosition, r.EndPosition, r.MatchValue, r.SubstringMatches }));

        plain.Should().Contain("regexp_matches(N'a1b2', N'[0-9]')");
        plain.Should().Contain("match_id");
        plain.Should().Contain("start_position");
        plain.Should().Contain("end_position");
        plain.Should().Contain("match_value");
        plain.Should().Contain("substring_matches");

        var flagged = SqlOf(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_matches("aAbB", "a", "i"))
            .Select(r => r.MatchValue));

        flagged.Should().Contain("regexp_matches(N'aAbB', N'a', cast(N'i' as varchar(4000)))");

        var row = typeof(SqlFunctions.ISqlServerRegexpMatchRow);
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.MatchId))!.PropertyType.Should().Be(typeof(long));
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.StartPosition))!.PropertyType.Should().Be(typeof(int));
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.EndPosition))!.PropertyType.Should().Be(typeof(int));
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.MatchValue))!.PropertyType.Should().Be(typeof(string));
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.SubstringMatches))!.PropertyType.Should().Be(typeof(string));

        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.MatchId))!
            .GetCustomAttribute<ColumnAttribute>()!.Name.Should().Be("match_id");
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.StartPosition))!
            .GetCustomAttribute<ColumnAttribute>()!.Name.Should().Be("start_position");
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.EndPosition))!
            .GetCustomAttribute<ColumnAttribute>()!.Name.Should().Be("end_position");
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.MatchValue))!
            .GetCustomAttribute<ColumnAttribute>()!.Name.Should().Be("match_value");
        row.GetProperty(nameof(SqlFunctions.ISqlServerRegexpMatchRow.SubstringMatches))!
            .GetCustomAttribute<ColumnAttribute>()!.Name.Should().Be("substring_matches");
    }

    /// <summary>
    /// The five fuzzy members render their native call, including the bounded <c>edit_distance</c>; a
    /// supplied NULL maximum is a typed NULL and stays distinct from the omitted form.
    /// </summary>
    [Fact]
    public void FuzzyFamily_ShouldRenderEveryMemberAndTheBoundedForm()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Distance = SqlFunctions.SqlServer.edit_distance(x.String, "kitten"),
            Bounded = SqlFunctions.SqlServer.edit_distance(x.String, "kitten", 2),
            NullBound = SqlFunctions.SqlServer.edit_distance(x.String, "kitten", null),
            Similarity = SqlFunctions.SqlServer.edit_distance_similarity(x.String, "kitten"),
            JaroDistance = SqlFunctions.SqlServer.jaro_winkler_distance(x.String, "martha"),
            JaroSimilarity = SqlFunctions.SqlServer.jaro_winkler_similarity(x.String, "martha")
        }));

        sql.Should().Contain("edit_distance(somestring, N'kitten')");
        sql.Should().Contain("edit_distance(somestring, N'kitten', 2)");
        sql.Should().Contain("edit_distance(somestring, N'kitten', null)");
        sql.Should().Contain("edit_distance_similarity(somestring, N'kitten')");
        sql.Should().Contain("jaro_winkler_distance(somestring, N'martha')");
        sql.Should().Contain("jaro_winkler_similarity(somestring, N'martha')");
    }

    /// <summary>
    /// A wider CLR operand for the native <c>int</c> maximum distance would be rendered as a truncating
    /// SQL cast, so the translator refuses it before any command is prepared; a constant that fits the
    /// 32-bit range exactly is accepted.
    /// </summary>
    [Fact]
    public void FuzzyMaximum_ShouldRefuseAnUnrepresentableOperandBeforeSql()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var wide = 5L;
        AssertRefused(ctx,
            e.Select(x => new { V = SqlFunctions.SqlServer.edit_distance(x.String, "kitten", (int?)wide) }),
            "The edit_distance maximum_distance argument must fit a 32-bit integer; the 'Int64' operand would be truncated.");

        var fractional = 1.5m;
        AssertRefused(ctx,
            e.Select(x => new { V = SqlFunctions.SqlServer.edit_distance(x.String, "kitten", (int?)fractional) }),
            "The edit_distance maximum_distance argument must fit a 32-bit integer; the 'Decimal' operand would be truncated.");

        const long fits = 3;
        var accepted = SqlOf(ctx,
            e.Select(x => new { V = SqlFunctions.SqlServer.edit_distance(x.String, "kitten", (int?)fits) }));
        accepted.Should().Contain("edit_distance(somestring, N'kitten', 3)");
    }

    /// <summary>
    /// There is no speculative client-side regex validation: an invalid pattern, flag or position is
    /// rendered and reaches the engine, which owns the native error (Msg 19308/19303/19301).
    /// </summary>
    [Fact]
    public void RegexScalars_ShouldLeaveInvalidPatternsFlagsAndPositionsToTheEngine()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            BadPattern = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9"),
            BadFlag = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]", 1, 1, 0, "z"),
            BadStart = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]", 0),
            BadOccurrence = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]", 1, 0),
            BadReturnOption = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]", 1, 1, 2)
        }));

        sql.Should().Contain("regexp_substr(somestring, N'[0-9')");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]', 1, 1, 0, cast(N'z' as varchar(4000)))");
        sql.Should().Contain("regexp_substr(somestring, N'[0-9]', 0)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]', 1, 0)");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]', 1, 1, 2)");
    }

    [Fact]
    public void DeclaredSixteen_ShouldRefuseEveryRegexAndFuzzyName()
        => AssertRegexAndFuzzyRefused(Declared(16), "16");

    [Fact]
    public void UnversionedAndEditionOnly_ShouldRefuseEveryRegexAndFuzzyName()
    {
        using var unversioned = Unversioned();
        AssertRegexAndFuzzyRefused(unversioned, "unspecified");

        using var editionOnly = EditionOnly(SqlServerEngineEdition.SqlDatabase);
        AssertRegexAndFuzzyRefused(editionOnly, "unspecified");

        using var azure12 = Declared(12, SqlServerEngineEdition.SqlDatabase);
        AssertRegexAndFuzzyRefused(azure12, "12");
    }

    [Theory]
    [InlineData(17)]
    [InlineData(18)]
    public void DeclaredSeventeenAndLater_ShouldPermitEveryRegexAndFuzzyName(int major)
    {
        using var ctx = Declared(major);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Sub = SqlFunctions.SqlServer.regexp_substr(x.String, "[0-9]+", 1, 1, "i"),
            Instr = SqlFunctions.SqlServer.regexp_instr(x.String, "[0-9]+", 1, 1, 0, "i"),
            Distance = SqlFunctions.SqlServer.edit_distance(x.String, "kitten", 2),
            Similarity = SqlFunctions.SqlServer.edit_distance_similarity(x.String, "kitten"),
            JaroDistance = SqlFunctions.SqlServer.jaro_winkler_distance(x.String, "martha"),
            JaroSimilarity = SqlFunctions.SqlServer.jaro_winkler_similarity(x.String, "martha")
        }));

        sql.Should().Contain("regexp_substr(somestring, N'[0-9]+', 1, 1, cast(N'i' as varchar(4000)))");
        sql.Should().Contain("regexp_instr(somestring, N'[0-9]+', 1, 1, 0, cast(N'i' as varchar(4000)))");
        sql.Should().Contain("edit_distance(somestring, N'kitten', 2)");
        sql.Should().Contain("edit_distance_similarity(somestring, N'kitten')");
        sql.Should().Contain("jaro_winkler_distance(somestring, N'martha')");
        sql.Should().Contain("jaro_winkler_similarity(somestring, N'martha')");

        SqlOf(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_matches("a1", "[0-9]", "i"))
                .Select(r => r.MatchValue))
            .Should().Contain("regexp_matches(N'a1', N'[0-9]', cast(N'i' as varchar(4000)))");
    }

    private static void AssertRegexAndFuzzyRefused(SqlServerDataContext ctx, string declared)
    {
        var e = ctx.From<IComplexEntity>();

        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.regexp_substr(x.String, "p") }),
            $"The REGEXP_SUBSTR function requires SQL Server major version 17 or later; declared major version: {declared}.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.regexp_instr(x.String, "p") }),
            $"The REGEXP_INSTR function requires SQL Server major version 17 or later; declared major version: {declared}.");
        AssertRefused(ctx, ctx.From(() => SqlFunctions.SqlServer.regexp_matches("a", "p")).Select(r => r.MatchValue),
            $"The REGEXP_MATCHES function requires SQL Server major version 17 or later; declared major version: {declared}.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.edit_distance(x.String, "p") }),
            $"The EDIT_DISTANCE function requires SQL Server major version 17 or later; declared major version: {declared}.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.edit_distance_similarity(x.String, "p") }),
            $"The EDIT_DISTANCE_SIMILARITY function requires SQL Server major version 17 or later; declared major version: {declared}.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.jaro_winkler_distance(x.String, "p") }),
            $"The JARO_WINKLER_DISTANCE function requires SQL Server major version 17 or later; declared major version: {declared}.");
        AssertRefused(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.jaro_winkler_similarity(x.String, "p") }),
            $"The JARO_WINKLER_SIMILARITY function requires SQL Server major version 17 or later; declared major version: {declared}.");
    }

    /// <summary>A CLR target outside the closed map: a value struct with no SQL Server destination.</summary>
    private readonly record struct T348UnmappedTarget(int Value);

    /// <summary>A constructor projection, so the target check cannot hide behind member initialisation.</summary>
    private sealed class T348Projection(long id, T348UnmappedTarget? value)
    {
        public long Id { get; } = id;

        public T348UnmappedTarget? Value { get; } = value;
    }

    // ---------------------------------------------------------------------------------------------
    // U4 — aggregate + analytic family (the five remaining delivery names)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Every U4 aggregate renders its native spelling in an aggregate context: the approximate
    /// distinct count stays <c>approx_count_distinct</c> (it is not the exact
    /// <c>count(distinct ...)</c>), the grouping id flattens both arguments, and the approximate
    /// percentiles are ordered-set (<c>within group</c>) forms with no <c>OVER</c> expansion.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    public void DeclaredSixteenAndLater_ShouldRenderEveryU4Aggregate(int major)
    {
        using var ctx = Declared(major);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.GroupBy(x => new { x.Int, x.Boolean }).Select(x => new
        {
            x.Int,
            x.Boolean,
            A = SqlFunctions.SqlServer.approx_count_distinct(x.Int),
            C = SqlFunctions.SqlServer.checksum_agg(x.Int),
            G = SqlFunctions.SqlServer.grouping_id(x.Int, x.Boolean),
            P = SqlFunctions.SqlServer.approx_percentile_cont(0.5, x.Int),
            D = SqlFunctions.SqlServer.approx_percentile_disc(0.5, x.Int),
            E = SqlFunctions.Sql.count_distinct(x.Int)
        }));

        sql.Should().Contain("approx_count_distinct(nullableint)");
        sql.Should().Contain("checksum_agg(nullableint)");
        sql.Should().Contain("grouping_id(nullableint, b)");
        sql.Should().Contain("approx_percentile_cont(0.5) within group (order by nullableint)");
        sql.Should().Contain("approx_percentile_disc(0.5) within group (order by nullableint)");
        sql.Should().Contain("count(distinct nullableint)");
        sql.Should().Contain("group by nullableint, b");
        sql.Should().NotContain("over (", "the U4 aggregates offer no window form");
    }

    /// <summary>A NULL operand is rendered as a typed NULL (a bare <c>null</c> is Msg 8117); the
    /// aggregate keeps its own spelling. A NULL sort key is refused before SQL (Msg 5309).</summary>
    [Fact]
    public void U4Aggregates_ShouldRenderTypedNullOperandsAndRefuseANullSortKey()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.GroupBy(x => new { x.Boolean }).Select(x => new
        {
            x.Boolean,
            A = SqlFunctions.SqlServer.approx_count_distinct((int?)null),
            C = SqlFunctions.SqlServer.checksum_agg((int?)null)
        }));

        sql.Should().Contain("approx_count_distinct(CAST(NULL AS int))");
        sql.Should().Contain("checksum_agg(CAST(NULL AS int))");

        const string orderKeyMessage =
            "The order expression for approx_percentile_* on SQL Server must be a column, a parameter or a computed expression, not NULL or a literal constant.";

        FluentActions.Invoking(() => SqlOf(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_cont(0.5, (int?)null) })))
            .Should().Throw<NotSupportedException>().WithMessage(orderKeyMessage);

        FluentActions.Invoking(() => SqlOf(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_disc(0.5, (int?)null) })))
            .Should().Throw<NotSupportedException>().WithMessage(orderKeyMessage);

        // A captured scalar becomes a query parameter, which WITHIN GROUP does accept (Msg 5309 is
        // about literals only; the engine executes "order by @p").
        var captured = 1;
        SqlOf(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_cont(0.5, (int?)captured) }))
            .Should().Contain("within group (order by @");
    }

    /// <summary>The <c>params</c> array is flattened: one SQL argument per grouping expression, never a
    /// single array argument.</summary>
    [Fact]
    public void GroupingId_ShouldFlattenEveryGroupingArgument()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.GroupBy(x => new { x.Int, x.Boolean, x.Datetime }).Select(x => new
        {
            G = SqlFunctions.SqlServer.grouping_id(x.Int, x.Boolean, x.Datetime)
        }));

        sql.Should().Contain("grouping_id(nullableint, b, dt)");
        sql.Should().NotContain("grouping_id((nullableint, b, dt))");
    }

    /// <summary>GROUPING_ID is legal in the SELECT list, HAVING and ORDER BY of a grouped command.</summary>
    [Fact]
    public void GroupingId_ShouldBeAllowedInSelectHavingAndOrderBy()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.GroupBy(x => new { x.Int, x.Boolean })
                .Having(x => SqlFunctions.SqlServer.grouping_id(x.Int, x.Boolean) == 0)
                .Select(x => new { x.Int, x.Boolean, C = SqlFunctions.Sql.count() }))
            .Should().Contain("having grouping_id(nullableint, b) = 0");

        SqlOf(ctx, e.GroupBy(x => new { x.Int, x.Boolean })
                .Select(x => new { x.Int, G = SqlFunctions.SqlServer.grouping_id(x.Int, x.Boolean) })
                .OrderBy(x => x.G))
            .Should().Contain("order by grouping_id(nullableint, b)");
    }

    /// <summary>Without a <c>GROUP BY</c> (or outside the allowed clauses) the call is refused before SQL.</summary>
    [Fact]
    public void GroupingId_ShouldRequireAGroupByContext()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        AssertGroupingRefused(
            ctx,
            e.Select(x => new { G = SqlFunctions.SqlServer.grouping_id(x.Int) }),
            "The GROUPING_ID function requires a GROUP BY context.");

        AssertGroupingRefused(
            ctx,
            e.Where(x => SqlFunctions.SqlServer.grouping_id(x.Int) == 0)
                .GroupBy(x => new { x.Int })
                .Select(x => new { x.Int, C = SqlFunctions.Sql.count() }),
            "The GROUPING_ID function requires a GROUP BY context.");
    }

    /// <summary>An argument outside the grouping list is refused before SQL, like the engine's Msg 8161.</summary>
    [Fact]
    public void GroupingId_ShouldRequireEveryArgumentInTheGroupingList()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        AssertGroupingRefused(
            ctx,
            e.GroupBy(x => new { x.Int }).Select(x => new { G = SqlFunctions.SqlServer.grouping_id(x.Boolean) }),
            "The GROUPING_ID function requires every argument to match a GROUP BY expression.");

        AssertGroupingRefused(
            ctx,
            e.GroupBy(x => new { x.Int }).Select(x => new { G = SqlFunctions.SqlServer.grouping_id(x.Int, x.Boolean) }),
            "The GROUPING_ID function requires every argument to match a GROUP BY expression.");
    }

    /// <summary>The fraction is a literal, a captured scalar or a query parameter; endpoints stay exact.</summary>
    [Fact]
    public void ApproxPercentiles_ShouldRenderLiteralCapturedAndParameterFractions()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();
        var fraction = 0.75;

        var captured = SqlOf(ctx, e.GroupBy(x => new { x.Boolean })
            .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_cont(fraction, x.Int) }));
        captured.Should().Contain("approx_percentile_cont(@fraction) within group (order by nullableint)");

        var parameter = SqlOf(ctx, e.GroupBy(x => new { x.Boolean })
            .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_cont(SqlFunctions.Parameter<double>(0), x.Int) }));
        parameter.Should().Contain("approx_percentile_cont(@norm_p0) within group (order by nullableint)");

        var endpoints = SqlOf(ctx, e.GroupBy(x => new { x.Boolean }).Select(x => new
        {
            x.Boolean,
            L = SqlFunctions.SqlServer.approx_percentile_cont(0, x.Int),
            H = SqlFunctions.SqlServer.approx_percentile_disc(1, x.Int)
        }));
        endpoints.Should().Contain("approx_percentile_cont(0) within group (order by nullableint)");
        endpoints.Should().Contain("approx_percentile_disc(1) within group (order by nullableint)");
    }

    /// <summary>NaN/infinite/out-of-range and row-dependent fractions never reach the SQL text.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1.5)]
    [InlineData(-0.5)]
    public void ApproxPercentiles_ShouldRefuseANonFiniteOrOutOfRangeFraction(double fraction)
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                { P = SqlFunctions.SqlServer.approx_percentile_cont(fraction, x.Int) })))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("The fraction for approx_percentile_cont on SQL Server must be finite and between 0 and 1 inclusive.*");
    }

    [Fact]
    public void ApproxPercentiles_ShouldRefuseARowDependentFraction()
    {
        using var ctx = Declared(17);
        var e = ctx.From<IComplexEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                { P = SqlFunctions.SqlServer.approx_percentile_disc((double)x.Int!, x.Int) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The fraction for approx_percentile_disc on SQL Server must be a constant or a captured scalar.");
    }

    /// <summary>
    /// The declared matrix: count-distinct from major 15, both percentiles from major 16, and
    /// checksum/grouping with no version floor. Version-only: an unversioned or edition-only
    /// declaration refuses the versioned names and keeps the undated ones.
    /// </summary>
    [Fact]
    public void DeclaredFourteen_ShouldRefuseCountDistinctWithMajorFifteen()
    {
        using var ctx = Declared(14);
        var e = ctx.From<IComplexEntity>();

        AssertRefused(ctx, e.Select(x => new { A = SqlFunctions.SqlServer.approx_count_distinct(x.Int) }),
            "The APPROX_COUNT_DISTINCT function requires SQL Server major version 15 or later; declared major version: 14.");

        // No version floor: both keep rendering on 14.
        SqlOf(ctx, e.GroupBy(x => new { x.Int }).Select(x => new
        {
            C = SqlFunctions.SqlServer.checksum_agg(x.Int),
            G = SqlFunctions.SqlServer.grouping_id(x.Int)
        })).Should().Contain("checksum_agg(nullableint)").And.Contain("grouping_id(nullableint)");
    }

    [Fact]
    public void DeclaredFifteen_ShouldPermitCountDistinctAndRefuseBothPercentiles()
    {
        using var ctx = Declared(15);
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { x.Boolean, A = SqlFunctions.SqlServer.approx_count_distinct(x.Int) }))
            .Should().Contain("approx_count_distinct(nullableint)");

        AssertRefused(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_cont(0.5, x.Int) }),
            "The APPROX_PERCENTILE_CONT function requires SQL Server major version 16 or later; declared major version: 15.");

        AssertRefused(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_disc(0.5, x.Int) }),
            "The APPROX_PERCENTILE_DISC function requires SQL Server major version 16 or later; declared major version: 15.");
    }

    [Theory]
    [InlineData(nameof(Unversioned))]
    [InlineData("edition-only")]
    public void UnversionedAndEditionOnly_ShouldRefuseTheVersionedU4NamesAndKeepTheUndatedOnes(string declaration)
    {
        using var ctx = declaration == nameof(Unversioned) ? Unversioned() : EditionOnly(SqlServerEngineEdition.SqlDatabase);
        const string declared = "unspecified";
        var e = ctx.From<IComplexEntity>();

        AssertRefused(ctx, e.Select(x => new { A = SqlFunctions.SqlServer.approx_count_distinct(x.Int) }),
            $"The APPROX_COUNT_DISTINCT function requires SQL Server major version 15 or later; declared major version: {declared}.");
        AssertRefused(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_cont(0.5, x.Int) }),
            $"The APPROX_PERCENTILE_CONT function requires SQL Server major version 16 or later; declared major version: {declared}.");
        AssertRefused(ctx, e.GroupBy(x => new { x.Boolean })
                .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_disc(0.5, x.Int) }),
            $"The APPROX_PERCENTILE_DISC function requires SQL Server major version 16 or later; declared major version: {declared}.");

        SqlOf(ctx, e.GroupBy(x => new { x.Int }).Select(x => new
        {
            C = SqlFunctions.SqlServer.checksum_agg(x.Int),
            G = SqlFunctions.SqlServer.grouping_id(x.Int)
        })).Should().Contain("checksum_agg(nullableint)").And.Contain("grouping_id(nullableint)");
    }

    private static void AssertGroupingRefused<T>(IDataContext ctx, QueryCommand<T> command, string message)
        => FluentActions.Invoking(() => SqlOf(ctx, command))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage(message);

    private static void AssertRefused<T>(IDataContext ctx, QueryCommand<T> command, string message)
        => FluentActions.Invoking(() => SqlOf(ctx, command))
            .Should().Throw<NotSupportedException>()
            .WithMessage(message);

    // U5: the eleven deterministic refusals (the ten system names and ANY_VALUE). The message is one
    // stable template per member — `SqlFunctions.SqlServer.<member> (<TOKEN>) is refused: <reason>` —
    // and it is identical from the member body, from expression-tree translation and from the
    // SQL-builder command-boundary preflight.

    private static readonly string[] RefusedMemberNames =
    [
        "session_context", "session_id", "rowcount", "error",
        "error_number", "error_severity", "error_state", "error_line", "error_message", "error_procedure",
        "any_value"
    ];

    /// <summary>The eleven refused member names, one test case each.</summary>
    public static TheoryData<string> RefusedNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in RefusedMemberNames)
                data.Add(name);
            return data;
        }
    }

    private const string CatchScope =
        "requires a SQL Server CATCH-block scope; outside CATCH it returns NULL, "
        + "and nextorm does not establish that scope.";

    private static string ExpectedRefusal(string name) => name switch
    {
        "session_context" =>
            "SqlFunctions.SqlServer.session_context (SESSION_CONTEXT) is refused: returns sql_variant, "
            + "for which nextorm has no faithful CLR result contract; no lossy cast substitution is performed.",
        "session_id" =>
            "SqlFunctions.SqlServer.session_id (SESSION_ID) is refused: is not a SQL Server engine function; "
            + "the documented function belongs to Azure Synapse. SQL Server exposes session identity through "
            + "@@SPID or sys.dm_exec_sessions.session_id; no substitution is performed.",
        "rowcount" =>
            "SqlFunctions.SqlServer.rowcount (@@ROWCOUNT) is refused: describes the previous statement and is "
            + "statement-scoped; a per-row query projection cannot expose that value faithfully.",
        "error" =>
            "SqlFunctions.SqlServer.error (@@ERROR) is refused: describes the previous statement and is "
            + "statement-scoped; a per-row query projection cannot expose that value faithfully.",
        "error_number" => $"SqlFunctions.SqlServer.error_number (ERROR_NUMBER) is refused: {CatchScope}",
        "error_severity" => $"SqlFunctions.SqlServer.error_severity (ERROR_SEVERITY) is refused: {CatchScope}",
        "error_state" => $"SqlFunctions.SqlServer.error_state (ERROR_STATE) is refused: {CatchScope}",
        "error_line" => $"SqlFunctions.SqlServer.error_line (ERROR_LINE) is refused: {CatchScope}",
        "error_message" => $"SqlFunctions.SqlServer.error_message (ERROR_MESSAGE) is refused: {CatchScope}",
        "error_procedure" => $"SqlFunctions.SqlServer.error_procedure (ERROR_PROCEDURE) is refused: {CatchScope}",
        "any_value" =>
            "SqlFunctions.SqlServer.any_value (ANY_VALUE) is refused: SQL Server build 17.0.5005.3 rejected "
            + "this function with Msg 195, including with preview features enabled; no supported SQL Server "
            + "execution contract is established.",
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such refused SQL Server function.")
    };

    [Theory]
    [MemberData(nameof(RefusedNames))]
    public void RefusedMember_ShouldThrowTheExactDiagnosticOnDirectInvocation(string name)
    {
        var thrown = FluentActions.Invoking(DirectInvocation(name))
            .Should().Throw<NotSupportedException>("the member body is a callable refusal")
            .Which;

        thrown.Message.Should().Be(ExpectedRefusal(name));
    }

    [Theory]
    [MemberData(nameof(RefusedNames))]
    public void RefusedMember_ShouldThrowTheSameDiagnosticInAnExpressionTreeWithoutEvaluatingArguments(string name)
    {
        using var ctx = Declared(17);
        RefusalSentinel.Reset();

        var thrown = FluentActions.Invoking(ExpressionInvocation(ctx, name))
            .Should().Throw<NotSupportedException>("the translator refuses the exact member before visiting arguments")
            .Which;

        thrown.Message.Should().Be(ExpectedRefusal(name));
        RefusalSentinel.Touches.Should().Be(0, "a refused call must not evaluate its argument expressions");
    }

    /// <summary>
    /// The eleven refusals do not disturb their supported siblings: the live session/statement facts of
    /// the same surface still render, and the member set is closed (eleven distinct diagnostics, one per
    /// name). The refusal is raised while the SQL is being built — <c>GetPreparedQueryCommand</c> never
    /// returns a plan, so no <c>PreparedCommandFactory</c> and no plan command exist: the preflight is
    /// the first statement of <c>SqlBuilder.MakeSelect</c>, which <c>QueryPlanner</c> calls before
    /// <c>new DbPreparedQueryCommand</c> (QueryPlanner.cs:866/909). The live arm proves the same with
    /// the <c>IQueryInterceptor.CommandInitialized</c> creation hook.
    /// </summary>
    [Fact]
    public void RefusedMembers_ShouldKeepTheSupportedSystemSiblingsRendering()
    {
        using var ctx = Declared(17);

        var siblings = SqlOf(ctx, ctx.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => new
        {
            User = SqlFunctions.Sql.current_user(),
            Database = SqlFunctions.SqlServer.db_name(),
            Rows = SqlFunctions.SqlServer.rowcount_big(),
            State = SqlFunctions.SqlServer.xact_state(),
            Identity = SqlFunctions.SqlServer.scope_identity()
        }));

        siblings.Should().Contain("current_user")
            .And.Contain("db_name()")
            .And.Contain("rowcount_big()")
            .And.Contain("xact_state()")
            .And.Contain("scope_identity()");

        var diagnostics = RefusedMemberNames.Select(ExpectedRefusal).Distinct().ToList();
        diagnostics.Should().HaveCount(11, "one distinct diagnostic per refused member");
    }

    private static Action DirectInvocation(string name) => name switch
    {
        "session_context" => () => SqlFunctions.SqlServer.session_context("k"),
        "session_id" => () => SqlFunctions.SqlServer.session_id(),
        "rowcount" => () => SqlFunctions.SqlServer.rowcount(),
        "error" => () => SqlFunctions.SqlServer.error(),
        "error_number" => () => SqlFunctions.SqlServer.error_number(),
        "error_severity" => () => SqlFunctions.SqlServer.error_severity(),
        "error_state" => () => SqlFunctions.SqlServer.error_state(),
        "error_line" => () => SqlFunctions.SqlServer.error_line(),
        "error_message" => () => SqlFunctions.SqlServer.error_message(),
        "error_procedure" => () => SqlFunctions.SqlServer.error_procedure(),
        "any_value" => () => SqlFunctions.SqlServer.any_value((int?)1),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such refused SQL Server function.")
    };

    private static Action ExpressionInvocation(SqlServerDataContext ctx, string name) => name switch
    {
        "session_context" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.session_context(RefusalSentinel.TouchText()) })),
        "session_id" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.session_id() })),
        "rowcount" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.rowcount() })),
        "error" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.error() })),
        "error_number" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.error_number() })),
        "error_severity" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.error_severity() })),
        "error_state" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.error_state() })),
        "error_line" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.error_line() })),
        "error_message" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.error_message() })),
        "error_procedure" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.error_procedure() })),
        "any_value" => () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .Select(x => new { V = SqlFunctions.SqlServer.any_value(RefusalSentinel.TouchInt()) })),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such refused SQL Server function.")
    };

    /// <summary>
    /// A sentinel argument for the expression-tree path: touching it increments the counter and throws,
    /// so a refusal that evaluated its arguments would be visible as both a different exception and a
    /// non-zero count. Direct-call argument evaluation cannot be prevented and is not asserted.
    /// </summary>
    public static class RefusalSentinel
    {
        private static int _touches;

        public static int Touches => Volatile.Read(ref _touches);

        public static void Reset() => Volatile.Write(ref _touches, 0);

        public static string TouchText()
        {
            Interlocked.Increment(ref _touches);
            throw new InvalidOperationException("A refused call must not evaluate its arguments.");
        }

        public static int TouchInt()
        {
            Interlocked.Increment(ref _touches);
            throw new InvalidOperationException("A refused call must not evaluate its arguments.");
        }
    }
}

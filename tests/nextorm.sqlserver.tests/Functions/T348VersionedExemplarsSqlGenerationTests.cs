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

    /// <summary>A CLR target outside the closed map: a value struct with no SQL Server destination.</summary>
    private readonly record struct T348UnmappedTarget(int Value);

    /// <summary>A constructor projection, so the target check cannot hide behind member initialisation.</summary>
    private sealed class T348Projection(long id, T348UnmappedTarget? value)
    {
        public long Id { get; } = id;

        public T348UnmappedTarget? Value { get; } = value;
    }

    private static void AssertRefused<T>(IDataContext ctx, QueryCommand<T> command, string message)
        => FluentActions.Invoking(() => SqlOf(ctx, command))
            .Should().Throw<NotSupportedException>()
            .WithMessage(message);
}

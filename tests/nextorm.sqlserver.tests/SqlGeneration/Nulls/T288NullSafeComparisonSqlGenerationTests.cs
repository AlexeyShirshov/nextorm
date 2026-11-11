using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;
using NextORM.SqlServer.Tests;

namespace nextorm.sqlserver.tests.SqlGeneration;

/// <summary>
/// T288 (#288) on SQL Server: the explicit null-safe comparison helpers
/// (<see cref="CommonFunctions.is_not_distinct_from{T}(T, T)"/> and
/// <see cref="CommonFunctions.is_distinct_from{T}(T, T)"/>). SQL Server has no <c>IS [NOT] DISTINCT FROM</c> on the supported version floor, so both forms are the portable guarded emulation and a scalar result goes through the existing <c>bit</c> lowering, so
/// The test pins the rendered fragment, the capability disposition, the Boolean shaping in predicate and
/// scalar contexts, literal-NULL folding, the non-nullable value fast path, parameterisation and the
/// incompatible-type diagnostic — plus the explicit negative regression that ordinary <c>==</c>/<c>!=</c>
/// rendering is unchanged. No database is opened.
/// </summary>
public class T288NullSafeComparisonSqlGenerationTests
{
    /// <summary>The dialect placeholder prefix used by this provider.</summary>
    private const string P = "@";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string Collapse(string sql)
        => string.Join(' ', Normalize(sql).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlCollapsed<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Collapse(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void T288_Capability_ShouldDeclareBothFormsExplicitly()
    {
        var capability = SqlServerDialect.Instance.NullSafeComparison;

        capability.Should().NotBeNull();
        capability!.SupportsDistinct.Should().BeTrue();
        capability.SupportsNotDistinct.Should().BeTrue();
        capability.DistinctDisposition.Should().Be(NullSafeComparisonDisposition.Emulated);
        capability.NotDistinctDisposition.Should().Be(NullSafeComparisonDisposition.Emulated);
    }

    [Fact]
    public void T288_NotDistinct_ShouldRenderTheEmulatedFragment()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_not_distinct_from(x.A, x.B)).Select(x => x.Id))
            .Should().Contain("((a is not null and b is not null and a = b) or (a is null and b is null))");
    }

    [Fact]
    public void T288_Distinct_ShouldRenderTheComplementedFragment()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_distinct_from(x.A, x.B)).Select(x => x.Id))
            .Should().Contain("(not ((a is not null and b is not null and a = b) or (a is null and b is null)))");
    }

    [Fact]
    public void T288_ScalarProjection_ShouldShapeTheBoolean()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();

        var sql = SqlCollapsed(ctx, e.Select(x => new
        {
            Nd = SqlFunctions.Sql.is_not_distinct_from(x.A, x.B),
            D = SqlFunctions.Sql.is_distinct_from(x.A, x.B)
        }));

        // T-SQL has no boolean type: each predicate is legalised exactly once as a bit value.
        sql.Should().Contain(
            "cast(case when ((a is not null and b is not null and a = b) or (a is null and b is null)) then 1 else 0 end as bit) as [Nd]");
        sql.Should().Contain(
            "cast(case when (not ((a is not null and b is not null and a = b) or (a is null and b is null))) then 1 else 0 end as bit) as [D]");
        System.Text.RegularExpressions.Regex.Matches(sql, @"cast\(case when ").Count
            .Should().Be(2, "the whole predicate is legalised exactly once per projection");
    }

    [Fact]
    public void T288_LiteralNull_ShouldFoldToPresenceTest()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_not_distinct_from(x.A, null)).Select(x => x.Id))
            .Should().Contain("(a is null)").And.NotContain("is not distinct from");

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_distinct_from(x.A, null)).Select(x => x.Id))
            .Should().Contain("(a is not null)").And.NotContain("is distinct from");

        // Typed NULL and default(T?) produce the same folded shape.
        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_not_distinct_from(x.S1, (string?)null)).Select(x => x.Id))
            .Should().Contain("(s1 is null)");

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_distinct_from(x.A, default(int?))).Select(x => x.Id))
            .Should().Contain("(a is not null)");
    }

    [Fact]
    public void T288_TwoLiteralNulls_ShouldFoldToTheTwoValuedConstant()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_not_distinct_from<int?>(null, null)).Select(x => x.Id))
            .Should().Contain("1 = 1").And.NotContain("is not distinct from");

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_distinct_from<int?>(null, default)).Select(x => x.Id))
            .Should().Contain("1 = 0").And.NotContain("is distinct from");
    }

    [Fact]
    public void T288_NonNullableValueOperands_ShouldUseTheOrdinaryComparisonFastPath()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();
        int left = 1;
        int right = 2;

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_not_distinct_from(left, right) && x.Id > 0).Select(x => x.Id))
            .Should().Contain($"(@left = @right)").And.NotContain("is not distinct from");

        SqlCollapsed(ctx, e.Where(x => SqlFunctions.Sql.is_distinct_from(left, right) && x.Id > 0).Select(x => x.Id))
            .Should().Contain($"(@left <> @right)").And.NotContain("is distinct from");
    }

    [Fact]
    public void T288_CapturedOperand_ShouldStayOneParameterPerOccurrence()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();
        int? captured = null;

        var prepared = Prepare(ctx, e
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.A, captured))
            .Select(x => x.Id));

        var text = Normalize(prepared.DbCommand.CommandText);
        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToArray();

        parameters.Should().ContainSingle();
        parameters[0].ParameterName.Should().Be("captured");
        parameters[0].Value.Should().Be(DBNull.Value);
        text.Should().NotContain("= null");
        System.Text.RegularExpressions.Regex
            .Matches(text, System.Text.RegularExpressions.Regex.Escape($"@captured"))
            .Count.Should().Be(3, "the guarded form tests the operand for null and compares it");
    }

    [Fact]
    public void T288_IncompatibleOperands_ShouldThrowTheDeterministicDiagnostic()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();

        var act = () => SqlCollapsed(ctx, e
            .Where(x => SqlFunctions.Sql.is_distinct_from((object?)x.S1, (object?)x.A))
            .Select(x => x.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Null-safe comparison requires compatible operand types.");
    }

    [Fact]
    public void T288_OrdinaryEquality_ShouldRemainByteIdentical()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();
        int captured = 1;

        // The pre-change rendering of ordinary ==/!=: a plain comparison, one parameter, no null-safe
        // fragment. A captured non-nullable value never takes the #296 null-safe captured-parameter path.
        SqlCollapsed(ctx, e.Where(x => x.A == captured).Select(x => x.Id))
            .Should().Be("select id from t288_nsc where a = @captured");

        SqlCollapsed(ctx, e.Where(x => x.A != captured).Select(x => x.Id))
            .Should().Be("select id from t288_nsc where a != @captured");
    }

    [Fact]
    public void T288_CapturedNullOperand_ShouldBindTheMetadataDerivedType()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();
        int? captured = null;

        var prepared = Prepare(ctx, e
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.A, captured))
            .Select(x => x.Id));

        var parameter = prepared.DbCommand.Parameters.Cast<DbParameter>().Should().ContainSingle().Subject;

        // The SQL type comes from the compared column's mapping (int? -> int), never from the value, so a
        // captured NULL binds with the same type as every later non-null value.
        parameter.DbType.Should().Be(DbType.Int32);
        parameter.Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public void T288_UnmappedCapturedOperand_ShouldBeRejectedBeforeExecution()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IT288NullSafeComparisonEntity>();
        T288UnmappedOperand left = default;
        T288UnmappedOperand right = default;

        var act = () => SqlCollapsed(ctx, e
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(left, right) && x.Id > 0)
            .Select(x => x.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Null-safe comparison requires a SQL Server type mapping for null operands.");
    }
}

/// <summary>T288 SQL-generation fixture: two nullable numerics, two nullable strings and a non-nullable column.</summary>
[SqlTable("t288_nsc")]
public interface IT288NullSafeComparisonEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("a")]
    int? A { get; set; }

    [Column("b")]
    int? B { get; set; }

    [Column("s1")]
    string? S1 { get; set; }

    [Column("s2")]
    string? S2 { get; set; }
}

/// <summary>T288 fixture: a captured operand type the SQL Server parameter mapping does not know.</summary>
public readonly struct T288UnmappedOperand
{
    public int Value { get; init; }
}

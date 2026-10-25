using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T288 D1 (core): the explicit null-safe comparison helpers
/// (<see cref="CommonFunctions.is_distinct_from{T}(T, T)"/> /
/// <see cref="CommonFunctions.is_not_distinct_from{T}(T, T)"/>). The in-memory provider pins the five-row
/// truth table, the SQL side pins dispatch, literal-NULL folding, the compatible-type diagnostic, the
/// non-nullable value fast path and the capability seam. The six provider dialects are a later unit, so
/// the SQL assertions use a provider-free test dialect and the default capability stays fail-closed.
/// </summary>
public class T288NullSafeComparisonTests
{
    private readonly InMemoryRepository _sut;

    public T288NullSafeComparisonTests(InMemoryRepository sut)
    {
        _sut = sut;
        Rows().WithData(
        [
            new T288ComparisonEntity { Id = 1, N1 = 1, N2 = 1, S1 = "x", S2 = "x" },
            new T288ComparisonEntity { Id = 2, N1 = 1, N2 = 2, S1 = "x", S2 = "y" },
            new T288ComparisonEntity { Id = 3, N1 = null, N2 = null, S1 = null, S2 = null },
            new T288ComparisonEntity { Id = 4, N1 = null, N2 = 1, S1 = null, S2 = "x" },
            new T288ComparisonEntity { Id = 5, N1 = 1, N2 = null, S1 = "x", S2 = null },
        ]);
    }

    private EntityBuilder<T288ComparisonEntity> Rows() => _sut.DataProvider.From<T288ComparisonEntity>();

    private int[] Ids(Func<EntityBuilder<T288ComparisonEntity>, QueryCommand<int>> filter)
        => filter(Rows()).ToList().OrderBy(x => x).ToArray();

    // --- In-memory truth table (Q02/Q05) -------------------------------------------------------

    [Fact]
    public void InMemory_NullableNumeric_ShouldFollowTheFrozenTruthTable()
    {
        Ids(r => r.Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, x.N2)).Select(x => x.Id))
            .Should().Equal(1, 3);
        Ids(r => r.Where(x => SqlFunctions.Sql.is_distinct_from(x.N1, x.N2)).Select(x => x.Id))
            .Should().Equal(2, 4, 5);
    }

    [Fact]
    public void InMemory_NullableString_ShouldFollowTheFrozenTruthTable()
    {
        Ids(r => r.Where(x => SqlFunctions.Sql.is_not_distinct_from(x.S1, x.S2)).Select(x => x.Id))
            .Should().Equal(1, 3);
        Ids(r => r.Where(x => SqlFunctions.Sql.is_distinct_from(x.S1, x.S2)).Select(x => x.Id))
            .Should().Equal(2, 4, 5);
    }

    [Fact]
    public void InMemory_ReferenceNull_ShouldNotDereference()
    {
        var r = Rows()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                NotDistinct = SqlFunctions.Sql.is_not_distinct_from(x.S1, x.S2),
                Distinct = SqlFunctions.Sql.is_distinct_from(x.S1, x.S2)
            })
            .ToList();

        r.Select(x => x.NotDistinct).Should().Equal(true, false, true, false, false);
        r.Select(x => x.Distinct).Should().Equal(false, true, false, true, true);
    }

    [Fact]
    public void InMemory_Distinct_ShouldBeTheComplementOfNotDistinct()
    {
        var r = Rows()
            .Select(x => new
            {
                NotDistinct = SqlFunctions.Sql.is_not_distinct_from(x.N1, x.N2),
                Distinct = SqlFunctions.Sql.is_distinct_from(x.N1, x.N2)
            })
            .ToList();

        r.Should().HaveCount(5);
        r.Should().OnlyContain(x => x.NotDistinct != x.Distinct);
    }

    [Fact]
    public void InMemory_CapturedNullParameter_ShouldRebindWithoutSpecialisation()
    {
        int? captured = null;
        var query = Rows().Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, captured)).OrderBy(x => x.Id).Select(x => x.Id);

        // A captured NULL stays a parameter: the helper then asks "is the column NULL".
        query.ToList().Should().Equal(3, 4);

        captured = 1;
        query.ToList().Should().Equal(1, 2, 5);

        captured = null;
        query.ToList().Should().Equal(3, 4);

        string? capturedText = null;
        var textQuery = Rows().Where(x => SqlFunctions.Sql.is_distinct_from(x.S1, capturedText)).OrderBy(x => x.Id).Select(x => x.Id);

        textQuery.ToList().Should().Equal(1, 2, 5);

        capturedText = "x";
        textQuery.ToList().Should().Equal(3, 4);

        capturedText = null;
        textQuery.ToList().Should().Equal(1, 2, 5);
    }

    // --- SQL dispatch, folding and diagnostics (Q01/Q04) ---------------------------------------

    [Fact]
    public void Sql_BothForms_ShouldRenderThroughTheDialectCapability()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, x.N2))
            .Select(x => x.Id))
            .Should().Contain("is not distinct from");

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from(x.N1, x.N2))
            .Select(x => x.Id))
            .Should().Contain(" is distinct from ");
    }

    [Fact]
    public void Sql_LiteralNull_ShouldLowerToPresenceTest()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, null))
            .Select(x => x.Id))
            .Should().Contain("is null").And.NotContain("is not distinct from");

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from(x.N1, null))
            .Select(x => x.Id))
            .Should().Contain("is not null").And.NotContain("is distinct from");

        // A typed NULL and default(T?) produce the same shape.
        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.S1, (string?)null))
            .Select(x => x.Id))
            .Should().Contain("is null").And.NotContain("is not distinct from");

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from(x.N1, default(int?)))
            .Select(x => x.Id))
            .Should().Contain("is not null").And.NotContain("is distinct from");
    }

    [Fact]
    public void Sql_TwoLiteralNulls_ShouldFoldToAConstant()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from<int?>(null, null))
            .Select(x => x.Id))
            .Should().Contain("1 = 1").And.NotContain("is not distinct from");

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from<int?>(null, default))
            .Select(x => x.Id))
            .Should().Contain("1 = 0").And.NotContain("is distinct from");
    }

    [Fact]
    public void Sql_IncompatibleOperands_ShouldThrowTheDeterministicDiagnostic()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);

        var act = () => SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from((object?)x.S1, (object?)x.N1))
            .Select(x => x.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Null-safe comparison requires compatible operand types.");
    }

    [Fact]
    public void Sql_NonNullableValueOperands_ShouldTakeTheOrdinaryComparisonFastPath()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);
        int left = 1;
        int right = 2;

        var notDistinct = SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(left, right) && x.Id > 0)
            .Select(x => x.Id));

        notDistinct.Should().Contain(" = ").And.NotContain("is not distinct from");

        var distinct = SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from(left, right) && x.Id > 0)
            .Select(x => x.Id));

        distinct.Should().Contain(" <> ").And.NotContain("is distinct from");
    }

    [Fact]
    public void Sql_ColumnsAndReferences_ShouldKeepTheNullSafePath()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);

        // A column of a non-nullable CLR value type can still be SQL NULL (a nullable column or
        // outer-join null extension), so it never takes the ordinary-comparison fast path.
        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.Id, x.Id))
            .Select(x => x.Id))
            .Should().Contain("is not distinct from");

        // An NRT-annotated non-nullable reference is not runtime proof either.
        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.Name, x.Name))
            .Select(x => x.Id))
            .Should().Contain("is not distinct from").And.NotContain("t288_compare.name = t288_compare.name");
    }

    [Fact]
    public void Sql_OrdinaryEqualityAndInequality_ShouldNotBeRewritten()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => x.N1 == x.N2)
            .Select(x => x.Id))
            .Should().Contain(" = ").And.NotContain("is not distinct from");

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => x.N1 != x.N2)
            .Select(x => x.Id))
            .Should().Contain("!=").And.NotContain("is distinct from");
    }

    [Fact]
    public void Sql_CapturedNullableParameter_ShouldKeepOneCachedSqlShape()
    {
        using var ctx = new RenderingContext(SupportedDialect.Instance);
        int? captured = null;

        var first = CommandOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, captured))
            .Select(x => x.Id));

        captured = 1;

        var second = CommandOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, captured))
            .Select(x => x.Id));

        second.CommandText.Should().Be(first.CommandText);
        second.CommandText.Should().Contain("is not distinct from");
        ParameterValues(first).Should().ContainSingle().Which.Should().Be(DBNull.Value);
        ParameterValues(second).Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public void Sql_BooleanProjection_ShouldShapeThroughTheDialect()
    {
        using var ctx = new RenderingContext(BitShapingDialect.Instance);

        var sql = SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Select(x => new { V = SqlFunctions.Sql.is_not_distinct_from(x.N1, x.N2) }));

        sql.Should().Contain("cast(case when").And.Contain("is not distinct from");
    }

    // --- Capability seam ------------------------------------------------------------------------

    [Fact]
    public void Capability_Default_ShouldBeUnsupportedAndFailClosed()
    {
        new DefaultCapabilityDialect().NullSafeComparison.Should().BeNull();

        using var ctx = new RenderingContext(DefaultCapabilityDialect.Instance);

        var notDistinct = () => SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, x.N2))
            .Select(x => x.Id));

        notDistinct.Should().Throw<NotSupportedException>()
            .WithMessage("*is_not_distinct_from*DefaultCapabilityDialect*");

        var distinct = () => SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from(x.N1, x.N2))
            .Select(x => x.Id));

        distinct.Should().Throw<NotSupportedException>()
            .WithMessage("*is_distinct_from*DefaultCapabilityDialect*");
    }

    [Fact]
    public void Capability_Forms_ShouldBeIndependent()
    {
        INullSafeComparisonRenderer notDistinctOnly = NotDistinctOnlyRenderer.Instance;
        notDistinctOnly.SupportsNotDistinct.Should().BeTrue();
        notDistinctOnly.SupportsDistinct.Should().BeFalse();
        NotDistinctOnlyRenderer.Instance.NotDistinctDisposition.Should().Be(NullSafeComparisonDisposition.Native);

        using var ctx = new RenderingContext(NotDistinctOnlyDialect.Instance);

        SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.N1, x.N2))
            .Select(x => x.Id))
            .Should().Contain("is not distinct from");

        var act = () => SqlOf(ctx, ctx.From<T288ComparisonEntity>()
            .Where(x => SqlFunctions.Sql.is_distinct_from(x.N1, x.N2))
            .Select(x => x.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*is_distinct_from*NotDistinctOnlyDialect*");
    }

    // --- Harness --------------------------------------------------------------------------------

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => CommandOf(ctx, cmd).CommandText.Replace("\r\n", "\n");

    private static DbCommand CommandOf<T>(IDataContext ctx, QueryCommand<T> cmd) =>
        ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand;

    private static object?[] ParameterValues(DbCommand command)
        => command.Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray();

    /// <summary>A dialect with no null-safe-comparison capability: the fail-closed default.</summary>
    private sealed class DefaultCapabilityDialect : SqlDialectBase
    {
        internal static readonly DefaultCapabilityDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A dialect that supports both forms with an ANSI-style spelling.</summary>
    private sealed class SupportedDialect : SqlDialectBase
    {
        internal static readonly SupportedDialect Instance = new();

        public override INullSafeComparisonRenderer? NullSafeComparison => BothFormsRenderer.Instance;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A dialect that supports only the not-distinct form.</summary>
    private sealed class NotDistinctOnlyDialect : SqlDialectBase
    {
        internal static readonly NotDistinctOnlyDialect Instance = new();

        public override INullSafeComparisonRenderer? NullSafeComparison => NotDistinctOnlyRenderer.Instance;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A dialect without a Boolean type, like SQL Server: a scalar Boolean needs a CASE.</summary>
    private sealed class BitShapingDialect : SqlDialectBase
    {
        internal static readonly BitShapingDialect Instance = new();

        public override INullSafeComparisonRenderer? NullSafeComparison => BothFormsRenderer.Instance;

        public override string MakeBooleanPredicate(string predicate, bool asPredicate) =>
            asPredicate ? predicate : $"cast(case when {predicate} then 1 else 0 end as bit)";

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class BothFormsRenderer : INullSafeComparisonRenderer
    {
        internal static readonly BothFormsRenderer Instance = new();

        public NullSafeComparisonDisposition DistinctDisposition => NullSafeComparisonDisposition.Emulated;

        public NullSafeComparisonDisposition NotDistinctDisposition => NullSafeComparisonDisposition.Emulated;

        public string RenderDistinct(string left, string right) => $"({left} is distinct from {right})";

        public string RenderNotDistinct(string left, string right) => $"({left} is not distinct from {right})";
    }

    private sealed class NotDistinctOnlyRenderer : INullSafeComparisonRenderer
    {
        internal static readonly NotDistinctOnlyRenderer Instance = new();

        public NullSafeComparisonDisposition NotDistinctDisposition => NullSafeComparisonDisposition.Native;

        public string RenderDistinct(string left, string right) =>
            throw new InvalidOperationException("The distinct form is not supported by this renderer.");

        public string RenderNotDistinct(string left, string right) => $"({left} is not distinct from {right})";
    }

    /// <summary>
    /// Render-only context: the provider connection object is never opened (the SQL is read from the
    /// prepared command before execution), and the dialect under test supplies the rendering.
    /// </summary>
    private sealed class RenderingContext(ISqlDialect dialect)
        : DataContext(null, new SqliteConnection("Data Source=:memory:"), new DataContextBuilder())
    {
        public override ISqlDialect Dialect => dialect;

        public override DbParameter CreateParam(string name, object? value) => new SqliteParameter(name, value ?? DBNull.Value);

        protected override DbConnection CreateDbConnection(string? connectionString) =>
            new SqliteConnection(connectionString);
    }
}

/// <summary>T288 in-memory fixture: two nullable numeric and two nullable string comparison operands.</summary>
[SqlTable("t288_compare")]
public class T288ComparisonEntity
{
    public int Id { get; set; }

    public int? N1 { get; set; }

    public int? N2 { get; set; }

    public string? S1 { get; set; }

    public string? S2 { get; set; }

    /// <summary>NRT-annotated non-nullable reference that can still hold NULL at runtime.</summary>
    public string Name { get; set; } = null!;
}

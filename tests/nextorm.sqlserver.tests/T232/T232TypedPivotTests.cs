using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D-04 (#232) typed SQL Server <c>PIVOT</c>/<c>UNPIVOT</c> provider overloads: the exact SQL they
/// project (the frozen typed-column oracle of <see cref="T232ProjectionContractTests"/> is the typed
/// projection the overload feeds to <c>Select</c>), the supported shapes/cells, composition
/// (<c>Where</c>/<c>OrderBy</c>/<c>Select</c>, derived inner) and every fail-closed guard.
/// </summary>
/// <remarks>
/// The typed overload returns a builder over the typed projection; that projection is a derived
/// source, so these tests consume it through an explicit <c>Select</c> before materialization.
/// Projection-free terminal materialization of the returned builder is outside T232's scope; that
/// contract does not relax typed <c>Where</c>/<c>OrderBy</c>/<c>Select</c>, derived-source/CTE
/// composition, the frozen SQL or the declared shape and cell types. These tests never open a
/// connection.
/// </remarks>
public class T232TypedPivotTests
{
    /// <summary>A named class projection (object-initializer form).</summary>
    private sealed class PivotShape
    {
        public string? Category { get; set; }

        public decimal? Q1 { get; set; }
    }

    /// <summary>A positional record projection.</summary>
    private sealed record PivotRow(string? Category, decimal? Q1);

    /// <summary>A positional record-struct projection.</summary>
    private readonly record struct PivotStructRow(string? Category, decimal? Q1);

    /// <summary>A projection with no members, used for the empty-shape guard.</summary>
    private sealed class EmptyShape
    {
    }

    /// <summary>
    /// A named derived projection whose <see cref="Unprojected"/> member is never selected: it lets a
    /// typed projection name a CLR member that has no derived output column, so the unresolvable
    /// grouping-member branch of the typed rewrite is exercised rather than silently defaulted.
    /// </summary>
    private sealed class DerivedRow
    {
        public int Id { get; set; }

        public string? Unprojected { get; set; }
    }

    /// <summary>An entity whose column name is resolved through the active naming convention.</summary>
    [SqlTable("t232_typed_widget")]
    private interface IWidget
    {
        [Key]
        int Id { get; set; }

        decimal? MarginValue { get; set; }

        string? Region { get; set; }
    }

    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    /// <summary>The typed PIVOT select the frozen contract pins (the rewritten projection).</summary>
    private const string FrozenTypedPivotSelect = T232ProjectionContractTests.TypedColumnPivotSql;

    /// <summary>The typed UNPIVOT select the frozen contract pins (the rewritten projection).</summary>
    private const string FrozenTypedUnpivotSelect = T232ProjectionContractTests.TypedColumnUnpivotSql;

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // ------------------------------------------------------------------ V01: exact typed SQL

    [Fact]
    public void TypedPivot_EmitsTheFrozenTypedProjection()
    {
        using var ctx = SqlServerTestContext.Create();

        var typed = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("2") });

        var sql = SqlOf(ctx, typed.Select(x => new { x.Category, x.Q1, x.Q2 }));

        // The rewritten typed projection is byte-identical to the frozen oracle: bare grouping column,
        // every pivoted cell renamed to its projected member, and the declared value order in IN (...).
        sql.Should().Contain(FrozenTypedPivotSelect);
        sql.Should().Contain(T232ProjectionContractTests.PivotValueOrderFragment);
        sql.Should().Be($"select [category], [Q1], [Q2]\n from ({FrozenTypedPivotSelect}) as [t2]");
    }

    [Fact]
    public void TypedUnpivot_EmitsTheFrozenTypedProjection()
    {
        using var ctx = SqlServerTestContext.Create();

        var typed = ctx.From<IQuarterlyEntity>()
            .Unpivot(
                (m, u) => new { m.Category, Quarter = u.Name<string>("qtr"), Measure = u.Value<decimal?>("val") },
                UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"));

        var sql = SqlOf(ctx, typed.Select(x => new { x.Category, x.Quarter, x.Measure }));

        sql.Should().Contain(FrozenTypedUnpivotSelect);
        sql.Should().Contain($"unpivot ([{T232ProjectionContractTests.UnpivotValueRole}] for [{T232ProjectionContractTests.UnpivotNameRole}] in ([q1], [q2]))");
        sql.Should().Be($"select [category], [Quarter], [Measure]\n from ({FrozenTypedUnpivotSelect}) as [t2]");
    }

    [Fact]
    public void TypedPivot_DeclarationOrderDrivesBothTheInListAndTheProjection()
    {
        using var ctx = SqlServerTestContext.Create();

        // NewExpression argument order: Q2 before Category before Q1.
        var typed = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { Q2 = pv.Value<decimal?>("2"), m.Category, Q1 = pv.Value<decimal?>("1") });

        var sql = SqlOf(ctx, typed.Select(x => new { x.Q2, x.Category, x.Q1 }));

        sql.Should().Contain("for quarter in ([2], [1])", "the IN list follows the projection declaration order");
        sql.Should().Contain("select [2] as [Q2], [category], [1] as [Q1]");
    }

    [Fact]
    public void TypedPivot_MemberInitDeclarationOrderDrivesTheInList()
    {
        using var ctx = SqlServerTestContext.Create();

        var typed = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new PivotShape { Q1 = pv.Value<decimal?>("1"), Category = m.Category });

        var sql = SqlOf(ctx, typed.Select(x => new { x.Q1, x.Category }));

        sql.Should().Contain("for quarter in ([1])");
        sql.Should().Contain("select [1] as [Q1], [category]");
    }

    // ------------------------------------------------------------------ V06: grouping subsets

    [Fact]
    public void TypedPivot_SubsetAndFullGrouping_AreSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        // A subset of the grouping members is allowed; the unused inner members stay grouping inputs.
        var subset = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { Q1 = pv.Value<decimal?>("1") })
            .Select(x => new { x.Q1 }));
        subset.Should().Contain("from (select [1] as [Q1]\n from sales pivot (sum(margin) for quarter in ([1])) as [t1]) as [t2]");

        var full = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Id, m.Category, Q1 = pv.Value<decimal?>("1") })
            .Select(x => new { x.Id, x.Category, x.Q1 }));
        full.Should().Contain("select [id], [category], [1] as [Q1]\n from sales pivot (sum(margin) for quarter in ([1])) as [t1]");
    }

    // ------------------------------------------------------------------ V03/V04: grouping names

    [Fact]
    public void TypedPivot_EntityInner_UsesTheMappedColumnName()
    {
        using var ctx = SqlServerTestContext.Create();

        // ISalesEntity maps Category -> [Column("category")]; the grouping reference must use it.
        var sql = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.V }));

        sql.Should().Contain("select [category], [1] as [V]\n from sales pivot (sum(margin) for quarter in ([1])) as [t1]");
    }

    [Fact]
    public void TypedPivot_EntityInner_UsesTheNamingConventionColumnName()
    {
        using var ctx = new SqlServerDataContext(
            PlaceholderConnectionString,
            new DataContextBuilder().UseNamingConvention(SnakeCaseNamingConvention.Instance));

        // Region/MarginValue are auto-mapped, so the active convention resolves their SQL names both in
        // the grouping reference and in the pivot clause.
        var sql = SqlOf(ctx, ctx.From<IWidget>()
            .Pivot(PivotAggregate.Sum, w => w.MarginValue, w => w.Id, (m, pv) => new { m.Region, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Region, x.V }));

        sql.Should().Contain("select [region], [1] as [V]");
        sql.Should().Contain("from t232_typed_widget pivot (sum(margin_value) for id in ([1])) as [t1]");
    }

    [Fact]
    public void TypedPivot_DerivedInner_UsesTheSqlVisibleProjectionName()
    {
        using var ctx = SqlServerTestContext.Create();

        var derived = ctx.From<ISalesEntity>()
            .Where(s => s.Margin > 0)
            .Select(s => new { CategoryName = s.Category, Quarter = s.Quarter, Margin = s.Margin });

        // CategoryName is renamed by the derived projection, so the alias is the SQL-visible name;
        // Quarter keeps its mapped name.
        var sql = SqlOf(ctx, ctx.From(derived)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter,
                (m, pv) => new { m.CategoryName, Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("2") })
            .Select(x => new { x.CategoryName, x.Q1, x.Q2 }));

        sql.Should().Contain("select [CategoryName], [1] as [Q1], [2] as [Q2]\n from (select category as [CategoryName], quarter, margin");
        sql.Should().Contain("pivot (sum(margin) for quarter in ([1], [2])) as [t2]");
    }

    // ------------------------------------------------------------------ V11/V12: shapes and cells

    [Fact]
    public void TypedPivot_SupportsEveryProjectionShape()
    {
        using var ctx = SqlServerTestContext.Create();

        const string oneValueSelect = "select [category], [1] as [Q1]\n from sales pivot (sum(margin) for quarter in ([1])) as [t1]";

        // record / record struct / MemberInit all project the same shape.
        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new PivotRow(m.Category, pv.Value<decimal?>("1")))
            .Select(x => new { x.Category, x.Q1 }))
            .Should().Contain(oneValueSelect);

        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new PivotStructRow(m.Category, pv.Value<decimal?>("1")))
            .Select(x => new { x.Category, x.Q1 }))
            .Should().Contain(oneValueSelect);

        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new PivotShape { Category = m.Category, Q1 = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.Q1 }))
            .Should().Contain(oneValueSelect);

        // scalar: no member name is invented, so no rename alias is emitted for the cell; the derived
        // boundary still declares the generated output identifier the consumer references (`[c0]`), so
        // the scalar cell carries that declaration (T312).
        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => pv.Value<decimal?>("1"))
            .Select(x => x))
            .Should().Contain("from (select [1] as [c0]\n from sales pivot (sum(margin) for quarter in ([1])) as [t1]) as [t2]");
    }

    [Fact]
    public void TypedPivot_CellTypeDeclarationDrivesTheProjectedClrType()
    {
        using var ctx = SqlServerTestContext.Create();

        // The declared cell type is read off the reflected shape member of the returned builder
        // (not off the anonymous shape type itself): a nullable aggregate cell, a non-nullable COUNT
        // cell and a reference UNPIVOT cell.
        CellTypeOf(ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") }), "V")
            .Should().Be(typeof(decimal?));

        CellTypeOf(ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Count, s => s.Id, s => s.Quarter, (m, pv) => new { m.Category, N = pv.Value<int>("1") }), "N")
            .Should().Be(typeof(int));

        CellTypeOf(ctx.From<IQuarterlyEntity>()
            .Unpivot((m, u) => new { N = u.Name<string>("qtr"), V = u.Value<string?>("val") }, UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2")), "N")
            .Should().Be(typeof(string));

        // The declared cell type reaches the SQL unchanged.
        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Count, s => s.Id, s => s.Quarter, (m, pv) => new { m.Category, N = pv.Value<int>("1") })
            .Select(x => new { x.Category, x.N }))
            .Should().Contain("select [category], [1] as [N]\n from sales pivot (count(id) for quarter in ([1])) as [t1]");
    }

    /// <summary>Reads the CLR type declared for one cell member of the returned typed shape.</summary>
    private static Type CellTypeOf<TShape>(EntityBuilder<TShape> _, string cellMember)
        => typeof(TShape).GetProperty(cellMember)?.PropertyType
            ?? throw new InvalidOperationException($"The typed shape '{typeof(TShape)}' declares no cell member '{cellMember}'.");

    [Fact]
    public void TypedPivot_NonNumericValueName_IsEscapedLikeTheInList()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Category, (m, pv) => new { m.Quarter, North = pv.Value<decimal?>("North") })
            .Select(x => new { x.Quarter, x.North }));

        sql.Should().Contain("for category in ([North])");
        sql.Should().Contain("select [quarter], [North]\n from sales pivot (sum(margin) for category in ([North])) as [t1]");
    }

    // ------------------------------------------------------------------ V13: composition

    [Fact]
    public void TypedPivot_ComposesWithWhereOrderByAndSelect()
    {
        using var ctx = SqlServerTestContext.Create();

        var typed = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        var composed = SqlOf(ctx, typed
            .Where(x => x.Q1 > 0)
            .OrderBy(x => x.Category)
            .Select(x => new { x.Category, x.Q1 }));

        composed.Should().Contain("where (t2.[Q1] > ");
        composed.Should().Contain("order by t2.[category]");

        // Ordering by a value member, and a scalar select of a value member.
        SqlOf(ctx, typed.OrderBy(x => x.Q1).Select(x => new { x.Category, x.Q1 })).Should().Contain("order by t2.[Q1]");
        SqlOf(ctx, typed.Select(x => x.Q1)).Should().Contain("select [Q1]\n from (");
    }

    // ------------------------------------------------------------------ V16/V17: role/name forms

    [Fact]
    public void TypedUnpivot_RepeatedConsistentRoleReferences_ReuseTheRole()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IQuarterlyEntity>()
            .Unpivot(
                (m, u) => new { m.Category, N = u.Name<string>("qtr"), A = u.Value<decimal?>("val"), B = u.Value<string?>("val") },
                UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(x => new { x.Category, x.N, x.A }));

        sql.Should().Contain("unpivot ([val] for [qtr] in ([q1], [q2]))");
        sql.Should().Contain("select [category], [qtr] as [N], [val] as [A], [val] as [B]");
    }

    [Fact]
    public void TypedPivot_CapturedValueName_IsSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var value = "1";
        var sql = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>(value) })
            .Select(x => new { x.Category, x.V }));

        sql.Should().Contain("[1] as [V]");
        sql.Should().Contain("for quarter in ([1])");
    }

    // ------------------------------------------------------------------ V24: cache locality

    [Fact]
    public void TypedPivot_RepeatedAndInterleavedQueries_ShareNoState()
    {
        using var ctx = SqlServerTestContext.Create();

        var first = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") });
        var second = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("2") });

        var firstSql = SqlOf(ctx, first.Select(x => new { x.Category, x.V }));
        var secondSql = SqlOf(ctx, second.Select(x => new { x.Category, x.V }));

        secondSql.Should().Contain("[2] as [V]").And.Contain("in ([2])");
        firstSql.Should().Contain("[1] as [V]").And.Contain("in ([1])");
        // Repeating the first query after the interleaved one yields the identical SQL: no sticky state.
        SqlOf(ctx, first.Select(x => new { x.Category, x.V })).Should().Be(firstSql);
    }

    // ------------------------------------------------------------------ fail-closed guards

    [Fact]
    public void TypedPivot_DuplicateValue_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("1") });

        act.Should().Throw<ArgumentException>().WithMessage("*duplicate*").WithMessage("*'1'*");
    }

    [Fact]
    public void TypedPivot_WithoutValues_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category });

        act.Should().Throw<ArgumentException>().WithMessage("*at least one value*");
    }

    [Fact]
    public void TypedPivot_EmptyShape_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new EmptyShape());

        act.Should().Throw<ArgumentException>().WithMessage("*empty*").WithMessage("*at least one value*");
    }

    [Fact]
    public void TypedPivot_ConsumedGroupingMember_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        // The aggregate column and the FOR column are not part of the PIVOT result.
        new Action(() => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Margin, V = pv.Value<decimal?>("1") }))
            .Should().Throw<ArgumentException>().WithMessage("*grouping*");

        new Action(() => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Quarter, V = pv.Value<decimal?>("1") }))
            .Should().Throw<ArgumentException>().WithMessage("*grouping*");

        // A converted access is recognised too.
        new Action(() => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { M = (decimal?)m.Margin, V = pv.Value<decimal?>("1") }))
            .Should().Throw<ArgumentException>().WithMessage("*grouping*");
    }

    [Fact]
    public void TypedPivot_RowDependentValueName_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>(m.Category!) });

        act.Should().Throw<ArgumentException>().WithMessage("*column name*");
    }

    [Fact]
    public void TypedPivot_ProjectionWithoutAValueDeclaration_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        // A non-empty projection that declares no pivot.Value<T> at all — here it only references the
        // inner source as a whole — is rejected by the first-firing value guard
        // (PivotProjectionRewriter.cs:54-57), before the whole-source reference is ever rewritten.
        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { M = m });

        act.Should().Throw<ArgumentException>().WithMessage("*requires at least one value*");
    }

    [Fact]
    public void TypedPivot_WholeInnerSource_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        // A valid value declaration does not mask the branch under test: the direct-member rewrite
        // still rejects the bare whole-source reference (PivotProjectionRewriter.cs:329-330).
        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, M = m, V = pv.Value<decimal?>("1") });

        act.Should().Throw<NotSupportedException>().WithMessage("*direct member*");
    }

    [Fact]
    public void TypedUnpivot_ZeroColumns_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<IQuarterlyEntity>()
            .Unpivot((m, u) => new { m.Category, N = u.Name<string>("qtr"), V = u.Value<decimal?>("val") });

        act.Should().Throw<ArgumentException>().WithMessage("*at least one column*");
    }

    [Fact]
    public void TypedUnpivot_MissingAndConflictingRoles_FailClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        new Action(() => ctx.From<IQuarterlyEntity>()
            .Unpivot((m, u) => new { m.Category, V = u.Value<decimal?>("val") }, UnpivotColumn.Create("q1")))
            .Should().Throw<ArgumentException>().WithMessage("*role*");

        new Action(() => ctx.From<IQuarterlyEntity>()
            .Unpivot((m, u) => new { m.Category, V = u.Value<decimal?>("val"), N = u.Name<string>("qtr"), Other = u.Name<string>("other") },
                UnpivotColumn.Create("q1")))
            .Should().Throw<ArgumentException>().WithMessage("*role*");
    }

    [Fact]
    public void TypedPivot_NullArguments_FailClosed()
    {
        using var ctx = SqlServerTestContext.Create();
        Expression<Func<ISalesEntity, object?>> column = s => s.Margin;

        new Action(() => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, null!, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") }))
            .Should().Throw<ArgumentNullException>();

        new Action(() => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, column, null!, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") }))
            .Should().Throw<ArgumentNullException>();

        new Action(() => ctx.From<ISalesEntity>()
            .Pivot<ISalesEntity, object>(PivotAggregate.Sum, column, s => s.Quarter, null!))
            .Should().Throw<ArgumentNullException>();

        new Action(() => ctx.From<ISalesEntity>()
            .Unpivot<ISalesEntity, object>(null!, UnpivotColumn.Create("q1")))
            .Should().Throw<ArgumentNullException>();

        new Action(() => ctx.From<IQuarterlyEntity>()
            .Unpivot((m, u) => new { m.Category, V = u.Value<decimal?>("val") }, null!))
            .Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------ D-05: branch closure

    [Fact]
    public void TypedPivot_EmptyMarkerValueName_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        // The empty name is rejected by the same fail-closed read that rejects a row-dependent name.
        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("") });

        act.Should().Throw<ArgumentException>().WithMessage("*column name*");
    }

    [Fact]
    public void TypedPivot_DerivedInner_MissingGroupingMember_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        // The derived projection selects only Id; Unprojected is a member of the CLR shape but not a
        // derived output column, so no SQL name can be resolved for it — the rewrite must fail closed
        // instead of defaulting to the CLR name.
        var derived = ctx.From<ISalesEntity>().Where(s => s.Margin > 0).Select(s => new DerivedRow { Id = s.Id });

        var act = () => ctx.From(derived)
            .Pivot(PivotAggregate.Sum, x => x.Id, x => x.Id, (m, pv) => new { m.Unprojected, V = pv.Value<decimal?>("1") });

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*registered column name*");
    }

    [Fact]
    public void TypedPivot_PlainSourceWithModifier_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISalesEntity>()
            .Where(s => s.Margin > 0)
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") });

        act.Should().Throw<NotSupportedException>().WithMessage("*plain table*");
    }

    [Fact]
    public void TypedPivot_DerivedSourceWithModifier_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var derived = ctx.From<ISalesEntity>().Where(s => s.Margin > 0).Select(s => new { s.Category, s.Quarter, s.Margin });

        var act = () => ctx.From(derived)
            .Where(x => x.Margin > 0)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") });

        act.Should().Throw<NotSupportedException>().WithMessage("*derived query*");
    }
}

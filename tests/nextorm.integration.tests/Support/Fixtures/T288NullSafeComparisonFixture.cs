using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T288 (#288) live fixture and scenario body for the explicit null-safe comparison helpers
/// (<c>is_not_distinct_from</c> / <c>is_distinct_from</c>). One five-row fixture carries the frozen truth
/// table for a nullable numeric, a nullable string and a mapped enum column; the shared partial and the
/// dedicated ClickHouse/MariaDB classes only differ in how they obtain their context.
/// <para>
/// The semantic oracle is by <b>rows</b>, never by SQL text: not-distinct must return <c>[1,3]</c>,
/// distinct <c>[2,4,5]</c>, and the ordinary <c>=</c>/<c>&lt;&gt;</c> controls <c>[1]</c>/<c>[2]</c> in the
/// same execution. The harness owns the two tables (created, seeded and dropped per scenario), so it
/// never depends on the provider's shared seed data.
/// </para>
/// </summary>
[SqlTable("t288_nsc")]
public interface IT288NullSafeComparisonRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>Nullable numeric left operand.</summary>
    [Column("a")]
    int? A { get; set; }

    /// <summary>Nullable numeric right operand.</summary>
    [Column("b")]
    int? B { get; set; }

    /// <summary>Nullable string left operand.</summary>
    [Column("s1")]
    string? S1 { get; set; }

    /// <summary>Nullable string right operand.</summary>
    [Column("s2")]
    string? S2 { get; set; }

    /// <summary>Mapped enum (stored as the provider integer of its underlying type).</summary>
    [Column("state")]
    T288NullSafeState? State { get; set; }

    /// <summary>Second mapped enum operand.</summary>
    [Column("state2")]
    T288NullSafeState? State2 { get; set; }
}

/// <summary>T288 fixture entity: the concrete row the insert builder writes.</summary>
[SqlTable("t288_nsc")]
public sealed class T288NullSafeComparisonRow : IT288NullSafeComparisonRow
{
    /// <inheritdoc/>
    public int Id { get; set; }

    /// <inheritdoc/>
    public int? A { get; set; }

    /// <inheritdoc/>
    public int? B { get; set; }

    /// <inheritdoc/>
    public string? S1 { get; set; }

    /// <inheritdoc/>
    public string? S2 { get; set; }

    /// <inheritdoc/>
    public T288NullSafeState? State { get; set; }

    /// <inheritdoc/>
    public T288NullSafeState? State2 { get; set; }
}

/// <summary>T288 fixture enum: a mapped value whose provider representation is its underlying integer.</summary>
public enum T288NullSafeState
{
    /// <summary>Zero.</summary>
    None = 0,

    /// <summary>One.</summary>
    One = 1,

    /// <summary>Two.</summary>
    Two = 2,
}

/// <summary>T288 outer-join fixture: an optional row for ids 1 and 2 only.</summary>
[SqlTable("t288_nsc_opt")]
public interface IT288NullSafeOptionalRow
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>A non-nullable CLR column that a left join null-extends.</summary>
    [Column("ref_id")]
    int RefId { get; set; }
}

/// <summary>T288 outer-join fixture entity.</summary>
[SqlTable("t288_nsc_opt")]
public sealed class T288NullSafeOptionalRow : IT288NullSafeOptionalRow
{
    /// <inheritdoc/>
    public int Id { get; set; }

    /// <inheritdoc/>
    public int RefId { get; set; }
}

/// <summary>
/// T288 live scenario body, shared by the four <c>CommonTestSuite</c> derivatives and the two dedicated
/// provider classes. Every assertion is by row identity; the ordinary comparison controls run in the same
/// execution as the null-safe form so the red/green pair is one boundary execution.
/// </summary>
internal static class T288NullSafeComparisonFixture
{
    internal const string Table = "t288_nsc";
    internal const string OptionalTable = "t288_nsc_opt";

    /// <summary>Creates and seeds the five-row truth-table fixture, dropping any previous copy.</summary>
    internal static void Reset(IDataContext ctx)
    {
        var dialect = ((DataContext)ctx).Dialect;

        Exec(ctx, $"drop table if exists {Table}");
        Exec(ctx, $"create table {Table} (" +
                  $"id {IntType(dialect)}, " +
                  $"a {IntType(dialect)}, " +
                  $"b {IntType(dialect)}, " +
                  $"s1 {TextType(dialect)}, " +
                  $"s2 {TextType(dialect)}, " +
                  $"state {IntType(dialect)}, " +
                  $"state2 {IntType(dialect)}){EngineSuffix(dialect)}");

        // Seeded with one literal multi-row insert: the fixture pins the comparison semantics, so the
        // seed must not depend on a driver's parameter typing for the mapped enum column.
        Exec(ctx, $"insert into {Table} (id, a, b, s1, s2, state, state2) values " +
                  "(1, 1, 1, 'x', 'x', 1, 1), " +
                  "(2, 1, 2, 'x', 'y', 1, 2), " +
                  "(3, null, null, null, null, null, null), " +
                  "(4, null, 1, null, 'x', null, 1), " +
                  "(5, 1, null, 'x', null, 1, null)");
    }

    /// <summary>Creates and seeds the optional table used by the outer-join scenario.</summary>
    internal static void ResetOptional(IDataContext ctx)
    {
        var dialect = ((DataContext)ctx).Dialect;

        Exec(ctx, $"drop table if exists {OptionalTable}");
        Exec(ctx, $"create table {OptionalTable} (id {IntType(dialect)}, ref_id {IntType(dialect)}){EngineSuffix(dialect)}");

        Exec(ctx, $"insert into {OptionalTable} (id, ref_id) values (1, 1), (2, 2)");
    }

    /// <summary>Drops both fixture tables.</summary>
    internal static void Cleanup(IDataContext ctx)
    {
        Exec(ctx, $"drop table if exists {OptionalTable}");
        Exec(ctx, $"drop table if exists {Table}");
    }

    /// <summary>
    /// The frozen truth table by rows, for the nullable numeric and the nullable string operands, in a
    /// predicate and in a scalar projection, with the ordinary comparison controls in the same execution.
    /// </summary>
    internal static void TruthTable(IDataContext ctx)
    {
        Reset(ctx);
        try
        {
            var e = ctx.From<IT288NullSafeComparisonRow>();

            // Predicate form: not-distinct [1,3], distinct [2,4,5], ordinary controls [1] / [2].
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.A, x.B)).Should().Equal(1, 3);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.A, x.B)).Should().Equal(2, 4, 5);
            Ids(ctx, x => x.A == x.B).Should().Equal(1);
            Ids(ctx, x => x.A != x.B).Should().Equal(2);

            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.S1, x.S2)).Should().Equal(1, 3);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.S1, x.S2)).Should().Equal(2, 4, 5);
            Ids(ctx, x => x.S1 == x.S2).Should().Equal(1);
            Ids(ctx, x => x.S1 != x.S2).Should().Equal(2);

            // Mapped enum column (provider integer representation), same truth table.
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.State, x.State2)).Should().Equal(1, 3);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.State, x.State2)).Should().Equal(2, 4, 5);

            // Scalar projection: the result is two-valued (never null/unknown) and matches the table.
            // The ordinary comparison is deliberately pinned in the predicate form above: as a scalar it
            // keeps SQL NULL/UNKNOWN, which is exactly the pre-change behaviour T288 must not alter and
            // which cannot materialise into a non-nullable bool.
            var rows = e.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    Nd = SqlFunctions.Sql.is_not_distinct_from(x.A, x.B),
                    D = SqlFunctions.Sql.is_distinct_from(x.A, x.B),
                })
                .ToList();

            rows.Should().HaveCount(5);
            rows.Select(x => x.Nd).Should().Equal(true, false, true, false, false);
            rows.Select(x => x.D).Should().Equal(false, true, false, true, true);
            rows.Should().OnlyContain(x => x.Nd != x.D);

            // The string projection is two-valued and null-safe too.
            var text = e.OrderBy(x => x.Id)
                .Select(x => new { x.Id, Nd = SqlFunctions.Sql.is_not_distinct_from(x.S1, x.S2) })
                .ToList();
            text.Select(x => x.Nd).Should().Equal(true, false, true, false, false);

            // The enum projection proves a mapped value keeps the null-safe semantics.
            var mapped = e.OrderBy(x => x.Id)
                .Select(x => new { x.Id, Nd = SqlFunctions.Sql.is_not_distinct_from(x.State, x.State2) })
                .ToList();
            mapped.Select(x => x.Nd).Should().Equal(true, false, true, false, false);
        }
        finally
        {
            Cleanup(ctx);
        }
    }

    /// <summary>
    /// Column–parameter, reversed operands and parameter–parameter: the captured operand stays a boundary
    /// parameter (its value is never inlined) and both operand orders agree.
    /// </summary>
    internal static void ColumnParameterAndReversed(IDataContext ctx)
    {
        Reset(ctx);
        try
        {
            int? captured = null;

            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.A, captured)).Should().Equal(3, 4);
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(captured, x.A)).Should().Equal(3, 4);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.A, captured)).Should().Equal(1, 2, 5);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(captured, x.A)).Should().Equal(1, 2, 5);

            captured = 1;
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.A, captured)).Should().Equal(1, 2, 5);
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(captured, x.A)).Should().Equal(1, 2, 5);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.A, captured)).Should().Equal(3, 4);

            string? capturedText = null;
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.S1, capturedText)).Should().Equal(3, 4);
            capturedText = "x";
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.S1, capturedText)).Should().Equal(1, 2, 5);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.S1, capturedText)).Should().Equal(3, 4);

            // Parameter–parameter: two captured operands compare as one boundary value pair.
            int? first = null;
            int? second = null;
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(first, second) && x.Id > 0).Should().Equal(1, 2, 3, 4, 5);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(first, second) && x.Id > 0).Should().BeEmpty();

            first = 1;
            second = 1;
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(first, second) && x.Id > 0).Should().Equal(1, 2, 3, 4, 5);

            second = 2;
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(first, second) && x.Id > 0).Should().BeEmpty();
        }
        finally
        {
            Cleanup(ctx);
        }
    }

    /// <summary>
    /// Both parameters <c>NULL</c> and then non-<c>NULL</c> on the same context and the same query shape:
    /// the statement is reused with rebinding, so no value-dependent SQL may be cached. This is the SQL
    /// Server emulation's risk case (an untyped NULL operand would either fail or freeze the value).
    /// </summary>
    internal static void CapturedNullThenValueRebinding(IDataContext ctx)
    {
        Reset(ctx);
        try
        {
            int? left = null;
            int? right = null;

            var query = ctx.From<IT288NullSafeComparisonRow>()
                .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.A, left) && SqlFunctions.Sql.is_not_distinct_from(x.B, right) && x.Id > 0)
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            // Both NULL: rows 3 and 4 have a NULL `a`, and `b` is NULL only on row 3 and 5.
            query.ToList().Should().Equal(3);

            left = 1;
            right = 1;
            query.ToList().Should().Equal(1);

            right = null;
            query.ToList().Should().Equal(5);

            left = null;
            query.ToList().Should().Equal(3);

            // Parameter–parameter on the same context: both NULL is true for every row, then a
            // non-NULL pair narrows without changing the statement.
            int? p1 = null;
            int? p2 = null;
            var pair = ctx.From<IT288NullSafeComparisonRow>()
                .Where(x => SqlFunctions.Sql.is_not_distinct_from(p1, p2) && x.Id > 0)
                .Select(x => x.Id);

            pair.ToList().Should().HaveCount(5);

            p1 = 7;
            pair.ToList().Should().BeEmpty();
        }
        finally
        {
            Cleanup(ctx);
        }
    }

    /// <summary>Literal NULL, typed NULL and <c>default</c> fold to the presence test and return rows.</summary>
    internal static void LiteralNullAndDefault(IDataContext ctx)
    {
        Reset(ctx);
        try
        {
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.A, null)).Should().Equal(3, 4);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.A, null)).Should().Equal(1, 2, 5);
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.S1, (string?)null)).Should().Equal(3, 4);
            Ids(ctx, x => SqlFunctions.Sql.is_distinct_from(x.A, default(int?))).Should().Equal(1, 2, 5);
            Ids(ctx, x => SqlFunctions.Sql.is_not_distinct_from(x.State, default(T288NullSafeState?))).Should().Equal(3, 4);
        }
        finally
        {
            Cleanup(ctx);
        }
    }

    /// <summary>
    /// A left join null-extends the joined row, so the non-nullable CLR column of the right table is NULL
    /// at runtime. The null-safe form must treat those rows as distinct from a non-NULL operand instead of
    /// dropping them through an UNKNOWN result — that is the guard against the ordinary-comparison fast
    /// path on a null-extended operand.
    /// </summary>
    internal static void OuterJoinNullExtension(IDataContext ctx)
    {
        Reset(ctx);
        ResetOptional(ctx);
        try
        {
            // Joined column against itself: matched rows compare their values, unmatched rows compare two
            // NULLs — so the null-safe form keeps all five rows while ordinary equality keeps only the
            // two matched ones.
            var joined = ctx.From<IT288NullSafeComparisonRow>()
                .LeftJoin(ctx.From<IT288NullSafeOptionalRow>(), (l, r) => l.Id == r.Id);

            var nullSafe = joined
                .Where(p => SqlFunctions.Sql.is_not_distinct_from(p.Item2.RefId, p.Item2.RefId))
                .Select(p => p.Item1.Id)
                .ToList()
                .OrderBy(x => x)
                .ToArray();
            nullSafe.Should().Equal(1, 2, 3, 4, 5);

            var distinct = joined
                .Where(p => SqlFunctions.Sql.is_distinct_from(p.Item2.RefId, p.Item2.RefId))
                .Select(p => p.Item1.Id)
                .ToList();
            distinct.Should().BeEmpty();

            var control = joined
                .Where(p => p.Item2.RefId == p.Item2.RefId)
                .Select(p => p.Item1.Id)
                .ToList()
                .OrderBy(x => x)
                .ToArray();
            control.Should().Equal(1, 2);

            // A non-NULL operand against the null-extended column: the null-safe form answers two-valued
            // (false for the unmatched rows) instead of UNKNOWN.
            var captured = 2;
            var againstValue = joined
                .Where(p => SqlFunctions.Sql.is_not_distinct_from(p.Item2.RefId, captured))
                .Select(p => p.Item1.Id)
                .ToList();
            againstValue.Should().Equal(2);

            var distinctAgainstValue = joined
                .Where(p => SqlFunctions.Sql.is_distinct_from(p.Item2.RefId, captured))
                .Select(p => p.Item1.Id)
                .ToList()
                .OrderBy(x => x)
                .ToArray();
            distinctAgainstValue.Should().Equal(1, 3, 4, 5);
        }
        finally
        {
            Cleanup(ctx);
        }
    }

    /// <summary>
    /// The same context and the same query shape across executions: the plan cache is reused without a
    /// sticky mutation, and the shared terminal commands (<c>Any</c>/<c>Count</c>) keep working after a
    /// null-safe query, so no command state leaked.
    /// </summary>
    internal static void SharedCommandAndCacheReuse(IDataContext ctx)
    {
        Reset(ctx);
        try
        {
            int? captured = null;
            var query = ctx.From<IT288NullSafeComparisonRow>()
                .Where(x => SqlFunctions.Sql.is_not_distinct_from(x.A, captured))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);

            query.ToList().Should().Equal(3, 4);
            query.ToList().Should().Equal(3, 4);

            // The shared Any/Count commands on the same context are unaffected by the null-safe query.
            ctx.From<IT288NullSafeComparisonRow>().Where(x => x.Id > 0).Any().Should().BeTrue();
            ctx.From<IT288NullSafeComparisonRow>().Where(x => x.Id > 0).Count().Should().Be(5);

            captured = 1;
            query.ToList().Should().Equal(1, 2, 5);
            query.ToList().Should().Equal(1, 2, 5);

            ctx.From<IT288NullSafeComparisonRow>().Where(x => x.Id > 0).Any().Should().BeTrue();

            captured = null;
            query.ToList().Should().Equal(3, 4);
        }
        finally
        {
            Cleanup(ctx);
        }
    }

    private static int[] Ids(IDataContext ctx, System.Linq.Expressions.Expression<Func<IT288NullSafeComparisonRow, bool>> filter)
        => ctx.From<IT288NullSafeComparisonRow>()
            .Where(filter)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList()
            .ToArray();

    // The DDL type is spelled per provider: MakeResultColumnType is a result-cast helper, and its MySQL
    // answer ('signed') is not a valid column type in a create table.
    private static string IntType(ISqlDialect dialect) => dialect.GetType().Name switch
    {
        "SqlServerDialect" => "int",
        "MySqlDialect" or "MariaDbDialect" => "int",
        "ClickHouseDialect" => "Nullable(Int32)",
        _ => "integer",
    };

    private static string TextType(ISqlDialect dialect) => dialect.GetType().Name switch
    {
        "SqlServerDialect" => "nvarchar(16)",
        "MySqlDialect" or "MariaDbDialect" => "varchar(16)",
        "ClickHouseDialect" => "Nullable(String)",
        _ => "text",
    };

    private static string EngineSuffix(ISqlDialect dialect) =>
        dialect.GetType().Name == "ClickHouseDialect" ? " engine = Memory" : string.Empty;

    private static void Exec(IDataContext ctx, string sql)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}

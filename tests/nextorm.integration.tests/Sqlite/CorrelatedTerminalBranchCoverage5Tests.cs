using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Correlated-terminal branch coverage (cycle 5, cluster C4) executed against a live SQLite database.
/// Every fact drives a real correlated query over class-owned marker rows of <c>ct5_parent</c> /
/// <c>ct5_child</c> / <c>ct5_note</c> (dropped, created and seeded by this class's own DDL in the
/// constructor) and asserts the returned values, including the childless parent by value
/// (<c>0</c>/<c>NULL</c>/<c>false</c>).
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>CorrelatedQueryExpressionVisitor.VisitMember</c>
/// (:445-454) for the <c>Count</c> PROPERTY of a declared <c>ICollection</c> navigation inside a
/// projection; <c>LowerCollectionTerminal</c> (:544) for the parameterless <c>Any()</c>/<c>Count()</c>/
/// <c>LongCount()</c> method terminals in projections AND predicates;
/// <c>LowerAdapterTerminal</c> (:524-542) for the <c>AsEntityBuilder()</c> adapter on a collection
/// navigation (Any/Count/LongCount, projection and predicate); <c>AggregateTerminalRewriter.Rewrite</c>
/// (:22-81) for builder aggregate terminals inside a PROJECTION only (Count/Min/Max/Sum over nullable
/// columns, Avg/Sum/Min/Max over non-nullable ones); <c>VisitBinary</c> (:1684) for nullable-vs-
/// non-nullable comparisons, null-constant polarity, AndAlso/OrElse combinations and a captured outer
/// parameter; plus the outer-parameter rewriting of a captured local used inside the correlated
/// predicate and inside the projection on ONE context (A-&gt;A-&gt;B-&gt;A).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>CorrelatedBranchCoverage2Tests</c> owns the
/// collection terminals over a NULLABLE foreign key, the <c>Count</c> property in a PREDICATE, the
/// debug cache-miss log and the member-init projection; <c>CorrelatedAggregateBranchCoverage4Tests</c>
/// owns the aggregate rewriter over a child set filtered by <c>Amount != null</c> and one- and two-level
/// reference hops (which are #304-affected and are NOT used here);
/// <c>CorrelatedSubquerySweepTests</c> owns the terminal matrix over the shared seed tables. No
/// reference-navigation hop (<c>parent.Group.Name</c>) appears in this class: #304 is an open defect.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class CorrelatedTerminalBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists ct5_note;
        drop table if exists ct5_child;
        drop table if exists ct5_parent;
        create table ct5_parent
        (
            id integer primary key,
            name text not null,
            bucket int null,
            qty int not null
        );
        create table ct5_child
        (
            id integer primary key,
            parent_id int not null,
            amount int null,
            weight int not null,
            tag text null
        );
        create table ct5_note
        (
            id integer primary key,
            parent_id int not null,
            body text not null
        );
        -- 9001: two children and two notes; 9002: one child with NULL amount/tag and no note;
        -- 9003: no child at all but one note; 9004: one child and one note.
        insert into ct5_parent (id, name, bucket, qty) values
            (9001, 'p-rich', 1, 10),
            (9002, 'p-null-amount', null, 20),
            (9003, 'p-empty', 2, 30),
            (9004, 'p-one', 1, 5);
        insert into ct5_child (id, parent_id, amount, weight, tag) values
            (9101, 9001, 5, 3, 'a'),
            (9102, 9001, 7, 4, 'a'),
            (9103, 9002, null, 6, null),
            (9104, 9004, 9, 9, 'd');
        insert into ct5_note (id, parent_id, body) values
            (9201, 9001, 'n1'),
            (9202, 9001, 'n2'),
            (9203, 9003, 'n3'),
            (9204, 9004, 'n4');
        """;

    public CorrelatedTerminalBranchCoverage5Tests()
    {
        ExecuteRaw(Ddl);

        // The collection navigations must be registered before any mapping of the parent type, so the
        // members are recognized as navigations and not as physical columns.
        Context.From<Ct5Note>();
        Context.From<Ct5Child>();
        Context.From<Ct5Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
    }

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------------------------------------
    // 1. The Count PROPERTY of a collection navigation inside a projection, over two collections
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>p.Children.Count</c> / <c>p.Notes.Count</c> are <c>ICollection.Count</c>
    /// property reads inside a PROJECTION, lowered by <c>VisitMember</c> (:445-454); the neighbour only
    /// drove the property inside a predicate and never over a second collection, so the value of both
    /// collections distinguishes 9003 (no child, one note) from 9002 (one child, no note).
    /// </summary>
    [Fact]
    public void CollectionCountPropertyInProjection_ShouldCountEachNavigationIndependently()
    {
        var rows = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Children = p.Children.Count, Notes = p.Notes.Count })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(9001, 9002, 9003, 9004);
        rows.Select(r => r.Children).Should().Equal(2, 1, 0, 1);
        rows.Select(r => r.Notes).Should().Equal(new[] { 2, 0, 1, 1 },
            "the note count of the childless parent is 1, so the two navigations never share a correlation");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Any/Count/LongCount method terminals inside a projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the parameterless method terminals in a projection
    /// (<c>LowerCollectionTerminal</c> :544) including the 64-bit <c>LongCount</c> over both declared
    /// collections; the childless parent is asserted by value (<c>false</c>/<c>0</c>/<c>0L</c>).
    /// </summary>
    [Fact]
    public void CollectionMethodTerminalsInProjection_ShouldReportPresenceAndCounts()
    {
        var rows = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Has = p.Children.Any(),
                C = p.Children.Count(),
                L = p.Children.LongCount(),
                Notes = p.Notes.LongCount(),
            })
            .ToList();

        rows.Select(r => r.Has).Should().Equal(true, true, false, true);
        rows.Select(r => r.C).Should().Equal(2, 1, 0, 1);
        rows.Select(r => r.L).Should().Equal(2L, 1L, 0L, 1L);
        rows.Select(r => r.Notes).Should().Equal(2L, 0L, 1L, 1L);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Any/Count/LongCount method terminals inside predicates
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the method terminals as PREDICATES combined with <c>&amp;&amp;</c> - <c>Any()</c>
    /// plus <c>Count()</c> over the same navigation, then <c>LongCount() == 0</c> for the childless parent
    /// by value. <c>Count() &gt; 1</c> in a predicate is owned by the neighbour and is not repeated.
    /// </summary>
    [Fact]
    public void CollectionMethodTerminalsInPredicate_ShouldFilterOnPresenceAndCount()
    {
        var two = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004 && p.Children.Any() && p.Children.Count() == 2)
            .Select(p => p.Id)
            .ToList();
        two.Should().Equal(new[] { 9001 }, "only 9001 has two children");

        var empty = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004 && p.Children.LongCount() == 0)
            .Select(p => p.Id)
            .ToList();
        empty.Should().Equal(new[] { 9003 }, "9003 is the only parent without children");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. The AsEntityBuilder adapter in a projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>LowerAdapterTerminal</c> (:524-542) takes its collection branch for
    /// <c>parent.Children.AsEntityBuilder().Any()/Count()/LongCount()</c> and
    /// <c>parent.Notes.AsEntityBuilder().LongCount()</c> inside a projection; the neighbour's adapter
    /// fact only reads <c>Any()</c> over the shared seed graph.
    /// </summary>
    [Fact]
    public void AdapterTerminalsInProjection_ShouldLowerEachCollectionTerminal()
    {
        var rows = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Has = p.Children.AsEntityBuilder().Any(),
                C = p.Children.AsEntityBuilder().Count(),
                L = p.Children.AsEntityBuilder().LongCount(),
                Notes = p.Notes.AsEntityBuilder().LongCount(),
            })
            .ToList();

        rows.Select(r => r.Has).Should().Equal(true, true, false, true);
        rows.Select(r => r.C).Should().Equal(2, 1, 0, 1);
        rows.Select(r => r.L).Should().Equal(2L, 1L, 0L, 1L);
        rows.Select(r => r.Notes).Should().Equal(2L, 0L, 1L, 1L);
    }

    // ---------------------------------------------------------------------------------------------
    // 5. The AsEntityBuilder adapter in a predicate
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the adapter path in a PREDICATE, where the two adapted collections must be
    /// correlated independently: 9002 has a child but no note, so only it satisfies
    /// <c>Children.AsEntityBuilder().Any() &amp;&amp; Notes.AsEntityBuilder().LongCount() == 0</c>.
    /// </summary>
    [Fact]
    public void AdapterTerminalsInPredicate_ShouldCorrelateEachCollectionIndependently()
    {
        var rows = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004
                && p.Children.AsEntityBuilder().Any()
                && p.Notes.AsEntityBuilder().LongCount() == 0)
            .Select(p => p.Id)
            .ToList();

        rows.Should().Equal(new[] { 9002 },
            "9002 has a child and no note; 9003 has a note and no child");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Aggregate terminals on a correlated subquery builder, projection only, nullable results
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>AggregateTerminalRewriter.Rewrite</c> (:22-81) inside a PROJECTION over
    /// nullable columns - the neighbour always filters <c>Amount != null</c>, whereas 9002 owns exactly
    /// one child whose amount is NULL, so the non-empty all-NULL set arm (NULL/0) is asserted here next
    /// to the childless 9003 (NULL/0). No aggregate terminal is placed in a WHERE predicate (#303).
    /// </summary>
    [Fact]
    public void CorrelatedAggregateTerminalsInProjection_ShouldReportNullableAggregatesForAllNullAndEmptySets()
    {
        var rows = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                C = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Count(),
                MinAmount = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Min(c => c.Amount),
                MaxAmount = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Max(c => c.Amount),
                SumAmount = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount),
            })
            .ToList();

        rows.Should().HaveCount(4);
        rows[0].C.Should().Be(2);
        rows[0].MinAmount.Should().Be(5);
        rows[0].MaxAmount.Should().Be(7);
        rows[0].SumAmount.Should().Be(12, "5 + 7");

        rows[1].C.Should().Be(1, "the child of 9002 exists, so the count is one");
        rows[1].MinAmount.Should().BeNull("MIN over a set whose only amount is NULL is NULL");
        rows[1].MaxAmount.Should().BeNull();
        rows[1].SumAmount.Should().BeNull("SUM ignores the NULL amount and yields NULL");

        rows[2].C.Should().Be(0, "9003 has no children");
        rows[2].MinAmount.Should().BeNull("MIN over an empty set is NULL");
        rows[2].MaxAmount.Should().BeNull();
        rows[2].SumAmount.Should().BeNull();

        rows[3].C.Should().Be(1);
        rows[3].MinAmount.Should().Be(9);
        rows[3].MaxAmount.Should().Be(9);
        rows[3].SumAmount.Should().Be(9);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Aggregate terminals, projection only, non-nullable results over a non-empty set
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the non-nullable selector arms of the rewriter
    /// (<c>AggregateMethodFor</c>/<c>SelectMethodFor</c>, :56-69) - <c>Avg</c>/<c>Sum</c>/<c>Min</c>/
    /// <c>Max</c> over <c>Weight</c> for the one parent that has children, so the result types are
    /// <c>int?</c>/<c>double?</c> and the values are exact.
    /// </summary>
    [Fact]
    public void CorrelatedAggregateTerminalsInProjection_ShouldReportNonNullableAggregates()
    {
        var row = Context.From<Ct5Parent>()
            .Where(p => p.Id == 9001)
            .Select(p => new
            {
                AvgWeight = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Avg(c => (double)c.Weight),
                SumWeight = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Weight),
                MinWeight = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Min(c => c.Weight),
                MaxWeight = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Max(c => c.Weight),
            })
            .ToList();

        row.Should().ContainSingle();
        row[0].AvgWeight.Should().Be(3.5, "(3 + 4) / 2");
        row[0].SumWeight.Should().Be(7);
        row[0].MinWeight.Should().Be(3);
        row[0].MaxWeight.Should().Be(4);
    }

    // ---------------------------------------------------------------------------------------------
    // 8. VisitBinary: nullable versus non-nullable comparison inside the correlation
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the correlated predicate compares a NULLABLE inner column with a
    /// NON-NULLABLE outer column (<c>c.Amount &lt; p.Qty</c> / <c>&gt;=</c>), so both directions of a
    /// lifted comparison are rendered; the NULL amount of 9002 never matches, and the two queries
    /// partition 9001 and 9004.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_NullableAgainstNonNullable_ShouldCompareWithOuterColumn()
    {
        var less = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Ct5Child>().Where(c => c.ParentId == p.Id && c.Amount < p.Qty)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        less.Should().Equal(new[] { 9001 }, "5 and 7 are below the outer qty 10; 9002 is NULL and 9004 is 9 vs 5");

        var atLeast = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Ct5Child>().Where(c => c.ParentId == p.Id && c.Amount >= p.Qty)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        atLeast.Should().Equal(new[] { 9004 }, "9 is at least the outer qty 5");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. VisitBinary: null-constant polarity and OrElse inside the correlation
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: both polarities of the lifted null comparison inside the correlated
    /// predicate (<c>Amount == null</c> and <c>Amount != null</c>), then an <c>OrElse</c> that mixes the
    /// null test with an outer-column comparison (<c>Weight &gt; p.Qty</c>).
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_NullConstantPolarityAndOrElse_ShouldMatchTheExpectedParents()
    {
        var isNull = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Ct5Child>().Where(c => c.ParentId == p.Id && c.Amount == null)))
            .Select(p => p.Id)
            .ToList();
        isNull.Should().Equal(new[] { 9002 }, "only 9002 owns a child whose amount is NULL");

        var notNull = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Ct5Child>().Where(c => c.ParentId == p.Id && c.Amount != null)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        notNull.Should().Equal(new[] { 9001, 9004 });

        var either = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Ct5Child>().Where(c => c.ParentId == p.Id && (c.Amount == null || c.Weight > p.Qty))))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        either.Should().Equal(new[] { 9002, 9004 }, "the NULL amount or a weight above the outer qty");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. A captured local inside the correlated predicate and inside the projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: one captured local is bound in the correlated inner predicate
    /// (<c>c.Amount &gt;= floor</c>) and read again in the outer projection (<c>Floor = floor</c>), so the
    /// same closure member is rewritten through both the inner command and the outer projection.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_WithCapturedLocalInPredicateAndProjection_ShouldBindBothPlaces()
    {
        var floor = 5;

        var rows = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Ct5Child>().Where(c => c.ParentId == p.Id && c.Amount >= floor)))
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Floor = floor,
                C = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Count(),
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(new[] { 9001, 9004 }, "the NULL amount of 9002 never matches");
        rows.Select(r => r.Floor).Should().Equal(5, 5);
        rows.Select(r => r.C).Should().Equal(2, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Different captured values ON ONE context (A -> A -> B -> A)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the same correlated shape is prepared and executed four times on ONE context
    /// with two different captured thresholds (A=5, B=8). Only the parameter value changes, so the SQL
    /// shape is identical and no plan-shape distinction is claimed; only the live rows are asserted.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_WithDifferentCapturedValues_ShouldFollowTheValueOnOneContext()
    {
        List<int> Run(int minAmount) => Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Ct5Child>().Where(c => c.ParentId == p.Id && c.Amount >= minAmount)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        Run(5).Should().Equal(new[] { 9001, 9004 }, "A: amounts 5/7/9 reach the threshold 5");
        Run(5).Should().Equal(new[] { 9001, 9004 }, "A again: the cached preparation still binds the value");
        Run(8).Should().Equal(new[] { 9004 }, "B: only the amount 9 reaches the threshold 8");
        Run(5).Should().Equal(new[] { 9001, 9004 }, "A once more: the first shape still returns its rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. The Count property of a navigation compared against a captured threshold in a predicate
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the <c>Count</c> property inside a predicate compared against a CAPTURED
    /// threshold (the neighbour compares it with a literal), so the wide count is narrowed and then
    /// compared to a bound parameter; the childless parent is reported at the zero threshold.
    /// </summary>
    [Fact]
    public void CollectionCountPropertyInPredicate_AgainstCapturedThreshold_ShouldFilterByChildCount()
    {
        List<int> Run(int minChildren) => Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004 && p.Children.Count >= minChildren)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        Run(2).Should().Equal(new[] { 9001 });
        Run(0).Should().Equal(new[] { 9001, 9002, 9003, 9004 }, "a zero threshold admits the childless parent as well");
        Run(2).Should().Equal(new[] { 9001 });
    }

    // ---------------------------------------------------------------------------------------------
    // 13. Typed aggregate terminal overloads in a correlated projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>AggregateTerminalRewriter.Rewrite</c> (:22-47) with typed selector
    /// overloads that the class does not yet use - a <c>long?</c> <c>Min</c> over the non-nullable
    /// weight, a <c>double?</c> <c>Sum</c> and <c>double?</c> <c>Avg</c> over the NULLABLE amount and a
    /// <c>double?</c> <c>Avg</c> over the non-nullable weight - so <c>AggregateMethodFor</c> is driven
    /// with both nullable result types. Every selector is projected as nullable so the childless 9003
    /// really yields NULL (a non-nullable projection could only ever return a default value) and the
    /// all-NULL-amount 9002 is asserted next to it. The aggregate terminals stay in a PROJECTION only
    /// (never a predicate, #303).
    /// </summary>
    [Fact]
    public void CorrelatedAggregateTerminals_TypedOverloadsInProjection_ShouldReportExactValues()
    {
        var rows = Context.From<Ct5Parent>()
            .Where(p => p.Id >= 9001 && p.Id <= 9004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                MinWeight = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Min(c => (long?)c.Weight),
                SumAmount = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Sum(c => (double?)c.Amount),
                AvgAmount = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Avg(c => (double?)c.Amount),
                AvgWeight = Context.From<Ct5Child>().Where(c => c.ParentId == p.Id).Avg(c => (double?)c.Weight),
            })
            .ToList();

        rows.Should().HaveCount(4);

        rows[0].MinWeight.Should().Be(3L, "min of the weights 3 and 4");
        rows[0].SumAmount.Should().Be(12.0, "5 + 7");
        rows[0].AvgAmount.Should().Be(6.0, "(5 + 7) / 2");
        rows[0].AvgWeight.Should().Be(3.5, "(3 + 4) / 2");

        rows[1].MinWeight.Should().Be(6L);
        rows[1].SumAmount.Should().BeNull("SUM ignores the only NULL amount and yields NULL");
        rows[1].AvgAmount.Should().BeNull("AVG ignores the only NULL amount and yields NULL");
        rows[1].AvgWeight.Should().Be(6.0, "the nullable AMOUNT does not affect the non-null weight");

        rows[2].MinWeight.Should().BeNull("MIN over an empty set is NULL");
        rows[2].SumAmount.Should().BeNull("9003 has no child at all");
        rows[2].AvgAmount.Should().BeNull();
        rows[2].AvgWeight.Should().BeNull();

        rows[3].MinWeight.Should().Be(9L);
        rows[3].SumAmount.Should().Be(9.0);
        rows[3].AvgAmount.Should().Be(9.0);
        rows[3].AvgWeight.Should().Be(9.0);
    }

    /// <summary>Parent marker row of the class-owned <c>ct5_parent</c> table.</summary>
    [SqlTable("ct5_parent")]
    public sealed class Ct5Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("bucket")]
        public int? Bucket { get; set; }

        [Column("qty")]
        public int Qty { get; set; }

        public ICollection<Ct5Child> Children { get; } = new List<Ct5Child>();

        public ICollection<Ct5Note> Notes { get; } = new List<Ct5Note>();
    }

    /// <summary>Child marker row of the class-owned <c>ct5_child</c> table.</summary>
    [SqlTable("ct5_child")]
    public sealed class Ct5Child
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }

        [Column("weight")]
        public int Weight { get; set; }

        [Column("tag")]
        public string? Tag { get; set; }
    }

    /// <summary>Note marker row of the class-owned <c>ct5_note</c> table (the second collection).</summary>
    [SqlTable("ct5_note")]
    public sealed class Ct5Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("body")]
        public string Body { get; set; } = string.Empty;
    }
}

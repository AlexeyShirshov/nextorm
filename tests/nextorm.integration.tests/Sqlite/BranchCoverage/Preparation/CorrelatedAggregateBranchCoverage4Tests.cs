using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Correlated-aggregate coverage (cycle 4, cluster D5) executed against a live SQLite database. Every
/// fact drives a real correlated query over class-owned marker rows of <c>c4_parent</c>/<c>c4_child</c>
/// (with the declared <c>Children</c> collection and the <c>Group</c>/<c>Root</c> reference chain over
/// <c>c4_group</c>/<c>c4_root</c>), created and seeded by this class's own DDL in the constructor, and
/// asserts the returned values - including the value of the parent that has no children.
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>AggregateTerminalRewriter.Rewrite</c> (:22) -
/// the <c>Count</c> branch (:30-35) and the selector branch (:37-44) with <c>UnwrapLambda</c> (:49), the
/// <c>AggregateMethodFor</c> Sum/Min/Max arms (:56-67) and <c>SelectMethodFor</c> (:69) - for nullable
/// and non-nullable result types over empty and non-empty child sets;
/// <c>CorrelatedQueryExpressionVisitor</c>: the outer-parameter capture inside a correlated scalar
/// subquery predicate (<c>GetQueryCommand</c>/<c>ReplaceQueryCommand</c> path), the typed null constant
/// arm, the result-typed aggregate placeholder in a WHERE predicate (#303), and the reference-hop
/// rewrite of a one- and a two-level navigation chain (<c>RewriteOuterReference</c> :90,
/// <c>VisitMember</c> :445).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>CorrelatedBranchCoverage2Tests</c> owns the
/// collection <c>Any</c>/<c>Count</c>/<c>LongCount</c> terminals and the <c>Count</c> property in
/// predicates/projections over a nullable foreign key, the debug cache-miss log and the member-init
/// projection; <c>CorrelatedSubquerySweepTests</c> owns the terminal matrix over the shared seed tables;
/// <c>CoreTerminalSweepTests</c> and <c>CommonTestSuite.Aggregates</c> own the uncorrelated aggregate
/// terminals. This class instead drives the aggregate-terminal REWRITER (builder receiver inside a
/// correlated projection/predicate), the typed-null arm and the nested reference hops over class-owned
/// rows.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class CorrelatedAggregateBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists c4_child;
        drop table if exists c4_parent;
        drop table if exists c4_group;
        drop table if exists c4_root;
        create table c4_root
        (
            id integer primary key,
            label text not null
        );
        create table c4_group
        (
            id integer primary key,
            code text not null,
            root_id int not null
        );
        create table c4_parent
        (
            id integer primary key,
            name text not null,
            bucket int null,
            group_id int not null,
            qty int not null
        );
        create table c4_child
        (
            id integer primary key,
            parent_id int not null,
            amount int null,
            weight int not null,
            tag text null,
            group_code text null
        );
        insert into c4_root (id, label) values (1, 'root-a'), (2, 'root-b');
        insert into c4_group (id, code, root_id) values (1, 'alpha', 1), (2, 'beta', 2);
        -- 8001: two children with amounts; 8002: one child with a NULL amount; 8003: no child at all.
        -- 8104 is a grandchild (its parent_id is the child 8101), so the nested correlation has a match.
        insert into c4_parent (id, name, bucket, group_id, qty) values
            (8001, 'p-rich', 1, 1, 10),
            (8002, 'p-null-amount', null, 1, 20),
            (8003, 'p-empty', 2, 2, 30);
        insert into c4_child (id, parent_id, amount, weight, tag, group_code) values
            (8101, 8001, 5, 3, 'root-a', 'alpha'),
            (8102, 8001, 7, 4, 'root-a', 'beta'),
            (8103, 8002, null, 6, 'root-b', 'alpha'),
            (8104, 8101, 3, 5, 'grand', 'alpha');
        """;

    public CorrelatedAggregateBranchCoverage4Tests()
    {
        ExecuteRaw(Ddl);

        // Register the reference chain (parent -> group -> root) before the parent mapping, so the
        // navigation properties are recognised as relationships and not as physical columns.
        Context.From<C4Root>();
        Context.From<C4Group>(b => b.HasOne(g => g.Root, g => g.RootId));
        Context.From<C4Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasOne(p => p.Group, p => p.GroupId));
        Context.From<C4Child>();
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
    // 1. The aggregate-terminal rewriter in a projection: Count vs selector aggregates, empty vs
    //    non-empty child sets, nullable vs non-nullable result types
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: <c>AggregateTerminalRewriter.Rewrite</c>'s <c>Count</c> branch is exercised
    /// next to the selector branch (Sum and Max through <c>AggregateMethodFor</c>/<c>SelectMethodFor</c>)
    /// over a builder receiver with the outer parent captured in the inner predicate. The childless
    /// parent's VALUE is asserted (0 / null), not merely the absence of a throw; the non-nullable
    /// <c>Sum(weight)</c> runs in a second query over the parent that has children.
    /// </summary>
    [Fact]
    public void CorrelatedAggregateInProjection_ShouldReportCountSumAndMaxForEmptyAndNonEmptySets()
    {
        var rows = Context.From<C4Parent>()
            .Where(p => p.Id >= 8001 && p.Id <= 8003)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                C = Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count(),
                S = Context.From<C4Child>().Where(c => c.ParentId == p.Id && c.Amount != null).Sum(c => c.Amount),
                M = Context.From<C4Child>().Where(c => c.ParentId == p.Id && c.Amount != null).Max(c => c.Amount),
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].C.Should().Be(2, "parent 8001 owns two children");
        rows[0].S.Should().Be(12, "5 + 7");
        rows[0].M.Should().Be(7);
        rows[1].C.Should().Be(1);
        rows[1].S.Should().BeNull("the only child of parent 8002 has a NULL amount");
        rows[1].M.Should().BeNull();
        rows[2].C.Should().Be(0, "parent 8003 has no children, and the COUNT value is 0");
        rows[2].S.Should().BeNull("SUM over an empty set is NULL");
        rows[2].M.Should().BeNull("MAX over an empty set is NULL");

        // A non-nullable result type over a non-empty set (the Sum/Weight selector path).
        var nonNullable = Context.From<C4Parent>()
            .Where(p => p.Id == 8001)
            .Select(p => new
            {
                W = Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Weight),
                N = Context.From<C4Child>().Where(c => c.ParentId == p.Id).Min(c => c.Weight),
            })
            .ToList();

        nonNullable.Should().ContainSingle();
        nonNullable[0].W.Should().Be(7, "3 + 4 over the two children of parent 8001");
        nonNullable[0].N.Should().Be(3);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. The same rewriter re-prepared after an outer-filter change on ONE context (A->A->B->B->A)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: the rewritten aggregate projection is re-executed on ONE context with a
    /// changed outer filter (plan-cache hit, then miss, then hit again) and the childless parent's
    /// VALUE is asserted on every step. The builder aggregate terminal in a WHERE predicate is lowered
    /// by the same rewriter on the public path since #303; the live predicate facts are the next
    /// section, and the same-context alternating-shape case is asserted there.
    /// </summary>
    [Fact]
    public void CorrelatedAggregateInProjection_ShouldFollowTheOuterFilterOnTheSameContext()
    {
        List<(int Id, int C, int? S)> Run(int maxId) => Context.From<C4Parent>()
            .Where(p => p.Id >= 8001 && p.Id <= maxId)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                C = Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count(),
                S = Context.From<C4Child>().Where(c => c.ParentId == p.Id && c.Amount != null).Sum(c => c.Amount),
            })
            .ToList()
            .Select(r => (r.Id, r.C, r.S))
            .ToList();

        Run(8001).Should().Equal(new[] { (8001, 2, (int?)12) }, "the narrowed shape is prepared first");
        Run(8001).Should().Equal(new[] { (8001, 2, (int?)12) }, "the same shape is served by the cached preparation");

        var widened = Run(8003);
        widened.Should().Equal(
          new[] { (8001, 2, (int?)12), (8002, 1, (int?)null), (8003, 0, (int?)null) },
          "the widened shape is prepared again and the childless parent still reports 0 / NULL");

        Run(8001).Should().Equal(new[] { (8001, 2, (int?)12) }, "the first shape is correct again after the changed one");
    }

    // ---------------------------------------------------------------------------------------------
    // 2b. The same aggregate terminal lowered inside a WHERE predicate (#303)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// #303: a builder aggregate terminal inside a WHERE predicate is lowered to the same correlated
    /// scalar subquery the projection form uses, so the issue repro returns exactly the parent whose
    /// child amounts sum above the threshold. Parent 8002 owns only a NULL-amount child and parent 8003
    /// owns no child at all: neither may match a positive threshold, because SQL NULL is not zero.
    /// </summary>
    [Fact]
    public void AggregateTerminalInPredicate_ShouldReturnExactlyTheIssueSeedRows()
    {
        var aboveSix = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        aboveSix.Should().Equal(new[] { 8001 }, "5 + 7 = 12 > 6; 8002's only amount is NULL and 8003 has no child");

        var aboveZero = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 0)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        aboveZero.Should().Equal(new[] { 8001 }, "an all-NULL and an empty child set both yield NULL, which is not above 0");

        var atLeastZero = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount) >= 0)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        atLeastZero.Should().Equal(new[] { 8001 }, "a NULL sum does not satisfy an ordered comparison with 0 either");

        var twoChildren = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() > 1)
            .Select(p => p.Id)
            .ToList();
        twoChildren.Should().Equal(new[] { 8001 });

        var exactlyTwo = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() == 2)
            .Select(p => p.Id)
            .ToList();
        exactlyTwo.Should().Equal(new[] { 8001 });

        var notTwo = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() != 2)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        notTwo.Should().Equal(new[] { 8002, 8003 }, "the equality arm keeps its own comparison semantics");
    }

    /// <summary>
    /// #303: the predicate form composes with the logical, arithmetic, reversed and selector-aggregate
    /// operators the projection form already supported; every expectation is a row value, not a
    /// throw. <c>Min</c>/<c>Max</c>/<c>Avg</c> are read through the nullable amount, so the all-NULL
    /// child set of 8002 and the empty set of 8003 never qualify.
    /// </summary>
    [Fact]
    public void AggregateTerminalInPredicate_ShouldComposeWithLogicalArithmeticAndSelectorForms()
    {
        var and = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6
                && Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() > 1)
            .Select(p => p.Id)
            .ToList();
        and.Should().Equal(new[] { 8001 });

        var or = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6
                || Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() == 0)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        or.Should().Equal(new[] { 8001, 8003 }, "8003 matches the zero-count arm");

        var negated = Context.From<C4Parent>()
            .Where(p => !(Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() > 1))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        negated.Should().Equal(new[] { 8002, 8003 });

        var arithmetic = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() + 1 > 2)
            .Select(p => p.Id)
            .ToList();
        arithmetic.Should().Equal(new[] { 8001 });

        var reversed = Context.From<C4Parent>()
            .Where(p => 6 < Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount))
            .Select(p => p.Id)
            .ToList();
        reversed.Should().Equal(new[] { 8001 });

        var min = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Min(c => c.Amount) > 4)
            .Select(p => p.Id)
            .ToList();
        min.Should().Equal(new[] { 8001 }, "the minimum amount is 5; 8002 is NULL and 8003 is empty");

        var max = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Max(c => c.Weight) >= 4)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        max.Should().Equal(new[] { 8001, 8002 }, "weights 4 and 6 are the per-parent maxima");

        var avg = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Avg(c => c.Amount) > 5)
            .Select(p => p.Id)
            .ToList();
        avg.Should().Equal(new[] { 8001 }, "(5 + 7) / 2 = 6");
    }

    /// <summary>
    /// #303: correlation depth 2 with an aggregate predicate at each level - the outer subquery binds
    /// the parent row and the nested subquery binds its child row (8104 hangs below 8101), so the
    /// rewritten placeholders render one per level and the middle alias is not shared.
    /// </summary>
    [Fact]
    public void AggregateTerminalInPredicate_Depth2_ShouldBindBothLevels()
    {
        var rows = Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id
                    && Context.From<C4Child>().Where(d => d.ParentId == c.Id).Sum(d => d.Amount) > 0)
                .Count() > 0)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        rows.Should().Equal(new[] { 8001 },
            "child 8101 owns the grandchild 8104; 8002's child has none and 8003 has no child");
    }

    /// <summary>
    /// #303: the same-context A-&gt;A-&gt;B-&gt;B-&gt;A sequence over two aggregate-predicate shapes
    /// keeps every result and never disables the plan cache (<c>Cache</c> stays <c>true</c> on each
    /// prepared command - no sticky <c>Cache=false</c>).
    /// </summary>
    [Fact]
    public void AggregateTerminalInPredicate_SameContextAlternating_ShouldStayCorrectAndCached()
    {
        QueryCommand<int> ShapeA() => Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6)
            .OrderBy(p => p.Id)
            .Select(p => p.Id);

        QueryCommand<int> ShapeB() => Context.From<C4Parent>()
            .Where(p => Context.From<C4Child>().Where(c => c.ParentId == p.Id).Count() != 2)
            .OrderBy(p => p.Id)
            .Select(p => p.Id);

        static List<int> Execute(QueryCommand<int> command)
        {
            var rows = command.ToList();
            command.Cache.Should().BeTrue("preparing an aggregate predicate must not disable the plan cache");
            return rows;
        }

        Execute(ShapeA()).Should().Equal(new[] { 8001 });
        Execute(ShapeA()).Should().Equal(new[] { 8001 }, "the cached preparation still binds the outer row");
        Execute(ShapeB()).Should().Equal(new[] { 8002, 8003 });
        Execute(ShapeB()).Should().Equal(new[] { 8002, 8003 }, "the second shape is stable on reuse");
        Execute(ShapeA()).Should().Equal(new[] { 8001 }, "the first shape stays correct after the changed one");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Typed null constants and outer-parameter capture inside the correlated predicate
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: a TYPED null constant (<c>(int?)null</c>) compared against the inner nullable
    /// column inside a correlated predicate, next to a captured outer value (a closure member bound as a
    /// parameter) - the neighbour's null literal is an untyped string comparison outside a correlation.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_TypedNullAndCapturedValue_ShouldSelectTheMatchingParents()
    {
        var nullAmount = Context.From<C4Parent>()
            .Where(p => p.Id >= 8001 && p.Id <= 8003)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<C4Child>().Where(c => c.ParentId == p.Id && c.Amount == null)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        nullAmount.Should().Equal(new[] { 8002 }, "only parent 8002 owns a child whose amount is NULL");

        int? captured = 5;
        var byCaptured = Context.From<C4Parent>()
            .Where(p => p.Id >= 8001 && p.Id <= 8003)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<C4Child>().Where(c => c.ParentId == p.Id && c.Amount == captured)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        byCaptured.Should().Equal(new[] { 8001 }, "the captured 5 is bound into the inner parameter list");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Not claimed in this class (deferred, see rv1/inputs/deferred.json)
    // ---------------------------------------------------------------------------------------------

    // - The builder aggregate terminal in a WHERE predicate is LIVE (section 2b): the placeholder is
    //   now result-typed, so the comparison lowers to a correlated scalar subquery. A terminal still
    //   outside the aggregate whitelist (LongCount, T279) and a leftover scalar First() subquery fail
    //   closed with QueryPreparationException; their provider SQL-generation guards own that boundary.
    // - One- and two-level reference hops (p.Group.Code / p.Group.Root.Label) as a correlated outer
    //   reference raise "variable 'navigation0' ... is not defined" at preparation - deferred (T304).
    // - The AsEntityBuilder adapter on a reference navigation is NOT rejected on this provider (no
    //   exception surfaced), so its documented NotSupportedException arm is not publicly reachable here.

    /// <summary>Marker row of the class-owned <c>c4_parent</c> table.</summary>
    [SqlTable("c4_parent")]
    public sealed class C4Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("bucket")]
        public int? Bucket { get; set; }

        [Column("group_id")]
        public int GroupId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }

        public ICollection<C4Child> Children { get; } = new List<C4Child>();

        public C4Group? Group { get; set; }
    }

    /// <summary>Marker row of the class-owned <c>c4_child</c> table.</summary>
    [SqlTable("c4_child")]
    public sealed class C4Child
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

        [Column("group_code")]
        public string? GroupCode { get; set; }
    }

    /// <summary>Marker row of the class-owned <c>c4_group</c> table.</summary>
    [SqlTable("c4_group")]
    public sealed class C4Group
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("root_id")]
        public int RootId { get; set; }

        public C4Root? Root { get; set; }
    }

    /// <summary>Marker row of the class-owned <c>c4_root</c> table.</summary>
    [SqlTable("c4_root")]
    public sealed class C4Root
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}

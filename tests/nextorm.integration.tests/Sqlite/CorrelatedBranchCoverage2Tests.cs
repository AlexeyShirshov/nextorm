using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Correlated-query branch coverage executed against a live SQLite database. Every fact drives a real
/// correlated query over the seeded <c>simple_entity</c>/<c>complex_entity</c> tables or over
/// class-owned marker rows in <c>eager_parent</c>/<c>eager_child</c> (negative ids, deleted in a
/// <c>finally</c>), and asserts the returned rows.
///
/// <para>
/// Targeted branches of <c>src/nextorm.core/Visitors/CorrelatedQueryExpressionVisitor.cs</c>: the
/// projection-mode / outer-parameter-stack setup (<c>ProjectionMode</c> :52, <c>PushOuter</c> :65), the
/// scalar-subquery rewrite (<c>GetQueryCommand</c> :1436, <c>ReplaceQueryCommand</c> :1532), the
/// navigation-correlation key alignment (<c>AlignNavigationKeys</c> :864), the collection-terminal and
/// collection-<c>Count</c> lowerings (<c>TryLowerCollectionTerminalCall</c> :481, <c>VisitMember</c>
/// :445, <c>BuildCollectionCorrelation</c> :703), the boolean/predicate normalisation and the
/// equality operand handling of <c>VisitBinary</c> (:1683), the navigation-count narrowing probe
/// (:941-982) and the subquery cache-miss log (:316-320).
/// </para>
///
/// <para>
/// Not driven here because no public path reaches them on a SQL provider (all are guarded by
/// <c>IsInMemory</c> or by <c>!_forPrepare</c>, and the integration suite forbids
/// <see cref="InMemoryDataContext"/>): the reference-scalar lowering <c>TryLowerReferenceScalar</c>
/// :993, <c>LowerMultiHopReference</c> :1070, <c>BuildNavigationChainCommand</c> :1082,
/// <c>ResolveHopOuterKey</c> :1138 / <c>ResolveHopMatchKey</c> :1144, <c>BuildReferenceExists</c>
/// :1155, <c>BuildReferenceCorrelation</c> :1192, <c>IsReferenceScalarValueAccess</c> :1257,
/// <c>VisitAlignedReferenceComparison</c> :1266, <c>TryGetReferenceNullComparison</c> :1286 (the
/// null-constant binary at :1290-1295), <c>TryResolveReferenceAccess</c> :1307, the in-memory
/// collection rejection :463-470, <c>BuildReferencePrefixKey</c> :796, the multi-hop collection
/// prefix branch :736-743, the non-prepare <c>Any&lt;TResult&gt;</c> :1808 and the non-prepare
/// exists branch :183-202. They are reachable only through <see cref="InMemoryDataContext"/>, which
/// the integration project must not use, so they stay with the unit suites
/// (<c>tests/nextorm.core.tests/Navigation/*</c>).
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class CorrelatedBranchCoverage2Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private static int _navSeed = -1_000_000_000;

    private static int NextNavBase() => Interlocked.Add(ref _navSeed, -1_000);

    /// <summary>
    /// Registers the nullable-foreign-key navigation (the key alignment of
    /// <c>AlignNavigationKeys</c> only runs when the child foreign key type differs from the parent
    /// principal key type) and seeds class-owned marker rows: <c>rich</c> with two children that
    /// deliberately share a name, <c>few</c> with one, <c>empty</c> with none.
    /// </summary>
    private (int Rich, int Few, int Empty) SeedNav()
    {
        var ctx = _sut.DataProvider;
        ctx.From<CorrNavParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        var rich = NextNavBase();
        var few = rich + 1;
        var empty = rich + 2;

        ctx.CreateInsertBuilder<CorrNavParent>().Values([
            new CorrNavParent { Id = rich, Name = "corr-rich" },
            new CorrNavParent { Id = few, Name = "corr-few" },
            new CorrNavParent { Id = empty, Name = "corr-empty" },
        ]).Insert();

        ctx.CreateInsertBuilder<CorrNavChild>().Values([
            new CorrNavChild { Id = rich + 100, ParentId = rich, Name = "dup" },
            new CorrNavChild { Id = rich + 101, ParentId = rich, Name = "dup" },
            new CorrNavChild { Id = rich + 102, ParentId = few, Name = "few-a" },
        ]).Insert();

        return (rich, few, empty);
    }

    private void DeleteNav(int rich)
    {
        var ctx = _sut.DataProvider;
        ctx.CreateDeleteBuilder<CorrNavChild>().Where(c => c.Id >= rich && c.Id <= rich + 199).Delete();
        ctx.CreateDeleteBuilder<CorrNavParent>().Where(p => p.Id >= rich && p.Id <= rich + 2).Delete();
    }

    private void WithNav(Action<(int Rich, int Few, int Empty)> body)
    {
        var fixture = SeedNav();
        try
        {
            body(fixture);
        }
        finally
        {
            DeleteNav(fixture.Rich);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 1. A correlated scalar subquery as the LEFT operand of an equality
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The correlated scalar subquery is the left operand of <c>==</c>, so
    /// <c>VisitBinary</c>'s <c>node.Left.NeedToConvert()</c> arm (the boxed-operand rebuild at
    /// :1750-1767) runs instead of the right-operand arm every other sweep exercises.
    /// </summary>
    [Fact]
    public void CorrelatedScalarSubquery_AsLeftOperandOfEquality_ShouldFilterOnTheOuterRow()
    {
        var rows = _sut.ComplexEntity
            .Where(x => _sut.SimpleEntity.Where(s => s.Id == x.Id).Select(s => (long?)s.Id).FirstOrDefault() == x.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(new[] { 1L, 2L, 3L }, "the correlated scalar mirrors the outer id for every seeded row");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. exists(...) combined with || (the logical-operator normalisation)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>exists(...) || predicate</c> forces both operands through
    /// <c>NormalizePredicateOperand</c> (:1634) so the registry lambda of the subquery is re-bound to a
    /// real boolean predicate before <c>Expression.MakeBinary</c> (:1732-1740).
    /// </summary>
    [Fact]
    public void CorrelatedExistsCombinedWithOrElse_ShouldNormalizeThePredicateAndFilter()
    {
        var rows = _sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == x.Id)) || x.Int == null)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(new[] { 1L, 2L, 3L }, "every seeded complex row has a matching simple row");
    }

    // ---------------------------------------------------------------------------------------------
    // 3-7. Collection navigation correlation on a nullable foreign key
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>Children.Any()</c> inside a predicate lowers through
    /// <c>TryLowerCollectionTerminalCall</c> (:481) and <c>BuildCollectionCorrelation</c> (:703); the
    /// nullable child foreign key makes <c>AlignNavigationKeys</c> (:864) take its unifying path
    /// (:869-884) instead of the early same-type return (:866).
    /// </summary>
    [Fact]
    public void CorrelatedCollectionAnyInPredicate_OnNullableForeignKey_ShouldFilterOwnChildren()
    {
        WithNav(f =>
        {
            var rows = _sut.DataProvider.From<CorrNavParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty && p.Children.Any())
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();

            rows.Should().Equal(new[] { f.Rich, f.Few }, "only the two parents with marker children match");
        });
    }

    /// <summary><c>Children.Count()</c> in a predicate (the wide count plus the checked narrowing).</summary>
    [Fact]
    public void CorrelatedCollectionCountInPredicate_OnNullableForeignKey_ShouldFilterByChildCount()
    {
        WithNav(f =>
        {
            var rows = _sut.DataProvider.From<CorrNavParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty && p.Children.Count() > 1)
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();

            rows.Should().Equal(f.Rich);
        });
    }

    /// <summary>The <c>Count</c> property form, lowered by <c>VisitMember</c> (:445-454).</summary>
    [Fact]
    public void CorrelatedCollectionCountPropertyInPredicate_OnNullableForeignKey_ShouldFilterByChildCount()
    {
        WithNav(f =>
        {
            var rows = _sut.DataProvider.From<CorrNavParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty && p.Children.Count > 1)
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();

            rows.Should().Equal(new[] { f.Rich }, "the Count property takes the same lowering as Count()");
        });
    }

    /// <summary><c>Children.LongCount()</c> projected per parent (the 64-bit terminal, no narrowing).</summary>
    [Fact]
    public void CorrelatedCollectionLongCountInProjection_OnNullableForeignKey_ShouldCountPerParent()
    {
        WithNav(f =>
        {
            var rows = _sut.DataProvider.From<CorrNavParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, L = p.Children.LongCount() })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(f.Rich, f.Few, f.Empty);
            rows.Select(r => r.L).Should().Equal(2L, 1L, 0L);
        });
    }

    /// <summary><c>Children.Any()</c> projected per parent (presence, never a CLR-graph read).</summary>
    [Fact]
    public void CorrelatedCollectionAnyInProjection_OnNullableForeignKey_ShouldReportPresence()
    {
        WithNav(f =>
        {
            var rows = _sut.DataProvider.From<CorrNavParent>()
                .Where(p => p.Id >= f.Rich && p.Id <= f.Empty)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Has = p.Children.Any() })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(f.Rich, f.Few, f.Empty);
            rows.Select(r => r.Has).Should().Equal(true, true, false);
        });
    }

    /// <summary>
    /// The correlated collection count keeps junction/child multiplicity: two distinct child rows that
    /// share a name are both counted (no implicit DISTINCT).
    /// </summary>
    [Fact]
    public void CorrelatedCollectionCount_DuplicateChildNames_ShouldNotDeduplicate()
    {
        WithNav(f =>
        {
            var rows = _sut.DataProvider.From<CorrNavParent>()
                .Where(p => p.Id == f.Rich)
                .Select(p => new { C = p.Children.Count() })
                .ToList();

            rows.Should().ContainSingle();
            rows[0].C.Should().Be(2, "two physical child rows are counted even though they share a name");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Navigation-count narrowing probe: a sibling int conversion that is not a wide count
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A projection that mixes an ordinary <c>(int)</c> conversion with a correlated scalar makes the
    /// registry non-empty, so <c>ContainsNavigationCountNarrowing</c> (:921) probes the conversion
    /// column and <c>NavigationCountNarrowingProbe.VisitUnary</c> takes its non-matching arm (:956)
    /// through <c>TryGetWideCountCommand</c>'s negative result (:965-981).
    /// </summary>
    [Fact]
    public void CorrelatedNavigationCountWithSiblingIntConversion_ShouldMaterializeBothColumns()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                X = (int)x.Id,
                S = _sut.SimpleEntity.Where(s => s.Id == x.Id).Select(s => s.Id).First()
            })
            .ToList();

        rows.Select(r => r.X).Should().Equal(1, 2, 3);
        rows.Select(r => r.S).Should().Equal(new[] { 1, 2, 3 }, "the correlated scalar still evaluates per outer row");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. A correlated scalar subquery combined with && in the outer predicate
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A correlated scalar equality nested inside <c>&amp;&amp;</c>: both operands of the logical node are
    /// normalised (:1732-1740) while the scalar subquery is rewritten through <c>GetQueryCommand</c>.
    /// </summary>
    [Fact]
    public void CorrelatedScalarSubqueryInsideAndAlso_ShouldFilterOnTheOuterRow()
    {
        var rows = _sut.ComplexEntity
            .Where(x => x.Int != null && _sut.SimpleEntity.Where(s => s.Id == x.Int!.Value).Select(s => (int?)s.Id).First() == x.Int)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(new[] { 2L, 3L }, "both rows carry the nullable int 1, which correlates to simple row 1");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. A two-level member hop as the correlated outer reference
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The correlation key is a nested member access (<c>x.Int.Value</c>) rather than a direct column,
    /// so the outer member is rewritten through the nested member chain of
    /// <c>ReplaceConstantsExpressionVisitor</c> before the marker is registered.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_OnNestedOuterMember_ShouldEvaluatePerRow()
    {
        var rows = _sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == x.Int!.Value)))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(new[] { 2L, 3L }, "the seeded nullable int is 1 for both rows, so both correlate to simple row 1");
    }

    // ---------------------------------------------------------------------------------------------
    // 11-12. Captured values and null literals inside the correlated predicate
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A captured non-null value inside the correlated predicate: the closure member is bound as a query
    /// parameter on the inner command (<c>ReplaceConstantsExpressionVisitor</c> inside
    /// <c>GetQueryCommand</c>) rather than folded into the plan.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_WithCapturedValueInTheInnerPredicate_ShouldFilterInnerRows()
    {
        var captured = 1;

        var rows = _sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .Where(s => SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id && c.Int == captured)))
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToList();

        rows.Should().Equal(new[] { 2, 3 }, "complex rows 2 and 3 carry the captured nullable int 1");
    }

    /// <summary>
    /// A null literal compared against the inner nullable string inside the correlated predicate: the
    /// translator renders <c>somestring is null</c> for the literal form.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_WithNullLiteralInTheInnerPredicate_ShouldMatchNullRows()
    {
        var rows = _sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .Where(s => SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id && c.String == (string?)null)))
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToList();

        rows.Should().Equal(new[] { 3 }, "only complex row 3 has a null somestring");
    }

    // ---------------------------------------------------------------------------------------------
    // 13. The subquery cache-miss log at Debug level
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A context built with a Debug-level logger observes the subquery cache miss
    /// (<c>VisitMethodCall</c> :316-320) for the non-correlated builder terminal, while the correlated
    /// scalar in the same projection still evaluates per outer row.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_WithDebugLogger_ShouldLogTheCacheMissAndReturnRows()
    {
        var recording = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(recording);
        });
        using var ctx = SqliteTestProvider.Instance.CreateContext(loggerFactory);

        var rows = ctx.From<IComplexEntity>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Scalar = ctx.From<ISimpleEntity>().Where(s => s.Id == x.Id).Select(s => s.Id).First(),
                Any = ctx.From<ISimpleEntity>().Where(s => s.Id > 0).Any()
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1L, 2L);
        rows.Select(r => r.Scalar).Should().Equal(1, 2);
        rows.Should().OnlyContain(r => r.Any);
        recording.Messages.Should().Contain(m => m.Contains("Subquery expression miss", StringComparison.Ordinal),
            "the Debug logger records the subquery expression cache miss");
    }

    // ---------------------------------------------------------------------------------------------
    // 14-15. Projection shapes that reach ProjectionMode / PushOuter
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A member-init projection with a correlated member: the projection branch of
    /// <c>QueryCommand.QueryPreparer</c> sets <c>ProjectionMode</c> and pushes the projection parameter
    /// (<c>PushOuter</c> :65) before visiting each binding.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_InMemberInitProjection_ShouldEvaluatePerRow()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new CorrProjectionRow
            {
                Id = x.Id,
                Sid = _sut.SimpleEntity.Where(s => s.Id == x.Id).Select(s => s.Id).First()
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1L, 2L, 3L);
        rows.Select(r => r.Sid).Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// A whole-projection correlated scalar: the single-column projection branch sets
    /// <c>ProjectionMode</c> (:52) without pushing an outer scope, because the projection lambda is the
    /// one visited by <c>VisitLambda</c>.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_AsWholeScalarProjection_ShouldEvaluatePerRow()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => _sut.SimpleEntity.Where(s => s.Id == x.Id).Select(s => s.Id).First())
            .ToList();

        rows.Should().Equal(1, 2, 3);
    }

    /// <summary>Materialized projection row used by the member-init fact.</summary>
    public sealed class CorrProjectionRow
    {
        public long Id { get; set; }

        public int Sid { get; set; }
    }

    /// <summary>Parent marker fixture: the <c>eager_parent</c> table with a declared one-to-many navigation.</summary>
    [SqlTable("eager_parent")]
    public sealed class CorrNavParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<CorrNavChild> Children { get; } = new List<CorrNavChild>();
    }

    /// <summary>
    /// Child marker fixture: the <c>eager_child</c> table with a NULLABLE foreign key, which is what
    /// makes the correlation-key alignment take its unifying path.
    /// </summary>
    [SqlTable("eager_child")]
    public sealed class CorrNavChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    /// <summary>Records every log message a Debug-level logger observes.</summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                    return _messages.ToArray();
            }
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                lock (messages)
                    messages.Add(formatter(state, exception));
            }
        }
    }
}

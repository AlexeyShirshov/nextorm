using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Preparation/projection shape coverage against a live SQLite database. Every fact prepares and
/// executes at least one real query through the inherited <see cref="ProviderTestSuite"/> repository,
/// so the projection/preparation branches of <c>QueryCommand.QueryPreparer</c>
/// (<c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c>) are exercised end to end: scalar,
/// entity, anonymous, nullable, computed, typed-CTE and grouped shapes on the live path, and the
/// publicly reachable rejection guards next to a live valid counterpart of the same shape.
/// </summary>
[Collection("Sqlite")]
public sealed class SqlitePreparationShapeCoverageTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    /// <summary>
    /// #294 frozen derived-source refusal: a derived source read without an outer projection whose
    /// projected columns need re-translation. The restriction is the projection's expression shape,
    /// not the anonymity of the result type.
    /// </summary>
    private const string DerivedShapeMessage = "Cannot materialize this derived source without an explicit outer Select: its projected columns cannot be reused safely (row-member expressions require re-translation). Add an outer Select, for example Select(x => x.Id). This restriction applies to anonymous types and DTOs alike.";

    /// <summary>Proves the seeded <c>simple_entity</c> table is live before a shape assertion.</summary>
    private void SeedIsLive() => _sut.SimpleEntityAsClass.Count().Should().Be(10);

    [Fact]
    public void ScalarProjection_ShouldMaterializeLiveValues()
    {
        SeedIsLive();

        var ids = _sut.SimpleEntity
            .Where(x => x.Id >= 8)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(8, 9, 10);
    }

    [Fact]
    public void WholeEntityProjection_ShouldMaterializeEveryRow()
    {
        var rows = _sut.SimpleEntityAsClass.OrderBy(x => x.Id).ToList();

        rows.Should().HaveCount(10);
        rows[0].Id.Should().Be(1);
        rows[^1].Id.Should().Be(10);
    }

    [Fact]
    public void AnonymousProjectionWithAliases_ShouldBindByOutputAlias()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { Text = x.RequiredString, Key = x.Id, Maybe = x.Int })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Text.Should().Be("sdf");
        rows[0].Key.Should().Be(1L);
        rows[0].Maybe.Should().BeNull();
        rows[1].Text.Should().Be("asdfgoi");
        rows[1].Maybe.Should().Be(1);
    }

    [Fact]
    public void NullableMembers_ShouldMaterializeNullsAndValues()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { x.Int, x.String, x.SmallInt, x.Real })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Int.Should().BeNull();
        rows[0].String.Should().Be("dadfasd");
        rows[0].SmallInt.Should().Be((short)3);
        rows[2].String.Should().BeNull();
        rows[2].Real.Should().BeNull();
    }

    [Fact]
    public void ComputedProjectionExpressions_ShouldEvaluateOnTheDatabase()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { Doubled = x.Id * 2, Coalesced = x.Int ?? -1, Length = x.RequiredString.Length })
            .ToList();

        rows.Select(r => r.Doubled).Should().Equal(2L, 4L, 6L);
        rows.Select(r => r.Coalesced).Should().Equal(-1, 1, 1);
        rows.Select(r => r.Length).Should().Equal(3, 7, 5);
    }

    [Fact]
    public void MemberInitDtoProjection_ShouldMaterialize()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new PreparationRow { Id = x.Id, Value = x.Int, Text = x.String })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Id.Should().Be(1L);
        rows[0].Value.Should().BeNull();
        rows[1].Text.Should().Be("xxx");
    }

    [Fact]
    public void TypedCteAnonymousShape_ShouldMaterializeThroughTheCte()
    {
        var cte = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Int })
            .AsCte("d4_anon_shape");

        var rows = _sut.DataProvider.From(cte).Select(r => new { r.Id, r.Int }).ToList();

        rows.Select(r => r.Id).Should().Equal(1L, 2L, 3L);
        rows.Select(r => r.Int).Should().Equal(null, 1, 1);
    }

    [Fact]
    public void TypedCteReorderedSlots_ShouldBindByAliasNotPosition()
    {
        var cte = _sut.ComplexEntity
            .Select(x => new { Text = x.RequiredString, Value = x.Int, Key = x.Id })
            .AsCte("d4_reordered_shape");

        var rows = _sut.DataProvider
            .From(cte)
            .OrderBy(r => r.Key)
            .Select(r => new { r.Key, r.Value, r.Text })
            .ToList();

        rows.Select(r => r.Key).Should().Equal(1L, 2L, 3L);
        rows[1].Value.Should().Be(1);
        rows[1].Text.Should().Be("asdfgoi");
    }

    [Fact]
    public void TypedCteScalarShape_ShouldMaterializeTheSingleColumn()
    {
        var cte = _sut.SimpleEntity
            .Where(x => x.Id <= 3)
            .Select(x => x.Id)
            .AsCte("d4_scalar_shape");

        var values = _sut.DataProvider.From(cte).ToList();

        values.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void TypedCteWholeEntityShape_ShouldMaterializeProjectedMembers()
    {
        var cte = _sut.ComplexEntity.ToCommand().AsCte("d4_entity_shape");

        var rows = _sut.DataProvider.From(cte).Select(r => new { r.Id, r.RequiredString }).ToList();

        rows.Should().HaveCount(3);
        rows[1].RequiredString.Should().Be("asdfgoi");
    }

    [Fact]
    public void JoinFeedingTypedCte_ShouldMaterialize()
    {
        var paired = _sut.SimpleEntityAsClass
            .Where(s => s.Id <= 3)
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { p.Item1.Id, p.Item2.Int })
            .AsCte("d4_join_shape");

        var rows = _sut.DataProvider.From(paired).OrderBy(r => r.Id).Select(r => new { r.Id, r.Int }).ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 3);
        rows[0].Int.Should().BeNull();
        rows[1].Int.Should().Be(1);
    }

    [Fact]
    public void GroupedAggregatedShapeWithHaving_ShouldMaterialize()
    {
        var groups = _sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Having(g => SqlFunctions.Sql.count() > 1)
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count(), Sum = SqlFunctions.Sql.sum(g.Id) })
            .ToList();

        groups.Should().ContainSingle();
        groups[0].Int.Should().Be(1);
        groups[0].Count.Should().Be(2);
        groups[0].Sum.Should().Be(5L);
    }

    [Fact]
    public void DistinctProjectionShape_ShouldDeduplicate()
    {
        var values = _sut.ComplexEntity.Distinct().Select(x => x.Int).ToList();

        values.Should().HaveCount(2);
        values.Should().Contain(1);
        values.Should().Contain((int?)null);
    }

    [Fact]
    public void PagingShape_ShouldReturnTheRequestedWindow()
    {
        SeedIsLive();

        var ids = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Limit(2)
            .Offset(1)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(2L, 3L);
    }

    [Fact]
    public void TypedCteWithoutTuple_ShouldRunWhileTupleColumnThroughTypedCte_ShouldBeRejected()
    {
        // The live counterpart is the same typed-CTE read without the System.Tuple column; a tuple in
        // the CTE's column shape is not addressable through the typed member reader, so the preparer
        // fails fast instead of emitting an empty select list.
        var okCte = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Int })
            .AsCte("d4_tuple_ok_shape");

        var ok = _sut.DataProvider.From(okCte).Select(r => new { r.Id, r.Int }).ToList();
        ok.Select(r => r.Id).Should().Equal(1L, 2L, 3L);

        var tupleCte = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { Pair = Tuple.Create(x.Id, x.Int) })
            .AsCte("d4_tuple_shape");

        FluentActions
            .Invoking(() => _sut.DataProvider.From(tupleCte).Select(r => new { r.Pair }).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*System.Tuple column*typed CTE*");
    }

    [Fact]
    public void BareDomScalarProjection_ShouldBeRejectedWhileScalarMemberRuns()
    {
        var live = _sut.DataProvider
            .From<PreparationDomProbe>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        live.Should().Equal(1L, 2L, 3L);

        FluentActions
            .Invoking(() => _sut.DataProvider.From<PreparationDomProbe>().Select(x => x.Doc).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*bare top-level scalar projection*JsonObject*");
    }

    [Fact]
    public void DynamicColumnsStoreWithJoin_ShouldBeRejectedWhileSingleSourceRuns()
    {
        // The SQLite suite shares one database file for the whole process, and the dynamic-columns
        // write tests insert rows with random keys without deleting them. The single-source read is
        // therefore proved on an impossible key filter: it still prepares and executes the real
        // query, but the expectation no longer depends on which test ran first.
        var live = _sut.DataProvider
            .From<PreparationDynamicStoreProbe>()
            .Where(x => x.Id < 0)
            .ToList();
        live.Should().BeEmpty();

        FluentActions
            .Invoking(() => _sut.DataProvider
                .From<PreparationDynamicStoreProbe>()
                .JoinInto(
                    _sut.DataProvider.From<EagerChild>(),
                    (p, c) => p.Id == c.ParentId,
                    p => p.Children,
                    p => p.Id,
                    c => c.ParentId)
                .ToCommand()
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*dynamic-columns store*joins*");
    }

    [Fact]
    public void OrderByProjectedMember_ShouldRunAndUnknownMember_ShouldBeRejected()
    {
        var ordered = _sut.ComplexEntity
            .Select(x => new PreparationOrderDto { Id = x.Id })
            .OrderBy(p => p.Id)
            .ToList();

        ordered.Select(p => p.Id).Should().Equal(1L, 2L, 3L);

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Select(x => new PreparationOrderDto { Id = x.Id })
                .OrderBy(p => p.Extra)
                .ToList())
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*ORDER BY member 'Extra' is not part of the projection*");
    }

    [Fact]
    public void UnmappedJoinedProjectionItem_ShouldBeRejectedWhileItsColumnsRun()
    {
        // A join whose item source is a derived query has no entity metadata, so a bare whole-item read
        // of that slot cannot be expanded into columns; the same join with an explicit column
        // projection is the live counterpart.
        var derived = _sut.DataProvider.From(
            _sut.SimpleEntityAsClass.Where(x => x.Id <= 3).Select(x => new { x.Id }));

        var live = _sut.SimpleEntityAsClass
            .Join(derived, (s, d) => s.Id == d.Id)
            .Select(p => new { Item = p.Item1.Id, Derived = p.Item2.Id })
            .OrderBy(p => p.Item)
            .ToList();
        live.Select(p => p.Derived).Should().Equal(1, 2, 3);

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass
                .Join(derived, (s, d) => s.Id == d.Id)
                .Select(p => p.Item2)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*not a mapped entity*cannot be projected as a whole*");
    }

    [Fact]
    public void NonAnonymousDerivedSource_ShouldBeRejectedWhileAnonymousRuns()
    {
        // "Anonymous plus consumer Select": the outer projection supplies the shape, so the derived
        // source is read successfully.
        var live = _sut.DataProvider
            .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new { x.Id }))
            .Select(x => x.Id)
            .ToList();

        live.Should().Equal(9, 10);

        // "Bare derived read" (the contrasting form): the same derived source is read without an outer
        // Select. A member-init DTO projection is not an anonymous type, but that is not the
        // discriminator — its projected member is a row-member expression that the enclosing command
        // would have to re-translate against the derived source, so preparation refuses it with the
        // derived-shape message (#294).
        FluentActions
            .Invoking(() => _sut.DataProvider
                .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new PreparationIdRow { Id = x.Id }))
                .ToList())
            .Should().Throw<QueryPreparationException>()
            .WithMessage(DerivedShapeMessage);
    }

    /// <summary>
    /// #294: the bare anonymous plain-member form fails on the same expression-shape rule as the DTO
    /// form above — anonymity is not the discriminator, a projected row-member need re-translation is.
    /// </summary>
    [Fact]
    public void AnonymousPlainMemberBareDerivedSource_ShouldBeRejectedWithDerivedShapeMessage()
    {
        SeedIsLive();

        FluentActions
            .Invoking(() => _sut.DataProvider
                .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new { x.Id }))
                .ToList())
            .Should().Throw<QueryPreparationException>()
            .WithMessage(DerivedShapeMessage);
    }

    /// <summary>
    /// #294: a constructor DTO derived-source projection is named, yet a bare read of it is refused
    /// with the same derived-shape message; DTO projection support on a direct read (see
    /// <c>PrepareColumns</c> coverage) does not imply bare derived-read support.
    /// </summary>
    [Fact]
    public void ConstructorDtoBareDerivedSource_ShouldBeRejectedWithDerivedShapeMessage()
    {
        SeedIsLive();

        FluentActions
            .Invoking(() => _sut.DataProvider
                .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new PreparationCtorRow(x.Id)))
                .ToList())
            .Should().Throw<QueryPreparationException>()
            .WithMessage(DerivedShapeMessage);
    }

    /// <summary>
    /// #294 accepted-path control: the documented anonymous-plus-outer-<c>Select</c> form keeps running.
    /// </summary>
    [Fact]
    public void ConsumerSelectOverAnonymousDerivedSource_ShouldRun()
    {
        SeedIsLive();

        _sut.DataProvider
            .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new { x.Id }))
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(9, 10);
    }

    /// <summary>
    /// #294 accepted-path control: a computed inner column is read through the outer projection.
    /// The bare single-column (scalar) derived read — the other accepted control — stays covered by the
    /// retained <c>PreparerBranchCoverage6Tests.PrepareColumns_DerivedScalarPassThrough_ShouldReuseTheSingleColumn</c>.
    /// It is deliberately not duplicated here: a second projection-less scalar derived source in one
    /// process shares the result-type-keyed <c>DataContextCache.SelectListCache</c> entry, and the cache
    /// hit skips the pass-through branch (including <c>OneColumn</c>) for the other source.
    /// </summary>
    [Fact]
    public void ConsumerSelectOverComputedDerivedSource_ShouldRun()
    {
        SeedIsLive();

        _sut.DataProvider
            .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new { Id = x.Id, Twice = x.Id * 2 }))
            .Select(x => x.Twice)
            .ToList()
            .Should().Equal(18, 20);
    }

    /// <summary>
    /// #294 accepted-path control: a member-init DTO inner projection is read through an outer
    /// <c>Select</c> (scalar and row forms) — the rewrite the refusal message points at.
    /// </summary>
    [Fact]
    public void ConsumerSelectOverMemberInitDtoDerivedSource_ShouldRun()
    {
        SeedIsLive();

        _sut.DataProvider
            .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new PreparationIdRow { Id = x.Id }))
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(9, 10);

        _sut.DataProvider
            .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new PreparationIdRow { Id = x.Id }))
            .Select(x => new { x.Id })
            .ToList()
            .Select(r => r.Id)
            .Should().Equal(9, 10);
    }

    /// <summary>
    /// #294 accepted-path control: a positional-record inner projection is read through an outer
    /// <c>Select</c>. A plain constructor-only class projection is not addressed here: it is refused
    /// at rendering on the base revision too (`Cannot find inner column Id`), so it is a pre-existing
    /// limitation recorded in the cycle evidence, not an accepted shape and not touched by #294.
    /// </summary>
    [Fact]
    public void ConsumerSelectOverRecordDerivedSource_ShouldRun()
    {
        SeedIsLive();

        _sut.DataProvider
            .From(_sut.SimpleEntityAsClass.Where(x => x.Id > 8).Select(x => new PreparationRecordRow(x.Id)))
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(9, 10);
    }

    [Fact]
    public void ConditionalProjectionExpression_ShouldEvaluateOnTheDatabase()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { Kind = x.Int == null ? "none" : "some", Amount = x.Int ?? 0 })
            .ToList();

        rows.Select(r => r.Kind).Should().Equal("none", "some", "some");
        rows.Select(r => r.Amount).Should().Equal(0, 1, 1);
    }

    /// <summary>Materialized shape of the member-init projection fact.</summary>
    public sealed class PreparationRow
    {
        public long Id { get; set; }
        public int? Value { get; set; }
        public string? Text { get; set; }
    }

    /// <summary>Projection DTO whose <see cref="Extra"/> member is never assigned by the projection.</summary>
    public sealed class PreparationOrderDto
    {
        public long Id { get; set; }
        public int Extra { get; set; }
    }

    /// <summary>Member-init derived-source DTO: not an anonymous type, so it has no reusable shape.</summary>
    public sealed class PreparationIdRow
    {
        public int Id { get; set; }
    }

    /// <summary>Constructor derived-source DTO: named projection whose argument is a row-member expression.</summary>
    public sealed class PreparationCtorRow(int id)
    {
        public int Id { get; } = id;
    }

    /// <summary>Positional-record derived-source projection: constructor argument is a row-member expression.</summary>
    public sealed record PreparationRecordRow(int Id);

    /// <summary>
    /// Reads the seeded <c>complex_entity</c> table but exposes its text column as a bare JSON DOM
    /// scalar with no <c>[JsonColumn]</c> converter, which the preparer rejects as a top-level shape.
    /// </summary>
    [SqlTable("complex_entity")]
    public sealed class PreparationDomProbe
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("doc")]
        public JsonObject Doc { get; set; } = new();
    }

    /// <summary>
    /// Dynamic-columns store over the seeded <c>dynamic_entity</c> table plus a conventional child
    /// navigation, so a <c>JoinInto</c> join can be attached while the identity read keeps the root
    /// entity as the command source.
    /// </summary>
    [SqlTable("dynamic_entity")]
    public sealed class PreparationDynamicStoreProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> Extra { get; set; } = new();

        public ICollection<EagerChild> Children { get; } = new List<EagerChild>();
    }
}

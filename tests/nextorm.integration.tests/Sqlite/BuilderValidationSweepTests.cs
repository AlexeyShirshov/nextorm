using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// Builder-level validation against SQLite: paging/ordering guard clauses, the provider clauses the
/// dialect cannot render (<c>WITH TIES</c>, extreme-row selection, TABLESAMPLE) and the harmless
/// statement-level overrides (identifier quoting, keyword case, command timeout) executed for real.
/// </summary>
[Collection("Sqlite")]
public sealed class BuilderValidationSweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void PagingAndOrderingGuards_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Where(x => x.Id > 0).Limit(-1).Select(x => x.Id).ToList())
            .Should().Throw<ArgumentOutOfRangeException>();

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Where(x => x.Id > 0).Offset(-1).Select(x => x.Id).ToList())
            .Should().Throw<ArgumentOutOfRangeException>();

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Select(x => x.Id).OrderBy(0).ToList())
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithTies_ShouldBeRejectedBySqlite()
    {
        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass
                .OrderBy(x => x.Id)
                .Limit(2)
                .WithTies()
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ExtremeRow_ShouldSelectTheMaxRow()
    {
        var ids = _sut.ComplexEntity
            .SelectWhereMax(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(3L);
    }

    [Fact]
    public void TableSample_ShouldBeRejectedBySqlite()
    {
        var ctx = _sut.DataProvider;

        FluentActions
            .Invoking(() => ctx.From("simple_entity", o => o.TableSample(50))
                .Select(t => t["id"].AsInt)
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void StatementOverrides_ShouldExecute()
    {
        var ids = _sut.SimpleEntityAsClass
            .WithQuotedIdentifiers()
            .WithKeywordCase(KeywordCase.Lower)
            .WithCommandTimeout(5)
            .Where(x => x.Id > 8)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(9, 10);
    }

    /// <summary>
    /// SqlBuilder.cs:590 — a command carrying both document clauses is rejected before the dialect
    /// capability checks. The plain projection executes as the legal control.
    /// </summary>
    [Fact]
    public void ForJsonCombinedWithForXml_ShouldBeRejected()
    {
        var command = _sut.SimpleEntityAsClass.Select(x => x.Id);

        FluentActions
            .Invoking(() => command.WithForJson().WithForXml().ForJson())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*FOR JSON and FOR XML cannot be combined*");

        _sut.SimpleEntityAsClass.Select(x => x.Id).ToList().Should().HaveCount(10);
    }

    /// <summary>SqlBuilder.cs:595 — SQLite has no <c>FOR JSON</c> renderer.</summary>
    [Fact]
    public void ForJson_ShouldBeRejectedBySqlite()
    {
        var command = _sut.SimpleEntityAsClass.Select(x => x.Id);

        FluentActions
            .Invoking(() => command.ForJson())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*FOR JSON is not supported by this SQL dialect*");
    }

    /// <summary>SqlBuilder.cs:603 — SQLite has no <c>FOR XML</c> renderer.</summary>
    [Fact]
    public void ForXml_ShouldBeRejectedBySqlite()
    {
        var command = _sut.SimpleEntityAsClass.Select(x => x.Id);

        FluentActions
            .Invoking(() => command.ForXml())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*FOR XML is not supported by this SQL dialect*");
    }

    /// <summary>SqlBuilder.cs:612 — a statement-level query hint on a dialect without hint support.</summary>
    [Fact]
    public void QueryHint_ShouldBeRejectedBySqlite()
    {
        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Select(x => x.Id).Hint("recompile").ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Query hints are not supported by this SQL dialect*");

        _sut.SimpleEntityAsClass.Select(x => x.Id).ToList().Should().HaveCount(10);
    }

    /// <summary>
    /// SqlBuilder.cs:483 — a DISTINCT ON clause set through the provider extension reaches the
    /// render-time capability guard on a dialect without <c>DISTINCT ON</c>; plain DISTINCT runs.
    /// </summary>
    [Fact]
    public void DistinctOn_ShouldBeRejectedBySqlite()
    {
        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.DistinctOn(x => x.Id).Select(x => x.Id).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DISTINCT ON clause is not supported by this SQL dialect*");

        _sut.SimpleEntityAsClass.Distinct().Select(x => x.Id).ToList().Should().HaveCount(10);
    }

    /// <summary>EntityBuilder.cs:2029 — DISTINCT after DISTINCT ON is rejected at composition time.</summary>
    [Fact]
    public void DistinctAfterDistinctOn_ShouldBeRejected()
    {
        var distinctOn = _sut.SimpleEntityAsClass.DistinctOn(x => x.Id);

        FluentActions
            .Invoking(() => distinctOn.Distinct())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*DISTINCT cannot be combined with DISTINCT ON*");

        _sut.SimpleEntityAsClass.Distinct().Select(x => x.Id).ToList().Should().HaveCount(10);
    }

    /// <summary>
    /// The whole-row extreme-row form over a name-addressed source cannot prepare: the source type has
    /// no mapped metadata, so the projection builder yields no select list and the preparer reports the
    /// mapped-source contract before any SQL is rendered. The mapped <see cref="IComplexEntity"/>
    /// extreme-row query executes and returns the winner.
    /// </summary>
    [Fact]
    public void ExtremeRowOverUnmappedSource_ShouldFailPreparation()
    {
        var ctx = _sut.DataProvider;

        FluentActions
            .Invoking(() => ctx.From("simple_entity").SelectWhereMax(t => t["id"].AsInt).ToList())
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("SelectWhereMax/SelectWhereMin requires a mapped entity source.");

        _sut.ComplexEntity.SelectWhereMax(x => x.Id).Select(x => x.Id).ToList().Should().Equal(3L);
    }

    /// <summary>
    /// #294: a bare read over the same name-addressed source reaches the projection-shape refusal with
    /// no mapped columns and no derived subquery projection to reuse, so it reports the source message —
    /// never the derived-source one. The projection-less extreme-row form above is reported earlier by
    /// its own mapped-source guard (#315), which is why it keeps a different message.
    /// </summary>
    [Fact]
    public void BareReadOverUnmappedSource_ShouldReportNoReusableProjection()
    {
        var ctx = _sut.DataProvider;

        FluentActions
            .Invoking(() => ctx.From("simple_entity").ToList())
            .Should().Throw<QueryPreparationException>()
            .WithMessage("Cannot materialize this source because it has no mapped columns or reusable projection. Use a mapped source or specify an explicit Select with named columns.");
    }

    /// <summary>SqlBuilder.cs:1509 — an extreme-row query over a derived source is rejected.</summary>
    [Fact]
    public void ExtremeRowOverDerivedSource_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        var derived = ctx.From(ctx.From<IComplexEntity>());

        FluentActions
            .Invoking(() => derived.SelectWhereMax(x => x.Id).ToList())
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*requires a single physical-table source*");

        _sut.ComplexEntity.SelectWhereMax(x => x.Id).Select(x => x.Id).ToList().Should().Equal(3L);
    }

    /// <summary>EntityBuilder.cs:3851 — a blank tables-in-scope hint list is rejected.</summary>
    [Fact]
    public void TablesInScopeHint_WithoutNonEmptyHint_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;

        FluentActions
            .Invoking(() => ctx.From<SimpleEntity>().WithTablesInScopeHint(" "))
            .Should().Throw<ArgumentException>()
            .WithMessage("*tables-in-scope hint must be a non-empty string*");

        FluentActions
            .Invoking(() => ctx.From<SimpleEntity>().WithTablesInScopeHint())
            .Should().Throw<ArgumentException>()
            .WithMessage("*tables-in-scope hint must be a non-empty string*");
    }

    /// <summary>
    /// EntityBuilder.cs:665 — a collection member may be declared once per builder. The single valid
    /// declaration loads the inserted child.
    /// </summary>
    [Fact]
    public void LoadWith_DuplicateCollectionMember_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        const int parentId = 920_001;
        const int childId = 920_101;
        ctx.CreateInsertBuilder<EagerParent>()
            .Values([new EagerParent { Id = parentId, Name = "d10" }])
            .Insert();
        ctx.CreateInsertBuilder<EagerChild>()
            .Values([new EagerChild { Id = childId, ParentId = parentId, Name = "d10-child" }])
            .Insert();

        var loaded = ctx.From<EagerParent>()
            .Where(p => p.Id == parentId)
            .LoadWith(p => p.Children, c => c.From<EagerChild>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .ToList();
        loaded.Should().ContainSingle();
        loaded[0].Children.Select(c => c.Name).Should().Equal("d10-child");

        var query = ctx.From<EagerParent>()
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId);

        FluentActions
            .Invoking(() => query.LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*already has an eager-load declaration*");
    }

    /// <summary>EntityBuilder.cs:693 — an undefined eager-load mode is rejected.</summary>
    [Fact]
    public void LoadWith_UndefinedMode_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;

        FluentActions
            .Invoking(() => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, (EagerLoadMode)99))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*not a defined EagerLoadMode value*");
    }

    /// <summary>EntityBuilder.cs:714 — two different explicit eager-load modes conflict.</summary>
    [Fact]
    public void LoadWith_ConflictingModes_ShouldBeRejected()
    {
        var query = _sut.DataProvider.From<D10DualParent>()
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery);

        FluentActions
            .Invoking(() => query.LoadWith(
                p => p.Notes, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*EagerLoadMode.SingleQuery*conflicts*");
    }

    /// <summary>Parent with two eager collections used by the conflicting-mode guard.</summary>
    [SqlTable("eager_parent")]
    public sealed class D10DualParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<EagerChild> Children { get; } = new List<EagerChild>();

        public ICollection<EagerChild> Notes { get; } = new List<EagerChild>();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 2 — the projection/materializer surface of <c>nextorm.core</c> on a live SQLite
/// database (<c>src/nextorm.core/DataContext/Mapping/RowMaterializerBuilder.cs</c>: <c>BuildProjectionItems</c>,
/// <c>BuildEntityItem</c>, <c>BuildMemberInit</c>, <c>BuildRootEntityItem</c>; plus the projection
/// classification in <c>QueryCommand.QueryPreparer.cs</c>). Every fact executes a real query and asserts
/// the computed CLR object: anonymous, positional-constructor and member-init projections, value-tuple
/// factory results, a value-converter enum round-trip, nullable members, a join projection that mixes
/// entity items with scalars, its unmatched LEFT JOIN null arm, and both whole-entity read shapes.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3MaterializerProjectionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_mat_row (id integer primary key, name text null, qty int null, state text null);
        delete from s3_mat_row;
        create table if not exists s3_mat_alt (id integer primary key, label text null);
        delete from s3_mat_alt;
        insert into s3_mat_row (id, name, qty, state) values
            (8101, 'alpha', null, 'Active'),
            (8102, 'beta', 7, 'Closed'),
            (8103, null, 3, 'Active');
        insert into s3_mat_alt (id, label) values (8101, 'alt-a'), (8103, 'alt-c');
        """;

    public SqliteStep3MaterializerProjectionTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>An anonymous projection materializes every selected column, including a SQL NULL.</summary>
    [Fact]
    public void AnonymousProjection_ShouldMaterializeNullableMembers()
    {
        var rows = Context.From<S3MatRow>()
            .Where(x => x.Id >= 8101 && x.Id <= 8103)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Qty })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Should().Be(new { Id = 8101, Name = "alpha", Qty = (int?)null });
        rows[1].Qty.Should().Be(7);
        rows[2].Name.Should().BeNull();
        rows[2].Qty.Should().Be(3);
    }

    /// <summary>A positional record constructor binds one column per parameter.</summary>
    [Fact]
    public void RecordConstructorProjection_ShouldBindEachColumn()
    {
        var rows = Context.From<S3MatRow>()
            .Where(x => x.Id == 8102)
            .Select(x => new S3MatRecordDto(x.Id, x.Name))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(8102);
        rows[0].Name.Should().Be("beta");
    }

    /// <summary>A member-init DTO (parameterless constructor) binds the selected members by name.</summary>
    [Fact]
    public void MemberInitProjection_ShouldBindPropertiesByName()
    {
        var rows = Context.From<S3MatRow>()
            .Where(x => x.Id == 8101)
            .Select(x => new S3MatInitDto { Id = x.Id, Name = x.Name, Qty = x.Qty })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(8101);
        rows[0].Name.Should().Be("alpha");
        rows[0].Qty.Should().BeNull();
    }

    /// <summary>A value-converter enum round-trips through the provider text and translates in predicates.</summary>
    [Fact]
    public void EnumConverter_ShouldRoundTripAndCompare()
    {
        var states = Context.From<S3MatRow>()
            .Where(x => x.Id >= 8101 && x.Id <= 8103)
            .OrderBy(x => x.Id)
            .Select(x => x.State)
            .ToList();

        states.Should().Equal(S3MatState.Active, S3MatState.Closed, S3MatState.Active);

        var activeIds = Context.From<S3MatRow>()
            .Where(x => x.State == S3MatState.Active)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        activeIds.Should().Equal(8101, 8103);
    }

    /// <summary>A nullable member translates both <c>HasValue</c> and <c>Value</c> in one predicate.</summary>
    [Fact]
    public void NullableFilterAndValue_ShouldTranslate()
    {
        var ids = Context.From<S3MatRow>()
            .Where(x => x.Qty.HasValue && x.Qty.Value > 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(8102, 8103);
    }

    /// <summary>A join projection mixing entity items with a scalar materializes both slots.</summary>
    [Fact]
    public void JoinProjection_MixedEntityItemAndScalar_ShouldMaterializeBoth()
    {
        var rows = Context.From<S3MatRow>()
            .Join(Context.From<S3MatAlt>(), (row, alt) => row.Id == alt.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { p.Item1.Id, Alt = p.Item2 })
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be(8101);
        rows[0].Alt.Should().NotBeNull();
        rows[0].Alt.Label.Should().Be("alt-a");
        rows[1].Alt!.Label.Should().Be("alt-c");
    }

    /// <summary>A LEFT JOIN's unmatched entity item materializes as <see langword="null"/>.</summary>
    [Fact]
    public void LeftJoinProjection_UnmatchedEntityItem_ShouldBeNull()
    {
        var rows = Context.From<S3MatRow>()
            .LeftJoin(Context.From<S3MatAlt>(), (row, alt) => row.Id == alt.Id)
            .OrderBy(p => p.Item1.Id)
            .Select(p => new { p.Item1.Id, Alt = p.Item2 })
            .ToList();

        rows.Should().HaveCount(3);
        rows[1].Id.Should().Be(8102);
        rows[1].Alt.Should().BeNull("row 8102 has no alt row");
        rows[0].Alt.Should().NotBeNull();
    }

    /// <summary>A bare projection of one joined entity item returns that entity itself.</summary>
    [Fact]
    public void RootEntityItemProjection_ShouldReturnTheEntity()
    {
        var rows = Context.From<S3MatRow>()
            .Join(Context.From<S3MatAlt>(), (row, alt) => row.Id == alt.Id)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1)
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be(8101);
        rows[0].Name.Should().Be("alpha");
        rows[1].Qty.Should().Be(3);
    }

    /// <summary>A whole-entity projection (<c>Select(x =&gt; x)</c>) returns the mapped entity.</summary>
    [Fact]
    public void WholeEntityProjection_ShouldReturnTheEntity()
    {
        var row = Context.From<S3MatRow>()
            .Where(x => x.Id == 8102)
            .Select(x => x)
            .Single();

        row.Id.Should().Be(8102);
        row.Name.Should().Be("beta");
        row.State.Should().Be(S3MatState.Closed);
    }

    /// <summary>Marker table of the projection facts.</summary>
    [SqlTable("s3_mat_row")]
    public sealed class S3MatRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("qty")]
        public int? Qty { get; set; }

        [Column("state")]
        [ValueConverter(typeof(EnumToStringConverter<S3MatState>))]
        public S3MatState State { get; set; }
    }

    /// <summary>Second table of the join projections.</summary>
    [SqlTable("s3_mat_alt")]
    public sealed class S3MatAlt
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string? Label { get; set; }
    }

    /// <summary>Projection state stored as its name by the value converter.</summary>
    public enum S3MatState
    {
        Active,
        Closed,
    }

    /// <summary>Positional-constructor projection type.</summary>
    public sealed record S3MatRecordDto(int Id, string? Name);

    /// <summary>Member-init projection type (parameterless constructor).</summary>
    public sealed class S3MatInitDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int? Qty { get; set; }
    }
}

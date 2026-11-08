using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 1 — bounded probe of <c>EntityBuilder.JoinInto</c>'s derived/child-shape inventory
/// (<c>src/nextorm.core/Builders/Entity/EntityBuilder.cs</c>: <c>UnsupportedJoinIntoChildShapes</c> and
/// <c>AddUnsupportedDerivedChildShapes</c>, 0/N in v4). The inventory ranks this pool conditionally
/// (campaign-12 zero-flip history), so this class is the probe the plan requires: one live accepted
/// path plus one fact per rejected modifier, each asserted by the shape name the guard reports.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3DerivedChildShapeProbeTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_probe_parent (id integer primary key, name text null);
        delete from s3_probe_parent;
        create table if not exists s3_probe_child (id integer primary key, parent_id int not null, label text null);
        delete from s3_probe_child;
        insert into s3_probe_parent (id, name) values (7401, 'p1'), (7402, 'p2');
        insert into s3_probe_child (id, parent_id, label) values
            (7501, 7401, 'keep'), (7502, 7401, 'drop'), (7503, 7402, 'keep');
        """;

    public SqliteStep3DerivedChildShapeProbeTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();

        Context.From<S3ProbeChild>();
        Context.From<S3ProbeParent>(builder => builder.HasMany(p => p.Children, c => c.ParentId));
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Accepted path: a plain child source joins live rows.</summary>
    [Fact]
    public void PlainChildSource_JoinInto_ShouldFillChildren()
    {
        var parents = Context.From<S3ProbeParent>()
            .OrderBy(p => p.Id)
            .JoinInto(Context.From<S3ProbeChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(7401, 7402);
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([7501, 7502]);
        parents[1].Children.Select(c => c.Id).Should().Equal(7503);
    }

    /// <summary>Accepted path: the child's own <c>Where</c> is folded into the join predicate exactly once.</summary>
    [Fact]
    public void ChildWithWhere_JoinInto_ShouldFoldThePredicate()
    {
        var parents = Context.From<S3ProbeParent>()
            .OrderBy(p => p.Id)
            .JoinInto(
                Context.From<S3ProbeChild>().Where(c => c.Label == "keep"),
                (p, c) => p.Id == c.ParentId,
                p => p.Children)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(7501);
        parents[1].Children.Select(c => c.Id).Should().Equal(7503);
    }

    [Fact]
    public void ChildOrdering_ShouldBeRefusedByShapeName()
    {
        var action = () => Context.From<S3ProbeParent>()
            .JoinInto(Context.From<S3ProbeChild>().OrderBy(c => c.Id), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's OrderBy*only the child's own Where is supported*");
    }

    [Fact]
    public void ChildPaging_ShouldBeRefusedByShapeName()
    {
        var action = () => Context.From<S3ProbeParent>()
            .JoinInto(Context.From<S3ProbeChild>().OrderBy(c => c.Id).Limit(1), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void ChildDistinct_ShouldBeRefusedByShapeName()
    {
        var action = () => Context.From<S3ProbeParent>()
            .JoinInto(Context.From<S3ProbeChild>().Distinct(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Distinct*");
    }

    [Fact]
    public void ChildGrouping_ShouldBeRefusedByShapeName()
    {
        var action = () => Context.From<S3ProbeParent>()
            .JoinInto(
                Context.From<S3ProbeChild>().GroupBy(c => c.ParentId).Having(c => c.ParentId >= 7401),
                (p, c) => p.Id == c.ParentId,
                p => p.Children)
            .ToList();

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*GroupBy, Having*");
    }

    /// <summary>Parent of the probe graph.</summary>
    [SqlTable("s3_probe_parent")]
    public sealed class S3ProbeParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<S3ProbeChild> Children { get; } = new List<S3ProbeChild>();
    }

    /// <summary>Child of the probe graph.</summary>
    [SqlTable("s3_probe_child")]
    public sealed class S3ProbeChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("label")]
        public string? Label { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// D01 (#213) focused MariaDB adapter: MariaDB has no <see cref="CommonTestSuite"/>-based integration
/// class, so the shared many-to-many <c>JoinInto</c> composed with a
/// <see cref="EagerLoadMode.SingleQuery"/> <c>LoadWith</c> is re-pinned here against a real server. The
/// regression is the unmodified dispatcher rejecting the many-to-many declaration; once lifted, one
/// denormalized round trip must stitch the many-to-many children and the LoadWith notes.
/// </summary>
public sealed class MariaDbJoinIntoRemainderIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public MariaDbJoinIntoRemainderIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinInto_ManyToMany_WithSingleQueryLoadWith_ShouldStitchChildrenAndNotes()
    {
        var parents = _ctx.From<MdbJiParent>(b => b
                .HasManyThrough<MdbJiChild, MdbJiLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .Where(p => p.Id == 1)
            .JoinInto(_ctx.From<MdbJiChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<MdbJiNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(20);
    }

    private void Seed()
    {
        Execute("drop table if exists mdb_ji_link");
        Execute("drop table if exists mdb_ji_note");
        Execute("drop table if exists mdb_ji_child");
        Execute("drop table if exists mdb_ji_parent");
        Execute("create table mdb_ji_parent (id int not null primary key, name varchar(50) null)");
        Execute("create table mdb_ji_child (id int not null primary key, name varchar(50) null)");
        Execute("create table mdb_ji_link (id int not null primary key, parent_id int not null, child_id int not null)");
        Execute("create table mdb_ji_note (id int not null primary key, parent_id int not null, text varchar(50) null)");
        Execute("insert into mdb_ji_parent (id, name) values (1, 'p1')");
        Execute("insert into mdb_ji_child (id, name) values (10, 'c1'), (11, 'c2')");
        Execute("insert into mdb_ji_link (id, parent_id, child_id) values (100, 1, 10), (101, 1, 11)");
        Execute("insert into mdb_ji_note (id, parent_id, text) values (20, 1, 'n1')");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("mdb_ji_parent")]
internal sealed class MdbJiParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MdbJiChild> Children { get; set; } = new List<MdbJiChild>();

    public ICollection<MdbJiNote> Notes { get; set; } = new List<MdbJiNote>();
}

[SqlTable("mdb_ji_child")]
internal sealed class MdbJiChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("mdb_ji_link")]
internal sealed class MdbJiLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("mdb_ji_note")]
internal sealed class MdbJiNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}

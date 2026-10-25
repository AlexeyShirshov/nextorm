using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T304 (#304) A1/A3/A4 against a live SQLite database (the provider's throwaway temp-file database):
/// a declared reference navigation used inside a correlated subquery must execute as the portable
/// JOIN + correlated EXISTS shape and return exactly the rows the join semantics imply - including the
/// negative arms, the nullable foreign key and the projection form.
///
/// <para>
/// The class owns its fixture tables (<c>t304_*</c>, not shared with any other suite) and seeds every
/// cell of the matrix: matching rows (8001, 8002), a row whose child does not match (8003), a dangling
/// non-null foreign key (8004), a nullable foreign key with a NULL value (8005) and with a value
/// (8006). The hop-2 query is the historical red arm of the bug and must return exactly
/// <c>[8001, 8002]</c>.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class CorrelatedReferenceHopTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists t304_child;
        drop table if exists t304_nullable_parent;
        drop table if exists t304_parent;
        drop table if exists t304_group;
        drop table if exists t304_root;
        create table t304_root
        (
            id integer primary key,
            label text null
        );
        create table t304_group
        (
            id integer primary key,
            code text null,
            root_id int not null
        );
        create table t304_parent
        (
            id integer primary key,
            group_id int not null
        );
        create table t304_nullable_parent
        (
            id integer primary key,
            group_id int null
        );
        create table t304_child
        (
            id integer primary key,
            parent_id int not null,
            group_code text null
        );
        insert into t304_root (id, label) values (1, 'root-a'), (2, 'root-b');
        insert into t304_group (id, code, root_id) values (1, 'alpha', 1), (2, 'beta', 2);
        -- 8001/8002 match the hop-2 join; 8003 has a child that matches neither hop; 8004 has a
        -- dangling non-null foreign key; the nullable table has a NULL key (8005) and a real one (8006).
        insert into t304_parent (id, group_id) values (8001, 1), (8002, 1), (8003, 2), (8004, 999);
        insert into t304_nullable_parent (id, group_id) values (8005, null), (8006, 1);
        insert into t304_child (id, parent_id, group_code) values
            (8101, 8001, 'root-a'),
            (8102, 8002, 'root-a'),
            (8103, 8003, 'no-match'),
            (8107, 8003, 'beta'),
            (8104, 8004, 'root-a'),
            (8105, 8005, 'root-a'),
            (8106, 8006, 'alpha'),
            (8108, 8006, 'root-a');
        """;

    public CorrelatedReferenceHopTests()
    {
        Execute(Ddl);

        var ctx = _sut.DataProvider;
        ctx.From<T304Root>();
        ctx.From<T304Group>(b => b.HasOne(g => g.Root, g => g.RootId));
        ctx.From<T304Parent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        ctx.From<T304NullableParent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        ctx.From<T304Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
    }

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private List<int> ParentIdsByHop(int hops)
    {
        var inner = Context.From<T304Child>();

        if (hops == 1)
        {
            return Context.From<T304Parent>()
                .Where(p => SqlFunctions.Sql.exists(inner.Where(
                    c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();
        }

        return Context.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
    }

    /// <summary>
    /// A1/A4: the hop-2 correlated reference executes and returns exactly the seeded matches - not a
    /// superset produced by a lost correlation, and not an empty set produced by a broken join.
    /// </summary>
    [Fact]
    public void S_Hop2_ReturnsExactRows()
    {
        ParentIdsByHop(2).Should().Equal(8001, 8002);
    }

    /// <summary>A3: a parent whose child matches neither hop contributes no row.</summary>
    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var inner = Context.From<T304Child>();

        Context.From<T304Parent>()
            .Where(p => p.Id == 8003 && SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id)
            .ToList()
            .Should().BeEmpty();
    }

    /// <summary>
    /// A3: a dangling non-null foreign key joins to no principal, so its correlated scalar is NULL and
    /// the row must not match - a lost correlation would produce a false positive here.
    /// </summary>
    [Fact]
    public void NullReference_ReturnsEmpty()
    {
        var inner = Context.From<T304Child>();

        Context.From<T304Parent>()
            .Where(p => p.Id == 8004 && SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id)
            .ToList()
            .Should().BeEmpty();
    }

    /// <summary>
    /// A3/A4: the nullable foreign key translates and executes without a type mismatch, matches when
    /// the key resolves (8006) and never matches the NULL key (8005) at either hop.
    /// </summary>
    [Fact]
    public void NullableForeignKey_Matrix()
    {
        var inner = Context.From<T304Child>();

        Context.From<T304NullableParent>()
            .Where(p => p.Id == 8006 && SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(new[] { 8006 }, "the nullable key resolves to group 1 and child 8106 carries its code");

        Context.From<T304NullableParent>()
            .Where(p => p.Id == 8006 && SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(new[] { 8006 }, "the same key resolves two hops up and child 8108 carries the root label");

        Context.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(new[] { 8006 }, "the NULL-key row (8005) never matches");

        Context.From<T304NullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(new[] { 8006 }, "nor does it match two hops up");
    }

    /// <summary>
    /// A4: bare reference presence inside the correlated body (E) and the reference scalar projected by
    /// the correlated subquery (P) both execute over the same seed: presence selects exactly the rows
    /// with a resolvable principal, and the projection distinguishes hop 1 from hop 2 by value.
    /// </summary>
    [Fact]
    public void PresenceAndProjection_HopMatrix()
    {
        var inner = Context.From<T304Child>();

        Context.From<T304Parent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group != null)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(new[] { 8001, 8002, 8003 }, "8004's principal is dangling, so its presence test is false");

        var hop1 = Context.From<T304Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Code = Context.From<T304Child>()
                    .Where(c => c.ParentId == p.Id)
                    .Select(c => p.Group!.Code)
                    .FirstOrDefault()
            })
            .ToList();

        hop1.Select(r => (r.Id, r.Code)).Should().Equal(
            (8001, (string?)"alpha"),
            (8002, "alpha"),
            (8003, "beta"),
            (8004, (string?)null));

        var hop2 = Context.From<T304Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Label = Context.From<T304Child>()
                    .Where(c => c.ParentId == p.Id)
                    .Select(c => p.Group!.Root!.Label)
                    .FirstOrDefault()
            })
            .ToList();

        hop2.Select(r => (r.Id, r.Label)).Should().Equal(
            (8001, (string?)"root-a"),
            (8002, "root-a"),
            (8003, "root-b"),
            (8004, (string?)null));
    }

    /// <summary>
    /// A5: two identically shaped commands carry the same captured parameter name and CLR type with
    /// distinct values; the live oracle must return each command's own rows, and re-running the first
    /// must still bind its own value (no cross-talk through the cached plan).
    /// </summary>
    [Fact]
    public void ParameterIdentity_Isolated()
    {
        var inner = Context.From<T304Child>();
        List<int> Hop2(string expected)
            => Context.From<T304Parent>()
                .Where(p => SqlFunctions.Sql.exists(inner.Where(
                    c => c.ParentId == p.Id
                         && c.GroupCode == p.Group!.Root!.Label
                         && p.Group.Root.Label == expected)))
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();

        Hop2("root-a").Should().Equal(8001, 8002);
        Hop2("root-b").Should().BeEmpty("the second command binds its own value under the same parameter name");
        Hop2("root-a").Should().Equal(new[] { 8001, 8002 }, "a cached re-run still binds the first command's value");
    }
}

[SqlTable("t304_root")]
public sealed class T304Root
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("t304_group")]
public sealed class T304Group
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public string? Code { get; set; }

    [Column("root_id")]
    public int RootId { get; set; }

    public T304Root? Root { get; set; }
}

[SqlTable("t304_parent")]
public sealed class T304Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

    public T304Group? Group { get; set; }
}

[SqlTable("t304_nullable_parent")]
public sealed class T304NullableParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int? GroupId { get; set; }

    public T304Group? Group { get; set; }
}

[SqlTable("t304_child")]
public sealed class T304Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("group_code")]
    public string? GroupCode { get; set; }

    public T304Parent? Parent { get; set; }
}

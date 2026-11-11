using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T304 (#304) cross-provider conformance: a declared reference navigation used inside a correlated
/// EXISTS body executes on every provider that derives <see cref="CommonTestSuite"/> (SQLite,
/// PostgreSQL, SQL Server, MySQL) and returns the rows the join semantics imply. The fixture is
/// class-owned (<c>t304s_*</c> tables created from portable DDL) so the suite never depends on a
/// provider's seed schema, and every provider runs the same cells: hop 1, hop 2+, missing/dangling
/// references, the nullable foreign key, presence, scalar projection and parameter identity.
///
/// <para>
/// No unsupported-provider skip is used: these are reference-only forms (no collection capability and
/// no APPLY), which all four live providers support. MariaDB and ClickHouse have no live harness in
/// this suite; their SQL-generation files pin the same shape.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    private const string T304Ddl = """
        drop table if exists t304s_child;
        drop table if exists t304s_nullable_parent;
        drop table if exists t304s_parent;
        drop table if exists t304s_group;
        drop table if exists t304s_root;
        """;

    private const string T304Create = """
        create table t304s_root (id int not null primary key, label varchar(50) null);
        create table t304s_group (id int not null primary key, code varchar(50) null, root_id int not null);
        create table t304s_parent (id int not null primary key, group_id int not null);
        create table t304s_nullable_parent (id int not null primary key, group_id int null);
        create table t304s_child (id int not null primary key, parent_id int not null, group_code varchar(50) null);
        """;

    private const string T304Seed = """
        insert into t304s_root (id, label) values (1, 'root-a'), (2, 'root-b');
        insert into t304s_group (id, code, root_id) values (1, 'alpha', 1), (2, 'beta', 2);
        insert into t304s_parent (id, group_id) values (8001, 1), (8002, 1), (8003, 2), (8004, 999);
        insert into t304s_nullable_parent (id, group_id) values (8005, null), (8006, 1);
        insert into t304s_child (id, parent_id, group_code) values
            (8101, 8001, 'root-a'),
            (8102, 8002, 'root-a'),
            (8103, 8003, 'no-match'),
            (8107, 8003, 'beta'),
            (8104, 8004, 'root-a'),
            (8105, 8005, 'root-a'),
            (8106, 8006, 'alpha'),
            (8108, 8006, 'root-a');
        """;

    private void T304CreateFixture()
    {
        var ctx = _sut.DataProvider;
        T304Execute(ctx, T304Ddl);
        T304Execute(ctx, T304Create);
        T304Execute(ctx, T304Seed);

        ctx.From<T304sRoot>();
        ctx.From<T304sGroup>(b => b.HasOne(g => g.Root, g => g.RootId));
        ctx.From<T304sParent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        ctx.From<T304sNullableParent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        ctx.From<T304sChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
    }

    private static void T304Execute(IDataContext ctx, string sql)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// A1/A4: hop 1 and hop 2+ over the live provider. Hop 1 matches exactly the parent whose child
    /// carries the group code (8003 via child 8107); hop 2 matches exactly the parents whose child
    /// carries the root label (8001, 8002) - the historical red arm's row set.
    /// </summary>
    [Fact]
    public void T304_ReferenceHop_ScalarExists_ShouldReturnExactRowsAtBothHops()
    {
        T304CreateFixture();
        var ctx = _sut.DataProvider;
        var inner = ctx.From<T304sChild>();

        var hop1 = ctx.From<T304sParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        hop1.Should().Equal(8003);

        var hop2 = ctx.From<T304sParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        hop2.Should().Equal(8001, 8002);
    }

    /// <summary>
    /// A3: a child that matches neither hop (8003 at hop 2) and a dangling non-null foreign key (8004)
    /// contribute no row. A lost correlation would produce a false positive for the dangling key.
    /// </summary>
    [Fact]
    public void T304_ReferenceHop_NoMatchAndNullReference_ShouldReturnNoRows()
    {
        T304CreateFixture();
        var ctx = _sut.DataProvider;
        var inner = ctx.From<T304sChild>();

        ctx.From<T304sParent>()
            .Where(p => p.Id == 8003 && SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id)
            .ToList()
            .Should().BeEmpty();

        ctx.From<T304sParent>()
            .Where(p => p.Id == 8004 && SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id)
            .ToList()
            .Should().BeEmpty();
    }

    /// <summary>A4: bare reference presence - only rows with a resolvable principal and a child remain.</summary>
    [Fact]
    public void T304_ReferenceHop_Presence_ShouldFollowTheJoinedKey()
    {
        T304CreateFixture();
        var ctx = _sut.DataProvider;
        var inner = ctx.From<T304sChild>();

        ctx.From<T304sParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(c => c.ParentId == p.Id && p.Group != null)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(new[] { 8001, 8002, 8003 }, "8004's principal is dangling, so its presence test is false");
    }

    /// <summary>
    /// A4: the reference scalar projected by the correlated subquery. Hop 1 yields the group code and
    /// hop 2 the root label - different values, so a lost hop would be visible, and the dangling key
    /// projects NULL instead of failing.
    /// </summary>
    [Fact]
    public void T304_ReferenceHop_ScalarProjection_ShouldReadTheCorrelatedAlias()
    {
        T304CreateFixture();
        var ctx = _sut.DataProvider;

        var hop1 = ctx.From<T304sParent>()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Code = ctx.From<T304sChild>()
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

        var hop2 = ctx.From<T304sParent>()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Label = ctx.From<T304sChild>()
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
    /// A3: the nullable foreign key translates and executes without a type mismatch at both hops, and
    /// never matches the NULL key (8005).
    /// </summary>
    [Fact]
    public void T304_ReferenceHop_NullableForeignKey_ShouldTranslateAndExecute()
    {
        T304CreateFixture();
        var ctx = _sut.DataProvider;
        var inner = ctx.From<T304sChild>();

        ctx.From<T304sNullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(8006);

        ctx.From<T304sNullableParent>()
            .Where(p => SqlFunctions.Sql.exists(inner.Where(
                c => c.ParentId == p.Id && c.GroupCode == p.Group!.Root!.Label)))
            .Select(p => p.Id)
            .ToList()
            .Should().Equal(8006);
    }

    /// <summary>
    /// A5: two identically shaped commands carry the same captured parameter name and CLR type with
    /// distinct values; each returns its own rows and the first still binds its own value afterwards.
    /// </summary>
    [Fact]
    public void T304_ReferenceHop_ParameterIdentity_ShouldStayIsolated()
    {
        T304CreateFixture();
        var ctx = _sut.DataProvider;
        var inner = ctx.From<T304sChild>();

        List<int> Hop2(string expected)
            => ctx.From<T304sParent>()
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

[SqlTable("t304s_root")]
public sealed class T304sRoot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("t304s_group")]
public sealed class T304sGroup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public string? Code { get; set; }

    [Column("root_id")]
    public int RootId { get; set; }

    public T304sRoot? Root { get; set; }
}

[SqlTable("t304s_parent")]
public sealed class T304sParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

    public T304sGroup? Group { get; set; }
}

[SqlTable("t304s_nullable_parent")]
public sealed class T304sNullableParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int? GroupId { get; set; }

    public T304sGroup? Group { get; set; }
}

[SqlTable("t304s_child")]
public sealed class T304sChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("group_code")]
    public string? GroupCode { get; set; }

    public T304sParent? Parent { get; set; }
}

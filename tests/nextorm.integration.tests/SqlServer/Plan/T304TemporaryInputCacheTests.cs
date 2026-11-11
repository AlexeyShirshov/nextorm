using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T304 (#304) A7/R10 on the live SQL Server container: the correlated reference-hop preparation must
/// stay local to the nested command even when the correlated body reads a <b>temporary input</b> - a
/// session temporary table or a table-valued parameter. Neither branch may mutate the context-shared
/// <c>QueryCommand</c> that <c>Any()</c> reuses: its sticky <c>Cache = false</c> flag would silently
/// disable the plan cache for the whole context.
///
/// <para>
/// The temporary-table form reads a real <c>#session</c> table as the correlated inner source and keeps
/// the reference hop on the outer entity. The TVP form binds through the raw command executor (TVPs
/// never enter query preparation) and can therefore only touch the process-wide caches; both facts
/// assert that the shared command keeps its identity, its FROM and its outer-reference list, and that
/// the reference hop still returns its exact rows afterwards.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class T304TemporaryInputCacheTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private const string Ddl = """
        drop table if exists t304ss_child;
        drop table if exists t304ss_parent;
        drop table if exists t304ss_group;
        create table t304ss_group (id int not null primary key, code varchar(50) null);
        create table t304ss_parent (id int not null primary key, group_id int not null);
        create table t304ss_child (id int not null primary key, parent_id int not null, group_code varchar(50) null);
        insert into t304ss_group (id, code) values (1, 'alpha'), (2, 'beta');
        insert into t304ss_parent (id, group_id) values (8001, 1), (8002, 1), (8003, 2), (8004, 999);
        """;

    private void CreateFixture()
    {
        var ctx = Context;
        Execute(ctx, Ddl);
        ctx.From<T304ssGroup>();
        ctx.From<T304ssParent>(b => b.HasOne(p => p.Group, p => p.GroupId));
        ctx.From<T304ssChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// A session temporary table is the correlated inner input while the outer entity carries the
    /// reference hop. The shared Any command must keep its identity and state, and the hop must still
    /// return exactly the matching parents afterwards.
    /// </summary>
    [Fact]
    public void TemporaryTable_PreservesSharedQueryCommand()
    {
        CreateFixture();
        var ctx = Context;
        var tempTable = "#t304_temp_" + Guid.NewGuid().ToString("N")[..8];

        Execute(ctx, $"create table {tempTable} (parent_id int not null, group_code varchar(50) null)");
        Execute(ctx, $"insert into {tempTable} (parent_id, group_code) values "
                     + "(8001, 'alpha'), (8002, 'alpha'), (8003, 'beta'), (8004, 'alpha')");

        try
        {
            // (1) Use the context-shared Any command first, then snapshot its observable state.
            ctx.From<T304ssParent>().Where(p => p.Id > 0).Any().Should().BeTrue();
            var shared = ctx.AnyCommand!.Value;
            shared.Cache.Should().BeTrue();
            var sharedFrom = shared.From;
            var sharedOuterReferences = shared.OuterReferences;

            // (2) The correlated reference hop reads the session temporary table as its inner source:
            //     the joined group alias must stay correlated inside the EXISTS.
            var temp = ctx.From(tempTable);
            var ids = ctx.From<T304ssParent>()
                .Where(p => SqlFunctions.Sql.exists(temp.Where(t =>
                    t.GetInt32("parent_id") == p.Id && t.GetString("group_code") == p.Group!.Code)))
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();

            ids.Should().Equal(new[] { 8001, 8002, 8003 },
                "each temp row matches its parent's group code; the dangling key (8004) matches nothing");

            // (3) The shared command is the same object with the same state.
            ctx.AnyCommand!.Value.Should().BeSameAs(shared);
            shared.Cache.Should().BeTrue(
                "a temporary-table correlated input must not flip the shared command's sticky cache flag");
            shared.From.Should().BeSameAs(sharedFrom);
            shared.OuterReferences.Should().BeSameAs(sharedOuterReferences,
                "the joined reference belongs to the nested command, never to the shared outer command");
        }
        finally
        {
            Execute(ctx, $"drop table if exists {tempTable}");
        }
    }

    /// <summary>
    /// The table-valued-parameter route binds and executes through the raw command executor, never
    /// through query preparation, and leaves the shared command untouched. The correlated reference hop
    /// runs before and after it and keeps producing the same exact rows.
    /// </summary>
    [Fact]
    public void Tvp_PreservesSharedQueryCommand()
    {
        CreateFixture();
        var ctx = Context;
        var typeName = "dbo.T304Tvp_" + Guid.NewGuid().ToString("N");

        Execute(ctx, $"create type {typeName} as table (value int not null)");

        try
        {
            var inner = ctx.From<T304ssChild>();
            List<int> Hop1()
                => ctx.From<T304ssParent>()
                    .Where(p => SqlFunctions.Sql.exists(inner.Where(
                        c => c.ParentId == p.Id && c.GroupCode == p.Group!.Code)))
                    .OrderBy(p => p.Id)
                    .Select(p => p.Id)
                    .ToList();

            // (1) The reference hop prepares against the real context (no children yet: empty set).
            Hop1().Should().BeEmpty();

            ctx.From<T304ssParent>().Where(p => p.Id > 0).Any().Should().BeTrue();
            var shared = ctx.AnyCommand!.Value;
            shared.Cache.Should().BeTrue();
            var sharedFrom = shared.From;
            var sharedOuterReferences = shared.OuterReferences;

            // (2) A table-valued parameter binds through the raw executor.
            using (var result = ctx.ExecuteRaw(
                       "select value from @p where value > 1",
                       [ProcedureParameter.Table("p", typeName, new[] { 1, 2, 3 })]))
            {
                result.Read<int>().Should().Equal(2, 3);
            }

            // (3) The reference hop still prepares and runs, and the shared command is unchanged.
            Hop1().Should().BeEmpty();
            ctx.AnyCommand!.Value.Should().BeSameAs(shared);
            shared.Cache.Should().BeTrue(
                "a table-valued parameter must not flip the shared command's sticky cache flag");
            shared.From.Should().BeSameAs(sharedFrom);
            shared.OuterReferences.Should().BeSameAs(sharedOuterReferences);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName};");
        }
    }
}

[SqlTable("t304ss_group")]
public sealed class T304ssGroup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public string? Code { get; set; }
}

[SqlTable("t304ss_parent")]
public sealed class T304ssParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

    public T304ssGroup? Group { get; set; }
}

[SqlTable("t304ss_child")]
public sealed class T304ssChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("group_code")]
    public string? GroupCode { get; set; }

    public T304ssParent? Parent { get; set; }
}

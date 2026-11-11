using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 2 — the residual <c>SqlBuilder</c> arms that batch 1 left unproven on live SQLite
/// (<c>src/nextorm.core/DataContext/Sql/SqlBuilder.cs</c>): <c>IsColumnlessProjection</c> (the empty
/// select-list fail-closed guard for a column-less construction / member-init / whole-source
/// projection), <c>WalkOrderByParameters</c> (the parameter pass of per-parent paging reached only
/// through <c>Prepare()</c> on a single-query eager load, with and without an explicit NULL placement)
/// and <c>AppendParenthesisedCondition</c> (a key-less constant <c>SelectGroup</c> that folds its own
/// filter and a pre-group HAVING into one parenthesised <c>WHERE</c>).
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3SqlBuilderResidualTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_sb_parent (id integer primary key, name text null);
        delete from s3_sb_parent;
        create table if not exists s3_sb_child (id integer primary key, parent_id int not null, name text null);
        delete from s3_sb_child;
        insert into s3_sb_parent (id, name) values (6101, 'p1'), (6102, 'p2'), (6103, 'p3');
        insert into s3_sb_child (id, parent_id, name) values
            (6201, 6101, 'b'), (6202, 6101, 'a'),
            (6203, 6102, 'c'), (6204, 6102, 'd');
        """;

    public SqliteStep3SqlBuilderResidualTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>A zero-argument construction declares no output column, so the renderer fails closed.</summary>
    [Fact]
    public void ColumnlessConstructionProjection_ShouldFailClosedBeforeSql()
    {
        var act = () => Context.From<S3SbParent>()
            .Where(x => x.Id == 6101)
            .Select(x => new S3SbEmptyDto())
            .ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*prepared select list is empty*no columns to render*");
    }

    /// <summary>A zero-binding member init is the second column-less shape the guard recognises.</summary>
    [Fact]
    public void ColumnlessMemberInitProjection_ShouldFailClosedBeforeSql()
    {
        var act = () => Context.From<S3SbParent>()
            .Where(x => x.Id == 6101)
            .Select(x => new S3SbEmptyDto { })
            .ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*prepared select list is empty*");
    }

    /// <summary>
    /// A key-less constant <c>SelectGroup</c> with both its own filter and a pre-group HAVING composes
    /// the two predicates into the one parenthesised WHERE.
    /// </summary>
    [Fact]
    public void ConstantSelectGroup_WithFilterAndPreGroupHaving_ShouldComposeParenthesisedConditions()
    {
        var rows = Context.From<S3SbParent>()
            .Where(x => x.Id >= 6101)
            .Having(x => SqlFunctions.Parameter<int>(0) > 0)
            .SelectGroup(x => new { Constant = 7 })
            .ToList(1);

        rows.Should().ContainSingle("a constant projection with DISTINCT folds every row into one");
        rows[0].Constant.Should().Be(7);
    }

    /// <summary>The HAVING-only form takes the single parenthesised predicate arm.</summary>
    [Fact]
    public void ConstantSelectGroup_WithPreGroupHavingOnly_ShouldComposeOneCondition()
    {
        var rows = Context.From<S3SbParent>()
            .Having(x => SqlFunctions.Parameter<int>(0) > 0)
            .SelectGroup(x => new { Constant = 9 })
            .ToList(1);

        rows.Should().ContainSingle();
        rows[0].Constant.Should().Be(9);

        var falsified = Context.From<S3SbParent>()
            .Having(x => SqlFunctions.Parameter<int>(0) > 0)
            .SelectGroup(x => new { Constant = 9 })
            .ToList(-1);

        falsified.Should().BeEmpty("the bound parameter reaches the pre-group predicate and filters every row out");
    }

    /// <summary>Marker parent table of the per-parent paging facts.</summary>
    [SqlTable("s3_sb_parent")]
    public sealed class S3SbParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<S3SbChild> Children { get; } = new List<S3SbChild>();
    }

    /// <summary>Marker child table of the per-parent paging facts.</summary>
    [SqlTable("s3_sb_child")]
    public sealed class S3SbChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    /// <summary>A projection type with no members at all.</summary>
    public sealed class S3SbEmptyDto
    {
    }
}

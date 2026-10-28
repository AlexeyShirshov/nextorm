using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// #249 / T353 D4a: the collection-valued projection slot's generated SQL on PostgreSQL. The slot is carried by
/// an entity query, so the statements it issues are exactly the eager-loading ones: the split shape is a parent
/// statement plus one child statement (never one per parent) and the single shape is one denormalized join
/// statement. No correlation column is appended for the slot, the adapter's <c>Limit</c> never leaks into SQL
/// (it pages the materialized collection in memory), and a derived-table rank is allowed where the existing
/// many-to-many lowering needs it.
/// <para>
/// These tests never open a connection: the statements are observed at the preparation seam of the entity query
/// the carrier holds, so a placeholder connection string is enough. The executed statement counts are asserted
/// against a real database by the core suite (<c>CollectionProjectionSlotTests</c>, <c>…DepthTests</c>).
/// </para>
/// </summary>
public class CollectionProjectionSqlGenerationTests
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    /// <summary>The dialect's row-number spelling, which the many-to-many derived link table projects.</summary>
    private const string RankFunction = "row_number()";

    [SqlTable("t353p_parent")]
    public sealed class Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public List<Child> Children { get; } = [];

        public List<Tag> Tags { get; } = [];
    }

    [SqlTable("t353p_child")]
    public sealed class Child
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("rank")]
        public int Rank { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        public List<Tag> Tags { get; } = [];
    }

    [SqlTable("t353p_tag")]
    public sealed class Tag
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    [SqlTable("t353p_parent_tag")]
    public sealed class ParentTag
    {
        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    [SqlTable("t353p_child_tag")]
    public sealed class ChildTag
    {
        [Column("child_id")]
        public int ChildId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    private static IDataContext Context(EagerLoadMode mode)
        => new PostgresDataContext(
            PlaceholderConnectionString,
            new DataContextBuilder().UseEagerLoadMode(mode));

    /// <summary>Registers the one-to-many and both many-to-many relationships the slot graph declares.</summary>
    private static void Register(IDataContext context)
    {
        _ = context.From<Parent>(cfg => cfg
            .HasMany(p => p.Children, c => c.ParentId)
            .HasManyThrough<Tag, ParentTag, int, int>(p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.TagId));
        _ = context.From<Child>(cfg => cfg
            .HasManyThrough<Tag, ChildTag, int, int>(c => c.Tags, c => c.Id, l => l.ChildId, t => t.Id, l => l.TagId));
    }

    /// <summary>
    /// The entity query the carrier holds. The carrier is internal (the projection result is an anonymous type),
    /// so the reference is read the way the other slot tests read carrier state.
    /// </summary>
    private static object EntityBuilderOf(QueryCommand command)
    {
        var carrier = command.CollectionSlotCarrier;
        carrier.Should().NotBeNull("a recognized collection slot is carried by an entity query");
        var field = carrier!.GetType().GetField("_entityBuilder", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull("the carrier keeps the entity query it executes");
        return field!.GetValue(carrier)!;
    }

    private static string Canon(string sql)
    {
        var s = sql
            .Replace("\r\n", "\n")
            .Replace('"', '\0')
            .Replace("`", string.Empty)
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);
        s = s.Replace("\0", string.Empty);
        return Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static string PreparedSql(IDataContext context, QueryCommand command)
        => Canon(((dynamic)context).GetPreparedQueryCommand((dynamic)command, false, false, CancellationToken.None).DbCommand.CommandText);

    /// <summary>
    /// The statements the slot's terminal issues: one denormalized pair command in single-query mode, otherwise
    /// the parent statement plus one child statement per split declaration (one key chunk for any parent count
    /// below the chunk size, which is what the executed counts in the core suite measure).
    /// </summary>
    private static int PlannedStatements(dynamic builder)
        => builder.SingleQuery ? 1 : 1 + builder.LoadSpecs.Count;

    private static (string Sql, int Statements) SplitSql(IDataContext context, object builder)
    {
        dynamic entityBuilder = builder;
        return (PreparedSql(context, entityBuilder.ToParentCommand(true)), PlannedStatements(entityBuilder));
    }

    private static (string Sql, int Statements) SingleSql(IDataContext context, object builder)
    {
        dynamic entityBuilder = builder;
        var pair = entityBuilder.BuildSingleQueryJoins();
        return (PreparedSql(context, entityBuilder.CreatePairCommand(pair.Item1, pair.Item2)), 1);
    }

    [Fact]
    public void DirectSlot_Split_ShouldIssueTheParentStatementAndOneChildStatement()
    {
        using var context = Context(EagerLoadMode.SplitQuery);
        Register(context);

        QueryCommand command = context.From<Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Children });

        var (sql, statements) = SplitSql(context, EntityBuilderOf(command));

        statements.Should().Be(2, "the parent statement plus one child statement, never one per parent");
        sql.Should().Contain("from t353p_parent");
        sql.Should().Contain("order by");
        sql.Should().NotContain("join", "the split shape loads the collection in its own statement");
        sql.Should().NotContain("parentkey").And.NotContain("childkey")
            .And.NotContain("occurrence", "no correlation column is appended for the slot");
    }

    [Fact]
    public void DirectSlot_Single_ShouldIssueOneJoinStatementWithoutACorrelationColumn()
    {
        using var context = Context(EagerLoadMode.SingleQuery);
        Register(context);

        QueryCommand command = context.From<Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Children });

        var (sql, statements) = SingleSql(context, EntityBuilderOf(command));

        statements.Should().Be(1, "the single-query mode flattens the graph into one statement");
        sql.Should().Contain("from t353p_parent");
        sql.Should().Contain("join t353p_child");
        sql.Should().Contain("on ");
        sql.Should().NotContain("parentkey").And.NotContain("childkey").And.NotContain("occurrence");
    }

    [Fact]
    public void AdapterSlot_Split_ShouldKeepThePageOutOfTheSql()
    {
        using var context = Context(EagerLoadMode.SplitQuery);
        Register(context);

        QueryCommand command = context.From<Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Items = p.Children.AsEntityBuilder<Child>()
                    .Where(c => c.Active)
                    .OrderBy(c => c.Rank)
                    .Limit(1)
                    .Select(c => new { c.Id, c.Rank })
                    .ToList(),
            });

        var (sql, statements) = SplitSql(context, EntityBuilderOf(command));

        statements.Should().Be(2);
        sql.Should().Contain("from t353p_parent");
        sql.Should().NotContain("join");
        sql.Should().NotContain(" limit ").And.NotContain(" fetch ").And.NotContain(" top ",
            "the adapter page is applied per parent in memory, never as a global SQL page");
    }

    [Fact]
    public void AdapterSlot_Single_ShouldKeepThePageOutOfTheSql()
    {
        using var context = Context(EagerLoadMode.SingleQuery);
        Register(context);

        QueryCommand command = context.From<Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Items = p.Children.AsEntityBuilder<Child>()
                    .Where(c => c.Active)
                    .OrderBy(c => c.Rank)
                    .Limit(2)
                    .Select(c => new { c.Id, c.Rank })
                    .ToList(),
            });

        var (sql, statements) = SingleSql(context, EntityBuilderOf(command));

        statements.Should().Be(1);
        sql.Should().Contain("join t353p_child");
        sql.Should().NotContain(" limit ").And.NotContain(" fetch ").And.NotContain(" top ");
    }

    [Fact]
    public void FlatManyToMany_Single_ShouldRenderTheDerivedLinkTableWithItsRank()
    {
        using var context = Context(EagerLoadMode.SingleQuery);
        Register(context);

        QueryCommand command = context.From<Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Tags });

        var (sql, statements) = SingleSql(context, EntityBuilderOf(command));

        statements.Should().Be(1);
        sql.Should().Contain("from t353p_parent_tag", "the junction is a derived link subquery");
        sql.Should().Contain(RankFunction, "a derived-table rank is allowed for the link lowering");
        sql.Should().Contain("occurrence", "the derived link table carries the occurrence rank");
        sql.Should().Contain("parentkey").And.Contain("childkey");
    }

    [Fact]
    public void NestedManyToMany_Single_ShouldRenderTheNestedGraphInOneStatement()
    {
        using var context = Context(EagerLoadMode.SingleQuery);
        Register(context);

        QueryCommand command = context.From<Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Items = p.Children.AsEntityBuilder<Child>()
                    .OrderBy(c => c.Id)
                    .Select(c => new { c.Id, Tags = c.Tags })
                    .ToList(),
            });

        var (sql, statements) = SingleSql(context, EntityBuilderOf(command));

        statements.Should().Be(1, "the inherited single mode flattens every level");
        sql.Should().Contain("join t353p_child");
        sql.Should().Contain("from t353p_child_tag", "the level-two many-to-many link is joined as a derived link table");
        sql.Should().Contain("join t353p_tag");
    }

    [Fact]
    public void ManyToManyWithLimit_ShouldBeRefusedBeforeAnySql()
    {
        using var context = Context(EagerLoadMode.SplitQuery);
        Register(context);

        var act = () => context.From<Parent>()
            .Select(p => new
            {
                p.Id,
                Tags = p.Tags.AsEntityBuilder<Tag>().OrderBy(t => t.Id).Limit(1).ToList(),
            });

        // The refusal is raised while the projection is bound: no command exists yet, so no statement can reach
        // a connection and no plan entry is created.
        act.Should().Throw<NotSupportedException>().WithMessage("*M:N + Limit*");
    }
}

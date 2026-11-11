using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class D362CompositionTests
{
    private sealed class E
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public int Group2 { get; set; }
    }

    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    private sealed class FilteredE
    {
        public long Id { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public bool Active { get; set; }
        public bool IsDeleted { get; set; }

        public static Expression<Func<FilteredE, bool>> ActiveOnly => x => x.Active && !x.IsDeleted;
    }

    // A second grouping owner with its own slot. It carries one explicit key, so two instances of the
    // same slot can be applied with different keys to prove replacement.
    private sealed class OtherGroupingComponent : IQueryComponent
    {
        private readonly LambdaExpression _key;
        public OtherGroupingComponent(LambdaExpression key) { _key = key; }
        public Type Slot => typeof(OtherGroupingComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveGrouping;
        public object? ShapeState => "OtherGrouping";
        public int ShapeSelectorCount => 1;
        public LambdaExpression[] ShapeSelectors => new[] { _key };
        public LambdaExpression GetShapeSelector(int index) => _key;
        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
        {
            if (context.Command.DataContext is InMemoryDataContext)
                throw new NotSupportedException("in-memory");
            var keys = new[] { _key };
            var prep = new QueryPreparationContribution(keys, QueryPreparationHavingMode.Unchanged, null, null, false);
            var hash = new HashCode();
            hash.Add(Slot);
            hash.Add(_key, context.ExpressionComparer);
            return new Desc(this, prep, hash.ToHashCode());
        }
        public void BuildQuery(IQueryComponentBuildContext context) { }
        internal sealed class Desc : IQueryPreparationDescriptor
        {
            private readonly OtherGroupingComponent _c;
            public Desc(OtherGroupingComponent c, QueryPreparationContribution p, int h) { _c = c; Preparation = p; StructuralHash = h; }
            public Type Slot => _c.Slot;
            public Type ComponentType => _c.GetType();
            public QueryComponentContribution Contribution => _c.Contribution;
            public string Discriminator => "Other";
            public int StructuralHash { get; }
            public QueryPreparationContribution Preparation { get; }
            public bool StructurallyEquals(IQueryComponentDescriptor? other) =>
                other is Desc rhs && Slot == rhs.Slot && StructuralHash == rhs.StructuralHash;
        }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);

    /// <summary>The single rendered GROUP BY line, or an empty string.</summary>
    private static string GroupByLine(string sql)
        => sql.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("group by ", StringComparison.Ordinal))?.Trim() ?? string.Empty;

    /// <summary>The single rendered WHERE line, or an empty string.</summary>
    private static string WhereLine(string sql)
        => sql.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("where ", StringComparison.Ordinal))?.Trim() ?? string.Empty;

    private static int CountOf(string sql, string fragment)
        => Regex.Matches(sql, Regex.Escape(fragment)).Count;

    private static int DistinctCount(string sql)
        => Regex.Matches(sql, @"\bdistinct\b", RegexOptions.IgnoreCase).Count;

    // 1. Same slot applied twice: the second component replaces the first before preparation, so the
    //    rendered statement groups by the second component's key only, in exactly one GROUP BY clause.
    [Fact]
    public void SameSlot_Replacement_UsesTheSecondComponentsKeys()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();

        var first = new OtherGroupingComponent((Expression<Func<E, int>>)(x => x.Group2));
        var second = new OtherGroupingComponent((Expression<Func<E, string>>)(x => x.Category!));

        var cmd = e.WithComponent(first).WithComponent(second).Select(x => x.Id);
        var sql = SqlOf(ctx, cmd);

        GroupByLine(sql).Should().Be("group by Category");
        GroupByLine(sql).Should().NotContain("Group2", "the replaced component's key must not leak into the statement");
        CountOf(sql, "group by").Should().Be(1);
    }

    // 2. Two distinct grouping owners: concrete exception type and a message naming both slots, in both
    //    addition orders.
    [Fact]
    public void TwoGroupingOwners_Rejected_BothOrders_ConcreteException()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var key = (LambdaExpression)(Expression<Func<E, long>>)(x => x.Id);
        var packaged = new SelectGroupComponent<E>((Expression<Func<E, long>>)(x => x.Id));
        var foreign = new OtherGroupingComponent(key);

        var firstOrder = e.WithComponent(packaged).WithComponent(foreign).Select(x => x.Id);
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(firstOrder, false, true, CancellationToken.None))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*Multiple distinct exclusive grouping components*'SelectGroupComponent`1' and 'OtherGroupingComponent'*");

        var secondOrder = e.WithComponent(foreign).WithComponent(packaged).Select(x => x.Id);
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(secondOrder, false, true, CancellationToken.None))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*Multiple distinct exclusive grouping components*'OtherGroupingComponent' and 'SelectGroupComponent`1'*");
    }

    // 3. Exclusive grouping + exclusive row source: concrete exception type and the conflict fragment,
    //    in both addition orders.
    [Fact]
    public void GroupingAndExclusiveRowSource_Rejected_BothOrders_ConcreteException()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var grouping = new SelectGroupComponent<E>((Expression<Func<E, long>>)(x => x.Id));
        var reshape = new ExtremeRowComponent<E>(ExtremeKind.Min, (LambdaExpression)(Expression<Func<E, int?>>)(x => x.Score), ExtremeRowTies.One, null);

        var firstOrder = e.WithComponent(grouping).WithComponent(reshape).Select(x => x.Id);
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(firstOrder, false, true, CancellationToken.None))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*exclusive grouping component*cannot be combined*exclusive row-source*");

        var secondOrder = e.WithComponent(reshape).WithComponent(grouping).Select(x => x.Id);
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(secondOrder, false, true, CancellationToken.None))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*exclusive grouping component*cannot be combined*exclusive row-source*");
    }

    // 4. Additive predicate once: the entity/global filter, the user Where and the component's additive
    //    predicate each appear exactly once in the WHERE; the captured value is bound once.
    [Fact]
    public void AdditivePredicate_AppliedOnce_WithEntityFilterAndUserWhere()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seed = connection.CreateCommand())
        {
            seed.CommandText = "CREATE TABLE FilteredE (Id INTEGER PRIMARY KEY, Category TEXT NULL, Amount REAL NOT NULL, Active INTEGER NOT NULL, IsDeleted INTEGER NOT NULL);";
            seed.ExecuteNonQuery();
        }

        using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        var min = 5m;

        var cmd = ctx.From<FilteredE>()
            .Where(x => x.Amount > min)
            .WithComponent(new AdditivePredicateComponent<FilteredE>((Expression<Func<FilteredE, bool>>)(x => x.Category != "x")))
            .SelectGroup(x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) });

        var prepared = Prepare(ctx, cmd);
        var sql = Normalize(prepared.SqlStmt ?? string.Empty);

        var where = WhereLine(sql);
        where.Should().Be("where (((Amount > $min) and (Active and not (IsDeleted))) and Category != 'x')", "exact WHERE composition");
        CountOf(sql, "Active").Should().Be(1, "the entity filter is applied exactly once");
        CountOf(sql, "IsDeleted").Should().Be(1, "the entity filter's second conjunct appears exactly once");
        CountOf(sql, "Amount >").Should().Be(1, "the user Where predicate is applied exactly once");
        CountOf(sql, "Category !=").Should().Be(1, "the component's additive predicate is applied exactly once");

        // The prepared parameter collection itself is engine-internal and this assembly has no IVT
        // (the contract forbids one); the observable evidence is the single placeholder in the
        // statement plus the refresh flag that marks a value-dependent capture.
        CountOf(sql, "$min").Should().Be(1, "the captured value placeholder occurs exactly once");
        prepared.NoParams.Should().BeFalse();
        prepared.NeedsParamRefresh.Should().BeTrue("a captured value must refresh on every execution");
    }

    // 5. DISTINCT is monotonic: user DISTINCT and require-DISTINCT never double, and a user DISTINCT is
    //    never removed by a keyed grouping.
    [Fact]
    public void Distinct_UserAndRequired_SingleKeyword()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var sql = SqlOf(ctx, ctx.From<E>().Distinct().SelectGroup(x => new { V = 1 }));

        DistinctCount(sql).Should().Be(1);
    }

    [Fact]
    public void Distinct_RequiredOnly_SingleKeyword()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var sql = SqlOf(ctx, ctx.From<E>().SelectGroup(x => new { V = 1 }));

        DistinctCount(sql).Should().Be(1);
    }

    [Fact]
    public void Distinct_UserOnly_IsNeverRemoved()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var sql = SqlOf(ctx, ctx.From<E>().Distinct().SelectGroup(x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) }));

        DistinctCount(sql).Should().Be(1);
        GroupByLine(sql).Should().Be("group by Category");
        sql.Should().StartWith("select distinct", "a user DISTINCT survives a keyed grouping");
    }

    // 6. HAVING ownership (PF1b): Replace contributions carry only the ADDED condition; the engine
    //    ANDs the raw source Having exactly once. Clear carries the full replacement (constant
    //    projection lowers it into the pre-group WHERE).
    [Fact]
    public void Having_WithSelectGroup_AppliedOnce()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var a = 5;

        var sql = SqlOf(ctx, ctx.From<E>()
            .Having(x => SqlFunctions.Sql.count(x.Id) > a)
            .SelectGroup(x => new { x.Category, Total = SqlFunctions.Sql.count(x.Id) }));

        CountOf(sql, "having").Should().Be(1, "one HAVING clause");
        CountOf(sql, "count(Id) >").Should().Be(1, "the raw source predicate appears once");
        CountOf(sql, "$a").Should().Be(1, "its parameter is bound once");
    }

    [Fact]
    public void Having_WithSelectGroupHaving_BothPredicatesOnce()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var a = 5;
        var b = 10;

        var sql = SqlOf(ctx, ctx.From<E>()
            .Having(x => SqlFunctions.Sql.count(x.Id) > a)
            .SelectGroupHaving(
                x => new { x.Category, Total = SqlFunctions.Sql.count(x.Id) },
                r => r.Total > b));

        CountOf(sql, "having").Should().Be(1);
        CountOf(sql, "count(Id) >").Should().Be(2, "each of the two aggregate predicates appears once");
        CountOf(sql, "$a").Should().Be(1, "the raw source predicate parameter is bound once");
        CountOf(sql, "$b").Should().Be(1, "the operator condition parameter is bound once");
    }

    [Fact]
    public void GlobalAggregation_WithExistingHaving_OneHaving()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var a = 5;

        var sql = SqlOf(ctx, ctx.From<E>()
            .Having(x => SqlFunctions.Sql.count(x.Id) > a)
            .SelectGroup(x => new { Total = SqlFunctions.Sql.count(x.Id) }));

        CountOf(sql, "having").Should().Be(1);
        CountOf(sql, "count(Id) >").Should().Be(1);
        CountOf(sql, "$a").Should().Be(1);
        CountOf(sql, "group by").Should().Be(0, "a global aggregate has no grouping key");
    }

    [Fact]
    public void ConstantProjection_WithExistingHaving_LoweredOnceWithOneDistinct()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var a = 5;
        var b = 10;

        var sql = SqlOf(ctx, ctx.From<E>()
            .Having(x => a > 0)
            .SelectGroupHaving(x => new { Constant = 7 }, r => b > 0));

        CountOf(sql, "having").Should().Be(0, "the constant outcome lowers the predicate into WHERE");
        CountOf(sql, "where").Should().Be(1);
        CountOf(sql, "$a").Should().Be(1, "the existing Having predicate appears once");
        CountOf(sql, "$b").Should().Be(1, "the operator condition appears once");
        DistinctCount(sql).Should().Be(1, "the constant projection keeps one DISTINCT");
    }

    [Fact]
    public void PlainQuery_WithExistingHaving_StillDropped()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        var sql = SqlOf(ctx, ctx.From<E>().Having(x => x.Id > 1).Select(x => new { x.Id }));

        CountOf(sql, "having").Should().Be(0, "an ungrouped legacy HAVING keeps its historical silent drop");
    }
}

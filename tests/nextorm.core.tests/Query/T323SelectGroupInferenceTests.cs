using FluentAssertions;
using NextORM.Postgres;

namespace NextORM.Core.Tests;

/// <summary>A plain row for the T323 <c>SelectGroup</c> key-inference matrix.</summary>
public class T323LegacyEntity
{
    public int Id { get; set; }
    public int? Group2 { get; set; }
    public decimal Amount { get; set; }
    public string? Name { get; set; }
    public DateTime OrderedAt { get; set; }
}

/// <summary>A second row for the subquery shapes of the T323 guard matrix.</summary>
public class T323LegacyOther
{
    public int Id { get; set; }
    public decimal Value { get; set; }
}

/// <summary>
/// T323 (#323) D2: one fact per cell of the <c>SelectGroup</c> key-inference matrix. Every expectation
/// is expressed through the SQL of the equivalent explicit <c>GroupBy</c>/<c>Select</c> form, never a
/// hand-written SQL string, except where the cell is about the absence of a key. Only preparation runs:
/// the context carries a placeholder connection string, so no database is opened.
/// </summary>
public class T323SelectGroupInferenceTests
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    private static IDataContext CreateContext()
        => new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder());

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>
    /// Extracts the rendered <c>GROUP BY</c> line. Assertions on the key list must not be satisfied by
    /// an aggregate's argument in the select list, which precedes the clause.
    /// </summary>
    private static string GroupByLine(string sql)
    {
        var index = sql.IndexOf("group by ", StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "the rendered SQL must carry a GROUP BY clause");
        var end = sql.IndexOf('\n', index);
        return end < 0 ? sql[index..] : sql[index..end];
    }

    /// <summary>
    /// Renders the explicit form of a query whose projection references the raw row member
    /// <c>x.OrderedAt</c>; used as the negative oracle of "the key is the whole scalar function".
    /// </summary>
    private static string OrderedAtOnlyKeySql(IDataContext ctx)
        => SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.OrderedAt)
            .Select(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            }));

    [Fact]
    public void WholeScalarFunction_ShouldBeOneKey()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt))
            .Select(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().Contain("date_trunc");
        GroupByLine(sql).Should().NotBe(GroupByLine(OrderedAtOnlyKeySql(ctx)),
            "the whole scalar function is a key, not a bare row column");
    }

    [Fact]
    public void AggregatePlusMember_ShouldUseOnlyTheMember()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount), K = x.Amount + 1 });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Amount + 1)
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount), K = x.Amount + 1 });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().NotContainEquivalentOf("sum",
            "an aggregate's argument never contributes a key");
    }

    [Fact]
    public void AggregatePlusRowMember_ShouldGroupOnlyByTheMember()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) + x.Amount });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Amount)
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount) + x.Amount });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));

        var keyedByMemberOnly = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Amount + 1)
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount) + x.Amount }));
        GroupByLine(sql).Should().NotBe(GroupByLine(keyedByMemberOnly),
            "the mixed aggregate/member expression descends to the bare row member");
    }

    [Fact]
    public void AggregatePlusProduct_ShouldKeepTheWholeProduct()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount), M = x.Amount * 2 });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Amount * 2)
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount), M = x.Amount * 2 });
        var memberOnlyKey = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Amount)
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount), M = x.Amount * 2 }));

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().NotBe(GroupByLine(memberOnlyKey),
            "the product is one maximal key, not its row member alone");
    }

    [Fact]
    public void AggregatePlusColumnProduct_ShouldKeepTheWholeProduct()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount), M = x.Amount * x.Id });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Amount * x.Id)
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount), M = x.Amount * x.Id });
        var memberOnlyKey = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount), M = x.Amount * x.Id }));

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().NotBe(GroupByLine(memberOnlyKey),
            "only one of the product's operands is not a key of its own");
    }

    [Fact]
    public void ScalarWrapperAroundAggregate_ShouldDescend()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { V = SqlFunctions.Sql.greatest(SqlFunctions.Sql.sum(x.Amount), 0m) });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .Select(x => new { V = SqlFunctions.Sql.greatest(SqlFunctions.Sql.sum(x.Amount), 0m) });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().NotContain("group by", "a scalar wrapper around an aggregate contributes no key");
    }

    [Fact]
    public void DuplicateKeys_ShouldCollapse()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { A = x.Id, B = x.Id });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { A = x.Id, B = x.Id });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().NotContain(",", "one structural key renders once");
    }

    [Fact]
    public void DifferentLiterals_ShouldStayTwoKeys()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                M = SqlFunctions.Sql.date_trunc("month", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                M = SqlFunctions.Sql.date_trunc("month", x.OrderedAt),
            })
            .Select(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                M = SqlFunctions.Sql.date_trunc("month", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().Contain(",",
            "a different literal makes a different key even when the row argument is the same");
    }

    [Fact]
    public void DifferentCapturedMembers_ShouldStayTwoKeys()
    {
        using var ctx = CreateContext();
        var a = 2;
        var b = 2;

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { A = x.Amount * a, B = x.Amount * b });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { A = x.Amount * a, B = x.Amount * b })
            .Select(x => new { A = x.Amount * a, B = x.Amount * b });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().Contain(",", "two captured members are two closure identities, not one value");
    }

    [Fact]
    public void RepeatedCapturedMember_ShouldCollapseToOneKey()
    {
        using var ctx = CreateContext();
        var a = 2;

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { A = x.Amount * a, B = x.Amount * a });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Amount * a)
            .Select(x => new { A = x.Amount * a, B = x.Amount * a });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().NotContain(",",
            "the same captured member is one structural key");
    }

    [Fact]
    public void DistinctAggregate_ShouldNotLeakItsArgument()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, D = SqlFunctions.Sql.sum_distinct(x.Amount) });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, D = SqlFunctions.Sql.sum_distinct(x.Amount) });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().NotContainEquivalentOf("amount",
            "the DISTINCT aggregate's argument is opaque");
    }

    [Fact]
    public void FilteredAggregate_ShouldNotLeakItsArgumentOrPredicate()
    {
        using var ctx = CreateContext();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { K = x.Group2, S = SqlFunctions.Sql.sum(x.Amount, () => x.Id > 5) });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Group2)
            .Select(x => new { K = x.Group2, S = SqlFunctions.Sql.sum(x.Amount, () => x.Id > 5) });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        GroupByLine(sql).Should().NotContainEquivalentOf("id",
            "neither the argument nor the FILTER predicate of an aggregate contributes a key");
        GroupByLine(sql).Should().NotContainEquivalentOf("amount");
    }

    [Fact]
    public void OrderOfKeys_ShouldFollowFirstAppearance()
    {
        using var ctx = CreateContext();

        // The aggregate comes first in the anonymous shape, so the keys appear in the order the row
        // expressions are visited: Group2, then Id.
        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });
        var sameOrder = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Group2, x.Id })
            .Select(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });
        var reversed = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Id, x.Group2 })
            .Select(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });

        var inferredSql = SqlOf(ctx, inferred);
        var sameOrderSql = SqlOf(ctx, sameOrder);

        inferredSql.Should().Be(sameOrderSql);
        GroupByLine(inferredSql).Should().Be(GroupByLine(sameOrderSql));
        GroupByLine(inferredSql).Should().NotBe(GroupByLine(SqlOf(ctx, reversed)));
    }
}

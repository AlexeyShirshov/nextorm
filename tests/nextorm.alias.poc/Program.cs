using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_poc;
using NextORM.Sqlite;

namespace Poc;

/// <summary>PoC entity with a repeated CLR type in the join (Buyer/Approver are both <see cref="Person"/>).</summary>
public sealed class Order
{
    public int Id { get; set; }

    public int BuyerId { get; set; }

    public int ApproverId { get; set; }
}

/// <summary>PoC entity joined twice under two aliases.</summary>
public sealed class Person
{
    public int Id { get; set; }
}

/// <summary>Captures the SQL of every executed command.</summary>
internal sealed class SqlRecordingInterceptor : IQueryInterceptor
{
    public List<string> Statements { get; } = new();

    public void CommandExecuting(CommandEventData eventData, DbCommand command)
        => Statements.Add(eventData.Sql ?? command.CommandText);
}

/// <summary>
/// Throwaway feasibility spike for issue #113 (join alias projection): two aliased joins over the
/// same CLR type, resolved to distinct slots and executed end-to-end on SQLite.
/// </summary>
public static class Program
{
    private const string SqlLogPath = "/tmp/opencode/poc-sql.txt";

    public static void Main()
    {
        // Point 4 (runtime, no query execution): the generated lexical properties resolve to
        // distinct slot markers, even though both carry the same CLR type Person.
        var projection = typeof(AliasProjection_Buyer_Approver<Order, Person, Person>);
        var buyer = projection.GetProperty("Buyer")!;
        var approver = projection.GetProperty("Approver")!;
        var buyerSlot = ((JoinSlotAttribute)buyer.GetCustomAttribute(typeof(JoinSlotAttribute))!).Position;
        var approverSlot = ((JoinSlotAttribute)approver.GetCustomAttribute(typeof(JoinSlotAttribute))!).Position;
        var sameClrType = buyer.PropertyType == typeof(Person) && approver.PropertyType == typeof(Person);

        // Point 2 (runtime MemberInfo): p.Buyer.Id / p.Approver.Id expression trees bind to the
        // generated lexical properties, not to Item1/Item2.
        Expression<Func<AliasProjection_Buyer_Approver<Order, Person, Person>, int>> byApprover = p => p.Approver.Id;
        Expression<Func<AliasProjection_Buyer_Approver<Order, Person, Person>, int>> byBuyer = p => p.Buyer.Id;
        var approverMember = ((MemberExpression)((MemberExpression)byApprover.Body).Expression!).Member;
        var buyerMember = ((MemberExpression)((MemberExpression)byBuyer.Body).Expression!).Member;

        Console.WriteLine($"POC-SLOT Buyer={buyerSlot} Approver={approverSlot} sameClrType={sameClrType}");
        Console.WriteLine($"POC-MEMBER buyer={buyerMember.DeclaringType}.{buyerMember.Name} approver={approverMember.DeclaringType}.{approverMember.Name}");

        var path = Path.Combine(Path.GetTempPath(), $"nextorm-alias-poc-{Guid.NewGuid():N}.db");
        var interceptor = new SqlRecordingInterceptor();
        try
        {
            Seed(path);

            using var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().AddInterceptor(interceptor));
            var people = ctx.From<Person>(b => b.Table("person"));

            // Point 3: a local variable holds the intermediate; a method adds the second join and
            // returns the generated builder type through its stable generated name.
            var intermediate = ctx.From<Order>(b => b.Table("orders"))
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer);
            var chained = AddApprover(intermediate, people);

            var buyerIds = chained.Select(p => p.Buyer.Id).ToList();
            var approverIds = chained.Select(p => p.Approver.Id).ToList();

            File.WriteAllText(SqlLogPath,
                "BUYER SQL:\n" + interceptor.Statements[0] + "\n\nAPPROVER SQL:\n" + interceptor.Statements[1] + "\n");

            Console.WriteLine("POC-SQL-BUYER " + interceptor.Statements[0]);
            Console.WriteLine("POC-SQL-APPROVER " + interceptor.Statements[1]);
            Console.WriteLine($"POC-EXEC buyerIds=[{string.Join(",", buyerIds)}] approverIds=[{string.Join(",", approverIds)}]");

            if (buyerIds.Count != 1 || approverIds.Count != 1
                || buyerIds[0] != 10 || approverIds[0] != 20
                || buyerIds[0] == approverIds[0])
            {
                throw new InvalidOperationException(
                    $"Alias slots did not resolve to distinct expected rows: buyerIds=[{string.Join(",", buyerIds)}] approverIds=[{string.Join(",", approverIds)}]");
            }

            Console.WriteLine("POC-RESULT slotResolved=True distinctIds=True");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        CheckAliasOperators();
    }

    /// <summary>
    /// Exercises the <c>JoinAlias</c> seam for the conditional operators and the Cartesian
    /// <c>Cross</c> operator, comparing each against its positional counterpart over the same data:
    /// the rendered SQL must carry the operator's keyword, and the alias-projected slot must produce
    /// exactly the positional results. (APPLY cannot be exercised here: SQLite has no APPLY.)
    /// </summary>
    private static void CheckAliasOperators()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-alias-ops-{Guid.NewGuid():N}.db");
        try
        {
            SeedOperators(path);
            var interceptor = new SqlRecordingInterceptor();
            using var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().AddInterceptor(interceptor));
            var people = ctx.From<Person>(b => b.Table("person"));

            // INNER
            {
                var positional = ctx.From<Order>(b => b.Table("orders"))
                    .Join(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
                var alias = ctx.From<Order>(b => b.Table("orders"))
                    .Join(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                    .Select(p => p.Buyer.Id).ToList();
                AssertOperator("Inner", "join ", positional, alias, interceptor.Statements[^1]);
            }

            // LEFT
            {
                var positional = ctx.From<Order>(b => b.Table("orders"))
                    .LeftJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
                var alias = ctx.From<Order>(b => b.Table("orders"))
                    .LeftJoin(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                    .Select(p => p.Buyer.Id).ToList();
                AssertOperator("Left", " left join ", positional, alias, interceptor.Statements[^1]);
            }

            // RIGHT
            {
                var positional = ctx.From<Order>(b => b.Table("orders"))
                    .RightJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
                var alias = ctx.From<Order>(b => b.Table("orders"))
                    .RightJoin(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                    .Select(p => p.Buyer.Id).ToList();
                AssertOperator("Right", " right join ", positional, alias, interceptor.Statements[^1]);
            }

            // FULL
            {
                var positional = ctx.From<Order>(b => b.Table("orders"))
                    .FullJoin(people, (a, b) => a.BuyerId == b.Id).Select(p => p.Item2.Id).ToList();
                var alias = ctx.From<Order>(b => b.Table("orders"))
                    .FullJoin(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                    .Select(p => p.Buyer.Id).ToList();
                AssertOperator("Full", " full join ", positional, alias, interceptor.Statements[^1]);
            }

            // CROSS (conditionless overload)
            {
                var positional = ctx.From<Order>(b => b.Table("orders"))
                    .CrossJoin(people).Select(p => p.Item2.Id).ToList();
                var alias = ctx.From<Order>(b => b.Table("orders"))
                    .CrossJoin(people, Alias.Buyer)
                    .Select(p => p.Buyer.Id).ToList();
                AssertOperator("Cross", " cross join ", positional, alias, interceptor.Statements[^1]);
            }

            Console.WriteLine("POC-OPS-RESULT allMatched=True");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Compile-only coverage for the conditionless APPLY operators: SQLite cannot render APPLY, but the
    /// generated overloads must bind and forward their <c>JoinType</c> (this method is never invoked).
    /// </summary>
    internal static void CheckApplyCompiles(IDataContext ctx)
    {
        var people = ctx.From<Person>(b => b.Table("person"));
        _ = ctx.From<Order>(b => b.Table("orders")).CrossApply(people, Alias.Buyer);
        _ = ctx.From<Order>(b => b.Table("orders")).OuterApply(people, Alias.Buyer);
    }

    private static void AssertOperator(string name, string keyword, List<int> positional, List<int> alias, string sql)
    {
        if (!positional.OrderBy(x => x).SequenceEqual(alias.OrderBy(x => x)))
        {
            throw new InvalidOperationException(
                $"Alias {name} operator diverged from positional: positional=[{string.Join(",", positional)}] alias=[{string.Join(",", alias)}]");
        }

        if (!sql.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Alias {name} operator did not render the expected keyword '{keyword}': {sql}");
        }

        Console.WriteLine($"POC-OP-{name} keyword='{keyword}' rows=[{string.Join(",", alias)}]");
    }

    private static void SeedOperators(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "create table orders (Id integer primary key, BuyerId integer, ApproverId integer);" +
            "create table person (Id integer primary key);" +
            "insert into orders (Id, BuyerId, ApproverId) values (1, 10, 20), (2, 20, 10);" +
            "insert into person (Id) values (10), (20);";
        cmd.ExecuteNonQuery();
    }

    // Point 3: the split method names the generated types in its signature.
    private static AliasJoin_Buyer_Approver<Order, Person, Person> AddApprover(
        AliasJoin_Buyer<Order, Person> builder,
        EntityBuilder<Person> people)
        => builder.Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver);

    private static void Seed(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "create table orders (Id integer primary key, BuyerId integer, ApproverId integer);" +
            "create table person (Id integer primary key);" +
            "insert into orders (Id, BuyerId, ApproverId) values (1, 10, 20);" +
            "insert into person (Id) values (10), (20);";
        cmd.ExecuteNonQuery();
    }
}

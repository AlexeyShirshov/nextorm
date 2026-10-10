using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Alias-slot coverage for an ordinary typed CTE (#146 slice A). A <c>Cte&lt;T&gt;</c> read through
/// <c>From(cte)</c> and joined through a generated alias slot must be referenced by the CTE name under a
/// distinct table alias, with its columns binding to that alias. The suite also covers a self-join of
/// the same typed CTE and a join of two distinct same-type typed CTEs.
/// </summary>
public class TypedCteAliasTests
{
    [Fact]
    public void Typed_cte_joined_as_alias_slot_gets_its_own_alias_and_binds_columns()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var peopleCte = ctx.From<Person>(b => b.Table("person")).ToCommand().AsCte("people_cte");

            var rows = orders
                .Join<Person>(ctx.From(peopleCte), (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, BuyerName = p.Buyer.Name })
                .ToList();

            // The single order's buyer is person 10 ('Buyer').
            rows.Should().ContainSingle();
            rows[0].OrderId.Should().Be(1);
            rows[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            rows[0].BuyerName.Should().Be("Buyer");

            // The CTE is declared once, referenced by name in the join and given its own alias; the
            // projected columns bind to that alias, not to a derived-table wrapper.
            var last = sql.Statements[^1];
            last.Should().Contain("with people_cte as (");
            last.Should().Contain("join people_cte as 't2'");
            last.Should().Contain("t2.Id");
            last.Should().Contain("t2.Name");
            last.Should().NotContain("join (select");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Direct_typed_cte_joined_as_alias_slot_gets_its_own_alias_and_binds_columns()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var peopleCte = ctx.From<Person>(b => b.Table("person")).ToCommand().AsCte("people_cte");

            // #159: the descriptor is passed straight to the generated alias overload.
            var rows = orders
                .Join<Person>(peopleCte, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, BuyerName = p.Buyer.Name })
                .ToList();

            rows.Should().ContainSingle();
            rows[0].OrderId.Should().Be(1);
            rows[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            rows[0].BuyerName.Should().Be("Buyer");

            var last = sql.Statements[^1];
            last.Should().Contain("with people_cte as (");
            last.Should().Contain("join people_cte as 't2'");
            last.Should().Contain("t2.Id");
            last.Should().Contain("t2.Name");
            last.Should().NotContain("join (select");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Self_join_of_same_typed_cte_uses_two_aliases_and_one_declaration()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var peopleCte = ctx.From<Person>(b => b.Table("person")).ToCommand().AsCte("people_cte");

            // The typed CTE is the base source and is also joined as an alias slot: a real self-join of
            // the same descriptor, whose columns must bind to two distinct aliases.
            var rows = ctx.From(peopleCte)
                .Join<Person>(ctx.From(peopleCte), (a, b) => a.Id == b.Id, Alias.Buyer)
                .Select(p => new { Id = p.Item1.Id, BuyerName = p.Buyer.Name })
                .ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(10, 20);
            rows.Single(r => r.Id == 10).BuyerName.Should().Be("Buyer");
            rows.Single(r => r.Id == 20).BuyerName.Should().Be("Approver");

            var last = sql.Statements[^1];
            last.Should().Contain("from people_cte as 't1'");
            last.Should().Contain("join people_cte as 't2'");
            last.Should().Contain("t2.Id");

            // The same descriptor is reused on both sides, so its declaration must not be duplicated.
            CountOccurrences(last, "people_cte as (").Should().Be(1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Join_of_two_distinct_same_type_typed_ctes_binds_each_to_its_alias()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var buyersCte = ctx.From<Person>(b => b.Table("person")).Where(p => p.Id < 20).ToCommand().AsCte("buyers_cte");
            var approversCte = ctx.From<Person>(b => b.Table("person")).Where(p => p.Id > 10).ToCommand().AsCte("approvers_cte");

            var rows = orders
                .Join<Person>(ctx.From(buyersCte), (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Join<Person>(ctx.From(approversCte), (a, p) => a.Item1.ApproverId == p.Id, Alias.Approver)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, ApproverId = p.Approver.Id })
                .ToList();

            // Only order 1 has buyer 10 (< 20) and approver 20 (> 10); each side must keep its own body.
            rows.Should().ContainSingle();
            rows[0].OrderId.Should().Be(1);
            rows[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            rows[0].ApproverId.Should().Be(AliasSqliteDatabase.ApproverId);

            var last = sql.Statements[^1];
            last.Should().Contain("with buyers_cte as (");
            last.Should().Contain(", approvers_cte as (");
            last.Should().Contain("join buyers_cte as 't2'");
            last.Should().Contain("join approvers_cte as 't3'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // #146 slice B / D5: a recursive typed CTE through the alias-slot machinery. The recursive
    // declaration is hoisted once (`with recursive`), read by its bare name, and its columns bind to
    // the join alias; a consumer CTE built From the recursive one reads through a distinct alias.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Recursive_typed_cte_joined_as_alias_slot_gets_its_own_alias_and_binds_columns()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            // Bounded Person series: 10 then 20 (the step stops once the current Id reaches 20).
            var peopleCte = ctx.From<Person>(b => b.Table("person"))
                .Where(p => p.Id == 10)
                .ToCommand()
                .AsRecursiveCte("people_nums", self => ctx.From(self)
                    .Where(p => p.Id < 20)
                    .Select(p => new Person { Id = p.Id + 10, Name = p.Name }));

            var rows = ctx.From<Order>(b => b.Table("orders"))
                .Join<Person>(ctx.From(peopleCte), (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, BuyerName = p.Buyer.Name })
                .ToList();

            rows.Should().ContainSingle();
            rows[0].OrderId.Should().Be(1);
            rows[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            rows[0].BuyerName.Should().Be("Buyer");

            var last = sql.Statements[^1];
            last.Should().Contain("with recursive people_nums as (");
            last.Should().Contain("union all");
            last.Should().Contain("join people_nums as 't2'");
            last.Should().Contain("t2.Id");
            last.Should().Contain("t2.Name");
            last.Should().NotContain("join (select");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Recursive_typed_cte_as_source_and_chained_consumer_read_through_name()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var peopleCte = ctx.From<Person>(b => b.Table("person"))
                .Where(p => p.Id == 10)
                .ToCommand()
                .AsRecursiveCte("nums", self => ctx.From(self)
                    .Where(p => p.Id < 30)
                    .Select(p => new Person { Id = p.Id + 10, Name = p.Name }));

            // From(Cte<T>) outside the step: the recursive CTE is the main source, read by name.
            var direct = ctx.From(peopleCte).Select(p => new { p.Id, p.Name }).ToList();
            direct.Select(r => r.Id).OrderBy(id => id).Should().Equal(10, 20, 30);

            // A consumer CTE depends on the recursive declaration and is ordered after it.
            var consumer = ctx.From(peopleCte).Select(p => new { p.Id }).AsCte("consumer");
            var rows = ctx.From(consumer).Select(r => r.Id).ToList();
            rows.OrderBy(id => id).Should().Equal(10, 20, 30);

            var last = sql.Statements[^1];
            last.Should().Contain("with recursive nums as (");
            last.Should().Contain(", consumer as (");
            last.Should().Contain("from nums");
            last.Should().Contain("from consumer");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // #160 root alias on a recursive self-reference: '.WithAlias(Alias.Root)' names slot 1 of the
    // AsRecursiveCte step source (From(CteReference<T>)), and the step keeps reading the CTE by name.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Root_alias_on_a_recursive_cte_self_reference_names_slot_one()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var peopleCte = ctx.From<Person>(b => b.Table("person"))
                .Where(p => p.Id == 10)
                .ToCommand()
                .AsRecursiveCte("people_root_alias", self =>
                    ctx.From(self)
                        .WithAlias(Alias.Root)
                        .Where(p => p.Root.Id < 20)
                        .Select(p => new Person { Id = p.Root.Id + 10, Name = p.Root.Name }));

            var rows = ctx.From(peopleCte).Select(p => new { p.Id, p.Name }).ToList();
            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(10, 20);

            var last = sql.Statements[^1];
            last.Should().Contain("with recursive people_root_alias as (");
            last.Should().Contain("union all");
            last.Should().Contain("from people_root_alias as 't1'");
            last.Should().NotContain("join (select");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // #159 C1 regression: two chains with the SAME base, alias sequence, operators and joined types
    // that differ only in an INTERMEDIATE step's source kind (EntityBuilder<Person> vs Cte<Person>)
    // previously emitted the tail method twice (CS0111). Both branches must now compile in this one
    // assembly, bind the same alias slots and render identical SQL.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Shared_two_step_chain_with_entity_and_cte_intermediate_compiles_and_binds_both_branches()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var buyersCte = ctx.From<Person>(b => b.Table("person")).Where(p => p.Id < 20).ToCommand().AsCte("buyers_cte");
            var approversCte = ctx.From<Person>(b => b.Table("person")).Where(p => p.Id > 10).ToCommand().AsCte("approvers_cte");

            // Branch 1: the intermediate Buyer step joins the converted builder (EntityBuilder<Person>).
            var entityIntermediate = orders
                .Join<Person>(ctx.From(buyersCte), (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Join<Person>(ctx.From(approversCte), (a, p) => a.Item1.ApproverId == p.Id, Alias.Approver)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, ApproverId = p.Approver.Id })
                .ToList();

            var entitySql = sql.Statements[^1];

            // Branch 2: the intermediate Buyer step joins the descriptor directly (Cte<Person>). Its
            // emitted tail method is byte-identical to branch 1's - the C1 collision shape.
            var cteIntermediate = orders
                .Join<Person>(buyersCte, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Join<Person>(ctx.From(approversCte), (a, p) => a.Item1.ApproverId == p.Id, Alias.Approver)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, ApproverId = p.Approver.Id })
                .ToList();

            var cteSql = sql.Statements[^1];

            cteIntermediate.Should().Equal(entityIntermediate);
            entityIntermediate.Should().ContainSingle();
            entityIntermediate[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            entityIntermediate[0].ApproverId.Should().Be(AliasSqliteDatabase.ApproverId);

            cteSql.Should().Be(entitySql);
            entitySql.Should().Contain("buyers_cte as (").And.Contain("approvers_cte as (");
            entitySql.Should().Contain("join buyers_cte as 't2'").And.Contain("join approvers_cte as 't3'");
            entitySql.Should().NotContain("join (select");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Shared_two_step_chain_with_anonymous_cte_tail_binds_both_branches()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var buyersCte = ctx.From<Person>(b => b.Table("person")).Where(p => p.Id < 20).ToCommand().AsCte("buyers_cte");
            // Anonymous CTE projection: the tail joined type is inferred and becomes the method's own TJoin.
            var approversAnon = ctx.From<Person>(b => b.Table("person"))
                .Where(p => p.Id > 10)
                .Select(p => new { p.Id, p.Name })
                .AsCte("approvers_anon");

            var entityIntermediate = orders
                .Join<Person>(ctx.From(buyersCte), (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Join(approversAnon, (a, p) => a.Item1.ApproverId == p.Id, Alias.Approver)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, ApproverName = p.Approver.Name })
                .ToList();

            var entitySql = sql.Statements[^1];

            var cteIntermediate = orders
                .Join<Person>(buyersCte, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
                .Join(approversAnon, (a, p) => a.Item1.ApproverId == p.Id, Alias.Approver)
                .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, ApproverName = p.Approver.Name })
                .ToList();

            var cteSql = sql.Statements[^1];

            cteIntermediate.Should().Equal(entityIntermediate);
            entityIntermediate.Should().ContainSingle();
            entityIntermediate[0].BuyerId.Should().Be(AliasSqliteDatabase.BuyerId);
            entityIntermediate[0].ApproverName.Should().Be("Approver");

            cteSql.Should().Be(entitySql);
            entitySql.Should().Contain("buyers_cte as (").And.Contain("approvers_anon as (");
            entitySql.Should().Contain("join buyers_cte as 't2'").And.Contain("join approvers_anon as 't3'");
            entitySql.Should().NotContain("join (select");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

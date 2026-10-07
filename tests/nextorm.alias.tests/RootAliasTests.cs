using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// Root alias (<c>.WithAlias(Alias.X)</c>, issue #160 Phase 2). A root alias names slot 1 and must be
/// usable with every root source while keeping the physical primary table and the source state; the
/// in-memory provider fails closed, and the generated surface exposes the root name alongside
/// <c>Item1</c>. The dim-1 (<see cref="Projection{T1}"/>) planner path is proven by the bounded spike
/// below before the generated surface is exercised.
/// </summary>
public class RootAliasTests
{
    /// <summary>Minimal hand-written root projection used only to prove the dim-1 planner path.</summary>
    public sealed class SpikeRootProjection<T> : Projection<T>
    {
        [JoinSlot(1)]
        public T Root => throw new NotSupportedException();
    }

    /// <summary>Minimal hand-written root+join projection used only to probe the planner path.</summary>
    public sealed class SpikeJoinProjection<T1, T2> : Projection<T1, T2>
    {
        [JoinSlot(2)]
        public T2 Buyer => throw new NotSupportedException();
    }

    [Fact]
    public void Root_projection_with_a_join_maps_the_root_slot_to_t1()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));
            var rooted = orders.AliasRoot<EntityBuilder<SpikeRootProjection<Order>>, SpikeRootProjection<Order>>(
                static dc => new EntityBuilder<SpikeRootProjection<Order>>(dc));
            var joined = rooted.JoinAlias<EntityBuilder<SpikeJoinProjection<Order, Person>>, SpikeJoinProjection<Order, Person>, Person>(
                static dc => new EntityBuilder<SpikeJoinProjection<Order, Person>>(dc),
                people,
                (a, b) => a.Root.BuyerId == b.Id);

            var ids = joined.Select(p => p.Buyer.Id).ToList();
            ids.Should().Equal(10);

            var last = sql.Statements[^1];
            last.Should().Contain("orders as 't1'");
            last.Should().Contain("on t1.BuyerId = t2.Id");
            last.Should().Contain("join person as 't2'");
            last.Should().Contain("t2.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Dim1_root_projection_plans_and_maps_the_root_slot_to_t1()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var rooted = orders.AliasRoot<EntityBuilder<SpikeRootProjection<Order>>, SpikeRootProjection<Order>>(
                static dc => new EntityBuilder<SpikeRootProjection<Order>>(dc));

            var ids = rooted.Select(p => p.Root.Id).ToList();
            ids.Should().Equal(1);

            // A single-table root query needs no table alias, but the source must stay the physical
            // table (no derived/nested projection, no flattening) and slot 1 must resolve to its column.
            var last = sql.Statements[^1];
            last.Should().Be("select Id from orders");
            last.Should().NotContain("select * from (");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Projection_dim1_extends_the_root_item_to_slot2()
    {
        IExtendableProjection projection = new Projection<Order> { Item1 = new Order { Id = 7 } };
        var extended = projection.Extend("second");

        extended.Should().BeOfType<Projection<Order, string>>();
        var pair = (Projection<Order, string>)extended;
        pair.Item1.Id.Should().Be(7);
        pair.Item2.Should().Be("second");

        typeof(Projection<Order>).TryGetProjectionDimension(out var dim).Should().BeTrue();
        dim.Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------------
    // Generated surface: '.WithAlias(Alias.X)' across the root matrix (issue #160, Phase 2).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Generated_root_alias_with_an_alias_join_maps_slot1_to_t1()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var rooted = ctx.From<Order>(b => b.Table("orders")).WithAlias(Alias.Root);
            var people = ctx.From<Person>(b => b.Table("person"));

            var joined = rooted.Join<Person>(people, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer);

            joined.Select(p => p.Buyer.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);

            // The alias member 'Root' and the retained Item1 are the SAME slot 1.
            joined.Select(p => p.Root.Id).ToList().Should().Equal(joined.Select(p => p.Item1.Id).ToList());

            var last = sql.Statements[^1];
            last.Should().Contain("orders as 't1'");
            last.Should().Contain("on t1.BuyerId = t2.Id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_root_alias_alone_keeps_the_physical_source()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var rooted = ctx.From<Order>(b => b.Table("orders")).WithAlias(Alias.Root);

            rooted.Select(p => p.Root.Id).ToList().Should().Equal(1);

            // No derived wrapper and no flattening: the single-table root still reads the physical table.
            sql.Statements[^1].Should().Be("select Id from orders");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_root_alias_supports_a_positional_join_after_it()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var rooted = ctx.From<Order>(b => b.Table("orders")).WithAlias(Alias.Root);
            var people = ctx.From<Person>(b => b.Table("person"));

            var joined = rooted.Join(people, (o, p) => o.Root.BuyerId == p.Id);

            joined.Select(p => p.Item2.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            sql.Statements[^1].Should().Contain("orders as 't1'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_root_alias_on_a_raw_table_source_maps_slot1_to_t1()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var rooted = ctx.CreateQueryBuilder("orders").WithAlias(Alias.Root);
            var people = ctx.From<Person>(b => b.Table("person"));

            var joined = rooted.Join<Person>(people, (o, p) => o.Root.GetInt64("BuyerId") == p.Id, Alias.Buyer);

            joined.Select(p => p.Buyer.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            sql.Statements[^1].Should().Contain("orders as 't1'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_root_alias_on_a_fromsql_source_keeps_the_derived_source()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var rooted = ctx.FromSql("select Id, BuyerId from orders").WithAlias(Alias.Root);
            var people = ctx.From<Person>(b => b.Table("person"));

            var joined = rooted.Join<Person>(people, (o, p) => o.Root.GetInt64("BuyerId") == p.Id, Alias.Buyer);

            joined.Select(p => p.Buyer.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            var last = sql.Statements[^1];
            // The derived FromSql source must stay a derived table aliased to the root slot ('t1'), not be
            // unwrapped to the physical table (issue #160, D:160-03, rv=3).
            last.Should().Contain("(select Id, BuyerId from orders) as 't1'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_root_alias_on_a_query_command_source_keeps_the_derived_query()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var source = ctx.From<Order>(b => b.Table("orders")).Where(o => o.Id == 1).ToCommand();
            var rooted = ctx.From(source).WithAlias(Alias.Root);
            var people = ctx.From<Person>(b => b.Table("person"));

            var joined = rooted.Join<Person>(people, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer);

            joined.Select(p => p.Buyer.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            var last = sql.Statements[^1];
            // The derived QueryCommand source must stay a derived table aliased to the root slot ('t1'),
            // not be unwrapped to the physical table (issue #160, D:160-03, rv=3).
            last.Should().Contain("from orders");
            last.Should().Contain("where Id = 1");
            last.Should().Contain(") as 't1'");
            last.Should().NotContain("orders as 't1'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_root_alias_on_a_builder_source_keeps_the_source()
    {
        var (path, ctx, sql) = AliasSqliteDatabase.CreateContext();
        try
        {
            var builder = ctx.From<Order>(b => b.Table("orders"));
            var rooted = ctx.From(builder).WithAlias(Alias.Root);
            var people = ctx.From<Person>(b => b.Table("person"));

            var joined = rooted.Join<Person>(people, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer);

            joined.Select(p => p.Buyer.Id).ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
            var last = sql.Statements[^1];
            // The derived builder source must stay a derived table aliased to the root slot ('t1'), not be
            // unwrapped to the physical table (issue #160, D:160-03, rv=3).
            last.Should().Contain("(select Id, BuyerId, ApproverId from orders) as 't1'");
            last.Should().NotContain("orders as 't1'");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_WithAlias_rejects_a_null_marker()
    {
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));

            Action act = () => orders.WithAlias((Alias.RootMarker)null!);

            act.Should().Throw<ArgumentNullException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_root_alias_fails_closed_on_the_in_memory_provider()
    {
        using var ctx = new InMemoryDataContext();
        var orders = ctx.From<Order>();

        Action act = () => orders.WithAlias(Alias.Root);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Hand_written_AliasRoot_is_refused_after_a_join()
    {
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var orders = ctx.From<Order>(b => b.Table("orders"));
            var people = ctx.From<Person>(b => b.Table("person"));
            var joined = orders.Join(people, (o, p) => o.BuyerId == p.Id);

            // The generated '.WithAlias' is rejected at compile time (NORMGEN008); this hand-written call
            // proves the runtime AliasRoot seam keeps the same root-only guard as a failsafe.
            Action act = () => joined.AliasRoot<EntityBuilder<SpikeRootProjection<Order>>, SpikeRootProjection<Order>>(
                static dc => new EntityBuilder<SpikeRootProjection<Order>>(dc));

            act.Should().Throw<NotSupportedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

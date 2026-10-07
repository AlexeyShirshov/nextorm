using FluentAssertions;
using NextORM.Core;

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
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// #249 / T353 D10 (C7): the structural plan identity of collection-valued projection slots. The carrier
/// executes one entity query whose SQL is variant-independent, so the slot graph itself has to key the plan
/// cache: two projections that differ in CLR destination shape, adapter predicate/order/limit/nested
/// projection shape or the inherited split/single mode own distinct entries, while identical shapes with
/// different captured values share one entry and rebind at execution. The assertions read the plan store's
/// own enumeration (<c>QueryPlanStore.Values</c>), the convention the plan-cache guards already use, not an
/// internal field of the shape.
/// </summary>
[Collection("Query cache controls")]
public sealed class CollectionProjectionSlotPlanIdentityTests
{
    public CollectionProjectionSlotPlanIdentityTests()
    {
        DataContextCache.Clear();
    }

    [SqlTable("t353_key_parent")]
    public sealed class SlotParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public List<SlotChild> Children { get; } = [];
    }

    [SqlTable("t353_key_child")]
    public sealed class SlotChild
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
    }

    public sealed class ChildDto
    {
        public int Id { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>Direct slot, destination <see cref="IReadOnlyCollection{T}"/>.</summary>
    public sealed class DirectReadOnly
    {
        public int Id { get; set; }
        public IReadOnlyCollection<SlotChild> Items { get; set; } = [];
    }

    /// <summary>Direct slot, destination <see cref="List{T}"/> — the same declaration, a different CLR shape.</summary>
    public sealed class DirectList
    {
        public int Id { get; set; }
        public List<SlotChild> Items { get; set; } = [];
    }

    /// <summary>Adapter slot over the navigation element itself, so only the operation set varies.</summary>
    public sealed class AdapterChildren
    {
        public int Id { get; set; }
        public List<SlotChild> Items { get; set; } = [];
    }

    /// <summary>Adapter slot whose <c>Select</c> projects a nested shape.</summary>
    public sealed class AdapterProjection
    {
        public int Id { get; set; }
        public List<ChildDto> Items { get; set; } = [];
    }

    private static (SqliteDataContext Context, string Path) CreateDb(EagerLoadMode mode = EagerLoadMode.SplitQuery)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t353-key-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "create table t353_key_parent (id integer primary key);" +
                "create table t353_key_child (id integer primary key, parent_id int not null, rank int not null, active int not null);" +
                "insert into t353_key_parent (id) values (1), (2);" +
                "insert into t353_key_child (id, parent_id, rank, active) values" +
                " (10, 1, 2, 1), (11, 1, 1, 1), (12, 1, 3, 0);";
            command.ExecuteNonQuery();
        }

        var context = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().UseEagerLoadMode(mode));
        return (context, path);
    }

    private static void RegisterMetadata(SqliteDataContext context)
        => _ = context.From<SlotParent>(cfg => cfg.HasMany(p => p.Children, c => c.ParentId));

    private static void ClearPlanCache(SqliteDataContext context)
    {
        context.PurgeQueryCache();
        PlanEntryCount().Should().Be(0, "the purge must leave a clean plan cache for the counting assertions");
    }

    private static int PlanEntryCount() => QueryPlanStore.Values.Count();

    /// <summary>Executes <paramref name="query"/> and returns how many plan entries it added.</summary>
    private static int NewEntries(Action query)
    {
        var before = PlanEntryCount();
        query();
        return PlanEntryCount() - before;
    }

    private static void AssertParentRows(SqliteDataContext context)
    {
        // Guard against a vacuous run: every shape reads both parents, and parent 2 has no children.
        var ids = context.From<SlotParent>().OrderBy(p => p.Id).Select(p => p.Id).ToList();
        ids.Should().Equal(1, 2);
    }

    [Fact]
    public void DifferentStructuralShapes_ShouldOwnDistinctPlanEntries()
    {
        var (context, path) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            RegisterMetadata(context);
            ClearPlanCache(context);

            // The entity query's SQL and eager declarations are identical for every cell below; only the
            // slot graph differs, so a shared entry would prove the key ignores the slot shape.
            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new DirectReadOnly { Id = p.Id, Items = p.Children }).ToList())
                .Should().Be(1, "the first slot shape stores one plan entry");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new DirectReadOnly { Id = p.Id, Items = p.Children }).ToList())
                .Should().Be(0, "an identical shape reuses its entry");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new DirectList { Id = p.Id, Items = p.Children }).ToList())
                .Should().Be(1, "a different CLR destination shape owns a distinct key");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>().Where(c => c.Active).ToList(),
                    }).ToList())
                .Should().Be(1, "an adapter slot with a predicate owns a distinct key");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>().Where(c => c.Rank > 0).ToList(),
                    }).ToList())
                .Should().Be(1, "a different adapter Where predicate shape owns a distinct key");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Where(c => c.Active).OrderBy(c => c.Id).ToList(),
                    }).ToList())
                .Should().Be(1, "an ordering operation owns a distinct key");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Where(c => c.Active).OrderByDescending(c => c.Id).ToList(),
                    }).ToList())
                .Should().Be(1, "OrderByDescending differs from OrderBy in the key");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Where(c => c.Active).OrderBy(c => c.Id).Limit(1).ToList(),
                    }).ToList())
                .Should().Be(1, "a per-parent Limit owns a distinct key");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterProjection
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Select(c => new ChildDto { Id = c.Id, Rank = c.Rank }).ToList(),
                    }).ToList())
                .Should().Be(1, "a nested projection shape owns a distinct key");

            NewEntries(() => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterProjection
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Select(c => new ChildDto { Id = c.Id }).ToList(),
                    }).ToList())
                .Should().Be(1, "a different nested projection shape owns a distinct key");

            AssertParentRows(context);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CapturedValues_ShouldShareOnePlanEntry_AndRebind()
    {
        var (context, path) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            RegisterMetadata(context);
            ClearPlanCache(context);

            var minRank = 0;
            List<AdapterChildren> Execute()
                => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Where(c => c.Rank >= minRank)
                            .OrderBy(c => c.Id)
                            .ToList(),
                    })
                    .ToList();

            NewEntries(() => Execute()).Should().Be(1, "the shape stores one entry");

            Execute().Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(10, 11, 12);

            minRank = 2;

            NewEntries(() => Execute()).Should().Be(0,
                "a different captured value is execution-time binding, not a different plan");
            Execute().Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(new[] { 10, 12 },
                "the same plan rebinds the new captured value per execution");

            AssertParentRows(context);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void OneDataContext_ShouldIsolatePredicateAndOrderingShapes()
    {
        var (context, path) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            RegisterMetadata(context);
            ClearPlanCache(context);

            var minRank = 0;
            List<AdapterChildren> ShapeA()
                => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Where(c => c.Active && c.Rank >= minRank)
                            .OrderBy(c => c.Id)
                            .ToList(),
                    })
                    .ToList();

            List<AdapterChildren> ShapeB()
                => context.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>()
                            .Where(c => c.Rank > 1)
                            .OrderByDescending(c => c.Id)
                            .Limit(1)
                            .ToList(),
                    })
                    .ToList();

            var firstA = ShapeA();
            firstA.Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(10, 11);
            firstA.Single(r => r.Id == 2).Items.Should().BeEmpty();
            var afterA = PlanEntryCount();
            afterA.Should().BeGreaterThan(0);

            var b = ShapeB();
            b.Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(new[] { 12 },
                "shape B applies its own predicate, descending order and per-parent limit");
            b.Single(r => r.Id == 2).Items.Should().BeEmpty();
            var afterB = PlanEntryCount();
            afterB.Should().BeGreaterThan(afterA, "a different predicate/order/limit shape owns a distinct key");

            minRank = 2;
            var secondA = ShapeA();
            secondA.Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(new[] { 10 },
                "shape A is intact after B and rebinds its changed captured value");
            secondA.Single(r => r.Id == 2).Items.Should().BeEmpty();
            PlanEntryCount().Should().Be(afterB, "the repeated shapes are served by the existing entries");

            AssertParentRows(context);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SplitAndSingle_ShouldOwnDistinctPlanEntries()
    {
        var (split, splitPath) = CreateDb(EagerLoadMode.SplitQuery);
        var (single, singlePath) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            RegisterMetadata(split);
            RegisterMetadata(single);
            ClearPlanCache(split);

            NewEntries(() => split.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>().Where(c => c.Active).ToList(),
                    }).ToList())
                .Should().BeGreaterThan(0, "the split form stores its plan entries");
            var splitEntries = PlanEntryCount();

            NewEntries(() => single.From<SlotParent>().OrderBy(p => p.Id)
                    .Select(p => new AdapterChildren
                    {
                        Id = p.Id,
                        Items = p.Children.AsEntityBuilder<SlotChild>().Where(c => c.Active).ToList(),
                    }).ToList())
                .Should().BeGreaterThan(0, "the inherited single mode is a different plan key");

            PlanEntryCount().Should().BeGreaterThan(splitEntries);

            AssertParentRows(split);
            AssertParentRows(single);
        }
        finally
        {
            split.Dispose();
            single.Dispose();
            File.Delete(splitPath);
            File.Delete(singlePath);
        }
    }
}

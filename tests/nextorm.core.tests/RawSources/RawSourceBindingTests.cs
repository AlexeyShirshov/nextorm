using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Focal coverage for the D1 half of #124: the immutable raw-source entity binding, its guards and the
/// typed copy. Filter injection itself is not exercised here (later task); these tests pin the public
/// contract and the carried binding state.
/// </summary>
public class RawSourceBindingTests
{
    public sealed class BoundEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    // A mapped subclass of TableAlias: only the *exact* TableAlias type is rejected, so this is a valid
    // binding target (D7, planner decision (a) — exact-type guard retained).
    public sealed class BoundTableAlias : TableAlias
    {
        public int Id { get; set; }
    }

    [Fact]
    public void BindEntity_NullSource_ShouldThrow()
    {
        EntityBuilder<TableAlias> source = null!;

        var act = () => source.BindEntity<BoundEntity>(["Id"]);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BindEntity_NullColumns_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var act = () => source.BindEntity<BoundEntity>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BindEntity_BlankColumn_ShouldThrow(string blank)
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var act = () => source.BindEntity<BoundEntity>(["Id", blank]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BindEntity_NullColumnEntry_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var act = () => source.BindEntity<BoundEntity>(["Id", null!]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BindEntity_EmptyColumns_ShouldBeAllowed()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var bound = source.BindEntity<BoundEntity>(Array.Empty<string>());

        bound.SourceFrom!.SourceBinding!.AvailableColumns.Should().BeEmpty();
    }

    [Fact]
    public void BindEntity_TableAlias_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var act = () => source.BindEntity<TableAlias>(["Id"]);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void BindEntity_MappedTableAliasSubclass_ShouldSucceed()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var bound = source.BindEntity<BoundTableAlias>(["Id"]);

        bound.Should().BeOfType<EntityBuilder<BoundTableAlias>>();
        bound.SourceFrom!.SourceBinding!.EntityType.Should().Be<BoundTableAlias>();
    }

    // --- D8: CTE / sub-query-hint state is not composition --------------------------------------

    [Fact]
    public void BindEntity_CteOnlySource_ShouldSucceed()
    {
        using var ctx = new InMemoryDataContext();
        var body = ctx.From<BoundEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var source = ctx.With("recent", body).From("recent");

        var bound = source.BindEntity<BoundEntity>(["Id"]);

        bound.SourceFrom!.SourceBinding!.EntityType.Should().Be<BoundEntity>();
    }

    [Fact]
    public void BindEntity_SubQueryHintOnly_ShouldSucceed()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");
        source.SubQueryHint = "NestLoop(t1)";

        var bound = source.BindEntity<BoundEntity>(["Id"]);

        bound.SourceFrom!.SourceBinding!.EntityType.Should().Be<BoundEntity>();
    }

    [Fact]
    public void BindEntity_CteAndSubQueryHint_ShouldSucceed()
    {
        using var ctx = new InMemoryDataContext();
        var body = ctx.From<BoundEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var source = ctx.FromSql("select 1 as Id");
        source.Ctes = ctx.With("recent", body).Ctes;
        source.SubQueryHint = "NestLoop(t1)";

        var bound = source.BindEntity<BoundEntity>(["Id"]);

        bound.SourceFrom!.SourceBinding!.EntityType.Should().Be<BoundEntity>();
    }

    [Fact]
    public void BindEntity_CteWithComposedPredicate_ShouldStillReject()
    {
        using var ctx = new InMemoryDataContext();
        var body = ctx.From<BoundEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var source = ctx.With("recent", body).From("recent").Where(_ => true);

        var act = () => source.BindEntity<BoundEntity>(["Id"]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BindEntity_NonRawSource_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        var source = new EntityBuilder<TableAlias>(ctx) { SourceFrom = new FromExpression(typeof(BoundEntity)) };

        var act = () => source.BindEntity<BoundEntity>(["Id"]);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void BindEntity_AfterWhere_ShouldThrowInvalidOperation()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id").Where(_ => true);

        var act = () => source.BindEntity<BoundEntity>(["Id"]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BindEntity_AfterOrderBy_ShouldThrowInvalidOperation()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id").OrderBy(t => t["Id"].AsInt);

        var act = () => source.BindEntity<BoundEntity>(["Id"]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BindEntity_ShouldReturnTypedBuilder_AndCarryBinding()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var bound = source.BindEntity<BoundEntity>(["Id", "Name"]);

        bound.Should().BeOfType<EntityBuilder<BoundEntity>>();
        var binding = bound.SourceFrom!.SourceBinding;
        binding.Should().NotBeNull();
        binding!.EntityType.Should().Be<BoundEntity>();
    }

    [Fact]
    public void BindEntity_ShouldNotMutateOriginal()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        _ = source.BindEntity<BoundEntity>(["Id"]);

        source.SourceFrom!.SourceBinding.Should().BeNull();
    }

    [Fact]
    public void BindEntity_ShouldDeduplicateSortAndIgnoreCase()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.FromSql("select 1 as Id");

        var bound = source.BindEntity<BoundEntity>(["Name", "id", "ID", "name", "Extra"]);

        bound.SourceFrom!.SourceBinding!.AvailableColumns.Should().Equal("Extra", "id", "Name");
    }

    [Fact]
    public void BindEntity_FromNamedTable_ShouldCarryBindingAndTable()
    {
        IDataContext ctx = new InMemoryDataContext();
        var source = ctx.From("some_table");

        var bound = source.BindEntity<BoundEntity>(["Id"]);

        bound.SourceFrom!.Table.Should().Be("some_table");
        bound.SourceFrom.SourceBinding!.EntityType.Should().Be<BoundEntity>();
    }
}

using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B D6: in-memory navigation lowering with parity to the SQL path. Declared references are
/// resolved to metadata-based correlated subqueries (scalar chain, presence, nullable lift/coalesce,
/// A7 guard) and the four collection terminals plus the adapter reuse the shared correlation and
/// <see cref="WideCountNarrowing"/> semantics. These mirror the SQLite D3 (references), D4
/// (collection terminals/adapter) and D5 (A7 guard) cases; the registered datasets are authoritative
/// and the CLR navigation graph is never read.
/// </summary>
[Collection("Query cache controls")]
public class ImplicitNavigationD6Tests
{
    public ImplicitNavigationD6Tests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<D6Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<D6Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<D6Parent>().WithData([
            new D6Parent { Id = 1, Age = 42, Name = "p1" },
            new D6Parent { Id = 2, Age = 7, Name = null },
        ]);
        ctx.From<D6Child>().WithData([
            new D6Child { Id = 10, ParentId = 1, Name = "c10" },
            new D6Child { Id = 11, ParentId = 1, Name = "c11" },
            new D6Child { Id = 12, ParentId = 999, Name = "dangling" },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateManyToMany()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<D6MChild>();
        ctx.From<D6MLink>();
        ctx.From<D6MParent>(b => b.HasManyThrough<D6MChild, D6MLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        ctx.From<D6MParent>().WithData([new D6MParent { Id = 1 }, new D6MParent { Id = 2 }]);
        ctx.From<D6MChild>().WithData([new D6MChild { Id = 100 }]);
        ctx.From<D6MLink>().WithData([
            new D6MLink { ParentId = 1, ChildId = 100 },
            new D6MLink { ParentId = 1, ChildId = 100 },
        ]);
        return ctx;
    }

    // #148-B R2.1: parent 1 has 3 valid junction rows (two to the same child) plus 1 dangling; parent 2
    // has only a dangling row; parent 3 has none.
    private static InMemoryDataContext CreateManyToManyWithDangling()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<D6MChild>();
        ctx.From<D6MLink>();
        ctx.From<D6MParent>(b => b.HasManyThrough<D6MChild, D6MLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        ctx.From<D6MParent>().WithData([new D6MParent { Id = 1 }, new D6MParent { Id = 2 }, new D6MParent { Id = 3 }]);
        ctx.From<D6MChild>().WithData([new D6MChild { Id = 100 }, new D6MChild { Id = 101 }]);
        ctx.From<D6MLink>().WithData([
            new D6MLink { ParentId = 1, ChildId = 100 },
            new D6MLink { ParentId = 1, ChildId = 100 },
            new D6MLink { ParentId = 1, ChildId = 101 },
            new D6MLink { ParentId = 1, ChildId = 999 },
            new D6MLink { ParentId = 2, ChildId = 999 },
        ]);
        return ctx;
    }

    // ---- Reference navigation (D3 parity) --------------------------------------------------------

    [Fact]
    public void Reference_scalar_chain_should_read_the_principal_and_a_dangling_key_as_absent()
    {
        using var ctx = CreateContext();

        var rows = ctx.From<D6Child>()
            .Select(c => new { c.Id, ParentName = c.Parent!.Name })
            .ToList();

        rows.Single(r => r.Id == 10).ParentName.Should().Be("p1");
        rows.Single(r => r.Id == 11).ParentName.Should().Be("p1");
        rows.Single(r => r.Id == 12).ParentName.Should().BeNull("the foreign key dangles and the principal is absent");
    }

    [Fact]
    public void Reference_presence_should_test_the_principal_and_ignore_the_raw_foreign_key()
    {
        using var ctx = CreateContext();

        var rows = ctx.From<D6Child>()
            .Select(c => new { c.Id, Present = c.Parent != null, Absent = c.Parent == null })
            .ToList();

        rows.Single(r => r.Id == 10).Present.Should().BeTrue();
        rows.Single(r => r.Id == 10).Absent.Should().BeFalse();
        rows.Single(r => r.Id == 12).Present.Should().BeFalse();
        rows.Single(r => r.Id == 12).Absent.Should().BeTrue();
    }

    [Fact]
    public void Reference_presence_predicates_should_filter_in_where()
    {
        using var ctx = CreateContext();

        ctx.From<D6Child>().Where(c => c.Parent != null).Select(c => c.Id).ToList()
            .Should().BeEquivalentTo(new[] { 10, 11 });
        ctx.From<D6Child>().Where(c => c.Parent == null).Select(c => c.Id).ToList()
            .Should().BeEquivalentTo(new[] { 12 });
    }

    [Fact]
    public void Reference_scalar_predicate_should_filter_and_skip_the_dangling_row()
    {
        using var ctx = CreateContext();

        var ids = ctx.From<D6Child>().Where(c => c.Parent!.Name == "p1").Select(c => c.Id).ToList();

        ids.Should().BeEquivalentTo(new[] { 10, 11 });
    }

    [Fact]
    public void Unlifted_non_nullable_value_scalar_in_a_predicate_should_be_null_safe()
    {
        using var ctx = CreateContext();

        // The value is read as nullable, so the dangling row's absent principal makes the predicate
        // false instead of throwing.
        var ids = ctx.From<D6Child>().Where(c => c.Parent!.Age > 5).Select(c => c.Id).ToList();

        ids.Should().BeEquivalentTo(new[] { 10, 11 });
    }

    [Fact]
    public void Nullable_lift_and_coalesce_should_materialize_present_and_absent()
    {
        using var ctx = CreateContext();

        var rows = ctx.From<D6Child>()
            .Select(c => new { c.Id, Lifted = (int?)c.Parent!.Age, Coalesced = (int?)c.Parent!.Age ?? -1 })
            .ToList();

        var present = rows.Single(r => r.Id == 10);
        present.Lifted.Should().Be(42);
        present.Coalesced.Should().Be(42);

        var dangling = rows.Single(r => r.Id == 12);
        dangling.Lifted.Should().BeNull();
        dangling.Coalesced.Should().Be(-1);
    }

    [Fact]
    public void Unlifted_non_nullable_scalar_projection_on_absent_principal_should_throw_with_path_and_type()
    {
        using var ctx = CreateContext();

        Action act = () => ctx.From<D6Child>()
            .Select(c => new { c.Id, Age = c.Parent!.Age })
            .ToList();

        // Same explicit diagnostic (navigation path + result type) as the SQL path A7.
        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*D6Child.Parent.Age*System.Int32*");
    }

    [Fact]
    public void Single_column_reference_scalar_projection_should_use_the_same_lowering()
    {
        using var ctx = CreateContext();

        var names = ctx.From<D6Child>().OrderBy(c => c.Id).Select(c => c.Parent!.Name).ToList();

        names.Should().Equal("p1", "p1", null);
    }

    [Fact]
    public void Single_column_collection_count_projection_should_use_the_correlated_count()
    {
        using var ctx = CreateContext();

        var counts = ctx.From<D6Parent>().OrderBy(p => p.Id).Select(p => p.Children.Count()).ToList();

        counts.Should().Equal(2, 0);
    }

    // #148-B R2.3 red->green: the whole reference now materializes the matched principal entity or
    // null through the metadata binding (the previous fail-closed shape is superseded).
    [Fact]
    public void Whole_reference_projection_should_return_the_entity_or_null()
    {
        using var ctx = CreateContext();

        var rows = ctx.From<D6Child>()
            .Select(c => new { c.Id, Parent = c.Parent })
            .ToList();

        var present = rows.Single(r => r.Id == 10);
        present.Parent.Should().NotBeNull();
        present.Parent!.Id.Should().Be(1);
        present.Parent.Name.Should().Be("p1");

        rows.Single(r => r.Id == 12).Parent.Should().BeNull("a dangling foreign key materializes the absent reference as null");
    }

    // ---- Collection terminals (D4 parity) --------------------------------------------------------

    [Fact]
    public void One_to_many_terminals_should_report_empty_and_non_empty_cardinality()
    {
        using var ctx = CreateContext();

        var rows = ctx.From<D6Parent>()
            .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
            .ToList();

        var one = rows.Single(r => r.Id == 1);
        one.Has.Should().BeTrue();
        one.C.Should().Be(2);
        one.L.Should().Be(2L);
        one.P.Should().Be(2);

        var empty = rows.Single(r => r.Id == 2);
        empty.Has.Should().BeFalse();
        empty.C.Should().Be(0);
        empty.L.Should().Be(0L);
        empty.P.Should().Be(0);
    }

    [Fact]
    public void Many_to_many_terminals_should_preserve_duplicate_junction_rows_and_empty_parent()
    {
        using var ctx = CreateManyToMany();

        var rows = ctx.From<D6MParent>()
            .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
            .ToList();

        var one = rows.Single(r => r.Id == 1);
        one.Has.Should().BeTrue();
        one.C.Should().Be(2, "duplicate junction rows must not be deduplicated");
        one.L.Should().Be(2L);
        one.P.Should().Be(2);

        var empty = rows.Single(r => r.Id == 2);
        empty.Has.Should().BeFalse();
        empty.C.Should().Be(0);
        empty.L.Should().Be(0L);
        empty.P.Should().Be(0);
    }

    // #148-B R2.1 red->green: the in-memory direct many-to-many terminal counts the junction rows whose
    // mapped child exists. The fixture is 3 valid + 1 dangling -> 3, NOT 4.
    [Fact]
    public void Many_to_many_terminals_should_exclude_dangling_junction_rows_without_deduplicating()
    {
        using var ctx = CreateManyToManyWithDangling();

        var rows = ctx.From<D6MParent>()
            .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
            .ToList();

        var mixed = rows.Single(r => r.Id == 1);
        mixed.Has.Should().BeTrue();
        mixed.C.Should().Be(3, "3 junction rows have a mapped child; the dangling link is excluded");
        mixed.L.Should().Be(3L);
        mixed.P.Should().Be(3);

        var danglingOnly = rows.Single(r => r.Id == 2);
        danglingOnly.Has.Should().BeFalse("a junction row whose mapped child is absent yields no element");
        danglingOnly.C.Should().Be(0);
        danglingOnly.L.Should().Be(0L);
        danglingOnly.P.Should().Be(0);

        var empty = rows.Single(r => r.Id == 3);
        empty.Has.Should().BeFalse();
        empty.C.Should().Be(0);
    }

    [Fact]
    public void Adapter_terminal_should_match_the_direct_navigation_terminal()
    {
        using var ctx = CreateContext();

        var direct = ctx.From<D6Parent>()
            .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount() })
            .ToList();

        var adapted = ctx.From<D6Parent>()
            .Select(p => new
            {
                p.Id,
                Has = p.Children.AsEntityBuilder<D6Child>().Any(),
                C = p.Children.AsEntityBuilder<D6Child>().Count(),
                L = p.Children.AsEntityBuilder<D6Child>().LongCount(),
            })
            .ToList();

        adapted.Should().BeEquivalentTo(direct);
    }

    [Fact]
    public void LongCount_terminal_should_be_a_true_64_bit_count()
    {
        using var ctx = CreateContext();

        var l = ctx.From<D6Parent>().Where(p => p.Id == 1).Select(p => p.Children.LongCount()).ToList();

        l.Should().ContainSingle();
        l[0].Should().Be(2L);
    }

    [Fact]
    public void Direct_collection_composition_and_extra_terminals_should_fail_closed()
    {
        using var ctx = CreateContext();

        Action where = () => ctx.From<D6Parent>()
            .Select(p => new { p.Id, X = p.Children.Where(c => c.Id > 0) })
            .ToList();
        Action first = () => ctx.From<D6Parent>()
            .Select(p => new { p.Id, X = p.Children.First().Id })
            .ToList();

        where.Should().Throw<NotSupportedException>();
        first.Should().Throw<NotSupportedException>();
    }
}

[SqlTable("d6_parent")]
public sealed class D6Parent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("age")]
    public int Age { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    public ICollection<D6Child> Children { get; set; } = new List<D6Child>();
}

[SqlTable("d6_child")]
public sealed class D6Child
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    public D6Parent? Parent { get; set; }
}

[SqlTable("d6m_parent")]
public sealed class D6MParent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    public ICollection<D6MChild> Children { get; set; } = new List<D6MChild>();
}

[SqlTable("d6m_child")]
public sealed class D6MChild
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }
}

[SqlTable("d6m_link")]
public sealed class D6MLink
{
    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("child_id")]
    public int ChildId { get; set; }
}

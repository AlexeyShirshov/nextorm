using FluentAssertions;

namespace NextORM.Core.Tests;

public class CteQueryTests
{
    [Fact]
    public void WithRecursive_ShouldAppendRecursiveDefinitionAndKeepDeclarations()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();
        var query = e.Where(x => x.Id > 0).Select(x => new { x.Id });

        var cte = ctx.With("recent", query);
        cte.Ctes.Should().ContainSingle().Which.Name.Should().Be("recent");
        cte.Ctes[0].Recursive.Should().BeFalse();

        var recursive = cte.WithRecursive("nums", query, 50);
        recursive.Ctes.Should().HaveCount(2);
        recursive.Ctes[1].Name.Should().Be("nums");
        recursive.Ctes[1].Recursive.Should().BeTrue();
        recursive.Ctes[1].MaxRecursion.Should().Be(50);
    }

    [Fact]
    public void Join_WithSameCteNameDeclaredOnBothSides_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();
        var left = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));
        var right = ctx.With("c", e.Where(x => x.Id > 1).Select(x => new { x.Id }));

        var act = () => left.From("c").Join(right.From("c"), (a, b) => a.GetInt64("id") == b.GetInt64("id"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*'c'*both sides of the join*");
    }

    [Fact]
    public void Hoist_AlreadyFlat_ShouldReturnSameListInstance()
    {
        using var ctx = new InMemoryDataContext();
        var query = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var list = new[] { new CteDefinition("a", query) };

        CteHoister.Hoist(list).Should().BeSameAs(list);
    }

    [Fact]
    public void Hoist_RepeatedReference_ShouldKeepDeclarationOnce()
    {
        using var ctx = new InMemoryDataContext();
        var query = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var definition = new CteDefinition("a", query);
        var list = new[] { definition, definition };

        var hoisted = CteHoister.Hoist(list);

        hoisted.Should().ContainSingle().Which.Should().BeSameAs(definition);
    }

    [Fact]
    public void Hoist_DifferentDefinitionsUnderSameName_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();
        var list = new[]
        {
            new CteDefinition("a", e.Where(x => x.Id > 0).Select(x => new { x.Id })),
            new CteDefinition("a", e.Where(x => x.Id > 1).Select(x => new { x.Id })),
        };

        var act = () => CteHoister.Hoist(list);

        act.Should().Throw<InvalidOperationException>().WithMessage("*'a'*");
    }

    [Fact]
    public void Hoist_NestedDefinition_ShouldListDependencyBeforeConsumer()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var inner = ctx.With("i", e.Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });

        var hoisted = CteHoister.Hoist(new[] { new CteDefinition("o", inner) });

        hoisted!.Select(c => c.Name).Should().Equal("i", "o");
    }

    [Fact]
    public void Hoist_SingleDeclarationWithNestedBody_ShouldFlattenInsteadOfTakingFastPath()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        // Issue #200 L1: a lone declaration whose body carries a nested declaration must not take the
        // single-item fast path, or the nested CTE would survive inside its consumer's WITH.
        var inner = ctx.With("i", e.Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });
        var list = new[] { new CteDefinition("o", inner) };

        var hoisted = CteHoister.Hoist(list);

        ReferenceEquals(hoisted, list).Should().BeFalse();
        hoisted!.Select(c => c.Name).Should().Equal("i", "o");
    }

    [Fact]
    public void Hoist_SiblingReferencedBeforeItsDeclaration_ShouldListDependencyFirst()
    {
        using var ctx = new InMemoryDataContext();

        // The fluent API builds a body before the CTE it reads is declared, so "b" can reference "a"
        // while "a" is appended to the declaration list only afterwards.
        var a = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var b = ctx.From("a").Select(t => new { id = t["id"].AsInt });

        var hoisted = CteHoister.Hoist(
        [
            new CteDefinition("b", b),
            new CteDefinition("a", a),
        ]);

        hoisted!.Select(c => c.Name).Should().Equal("a", "b");
    }

    [Fact]
    public void Hoist_DeclarationCycle_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var qa = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var qb = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var a = new CteDefinition("a", qa);
        var b = new CteDefinition("b", qb);

        // `a`'s body declares `b`, whose body declares `a` again: the declaration tree never terminates,
        // so the hoist must fail instead of recursing forever.
        qa.Ctes = [b];
        qb.Ctes = [a];

        var act = () => CteHoister.Hoist([a]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Hoist_NullOrEmpty_ShouldReturnInput()
    {
        // No declarations is not an error: a null list stays null and an empty list stays empty.
        CteHoister.Hoist(null).Should().BeNull();
        CteHoister.Hoist(System.Array.Empty<CteDefinition>()).Should().BeEmpty();
    }

    [Fact]
    public void EnsureNoUnhoistedCtes_NestedDeclarationOutsideHoistedSet_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        // `inner` carries its own declaration `i`; used as a derived-table source it is a nested
        // declaration the top-level WITH cannot reach, so the guard rejects it before rendering.
        var inner = ctx.With("i", ctx.From<SimpleEntity>().Select(x => new { x.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });
        var root = ctx.From(inner).Select(t => new { t.id });

        var act = () => CteHoister.EnsureNoUnhoistedCtes(root, System.Array.Empty<CteDefinition>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be hoisted*");
    }
}

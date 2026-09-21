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
}

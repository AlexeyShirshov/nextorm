using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// T296b (#296, D4b) in-memory parity: the approved total comparison must follow plain C# null
/// semantics — <c>int? == int?</c> is <see langword="true"/> only when both sides are equal (including
/// both <see langword="null"/>), <c>!=</c> is its complement, and <c>HasValue</c> is a presence test.
/// The in-memory provider compiles the predicate, so these facts pin the CLR behaviour the SQL
/// translation mirrors; no in-memory production code is involved.
/// </summary>
public class T296bNullableComparisonTests
{
    private readonly InMemoryRepository _sut;

    public T296bNullableComparisonTests(InMemoryRepository sut)
    {
        _sut = sut;
        Rows().WithData(
        new[]
        {
            new T296bNullableEntity { Id = 1, N = null, S = null },
            new T296bNullableEntity { Id = 2, N = 42, S = "alpha" },
            new T296bNullableEntity { Id = 3, N = 7, S = "beta" },
            new T296bNullableEntity { Id = 4, N = 42, S = "alpha" },
            new T296bNullableEntity { Id = 5, N = null, S = "gamma" },
        });
    }

    private EntityBuilder<T296bNullableEntity> Rows() => _sut.DataProvider.From<T296bNullableEntity>();

    private int[] Ids(Func<EntityBuilder<T296bNullableEntity>, QueryCommand<int>> filter)
        => filter(Rows()).ToList().OrderBy(x => x).ToArray();

    [Fact]
    public void CapturedNullableIntEquality_ShouldFollowCSharpNullEquality()
    {
        int? captured = null;
        Ids(r => r.Where(x => x.N == captured).Select(x => x.Id)).Should().Equal(1, 5);
        Ids(r => r.Where(x => captured == x.N).Select(x => x.Id)).Should().Equal(1, 5);
        Ids(r => r.Where(x => x.N != captured).Select(x => x.Id)).Should().Equal(2, 3, 4);
        Ids(r => r.Where(x => captured != x.N).Select(x => x.Id)).Should().Equal(2, 3, 4);

        captured = 42;
        Ids(r => r.Where(x => x.N == captured).Select(x => x.Id)).Should().Equal(2, 4);
        Ids(r => r.Where(x => captured == x.N).Select(x => x.Id)).Should().Equal(2, 4);
        Ids(r => r.Where(x => x.N != captured).Select(x => x.Id)).Should().Equal(1, 3, 5);
        Ids(r => r.Where(x => captured != x.N).Select(x => x.Id)).Should().Equal(1, 3, 5);

        captured = 7;
        Ids(r => r.Where(x => x.N == captured).Select(x => x.Id)).Should().Equal(3);
        Ids(r => r.Where(x => x.N != captured).Select(x => x.Id)).Should().Equal(1, 2, 4, 5);
    }

    [Fact]
    public void CapturedNullableStringEquality_ShouldFollowCSharpNullEquality()
    {
        string? captured = null;
        Ids(r => r.Where(x => x.S == captured).Select(x => x.Id)).Should().Equal(1);
        Ids(r => r.Where(x => captured == x.S).Select(x => x.Id)).Should().Equal(1);
        Ids(r => r.Where(x => x.S != captured).Select(x => x.Id)).Should().Equal(2, 3, 4, 5);
        Ids(r => r.Where(x => captured != x.S).Select(x => x.Id)).Should().Equal(2, 3, 4, 5);

        captured = "alpha";
        Ids(r => r.Where(x => x.S == captured).Select(x => x.Id)).Should().Equal(2, 4);
        Ids(r => r.Where(x => captured == x.S).Select(x => x.Id)).Should().Equal(2, 4);
        Ids(r => r.Where(x => x.S != captured).Select(x => x.Id)).Should().Equal(1, 3, 5);
        Ids(r => r.Where(x => captured != x.S).Select(x => x.Id)).Should().Equal(1, 3, 5);

        // The literal null forms stay the plain presence test.
        Ids(r => r.Where(x => x.S == null).Select(x => x.Id)).Should().Equal(1);
        Ids(r => r.Where(x => x.S != null).Select(x => x.Id)).Should().Equal(2, 3, 4, 5);
    }

    [Fact]
    public void HasValueAndNegation_ShouldBePresenceTests()
    {
        Ids(r => r.Where(x => x.N.HasValue).Select(x => x.Id)).Should().Equal(2, 3, 4);
        Ids(r => r.Where(x => !x.N.HasValue).Select(x => x.Id)).Should().Equal(1, 5);
        Ids(r => r.Where(x => x.S != null && x.N.HasValue).Select(x => x.Id)).Should().Equal(2, 3, 4);
        Ids(r => r.Where(x => x.N.HasValue && x.S == null).Select(x => x.Id)).Should().BeEmpty();
    }

    [Fact]
    public void HasValueCompositions_ShouldKeepTheirConjunctionAndDisjunctionSemantics()
    {
        Ids(r => r.Where(x => x.N.HasValue && x.N > 15).Select(x => x.Id)).Should().Equal(2, 4);
        Ids(r => r.Where(x => !x.N.HasValue || x.N > 15).Select(x => x.Id)).Should().Equal(1, 2, 4, 5);
        Ids(r => r.Where(x => x.N.HasValue && x.S != null).Select(x => x.Id)).Should().Equal(2, 3, 4);
        Ids(r => r.Where(x => !x.N.HasValue && x.S != null).Select(x => x.Id)).Should().Equal(5);
        Ids(r => r.Where(x => x.N.HasValue || x.S == "gamma").Select(x => x.Id)).Should().Equal(2, 3, 4, 5);
        Ids(r => r.Where(x => !x.N.HasValue && x.S == "gamma").Select(x => x.Id)).Should().Equal(5);

        int? captured = 42;
        Ids(r => r.Where(x => x.N == captured && x.S == "alpha").Select(x => x.Id)).Should().Equal(2, 4);
        Ids(r => r.Where(x => x.N == captured || x.S == "beta").Select(x => x.Id)).Should().Equal(2, 3, 4);
        captured = null;
        Ids(r => r.Where(x => x.N == captured || x.S == "beta").Select(x => x.Id)).Should().Equal(1, 3, 5);
    }

    [Fact]
    public void BooleanProjection_ShouldMaterialiseTheSameCSharpResult()
    {
        int? captured = null;
        var rows = Rows().OrderBy(x => x.Id).Select(x => new { x.Id, Match = x.N == captured }).ToList();
        rows.Select(x => x.Id).Should().Equal(1, 2, 3, 4, 5);
        rows.Select(x => x.Match).Should().Equal(true, false, false, false, true);

        captured = 42;
        rows = Rows().OrderBy(x => x.Id).Select(x => new { x.Id, Match = x.N == captured }).ToList();
        rows.Select(x => x.Match).Should().Equal(false, true, false, true, false);

        var presence = Rows().OrderBy(x => x.Id).Select(x => new { x.Id, Has = x.N.HasValue }).ToList();
        presence.Select(x => x.Has).Should().Equal(false, true, true, true, false);
    }

    [Fact]
    public void SameQueryRepeatedAfterTheCapturedValueChanges_ShouldReturnFreshRows()
    {
        int? captured = null;
        var query = Rows().Where(x => x.N == captured).OrderBy(x => x.Id).Select(x => x.Id);

        query.ToList().Should().Equal(1, 5);

        captured = 42;
        query.ToList().Should().Equal(2, 4);

        captured = null;
        query.ToList().Should().Equal(1, 5);

        string? capturedText = null;
        var textQuery = Rows().Where(x => x.S == capturedText).OrderBy(x => x.Id).Select(x => x.Id);
        textQuery.ToList().Should().Equal(1);
        capturedText = "alpha";
        textQuery.ToList().Should().Equal(2, 4);
        capturedText = null;
        textQuery.ToList().Should().Equal(1);
    }
}

/// <summary>In-memory parity fixture: nullable integer and string columns with null, duplicate and unequal values.</summary>
public class T296bNullableEntity
{
    public int Id { get; set; }

    public int? N { get; set; }

    public string? S { get; set; }
}

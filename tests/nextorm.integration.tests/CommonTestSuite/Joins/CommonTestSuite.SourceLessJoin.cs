using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D384 (#384) U7: live execution parity of the source-less join overloads against their explicit
/// <c>ctx.From(...)</c> twins on real databases. Every scenario runs both calls against the same seeded
/// tables and compares the materialised rows (or, where a capability is absent, asserts the documented
/// capability marker on both forms). Nothing is skipped silently — an unsupported operator/provider
/// pair is documented by the <see cref="ITestProvider.SupportsFullJoin"/> /
/// <see cref="ITestProvider.SupportsApply"/> guards and exercised as an equivalent rejection.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void SourceLessJoin_StringJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitRows = simple
            .Join(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = p.Item2["id"].AsInt })
            .ToList();

        var sourceLessRows = simple
            .Join("complex_entity", (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = p.Item2["id"].AsInt })
            .ToList();

        sourceLessRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().NotBeEmpty();
    }

    [Fact]
    public void SourceLessJoin_StringLeftJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitRows = simple
            .LeftJoin(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = (int?)p.Item2["id"].AsInt })
            .ToList();

        var sourceLessRows = simple
            .LeftJoin("complex_entity", (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = (int?)p.Item2["id"].AsInt })
            .ToList();

        sourceLessRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().HaveCount(10);
        explicitRows.Count(r => r.RightId is null).Should().Be(7);
    }

    [Fact]
    public void SourceLessJoin_StringRightJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitRows = complex
            .RightJoin(simple, (c, s) => c["id"].AsInt == s["id"].AsInt)
            .Select(p => new { LeftId = (int?)p.Item1["id"].AsInt, RightId = p.Item2["id"].AsInt })
            .ToList();

        var sourceLessRows = complex
            .RightJoin("simple_entity", (c, s) => c["id"].AsInt == s["id"].AsInt)
            .Select(p => new { LeftId = (int?)p.Item1["id"].AsInt, RightId = p.Item2["id"].AsInt })
            .ToList();

        sourceLessRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().HaveCount(10);
    }

    [Fact]
    public void SourceLessJoin_StringFullJoin_EquivalentToExplicitOrEquivalentlyRejected()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        Action explicitAct = () => simple.FullJoin(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt }).ToList();
        Action sourceLessAct = () => simple.FullJoin("complex_entity", (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt }).ToList();

        if (!Provider.SupportsFullJoin)
        {
            // Declared capability marker for this provider (never a comparison against the explicit
            // call's message): FULL JOIN is refused by the dialect with this documented reason.
            const string marker = "The Full join is not supported by this SQL dialect";
            FluentActions.Invoking(() => explicitAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the explicit form must carry the documented capability marker");
            FluentActions.Invoking(() => sourceLessAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the source-less form must carry the same documented capability marker");
            return;
        }

        var explicitRows = simple.FullJoin(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt }).ToList();
        var sourceLessRows = simple.FullJoin("complex_entity", (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt }).ToList();

        sourceLessRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().HaveCount(10);
    }

    [Fact]
    public void SourceLessJoin_StringCrossJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitCount = simple.CrossJoin(complex).Count();
        var sourceLessCount = simple.CrossJoin("complex_entity").Count();

        sourceLessCount.Should().Be(explicitCount);
        explicitCount.Should().Be(30);
    }

    [Fact]
    public void SourceLessJoin_StringCrossApply_EquivalentToExplicitOrEquivalentlyRejected()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        Action explicitAct = () => simple.CrossApply(complex).Count();
        Action sourceLessAct = () => simple.CrossApply("complex_entity").Count();

        if (!Provider.SupportsApply)
        {
            // Declared capability marker for this provider: no lateral/APPLY source.
            const string marker = "The CrossApply join is not supported by this SQL dialect";
            FluentActions.Invoking(() => explicitAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the explicit form must carry the documented capability marker");
            FluentActions.Invoking(() => sourceLessAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the source-less form must carry the same documented capability marker");
            return;
        }

        var explicitCount = simple.CrossApply(complex).Count();
        var sourceLessCount = simple.CrossApply("complex_entity").Count();

        sourceLessCount.Should().Be(explicitCount);
    }

    [Fact]
    public void SourceLessJoin_StringOuterApply_EquivalentToExplicitOrEquivalentlyRejected()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        Action explicitAct = () => simple.OuterApply(complex).Count();
        Action sourceLessAct = () => simple.OuterApply("complex_entity").Count();

        if (!Provider.SupportsApply)
        {
            // Declared capability marker for this provider: no lateral/APPLY source.
            const string marker = "The OuterApply join is not supported by this SQL dialect";
            FluentActions.Invoking(() => explicitAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the explicit form must carry the documented capability marker");
            FluentActions.Invoking(() => sourceLessAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the source-less form must carry the same documented capability marker");
            return;
        }

        var explicitCount = simple.OuterApply(complex).Count();
        var sourceLessCount = simple.OuterApply("complex_entity").Count();

        sourceLessCount.Should().Be(explicitCount);
    }

    [Fact]
    public void SourceLessJoin_TypeOnlyCrossJoin_EquivalentToExplicit()
    {
        var explicitCount = _sut.SimpleEntity.CrossJoin(_sut.ComplexEntity).Count();
        var typeOnlyCount = _sut.SimpleEntity.CrossJoin<IComplexEntity>().Count();

        typeOnlyCount.Should().Be(explicitCount);
        explicitCount.Should().Be(30);
    }

    [Fact]
    public void SourceLessJoin_TypeOnlyCrossApply_EquivalentToExplicitOrEquivalentlyRejected()
    {
        if (!Provider.SupportsApply)
        {
            Action explicitAct = () => _sut.SimpleEntity.CrossApply(_sut.ComplexEntity).Count();
            Action typeOnlyAct = () => _sut.SimpleEntity.CrossApply<IComplexEntity>().Count();

            // Declared capability marker for this provider: no lateral/APPLY source.
            const string marker = "The CrossApply join is not supported by this SQL dialect";
            FluentActions.Invoking(() => explicitAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the explicit form must carry the documented capability marker");
            FluentActions.Invoking(() => typeOnlyAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the source-less form must carry the same documented capability marker");
            return;
        }

        var explicitCount = _sut.SimpleEntity.CrossApply(_sut.ComplexEntity).Count();
        var typeOnlyCount = _sut.SimpleEntity.CrossApply<IComplexEntity>().Count();

        typeOnlyCount.Should().Be(explicitCount);
    }

    [Fact]
    public void SourceLessJoin_TypeOnlyOuterApply_EquivalentToExplicitOrEquivalentlyRejected()
    {
        if (!Provider.SupportsApply)
        {
            Action explicitAct = () => _sut.SimpleEntity.OuterApply(_sut.ComplexEntity).Count();
            Action typeOnlyAct = () => _sut.SimpleEntity.OuterApply<IComplexEntity>().Count();

            // Declared capability marker for this provider: no lateral/APPLY source.
            const string marker = "The OuterApply join is not supported by this SQL dialect";
            FluentActions.Invoking(() => explicitAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the explicit form must carry the documented capability marker");
            FluentActions.Invoking(() => typeOnlyAct()).Should().Throw<NotSupportedException>()
                .Which.Message.Should().Be(marker, "the source-less form must carry the same documented capability marker");
            return;
        }

        var explicitCount = _sut.SimpleEntity.OuterApply(_sut.ComplexEntity).Count();
        var typeOnlyCount = _sut.SimpleEntity.OuterApply<IComplexEntity>().Count();

        typeOnlyCount.Should().Be(explicitCount);
    }
}

/// <summary>
/// ClickHouse adapter: ClickHouse does not derive <see cref="CommonTestSuite"/>, so the live parity
/// of the source-less join forms is re-pinned here against a real server, mirroring the shared suite
/// above. CrossApply/OuterApply are unsupported on ClickHouse (no lateral source) and are asserted as
/// equivalent rejections.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseSourceLessJoinIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void SourceLessJoin_StringJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitRows = simple
            .Join(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = p.Item2.GetInt64("id") })
            .ToList();

        var sourceLessRows = simple
            .Join("complex_entity", (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = p.Item2.GetInt64("id") })
            .ToList();

        sourceLessRows.Should().BeEquivalentTo(explicitRows);
        explicitRows.Should().NotBeEmpty();
    }

    [Fact]
    public void SourceLessJoin_StringLeftJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitRows = simple
            .LeftJoin(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = (long?)p.Item2.GetInt64("id") })
            .ToList();

        var sourceLessRows = simple
            .LeftJoin("complex_entity", (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt, RightId = (long?)p.Item2.GetInt64("id") })
            .ToList();

        sourceLessRows.Should().BeEquivalentTo(explicitRows);
    }

    [Fact]
    public void SourceLessJoin_StringCrossJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitCount = simple.CrossJoin(complex).Count();
        var sourceLessCount = simple.CrossJoin("complex_entity").Count();

        sourceLessCount.Should().Be(explicitCount);
        explicitCount.Should().Be(30);
    }

    [Fact]
    public void SourceLessJoin_TypeOnlyCrossJoin_EquivalentToExplicit()
    {
        var explicitCount = _sut.SimpleEntity.CrossJoin(_sut.ComplexEntity).Count();
        var typeOnlyCount = _sut.SimpleEntity.CrossJoin<IComplexEntity>().Count();

        typeOnlyCount.Should().Be(explicitCount);
        explicitCount.Should().Be(30);
    }

    [Fact]
    public void SourceLessJoin_StringFullJoin_EquivalentToExplicit()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var explicitRows = simple.FullJoin(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt }).ToList();
        var sourceLessRows = simple.FullJoin("complex_entity", (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { LeftId = p.Item1["id"].AsInt }).ToList();

        sourceLessRows.Should().BeEquivalentTo(explicitRows);
    }

    [Fact]
    public void SourceLessJoin_StringCrossApply_EquivalentlyRejected()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        Action explicitAct = () => simple.CrossApply(complex).Count();
        Action sourceLessAct = () => simple.CrossApply("complex_entity").Count();

        // ClickHouse has no lateral source: the dialect refuses with this documented marker, asserted
        // identically on both forms (never compared against the explicit call's message).
        const string marker = "The CrossApply join is not supported by this SQL dialect";
        FluentActions.Invoking(() => explicitAct()).Should().Throw<NotSupportedException>()
            .Which.Message.Should().Be(marker, "the explicit form must carry the documented capability marker");
        FluentActions.Invoking(() => sourceLessAct()).Should().Throw<NotSupportedException>()
            .Which.Message.Should().Be(marker, "the source-less form must carry the same documented capability marker");
    }

    [Fact]
    public void SourceLessJoin_StringOuterApply_EquivalentlyRejected()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        Action explicitAct = () => simple.OuterApply(complex).Count();
        Action sourceLessAct = () => simple.OuterApply("complex_entity").Count();

        // ClickHouse has no lateral source: the dialect refuses with this documented marker, asserted
        // identically on both forms (never compared against the explicit call's message).
        const string marker = "The OuterApply join is not supported by this SQL dialect";
        FluentActions.Invoking(() => explicitAct()).Should().Throw<NotSupportedException>()
            .Which.Message.Should().Be(marker, "the explicit form must carry the documented capability marker");
        FluentActions.Invoking(() => sourceLessAct()).Should().Throw<NotSupportedException>()
            .Which.Message.Should().Be(marker, "the source-less form must carry the same documented capability marker");
    }
}
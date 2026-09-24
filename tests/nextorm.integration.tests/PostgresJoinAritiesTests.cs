using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Exercises the positional-join arities (2..8) and their multi-table terminal helpers
/// (<see cref="DataContextExtensions.ToSql{T1, T2}(JoinedEntityBuilder{T1, T2})"/>,
/// <c>Delete</c>/<c>DeleteAsync</c>, <c>UpdateJoin</c>) against PostgreSQL.
/// </summary>
public sealed class PostgresJoinAritiesTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void JoinArities_ToSqlAndUpdateJoin_ShouldRender()
    {
        var b2 = _sut.SimpleEntity.Join(_sut.ComplexEntity, (a, b) => a.Id == b.Id);
        b2.ToSql().Should().NotBeNullOrEmpty();
        b2.UpdateJoin().Set(p => p.Item1.Id, 1).ToSql().Should().ContainEquivalentOf("update");

        var b3 = b2.Join(_sut.SimpleEntity, (p, c) => p.Item2.Id == c.Id);
        b3.ToSql().Should().NotBeNullOrEmpty();
        b3.UpdateJoin().Set(p => p.Item1.Id, 1).ToSql().Should().ContainEquivalentOf("update");

        var b4 = b3.Join(_sut.ComplexEntity, (p, c) => p.Item3.Id == c.Id);
        b4.ToSql().Should().NotBeNullOrEmpty();
        b4.UpdateJoin().Set(p => p.Item1.Id, 1).ToSql().Should().ContainEquivalentOf("update");

        var b5 = b4.Join(_sut.SimpleEntity, (p, c) => p.Item4.Id == c.Id);
        b5.ToSql().Should().NotBeNullOrEmpty();
        b5.UpdateJoin().Set(p => p.Item1.Id, 1).ToSql().Should().ContainEquivalentOf("update");

        var b6 = b5.Join(_sut.ComplexEntity, (p, c) => p.Item5.Id == c.Id);
        b6.ToSql().Should().NotBeNullOrEmpty();
        b6.UpdateJoin().Set(p => p.Item1.Id, 1).ToSql().Should().ContainEquivalentOf("update");

        var b7 = b6.Join(_sut.SimpleEntity, (p, c) => p.Item6.Id == c.Id);
        b7.ToSql().Should().NotBeNullOrEmpty();
        b7.UpdateJoin().Set(p => p.Item1.Id, 1).ToSql().Should().ContainEquivalentOf("update");

        var b8 = b7.Join(_sut.ComplexEntity, (p, c) => p.Item7.Id == c.Id);
        b8.ToSql().Should().NotBeNullOrEmpty();
        b8.UpdateJoin().Set(p => p.Item1.Id, 1).ToSql().Should().ContainEquivalentOf("update");
    }

    [Fact]
    public async Task JoinArities_Delete_ShouldAffectNothing()
    {
        var ct = TestContext.Current.CancellationToken;

        var b2 = _sut.SimpleEntity.Join(_sut.ComplexEntity, (a, b) => a.Id == b.Id);
        (await b2.Where(p => p.Item1.Id < 0).DeleteAsync(ct)).Should().Be(0);

        var b3 = b2.Join(_sut.SimpleEntity, (p, c) => p.Item2.Id == c.Id);
        (await b3.Where(p => p.Item1.Id < 0).DeleteAsync(ct)).Should().Be(0);

        var b4 = b3.Join(_sut.ComplexEntity, (p, c) => p.Item3.Id == c.Id);
        (await b4.Where(p => p.Item1.Id < 0).DeleteAsync(ct)).Should().Be(0);

        var b5 = b4.Join(_sut.SimpleEntity, (p, c) => p.Item4.Id == c.Id);
        (await b5.Where(p => p.Item1.Id < 0).DeleteAsync(ct)).Should().Be(0);

        var b6 = b5.Join(_sut.ComplexEntity, (p, c) => p.Item5.Id == c.Id);
        (await b6.Where(p => p.Item1.Id < 0).DeleteAsync(ct)).Should().Be(0);

        var b7 = b6.Join(_sut.SimpleEntity, (p, c) => p.Item6.Id == c.Id);
        (await b7.Where(p => p.Item1.Id < 0).DeleteAsync(ct)).Should().Be(0);

        var b8 = b7.Join(_sut.ComplexEntity, (p, c) => p.Item7.Id == c.Id);
        (await b8.Where(p => p.Item1.Id < 0).DeleteAsync(ct)).Should().Be(0);
    }
}

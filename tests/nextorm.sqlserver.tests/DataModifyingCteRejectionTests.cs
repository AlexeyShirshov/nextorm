using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Every data-modifying common table expression body is PostgreSQL-only. Constructing the CTE scope
/// through the public <c>With(name, returningBuilder)</c> surface must reject the provider for each body
/// kind before any SQL is rendered. No database is involved: the guard runs while the scope is built.
/// </summary>
public class DataModifyingCteRejectionTests
{
    [Fact]
    public void DataModifyingCte_UpdateReturningBody_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.With("upd", ctx.Update<IComplexEntity>()
            .Set(x => x.String, "a")
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }

    [Fact]
    public void DataModifyingCte_DeleteReturningBody_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.With("del", ctx.DeleteFrom<IComplexEntity>()
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }

    [Fact]
    public void DataModifyingCte_UpdateJoinReturningBody_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.With("upd", ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .UpdateJoin()
            .Set(p => p.Item1.String, "a")
            .Returning(p => new { p.Item1.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }

    [Fact]
    public void DataModifyingCte_DeleteJoinReturningBody_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.With("del", ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Returning(p => new { p.Item1.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }
}

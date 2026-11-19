using FluentAssertions;
using NextORM.Core;
using NextORM.Core.Repositories;

namespace NextORM.Core.Tests;

public sealed class RepositoryFacadeTests
{
    [Fact]
    public void FacadeReusesRepositoryButNotQueryOrMutationBuilders()
    {
        using var provider = new InMemoryDataContext();
        var facade = new ApplicationContext(provider);

        facade.DataContext.Should().BeSameAs(provider);
        facade.Rows.DataContext.Should().BeSameAs(provider);
        facade.Rows.Should().BeSameAs(facade.Rows);
        facade.Rows.Query().Should().NotBeSameAs(facade.Rows.Query());
        facade.Rows.MakeInsert().Should().NotBeSameAs(facade.Rows.MakeInsert());
        facade.Rows.MakeUpdate().Should().NotBeSameAs(facade.Rows.MakeUpdate());
        facade.Rows.MakeDelete().Should().NotBeSameAs(facade.Rows.MakeDelete());
    }

    [Fact]
    public void FromAndJoinRejectDifferentProviderInstances()
    {
        using var first = new InMemoryDataContext();
        using var second = new InMemoryDataContext();
        var facade = new ApplicationContext(first);
        var foreign = new ApplicationContext(second);

        var from = () => facade.From(foreign.Rows);
        var join = () => facade.Rows.Join(foreign.Rows, (left, right) => left.Id == right.Id);

        from.Should().Throw<ArgumentException>().WithMessage("*different data context*");
        join.Should().Throw<ArgumentException>().WithMessage("*different data context*");
    }

    [Fact]
    public void QueryDeleteRejectsMissingPredicateAndExplicitZeroAcrossClone()
    {
        using var provider = new InMemoryDataContext();
        var rows = new ApplicationContext(provider).Rows;

        var missing = () => rows.Limit(1).Delete();
        var zero = () => rows.Limit(0).Where(row => row.Id == 1).Clone().Delete();

        missing.Should().Throw<InvalidOperationException>().WithMessage("*explicit Where*");
        zero.Should().Throw<InvalidOperationException>().WithMessage("*explicit zero limit*");
    }

    private sealed class ApplicationContext(IDataContext provider) : DbContext(provider)
    {
        public IRepository<Row> Rows => Repository<Row>();
    }

    private sealed class Row
    {
        public int Id { get; set; }
    }
}

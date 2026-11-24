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
    public async Task SelectProjectsRowsDirectlyFromTheRepository()
    {
        using var provider = new InMemoryDataContext();
        provider.Data[typeof(Row)] = new[] { new Row { Id = 3 }, new Row { Id = 1 } };
        var rows = new ApplicationContext(provider).Rows;

        QueryCommand<int> ids = rows.Select(row => row.Id);
        var projected = await ids.ToListAsync(TestContext.Current.CancellationToken);

        projected.Should().BeEquivalentTo([1, 3]);
        rows.Select(row => row.Id).Limit(1).ToList().Should().ContainSingle();
    }

    [Fact]
    public void QueryStartersComposeWithoutChangingTheRepositoryRoot()
    {
        using var provider = new InMemoryDataContext();
        provider.Data[typeof(Row)] = new[] { new Row { Id = 3 }, new Row { Id = 2 }, new Row { Id = 1 } };
        var rows = new ApplicationContext(provider).Rows;

        var middle = rows.Page(limit: 1, offset: 1)
            .OrderBy(row => row.Id, OrderDirection.Desc)
            .Select(row => row.Id)
            .ToList();

        middle.Should().Equal(2);
        rows.Select(row => row.Id).ToList().Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task QueryTerminalsExecuteAgainstTheRepository()
    {
        using var provider = new InMemoryDataContext();
        provider.Data[typeof(Row)] = new[] { new Row { Id = 3 } };
        var rows = new ApplicationContext(provider).Rows;
        var cancellationToken = TestContext.Current.CancellationToken;

        rows.Any().Should().BeTrue();
        (await rows.SingleAsync(cancellationToken)).Id.Should().Be(3);
        (await rows.ToDictionaryAsync(row => row.Id, cancellationToken)).Should().ContainKey(3);
    }

    [Fact]
    public async Task AsyncStreamForwardsCancellationToTheProvider()
    {
        // Check forwarding on a fresh plan: the in-memory cache can reuse the first call's enumerator and token.
        using var provider = new InMemoryDataContext { QueryCacheEnabled = false };
        provider.Data[typeof(Row)] = new[] { new Row { Id = 3 } };
        var rows = new ApplicationContext(provider).Rows;
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var active = rows.ToAsyncEnumerable(cancellationToken).GetAsyncEnumerator(cancellationToken))
        {
            (await active.MoveNextAsync()).Should().BeTrue();
        }

        await using var canceled = rows.ToAsyncEnumerable(new CancellationToken(canceled: true))
            .GetAsyncEnumerator(cancellationToken);
        (await canceled.MoveNextAsync()).Should().BeFalse();
    }

    [Fact]
    public void RepositoryJoinsUseTheMappedSources()
    {
        using var provider = new InMemoryDataContext();
        provider.Data[typeof(Row)] = new[] { new Row { Id = 3 }, new Row { Id = 2 } };
        provider.Data[typeof(RelatedRow)] = new[] { new RelatedRow { RowId = 3 } };
        var facade = new ApplicationContext(provider);

        var inner = facade.Rows.Join((Row row, RelatedRow related) => row.Id == related.RowId)
            .Select(pair => pair.Item1.Id)
            .ToList();
        var left = facade.Rows.LeftJoin(facade.RelatedRows, (row, related) => row.Id == related.RowId)
            .Select(pair => pair.Item1.Id)
            .ToList();

        inner.Should().Equal(3);
        left.Should().BeEquivalentTo([2, 3]);
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
        var joinWithOptions = () => facade.Rows.Join(foreign.Rows, (left, right) => left.Id == right.Id, options: null);
        var leftJoin = () => facade.Rows.LeftJoin(foreign.Rows, (left, right) => left.Id == right.Id);
        var crossJoin = () => facade.Rows.CrossJoin(foreign.Rows);

        from.Should().Throw<ArgumentException>().WithMessage("*different data context*");
        join.Should().Throw<ArgumentException>().WithMessage("*different data context*");
        joinWithOptions.Should().Throw<ArgumentException>().WithMessage("*different data context*");
        leftJoin.Should().Throw<ArgumentException>().WithMessage("*different data context*");
        crossJoin.Should().Throw<ArgumentException>().WithMessage("*different data context*");
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

    private sealed class ApplicationContext(IDataContext provider) : RepositoryContext(provider)
    {
        public IRepository<Row> Rows => Repository<Row>();
        public IRepository<RelatedRow> RelatedRows => Repository<RelatedRow>();
    }

    private sealed class Row
    {
        public int Id { get; set; }
    }

    private sealed class RelatedRow
    {
        public int RowId { get; set; }
    }
}

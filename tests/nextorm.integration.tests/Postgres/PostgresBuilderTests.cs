using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Exercises the terminal/aggregate extension surface of <see cref="EntityBuilder{TEntity}"/> and
/// the prepared-command helpers of <see cref="DataContextExtensions"/> against PostgreSQL.
/// </summary>
public sealed class PostgresBuilderTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void TerminalMethods_ShouldReturnData()
    {
        var simple = _sut.SimpleEntityAsClass;

        simple.ToList().Should().HaveCount(10);
        simple.ToArray().Should().HaveCount(10);
        simple.ToHashSet().Should().HaveCount(10);
        simple.ToDictionary(x => x.Id).Should().HaveCount(10);
        simple.ToEnumerable().Should().HaveCount(10);

        simple.First().Should().NotBeNull();
        simple.FirstOrDefault().Should().NotBeNull();
        simple.Where(x => x.Id == 1).Single().Id.Should().Be(1);
        simple.Where(x => x.Id == 1).SingleOrDefault().Should().NotBeNull();
        simple.OrderBy(x => x.Id).Last().Id.Should().Be(10);
        simple.OrderBy(x => x.Id).LastOrDefault().Should().NotBeNull();

        simple.Count().Should().Be(10);
        simple.Any().Should().BeTrue();
        simple.Min(x => x.Id).Should().Be(1);
        simple.Max(x => x.Id).Should().Be(10);
        simple.Avg(x => (double)x.Id).Should().BeGreaterThan(0);
        simple.Sum(x => (long)x.Id).Should().Be(55);
        simple.Stdev(x => (double)x.Id).Should().BeGreaterThan(0);
        simple.Stdevp(x => (double)x.Id).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AsyncTerminals_ShouldReturnData()
    {
        var ct = TestContext.Current.CancellationToken;
        var simple = _sut.SimpleEntityAsClass;

        (await simple.ToListAsync()).Should().HaveCount(10);
        (await simple.ToArrayAsync()).Should().HaveCount(10);
        (await simple.ToHashSetAsync()).Should().HaveCount(10);
        (await simple.ToDictionaryAsync(x => x.Id)).Should().HaveCount(10);
        (await simple.FirstAsync()).Should().NotBeNull();
        (await simple.FirstOrDefaultAsync()).Should().NotBeNull();
        (await simple.Where(x => x.Id == 1).SingleAsync()).Id.Should().Be(1);
        (await simple.Where(x => x.Id == 1).SingleOrDefaultAsync()).Should().NotBeNull();
        (await simple.OrderBy(x => x.Id).LastAsync()).Id.Should().Be(10);
        (await simple.OrderBy(x => x.Id).LastOrDefaultAsync()).Should().NotBeNull();
        (await simple.CountAsync()).Should().Be(10);
        (await simple.AnyAsync()).Should().BeTrue();
        (await simple.MinAsync(x => x.Id)).Should().Be(1);
        (await simple.MaxAsync(x => x.Id)).Should().Be(10);
        (await simple.SumAsync(x => (long)x.Id)).Should().Be(55);

        var enumerated = 0;
        await foreach (var row in simple.ToAsyncEnumerable())
            enumerated++;
        enumerated.Should().Be(10);

        var count = 0;
        await foreach (var row in simple.ToAsyncEnumerable(ct))
            count++;
        count.Should().Be(10);
    }

    // The query-command-producing terminal family is verified shape by shape, each as its own test:
    // every shape is prepared explicitly and executed, so a terminal that only constructs a command can
    // no longer pass unnoticed. Entity results are asserted through a null-safe projection instead of a
    // null-forgiving dereference.

    [Fact]
    public void AnyCommand_ShouldPrepareAndExecute()
    {
        var simple = _sut.SimpleEntityAsClass;
        var command = simple.Where(x => x.Id > 5).AnyCommand()
            .PrepareBuffered(cancellationToken: TestContext.Current.CancellationToken);

        command.ExecuteScalar(_sut.DataProvider, throwIfNull: true).Should().BeTrue();
    }

    [Fact]
    public void FirstOrFirstOrDefaultCommand_Entity_ShouldPrepareAndExecute()
    {
        var simple = _sut.SimpleEntityAsClass;
        var db = _sut.DataProvider;

        // Fixture/mapping precondition: the same entity projection read as a list must produce the seed
        // rows, otherwise a null single-row result would say nothing about the terminal itself.
        simple.OrderBy(x => x.Id).ToList().Should().HaveCount(10, "the seed must expose ten rows to this shape");

        var command = simple.OrderBy(x => x.Id).FirstOrFirstOrDefaultCommand()
            .PrepareBuffered(cancellationToken: TestContext.Current.CancellationToken);

        var row = command.FirstOrDefault(db);
        (row?.Id).Should().Be(1, "FirstOrDefault over a ten-row ordered seed must return the first row");
    }

    [Fact]
    public void FirstOrFirstOrDefaultCommand_Scalar_ShouldPrepareAndExecute()
    {
        var simple = _sut.SimpleEntityAsClass;
        var command = simple.OrderBy(x => x.Id).FirstOrFirstOrDefaultCommand(x => x.Id)
            .PrepareBuffered(cancellationToken: TestContext.Current.CancellationToken);

        command.FirstOrDefault(_sut.DataProvider).Should().Be(1);
    }

    [Fact]
    public void SingleOrSingleOrDefaultCommand_Entity_ShouldPrepareAndExecute()
    {
        var simple = _sut.SimpleEntityAsClass;
        var command = simple.Where(x => x.Id == 1).SingleOrSingleOrDefaultCommand()
            .PrepareBuffered(cancellationToken: TestContext.Current.CancellationToken);

        var row = command.SingleOrDefault(_sut.DataProvider);
        (row?.Id).Should().Be(1);
    }

    [Fact]
    public void SingleOrSingleOrDefaultCommand_Scalar_ShouldPrepareAndExecute()
    {
        var simple = _sut.SimpleEntityAsClass;
        var command = simple.Where(x => x.Id == 1).SingleOrSingleOrDefaultCommand(x => x.Id)
            .PrepareBuffered(cancellationToken: TestContext.Current.CancellationToken);

        command.SingleOrDefault(_sut.DataProvider).Should().Be(1);
    }

    [Fact]
    public void Prepare_List_ShouldReturnEverySeededRow()
    {
        var simple = _sut.SimpleEntityAsClass;
        var prepared = simple.Where(x => x.Id > 0).PrepareBuffered(default, TestContext.Current.CancellationToken);

        prepared.ToList(_sut.DataProvider).Should().HaveCount(10);
    }

    [Fact]
    public void CountAll_ShouldReturnRowCount()
    {
        _sut.SimpleEntity.Count().Should().Be(10);
    }

    [Fact]
    public void TableAliasSource_ShouldQuery()
    {
        var rows = _sut.From("simple_entity").Select(x => x["id"].AsInt).ToList();
        rows.Should().HaveCount(10);

        var filtered = _sut.From("simple_entity").Where(x => x["id"].AsInt > 5).Select(x => x["id"].AsInt).ToList();
        filtered.Should().HaveCount(5);
    }

    [Fact]
    public void FromSql_ShouldQuery()
    {
        var rows = _sut.DataProvider.FromSql("select id from simple_entity where id <= 3")
            .Select(x => x["id"].AsInt)
            .ToList();

        rows.Should().HaveCount(3);
    }
}

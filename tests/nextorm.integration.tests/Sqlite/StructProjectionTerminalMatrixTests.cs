using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class StructProjectionTerminalMatrixTests : IDisposable
{
    private const string Table = "t26_struct_terminal_matrix";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nextorm.t26.terminals.{Guid.NewGuid():N}.db");
    private readonly SqliteDataContext _ctx;

    public StructProjectionTerminalMatrixTests()
    {
        _ctx = new SqliteDataContext($"Data Source='{_path}';Pooling=False", new DataContextBuilder());
        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand($"create table {Table} (id integer primary key, value text not null); insert into {Table} values (1, 'one'), (2, 'two'), (3, 'three');");
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _ctx.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [SqlTable(Table)]
    public sealed class Row
    {
        [Key, Column("id")] public int Id { get; set; }
        [Column("value")] public string Value { get; set; } = string.Empty;
    }

    public struct MutableProjection { public int Id { get; set; } public string Value { get; set; } }
    public readonly record struct ReadonlyProjection(int Id, string Value);

    public static TheoryData<string, string, string> CollectionCases => new()
    {
        { "Constructor", "ToList", "Populated" }, { "Constructor", "ToList", "Empty" }, { "Constructor", "ToArray", "Populated" }, { "Constructor", "ToArray", "Empty" }, { "Constructor", "ToHashSet", "Populated" }, { "Constructor", "ToHashSet", "Empty" }, { "Constructor", "ToDictionary", "Populated" }, { "Constructor", "ToDictionary", "Empty" },
        { "MemberInit", "ToList", "Populated" }, { "MemberInit", "ToList", "Empty" }, { "MemberInit", "ToArray", "Populated" }, { "MemberInit", "ToArray", "Empty" }, { "MemberInit", "ToHashSet", "Populated" }, { "MemberInit", "ToHashSet", "Empty" }, { "MemberInit", "ToDictionary", "Populated" }, { "MemberInit", "ToDictionary", "Empty" }
    };

    public static TheoryData<string, string, string> StreamCases => new()
    {
        { "Constructor", "ToEnumerable", "Populated" }, { "Constructor", "ToEnumerable", "Empty" }, { "Constructor", "ToAsyncEnumerable", "Populated" }, { "Constructor", "ToAsyncEnumerable", "Empty" },
        { "MemberInit", "ToEnumerable", "Populated" }, { "MemberInit", "ToEnumerable", "Empty" }, { "MemberInit", "ToAsyncEnumerable", "Populated" }, { "MemberInit", "ToAsyncEnumerable", "Empty" }
    };

    public static TheoryData<string, string> EnumeratorCases => new()
    {
        { "Constructor", "CreateEnumeratorAsync" }, { "Constructor", "CreateAsyncEnumerator" },
        { "MemberInit", "CreateEnumeratorAsync" }, { "MemberInit", "CreateAsyncEnumerator" }
    };

    public static TheoryData<string, string, string> TerminalCases => new()
    {
        { "Constructor", "First", "Populated" }, { "Constructor", "First", "Empty" },
        { "Constructor", "FirstOrDefault", "Populated" }, { "Constructor", "FirstOrDefault", "Empty" },
        { "Constructor", "Last", "Populated" }, { "Constructor", "Last", "Empty" },
        { "Constructor", "LastOrDefault", "Populated" }, { "Constructor", "LastOrDefault", "Empty" },
        { "Constructor", "Single", "One" }, { "Constructor", "Single", "Empty" }, { "Constructor", "Single", "Multiple" },
        { "Constructor", "SingleOrDefault", "One" }, { "Constructor", "SingleOrDefault", "Empty" }, { "Constructor", "SingleOrDefault", "Multiple" },
        { "MemberInit", "First", "Populated" }, { "MemberInit", "First", "Empty" },
        { "MemberInit", "FirstOrDefault", "Populated" }, { "MemberInit", "FirstOrDefault", "Empty" },
        { "MemberInit", "Last", "Populated" }, { "MemberInit", "Last", "Empty" },
        { "MemberInit", "LastOrDefault", "Populated" }, { "MemberInit", "LastOrDefault", "Empty" },
        { "MemberInit", "Single", "One" }, { "MemberInit", "Single", "Empty" }, { "MemberInit", "Single", "Multiple" },
        { "MemberInit", "SingleOrDefault", "One" }, { "MemberInit", "SingleOrDefault", "Empty" }, { "MemberInit", "SingleOrDefault", "Multiple" }
    };

    public static TheoryData<string, string, string> AsyncTerminalCases => TerminalCases;

    private QueryCommand<MutableProjection> Mutable(bool empty = false, bool one = false)
    {
        var query = _ctx.From<Row>();
        if (empty) query = query.Where(x => x.Id < 0);
        if (one) query = query.Where(x => x.Id == 2);
        return query.OrderBy(x => x.Id).Select(x => new MutableProjection { Id = x.Id, Value = x.Value });
    }

    private QueryCommand<ReadonlyProjection> Constructor(bool empty = false, bool one = false)
    {
        var query = _ctx.From<Row>();
        if (empty) query = query.Where(x => x.Id < 0);
        if (one) query = query.Where(x => x.Id == 2);
        return query.OrderBy(x => x.Id).Select(x => new ReadonlyProjection(x.Id, x.Value));
    }

    [Theory]
    [MemberData(nameof(CollectionCases))]
    public void SyncCollection_Terminal_ShapeAndScenario(string shape, string terminal, string scenario)
    {
        var empty = scenario == "Empty";
        if (shape == "Constructor")
        {
            var query = Constructor(empty);
            switch (terminal)
            {
                case "ToList": query.ToList().Should().HaveCount(empty ? 0 : 3); break;
                case "ToArray": query.ToArray().Should().HaveCount(empty ? 0 : 3); break;
                case "ToHashSet": query.ToHashSet().Should().HaveCount(empty ? 0 : 3); break;
                case "ToDictionary": query.ToDictionary(x => x.Id).Should().HaveCount(empty ? 0 : 3); break;
            }
            return;
        }

        var mutable = Mutable(empty);
        switch (terminal)
        {
            case "ToList": mutable.ToList().Should().HaveCount(empty ? 0 : 3); break;
            case "ToArray": mutable.ToArray().Should().HaveCount(empty ? 0 : 3); break;
            case "ToHashSet": mutable.ToHashSet().Should().HaveCount(empty ? 0 : 3); break;
            case "ToDictionary": mutable.ToDictionary(x => x.Id).Should().HaveCount(empty ? 0 : 3); break;
        }
    }

    [Theory]
    [MemberData(nameof(CollectionCases))]
    public async Task AsyncCollection_Terminal_ShapeAndScenario(string shape, string terminal, string scenario)
    {
        var empty = scenario == "Empty";
        if (shape == "Constructor")
        {
            switch (terminal)
            {
                case "ToList": (await Constructor(empty).ToListAsync()).Should().HaveCount(empty ? 0 : 3); break;
                case "ToArray": (await Constructor(empty).ToArrayAsync()).Should().HaveCount(empty ? 0 : 3); break;
                case "ToHashSet": (await Constructor(empty).ToHashSetAsync()).Should().HaveCount(empty ? 0 : 3); break;
                case "ToDictionary": (await Constructor(empty).ToDictionaryAsync(x => x.Id)).Should().HaveCount(empty ? 0 : 3); break;
            }
            return;
        }
        switch (terminal)
        {
            case "ToList": (await Mutable(empty).ToListAsync()).Should().HaveCount(empty ? 0 : 3); break;
            case "ToArray": (await Mutable(empty).ToArrayAsync()).Should().HaveCount(empty ? 0 : 3); break;
            case "ToHashSet": (await Mutable(empty).ToHashSetAsync()).Should().HaveCount(empty ? 0 : 3); break;
            case "ToDictionary": (await Mutable(empty).ToDictionaryAsync(x => x.Id)).Should().HaveCount(empty ? 0 : 3); break;
        }
    }

    [Theory]
    [MemberData(nameof(StreamCases))]
    public async Task Stream_Terminal_ShapeAndScenario(string shape, string terminal, string scenario)
    {
        var empty = scenario == "Empty";
        if (terminal == "ToEnumerable")
        {
            if (shape == "Constructor") Constructor(empty).ToEnumerable().Should().HaveCount(empty ? 0 : 3);
            else Mutable(empty).ToEnumerable().Should().HaveCount(empty ? 0 : 3);
            return;
        }
        if (shape == "Constructor")
        {
            await AssertStream(Constructor(empty).ToAsyncEnumerable(), empty ? 0 : 3);
        }
        else
        {
            await AssertStream(Mutable(empty).ToAsyncEnumerable(), empty ? 0 : 3);
        }
    }

    private static async Task AssertStream<T>(IAsyncEnumerable<T> stream, int count)
    {
        var rows = new List<T>();
        await foreach (var row in stream) rows.Add(row);
        rows.Should().HaveCount(count);
    }

    [Theory]
    [MemberData(nameof(EnumeratorCases))]
    public async Task DirectEnumeratorFactory_Shape_Terminal(string shape, string terminal)
    {
        if (terminal == "CreateEnumeratorAsync")
        {
            if (shape == "Constructor")
            {
                var enumerator = await Constructor().CreateEnumeratorAsync();
                try { var rows = new List<ReadonlyProjection>(); while (enumerator.MoveNext()) rows.Add(enumerator.Current); rows.Should().HaveCount(3); }
                finally { enumerator.Dispose(); }
            }
            else
            {
                var enumerator = await Mutable().CreateEnumeratorAsync();
                try { var rows = new List<MutableProjection>(); while (enumerator.MoveNext()) rows.Add(enumerator.Current); rows.Should().HaveCount(3); }
                finally { enumerator.Dispose(); }
            }
            return;
        }

        if (shape == "Constructor")
        {
            await using var enumerator = Constructor().CreateAsyncEnumerator();
            var rows = new List<ReadonlyProjection>();
            while (await enumerator.MoveNextAsync()) rows.Add(enumerator.Current);
            rows.Should().HaveCount(3);
        }
        else
        {
            await using var enumerator = Mutable().CreateAsyncEnumerator();
            var rows = new List<MutableProjection>();
            while (await enumerator.MoveNextAsync()) rows.Add(enumerator.Current);
            rows.Should().HaveCount(3);
        }
    }

    [Theory]
    [MemberData(nameof(TerminalCases))]
    public void SyncRowTerminal_Shape_Terminal_Scenario(string shape, string terminal, string scenario)
    {
        if (shape == "Constructor") AssertConstructorSync(terminal, scenario);
        else AssertMutableSync(terminal, scenario);
    }

    private void AssertConstructorSync(string terminal, string scenario)
    {
        var empty = scenario == "Empty";
        var one = scenario == "One";
        var query = Constructor(empty, one);
        switch (terminal)
        {
            case "First": if (empty) FluentActions.Invoking(() => query.First()).Should().Throw<InvalidOperationException>(); else query.First().Id.Should().Be(1); break;
            case "FirstOrDefault": (empty ? query.FirstOrDefault().Id : query.FirstOrDefault().Id).Should().Be(empty ? 0 : 1); break;
            case "Last": if (empty) FluentActions.Invoking(() => query.Last()).Should().Throw<InvalidOperationException>(); else query.Last().Id.Should().Be(one ? 2 : 3); break;
            case "LastOrDefault": query.LastOrDefault().Id.Should().Be(empty ? 0 : one ? 2 : 3); break;
            case "Single": AssertSingle(query, scenario, () => query.Single(), x => x.Id); break;
            case "SingleOrDefault": AssertSingleOrDefault(query, scenario, () => query.SingleOrDefault(), x => x.Id); break;
        }
    }

    private void AssertMutableSync(string terminal, string scenario)
    {
        var empty = scenario == "Empty";
        var one = scenario == "One";
        var query = Mutable(empty, one);
        switch (terminal)
        {
            case "First": if (empty) FluentActions.Invoking(() => query.First()).Should().Throw<InvalidOperationException>(); else query.First().Id.Should().Be(1); break;
            case "FirstOrDefault": query.FirstOrDefault().Id.Should().Be(empty ? 0 : 1); break;
            case "Last": if (empty) FluentActions.Invoking(() => query.Last()).Should().Throw<InvalidOperationException>(); else query.Last().Id.Should().Be(one ? 2 : 3); break;
            case "LastOrDefault": query.LastOrDefault().Id.Should().Be(empty ? 0 : one ? 2 : 3); break;
            case "Single": AssertSingle(query, scenario, () => query.Single(), x => x.Id); break;
            case "SingleOrDefault": AssertSingleOrDefault(query, scenario, () => query.SingleOrDefault(), x => x.Id); break;
        }
    }

    private static void AssertSingle<T>(QueryCommand<T> query, string scenario, Func<T> execute, Func<T, int> id) where T : struct
    {
        if (scenario == "Empty") FluentActions.Invoking(execute).Should().Throw<InvalidOperationException>();
        else if (scenario == "Multiple") FluentActions.Invoking(execute).Should().Throw<InvalidOperationException>();
        else id(execute()).Should().Be(2);
    }

    private static void AssertSingleOrDefault<T>(QueryCommand<T> query, string scenario, Func<T> execute, Func<T, int> id) where T : struct
    {
        if (scenario == "Empty") id(execute()).Should().Be(0);
        else if (scenario == "Multiple") FluentActions.Invoking(execute).Should().Throw<InvalidOperationException>();
        else id(execute()).Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(AsyncTerminalCases))]
    public async Task AsyncRowTerminal_Shape_Terminal_Scenario(string shape, string terminal, string scenario)
    {
        if (shape == "Constructor") await AssertConstructorAsync(terminal, scenario);
        else await AssertMutableAsync(terminal, scenario);
    }

    private async Task AssertConstructorAsync(string terminal, string scenario)
    {
        var query = Constructor(scenario == "Empty", scenario == "One");
        switch (terminal)
        {
            case "First": await CheckAsync(query.FirstAsync(), scenario, 1, x => x.Id); break;
            case "FirstOrDefault": await CheckDefaultAsync(query.FirstOrDefaultAsync(), scenario, 1, x => x.Id); break;
            case "Last": await CheckAsync(query.LastAsync(), scenario, scenario == "One" ? 2 : 3, x => x.Id); break;
            case "LastOrDefault": await CheckDefaultAsync(query.LastOrDefaultAsync(), scenario, scenario == "One" ? 2 : 3, x => x.Id); break;
            case "Single": await CheckSingleAsync(query.SingleAsync(), scenario, x => x.Id); break;
            case "SingleOrDefault": await CheckSingleDefaultAsync(query.SingleOrDefaultAsync(), scenario, x => x.Id); break;
        }
    }

    private async Task AssertMutableAsync(string terminal, string scenario)
    {
        var query = Mutable(scenario == "Empty", scenario == "One");
        switch (terminal)
        {
            case "First": await CheckAsync(query.FirstAsync(), scenario, 1, x => x.Id); break;
            case "FirstOrDefault": await CheckDefaultAsync(query.FirstOrDefaultAsync(), scenario, 1, x => x.Id); break;
            case "Last": await CheckAsync(query.LastAsync(), scenario, scenario == "One" ? 2 : 3, x => x.Id); break;
            case "LastOrDefault": await CheckDefaultAsync(query.LastOrDefaultAsync(), scenario, scenario == "One" ? 2 : 3, x => x.Id); break;
            case "Single": await CheckSingleAsync(query.SingleAsync(), scenario, x => x.Id); break;
            case "SingleOrDefault": await CheckSingleDefaultAsync(query.SingleOrDefaultAsync(), scenario, x => x.Id); break;
        }
    }

    private static async Task CheckAsync<T>(Task<T> task, string scenario, int expected, Func<T, int> id) where T : struct
    {
        if (scenario == "Empty") await FluentActions.Awaiting(() => task).Should().ThrowAsync<InvalidOperationException>();
        else id(await task).Should().Be(expected);
    }

    private static async Task CheckDefaultAsync<T>(Task<T> task, string scenario, int expected, Func<T, int> id) where T : struct
    {
        var result = await task;
        id(result).Should().Be(scenario == "Empty" ? 0 : expected);
    }

    private static async Task CheckSingleAsync<T>(Task<T> task, string scenario, Func<T, int> id) where T : struct
    {
        if (scenario is "Empty" or "Multiple") await FluentActions.Awaiting(() => task).Should().ThrowAsync<InvalidOperationException>();
        else id(await task).Should().Be(2);
    }

    private static async Task CheckSingleDefaultAsync<T>(Task<T> task, string scenario, Func<T, int> id) where T : struct
    {
        if (scenario == "Multiple") await FluentActions.Awaiting(() => task).Should().ThrowAsync<InvalidOperationException>();
        else id(await task).Should().Be(scenario == "Empty" ? 0 : 2);
    }
}

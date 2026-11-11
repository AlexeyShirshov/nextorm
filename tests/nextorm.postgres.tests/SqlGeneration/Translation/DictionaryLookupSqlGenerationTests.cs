using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// A captured collection indexed by a query column (<c>dict[column]</c>) renders a portable
/// <c>CASE WHEN</c>; a constant key folds to a parameter. No database connection is opened.
/// </summary>
public class DictionaryLookupSqlGenerationTests
{
    [Fact]
    public void Dictionary_IndexedByColumn_ShouldEmitCase()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var lookup = new Dictionary<int, DateTime> { [1] = new DateTime(2024, 1, 1), [2] = new DateTime(2024, 2, 1) };

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[x.Int!.Value] < x.Datetime).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("case when nullableint = @p0 then @p1 when nullableint = @p2 then @p3 end");
    }

    [Fact]
    public void Dictionary_IndexedByConstant_ShouldFoldToParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var lookup = new Dictionary<int, DateTime> { [2] = new DateTime(2024, 2, 1) };

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[2] < x.Datetime).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Should().NotContain("case");
        prepared.DbCommandParams[0].Value.Should().Be(new DateTime(2024, 2, 1));
    }

    [Fact]
    public void ReadOnlyList_IndexedByColumn_ShouldEmitCase()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        IReadOnlyList<int> lookup = [10, 20, 30];

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[x.Int!.Value] == 20).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain("case when");
    }

    [Fact]
    public void ReadOnlyList_IndexedByConstant_ShouldFoldToParameter()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        IReadOnlyList<int> lookup = [10, 20, 30];

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[0] == 10).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Should().NotContain("case");
        prepared.DbCommandParams[0].Value.Should().Be(10);
    }

    private sealed class UnsupportedIndexer
    {
        private readonly int[] _values = [100, 200];
        public int this[int index] => _values[index];
    }

    [Fact]
    public void UnsupportedIndexer_IndexedByColumn_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var indexed = new UnsupportedIndexer();

        var act = () => ctx.GetPreparedQueryCommand(
            e.Where(x => indexed[x.Int!.Value] == 100).Select(x => x.Id), false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*captured Dictionary/List/array*");
    }

    [Fact]
    public void NullCapturedCollection_IndexedByColumn_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        IReadOnlyList<int> lookup = null!;

        var act = () => ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[x.Int!.Value] == 10).Select(x => x.Id), false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be indexed*");
    }

    private sealed class ReadOnlyListOnly<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;
        public ReadOnlyListOnly(params T[] items) => _items = items;
        public T this[int index] => _items[index];
        public int Count => _items.Length;
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }

    private sealed class ReadOnlyDictionaryOnly<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _inner;
        public ReadOnlyDictionaryOnly(Dictionary<TKey, TValue> inner) => _inner = inner;
        public TValue this[TKey key] => _inner[key];
        public IEnumerable<TKey> Keys => _inner.Keys;
        public IEnumerable<TValue> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    [Fact]
    public void GenericOnlyList_IndexedByColumn_ShouldEmitCase()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        IReadOnlyList<int> lookup = new ReadOnlyListOnly<int>(10, 20, 30);

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[x.Int!.Value] == 20).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain("case when");
    }

    [Fact]
    public void GenericOnlyDictionary_IndexedByColumn_ShouldEmitCase()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        IReadOnlyDictionary<int, int> lookup = new ReadOnlyDictionaryOnly<int, int>(new Dictionary<int, int> { [1] = 10, [2] = 20 });

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[x.Int!.Value] == 20).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain("case when");
    }
}

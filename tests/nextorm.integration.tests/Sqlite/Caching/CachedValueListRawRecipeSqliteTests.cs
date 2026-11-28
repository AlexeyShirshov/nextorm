using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// The raw-recipe gate for cached value-list commands: a filter value list is never publishable to the
/// raw recipe store, so the ordinary normalized plan cache and the live results must stay correct while
/// the captured list changes values and length. Scalar string Contains and ToUpper are not value-list
/// memberships and must keep their cached route and values.
/// </summary>
public sealed class CachedValueListRawRecipeSqliteTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public CachedValueListRawRecipeSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.raw.recipe.{Environment.ProcessId}.{Guid.NewGuid():N}.db");
        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table cached_values (id integer primary key, name text not null);
            insert into cached_values (id, name) values
                (1, 'alpha'), (3, 'beta'), (5, 'gamma'), (10, 'delta');
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    [Fact]
    public void InlineFilterValueList_ShouldReturnRowsAndKeepTheCacheContract()
    {
        var first = _db.From<CachedValue>()
            .Where(x => new int[] { 1, 3, 10 }.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        var second = _db.From<CachedValue>()
            .Where(x => new int[] { 1, 3, 10 }.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        first.Should().Equal(1, 3, 10);
        second.Should().Equal(1, 3, 10);
    }

    /// <summary>
    /// The raw-recipe gate returns <see langword="null"/> from the raw fingerprint for a filter value
    /// list. That decision may only drop the (already refused) raw publication: the ordinary normalized
    /// plan cache must still store the shape and serve a second, structurally equal command from the
    /// entry it stored — observable through the store's own lookup and the holder instance identity.
    /// </summary>
    [Fact]
    public void InlineFilterValueList_ShouldKeepTheOrdinaryPlanCacheHit()
    {
        var minimum = 0;
        var command = _db.From<CachedValue>()
            .Where(x => new int[] { 1, 3, 10 }.Contains(x.Id) && x.Id > minimum)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        command.ToList().Should().Equal(1, 3, 10);

        QueryPlanStore.TryGet(typeof(SqliteDataContext), command.GetOrCreatePlanKey(null), out var stored, out _)
            .Should().BeTrue("the ordinary plan cache must store a filter value-list shape");
        stored.Should().NotBeNull();

        minimum = 3;
        var next = _db.From<CachedValue>()
            .Where(x => new int[] { 1, 3, 10 }.Contains(x.Id) && x.Id > minimum)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        next.ToList().Should().Equal(10);

        QueryPlanStore.TryGet(typeof(SqliteDataContext), next.GetOrCreatePlanKey(null), out var reused, out _)
            .Should().BeTrue();
        reused.Should().BeSameAs(stored,
            "a second identical shape must be served by the stored plan; a miss would have replaced the holder");
    }

    [Fact]
    public void CapturedFilterValueList_ChangingValuesAndLengths_ShouldReturnEachResult()
    {
        var values = new int[] { 1, 3 };

        var first = Query(values);
        values = new int[] { 5, 10 };
        var second = Query(values);
        values = new int[] { 1, 3, 5, 10 };
        var third = Query(values);
        values = new int[] { 3 };
        var fourth = Query(values);

        first.Should().Equal(1, 3);
        second.Should().Equal(5, 10);
        third.Should().Equal(1, 3, 5, 10);
        fourth.Should().Equal(3);
    }

    [Fact]
    public void ScalarStringContainsAndToUpper_ShouldStayCorrectAndCacheable()
    {
        List<int> Contains(string fragment) => _db.From<CachedValue>()
            .Where(x => x.Name.Contains(fragment))
            .OrderBy(x => x.Id).Select(x => x.Id).ToList();

        Contains("et").Should().Equal(3);
        Contains("mm").Should().Equal(5);
        var upper = _db.From<CachedValue>()
            .OrderBy(x => x.Id)
            .Select(x => x.Name.ToUpper())
            .ToList();

        upper.Should().Equal("ALPHA", "BETA", "GAMMA", "DELTA");
    }

    private List<int> Query(int[] values)
        => _db.From<CachedValue>()
            .Where(x => values.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
}

[Table("cached_values")]
public sealed class CachedValue
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

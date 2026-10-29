using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// #296: a syntactic nullable null literal (<c>(int?)null</c>, <c>default(int?)</c>) is lowered by C#
/// to <c>Convert(Constant(null, object), int?)</c>, so the translation consumers used to miss it and
/// rendered <c>= null</c> / <c>!= null</c> instead of <c>is null</c> / <c>is not null</c>. This is the
/// live end-to-end arm of that fix on a real, file-backed SQLite database: the class creates and seeds
/// its own three-row table, so it neither reads nor perturbs the shared <c>complex_entity</c> fixture.
/// </summary>
[Collection("Sqlite")]
public sealed class T296NullableNullComparisonTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _ctx;
    private readonly ITestOutputHelper _output;

    public T296NullableNullComparisonTests(ITestOutputHelper output)
    {
        _output = output;
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.t296.nullable.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _ctx = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((DataContext)_ctx).EnsureConnectionOpen();
        Execute(
            """
            create table t296_live_nullable (id integer not null primary key, nullableint int null);
            insert into t296_live_nullable (id, nullableint) values (1, null), (2, 10), (3, 20);
            """);
    }

    public void Dispose()
    {
        _ctx.Dispose();

        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    [Fact]
    public void NullableLiteralComparisonChangesSqlAndRows()
    {
        // `== (int?)null` is a presence test: the NULL row is the only match, and the SQL says so.
        var equalityCommand = _ctx.From<IT296LiveNullable>()
            .Where(x => x.Nullable == (int?)null)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var equalitySql = SqlOf(_ctx.From<IT296LiveNullable>()
                .Where(x => x.Nullable == (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id));

        equalitySql.Should().Contain("where nullableint is null").And.NotContain("= null");

        var equalityRows = equalityCommand.ToList();
        Log("nullable == (int?)null", equalitySql, $"[{string.Join(", ", equalityRows)}]");
        equalityRows.Should().Equal(1L);

        // `!= (int?)null` is the complementary presence test.
        var inequalityCommand = _ctx.From<IT296LiveNullable>()
            .Where(x => x.Nullable != (int?)null)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var inequalitySql = SqlOf(_ctx.From<IT296LiveNullable>()
                .Where(x => x.Nullable != (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id));

        inequalitySql.Should().Contain("where nullableint is not null").And.NotContain("!= null");

        var inequalityRows = inequalityCommand.ToList();
        Log("nullable != (int?)null", inequalitySql, $"[{string.Join(", ", inequalityRows)}]");
        inequalityRows.Should().Equal(2L, 3L);

        // A derived operand keeps its arithmetic and still compares against the null literal.
        var derivedEqualitySql = SqlOf(_ctx.From<IT296LiveNullable>()
                .Where(x => (x.Nullable + 1) == (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id));

        derivedEqualitySql.Should().Contain("where (nullableint + 1) is null").And.NotContain("= null");

        var derivedEqualityRows = _ctx.From<IT296LiveNullable>()
            .Where(x => (x.Nullable + 1) == (int?)null)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        Log("(nullable + 1) == (int?)null", derivedEqualitySql, $"[{string.Join(", ", derivedEqualityRows)}]");
        derivedEqualityRows.Should().Equal(1L);

        var derivedInequalitySql = SqlOf(_ctx.From<IT296LiveNullable>()
                .Where(x => (x.Nullable + 1) != (int?)null)
                .OrderBy(x => x.Id)
                .Select(x => x.Id));

        var derivedInequalityRows = _ctx.From<IT296LiveNullable>()
            .Where(x => (x.Nullable + 1) != (int?)null)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        Log("(nullable + 1) != (int?)null", derivedInequalitySql, $"[{string.Join(", ", derivedInequalityRows)}]");
        derivedInequalityRows.Should().Equal(2L, 3L);

        // A projected comparison is translated by the independent predicate path and returns booleans.
        var projectionSql = SqlOf(_ctx.From<IT296LiveNullable>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                IsNull = x.Nullable == (int?)null,
                IsNotNull = x.Nullable != (int?)null,
            }));

        var projected = _ctx.From<IT296LiveNullable>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                IsNull = x.Nullable == (int?)null,
                IsNotNull = x.Nullable != (int?)null,
            })
            .ToList();

        Log(
            "projection nullable == / != (int?)null",
            projectionSql,
            $"[{string.Join(", ", projected.Select(x => $"{{id={x.Id}, isNull={x.IsNull}, isNotNull={x.IsNotNull}}}"))}]");

        projected.Select(x => x.Id).Should().Equal(1L, 2L, 3L);
        projected.Select(x => x.IsNull).Should().Equal(true, false, false);
        projected.Select(x => x.IsNotNull).Should().Equal(false, true, true);
    }

    /// <summary>Emits one evidence line per scenario: provider, identity, executed SQL and observed rows.</summary>
    private void Log(string scenario, string sql, string rows)
        => _output.WriteLine(
            $"provider=SQLite test=T296NullableNullComparisonTests.NullableLiteralComparisonChangesSqlAndRows " +
            $"scenario={scenario} sql={sql.Replace('\n', ' ')} rows={rows}");

    /// <summary>Renders the SQL a fresh command is prepared with, without executing it.</summary>
    private string SqlOf<TResult>(QueryCommand<TResult> command)
        => ((DbPreparedQueryCommand<TResult>)((DataContext)_ctx)
                .GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private void Execute(string sql)
    {
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

/// <summary>The isolated three-row fixture: id 1 has a NULL value, ids 2 and 3 do not.</summary>
[SqlTable("t296_live_nullable")]
public interface IT296LiveNullable
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("nullableint")]
    int? Nullable { get; set; }
}

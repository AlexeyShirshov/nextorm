using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Coverage for the #112 plain multi-column reader seam (<c>DataContext.OpenResultReader</c>): SQLite
/// used to reject every multi-column reader because the LOB path appends a <c>rowid</c> locator, which
/// the single-column guard then rejects. The plain seam prepares without sequential access, so a
/// multi-column projection must execute end to end on an in-process SQLite database.
/// </summary>
public class ResultReaderTests
{
    [SqlTable("result_reader_row")]
    private sealed class ResultReaderRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("name")]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("flag")]
        public bool Flag { get; set; }
    }

    private static SqliteDataContext CreateDb(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"nextorm-resultreader-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "create table result_reader_row (id integer primary key, name text, flag integer);" +
                "insert into result_reader_row (id, name, flag) values (1, 'alpha', 1), (2, null, 0);";
            command.ExecuteNonQuery();
        }

        return new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
    }

    [Fact]
    public void OpenResultReader_Sqlite_MultiColumn_ReadsEveryRowWithoutLocator()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var command = ctx.From<ResultReaderRow>().OrderBy(it => it.Id).ToParentCommand();
            var cacheBefore = command.Cache;

            using var owner = ctx.OpenResultReader(command, ReadOnlySpan<object?>.Empty, CancellationToken.None);

            owner.Reader.FieldCount.Should().Be(3, "the plain seam must not append a locator column");
            owner.Reader.Read().Should().BeTrue();
            owner.Reader.GetInt32(0).Should().Be(1);
            owner.Reader.GetString(1).Should().Be("alpha");
            owner.Reader.GetBoolean(2).Should().BeTrue();
            owner.Reader.Read().Should().BeTrue();
            owner.Reader.GetInt32(0).Should().Be(2);
            owner.Reader.IsDBNull(1).Should().BeTrue();
            owner.Reader.Read().Should().BeFalse();

            command.Cache.Should().Be(cacheBefore, "the seam must not mutate the shared command");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenResultReaderAsync_Sqlite_MultiColumn_ReadsEveryRow()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var command = ctx.From<ResultReaderRow>().OrderBy(it => it.Id).ToParentCommand();

            var owner = await ctx.OpenResultReaderAsync(command, null, TestContext.Current.CancellationToken);
            await using (owner.ConfigureAwait(false))
            {
                owner.Reader.FieldCount.Should().Be(3);
                (await owner.Reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
                owner.Reader.GetInt32(0).Should().Be(1);
                (await owner.Reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
                owner.Reader.GetInt32(0).Should().Be(2);
                (await owner.Reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

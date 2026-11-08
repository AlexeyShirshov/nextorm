using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// #278 live evidence on a real file-based SQLite database: the shipped engine is built without
/// <c>SQLITE_SOUNDEX</c>, so a raw <c>select soundex('Robert')</c> fails with
/// <c>no such function: soundex</c>. That native observation is the durable counterpart of the
/// reconnaissance probe; the ORM cases then prove the façade refuses the call while the query is
/// prepared and that no command reaches the server — including on the second preparation of the very
/// same query instance. <see cref="IQueryInterceptor"/> counts actual executions, so the zero-command
/// claim is measured, not inferred from the absence of an exception.
/// </summary>
[Trait("Issue", "278")]
public sealed class SqliteSoundexContractTests : IDisposable
{
    private const string RefusalMessage = "The SQLite function 'soundex' is not supported by this provider.";

    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public SqliteSoundexContractTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.t278.soundex.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();
        Seed();
    }

    /// <summary>Creates the one-table fixture through the open connection, outside every counter.</summary>
    private void Seed()
    {
        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table t278_soundex (id integer primary key, name text);
            insert into t278_soundex (id, name) values (1, 'Robert');
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>Counts real command executions on the context (setup runs before it is attached).</summary>
    private sealed class CommandCountingInterceptor : IQueryInterceptor
    {
        private int _executing;

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executing);

        public int Executions => Volatile.Read(ref _executing);
    }

    [Fact]
    public void NativeSoundex_ShouldReportMissingFunction()
    {
        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = "select soundex('Robert')";

        var act = () => command.ExecuteScalar();

        act.Should().Throw<SqliteException>().WithMessage("*no such function: soundex*");
    }

    [Fact]
    public void Soundex_QueryShouldRejectBeforeCommandExecution()
    {
        var counter = new CommandCountingInterceptor();
        _db.AddInterceptor(counter);
        var baseline = counter.Executions;
        var e = _db.From<SoundexEntity>();

        var act = () => e.Select(x => new { V = SqlFunctions.Sqlite.soundex(x.Name) }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
        counter.Executions.Should().Be(baseline, "the refusal is decided before any command executes");
    }

    [Fact]
    public void Soundex_SecondPreparationShouldRejectBeforeCommandExecution()
    {
        var counter = new CommandCountingInterceptor();
        _db.AddInterceptor(counter);
        var baseline = counter.Executions;
        var command = _db.From<SoundexEntity>()
            .Select(x => new { V = SqlFunctions.Sqlite.soundex(x.Name) });

        var first = () => command.ToList();
        var second = () => command.ToList();

        first.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
        second.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
        counter.Executions.Should().Be(baseline, "neither preparation reaches the server");
    }

    public void Dispose()
    {
        _db.Dispose();

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException exception)
        {
            Console.WriteLine($"could not delete the probe database '{_databasePath}': {exception.Message}");
        }
    }

    [SqlTable("t278_soundex")]
    private sealed class SoundexEntity
    {
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("name")]
        public string? Name { get; set; }
    }
}

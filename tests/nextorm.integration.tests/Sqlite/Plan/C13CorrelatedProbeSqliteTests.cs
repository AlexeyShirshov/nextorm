using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Campaign 13 probe: correlated subqueries through the public query surface against a real SQLite file.
/// Its purpose is to establish whether the public API reaches <c>CorrelatedQueryExpressionVisitor</c> at
/// all and whether that produces branch flips relative to the pinned campaign-12 archive.
/// </summary>
public sealed class C13CorrelatedProbeSqliteTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public C13CorrelatedProbeSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.c13.probe.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table c13_parent (id integer primary key, name text null);
            create table c13_child (id integer primary key, parent_id integer not null, amount real null);
            insert into c13_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'c');
            insert into c13_child (id, parent_id, amount) values
                (1, 1, 10.0), (2, 1, 20.0), (3, 2, 30.0);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>A correlated scalar count in the projection is evaluated per outer row.</summary>
    [Fact]
    public void CorrelatedCountInProjection_ShouldCountPerParent()
    {
        var rows = _db.From<C13Parent>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Children = _db.From<C13Child>().Where(c => c.ParentId == x.Id).Count()
            })
            .ToList();

        rows.Select(r => r.Children).Should().Equal(2, 1, 0);
    }

    /// <summary>A correlated EXISTS-shaped predicate filters the outer rows.</summary>
    [Fact]
    public void CorrelatedAnyInPredicate_ShouldFilterOuterRows()
    {
        var ids = _db.From<C13Parent>()
            .Where(x => _db.From<C13Child>().Where(c => c.ParentId == x.Id).Any())
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1, 2);
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Locked temporary files are left to the OS; the test outcome is unaffected.
        }
    }

    /// <summary>The outer row.</summary>
    [SqlTable("c13_parent")]
    public sealed class C13Parent
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The parent name.</summary>
        [Column("name")]
        public string? Name { get; set; }
    }

    /// <summary>The inner row.</summary>
    [SqlTable("c13_child")]
    public sealed class C13Child
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The owning parent id.</summary>
        [Column("parent_id")]
        public int ParentId { get; set; }

        /// <summary>The optional amount.</summary>
        [Column("amount")]
        public double? Amount { get; set; }
    }
}

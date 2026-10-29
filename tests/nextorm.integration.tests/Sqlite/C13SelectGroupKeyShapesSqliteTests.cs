using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Campaign 13, group A (rewritten on the right API): <c>SelectGroup</c> projections whose key expressions
/// have deliberately different shapes, so <c>SelectGroupExpressionComparer</c> is traversed on the
/// select-group path. Every fact asserts the grouped rows; refused shapes assert the refusal.
/// </summary>
public sealed class C13SelectGroupKeyShapesSqliteTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public C13SelectGroupKeyShapesSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.c13.sg.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table c13_sg (id integer primary key, name text null, grp text null, amount real not null);
            insert into c13_sg (id, name, grp, amount) values
                (1, 'alpha', 'a', 10.0), (2, 'ALPHA', 'a', 20.0),
                (3, 'beta', 'b', 30.0), (4, 'gamma', 'b', 40.0);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>A member key on the select-group path.</summary>
    [Fact]
    public void MemberKey_ShouldGroupByTheColumn()
    {
        var groups = _db.From<C13Sg>()
            .SelectGroup(x => new { x.Grp, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() })
            .ToList();

        groups.Should().HaveCount(2);
        groups.Single(g => g.Grp == "a").S.Should().Be(30.0);
        groups.Single(g => g.Grp == "b").C.Should().Be(2);
    }

    /// <summary>A method-call key exercises the MethodCall arm of the comparer.</summary>
    [Fact]
    public void MethodCallKey_ShouldGroupByTheCallResult()
    {
        var groups = _db.From<C13Sg>()
            .SelectGroup(x => new { Key = x.Name!.ToUpper(), S = SqlFunctions.Sql.sum(x.Amount) })
            .ToList();

        groups.Should().HaveCount(3);
        groups.Single(g => g.Key == "ALPHA").S.Should().Be(30.0);
    }

    /// <summary>An arithmetic key exercises the Binary arm.</summary>
    [Fact]
    public void ArithmeticKey_ShouldGroupByTheResult()
    {
        var groups = _db.From<C13Sg>()
            .SelectGroup(x => new { Key = x.Id % 2, C = SqlFunctions.Sql.count() })
            .ToList();

        groups.Should().HaveCount(2);
        groups.Single(g => g.Key == 0).C.Should().Be(2);
    }

    /// <summary>A converted key exercises the Unary/Convert arm.</summary>
    [Fact]
    public void ConvertedKey_ShouldGroupByTheConvertedValue()
    {
        var groups = _db.From<C13Sg>()
            .SelectGroup(x => new { Key = (long)x.Id, C = SqlFunctions.Sql.count() })
            .ToList();

        groups.Should().HaveCount(4);
    }

    /// <summary>A conditional key either groups correctly or is refused by name.</summary>
    [Fact]
    public void ConditionalKey_ShouldGroupOrBeRefused()
    {
        var build = () => _db.From<C13Sg>()
            .SelectGroup(x => new { Key = x.Amount > 20.0 ? 1 : 0, C = SqlFunctions.Sql.count() })
            .ToList();

        try
        {
            var groups = build();
            groups.Should().HaveCount(2);
            groups.Sum(g => g.C).Should().Be(4);
        }
        catch (NotSupportedException refusal)
        {
            refusal.Message.Should().NotBeNullOrWhiteSpace("a refused key shape must name the reason");
        }
    }

    /// <summary>A captured closure value as the key keeps one group for that value.</summary>
    [Fact]
    public void CapturedKey_ShouldGroupByTheCapturedValue()
    {
        var wanted = "a";

        var groups = _db.From<C13Sg>()
            .Where(x => x.Grp == wanted)
            .SelectGroup(x => new { x.Grp, S = SqlFunctions.Sql.sum(x.Amount) })
            .ToList();

        groups.Should().ContainSingle();
        groups[0].S.Should().Be(30.0);
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

    /// <summary>The seeded select-group row.</summary>
    [SqlTable("c13_sg")]
    public sealed class C13Sg
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The optional name.</summary>
        [Column("name")]
        public string? Name { get; set; }

        /// <summary>The optional group label.</summary>
        [Column("grp")]
        public string? Grp { get; set; }

        /// <summary>The amount aggregated per group.</summary>
        [Column("amount")]
        public double Amount { get; set; }
    }
}

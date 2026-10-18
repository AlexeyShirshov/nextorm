using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// MariaDB dialect branch coverage (cycle 3, cluster D4) executed against a live MariaDB Testcontainers
/// server (or the server named by <c>NEXTORM_MARIADB_CONNECTION</c>). Every fact runs over the
/// class-owned <c>md3_marker</c> table, created, seeded and dropped by the class.
///
/// <para>
/// MariaDB has no <c>ITestProvider</c>, so this class follows the existing MariaDB integration
/// pattern (<c>MariaDbFunctionsIntegrationTests</c>): a direct <see cref="MariaDbDataContext"/> with an
/// explicit skip unless <see cref="MariaDbContainer"/> is available. All three
/// <c>IntegrationSuiteScopingTests</c> database-evidence markers hold (container type, harness type,
/// connection open).
/// </para>
///
/// <para>
/// Targeted surface: the identifier/limit rendering of a normal query through the MariaDB context, and
/// the MariaDB-only native render branch (<c>MariaDbNativeFunctions.regexp_replace</c>) that the MySQL
/// wrapper does not expose.
/// </para>
/// </summary>
public sealed class MariaDbDialectBranchCoverage3Tests : IDisposable
{
    private const string CreateTableSql =
        "create table md3_marker (id int not null primary key, dt datetime null, label varchar(50) null, grp int null)";

    private const string SeedSql =
        "insert into md3_marker (id, dt, label, grp) values "
        + "(1, '2023-05-17 13:45:30', 'one', 1), "
        + "(2, '2023-01-02 00:00:00', 'two', 1), "
        + "(3, '2024-03-04 05:06:07', 'three', 2)";

    private readonly IDataContext? _ctx;

    public MariaDbDialectBranchCoverage3Tests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        CreateMarker();
    }

    /// <summary>One case per MariaDB render branch: the identifier/limit query and the MariaDB-only native function.</summary>
    [Theory]
    [InlineData("identifier_and_limit")]
    [InlineData("mariadb_native_render_branch")]
    public void DialectBranches_ShouldReadLiveValues(string fn)
    {
        var context = _ctx!;

        switch (fn)
        {
            case "identifier_and_limit":
                var page = context.From<Md3Marker>()
                    .Where(x => x.Grp != null)
                    .OrderBy(x => x.Id)
                    .Offset(1)
                    .Limit(2)
                    .Select(x => new { x.Id, x.Label })
                    .ToList();

                page.Select(r => r.Id).Should().Equal(2, 3);
                page.Select(r => r.Label).Should().Equal("two", "three");
                break;
            case "mariadb_native_render_branch":
                context.From<Md3Marker>()
                    .Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.regexp_replace("ab12cd", "[0-9]", ""))
                    .First().Should().Be("abcd");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown MariaDB branch case.");
        }
    }

    public void Dispose()
    {
        if (_ctx is null)
            return;

        try
        {
            DropMarker();
        }
        finally
        {
            _ctx.Dispose();
        }
    }

    private void Execute(string sql)
    {
        var context = (DataContext)_ctx!;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateMarker()
    {
        Execute("drop table if exists md3_marker");
        Execute(CreateTableSql);
        Execute(SeedSql);
    }

    private void DropMarker() => Execute("drop table if exists md3_marker");

    /// <summary>The class-owned marker table: a nullable datetime, text payload and grouping column.</summary>
    [SqlTable("md3_marker")]
    public sealed class Md3Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("dt")]
        public DateTime? Dt { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }
}

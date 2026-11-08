using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the flat row-constructor surface (#126) against a real MariaDB server: row comparison in
/// <c>Where</c>, an AND/OR combination of row comparisons, and inline <c>.ItemN</c> folding in filter
/// and projection. MariaDB inherits the MySQL dialect, but this suite owns its own MariaDB
/// Testcontainers instance (or <c>NEXTORM_MARIADB_CONNECTION</c>) and <see cref="MariaDbDataContext"/>,
/// so a MySQL run can never stand in for MariaDB evidence. It skips explicitly when neither the
/// container nor an external server is available; the container is released by the
/// <see cref="DatabaseContainers"/> assembly fixture at the end of the run.
/// </summary>
public sealed class MariaDbTupleExecutionTests : IDisposable
{
    private const string ProbeTable = "tuple_exec_probe";

    private readonly IDataContext _ctx;

    public MariaDbTupleExecutionTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    private EntityBuilder<ITupleExecProbe> Probe => _ctx.From<ITupleExecProbe>();

    [Fact]
    public void Tuple_RowComparisonInWhere_ShouldExecuteAndFilter()
    {
        var ids = Probe
            .Where(x => Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a"))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1L);
    }

    [Fact]
    public void Tuple_RowComparisonAndOr_ShouldExecuteAndFilter()
    {
        var ids = Probe
            .Where(x => (Tuple.Create(x.Id, x.Name) == Tuple.Create(1L, "a"))
                        || (Tuple.Create(x.Id, x.Name) == Tuple.Create(3L, "c")))
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEquivalentTo(new[] { 1L, 3L });
    }

    [Fact]
    public void Tuple_InlineElementAccess_ShouldExecuteInFilterAndProjection()
    {
        var ids = Probe
            .Where(x => Tuple.Create(x.Id, x.Name).Item1 > 1L)
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEquivalentTo(new[] { 2L, 3L });

        var names = Probe
            .OrderBy(x => Tuple.Create(x.Id, x.Name).Item1)
            .Select(x => Tuple.Create(x.Id, x.Name).Item2)
            .ToList();

        names.Should().Equal("a", "b", "c");
    }

    private void Seed()
    {
        Execute($"drop table if exists {ProbeTable}");
        Execute($"create table {ProbeTable} (id bigint not null primary key, name varchar(50) null)");
        Execute($"insert into {ProbeTable} (id, name) values (1, 'a'), (2, 'b'), (3, 'c')");
    }

    private void Execute(string sql)
    {
        var ctx = (DataContext)_ctx;
        ctx.EnsureConnectionOpen();
        using var cmd = ctx.CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("tuple_exec_probe")]
internal interface ITupleExecProbe
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

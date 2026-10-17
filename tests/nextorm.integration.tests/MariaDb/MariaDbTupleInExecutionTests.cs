using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// D193.4 — MariaDB execution of the tuple value-list <c>IN</c>/<c>Contains</c> path (#193). MariaDB has
/// no <see cref="ProviderTestSuite"/> entry (the shared suite runs on MySQL only), so this standalone
/// class owns its MariaDB Testcontainers instance (or <c>NEXTORM_MARIADB_CONNECTION</c>) and
/// <see cref="MariaDbDataContext"/>. It skips explicitly when neither is available; the container is
/// released by the <see cref="DatabaseContainers"/> assembly fixture at the end of the run.
/// </summary>
public sealed class MariaDbTupleInExecutionTests : IDisposable
{
    private const string ProbeTable = "tuple_in_exec_probe";

    private readonly IDataContext _ctx;

    public MariaDbTupleInExecutionTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    private EntityBuilder<ITupleInExecProbe> Probe => _ctx.From<ITupleInExecProbe>();

    [Fact]
    public void TupleIn_CapturedList_ShouldFilter()
    {
        var tuples = new List<(long Id, string? Name)> { (1, "a"), (2, "b") };

        var ids = Probe
            .Where(x => tuples.Contains(new ValueTuple<long, string?>(x.Id, x.Name)))
            .Select(x => x.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(1L, 2L);
    }

    [Fact]
    public void TupleIn_MismatchedComponents_ShouldReturnEmpty()
    {
        var tuples = new List<(long, string?)> { (1, "b"), (2, "a") };

        var ids = Probe
            .Where(x => tuples.Contains(new ValueTuple<long, string?>(x.Id, x.Name)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void TupleIn_Empty_ShouldReturnEmpty()
    {
        var tuples = new List<(long, string?)>();

        var ids = Probe
            .Where(x => tuples.Contains(new ValueTuple<long, string?>(x.Id, x.Name)))
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void TupleIn_NullComponent_ShouldMatchNullRow()
    {
        var tuples = new List<(long, string?)> { (3, null) };

        var ids = Probe
            .Where(x => tuples.Contains(new ValueTuple<long, string?>(x.Id, x.Name)))
            .Select(x => x.Id)
            .ToList();

        // id 3 carries a null name; a null tuple component must match it deterministically.
        ids.Should().Equal(3L);
    }

    [Fact]
    public void TupleIn_Negation_ShouldExcludeMatch()
    {
        var tuples = new List<(long, string?)> { (1, "a") };

        var ids = Probe
            .Where(x => !tuples.Contains(new ValueTuple<long, string?>(x.Id, x.Name)))
            .Select(x => x.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(2L, 3L);
    }

    private void Seed()
    {
        Execute($"drop table if exists {ProbeTable}");
        Execute($"create table {ProbeTable} (id bigint not null primary key, name varchar(50) null)");
        Execute($"insert into {ProbeTable} (id, name) values (1, 'a'), (2, 'b'), (3, null)");
    }

    private void Execute(string sql)
    {
        var ctx = (DataContext)_ctx;
        ctx.EnsureConnectionOpen();
        using var cmd = ctx.CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("tuple_in_exec_probe")]
internal interface ITupleInExecProbe
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

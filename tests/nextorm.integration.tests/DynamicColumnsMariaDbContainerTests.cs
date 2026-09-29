using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// Container-backed capability probe for the dynamic-columns read on MariaDB 11.4 (issue #110):
/// MariaDB rejects a bare <c>*</c> mixed with explicit select expressions, so the projected star must
/// be alias-qualified (<c>select t1.id, t1.name, `t1`.* from … as `t1`</c>). SQL generation is pinned by
/// <c>nextorm.mariadb.tests.SqlGenerationTests.MariaDb_MappedPlusDynamic_QualifiedStar</c>; this test
/// proves the generated statement actually <b>executes</b> on the server and that the unmapped physical
/// column is materialised into the store. It skips explicitly when neither the Testcontainers instance
/// nor <c>NEXTORM_MARIADB_CONNECTION</c> is available, and the container is released by the
/// <see cref="DatabaseContainers"/> assembly fixture at the end of the run.
/// </summary>
public sealed class DynamicColumnsMariaDbContainerTests : IDisposable
{
    private const string ProbeTable = "dynamic_read_probe";

    private readonly IDataContext _ctx;

    public DynamicColumnsMariaDbContainerTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void DynamicRead_ShouldExecuteAndMaterialiseUnmappedColumn()
    {
        var rows = _ctx.From<DynamicReadProbe>().ToList();

        var row = rows.Should().ContainSingle(x => x.Id == 1).Subject;
        row.Name.Should().Be("fixed");
        row.Extra.Should().ContainKey("dyn_col").WhoseValue.Should().Be("inside-store");
        row.Extra.Should().NotContainKey("id");
        row.Extra.Should().NotContainKey("name");

        // The statement that ran is the exact alias-qualified star form MariaDB accepts.
        SqlOf(_ctx, _ctx.From<DynamicReadProbe>().ToCommand())
            .Should().Be("select t1.id, t1.name, `t1`.* from dynamic_read_probe as `t1`");
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(
            cmd, false, false, CancellationToken.None);
        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    private void Seed()
    {
        Execute($"drop table if exists {ProbeTable}");
        Execute($"create table {ProbeTable} (id int not null primary key, name varchar(50) not null, dyn_col varchar(50) null)");
        Execute($"insert into {ProbeTable} (id, name, dyn_col) values (1, 'fixed', 'inside-store')");
    }

    private void Execute(string sql)
    {
        var ctx = (DataContext)_ctx;
        ctx.EnsureConnectionOpen();
        using var cmd = ctx.CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("dynamic_read_probe")]
public sealed class DynamicReadProbe
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}

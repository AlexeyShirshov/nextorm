using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T219 (#219) MariaDB runtime for the session/server identity functions. MariaDB is not part of the
/// shared provider matrix (it inherits the MySQL dialect), so this case uses an explicit MariaDB server:
/// a <c>mariadb:11.4</c> Testcontainers instance unless <c>NEXTORM_MARIADB_CONNECTION</c> points at an
/// existing server — the MySQL runtime is never passed off as MariaDB. The native <c>version()</c> is
/// asserted to contain <c>MariaDB</c> so the provenance is proven, and the case skips explicitly when
/// neither source is available.
/// </summary>
public sealed class MariaDbSessionIdentitySpecificTests : IDisposable
{
    private const string ProbeTable = "t219_identity_probe";

    private readonly IDataContext _ctx;

    public MariaDbSessionIdentitySpecificTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());

        Execute($"drop table if exists {ProbeTable}");
        Execute($"create table {ProbeTable} (id int not null primary key)");
        Execute($"insert into {ProbeTable} (id) values (1)");
    }

    public void Dispose() => _ctx.Dispose();

    /// <summary>
    /// The inherited MySQL identity functions execute on MariaDB: <c>CONNECTION_ID()</c> materializes as
    /// <see cref="ulong"/>, <c>USER()</c> as the client <c>user@host</c> and the legacy
    /// <c>current_user()</c> as the effective account. All three are cross-checked against the native
    /// functions on the same held connection, and the server is proven to be MariaDB.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T219")]
    public void SessionIdentity_MariaDbFunctions_ShouldExecuteAndMatchNativeValues()
    {
        var projected = _ctx.From<T219IdentityProbe>().Where(x => x.Id == 1)
            .Select(_ => new
            {
                ConnectionId = SqlFunctions.Sql.connection_id(),
                User = SqlFunctions.Sql.user(),
                CurrentUser = SqlFunctions.Sql.current_user()
            })
            .First();

        projected.ConnectionId.Should().BePositive("CONNECTION_ID() is a live server connection id");
        projected.User.Should().NotBeNullOrWhiteSpace();
        projected.User.Should().Contain("@", "USER() is the client account as user@host");
        projected.CurrentUser.Should().NotBeNullOrWhiteSpace();

        using var native = _ctx.ExecuteRaw(
            "select CAST(CONNECTION_ID() AS UNSIGNED) as connection_id, USER() as `user`, " +
            "CURRENT_USER() as `current_user`, version() as server_version");

        var row = native.Read<MariaDbIdentityProbe>().Single();

        row.ServerVersion.Should().Contain("MariaDB", "this runtime case must run against MariaDB, not MySQL");
        row.ConnectionId.Should().Be(projected.ConnectionId);
        row.User.Should().Be(projected.User);
        row.CurrentUser.Should().Be(projected.CurrentUser);
    }

    private void Execute(string sql)
    {
        var ctx = (DataContext)_ctx;
        ctx.EnsureConnectionOpen();
        using var cmd = ctx.CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private sealed class MariaDbIdentityProbe
    {
        [Column("connection_id")]
        public ulong ConnectionId { get; set; }

        [Column("user")]
        public string User { get; set; } = "";

        [Column("current_user")]
        public string CurrentUser { get; set; } = "";

        [Column("server_version")]
        public string ServerVersion { get; set; } = "";
    }
}

[SqlTable("t219_identity_probe")]
public sealed class T219IdentityProbe
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

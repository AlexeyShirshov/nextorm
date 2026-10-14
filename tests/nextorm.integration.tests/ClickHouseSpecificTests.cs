using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert ClickHouse specific server/identity behaviour (T219 / #219), backed by a
/// Testcontainers instance unless NEXTORM_CLICKHOUSE_CONNECTION points at an existing server. The
/// session/server identity functions execute on the real server and materialize their declared CLR
/// types; the native server values are cross-checked on the same held connection.
/// </summary>
public sealed class ClickHouseSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    /// <summary>
    /// <c>hostName()</c> and <c>tcpPort()</c> are server properties: the projection materializes them as
    /// <see cref="string"/> and <see cref="int"/> and matches the native values, which pins the
    /// <c>CAST(tcpPort() AS Int32)</c> rendering. <c>connectionId()</c> is checked for the unsigned
    /// materialization contract: it is a <see cref="ulong"/> projection and the same server expression
    /// expanded twice in one query yields one value (the HTTP driver may hand a new connection id to a
    /// later request, so the native value is not compared across commands).
    /// </summary>
    [Fact]
    [Trait("PDCA", "T219")]
    public void SessionIdentity_ClickHouseFunctions_ShouldExecuteAndMaterialize()
    {
        var ctx = _sut.DataProvider;

        var projected = _sut.SimpleEntity.Where(x => x.Id == 1)
            .Select(_ => new
            {
                HostName = SqlFunctions.ClickHouse.host_name(),
                TcpPort = SqlFunctions.ClickHouse.tcp_port(),
                ConnectionId = SqlFunctions.Sql.connection_id(),
                ConnectionIdAgain = SqlFunctions.Sql.connection_id()
            })
            .First();

        projected.HostName.Should().NotBeNullOrWhiteSpace("hostName() is the host of the server that answered");
        projected.TcpPort.Should().BeInRange(1, 65535, "tcpPort() is the server's native listen port");

        projected.ConnectionId.Should().Be(
            projected.ConnectionIdAgain,
            "one query expands the same CAST(connectionId() AS UInt64) twice and the server returns one value");

        using var native = ctx.ExecuteRaw(
            "select hostName() as host_name, CAST(tcpPort() AS Int32) as tcp_port");

        var row = native.Read<ClickHouseIdentityProbe>().Single();

        row.HostName.Should().Be(projected.HostName);
        row.TcpPort.Should().Be(projected.TcpPort);
    }

    private sealed class ClickHouseIdentityProbe
    {
        [Column("host_name")]
        public string HostName { get; set; } = "";

        [Column("tcp_port")]
        public int TcpPort { get; set; }
    }
}

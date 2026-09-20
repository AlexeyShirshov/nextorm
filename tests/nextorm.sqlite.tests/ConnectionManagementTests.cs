using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Pins the connection contract of <see cref="DataContext"/>: who owns the connection, what
/// <see cref="DataContext.ConnectionString"/> resolves to, and that provider setup runs on every
/// connection the context starts using. The supplied-connection path is not covered by the
/// behavioural suite, so it is verified here.
/// </summary>
public class ConnectionManagementTests
{
    private static SqliteDataContext WithConnectionString(string connectionString = "Data Source=:memory:")
        => new(connectionString, new DataContextBuilder());

    private static SqliteDataContext WithConnection(DbConnection connection)
        => new(connection, new DataContextBuilder());

    [Fact]
    public void GetConnection_WithConnectionString_CreatesProviderConnection()
    {
        using var ctx = WithConnectionString();

        ctx.GetConnection().Should().BeOfType<SqliteConnection>();
    }

    [Fact]
    public void GetConnection_IsStableAcrossCalls()
    {
        using var ctx = WithConnectionString();

        ctx.GetConnection().Should().BeSameAs(ctx.GetConnection());
    }

    [Fact]
    public void GetConnection_WithSuppliedConnection_ReturnsThatInstance()
    {
        using var supplied = new SqliteConnection("Data Source=:memory:");
        using var ctx = WithConnection(supplied);

        ctx.GetConnection().Should().BeSameAs(supplied);
    }

    [Fact]
    public void Dispose_WithSuppliedConnection_LeavesItOpen()
    {
        using var supplied = new SqliteConnection("Data Source=:memory:");
        supplied.Open();

        using (var ctx = WithConnection(supplied))
        {
            ctx.EnsureConnectionOpen();
        }

        supplied.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public void Dispose_WithOwnConnection_DisposesIt()
    {
        var ctx = WithConnectionString();
        var conn = ctx.GetConnection();
        ctx.EnsureConnectionOpen();
        conn.State.Should().Be(ConnectionState.Open);

        ctx.Dispose();

        conn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void ConnectionString_WithConnectionString_ReturnsIt()
    {
        using var ctx = WithConnectionString("Data Source=:memory:");

        ctx.ConnectionString.Should().Be("Data Source=:memory:");
    }

    [Fact]
    public void ConnectionString_WithSuppliedConnection_ComesFromTheConnection()
    {
        using var supplied = new SqliteConnection("Data Source=:memory:");
        using var ctx = WithConnection(supplied);

        ctx.ConnectionString.Should().Be(supplied.ConnectionString);
    }

    [Fact]
    public void ConnectionManager_ExposesTheSameConnection()
    {
        using var ctx = WithConnectionString();

        ((IConnectionManager)ctx).GetConnection().Should().BeSameAs(ctx.GetConnection());
    }

    [Fact]
    public void SuppliedConnection_GetsProviderFunctionsRegistered()
    {
        using var supplied = new SqliteConnection("Data Source=:memory:");
        using var ctx = WithConnection(supplied);
        ctx.EnsureConnectionOpen();

        using var cmd = supplied.CreateCommand();
        cmd.CommandText = "select stdev(x) from (select 1.0 as x union all select 3.0)";

        var result = cmd.ExecuteScalar();

        result.Should().BeOfType<double>().Which.Should().BeApproximately(1.4142135623730951, 1e-12);
    }

    [Fact]
    public void SuppliedConnection_RunsQueriesAgainstItsOwnDatabase()
    {
        // ':memory:' is per-connection, so the table set up here is visible to the context only
        // because the context reuses this very connection instead of creating one of its own.
        using var supplied = new SqliteConnection("Data Source=:memory:");
        supplied.Open();
        using (var setup = supplied.CreateCommand())
        {
            setup.CommandText = "create table simple_entity (id integer primary key);" +
                                "insert into simple_entity (id) values (42);";
            setup.ExecuteNonQuery();
        }

        using var ctx = WithConnection(supplied);

        ctx.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(42);
    }

    [Fact]
    public void Dispose_WithSuppliedConnection_UnsubscribesDisposedHandler()
    {
        var supplied = new SqliteConnection("Data Source=:memory:");
        var weak = CreateDisposedContextWithSuppliedConnection(supplied);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // The still-alive (caller-owned) connection must not keep the disposed context reachable
        // through its Disposed event.
        weak.IsAlive.Should().BeFalse();
        GC.KeepAlive(supplied);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedContextWithSuppliedConnection(DbConnection connection)
    {
        var ctx = WithConnection(connection);
        ctx.GetConnection();
        var weak = new WeakReference(ctx);
        ctx.Dispose();
        return weak;
    }
}

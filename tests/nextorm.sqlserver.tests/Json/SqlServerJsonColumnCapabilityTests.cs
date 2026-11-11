using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>Shared fixtures for the T326 (#326) SQL Server native <c>json</c> capability tests.</summary>
internal static class T326JsonTestSupport
{
    internal const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";
}

/// <summary>A context type that can be constructed unversioned or with a version (mixed-use guard).</summary>
internal sealed class T326MixedVersionContext : SqlServerDataContext
{
    public T326MixedVersionContext()
        : base(T326JsonTestSupport.PlaceholderConnectionString, new DataContextBuilder())
    {
    }

    public T326MixedVersionContext(Version serverVersion)
        : base(serverVersion, T326JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>A context type used with two different declared versions (two-versions guard).</summary>
internal sealed class T326TwoVersionContext : SqlServerDataContext
{
    public T326TwoVersionContext(Version serverVersion)
        : base(serverVersion, T326JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>A context type declared for SQL Server 2016 (gate closed).</summary>
internal sealed class T326V16Context : SqlServerDataContext
{
    public T326V16Context()
        : base(new Version(16, 0), T326JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>A context type declared for SQL Server 2025 (gate open).</summary>
internal sealed class T326V17Context : SqlServerDataContext
{
    public T326V17Context()
        : base(new Version(17, 0), T326JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>Exercises the version-first <see cref="DbConnection"/> constructor.</summary>
internal sealed class T326ConnectionContext(DbConnection connection)
    : SqlServerDataContext(new Version(16, 0), connection);

/// <summary>
/// T326 (#326) — the SQL Server JSON storage gate. The native <c>json</c> column exists on SQL Server
/// 2025+ (major 17) and on the Azure SQL engines built on it, so the capability is true only for an
/// explicitly declared major version of 17 or higher. The unversioned dialect keeps the historical text
/// behaviour, because a <c>nvarchar(max)</c>-backed <c>[JsonColumn]</c> must not silently change. The
/// native representation is deliberately the plain CLR <c>string</c>: SqlClient has no DOM type that
/// round-trips both a <c>json</c> and an <c>nvarchar</c> column. These tests never open a connection.
/// </summary>
public class SqlServerJsonColumnCapabilityTests
{
    [Fact]
    public void UnversionedDialect_ShouldNotAdvertiseNativeJsonStorage()
    {
        SqlServerDialect.Instance.SupportsJson.Should().BeFalse("an unversioned context keeps the historical text mapping");
        new SqlServerDialect().SupportsJson.Should().BeFalse();
        SqlServerDialect.Instance.NativeJsonProviderType.Should().Be(typeof(string));
    }

    [Theory]
    [InlineData(15, 0, false)]
    [InlineData(16, 0, false)]
    [InlineData(17, 0, true)]
    [InlineData(17, 5, true)]
    [InlineData(18, 2, true)]
    public void VersionAwareDialect_ShouldGateNativeStorageOnMajorSeventeen(int major, int minor, bool expected)
    {
        var dialect = new SqlServerDialect(new Version(major, minor));

        dialect.SupportsJson.Should().Be(expected, "the gate is the declared major version, not the compatibility level");
        dialect.NativeJsonProviderType.Should().Be(
            typeof(string),
            "the native json column is transported as text in every configuration");
    }

    [Fact]
    public void VersionFirstConstructors_ShouldExistAndRejectANullVersion()
    {
        FluentActions.Invoking(() => new SqlServerDataContext((Version)null!, T326JsonTestSupport.PlaceholderConnectionString))
            .Should().Throw<ArgumentNullException>().WithParameterName("serverVersion");

        FluentActions.Invoking(() => new SqlServerDataContext((Version)null!, new SqlConnection(T326JsonTestSupport.PlaceholderConnectionString)))
            .Should().Throw<ArgumentNullException>().WithParameterName("serverVersion");
    }

    [Fact]
    public void VersionFirstConstructor_ShouldExposeTheDeclaredVersionThroughTheDialect()
    {
        using var v16 = new T326V16Context();
        using var v17 = new T326V17Context();

        v16.Dialect.SupportsJson.Should().BeFalse();
        v17.Dialect.SupportsJson.Should().BeTrue();
        v17.Dialect.NativeJsonProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void VersionFirstConnectionConstructor_ShouldBeAcceptedAndGateClosedForMajorSixteen()
    {
        using var connection = new SqlConnection(T326JsonTestSupport.PlaceholderConnectionString);
        using var context = new T326ConnectionContext(connection);

        context.Dialect.SupportsJson.Should().BeFalse();
    }
}

/// <summary>
/// T326 (#326) — the per-context-type version guard. The plan cache is keyed by the context type, so a
/// concrete context type owns exactly one immutable server version: mixing the unversioned and a
/// version-aware construction, or two different versions, is a configuration error raised in the
/// constructor — before any command is prepared or executed.
/// </summary>
public class SqlServerJsonColumnRegistrationTests
{
    [Fact]
    public void SameContextType_ShouldRefuseUnversionedThenVersioned()
    {
        using var unversioned = new T326MixedVersionContext();

        var act = () => new T326MixedVersionContext(new Version(17, 0));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(T326MixedVersionContext)}*")
            .WithMessage("*unset (null)*")
            .WithMessage("*17.0*")
            .WithMessage("*derive a distinct DataContext subclass*");
    }

    [Fact]
    public void SameContextType_ShouldRefuseSixteenThenSeventeen()
    {
        using var sixteen = new T326TwoVersionContext(new Version(16, 0));

        var act = () => new T326TwoVersionContext(new Version(17, 0));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(T326TwoVersionContext)}*")
            .WithMessage("*16.0*")
            .WithMessage("*17.0*");
    }

    [Fact]
    public void DistinctContextTypes_ShouldCoexistWithDifferentVersions()
    {
        using var v16 = new T326V16Context();
        using var v17 = new T326V17Context();

        v16.Dialect.SupportsJson.Should().BeFalse();
        v17.Dialect.SupportsJson.Should().BeTrue();
    }
}

/// <summary>A row whose JSON column explicitly asks for native storage.</summary>
[SqlTable("t326_native_refusal")]
public interface IT326NativeJsonRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("data")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    T326NativeJsonDoc Data { get; set; }
}

/// <summary>The JSON document model of <see cref="IT326NativeJsonRow"/>.</summary>
public sealed class T326NativeJsonDoc
{
    public string? Name { get; set; }
}

/// <summary>
/// T326 (#326) — an explicit <see cref="JsonColumnStorage.Native"/> mapping on a gate-closed SQL Server
/// (major 16) is refused with a named <see cref="NotSupportedException"/> while the converter is
/// resolved, so nothing reaches the server: zero commands are created and executed.
/// </summary>
public class SqlServerJsonColumnRefusalTests
{
    [Fact]
    public void NativeStorage_OnAnExplicitSixteenContext_ShouldRefuseWithDialectNameAndZeroCommands()
    {
        using var context = new T326V16Context();
        var counter = new CommandCounter();
        context.AddInterceptor(counter);

        var dataContext = (DataContext)context;
        var act = () => dataContext.GetPreparedQueryCommand(
            context.From<IT326NativeJsonRow>().Where(x => x.Id == 1).Select(x => x.Data),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*SqlServerDialect*")
            .WithMessage("*native JSON column*");

        counter.Executions.Should().Be(
            0,
            "the refusal happens while the converter is resolved, before a command exists — not at execution");
    }

    /// <summary>Counts the commands that actually reach execution.</summary>
    private sealed class CommandCounter : IQueryInterceptor
    {
        private int _executions;

        public int Executions => Volatile.Read(ref _executions);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executions);
    }
}

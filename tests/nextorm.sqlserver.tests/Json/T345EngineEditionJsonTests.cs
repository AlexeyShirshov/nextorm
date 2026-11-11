using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Shared fixtures for the T345 (#345) SQL Server engine-edition capability tests. Every context here is
/// built over a placeholder connection string and these tests never open a connection.
/// </summary>
internal static class T345JsonTestSupport
{
    internal const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";
}

/// <summary>Writes a capability-matrix row to stdout and, when a path is provided, to a file as well.</summary>
internal static class T345Evidence
{
    internal const string Variable = "NEXTORM_T345_MATRIX";

    public static void Write(string message)
    {
        Console.WriteLine(message);

        var path = Environment.GetEnvironmentVariable(Variable);
        if (!string.IsNullOrEmpty(path))
            File.AppendAllText(path, message + Environment.NewLine);
    }
}

/// <summary>The document model stored in the JSON columns of these tests.</summary>
public sealed class T345JsonDoc
{
    public string? Name { get; set; }
}

/// <summary>A row whose JSON column keeps the default <see cref="JsonColumnStorage.Auto"/> storage.</summary>
[SqlTable("t345_shared_json")]
public interface IT345AutoJsonRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("data")]
    [JsonColumn]
    T345JsonDoc Data { get; set; }
}

/// <summary>Concrete row for <see cref="IT345AutoJsonRow"/>.</summary>
public sealed class T345AutoJsonRow : IT345AutoJsonRow
{
    public long Id { get; set; }

    public T345JsonDoc Data { get; set; } = new();
}

/// <summary>A row whose JSON column explicitly asks for text storage.</summary>
[SqlTable("t345_shared_json")]
public interface IT345TextJsonRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("data")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    T345JsonDoc Data { get; set; }
}

/// <summary>Concrete row for <see cref="IT345TextJsonRow"/>.</summary>
public sealed class T345TextJsonRow : IT345TextJsonRow
{
    public long Id { get; set; }

    public T345JsonDoc Data { get; set; } = new();
}

/// <summary>A row whose JSON column explicitly asks for native storage.</summary>
[SqlTable("t345_shared_json")]
public interface IT345NativeJsonRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("data")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    T345JsonDoc Data { get; set; }
}

/// <summary>Concrete row for <see cref="IT345NativeJsonRow"/>.</summary>
public sealed class T345NativeJsonRow : IT345NativeJsonRow
{
    public long Id { get; set; }

    public T345JsonDoc Data { get; set; } = new();
}

/// <summary>A row without any JSON column, used to prove preparation touches no connection.</summary>
[SqlTable("t345_plain")]
public interface IT345PlainRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }
}

/// <summary>Concrete row for <see cref="IT345PlainRow"/>.</summary>
public sealed class T345PlainRow : IT345PlainRow
{
    public long Id { get; set; }
}

#region Context types — one concrete type per declared state, because the registry is per type

/// <summary>Declared Azure SQL Database: no version, edition 5.</summary>
internal sealed class T345AzureSqlDatabaseContext : SqlServerDataContext
{
    public T345AzureSqlDatabaseContext()
        : base(SqlServerEngineEdition.SqlDatabase, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>Declared (12.0, SqlDatabase), the boxed Azure-major declaration.</summary>
internal sealed class T345MajorTwelveSqlDatabaseContext : SqlServerDataContext
{
    public T345MajorTwelveSqlDatabaseContext()
        : base(new Version(12, 0), SqlServerEngineEdition.SqlDatabase, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>The unversioned, edition-less control.</summary>
internal sealed class T345UnversionedContext : SqlServerDataContext
{
    public T345UnversionedContext()
        : base(T345JsonTestSupport.PlaceholderConnectionString, new DataContextBuilder())
    {
    }
}

/// <summary>Declared major 12 with no edition: the boxed server shape, gate closed.</summary>
internal sealed class T345MajorTwelveNoEditionContext : SqlServerDataContext
{
    public T345MajorTwelveNoEditionContext()
        : base(new Version(12, 0), T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>Declared (16.0, Enterprise): version too old and the edition grants nothing.</summary>
internal sealed class T345MajorSixteenEnterpriseContext : SqlServerDataContext
{
    public T345MajorSixteenEnterpriseContext()
        : base(new Version(16, 0), SqlServerEngineEdition.Enterprise, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>Declared (12.0, AzureSynapseServerlessSqlPoolOrMicrosoftFabric): edition 11 grants nothing.</summary>
internal sealed class T345SynapseServerlessElevenContext : SqlServerDataContext
{
    public T345SynapseServerlessElevenContext()
        : base(new Version(12, 0), SqlServerEngineEdition.AzureSynapseServerlessSqlPoolOrMicrosoftFabric, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>One type constructed twice with the same unset declaration.</summary>
internal sealed class T345RepeatedUnsetContext : SqlServerDataContext
{
    public T345RepeatedUnsetContext()
        : base(T345JsonTestSupport.PlaceholderConnectionString, new DataContextBuilder())
    {
    }
}

/// <summary>One type constructed twice with the same Azure declaration.</summary>
internal sealed class T345RepeatedAzureContext : SqlServerDataContext
{
    public T345RepeatedAzureContext()
        : base(new Version(12, 0), SqlServerEngineEdition.SqlDatabase, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>One type declared first as SQL Database, then as Managed Instance.</summary>
internal sealed class T345EditionSwapContext : SqlServerDataContext
{
    public T345EditionSwapContext(SqlServerEngineEdition engineEdition)
        : base(new Version(12, 0), engineEdition, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>One type constructed unversioned first, then as declared Azure SQL Database.</summary>
internal sealed class T345UnsetThenAzureContext : SqlServerDataContext
{
    public T345UnsetThenAzureContext()
        : base(T345JsonTestSupport.PlaceholderConnectionString, new DataContextBuilder())
    {
    }

    public T345UnsetThenAzureContext(SqlServerEngineEdition engineEdition)
        : base(new Version(12, 0), engineEdition, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>One type constructed unversioned first, then with version 17 and no edition (legacy path).</summary>
internal sealed class T345LegacyVersionMixContext : SqlServerDataContext
{
    public T345LegacyVersionMixContext()
        : base(T345JsonTestSupport.PlaceholderConnectionString, new DataContextBuilder())
    {
    }

    public T345LegacyVersionMixContext(Version serverVersion)
        : base(serverVersion, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>A type declared only as Azure SQL Managed Instance (coexistence partner).</summary>
internal sealed class T345DistinctManagedInstanceContext : SqlServerDataContext
{
    public T345DistinctManagedInstanceContext()
        : base(new Version(12, 0), SqlServerEngineEdition.AzureSqlManagedInstance, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>A type declared only as Microsoft Fabric SQL database (coexistence partner).</summary>
internal sealed class T345DistinctFabricContext : SqlServerDataContext
{
    public T345DistinctFabricContext()
        : base(SqlServerEngineEdition.MicrosoftFabricSqlDatabase, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>A type over which undefined edition values are attempted.</summary>
internal sealed class T345UndefinedEditionContext : SqlServerDataContext
{
    public T345UndefinedEditionContext(SqlServerEngineEdition engineEdition)
        : base(engineEdition, T345JsonTestSupport.PlaceholderConnectionString)
    {
    }
}

/// <summary>A context over a caller-supplied connection double, declared as Azure SQL Database.</summary>
internal sealed class T345ProbingSqlDatabaseContext(DbConnection connection)
    : SqlServerDataContext(SqlServerEngineEdition.SqlDatabase, connection);

/// <summary>A context over a caller-supplied connection double, declared as Fabric on major 12.</summary>
internal sealed class T345ProbingFabricContext(DbConnection connection)
    : SqlServerDataContext(new Version(12, 0), SqlServerEngineEdition.MicrosoftFabricSqlDatabase, connection);

/// <summary>An unversioned, edition-less context over a caller-supplied connection double.</summary>
internal sealed class T345ProbingUnversionedContext(DbConnection connection)
    : SqlServerDataContext(connection, new DataContextBuilder());

#endregion

/// <summary>
/// T345 (#345) — the declared-state capability matrix. The expectation is a literal table transcribed
/// from the frozen contract and the observation comes from the dialect the context would actually build,
/// so a wrong production rule fails the matrix instead of agreeing with itself. Both dimensions are
/// exercised through the real constructor (version, edition) and the real converter resolution, so no
/// server is involved and none is contacted.
/// <para>
/// Declared major <c>{absent,12,16,17}</c> × edition <c>{unset,1,2,3,4,5,6,8,9,11,12}</c> is the 44-pair
/// capability matrix; adding the three storage forms yields the 132-row converter matrix. Every row is
/// unit-only: no Azure/Fabric instance is contacted, and the declaration is never verified against a server.
/// </para>
/// </summary>
public class T345CapabilityMatrixTests
{
    /// <summary>
    /// Independent expectation table, transcribed by hand from the frozen contract (major ≥ 17, or edition
    /// 5/8/12) — deliberately NOT expressed with the same expression as <see cref="SqlServerDialect.SupportsJson"/>,
    /// so a wrong rule fails the matrix.
    /// </summary>
    private static readonly (int? Major, SqlServerEngineEdition? Edition, bool Supports)[] Expected =
    [
        (null, null, false), (null, SqlServerEngineEdition.PersonalOrDesktop, false), (null, SqlServerEngineEdition.Standard, false),
        (null, SqlServerEngineEdition.Enterprise, false), (null, SqlServerEngineEdition.Express, false), (null, SqlServerEngineEdition.SqlDatabase, true),
        (null, SqlServerEngineEdition.AzureSynapseAnalytics, false), (null, SqlServerEngineEdition.AzureSqlManagedInstance, true),
        (null, SqlServerEngineEdition.AzureSqlEdge, false), (null, SqlServerEngineEdition.AzureSynapseServerlessSqlPoolOrMicrosoftFabric, false),
        (null, SqlServerEngineEdition.MicrosoftFabricSqlDatabase, true),
        (12, null, false), (12, SqlServerEngineEdition.PersonalOrDesktop, false), (12, SqlServerEngineEdition.Standard, false),
        (12, SqlServerEngineEdition.Enterprise, false), (12, SqlServerEngineEdition.Express, false), (12, SqlServerEngineEdition.SqlDatabase, true),
        (12, SqlServerEngineEdition.AzureSynapseAnalytics, false), (12, SqlServerEngineEdition.AzureSqlManagedInstance, true),
        (12, SqlServerEngineEdition.AzureSqlEdge, false), (12, SqlServerEngineEdition.AzureSynapseServerlessSqlPoolOrMicrosoftFabric, false),
        (12, SqlServerEngineEdition.MicrosoftFabricSqlDatabase, true),
        (16, null, false), (16, SqlServerEngineEdition.PersonalOrDesktop, false), (16, SqlServerEngineEdition.Standard, false),
        (16, SqlServerEngineEdition.Enterprise, false), (16, SqlServerEngineEdition.Express, false), (16, SqlServerEngineEdition.SqlDatabase, true),
        (16, SqlServerEngineEdition.AzureSynapseAnalytics, false), (16, SqlServerEngineEdition.AzureSqlManagedInstance, true),
        (16, SqlServerEngineEdition.AzureSqlEdge, false), (16, SqlServerEngineEdition.AzureSynapseServerlessSqlPoolOrMicrosoftFabric, false),
        (16, SqlServerEngineEdition.MicrosoftFabricSqlDatabase, true),
        (17, null, true), (17, SqlServerEngineEdition.PersonalOrDesktop, true), (17, SqlServerEngineEdition.Standard, true),
        (17, SqlServerEngineEdition.Enterprise, true), (17, SqlServerEngineEdition.Express, true), (17, SqlServerEngineEdition.SqlDatabase, true),
        (17, SqlServerEngineEdition.AzureSynapseAnalytics, true), (17, SqlServerEngineEdition.AzureSqlManagedInstance, true),
        (17, SqlServerEngineEdition.AzureSqlEdge, true), (17, SqlServerEngineEdition.AzureSynapseServerlessSqlPoolOrMicrosoftFabric, true),
        (17, SqlServerEngineEdition.MicrosoftFabricSqlDatabase, true),
    ];

    private static readonly JsonColumnStorage[] Storages =
    [
        JsonColumnStorage.Auto,
        JsonColumnStorage.Text,
        JsonColumnStorage.Native,
    ];

    /// <summary>The 44 declared (major, edition) pairs, each carrying its own expected capability.</summary>
    public static TheoryData<int?, SqlServerEngineEdition?, bool> DeclaredPairs()
    {
        var data = new TheoryData<int?, SqlServerEngineEdition?, bool>();
        foreach (var row in Expected)
            data.Add(row.Major, row.Edition, row.Supports);

        return data;
    }

    private static Version? VersionOf(int? major) => major is { } value ? new Version(value, 0) : null;

    private static string CellId(int? major, SqlServerEngineEdition? edition, JsonColumnStorage storage)
    {
        var majorText = major?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var editionText = edition is { } value ? ((int)value).ToString(CultureInfo.InvariantCulture) : "unset";

        return $"M-{majorText}-E{editionText}-{storage}";
    }

    [Fact]
    public void ExpectationTable_ShouldCoverTheWholeDeclaredMatrix()
    {
        Expected.Should().HaveCount(44, "4 declared majors × 11 engine editions");
        Expected.Select(row => row.Major).Distinct().Should().HaveCount(4, "the table declares 4 majors");
        Expected
            .GroupBy(row => row.Major)
            .Should().OnlyContain(
                group => group.Count() == 11,
                "every declared major is paired with all 11 engine editions");
        Expected
            .Select(row => (row.Major, row.Edition))
            .Distinct()
            .Should().HaveCount(44, "no declared pair may be repeated or dropped");
    }

    [Theory]
    [MemberData(nameof(DeclaredPairs))]
    public void SupportsJson_ShouldFollowTheDeclaredVersionAndEdition(
        int? major, SqlServerEngineEdition? edition, bool expected)
    {
        var dialect = new SqlServerDialect(VersionOf(major), edition);

        dialect.SupportsJson.Should().Be(
            expected,
            "the capability is a function of the declared state only");
        dialect.NativeJsonProviderType.Should().Be(
            typeof(string),
            "the native json column is transported as text in every configuration");
    }

    /// <summary>
    /// Negative control proving the matrix can fail: on major 12 the Enterprise cell (text only) and the
    /// SQL Database cell (native) disagree. A rule of <c>major &gt;= 17</c> alone reports <c>false</c> for
    /// both, so this is exactly the cell the pre-change rule got wrong — the table distinguishes the two rules.
    /// </summary>
    [Fact]
    public void NegativeControl_EnterpriseAndSqlDatabaseOnMajorTwelve_ShouldDisagree()
    {
        var enterprise = new SqlServerDialect(new Version(12, 0), SqlServerEngineEdition.Enterprise);
        var sqlDatabase = new SqlServerDialect(new Version(12, 0), SqlServerEngineEdition.SqlDatabase);

        enterprise.SupportsJson.Should().BeFalse(
            "major 12 with the Enterprise edition has no native json type");
        sqlDatabase.SupportsJson.Should().BeTrue(
            "the declared Azure SQL Database edition has the native json type even on major 12, "
            + "which the pre-change version-only rule could not tell apart from Enterprise");
    }

    /// <summary>
    /// All 132 declared-state rows through the real converter factory: the resolved converter's provider
    /// type (or the named refusal) must match the literal expectation for every (major, edition, storage) cell.
    /// </summary>
    [Fact]
    public void DeclaredStorageRows_ShouldResolveThroughTheConverterFactory()
    {
        var rows = 0;

        foreach (var row in Expected)
        {
            var dialect = new SqlServerDialect(VersionOf(row.Major), row.Edition);
            var supports = row.Supports;
            dialect.SupportsJson.Should().Be(supports);

            foreach (var storage in Storages)
            {
                rows++;
                var id = CellId(row.Major, row.Edition, storage);
                var converter = JsonColumnConverterFactory.Create(typeof(T345JsonDoc), storage);

                if (storage == JsonColumnStorage.Native && !supports)
                {
                    var act = () => ((IJsonColumnConverter)converter).Resolve(dialect);
                    act.Should().Throw<NotSupportedException>()
                        .Which.Message.Should().Contain("SqlServerDialect").And.Contain("native JSON column");

                    T345Evidence.Write($"{id} | supports=false | storage=exception");
                    continue;
                }

                var resolved = ((IJsonColumnConverter)converter).Resolve(dialect);
                resolved.ProviderType.Should().Be(
                    typeof(string),
                    $"{id}: SQL Server transports a JSON column as a string in every configuration");

                T345Evidence.Write($"{id} | supports={supports.ToString().ToLowerInvariant()} | storage=resolved");
            }
        }

        rows.Should().Be(132, "4 declared majors × 11 editions × 3 storage forms");
    }
}

/// <summary>
/// T345 (#345) — the per-context-type declaration identity. The plan cache is keyed by the context type,
/// so a concrete type owns one immutable (version, edition) pair: repeating it is accepted, changing either
/// component is refused before the registry is mutated, and a version-only conflict keeps the exact #326
/// message. Each conflicting test owns its own concrete type, because the registry is process-wide.
/// </summary>
public class T345ContextIdentityTests
{
    [Fact]
    public void SameUnsetDeclarationTwice_ShouldBeAccepted()
    {
        using var first = new T345RepeatedUnsetContext();
        using var second = new T345RepeatedUnsetContext();

        first.Dialect.SupportsJson.Should().BeFalse();
        second.Dialect.SupportsJson.Should().BeFalse();
    }

    [Fact]
    public void SameAzureDeclarationTwice_ShouldBeAccepted()
    {
        using var first = new T345RepeatedAzureContext();
        using var second = new T345RepeatedAzureContext();

        first.Dialect.SupportsJson.Should().BeTrue();
        second.Dialect.SupportsJson.Should().BeTrue();
    }

    [Fact]
    public void ChangingTheEditionOnOneType_ShouldThrowWithBothDeclarations()
    {
        using var sqlDatabase = new T345EditionSwapContext(SqlServerEngineEdition.SqlDatabase);

        var act = () => new T345EditionSwapContext(SqlServerEngineEdition.AzureSqlManagedInstance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(T345EditionSwapContext).FullName}*")
            .WithMessage("*SqlDatabase (5)*")
            .WithMessage("*AzureSqlManagedInstance (8)*")
            .WithMessage("*derive a distinct DataContext subclass*");
    }

    [Fact]
    public void UnsetThenAzure_ShouldThrowTheEditionInvolvingMessage()
    {
        using var unversioned = new T345UnsetThenAzureContext();

        var act = () => new T345UnsetThenAzureContext(SqlServerEngineEdition.SqlDatabase);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(T345UnsetThenAzureContext).FullName}*")
            .WithMessage("*existing declaration [version: unset (null), engine edition: unset (null)]*")
            .WithMessage("*requested declaration [version: 12.0, engine edition: SqlDatabase (5)]*")
            .WithMessage("*derive a distinct DataContext subclass*");
    }

    [Fact]
    public void UnsetThenSeventeen_ShouldKeepTheLegacyVersionMessage()
    {
        using var unversioned = new T345LegacyVersionMixContext();

        var act = () => new T345LegacyVersionMixContext(new Version(17, 0));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(T345LegacyVersionMixContext)}*")
            .WithMessage("*unset (null)*")
            .WithMessage("*17.0*")
            .WithMessage("*derive a distinct DataContext subclass*");
    }

    [Fact]
    public void DistinctSubclasses_ShouldCoexistWithDifferentDeclarations()
    {
        using var managedInstance = new T345DistinctManagedInstanceContext();
        using var fabric = new T345DistinctFabricContext();

        managedInstance.Dialect.SupportsJson.Should().BeTrue();
        fabric.Dialect.SupportsJson.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(13)]
    public void UndefinedEdition_ShouldBeRejectedBeforeTheRegistryIsTouched(int undefined)
    {
        var act = () => new T345UndefinedEditionContext((SqlServerEngineEdition)undefined);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("engineEdition")
            .WithMessage("*Undefined SqlServerEngineEdition value.*");

        // No registry entry was written by the failed construction, so a valid declaration on the very same
        // type still succeeds.
        using var valid = new T345UndefinedEditionContext(SqlServerEngineEdition.SqlDatabase);
        valid.Dialect.SupportsJson.Should().BeTrue();
    }

    [Fact]
    public void EditionOverloads_ShouldRejectANullVersion()
    {
        FluentActions.Invoking(() => new SqlServerDataContext(
                (Version)null!, SqlServerEngineEdition.SqlDatabase, T345JsonTestSupport.PlaceholderConnectionString))
            .Should().Throw<ArgumentNullException>().WithParameterName("serverVersion");

        using var connection = new SqlConnection(T345JsonTestSupport.PlaceholderConnectionString);
        FluentActions.Invoking(() => new SqlServerDataContext(
                (Version)null!, SqlServerEngineEdition.SqlDatabase, connection))
            .Should().Throw<ArgumentNullException>().WithParameterName("serverVersion");
    }
}

/// <summary>
/// T345 (#345) — storage safety. Declaring an edition-derived capability must not change the existing text
/// transport: <see cref="JsonColumnStorage.Auto"/> and <see cref="JsonColumnStorage.Text"/> produce the very
/// same prepared statement and parameter metadata as the unversioned control, and an explicit
/// <see cref="JsonColumnStorage.Native"/> under a closed gate is refused by dialect name with zero executed
/// commands.
/// </summary>
public class T345StorageSafetyTests
{
    [Fact]
    public void AutoOnADeclaredAzureContext_ShouldMatchTheUnversionedControl()
    {
        using var azure = new T345MajorTwelveSqlDatabaseContext();
        using var control = new T345UnversionedContext();

        var azureShape = Prepare(azure, azure.From<IT345AutoJsonRow>().Where(x => x.Id == 1).Select(x => x.Data));
        var controlShape = Prepare(control, control.From<IT345AutoJsonRow>().Where(x => x.Id == 1).Select(x => x.Data));

        azure.Dialect.SupportsJson.Should().BeTrue("the declared edition opens the capability");
        control.Dialect.SupportsJson.Should().BeFalse("the unversioned control keeps the historical text mapping");

        azureShape.Sql.Should().Be(
            controlShape.Sql,
            "Auto must not silently flip an existing nvarchar(max)-backed [JsonColumn] to native storage");
        azureShape.Parameters.Should().Equal(
            controlShape.Parameters,
            "the bound parameter metadata is part of the unchanged text transport");
    }

    [Fact]
    public void AutoAndTextOnADeclaredAzureContext_ShouldResolveIdentically()
    {
        using var azure = new T345MajorTwelveSqlDatabaseContext();

        var autoShape = Prepare(azure, azure.From<IT345AutoJsonRow>().Where(x => x.Id == 1).Select(x => x.Data));
        var textShape = Prepare(azure, azure.From<IT345TextJsonRow>().Where(x => x.Id == 1).Select(x => x.Data));

        autoShape.Sql.Should().Be(textShape.Sql);
        autoShape.Parameters.Should().Equal(textShape.Parameters);
    }

    [Fact]
    public void NativeOnTheSynapseServerlessEdition_ShouldRefuseWithDialectNameAndZeroCommands()
    {
        AssertNativeRefusal(new T345SynapseServerlessElevenContext(), "edition 11 is deliberately incapable");
    }

    [Fact]
    public void NativeOnEnterpriseSixteen_ShouldRefuseWithDialectNameAndZeroCommands()
    {
        AssertNativeRefusal(new T345MajorSixteenEnterpriseContext(), "major 16 is below the version gate");
    }

    [Fact]
    public void NativeOnBoxedMajorTwelveWithoutAnEdition_ShouldRefuseWithDialectNameAndZeroCommands()
    {
        AssertNativeRefusal(new T345MajorTwelveNoEditionContext(), "an unset edition grants no capability");
    }

    private static void AssertNativeRefusal(SqlServerDataContext context, string because)
    {
        using var owned = context;
        var counter = new CommandCounter();
        owned.AddInterceptor(counter);

        var act = () => owned.GetPreparedQueryCommand(
            owned.From<IT345NativeJsonRow>().Where(x => x.Id == 1).Select(x => x.Data),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*SqlServerDialect*")
            .WithMessage("*native JSON column*");

        counter.Executions.Should().Be(
            0,
            $"the refusal happens while the converter is resolved, before a command exists — {because}");
    }

    private static (string Sql, string[] Parameters) Prepare<TResult>(DataContext context, QueryCommand<TResult> query)
    {
        var prepared = (DbPreparedQueryCommand<TResult>)context.GetPreparedQueryCommand(
            query,
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

        var parameters = prepared.DbCommand.Parameters
            .Cast<DbParameter>()
            .Select(static parameter => string.Create(
                CultureInfo.InvariantCulture,
                $"{parameter.ParameterName}:{parameter.DbType}:{parameter.Size}:{parameter.Direction}"))
            .ToArray();

        return (prepared.DbCommand.CommandText, parameters);
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

/// <summary>
/// T345 (#345) — no probing. Construction, capability evaluation and preparation must succeed with a
/// connection that rejects every open, command creation and transaction start, and the dialect must carry
/// no connection-typed state and no probing member at all.
/// </summary>
public class T345NoProbingTests
{
    [Fact]
    public void PreparingOnADeclaredAzureContext_ShouldNotTouchTheConnection()
    {
        var connection = new ProbingConnection();
        using var context = new T345ProbingSqlDatabaseContext(connection);

        context.Dialect.SupportsJson.Should().BeTrue("the declaration alone opens the capability");

        var prepared = context.GetPreparedQueryCommand(
            context.From<IT345PlainRow>().Where(x => x.Id == 1).Select(x => x.Id),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

        prepared.Should().NotBeNull();
        AssertUntouched(connection);
    }

    [Fact]
    public void PreparingOnAVersionAndEditionDeclaration_ShouldNotTouchTheConnection()
    {
        var connection = new ProbingConnection();
        using var context = new T345ProbingFabricContext(connection);

        context.Dialect.SupportsJson.Should().BeTrue("the declared Fabric edition opens the capability");

        var prepared = context.GetPreparedQueryCommand(
            context.From<IT345PlainRow>().Where(x => x.Id == 1).Select(x => x.Id),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

        prepared.Should().NotBeNull();
        AssertUntouched(connection);
    }

    [Fact]
    public void PreparingOnTheUnversionedContext_ShouldNotTouchTheConnection()
    {
        var connection = new ProbingConnection();
        using var context = new T345ProbingUnversionedContext(connection);

        context.Dialect.SupportsJson.Should().BeFalse("the unversioned context keeps the historical text mapping");

        var prepared = context.GetPreparedQueryCommand(
            context.From<IT345PlainRow>().Where(x => x.Id == 1).Select(x => x.Id),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

        prepared.Should().NotBeNull();
        AssertUntouched(connection);
    }

    [Fact]
    public void SqlServerDialect_ShouldDeclareNoConnectionStateAndNoProbingMember()
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var forbiddenTypes = new[] { typeof(DbConnection), typeof(IDbConnection), typeof(SqlConnection) };

        foreach (var field in typeof(SqlServerDialect).GetFields(Declared))
        {
            forbiddenTypes.Should().NotContain(
                type => type.IsAssignableFrom(field.FieldType),
                $"the dialect field '{field.Name}' must not hold a connection");
        }

        foreach (var property in typeof(SqlServerDialect).GetProperties(Declared))
        {
            forbiddenTypes.Should().NotContain(
                type => type.IsAssignableFrom(property.PropertyType),
                $"the dialect property '{property.Name}' must not expose a connection");
        }

        typeof(SqlServerDialect).GetMembers(Declared)
            .Select(static member => member.Name)
            .Should().NotContain(
                name => name.Contains("Probe", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Detect", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("ServerProperty", StringComparison.OrdinalIgnoreCase),
                "the dialect must not have a probing or detection member");
    }

    private static void AssertUntouched(ProbingConnection connection)
    {
        connection.Opens.Should().Be(0, "preparation must not open the connection");
        connection.OpenAsyncs.Should().Be(0, "preparation must not open the connection asynchronously");
        connection.CommandsCreated.Should().Be(0, "preparation must not create a command");
        connection.TransactionsStarted.Should().Be(0, "preparation must not begin a transaction");
    }

    /// <summary>A connection that refuses every interaction and counts the attempts.</summary>
    private sealed class ProbingConnection : DbConnection
    {
        private int _opens;
        private int _openAsyncs;
        private int _commands;
        private int _transactions;

        public int Opens => Volatile.Read(ref _opens);

        public int OpenAsyncs => Volatile.Read(ref _openAsyncs);

        public int CommandsCreated => Volatile.Read(ref _commands);

        public int TransactionsStarted => Volatile.Read(ref _transactions);

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => "t345";

        public override string DataSource => "t345";

        public override string ServerVersion => "12.0.0.0";

        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName)
            => throw new InvalidOperationException("no probing during preparation");

        public override void Close()
        {
        }

        public override void Open()
        {
            Interlocked.Increment(ref _opens);
            throw new InvalidOperationException("no probing during preparation");
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _openAsyncs);
            throw new InvalidOperationException("no probing during preparation");
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            Interlocked.Increment(ref _transactions);
            throw new InvalidOperationException("no probing during preparation");
        }

        protected override DbCommand CreateDbCommand()
        {
            Interlocked.Increment(ref _commands);
            throw new InvalidOperationException("no probing during preparation");
        }
    }
}

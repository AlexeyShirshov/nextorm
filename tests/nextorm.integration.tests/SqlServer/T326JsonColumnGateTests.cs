using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>A row of the gate table: the same shape as the storage table, its own name.</summary>
[SqlTable(T326JsonColumnGateTests.Table)]
public interface IT326JsonGateRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("j")]
    [JsonColumn(Storage = JsonColumnStorage.Native)]
    JsonElement? J { get; set; }
}

/// <summary>Concrete row for <see cref="IT326JsonGateRow"/>.</summary>
public sealed class T326JsonGateRow : IT326JsonGateRow
{
    public long Id { get; set; }

    public JsonElement? J { get; set; }
}

/// <summary>A context declared for SQL Server 2022 (major 16, gate closed).</summary>
internal sealed class T326V16LiveContext(string connectionString)
    : SqlServerDataContext(new Version(16, 0), connectionString);

/// <summary>
/// T326 (#326) — C1: the version gate itself, on both servers. A <see cref="JsonColumnStorage.Native"/>
/// mapping on a context declared for major 16 is refused while the converter is resolved, so no command is
/// created or executed; a context declared for major 17 accepts it (on 2022 that acceptance is asserted as
/// configuration only — the server there has no <c>json</c> type and nothing is executed).
/// </summary>
public sealed class T326JsonColumnGateTests : IDisposable
{
    internal const string Table = "t326_json_gate";

    private const string ObjectRoot = "{\"name\":\"alpha\",\"inner\":{\"k\":1,\"flag\":true}}";

    private readonly int _major;

    public T326JsonColumnGateTests()
    {
        if (!SqlServerContainer.IsAvailable)
        {
            if (ProviderSkipPolicy.IsOptedOut("sqlserver"))
                Assert.Skip($"Provider 'sqlserver' is explicitly opted out via {ProviderSkipPolicy.EnvironmentVariable}.");

            SqlServerContainer.IsAvailable.Should().BeTrue(
                "provider 'sqlserver' is unavailable ({0}); the gate test fails closed rather than skipping silently",
                SqlServerContainer.Failure);
        }

        using var probe = new T326V16LiveContext(SqlServerContainer.ConnectionString);
        _major = ReadMajor(probe);
    }

    public void Dispose()
    {
        if (_major < 17)
            return;

        using var context = new T326V17LiveContext(SqlServerContainer.ConnectionString);
        Execute(context, $"drop table if exists {Table}");
    }

    [Fact]
    public void NativeOnSixteen_ShouldBeRefusedWithDialectNameAndZeroExecutions()
    {
        using var context = new T326V16LiveContext(SqlServerContainer.ConnectionString);
        var counter = new CommandCounter();
        context.AddInterceptor(counter);

        var act = () => ((DataContext)context).GetPreparedQueryCommand(
            context.From<T326JsonGateRow>().Where(x => x.Id == 1).Select(x => x.J),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*SqlServerDialect*")
            .WithMessage("*native JSON column*");

        counter.Executions.Should().Be(0, "the refusal is raised while resolving the converter, before any command exists");

        Report($"T326GATE case=C1_refuse_16 major={_major} dialect=SqlServerDialect exception=NotSupportedException executions={counter.Executions} outcome=refused");
    }

    [Fact]
    public void NativeOnSeventeen_ShouldBeAcceptedAndExecuteOn2025Only()
    {
        using var context = new T326V17LiveContext(SqlServerContainer.ConnectionString);
        var counter = new CommandCounter();
        context.AddInterceptor(counter);

        context.Dialect.SupportsJson.Should().BeTrue("an explicit major 17 opens the native storage gate");
        context.Dialect.NativeJsonProviderType.Should().Be(typeof(string));

        // Configuration-level acceptance: preparing a Native query resolves the text converter and must not
        // throw, on either server. On 2022 nothing is executed — the server there cannot store json at all.
        var prepared = ((DataContext)context).GetPreparedQueryCommand(
            context.From<T326JsonGateRow>().Where(x => x.Id == 1).Select(x => x.J),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);
        prepared.Should().NotBeNull();
        counter.Executions.Should().Be(0, "preparation alone does not execute");

        if (_major < 17)
        {
            Report($"T326GATE case=C1_accept_17_config major={_major} dialect_SupportsJson=True executions=0 outcome=configuration_only");
            return;
        }

        Execute(context, $"if object_id('{Table}') is null create table {Table} (id bigint not null primary key, j json null)");
        Execute(context, $"delete from {Table}");

        using var document = JsonDocument.Parse(ObjectRoot);
        context.CreateInsertBuilder<T326JsonGateRow>()
            .Values(new T326JsonGateRow { Id = 1, J = document.RootElement.Clone() })
            .Insert();

        var row = context.From<T326JsonGateRow>().ToList().Single(x => x.Id == 1);
        row.J.Should().NotBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(row.J!.Value.GetRawText()), JsonNode.Parse(ObjectRoot)).Should().BeTrue();

        Report($"T326GATE case=C1_accept_17_execute major={_major} dialect_SupportsJson=True executions={counter.Executions} outcome=accepted_and_executed");
    }

    private static int ReadMajor(IDataContext context)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand("select cast(serverproperty('ProductMajorVersion') as int)");
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static void Report(string message) => T326Evidence.Write(message);

    /// <summary>Counts the commands that actually reach execution.</summary>
    private sealed class CommandCounter : IQueryInterceptor
    {
        private int _executions;

        public int Executions => Volatile.Read(ref _executions);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executions);
    }
}

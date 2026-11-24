using System.Diagnostics;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Diagnostics;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

[Collection("SqlServer")]
public sealed class T406BatchOutputIntoTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsVisiblePermanentRowsOrOnlyRegisteredCaptureInOneExecution(bool permanent)
    {
        _ = _sut.DataProvider; // Seed outside the measurement window.
        using var connection = new SqlConnection(SqlServerContainer.ConnectionString);
        connection.Open();
        using IDataContext ctx = new SqlServerDataContext(connection, new DataContextBuilder());
        void ExecuteSetup(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        const string table = "t406_capture";
        const int inserted = 40602;
        var maximum = inserted;
        ExecuteSetup("create table t406_capture (id int not null); insert into t406_capture values (40601), (40603);");
        try
        {
            using var observed = new CommandObserver(connection);
            connection.StatisticsEnabled = true;
            connection.ResetStatistics();
            var mutation = ctx.CreateInsertBuilder<ISimpleEntity>()
                .Value(x => x.Id, inserted)
                .Returning(x => new CaptureRow { Id = x.Id });
            var batch = ctx.CreateBatchBuilder();
            if (permanent)
            {
                batch.OutputInto(mutation.OutputInto(table));
                batch.AddQuery(ctx.From(table)
                    .Where(row => row.GetInt32("id") <= maximum)
                    .OrderBy(row => row.GetInt32("id"))
                    .Select(row => new CaptureRow { Id = row.GetInt32("id") }));
            }
            else
            {
                batch.OutputIntoTableVariable(mutation, "@capture", "id int");
                batch.AddQuery(batch.FromOutput<CaptureRow>("@capture")
                    .Where(row => row.Id <= maximum)
                    .OrderBy(row => row.Id)
                    .Select(row => new CaptureRow { Id = row.Id }));
            }

            var rows = batch.Execute().Read<CaptureRow>().Select(row => row.Id).ToArray();
            var roundtrips = (long)connection.RetrieveStatistics()["ServerRoundtrips"]!;
            output.WriteLine($"permanent={permanent}; commands={observed.Commands.Count}; server roundtrips={roundtrips}; rows={string.Join(',', rows)}");
            observed.Commands.Should().ContainSingle()
                .Which.Should().Contain("output inserted.id into").And.Contain("; select");
            roundtrips.Should().Be(1, "the real write and typed read complete within one execution");
            rows.Should().Equal(permanent ? [40601, inserted] : [inserted]);
        }
        finally
        {
            ExecuteSetup("drop table t406_capture; delete from simple_entity where id = 40602;");
        }
    }

    private sealed class CaptureRow
    {
        public int Id { get; init; }
    }

    private sealed class CommandObserver
        : IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private readonly SqlConnection _connection;
        private readonly IDisposable _listeners;
        private IDisposable? _subscription;

        public CommandObserver(SqlConnection connection)
        {
            _connection = connection;
            _listeners = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public List<string> Commands { get; } = [];

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "SqlClientDiagnosticListener")
                _subscription = listener.Subscribe(this, name => name == SqlClientCommandBefore.Name);
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is SqlClientCommandBefore started
                && ReferenceEquals(started.Command.Connection, _connection))
                Commands.Add(started.Command.CommandText);
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listeners.Dispose();
            _subscription?.Dispose();
        }
    }
}

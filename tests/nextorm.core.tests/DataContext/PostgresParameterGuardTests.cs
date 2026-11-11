using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #327 D7: the core-half of the PostgreSQL final-value guard. The provider suite owns the raw command
/// and mutation-render cases; this file covers the guard API itself over both supported shapes (a driver
/// parameter collection and the planner's parameter list) and the refresh behaviour of a cached prepared
/// command that also carries an immutable occurrence template. The guard must run after every refresh,
/// before any command execution, and it must never echo the rejected value.
/// </summary>
public class PostgresParameterGuardTests
{
    private const string NulValue = "marker\0secret";

    [Fact]
    public void IsPostgres_Should_Recognize_Only_The_PostgreSQL_Policy()
    {
        PostgresParameterGuard.IsPostgres(SqlStringLiteralPolicy.Postgres).Should().BeTrue();

        foreach (var policy in new[]
                 {
                     SqlStringLiteralPolicy.Legacy,
                     SqlStringLiteralPolicy.Sqlite,
                     SqlStringLiteralPolicy.SqlServer,
                     SqlStringLiteralPolicy.MySql,
                     SqlStringLiteralPolicy.ClickHouse,
                 })
        {
            PostgresParameterGuard.IsPostgres(policy).Should().BeFalse();
        }

        PostgresParameterGuard.IsPostgres(null).Should().BeFalse("a command built outside the planner has no policy");
    }

    [Fact]
    public void ParameterList_With_Nul_String_Should_Be_Rejected_Without_Echo()
    {
        var parameters = new[] { new Parameter("p0", NulValue) };

        var exception = FluentActions.Invoking(() =>
                PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, parameters))
            .Should().Throw<NotSupportedException>().Which;

        AssertRejectionMessage(exception);
    }

    [Fact]
    public void ParameterList_With_Embedded_Nul_Should_Be_Rejected()
    {
        var parameters = new[] { new Parameter("p0", "a\0b") };

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, parameters))
            .Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*U+0000*");
    }

    [Fact]
    public void ParameterList_With_Default_Char_Should_Be_Rejected()
    {
        var parameters = new[] { new Parameter("p0", default(char)) };

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, parameters))
            .Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*U+0000*");
    }

    [Fact]
    public void ParameterList_With_Null_Or_DbNull_Should_Be_Accepted()
    {
        var parameters = new[] { new Parameter("p0", null), new Parameter("p1", DBNull.Value) };

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, parameters))
            .Should().NotThrow();
    }

    [Fact]
    public void ParameterList_With_Every_Safe_C0_Should_Be_Accepted()
    {
        var parameters = new List<Parameter>();
        for (var code = 1; code <= 0x1f; code++)
            parameters.Add(new Parameter("p" + code, ((char)code).ToString()));

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, parameters))
            .Should().NotThrow();
    }

    [Fact]
    public void ParameterList_With_Non_Text_Values_Should_Be_Accepted()
    {
        var parameters = new[]
        {
            new Parameter("p0", Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")),
            new Parameter("p1", 42),
            new Parameter("p2", 1.5m),
            new Parameter("p3", new byte[] { 0, 1, 2 }),
        };

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, parameters))
            .Should().NotThrow("only textual final values can carry a NUL");
    }

    [Fact]
    public void ParameterList_On_Non_Postgres_Policy_Should_Be_Inert()
    {
        var parameters = new[] { new Parameter("p0", NulValue) };

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.MySql, parameters))
            .Should().NotThrow("the MySQL family transports a NUL as a hex payload");
        FluentActions.Invoking(() => PostgresParameterGuard.Validate(null, parameters))
            .Should().NotThrow();
    }

    [Fact]
    public void ParameterCollection_With_Nul_String_Should_Be_Rejected_Without_Echo()
    {
        var collection = Collection(new StubParameter("p0", NulValue));

        var exception = FluentActions.Invoking(() =>
                PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, collection))
            .Should().Throw<NotSupportedException>().Which;

        AssertRejectionMessage(exception);
    }

    [Fact]
    public void ParameterCollection_With_Default_Char_Should_Be_Rejected()
    {
        var collection = Collection(new StubParameter("p0", default(char)));

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, collection))
            .Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*U+0000*");
    }

    [Fact]
    public void ParameterCollection_With_Null_And_Non_Text_Should_Be_Accepted()
    {
        var collection = Collection(
            new StubParameter("p0", null),
            new StubParameter("p1", DBNull.Value),
            new StubParameter("p2", 7),
            new StubParameter("p3", Guid.Empty));

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, collection))
            .Should().NotThrow();
    }

    [Fact]
    public void ParameterCollection_On_Non_Postgres_Policy_Should_Be_Inert()
    {
        var collection = Collection(new StubParameter("p0", NulValue));

        FluentActions.Invoking(() => PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Sqlite, collection))
            .Should().NotThrow();
    }

    [Fact]
    public void Cached_Prepared_Command_Should_Guard_Every_Refresh_And_Recover_On_A_Safe_Value()
    {
        var command = new StubCommand("select @norm_p0");
        var prepared = Prepared(command);

        // First checkout: a safe final value passes and is refreshed into the canonical parameter.
        Bind(prepared, "safe").Should().BeSameAs(command);
        command.CommandText.Should().Be("select @norm_p0", "PostgreSQL never specializes the prepared text");
        command.Parameters.Count.Should().Be(1);
        command.Parameters[0].Value.Should().Be("safe");

        // Second checkout: the same cached command is refreshed with a NUL-bearing value. The guard sees
        // the refreshed value and throws before any execution.
        var exception = FluentActions.Invoking(() => Bind(prepared, NulValue))
            .Should().Throw<NotSupportedException>().Which;
        AssertRejectionMessage(exception);
        command.ExecuteNonQueryCalls.Should().Be(0, "the guard runs before the command can be executed");
        command.CommandText.Should().Be("select @norm_p0", "a rejected execution must not rewrite the statement");

        // Third checkout: the command is still usable for a legal value.
        Bind(prepared, "safe again").Should().BeSameAs(command);
        command.Parameters.Count.Should().Be(1);
        command.Parameters[0].Value.Should().Be("safe again");
        command.ExecuteNonQueryCalls.Should().Be(0);
    }

    [Fact]
    public void Template_Bearing_Command_Should_Keep_The_Template_Reference_Across_A_Rejected_Refresh()
    {
        var prepared = Prepared(new StubCommand("select @norm_p0"));
        var template = prepared.ParameterTemplate;

        Bind(prepared, "safe");
        FluentActions.Invoking(() => Bind(prepared, NulValue)).Should().Throw<NotSupportedException>();
        Bind(prepared, "safe again");

        prepared.ParameterTemplate.Should().BeSameAs(template, "the immutable template is never rebuilt or mutated");
        template!.StatementText.Should().Be("select @norm_p0");
    }

    private static void AssertRejectionMessage(NotSupportedException exception)
    {
        exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
        exception.Message.Should().NotContain("marker").And.NotContain("secret",
            "the rejection must not echo the supplied value");
    }

    private static ListDbParameterCollection Collection(params DbParameter[] parameters)
    {
        var collection = new ListDbParameterCollection();
        foreach (var parameter in parameters)
            collection.Add(parameter);
        return collection;
    }

    private static DbPreparedQueryCommand<object> Prepared(DbCommand command)
    {
        var prepared = new DbPreparedQueryCommand<object>(
            command,
            mapDelegate: null,
            new PreparedCommandOptions(Sql: "select @norm_p0"));

        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("norm_p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("select ");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, "@norm_p0");

        prepared.SetParameterTemplate(recorder.Finalize("select @norm_p0"), SqlStringLiteralPolicy.Postgres);
        return prepared;
    }

    private static DbCommand Bind(DbPreparedQueryCommand<object> prepared, object? value) =>
        prepared.GetDbCommand(
            (ReadOnlySpan<object?>)new[] { value },
            static (name, parameterValue) => new StubParameter(name, parameterValue ?? DBNull.Value),
            new StubConnection());

    private sealed class StubParameter(string name, object? value) : DbParameter
    {
        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        [AllowNull]
        public override object Value { get; set; } = value;

        public override bool SourceColumnNullMapping { get; set; }

        public override int Size { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class StubCommand(string commandText) : DbCommand
    {
        private readonly ListDbParameterCollection _parameters = new();

        public int ExecuteNonQueryCalls { get; private set; }

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = null!;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        [AllowNull]
        protected override DbTransaction DbTransaction { get; set; } = null!;

        [AllowNull]
        public override string CommandText { get; set; } = commandText;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbParameter CreateDbParameter() => new StubParameter("command-minted", null);

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery()
        {
            ExecuteNonQueryCalls++;
            return 0;
        }

        public override object? ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class StubConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open() => throw new NotSupportedException();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class ListDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value!);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string parameterName)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].ParameterName, parameterName, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}

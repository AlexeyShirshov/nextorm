using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// D5: the PostgreSQL final-value guard. A value that becomes NUL-bearing only at execution time (a
/// captured/refreshed parameter, an explicit raw command or a mutation render) must raise the same
/// deterministic <see cref="NotSupportedException"/> as the translation-time literal check — naming
/// PostgreSQL and <c>U+0000</c>, never echoing the value — before the driver sees it. Other C0 values
/// and <see langword="null"/> stay legal, and no other provider policy is affected.
/// </summary>
public class PostgresNulParameterGuardTests
{
    private const string NulValue = "secret\0payload";

    [Fact]
    public void PreparedCommand_NulString_ThrowsBeforeExecution()
    {
        var command = new StubCommand();
        var prepared = Prepared(command, SqlStringLiteralPolicy.Postgres);

        var act = () => Bind(prepared, NulValue);

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("PostgreSQL").And.Contain("U+0000");
        exception.Message.Should().NotContain("secret").And.NotContain("payload");
        command.ExecuteNonQueryCalls.Should().Be(0, "the guard runs before the command can be executed");
    }

    [Fact]
    public void PreparedCommand_SecondCheckoutWithNul_Throws()
    {
        var command = new StubCommand();
        var prepared = Prepared(command, SqlStringLiteralPolicy.Postgres);

        var first = Bind(prepared, "safe");
        first.Should().BeSameAs(command);

        // The cached command is checked out again with a new final value: the stored plan is refreshed
        // in place and the guard must see the refreshed value, not only the cold one.
        var act = () => Bind(prepared, NulValue);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*PostgreSQL*U+0000*");
        command.DriverParameters[0].Value.Should().Be(NulValue, "the refresh happens before the guard, so the rejected value never reaches the driver");
    }

    [Fact]
    public void PreparedCommand_DefaultChar_Throws()
    {
        var prepared = Prepared(new StubCommand(), SqlStringLiteralPolicy.Postgres);

        var act = () => Bind(prepared, default(char));

        act.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*U+0000*");
    }

    [Theory]
    [InlineData("\u0001")]
    [InlineData("tab\tnewline\n")]
    [InlineData("")]
    public void PreparedCommand_SafeC0_DoesNotThrow(string value)
    {
        var prepared = Prepared(new StubCommand(), SqlStringLiteralPolicy.Postgres);

        var act = () => Bind(prepared, value);

        act.Should().NotThrow();
    }

    [Fact]
    public void PreparedCommand_Null_DoesNotThrow()
    {
        var command = new StubCommand();
        var prepared = Prepared(command, SqlStringLiteralPolicy.Postgres);

        var act = () => Bind(prepared, null);

        act.Should().NotThrow();
        command.DriverParameters[0].Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public void NonPostgresPolicy_LeavesTheSameValueAlone()
    {
        var prepared = Prepared(new StubCommand(), SqlStringLiteralPolicy.Sqlite);

        var act = () => Bind(prepared, NulValue);

        act.Should().NotThrow("the guard is inert for every non-PostgreSQL policy");
    }

    [Fact]
    public void MutationRender_NulValue_ThrowsBeforeSqlIsReturned()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateUpdateBuilder<IComplexEntity>()
            .Set(x => x.String, NulValue)
            .Where(x => x.Id == 1)
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*U+0000*");
    }

    [Fact]
    public void MutationRender_SafeC0_StillRenders()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateUpdateBuilder<IComplexEntity>()
            .Set(x => x.String, "tab\tnewline\n")
            .Where(x => x.Id == 1)
            .ToSql();

        sql.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ExecuteRaw_NulValue_ThrowsBeforeExecution()
    {
        var connection = new StubConnection();
        using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

        var act = () => ctx.ExecuteRaw("select @p0", new ProcedureParameter("p0", NulValue));

        act.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*U+0000*");
        connection.LastCommand!.ExecuteNonQueryCalls.Should().Be(0);
        connection.LastCommand.DriverParameters.Count.Should().Be(1);
    }

    [Fact]
    public void ExecuteRaw_SafeC0_ProceedsPastTheGuard()
    {
        var connection = new StubConnection();
        using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

        // The stub reader is empty, so a successful open returns an empty result; what matters is that
        // the guard did not reject a legal C0 value.
        using var result = ctx.ExecuteRaw("select @p0", new ProcedureParameter("p0", "\u0001\t\n"));

        connection.LastCommand.Should().NotBeNull();
    }

    private static DbPreparedQueryCommand<object> Prepared(DbCommand command, SqlStringLiteralPolicy policy)
    {
        var prepared = new DbPreparedQueryCommand<object>(
            command,
            mapDelegate: null,
            new PreparedCommandOptions(Sql: "select @norm_p0"));
        prepared.SetParameterTemplate(template: null, policy);
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

    private sealed class StubCommand : DbCommand
    {
        private readonly ListDbParameterCollection _parameters = new();

        public DbParameterCollection DriverParameters => _parameters;

        public int ExecuteNonQueryCalls { get; private set; }

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = null!;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        [AllowNull]
        protected override DbTransaction DbTransaction { get; set; } = null!;

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbParameter CreateDbParameter() => new StubParameter(string.Empty, null);

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery()
        {
            ExecuteNonQueryCalls++;
            return 0;
        }

        public override object ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new StubReader();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    private sealed class StubConnection : DbConnection
    {
        private StubCommand? _command;

        public StubCommand? LastCommand => _command;

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => _command = new StubCommand();
    }

    /// <summary>Minimal empty reader: <c>ExecuteRaw</c> only needs a reader to own.</summary>
    private sealed class StubReader : DbDataReader
    {
        public override int Depth => 0;
        public override int FieldCount => 0;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override object this[int ordinal] => throw new IndexOutOfRangeException();
        public override object this[string name] => throw new IndexOutOfRangeException();
        public override bool GetBoolean(int ordinal) => throw new IndexOutOfRangeException();
        public override byte GetByte(int ordinal) => throw new IndexOutOfRangeException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new IndexOutOfRangeException();
        public override char GetChar(int ordinal) => throw new IndexOutOfRangeException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new IndexOutOfRangeException();
        public override string GetDataTypeName(int ordinal) => throw new IndexOutOfRangeException();
        public override DateTime GetDateTime(int ordinal) => throw new IndexOutOfRangeException();
        public override decimal GetDecimal(int ordinal) => throw new IndexOutOfRangeException();
        public override double GetDouble(int ordinal) => throw new IndexOutOfRangeException();
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override Type GetFieldType(int ordinal) => throw new IndexOutOfRangeException();
        public override float GetFloat(int ordinal) => throw new IndexOutOfRangeException();
        public override Guid GetGuid(int ordinal) => throw new IndexOutOfRangeException();
        public override short GetInt16(int ordinal) => throw new IndexOutOfRangeException();
        public override int GetInt32(int ordinal) => throw new IndexOutOfRangeException();
        public override long GetInt64(int ordinal) => throw new IndexOutOfRangeException();
        public override string GetName(int ordinal) => throw new IndexOutOfRangeException();
        public override int GetOrdinal(string name) => throw new IndexOutOfRangeException();
        public override string GetString(int ordinal) => throw new IndexOutOfRangeException();
        public override object GetValue(int ordinal) => throw new IndexOutOfRangeException();
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => throw new IndexOutOfRangeException();
        public override bool NextResult() => false;
        public override bool Read() => false;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    /// <summary>List-backed parameter collection used by the stub command.</summary>
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

        public override bool Contains(string? value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string? parameterName) =>
            _items.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.Ordinal));

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string? parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                _items.RemoveAt(index);
        }

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string? parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string? parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                _items[index] = value;
            else
                _items.Add(value);
        }
    }
}

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the "exactly one parameter factory" invariant of
/// <see cref="DbPreparedQueryCommand{TResult}.GetDbCommandCore"/> and, through it, the delegate each
/// public/internal <c>GetDbCommand</c> overload chooses: the public 2-arg path must forward the
/// caller's command-unaware <see cref="Func{T1, T2, TResult}"/> untouched (no command passed, no
/// command-aware factory invoked), while the internal execution path must use the command-aware
/// factory.
/// </summary>
public class DbPreparedQueryCommandFactoryTests
{
    [Fact]
    public void Core_BothFactoriesNull_Throws()
    {
        var prepared = Prepared(out _);

        var act = () => prepared.GetDbCommandCore(
            ReadOnlySpan<object?>.Empty, null, null, new StubConnection(), null);

        act.Should().Throw<ArgumentException>().WithMessage("*Exactly one*");
    }

    [Fact]
    public void Core_BothFactoriesSupplied_Throws()
    {
        var prepared = Prepared(out _);

        Func<DbCommand, string, object?, DbParameter> aware = (_, _, _) => new MarkerParameter("aware");
        Func<string, object?, DbParameter> legacy = (_, _) => new MarkerParameter("legacy");

        var act = () => prepared.GetDbCommandCore(
            ReadOnlySpan<object?>.Empty, aware, legacy, new StubConnection(), null);

        act.Should().Throw<ArgumentException>().WithMessage("*Exactly one*");
    }

    [Fact]
    public void PublicOverload_BindsThroughTheSuppliedLegacyFactory_NotTheCommand()
    {
        var prepared = Prepared(out var command);
        var marker = new MarkerParameter("legacy-marker");
        var legacyCalls = 0;

        DbParameter Legacy(string name, object? value)
        {
            legacyCalls++;
            marker.ParameterName = name;
            marker.Value = value;
            return marker;
        }

        var result = prepared.GetDbCommand((ReadOnlySpan<object?>)new object?[] { 42 }, Legacy, new StubConnection());

        result.Should().BeSameAs(command);
        legacyCalls.Should().Be(1);
        command.LastAdded.Should().BeSameAs(marker);
        marker.Value.Should().Be(42);
        // The public path must not touch a command-aware path: the executing command never mints.
        command.CreateParameterCalls.Should().Be(0);
    }

    [Fact]
    public void InternalOverload_UsesTheCommandAwareFactory_WithTheExecutingCommand()
    {
        var prepared = Prepared(out var command);
        var marker = new MarkerParameter("aware-marker");
        var awareCalls = 0;
        DbCommand? observedCommand = null;

        DbParameter Aware(DbCommand cmd, string name, object? value)
        {
            awareCalls++;
            observedCommand = cmd;
            marker.ParameterName = name;
            marker.Value = value;
            return marker;
        }

        var result = prepared.GetDbCommand((ReadOnlySpan<object?>)new object?[] { 42 }, Aware, new StubConnection(), null);

        result.Should().BeSameAs(command);
        awareCalls.Should().Be(1);
        observedCommand.Should().BeSameAs(command);
        command.LastAdded.Should().BeSameAs(marker);
        // The command-aware factory is delegated to: the command itself is not asked to mint.
        command.CreateParameterCalls.Should().Be(0);
    }

    private static DbPreparedQueryCommand<int> Prepared(out StubCommand command)
    {
        command = new StubCommand();
        return new DbPreparedQueryCommand<int>(command, null, new PreparedCommandOptions());
    }

    private sealed class MarkerParameter(string name) : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class StubCommand : DbCommand
    {
        private readonly ListDbParameterCollection _parameters = new();

        public int CreateParameterCalls { get; private set; }

        public DbParameter? LastAdded => _parameters.LastAdded;

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

        protected override DbParameter CreateDbParameter()
        {
            CreateParameterCalls++;
            return new MarkerParameter("command-minted");
        }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object ExecuteScalar() => throw new NotSupportedException();

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

    /// <summary>
    /// Minimal list-backed parameter collection: the core resolves parameter positions through
    /// <see cref="IndexOf(string)"/> and adds new parameters through <see cref="Add(object)"/>.
    /// </summary>
    private sealed class ListDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public DbParameter? LastAdded { get; private set; }

        public override int Count => _items.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            LastAdded = (DbParameter)value;
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value!);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string? value) => _items.Any(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string? parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string? parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string? parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string? parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
                Add(value);
            else
                _items[index] = value;
        }
    }
}

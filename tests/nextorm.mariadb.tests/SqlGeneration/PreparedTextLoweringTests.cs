using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.MariaDb.Tests.SqlGeneration;

/// <summary>
/// D4c/D4d: a prepared MySQL-family statement lowers each planner-owned text binding whose final value
/// is a string/char into a charset-introduced UTF-8 hex literal at checkout, removes exactly those
/// bindings from the driver collection, and restores the canonical text and complete parameter set on
/// the next execution. Every test inspects the command the context would execute (a recording
/// connection, no database): the SQL text and the parameter list are both asserted.
/// <para>
/// MariaDB mirror of <c>tests/nextorm.mysql.tests/SqlGeneration/PreparedTextLoweringTests.cs</c>:
/// <see cref="MariaDbDialect"/> resolves to the same MySQL literal policy, so the whole contract has to
/// hold for <see cref="MariaDbDataContext"/> as well.
/// </para>
/// </summary>
[Collection("DataContextCache clear")] // purges the process-wide plan store; see the definition in RawSourceBindingSqlGenerationTests
public class PreparedTextLoweringTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool cache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, cache, CancellationToken.None);

    private static DbCommand Checkout<T>(DbPreparedQueryCommand<T> prepared)
        => prepared.GetDbCommand(
            ReadOnlySpan<object?>.Empty,
            NoRuntimeParameter,
            prepared.DbCommand.Connection ?? throw new InvalidOperationException("The prepared command has no connection."),
            null);

    private static DbParameter NoRuntimeParameter(string name, object? value)
        => throw new InvalidOperationException($"Unexpected runtime parameter '{name}'.");

    private static DbParameter[] Parameters(DbCommand command)
        => command.Parameters.Cast<DbParameter>().ToArray();

    private static string[] Names(DbCommand command)
        => Parameters(command).Select(p => p.ParameterName).ToArray();

    private static object?[] Values(DbCommand command)
        => Parameters(command).Select(p => p.Value).ToArray();

    private static QueryCommand<long> TextEquals(EntityBuilder<IComplexEntity> e, string? value)
        => e.Where(x => x.String == value).Select(x => x.Id);

    private static QueryCommand<long> TextEqualsTwice(EntityBuilder<IComplexEntity> e, string? value)
        => e.Where(x => x.String == value).Where(x => x.String == value).Select(x => x.Id);

    private static QueryCommand<long> TextAndThreshold(EntityBuilder<IComplexEntity> e, string? value, int threshold)
        => e.Where(x => x.String == value && x.Id > threshold).Select(x => x.Id);

    [Fact]
    public void CapturedString_ShouldBecomeAHexLiteral_AndLoseItsDriverParameter()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var captured = "it's";
        var prepared = Prepare(ctx, TextEquals(ctx.From<IComplexEntity>(), captured), cache: true);
        prepared.ParameterTemplate.Should().NotBeNull("an ORM-rendered statement carries its occurrence template");

        var command = Checkout(prepared);

        Normalize(command.CommandText).Should()
            .Contain("_utf8mb4 X'69742773'", "the final string value is transported as UTF-8 hex")
            .And.NotContain("@captured", "a lowered occurrence no longer refers to a driver parameter");
        command.Parameters.Count.Should().Be(0, "the only binding was fully lowered");
        prepared.DbCommandParams.Count.Should().Be(1, "the canonical parameter set is never shortened");
    }

    [Fact]
    public void SecondExecution_ShouldReRenderTheChangedValue_WithoutAStaleLiteral()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var first = Prepare(ctx, TextEquals(e, "alpha"), cache: true);
        Checkout(first);
        Normalize(first.DbCommand.CommandText).Should().Contain("_utf8mb4 X'616c706861'");

        var second = Prepare(ctx, TextEquals(e, "beta"), cache: true);
        ReferenceEquals(first, second).Should().BeTrue("the captured value is not part of the plan key");

        Checkout(second);
        Normalize(second.DbCommand.CommandText).Should()
            .Contain("_utf8mb4 X'62657461'", "the second execution re-renders the current closure value")
            .And.NotContain("_utf8mb4 X'616c706861'", "the previous execution's literal is never reused");
        second.DbCommand.Parameters.Count.Should().Be(0);
    }

    [Fact]
    public void TextThenNullThenText_ShouldKeepThePlaceholderAndParameterForNull_AndLowerAgainAfterwards()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        string? value = "alpha";
        var first = Prepare(ctx, TextEquals(e, value), cache: true);
        Checkout(first);
        Normalize(first.DbCommand.CommandText).Should().Contain("_utf8mb4 X'616c706861'");

        value = null;
        var second = Prepare(ctx, TextEquals(e, value), cache: true);
        var nullCommand = Checkout(second);
        Normalize(nullCommand.CommandText).Should()
            .Contain("@value", "a null value stays a bound database null")
            .And.NotContain("_utf8mb4 X'");
        nullCommand.Parameters.Cast<DbParameter>().Should().ContainSingle()
            .Which.Value.Should().Be(DBNull.Value, "SQL NULL is never encoded as an empty string");

        value = "gamma";
        var third = Prepare(ctx, TextEquals(e, value), cache: true);
        var thirdCommand = Checkout(third);
        Normalize(thirdCommand.CommandText).Should()
            .Contain("_utf8mb4 X'67616d6d61'")
            .And.NotContain("@value");
        thirdCommand.Parameters.Count.Should().Be(0);
        third.DbCommandParams.Count.Should().Be(1, "the canonical binding identity survives the null round trip");
    }

    [Fact]
    public void RepeatedCapture_ShouldLowerEveryOccurrence_AndRemoveTheBindingOnce()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var value = "abc";
        var prepared = Prepare(ctx, TextEqualsTwice(ctx.From<IComplexEntity>(), value), cache: true);
        var command = Checkout(prepared);

        var sql = Normalize(command.CommandText);
        sql.Should().Contain("_utf8mb4 X'616263'");
        // Each null-safe equality predicate renders three occurrences of the same binding (the null
        // guard in both directions plus the comparison), so two predicates carry six occurrences.
        sql.Split("_utf8mb4 X'616263'").Length.Should().Be(7, "all six occurrences of both predicates are lowered");
        sql.Should().NotContain("@value");
        command.Parameters.Count.Should().Be(0, "one binding is removed once, however many occurrences it has");
    }

    [Fact]
    public void MixedStatement_ShouldLowerOnlyTheText_AndKeepTheRemainingParametersInOrder()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var text = "abc";
        var threshold = 5;
        var guid = Guid.Parse("aabbccdd-1122-3344-5566-778899aabbcc");
        var prepared = Prepare(
            ctx,
            ctx.From<IComplexEntity>()
                .Where(x => x.String == text && x.Id > threshold)
                .Select(x => new { x.Id, G = guid }),
            cache: true);

        var canonical = Parameters(prepared.DbCommand);
        var textParameter = canonical.Single(p => p.Value is "abc");
        var expected = canonical.Where(p => !ReferenceEquals(p, textParameter)).ToArray();
        var expectedValues = expected.Select(p => p.Value).ToArray();

        var command = Checkout(prepared);

        Normalize(command.CommandText).Should()
            .Contain("_utf8mb4 X'616263'")
            .And.NotContain("@text");
        command.Parameters.Cast<DbParameter>().Should().Equal(
            expected,
            "every other binding keeps its canonical order, name and type");
        Values(command).Should().Equal(expectedValues);
    }

    [Fact]
    public void NonMySqlProvider_ShouldKeepTheCommandTextAndParametersByteIdentical()
    {
        var connection = new RecordingConnection();
        using var ctx = new SqlServerDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var captured = "it's";
        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            TextEquals(ctx.From<IComplexEntity>(), captured), false, false, CancellationToken.None);

        var beforeText = prepared.DbCommand.CommandText;
        var beforeNames = Names(prepared.DbCommand);
        var beforeValues = Values(prepared.DbCommand);

        var command = prepared.GetDbCommand(
            ReadOnlySpan<object?>.Empty, NoRuntimeParameter, connection, null);

        command.CommandText.Should().Be(beforeText, "a non-MySQL policy never specializes the statement");
        Names(command).Should().Equal(beforeNames);
        Values(command).Should().Equal(beforeValues);
        Values(command).Should().ContainSingle()
            .Which.Should().Be("it's", "the captured value keeps its usual parameter transport");
    }

    [Fact]
    public void CallerOwnedText_ShouldKeepItsCommandTextAndBindingsUnchanged()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var prepared = (DbPreparedQueryCommand<long>)ctx.From<IComplexEntity>()
            .Select(x => x.Id)
            .PrepareFromSql("select id from complex_entity where somestring = @value", new { value = "it's" },
                CancellationToken.None);

        prepared.ParameterTemplate.Should().BeNull("caller-owned text has no planner-owned emission");

        var command = Checkout(prepared);

        Normalize(command.CommandText).Should().Be(
            "select id from complex_entity where somestring = @value",
            "caller-owned SQL is never rewritten or specialized");
        Names(command).Should().Equal("value");
        Values(command).Should().Equal("it's");
    }

    [Fact]
    public void FastRefresh_ShouldBindTheCurrentValuesBeforeSpecialization()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var first = Prepare(ctx, TextAndThreshold(e, "a", 1), cache: true);
        Checkout(first);

        var second = Prepare(ctx, TextAndThreshold(e, "b", 2), cache: true);
        ReferenceEquals(first, second).Should().BeTrue("the same shape reuses the cached plan");

        var command = Checkout(second);
        Normalize(command.CommandText).Should().Contain("_utf8mb4 X'62'");
        Values(command).Should().Equal(new object?[] { 2 }, "the fast recipe refreshes the int binding before specialization");
        Names(command).Should().Equal("threshold");
    }

    [Fact]
    public void FallbackRefresh_ShouldBindTheCurrentValuesBeforeSpecialization()
    {
        var connection = new RecordingConnection();
        using var ctx = new MariaDbDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var first = Prepare(ctx, TextAndThreshold(e, "a", 1), cache: true);
        first.SetParamRecipe(null);
        Checkout(first);

        var second = Prepare(ctx, TextAndThreshold(e, "c", 3), cache: true);
        ReferenceEquals(first, second).Should().BeTrue();

        var command = Checkout(second);
        Normalize(command.CommandText).Should().Contain("_utf8mb4 X'63'");
        Values(command).Should().Equal(new object?[] { 3 }, "the fallback extraction writes the canonical bindings before specialization");
        Names(command).Should().Equal("threshold");
    }

    /// <summary>A connection that never opens: it only records the command the context builds.</summary>
    private sealed class RecordingConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Open;

        public RecordingCommand? Command { get; private set; }

        [AllowNull]
        public override string ConnectionString
        {
            get => string.Empty;
            set { }
        }

        public override string Database => "nextorm";

        public override string DataSource => "recording";

        public override string ServerVersion => "8.0.0";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => Command = new RecordingCommand(this);
    }

    /// <summary>The command the context binds and would execute; it records the text and parameters.</summary>
    private sealed class RecordingCommand : DbCommand
    {
        private readonly RecordingParameterCollection _parameters = new();

        public RecordingCommand(DbConnection connection) => Connection = connection;

        protected override DbConnection? DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar() => null;

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new MySqlParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => throw new NotSupportedException();
    }

    private sealed class RecordingParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new();

        public override int Count => _items.Count;

        public override object SyncRoot => ((System.Collections.ICollection)_items).SyncRoot;

        public override int Add(object? value)
        {
            _items.Add((DbParameter)value!);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value!);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object? value) => _items.Contains((DbParameter)value!);

        public override bool Contains(string value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);

        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object? value) => _items.IndexOf((DbParameter)value!);

        public override int IndexOf(string parameterName)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].ParameterName, parameterName, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public override void Insert(int index, object? value) => _items.Insert(index, (DbParameter)value!);

        public override void Remove(object? value) => _items.Remove((DbParameter)value!);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}

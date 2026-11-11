using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.MySql;
using NextORM.Postgres;
using Npgsql;

namespace NextORM.Core.Tests;

/// <summary>
/// #327 D7: command/template reuse invariants of the D4 specialization seam. The immutable template is
/// never mutated; every checkout restores the canonical text and the complete canonical parameter set
/// before refreshing values; a lowering provider (MySQL/MariaDB) composes the current value while a
/// non-lowering provider receives exactly the existing SQL text; and the shared plan cache stays
/// enabled across alternating executions.
/// </summary>
public class CommandTemplateReuseTests
{
    private const string OriginalStatement = "select * from t where s = @norm_p0 and id = @norm_p1";

    // ------------------------------------------------------------------------------------------------
    // Pure template composition
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void Compose_Should_Never_Mutate_The_Statement_And_Produce_Independent_Texts()
    {
        var template = TwoBindingTemplate();

        var first = template.Compose(new string?[] { "_utf8mb4 X'41'", null });
        var second = template.Compose(new string?[] { "_utf8mb4 X'42'", null });

        first.Should().Be("select * from t where s = _utf8mb4 X'41' and id = @norm_p1");
        second.Should().Be("select * from t where s = _utf8mb4 X'42' and id = @norm_p1");
        first.Should().NotBe(second, "each composition writes its own buffer");

        template.StatementText.Should().Be(OriginalStatement);
        template.ReconstructOriginalText().Should().Be(OriginalStatement);
    }

    [Fact]
    public void ReconstructOriginalText_Should_Return_The_Original_Statement_After_Any_Composition()
    {
        var template = TwoBindingTemplate();

        template.Compose(new[] { "_utf8mb4 X'6974'", null });
        template.Compose(new string?[template.OccurrenceCount]);
        template.Compose(static (in SqlParameterOccurrence _) => "_utf8mb4 X''");

        template.ReconstructOriginalText().Should().Be(OriginalStatement);
        template.StatementText.Should().Be(OriginalStatement);
    }

    [Fact]
    public void Compose_Should_Reproduce_A_Rewound_Wrapper_Text_And_Stay_Stable_Across_Compositions()
    {
        // #327 DEF-04: a renderer emits a fragment, truncates its builder back and re-appends the
        // fragment inside a wrapper. The template must reproduce that emitted text exactly (the
        // relocated occurrence sits behind the wrapper prefix), and composing twice must not accumulate.
        var recorder = new SqlParameterTemplateRecorder();
        var textBinding = recorder.RegisterBinding("norm_p0", SqlParameterBindingOrigin.Captured);
        var builder = new StringBuilder();
        recorder.AppendText("select ");
        builder.Append("select ");

        var mark = recorder.MarkAt(recorder.Length);
        recorder.RecordPlaceholder(textBinding, SqlStringLiteralRole.Value, "@norm_p0");
        var fragment = recorder.RewindTo(mark);

        var wrapper = "toInt32(@norm_p0)";
        SqlParameterFragmentRelocation.AppendComposed(
            builder,
            recorder,
            wrapper,
            [new SqlParameterTrackedFragment("@norm_p0", null, fragment)]);

        var emitted = "select " + wrapper;
        builder.ToString().Should().Be(emitted, "the builder sees one copy of the wrapper");
        var template = recorder.Finalize(emitted);

        template.Occurrences[0].Start.Should().Be(emitted.IndexOf("@norm_p0", StringComparison.Ordinal));

        var first = template.Compose(new string?[template.OccurrenceCount]);
        var second = template.Compose(new string?[template.OccurrenceCount]);

        first.Should().Be(emitted);
        second.Should().Be(first, "composition never accumulates wrapper text");
        template.StatementText.Should().Be(emitted);
    }

    // ------------------------------------------------------------------------------------------------
    // Command-level reuse across checkouts
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void Alternating_Safe_And_Hostile_Values_Should_Compose_Each_Checkout_And_Restore_Canonical_State()
    {
        var command = new StubCommand(OriginalStatement);
        var prepared = PreparedTwoBindingCommand(command);
        var template = prepared.ParameterTemplate;
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        // Checkout 1: safe text value.
        Checkout(prepared, "safe", guid);
        command.CommandText.Should().Be(
            "select * from t where s = " + HexLiteral("safe") + " and id = @norm_p1");
        AssertSingleRemainingGuidParameter(prepared, command, guid);

        // Checkout 2: hostile value. The canonical text and both parameters must have been restored
        // first, otherwise the refresh and the second specialization could not resolve the slots.
        Checkout(prepared, "it's \\ ", guid);
        command.CommandText.Should().Be(
            "select * from t where s = " + HexLiteral("it's \\ ") + " and id = @norm_p1");
        AssertSingleRemainingGuidParameter(prepared, command, guid);

        // Checkout 3: safe value again, after a fully specialized execution.
        Checkout(prepared, "safe again", guid);
        command.CommandText.Should().Be(
            "select * from t where s = " + HexLiteral("safe again") + " and id = @norm_p1");
        AssertSingleRemainingGuidParameter(prepared, command, guid);

        // Checkout 4: a null text value keeps its bound null and its placeholder; the driver-visible
        // statement is then exactly the canonical one.
        Checkout(prepared, null, guid);
        command.CommandText.Should().Be(OriginalStatement, "a null value is never encoded as an empty string");
        command.Parameters.Count.Should().Be(2);
        command.Parameters[0].Value.Should().Be(DBNull.Value);
        command.Parameters[1].Value.Should().Be(guid);

        prepared.ParameterTemplate.Should().BeSameAs(template);
        prepared.ParameterTemplate!.StatementText.Should().Be(OriginalStatement);
        prepared.DbCommandParams.Count.Should().Be(2, "the canonical pool is never shortened");
    }

    [Theory]
    [InlineData('a', "_utf8mb4 X'61'")]
    [InlineData("", "_utf8mb4 X''")]
    [InlineData("\0", "_utf8mb4 X'00'")]
    public void Char_And_Empty_Text_Should_Use_The_Provider_Transport(object value, string expectedLiteral)
    {
        var command = new StubCommand(OriginalStatement);
        var prepared = PreparedTwoBindingCommand(command);

        Checkout(prepared, value, Guid.Empty);

        command.CommandText.Should().StartWith("select * from t where s = " + expectedLiteral);
    }

    [Fact]
    public void Default_Char_Should_Lower_As_Its_Utf8_Byte()
    {
        var command = new StubCommand(OriginalStatement);
        var prepared = PreparedTwoBindingCommand(command);

        Checkout(prepared, default(char), Guid.Empty);

        command.CommandText.Should().StartWith("select * from t where s = _utf8mb4 X'00'");
    }

    [Fact]
    public void Template_Reference_And_Bindings_Should_Stay_Identical_Across_Three_Checkouts()
    {
        var command = new StubCommand(OriginalStatement);
        var prepared = PreparedTwoBindingCommand(command);
        var template = prepared.ParameterTemplate;
        var bindings = template!.Bindings.ToArray();

        Checkout(prepared, "safe", Guid.Empty);
        Checkout(prepared, "it's; --", Guid.Empty);
        Checkout(prepared, "safe", Guid.Empty);

        prepared.ParameterTemplate.Should().BeSameAs(template);
        template.Bindings.ToArray().Should().Equal(bindings);
        template.BindingCount.Should().Be(2);
    }

    // ------------------------------------------------------------------------------------------------
    // Real provider contexts: cache invariants and non-lowering byte identity
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void MySQL_Composed_Checkouts_Should_Not_Disable_The_Plan_Cache()
    {
        using var ctx = new MySqlDataContext(
            "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm",
            new DataContextBuilder());
        using var connection = new MySqlConnection("Server=localhost");
        var captured = "safe";

        var command = ctx.From<LiteralEntity>().Where(x => x.Name == captured).Select(x => x.Id);
        command.Cache.Should().BeTrue("the command starts with the default caching policy");

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        var canonicalText = prepared.DbCommand.CommandText;
        canonicalText.Should().Contain("@captured", "a captured value uses the planner-owned placeholder");

        void CachedCheckout(string current)
        {
            captured = current;
            var hit = ctx.GetPreparedQueryCommand(
                command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            hit.Should().BeSameAs(prepared, "every subsequent execution is a cache hit");
            prepared.DbCommandParams[0].Value.Should().Be(current, "the cached-hit refresh writes the current value");

            CheckoutWithoutRuntimeParameters(prepared, connection);
        }

        CachedCheckout("safe");
        prepared.DbCommand.CommandText.Should().Contain(HexLiteral("safe"));
        prepared.DbCommand.Parameters.Count.Should().Be(0, "the lowered binding is absent from the driver collection");

        CachedCheckout("it's; --");
        prepared.DbCommand.CommandText.Should().Contain(HexLiteral("it's; --"));

        CachedCheckout("safe");
        prepared.DbCommand.CommandText.Should().Contain(HexLiteral("safe"));

        command.Cache.Should().BeTrue("specializing a parameter must never mutate the sticky cache flag");
        prepared.DbCommandParams.Count.Should().Be(1);
    }

    [Fact]
    public void Postgres_Non_Lowering_Checkouts_Should_Receive_Exactly_The_Existing_Sql_Text()
    {
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var connection = new NpgsqlConnection("Host=localhost");
        var captured = "safe";

        var command = ctx.From<LiteralEntity>().Where(x => x.Name == captured).Select(x => x.Id);
        command.Cache.Should().BeTrue();

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        var canonicalText = prepared.DbCommand.CommandText;
        canonicalText.Should().Contain("@captured");

        void CachedCheckout(string current)
        {
            captured = current;
            var hit = ctx.GetPreparedQueryCommand(
                command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            hit.Should().BeSameAs(prepared, "every subsequent execution is a cache hit");
            prepared.DbCommandParams[0].Value.Should().Be(current);

            CheckoutWithoutRuntimeParameters(prepared, connection);
            prepared.DbCommand.CommandText.Should().Be(canonicalText, "PostgreSQL never specializes the statement");
            prepared.DbCommand.Parameters.Count.Should().Be(1);
            prepared.DbCommand.Parameters[0].Value.Should().Be(current);
        }

        CachedCheckout("safe");
        CachedCheckout("it's; --");
        CachedCheckout("safe");

        command.Cache.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------------

    private sealed class LiteralEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    private static SqlParameterTemplate TwoBindingTemplate()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var text = recorder.RegisterBinding("norm_p0", SqlParameterBindingOrigin.Captured);
        var id = recorder.RegisterBinding("norm_p1", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("select * from t where s = ");
        recorder.RecordPlaceholder(text, SqlStringLiteralRole.Value, "@norm_p0");
        recorder.AppendText(" and id = ");
        recorder.RecordPlaceholder(id, SqlStringLiteralRole.Value, "@norm_p1");
        return recorder.Finalize(OriginalStatement);
    }

    private static DbPreparedQueryCommand<object> PreparedTwoBindingCommand(DbCommand command)
    {
        var prepared = new DbPreparedQueryCommand<object>(
            command,
            mapDelegate: null,
            new PreparedCommandOptions(Sql: OriginalStatement));
        prepared.SetParameterTemplate(TwoBindingTemplate(), SqlStringLiteralPolicy.MySql);
        return prepared;
    }

    private static void Checkout(DbPreparedQueryCommand<object> prepared, object? text, object? id) =>
        prepared.GetDbCommand(
            (ReadOnlySpan<object?>)new[] { text, id },
            static (name, value) => new StubParameter(name, value ?? DBNull.Value),
            new StubConnection());

    private static void CheckoutWithoutRuntimeParameters(DbPreparedQueryCommand<int> prepared, DbConnection connection) =>
        prepared.GetDbCommand(
            ReadOnlySpan<object?>.Empty,
            static (command, name, parameterValue) => command.CreateParameter(),
            connection,
            transaction: null);

    private static string HexLiteral(string value) =>
        "_utf8mb4 X'" + Convert.ToHexString(Encoding.UTF8.GetBytes(value)).ToLower(CultureInfo.InvariantCulture) + "'";

    private static void AssertSingleRemainingGuidParameter(
        DbPreparedQueryCommand<object> prepared,
        StubCommand command,
        Guid guid)
    {
        command.Parameters.Count.Should().Be(1, "the lowered text binding is absent, the non-text binding remains");
        command.Parameters[0].Should().BeSameAs(prepared.DbCommandParams[1],
            "the original parameter object, order and type are preserved");
        command.Parameters[0].Value.Should().Be(guid);
        prepared.DbCommandParams.Count.Should().Be(2, "the canonical pool keeps both bindings");
    }

    private sealed class StubParameter(string name, object? value) : DbParameter
    {
        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override DbType DbType { get; set; } = DbType.Object;

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

        public override int ExecuteNonQuery() => 0;

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

        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

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

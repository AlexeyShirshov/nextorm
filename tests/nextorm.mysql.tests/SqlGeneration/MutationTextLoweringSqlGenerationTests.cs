using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.MySql.Tests;

/// <summary>The entity whose text/char/Guid values the lowering matrix writes.</summary>
[SqlTable("lowering_entity")]
public sealed class LoweringEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("text")]
    public string? Text { get; set; }

    [Column("letter")]
    public char Letter { get; set; }

    [Column("token")]
    public Guid Token { get; set; }
}

/// <summary>A converter that turns the model string into a provider string and counts its invocations.</summary>
public sealed class CountingTextConverter : ValueConverter<string, string>
{
    public static int Calls;

    public override string? ConvertToProvider(string? model)
    {
        Calls++;
        return model;
    }

    public override string? ConvertFromProvider(string? provider) => provider;
}

/// <summary>A converter whose provider value is not text, so the lowering must leave it bound.</summary>
public sealed class TextToIntConverter : ValueConverter<string, int>
{
    public override int ConvertToProvider(string? model)
        => model is null ? 0 : int.Parse(model, System.Globalization.CultureInfo.InvariantCulture);

    public override string? ConvertFromProvider(int provider)
        => provider.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Entity with converter-backed text columns.</summary>
[SqlTable("lowering_converter_entity")]
public sealed class LoweringConverterEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("converted")]
    [ValueConverter(typeof(CountingTextConverter))]
    public string? Converted { get; set; }

    [Column("converted_int")]
    [ValueConverter(typeof(TextToIntConverter))]
    public string? ConvertedInt { get; set; }
}

/// <summary>Entity whose key is text, so the key form of UPDATE/DELETE exercises the lowering.</summary>
[SqlTable("lowering_string_key_entity")]
public sealed class StringKeyEntity
{
    [Key]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>
/// D4g: an ad-hoc or batch MySQL-family mutation renders a planner-owned text value as a
/// charset-introduced UTF-8 hex literal and omits its binding from the driver projection, while every
/// other value keeps its placeholder, name, order and type. Every test inspects the <see cref="DbCommand"/>
/// the context would execute (a recording connection, no database): the SQL text and the parameter list
/// are both asserted.
/// </summary>
public class MutationTextLoweringSqlGenerationTests
{
    private static readonly Guid SampleGuid = new("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static RecordingCommand Capture(Action<MySqlDataContext> act)
    {
        var connection = new RecordingConnection();
        using var ctx = new MySqlDataContext(connection, new DataContextBuilder());
        act(ctx);
        return connection.Command ?? throw new InvalidOperationException("The mutation did not create a command.");
    }

    private static string[] Names(RecordingCommand command)
    {
        var names = new string[command.Parameters.Count];
        for (var i = 0; i < names.Length; i++)
            names[i] = command.Parameters[i].ParameterName!;

        return names;
    }

    private static object?[] Values(RecordingCommand command)
    {
        var values = new object?[command.Parameters.Count];
        for (var i = 0; i < values.Length; i++)
            values[i] = command.Parameters[i].Value;

        return values;
    }

    [Fact]
    public void Insert_SingleRow_ShouldLowerTextAndCharAndKeepTheRemainingBindings()
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Values(new LoweringEntity { Id = 1, Text = "it's", Letter = 'a', Token = SampleGuid })
            .Insert());

        Normalize(command.CommandText).Should().Be(
            "insert into lowering_entity (id, text, letter, token) "
            + "values (@p0, _utf8mb4 X'69742773', _utf8mb4 X'61', @p3)",
            "only the text and char values become literals; the int and Guid keep their placeholders");
        Names(command).Should().Equal("p0", "p3");
        Values(command).Should().Equal(1, SampleGuid);
    }

    [Fact]
    public void Insert_MultiRow_ShouldLowerEveryEmittedValueIndependently()
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Values([
                new LoweringEntity { Id = 1, Text = "a", Letter = 'b', Token = SampleGuid },
                new LoweringEntity { Id = 2, Text = null, Letter = 'c', Token = SampleGuid },
            ])
            .Insert());

        Normalize(command.CommandText).Should().Be(
            "insert into lowering_entity (id, text, letter, token) "
            + "values (@p0, _utf8mb4 X'61', _utf8mb4 X'62', @p3), (@p4, @p5, _utf8mb4 X'63', @p7)");
        Names(command).Should().Equal("p0", "p3", "p4", "p5", "p7");
        Values(command).Should().Equal(1, SampleGuid, 2, DBNull.Value, SampleGuid);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("it's", "69742773")]
    [InlineData("a\\b", "615c62")]
    [InlineData("a\\", "615c")]
    [InlineData("'\\", "275c")]
    [InlineData("\0", "00")]
    [InlineData("\t", "09")]
    [InlineData("\n", "0a")]
    [InlineData("\u001a", "1a")]
    [InlineData("привет", "d0bfd180d0b8d0b2d0b5d182")]
    [InlineData("😀", "f09f9880")]
    public void Insert_TextValue_ShouldLowerToUtf8Hex(string value, string expectedHex)
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Value(x => x.Text, value)
            .Insert());

        Normalize(command.CommandText).Should().Be(
            $"insert into lowering_entity (text) values (_utf8mb4 X'{expectedHex}')",
            "the literal is the lowercase UTF-8 hex of the value and is independent of the escaping mode");
        command.Parameters.Count.Should().Be(0, "the binding was replaced by a literal");
    }

    [Fact]
    public void Insert_EmptyString_ShouldRenderAnEmptyHexLiteral()
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Value(x => x.Text, string.Empty)
            .Insert());

        Normalize(command.CommandText).Should().Contain("values (_utf8mb4 X'')");
        command.Parameters.Count.Should().Be(0);
    }

    [Fact]
    public void Insert_Char_ShouldRenderOneCharacter()
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Value(x => x.Letter, 'a')
            .Insert());

        Normalize(command.CommandText).Should().Contain("values (_utf8mb4 X'61')");
        command.Parameters.Count.Should().Be(0);
    }

    [Fact]
    public void Insert_DefaultChar_ShouldRenderTheNulByte()
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Value(x => x.Letter, default(char))
            .Insert());

        Normalize(command.CommandText).Should().Contain("values (_utf8mb4 X'00')",
            "default(char) is U+0000: a one-character text value, never a NULL and never an empty string");
        command.Parameters.Count.Should().Be(0);
    }

    [Fact]
    public void Insert_NullText_ShouldKeepThePlaceholderAndBindNull()
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Values(new LoweringEntity { Id = 1, Text = null, Letter = 'a', Token = SampleGuid })
            .Insert());

        Normalize(command.CommandText).Should().Contain("values (@p0, @p1, _utf8mb4 X'61', @p3)",
            "a NULL keeps its bound placeholder instead of becoming an empty literal");
        Normalize(command.CommandText).Should().NotContain("_utf8mb4 X''");
        Names(command).Should().Equal("p0", "p1", "p3");
        Values(command).Should().Equal(1, DBNull.Value, SampleGuid);
    }

    [Fact]
    public void Insert_Guid_ShouldKeepThePlaceholderAndTheGuidBinding()
    {
        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringEntity>()
            .Value(x => x.Token, SampleGuid)
            .Insert());

        Normalize(command.CommandText).Should().Be("insert into lowering_entity (token) values (@p0)");
        Names(command).Should().Equal("p0");
        Values(command).Should().Equal(SampleGuid);
    }

    [Fact]
    public void Insert_ConvertedText_ShouldLowerAndConvertOnce()
    {
        CountingTextConverter.Calls = 0;

        var command = Capture(ctx => ctx.CreateInsertBuilder<LoweringConverterEntity>()
            .Values(new LoweringConverterEntity { Id = 1, Converted = "xy", ConvertedInt = "7" })
            .Insert());

        Normalize(command.CommandText).Should().Be(
            "insert into lowering_converter_entity (id, converted, converted_int) "
            + "values (@p0, _utf8mb4 X'7879', @p2)",
            "a converter producing text lowers; one producing a non-text value keeps the placeholder");
        Names(command).Should().Equal("p0", "p2");
        Values(command).Should().Equal(1, 7);
        CountingTextConverter.Calls.Should().Be(1, "the final value is resolved exactly once per binding");
    }

    [Fact]
    public void Insert_SecondBuildWithAChangedValue_ShouldReRenderTheLiteral()
    {
        var connection = new RecordingConnection();
        using var ctx = new MySqlDataContext(connection, new DataContextBuilder());

        ctx.CreateInsertBuilder<LoweringEntity>().Value(x => x.Text, "a").Insert();
        var first = Normalize(connection.Command!.CommandText);

        ctx.CreateInsertBuilder<LoweringEntity>().Value(x => x.Text, "b").Insert();
        var second = Normalize(connection.Command!.CommandText);

        first.Should().Contain("_utf8mb4 X'61'");
        second.Should().Contain("_utf8mb4 X'62'").And.NotContain("_utf8mb4 X'61'",
            "a second command is rendered from scratch: no literal is reused");
    }

    [Fact]
    public void Update_EntityForm_ShouldLowerTheTextKeyAndKeepTheAssignmentPlaceholder()
    {
        var command = Capture(ctx => ctx.Update(new StringKeyEntity { Code = "k1", Name = "n" }));

        Normalize(command.CommandText).Should()
            .Contain("where code = _utf8mb4 X'6b31'", "the key value is emitted by the mutation builder");
        Names(command).Should().NotContain("p1");
        Values(command).Should().Equal("n");
    }

    [Fact]
    public void Update_AssignmentAndPredicate_ShouldKeepTheirPlaceholders()
    {
        var code = "k1";
        var command = Capture(ctx => ctx.CreateUpdateBuilder<StringKeyEntity>()
            .Set(x => x.Name, "n")
            .Where(x => x.Code == code)
            .Update());

        Normalize(command.CommandText).Should()
            .Contain("set name = @p0")
            .And.Contain("code = @code")
            .And.NotContain("_utf8mb4",
                "the SET list and the WHERE predicate are rendered by the planner, outside SqlMutationBuilder.Make*");
        Names(command).Should().Equal("p0", "code");
        Values(command).Should().Equal("n", "k1");
    }

    [Fact]
    public void Delete_EntityForm_ShouldLowerTheTextKey()
    {
        var command = Capture(ctx => ctx.Delete(new StringKeyEntity { Code = "k1" }));

        Normalize(command.CommandText).Should().Be("delete from lowering_string_key_entity where code = _utf8mb4 X'6b31'");
        command.Parameters.Count.Should().Be(0);
    }

    [Fact]
    public void Delete_Predicate_ShouldKeepThePlaceholder()
    {
        var code = "k1";
        var command = Capture(ctx => ctx.CreateDeleteBuilder<StringKeyEntity>()
            .Where(x => x.Code == code)
            .Delete());

        Normalize(command.CommandText).Should()
            .StartWith("delete from lowering_string_key_entity where ")
            .And.Contain("code = @code")
            .And.NotContain("_utf8mb4",
                "the predicate placeholder is emitted by the planner, not by MakeDelete");
        Names(command).Should().Equal("code");
        Values(command).Should().Equal("k1");
    }

    [Fact]
    public void Merge_ShouldLowerEveryTextValue()
    {
        var command = Capture(ctx => ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5, Total = 9 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge());

        Normalize(command.CommandText).Should().Be(
            "insert into merge_entity (id, name, age) values (@p0, _utf8mb4 X'61', @p2) "
            + "on duplicate key update name = values(name), age = values(age)");
        Names(command).Should().Equal("p0", "p2");
        Values(command).Should().Equal(1L, 5);
    }

    [Fact]
    public void Merge_MultiRow_ShouldLowerEachRow()
    {
        var command = Capture(ctx => ctx.CreateMergeBuilder<IMergeEntity>()
            .Using([
                new MergeEntity { Id = 1, Name = "a", Age = 1 },
                new MergeEntity { Id = 2, Name = null, Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge());

        Normalize(command.CommandText).Should().Contain(
            "values (@p0, _utf8mb4 X'61', @p2), (@p3, @p4, @p5)");
        Names(command).Should().Equal("p0", "p2", "p3", "p4", "p5");
    }

    [Fact]
    public void Batch_InsertStep_ShouldLowerThroughTheBatchRender()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = Normalize(ctx.CreateBatchBuilder()
            .Insert(ctx.CreateInsertBuilder<LoweringEntity>().Value(x => x.Text, "a"))
            .AddQuery(ctx.From<LoweringEntity>().Select(x => x.Id))
            .ToSql());

        sql.Split("; ")[0].Should().Contain("values (_utf8mb4 X'61')",
            "a batch step is rendered by the same builder with the batch render kind");
    }

    [Fact]
    public void BatchRenderKind_ShouldLowerAndProjectTheParameters()
    {
        using var ctx = MySqlTestContext.CreateMySql();
        var command = (InsertCommand)ctx.CreateInsertBuilder<LoweringEntity>()
            .Values(new LoweringEntity { Id = 1, Text = "a", Letter = 'b', Token = SampleGuid })
            .BuildBatchCommand();

        var (sql, parameters) = SqlMutationBuilder.MakeInsert(
            MySqlDialect.Instance,
            quoteIdentifiers: false,
            namingConvention: null,
            command,
            KeywordCase.Lower,
            parameterProvider: new DefaultParameterProvider(),
            renderKind: MutationRenderKind.Batch);

        Normalize(sql).Should().Contain("values (@p0, _utf8mb4 X'61', _utf8mb4 X'62', @p3)");
        parameters.Should().HaveCount(2);
        parameters[0].Name.Should().Be("p0");
        parameters[1].Name.Should().Be("p3");
    }

    [Fact]
    public void PreparedRenderKind_ShouldNotLower()
    {
        using var ctx = MySqlTestContext.CreateMySql();
        var command = (InsertCommand)ctx.CreateInsertBuilder<LoweringEntity>()
            .Values(new LoweringEntity { Id = 1, Text = "a", Letter = 'b', Token = SampleGuid })
            .BuildBatchCommand();

        var (sql, parameters) = SqlMutationBuilder.MakeInsert(
            MySqlDialect.Instance,
            quoteIdentifiers: false,
            namingConvention: null,
            command,
            KeywordCase.Lower,
            parameterProvider: new DefaultParameterProvider(),
            renderKind: MutationRenderKind.Prepared);

        Normalize(sql).Should().Contain("values (@p0, @p1, @p2, @p3)",
            "a prepared render transports values through the parameter template and never lowers");
        parameters.Should().HaveCount(4);
    }

    [Fact]
    public void NonMySqlDialect_ShouldBeByteIdentical()
    {
        using var ctx = MySqlTestContext.CreateMySql();
        var command = (InsertCommand)ctx.CreateInsertBuilder<LoweringEntity>()
            .Values(new LoweringEntity { Id = 1, Text = "a", Letter = 'b', Token = SampleGuid })
            .BuildBatchCommand();

        var (sql, parameters) = SqlMutationBuilder.MakeInsert(
            SqlServerDialect.Instance,
            quoteIdentifiers: false,
            namingConvention: null,
            command,
            KeywordCase.Lower,
            parameterProvider: new DefaultParameterProvider(),
            renderKind: MutationRenderKind.AdHoc);

        Normalize(sql).Should().Be(
            "insert into lowering_entity (id, text, letter, token) values (@p0, @p1, @p2, @p3)",
            "a non-MySQL-family dialect keeps the pre-D4g SQL byte for byte");
        parameters.Select(p => p.Name).Should().Equal("p0", "p1", "p2", "p3");
    }

    [Fact]
    public void Lowering_IsActiveOnlyForTheMySqlFamilyOutsideThePreparedRender()
    {
        MutationParameterLowering.For(MySqlDialect.Instance, MutationRenderKind.Prepared).IsActive.Should().BeFalse();
        MutationParameterLowering.For(MySqlDialect.Instance, MutationRenderKind.AdHoc).IsActive.Should().BeTrue();
        MutationParameterLowering.For(MySqlDialect.Instance, MutationRenderKind.Batch).IsActive.Should().BeTrue();
        MutationParameterLowering.For(SqlServerDialect.Instance, MutationRenderKind.AdHoc).IsActive.Should().BeFalse();
    }

    [Fact]
    public void Lowering_BindingWithAnEmittedOccurrence_ShouldBeRetained()
    {
        var lowering = MutationParameterLowering.For(MySqlDialect.Instance, MutationRenderKind.AdHoc);

        lowering.TryRender("p0", "a", out var literal).Should().BeTrue();
        literal.Should().Be("_utf8mb4 X'61'");
        lowering.TryRender("p0", 42, out _).Should().BeFalse("a non-text value keeps its placeholder");

        var parameters = new List<Parameter> { new("p0", "a") };
        lowering.Project(parameters).Should().ContainSingle(p => p.Name == "p0",
            "another placeholder of the same name still needs the binding");
    }

    [Fact]
    public void Lowering_BindingWithEveryOccurrenceLowered_ShouldBeDropped()
    {
        var lowering = MutationParameterLowering.For(MySqlDialect.Instance, MutationRenderKind.AdHoc);

        lowering.TryRender("p0", "a", out _).Should().BeTrue();
        lowering.TryRender("p0", "b", out _).Should().BeTrue();

        var parameters = new List<Parameter> { new("p0", "a"), new("p1", 5) };
        lowering.Project(parameters).Select(p => p.Name).Should().Equal("p1");
    }

    [Fact]
    public void Lowering_UnrecordedBinding_ShouldBeRetained()
    {
        var lowering = MutationParameterLowering.For(MySqlDialect.Instance, MutationRenderKind.AdHoc);

        lowering.TryRender("p0", "a", out _).Should().BeTrue();

        // p1 was emitted by the planner, which never goes through the lowering: it must survive.
        var parameters = new List<Parameter> { new("p0", "a"), new("p1", "kept") };
        lowering.Project(parameters).Select(p => p.Name).Should().Equal("p1");
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

    /// <summary>The command the context binds and would execute; it records the text and the parameters.</summary>
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

using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level contract of the SQLite FTS5 maintenance/control command surface
/// (<see cref="DataContextExtensions.CreateSqliteFts5CommandBuilder"/>): factory guards, the immutable
/// operation-carrying builder, the unselected-terminal error and the exact rendered SQL. SQL rendering
/// uses a provider-free dialect that opts into FTS5, so the assertions run without a database; the
/// in-memory context pins the non-executing-context rejection.
/// </summary>
public class SqliteFts5CommandBuilderTests
{
    private const string Table = "ft";
    private const string OptimizeSql = "INSERT INTO \"ft\" (\"ft\") VALUES ('optimize')";
    private const string RebuildSql = "INSERT INTO \"ft\" (\"ft\") VALUES ('rebuild')";

    /// <summary>A dialect that opts into the SQLite FTS5 surface (only the capability gate matters here).</summary>
    private sealed class Fts5Dialect : SqlDialectBase
    {
        internal static readonly Fts5Dialect Instance = new();

        public override ISqliteFunctions SqliteFunctions => Fts5SqliteFunctions.Instance;

        public override bool SupportsTableFunction(string name) => name == "fts5";

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A dialect that exposes SQLite functions but does not advertise the FTS5 table function.</summary>
    private sealed class Fts5FunctionsOnlyDialect : SqlDialectBase
    {
        internal static readonly Fts5FunctionsOnlyDialect Instance = new();

        public override ISqliteFunctions SqliteFunctions => Fts5SqliteFunctions.Instance;

        public override bool SupportsTableFunction(string name) => false;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A dialect that advertises the FTS5 table function but exposes no SQLite functions.</summary>
    private sealed class Fts5TableFunctionOnlyDialect : SqlDialectBase
    {
        internal static readonly Fts5TableFunctionOnlyDialect Instance = new();

        public override ISqliteFunctions? SqliteFunctions => null;

        public override bool SupportsTableFunction(string name) => name == "fts5";

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class Fts5SqliteFunctions : ISqliteFunctions
    {
        internal static readonly Fts5SqliteFunctions Instance = new();

        public bool Supports(string name) => true;

        public string Render(string name, IReadOnlyList<string> args) => throw new NotSupportedException();
    }

    /// <summary>Uppercase + quoted so the asserted SQL matches the frozen canonical shape. The optional
    /// constructor switches keyword case (upper by default) and identifier quoting (on by default) to pin
    /// that the FTS5 identifier shape is independent of both.</summary>
    private sealed class Fts5Context : DataContext
    {
        public Fts5Context() : this(upper: true, quoted: true)
        {
        }

        public Fts5Context(bool upper, bool quoted)
            : base(Configure(upper, quoted))
        {
        }

        public override ISqlDialect Dialect => Fts5Dialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();

        private static DataContextBuilder Configure(bool upper, bool quoted)
        {
            var builder = new DataContextBuilder();
            if (upper)
                builder = builder.UseKeywordCase();
            if (quoted)
                builder = builder.UseQuotedIdentifiers();

            return builder;
        }
    }

    /// <summary>A context over a dialect that has SQLite functions but rejects the FTS5 table function.</summary>
    private sealed class Fts5FunctionsOnlyContext : DataContext
    {
        public Fts5FunctionsOnlyContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => Fts5FunctionsOnlyDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    /// <summary>A context over a dialect that advertises FTS5 but exposes no SQLite functions.</summary>
    private sealed class Fts5TableFunctionOnlyContext : DataContext
    {
        public Fts5TableFunctionOnlyContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => Fts5TableFunctionOnlyDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    // ---------------------------------------------------------------- factory guards -----------------

    [Fact]
    public void Factory_ShouldReturnTheFts5Builder()
    {
        using var ctx = new InMemoryDataContext();

        ctx.CreateSqliteFts5CommandBuilder(Table).Should().BeOfType<SqliteFts5CommandBuilder>();
    }

    [Fact]
    public void Factory_NullDataContext_ShouldThrowArgumentNull()
    {
        IDataContext? nullContext = null;

        var act = () => nullContext!.CreateSqliteFts5CommandBuilder(Table);

        act.Should().Throw<ArgumentNullException>().WithParameterName("dataContext");
    }

    [Fact]
    public void Factory_NullTableName_ShouldThrowArgumentNull()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("tableName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("a\0b")]
    public void Factory_EmptyWhitespaceOrNulTableName_ShouldThrowArgument(string tableName)
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(tableName);

        act.Should().Throw<ArgumentException>().WithParameterName("tableName");
    }

    // ---------------------------------------------------------------- unselected terminal -------------

    [Fact]
    public void Unselected_ToSql_ShouldThrowInvalidOperation()
    {
        using var ctx = new Fts5Context();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Unselected_Execute_ShouldThrowInvalidOperation()
    {
        using var ctx = new Fts5Context();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).Execute();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Unselected_ExecuteAsync_ShouldThrowInvalidOperation()
    {
        using var ctx = new Fts5Context();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).ExecuteAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------------------------------------------------------- immutability --------------------

    [Fact]
    public void Operations_ShouldReturnNewBuilderAndLeaveTheReceiverUntouched()
    {
        using var ctx = new Fts5Context();
        var receiver = ctx.CreateSqliteFts5CommandBuilder(Table);

        var selected = receiver.Optimize();

        selected.Should().NotBeSameAs(receiver);
        selected.ToSql().Should().Be(OptimizeSql);

        // The receiver never picked up the operation: it still has no selection.
        var act = () => receiver.ToSql();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reselecting_ShouldReturnAFreshInstanceEachTime()
    {
        using var ctx = new Fts5Context();
        var receiver = ctx.CreateSqliteFts5CommandBuilder(Table);

        receiver.Optimize().Should().NotBeSameAs(receiver.Optimize());
    }

    [Fact]
    public void ChainingAnotherOperation_ShouldReplaceNotQueue()
    {
        using var ctx = new Fts5Context();
        var optimize = ctx.CreateSqliteFts5CommandBuilder(Table).Optimize();

        var rebuild = optimize.Rebuild();

        rebuild.ToSql().Should().Be(RebuildSql, "the later operation replaces the earlier one; operations are not queued");
        optimize.ToSql().Should().Be(OptimizeSql, "the intermediate builder is unchanged");
    }

    // ---------------------------------------------------------------- exact SQL -----------------------

    [Theory]
    [InlineData("automerge", 4)]
    [InlineData("crisismerge", 2)]
    [InlineData("merge", 8)]
    public void RankValuesOperation_ShouldRenderTheTwoColumnForm(string operation, int value)
    {
        using var ctx = new Fts5Context();

        var sql = operation switch
        {
            "automerge" => ctx.CreateSqliteFts5CommandBuilder(Table).AutoMerge(value).ToSql(),
            "crisismerge" => ctx.CreateSqliteFts5CommandBuilder(Table).CrisisMerge(value).ToSql(),
            _ => ctx.CreateSqliteFts5CommandBuilder(Table).Merge(value).ToSql(),
        };

        sql.Should().Be($"INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('{operation}', {value})");
    }

    [Fact]
    public void Optimize_ShouldRenderTheOneColumnForm()
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().ToSql().Should().Be(OptimizeSql);
    }

    [Fact]
    public void Rebuild_ShouldRenderTheOneColumnForm()
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).Rebuild().ToSql().Should().Be(RebuildSql);
    }

    [Fact]
    public void IntegrityCheck_OmittedFlag_ShouldRenderTheOneColumnForm()
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).IntegrityCheck().ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\") VALUES ('integrity-check')");
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void IntegrityCheck_ExplicitFlag_ShouldRenderTheTwoColumnForm(bool flag, int rank)
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).IntegrityCheck(flag).ToSql()
            .Should().Be($"INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('integrity-check', {rank})");
    }

    [Fact]
    public void ExplicitNullFlag_ShouldMatchTheOmittedForm()
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).IntegrityCheck(null).ToSql()
            .Should().Be(ctx.CreateSqliteFts5CommandBuilder(Table).IntegrityCheck().ToSql());
    }

    // ---------------------------------------------------------------- AutoMerge bounds -----------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void AutoMerge_InRange_ShouldRender(int value)
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).AutoMerge(value).ToSql()
            .Should().Be($"INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('automerge', {value})");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void AutoMerge_OutOfRange_ShouldThrowArgumentOutOfRange(int value)
    {
        using var ctx = new Fts5Context();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).AutoMerge(value);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("value");
    }

    // ---------------------------------------------------------------- CrisisMerge / Merge --------------

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void CrisisMerge_Negative_ShouldThrowArgumentOutOfRange(int value)
    {
        using var ctx = new Fts5Context();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).CrisisMerge(value);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("value");
    }

    [Fact]
    public void CrisisMerge_Zero_ShouldRender()
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).CrisisMerge(0).ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('crisismerge', 0)");
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Merge_SignedValue_ShouldRenderUnchangedWithoutAbs(int pages)
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).Merge(pages).ToSql()
            .Should().Be($"INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('merge', {pages})");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    public void CrisisMerge_Positive_ShouldRenderTheTwoColumnForm(int value)
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).CrisisMerge(value).ToSql()
            .Should().Be($"INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('crisismerge', {value})");
    }

    [Fact]
    public void Merge_Zero_ShouldRender()
    {
        using var ctx = new Fts5Context();

        ctx.CreateSqliteFts5CommandBuilder(Table).Merge(0).ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('merge', 0)");
    }

    // ---------------------------------------------------------------- verbatim name / quoting -------

    [Fact]
    public void TableNameWithSurroundingSpaces_ShouldBePreservedAndQuoted()
    {
        // The factory preserves the name verbatim (no trim); quoting must not normalize the whitespace.
        using var ctx = new Fts5Context(upper: true, quoted: false);

        ctx.CreateSqliteFts5CommandBuilder(" ft ").Optimize().ToSql()
            .Should().Be("INSERT INTO \" ft \" (\" ft \") VALUES ('optimize')");
    }

    [Theory]
    [InlineData(true, true, "INSERT INTO", "VALUES")]   // upper + quoted
    [InlineData(true, false, "INSERT INTO", "VALUES")]  // upper + unquoted (Fts5Context default was quoted)
    [InlineData(false, true, "insert into", "values")]  // lower + quoted
    [InlineData(false, false, "insert into", "values")] // lower + unquoted (DataContextBuilder default)
    public void KeywordCaseAndQuoting_ShouldNotChangeTheQuotedIdentifierShape(bool upper, bool quoted, string into, string values)
    {
        using var ctx = new Fts5Context(upper, quoted);

        ctx.CreateSqliteFts5CommandBuilder(Table).AutoMerge(4).ToSql()
            .Should().Be($"{into} \"ft\" (\"ft\", \"rank\") {values} ('automerge', 4)");
    }

    // ---------------------------------------------------------------- isolated capability signals -

    [Fact]
    public async Task FunctionsWithoutTableFunction_AllTerminals_ShouldThrowNotSupported()
    {
        using var ctx = new Fts5FunctionsOnlyContext();

        await AssertAllTerminalsReject(ctx, nameof(Fts5FunctionsOnlyDialect));
    }

    [Fact]
    public async Task TableFunctionWithoutFunctions_AllTerminals_ShouldThrowNotSupported()
    {
        using var ctx = new Fts5TableFunctionOnlyContext();

        await AssertAllTerminalsReject(ctx, nameof(Fts5TableFunctionOnlyDialect));
    }

    private static async Task AssertAllTerminalsReject(DataContext ctx, string dialectName)
    {
        // `||` (never `&&`): a dialect that has only one of the two FTS5 signals must be rejected, not
        // rendered. The message carries the dialect name so the failing half is identifiable.
        var render = () => ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().ToSql();
        render.Should().Throw<NotSupportedException>().WithMessage($"*{dialectName}*SQLite FTS5*");

        var execute = () => ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().Execute();
        execute.Should().Throw<NotSupportedException>().WithMessage($"*{dialectName}*SQLite FTS5*");

        var executeAsync = () => ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().ExecuteAsync();
        await executeAsync.Should().ThrowAsync<NotSupportedException>().WithMessage($"*{dialectName}*SQLite FTS5*");
    }

    // ---------------------------------------------------------------- non-FTS5 context ----------------

    [Fact]
    public void InMemory_ToSql_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support data modification*");
    }

    [Fact]
    public void InMemory_Execute_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().Execute();

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support data modification*");
    }

    [Fact]
    public async Task InMemory_ExecuteAsync_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().ExecuteAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*does not support data modification*");
    }
}

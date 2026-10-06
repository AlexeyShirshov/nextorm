using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation for the SQLite FTS5 maintenance/control command surface
/// (<see cref="DataContextExtensions.CreateSqliteFts5CommandBuilder"/>) without a database. The
/// canonical shape is uppercase keywords with quoted identifiers, matching the frozen contract.
/// </summary>
public class SqliteFts5MaintenanceSqlGenerationTests
{
    private const string Table = "ft";

    [Fact]
    public void AutoMerge_ShouldRenderTheNativeControlForm()
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).AutoMerge(4).ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('automerge', 4)");
    }

    [Fact]
    public void CrisisMerge_ShouldRenderTheNativeControlForm()
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).CrisisMerge(2).ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('crisismerge', 2)");
    }

    [Fact]
    public void Merge_ShouldRenderTheNativeControlForm()
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).Merge(8).ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('merge', 8)");
    }

    [Fact]
    public void Optimize_ShouldRenderTheNativeControlForm()
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\") VALUES ('optimize')");
    }

    [Fact]
    public void Rebuild_ShouldRenderTheNativeControlForm()
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).Rebuild().ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\") VALUES ('rebuild')");
    }

    [Fact]
    public void IntegrityCheck_OmittedFlag_ShouldRenderTheOneColumnForm()
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).IntegrityCheck().ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\") VALUES ('integrity-check')");
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void IntegrityCheck_Flag_ShouldRenderTheTwoColumnForm(bool flag, int rank)
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).IntegrityCheck(flag).ToSql()
            .Should().Be($"INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('integrity-check', {rank})");
    }

    [Theory]
    [InlineData("a b", "\"a b\"")]
    [InlineData("o'brien", "\"o'brien\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("schema.table", "\"schema.table\"")]
    public void TableName_ShouldBeQuotedAsOneIdentifier(string tableName, string quoted)
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(tableName).Optimize().ToSql()
            .Should().Be($"INSERT INTO {quoted} ({quoted}) VALUES ('optimize')");
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void IntegerArgument_ShouldRenderInvariantUnchanged(int value)
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(Table).Merge(value).ToSql()
            .Should().Be($"INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('merge', {value})");
    }

    // Regression (D195-FTS5-IDENTIFIER-QUOTING): the DataContextBuilder default is unquoted
    // (`QuoteIdentifiers == false`), but FC195-1 requires the FTS5 maintenance identifiers to be always
    // safely double-quoted. The old renderer emitted the caller-supplied name verbatim here.
    [Fact]
    public void UnquotedContext_ShouldStillQuoteIdentifiers()
    {
        using var ctx = SqliteTestContext.CreateUppercase();

        ctx.CreateSqliteFts5CommandBuilder(Table).AutoMerge(4).ToSql()
            .Should().Be("INSERT INTO \"ft\" (\"ft\", \"rank\") VALUES ('automerge', 4)");
    }

    [Fact]
    public void LowercaseUnquotedDefaults_ShouldQuoteIdentifiersAndKeepLowercaseKeywords()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateSqliteFts5CommandBuilder(Table).Optimize().ToSql()
            .Should().Be("insert into \"ft\" (\"ft\") values ('optimize')");
    }

    [Theory]
    [InlineData("a b", "\"a b\"")]
    [InlineData("o'brien", "\"o'brien\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("ft\"; DROP TABLE x; --", "\"ft\"\"; DROP TABLE x; --\"")]
    public void UnquotedContext_TableName_ShouldBeQuotedAsOneIdentifier(string tableName, string quoted)
    {
        using var ctx = SqliteTestContext.CreateUppercase();

        ctx.CreateSqliteFts5CommandBuilder(tableName).Optimize().ToSql()
            .Should().Be($"INSERT INTO {quoted} ({quoted}) VALUES ('optimize')");
    }
}

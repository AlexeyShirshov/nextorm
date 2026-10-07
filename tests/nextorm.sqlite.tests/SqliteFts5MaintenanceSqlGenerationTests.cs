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

    // D196 (#196): the verbatim table name must survive byte-for-byte through all eight
    // maintenance/control forms for snake_case and camelCase. A renderer that trims,
    // case-normalizes or splits the identifier would break these golden strings. The expected
    // SQL is literal golden data: only the verbatim name is substituted, every keyword, column,
    // quoted identifier and value is written out, and no form carries a trailing semicolon.
    [Theory]
    [Trait("Issue", "196")]
    [InlineData("snake_case")]
    [InlineData("camelCase")]
    public void VerbatimTableName_ShouldRenderAllEightOperationForms(string name)
    {
        using var ctx = SqliteTestContext.CreateUppercaseQuoted();

        ctx.CreateSqliteFts5CommandBuilder(name).AutoMerge(4).ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\", \"rank\") VALUES ('automerge', 4)");
        ctx.CreateSqliteFts5CommandBuilder(name).CrisisMerge(2).ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\", \"rank\") VALUES ('crisismerge', 2)");
        ctx.CreateSqliteFts5CommandBuilder(name).Merge(-3).ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\", \"rank\") VALUES ('merge', -3)");
        ctx.CreateSqliteFts5CommandBuilder(name).Optimize().ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\") VALUES ('optimize')");
        ctx.CreateSqliteFts5CommandBuilder(name).Rebuild().ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\") VALUES ('rebuild')");
        ctx.CreateSqliteFts5CommandBuilder(name).IntegrityCheck().ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\") VALUES ('integrity-check')");
        ctx.CreateSqliteFts5CommandBuilder(name).IntegrityCheck(false).ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\", \"rank\") VALUES ('integrity-check', 0)");
        ctx.CreateSqliteFts5CommandBuilder(name).IntegrityCheck(true).ToSql()
            .Should().Be($"INSERT INTO \"{name}\" (\"{name}\", \"rank\") VALUES ('integrity-check', 1)");
    }
}

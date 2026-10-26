using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on SQLite. The native path does not exist on SQLite,
/// so the portable <c>INSERT ... VALUES</c> form is always rendered; these tests do not open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static InsertEntity Row(string name, int age, string description) => new() { Name = name, Age = age, Description = description };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1, "d"), Row("b", 2, "e")]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2), ($p3, $p4, $p5)");
    }

    [Fact]
    public void BulkInsert_MaxBatchSize_ShouldRenderOnlyTheFirstBatch()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(1)).Values([Row("a", 1, "d"), Row("b", 2, "e")]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldIncludeIdentityColumn()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age, description) values ($p0, $p1, $p2, $p3)");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldUseInsertOrIgnore()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert or ignore into insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldEmitReturning()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1, "d")]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2) returning id");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_WithReturningKey_ShouldEmitInsertOrIgnoreWithReturning()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1, "d")]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert or ignore into insert_entity (name, age, description) values ($p0, $p1, $p2) returning id");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_OnNonIdentityEntity_ShouldIncludeExplicitColumnOnly()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values ($p0)");
    }

    [Fact]
    public async Task BulkInsert_NativeMode_ShouldThrowBeforeEnumeratingTheSource()
    {
        using var ctx = SqliteTestContext.Create();
        var enumerated = false;

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native))
            .Values(TrackingSource(() => enumerated = true))
            .BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*Mode.Native*");
        enumerated.Should().BeFalse();
    }

    // ---------------------------------------------------------------- D2 destination / temp rendering

    [Fact]
    public void BulkInsert_SchemaNameOnly_ShouldQualifyWithTheMappedTable()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Schema("staging")).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into staging.insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_DatabaseName_ShouldQualifyTheTarget()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Database("other")).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into other.insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_QuotedDatabase_ShouldQuoteEachPartSeparately()
    {
        using var ctx = SqliteTestContext.CreateQuoted();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Database("other")).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into \"other\".\"insert_entity\" (\"name\", \"age\", \"description\") values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_QuotedSchemaOnly_ShouldQuoteEachPartSeparately()
    {
        using var ctx = SqliteTestContext.CreateQuoted();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Schema("staging")).Values([Row("a", 1, "d")]).ToSql();

        sql.Should().Be("insert into \"staging\".\"insert_entity\" (\"name\", \"age\", \"description\") values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_LocalTemporary_ShouldRenderTheSameTargetWithoutDdl()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary))
            .Values([Row("a", 1, "d")])
            .ToSql();

        // The caller's temp table lives on this connection; the portable insert addresses it as-is and
        // emits neither CREATE nor DROP.
        sql.Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void BulkInsert_LocalTemporaryWithDatabaseName_ShouldBeRefusedByName()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary).Database("other"))
            .Values([Row("a", 1, "d")])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*DatabaseName*");
    }

    private static async IAsyncEnumerable<IInsertEntity> TrackingSource(Action onEnumerated)
    {
        onEnumerated();
        await Task.Yield();
        yield return Row("a", 1, "d");
    }
}

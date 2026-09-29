using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// SQL-generation tests for the write side of the dynamic-columns store (issue #104): an INSERT,
/// UPDATE or MERGE over an entity whose <see cref="DynamicColumnsAttribute"/> store contributes
/// physical columns. No database is involved; a minimal dialect-backed context renders the SQL.
/// </summary>
public class DynamicColumnsWriteTests
{
    [SqlTable("plain_row")]
    private sealed class PlainRowEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [SqlTable("dynamic_row")]
    private sealed class DynamicRowEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    // No [DynamicColumns] attribute: the store is declared fluently through the builder's
    // configEntity callback, exercising the other declaration path.
    [SqlTable("fluent_dynamic_row")]
    private sealed class FluentDynamicRowEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    // The dynamic store is the only writable non-key column: the key upsert has no mapped column to
    // SET, so it must fall back to the store's dynamic columns instead of rejecting the merge.
    [SqlTable("store_only_row")]
    private sealed class StoreOnlyRowEntity
    {
        public int Id { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    // Same store-only shape, but the match key is database-generated. Such a key cannot be carried by
    // the USING source (identity columns are excluded from it), so it must stay rejected as a match key.
    [SqlTable("identity_store_only_row")]
    private sealed class IdentityStoreOnlyRowEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    // A range property stores its two bounds as scalar physical columns whose names need not match the
    // CLR name. The read side strips only the physical lower/upper names, so the CLR name is not a
    // mapped name and a dynamic key using it is an ordinary dynamic column, not a collision.
    [SqlTable("range_dynamic_row")]
    private sealed class RangeDynamicRowEntity
    {
        public int Id { get; set; }

        [RangeColumns("during_lower", "during_upper")]
        public Range<int> During { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    // The CLR name (Renamed) differs from the physical column (physical_name): a dynamic key naming the
    // physical column would shadow it and must be rejected.
    [SqlTable("renamed_dynamic_row")]
    private sealed class RenamedDynamicRowEntity
    {
        public int Id { get; set; }

        [Column("physical_name")]
        public string? Renamed { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    // The store is typed as the read-only interface so a caller can supply any implementation, including
    // one that is not an ICollection: the bulk path's non-emptiness probe must not depend on the concrete
    // dictionary shape.
    [SqlTable("readonly_dynamic_row")]
    private sealed class ReadOnlyDynamicRowEntity
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [DynamicColumns]
        public IReadOnlyDictionary<string, object?> Extra { get; set; } = new Dictionary<string, object?>();
    }

    // Implements IReadOnlyDictionary<string, object?> but deliberately NOT the non-generic ICollection,
    // so an ICollection.Count probe cannot see that it is populated.
    private sealed class ReadOnlyOnlyStore : IReadOnlyDictionary<string, object?>
    {
        private readonly Dictionary<string, object?> _inner;

        public ReadOnlyOnlyStore(Dictionary<string, object?> inner) => _inner = inner;

        public object? this[string key] => _inner[key];
        public IEnumerable<string> Keys => _inner.Keys;
        public IEnumerable<object?> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public bool TryGetValue(string key, out object? value) => _inner.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        // The full-MERGE branch form (WhenMatched()/WhenNotMatched()) is exercised here.
        public override bool SupportsMergeStatement => true;

        // An explicit On(...) search condition is exercised here. The store-only identity cases that
        // must stay rejected throw earlier in BuildCommand (the VALUES-source guard), so enabling this
        // does not weaken them.
        public override bool SupportsMergeConditionalBranches => true;

        // A key upsert over a store-only entity is exercised here (the key-upsert form needs an
        // ON CONFLICT / ON DUPLICATE KEY / MERGE dialect hook to render at all).
        public override bool SupportsOnConflict => true;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        // The context never opens a connection or creates a param; only SQL text is requested.
        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    // A bracket-quoting dialect (SQL Server style) so the test can prove the dynamic writer routes every
    // key through dialect.QuoteIdentifier instead of concatenating the raw name: the embedded closing
    // bracket must be doubled, not passed through.
    private sealed class BracketDialect : SqlDialectBase
    {
        internal static readonly BracketDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override string QuoteIdentifier(string name) => "[" + name.Replace("]", "]]") + "]";

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class BracketContext : DataContext
    {
        public BracketContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => BracketDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    [Fact]
    public void Insert_KeysAreOrdinalAndQuoted()
    {
        using var ctx = new TestContext();
        var entity = new DynamicRowEntity
        {
            Id = 1,
            Name = "row",
            Extra = new Dictionary<string, object?>
            {
                ["zeta"] = 3,
                ["Alpha"] = 1,
                ["beta"] = 2,
            },
        };

        var sql = ctx.InsertInto<DynamicRowEntity>().Values(entity).ToSql();

        // The global identifier-quoting flag is off, so the mapped columns stay unquoted while every
        // dynamic key is quoted through the dialect regardless.
        sql.Should().Contain("(Id, Name, \"Alpha\", \"beta\", \"zeta\")");
        sql.Should().Contain("values (@p0, @p1, @p2, @p3, @p4)");

        var alpha = sql.IndexOf("\"Alpha\"", StringComparison.Ordinal);
        var beta = sql.IndexOf("\"beta\"", StringComparison.Ordinal);
        var zeta = sql.IndexOf("\"zeta\"", StringComparison.Ordinal);
        alpha.Should().BeGreaterThanOrEqualTo(0);
        beta.Should().BeGreaterThan(alpha);
        zeta.Should().BeGreaterThan(beta);

        // The fluent declaration path (no [DynamicColumns] attribute) behaves identically. Declaring
        // any property fluently maps only the declared ones, so Id/Name are declared explicitly.
        var fluent = new FluentDynamicRowEntity
        {
            Id = 1,
            Name = "row",
            Extra = new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 },
        };
        var fluentSql = ctx.InsertInto<FluentDynamicRowEntity>(b =>
            {
                b.Property(x => x.Id).Key();
                b.Property(x => x.Name!);
                b.Property(x => x.Extra).DynamicColumnsStore();
            })
            .Values(fluent)
            .ToSql();

        fluentSql.Should().Contain("(Id, Name, \"a\", \"b\")");
        fluentSql.Should().Contain("values (@p0, @p1, @p2, @p3)");
    }

    [Fact]
    public void Insert_NullVersusMissing()
    {
        using var ctx = new TestContext();
        var entity = new DynamicRowEntity
        {
            Id = 1,
            Name = "row",
            // "a" is present with a null value (included as a bound null parameter); "c" is missing
            // (the column is omitted entirely).
            Extra = new Dictionary<string, object?>
            {
                ["a"] = null,
                ["b"] = 2,
            },
        };

        var sql = ctx.InsertInto<DynamicRowEntity>().Values(entity).ToSql();

        sql.Should().Contain("\"a\"");
        sql.Should().Contain("\"b\"");
        sql.Should().NotContain("\"c\"");
        // Two mapped columns plus the two present dynamic keys, one parameter each.
        sql.Should().Contain("values (@p0, @p1, @p2, @p3)");
    }

    [Fact]
    public void Insert_RowKeySetMismatchThrows()
    {
        using var ctx = new TestContext();
        var first = new DynamicRowEntity { Id = 1, Extra = new() { ["a"] = 1 } };
        var second = new DynamicRowEntity { Id = 2, Extra = new() { ["b"] = 2 } };

        var act = () => ctx.InsertInto<DynamicRowEntity>().Values([first, second]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*same dynamic-column keys*");
    }

    [Fact]
    public void Insert_FirstRowEmptyLaterNonEmpty_ShouldThrow()
    {
        using var ctx = new TestContext();

        // The first row's empty store fixes the batch's key set at "no keys"; a later row with a key is a
        // mismatch and must be rejected rather than silently dropping that key's column.
        var firstEmpty = () => ctx.InsertInto<DynamicRowEntity>().Values(
        [
            new DynamicRowEntity { Id = 1, Name = "first", Extra = new() },
            new DynamicRowEntity { Id = 2, Name = "second", Extra = new() { ["a"] = 1 } },
        ]);
        firstEmpty.Should().Throw<InvalidOperationException>().WithMessage("*same dynamic-column keys*");

        // The reverse order is rejected too: a later empty store does not match the first row's key.
        var laterEmpty = () => ctx.InsertInto<DynamicRowEntity>().Values(
        [
            new DynamicRowEntity { Id = 3, Name = "third", Extra = new() { ["a"] = 1 } },
            new DynamicRowEntity { Id = 4, Name = "fourth", Extra = new() },
        ]);
        laterEmpty.Should().Throw<InvalidOperationException>().WithMessage("*same dynamic-column keys*");
    }

    [Fact]
    public void Write_RejectsEmptyAndNulKey()
    {
        using var ctx = new TestContext();

        var insertEmpty = () => ctx.InsertInto<DynamicRowEntity>()
            .Values(new DynamicRowEntity { Id = 1, Extra = new() { [""] = 1 } });
        var insertNul = () => ctx.InsertInto<DynamicRowEntity>()
            .Values(new DynamicRowEntity { Id = 1, Extra = new() { ["a\0b"] = 1 } });
        var updateEmpty = () => ctx.Update<DynamicRowEntity>()
            .Set(new DynamicRowEntity { Id = 1, Extra = new() { [""] = 1 } });
        var updateNul = () => ctx.Update<DynamicRowEntity>()
            .Set(new DynamicRowEntity { Id = 1, Extra = new() { ["a\0b"] = 1 } });
        var mergeEmpty = () => ctx.MergeInto<DynamicRowEntity>()
            .Using(new DynamicRowEntity { Id = 1, Extra = new() { [""] = 1 } });

        insertEmpty.Should().Throw<ArgumentException>();
        insertNul.Should().Throw<ArgumentException>();
        updateEmpty.Should().Throw<ArgumentException>();
        updateNul.Should().Throw<ArgumentException>();
        mergeEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_OmittedKeyIsUnchanged()
    {
        using var ctx = new TestContext();
        var entity = new DynamicRowEntity
        {
            Id = 1,
            Name = "row",
            Extra = new Dictionary<string, object?> { ["a"] = 1 },
        };

        var sql = ctx.Update<DynamicRowEntity>()
            .Set(entity)
            .Where(x => x.Id == 1)
            .ToSql();

        // The present key is written (quoted even with the global flag off); the omitted key is absent
        // from the SET list, so its column stays unchanged.
        sql.Should().Contain("set Name = @p0, \"a\" = @p1");
        sql.Should().NotContain("\"b\"");
    }

    [Fact]
    public void Merge_DynamicKeysInSourceInsertSetNotOn()
    {
        using var ctx = new TestContext();
        var entity = new DynamicRowEntity
        {
            Id = 1,
            Name = "row",
            Extra = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 },
        };

        var sql = ctx.MergeInto<DynamicRowEntity>()
            .Using(entity)
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        // The dynamic keys extend the derived source and the INSERT/SET lists...
        sql.Should().Contain("as source (Id, Name, \"a\", \"b\")");
        sql.Should().Contain("then insert (Id, Name, \"a\", \"b\") values (source.Id, source.Name, source.\"a\", source.\"b\")");
        sql.Should().Contain("then update set target.Name = source.Name, target.\"a\" = source.\"a\", target.\"b\" = source.\"b\"");

        // ...but never the ON match, which stays on the declared key.
        var onStart = sql.IndexOf(" on ", StringComparison.Ordinal) + 4;
        var whenStart = sql.IndexOf(" when ", StringComparison.Ordinal);
        var on = sql[onStart..whenStart];
        on.Should().Be("target.Id = source.Id");
        on.Should().NotContain("\"a\"");
        on.Should().NotContain("\"b\"");
    }

    [Fact]
    public void Write_StorelessSqlUnchanged()
    {
        using var ctx = new TestContext();

        var insert = ctx.InsertInto<PlainRowEntity>()
            .Values(new PlainRowEntity { Id = 1, Name = "row" })
            .ToSql();
        var update = ctx.Update<PlainRowEntity>()
            .Set(new PlainRowEntity { Id = 1, Name = "row" })
            .Where(x => x.Id == 1)
            .ToSql();

        // Byte-identical to the pre-feature output: no dynamic plumbing leaks into a store-less write.
        insert.Should().Be("insert into plain_row (Id, Name) values (@p0, @p1)");
        update.Should().Be("update plain_row set Name = @p0 where Id = 1");
    }

    [Fact]
    public void Insert_MultipleRows_ShouldBindEveryRow()
    {
        using var ctx = new TestContext();
        // Three rows with the same two dynamic keys, inserted in a different dictionary order per row:
        // the writer must ordinal-sort once and bind one value tuple per row, in row order.
        var rows = new[]
        {
            DynamicRow(1, "a", beta: "B1", alpha: 1),
            DynamicRow(2, "b", beta: "B2", alpha: 2),
            DynamicRow(3, "c", beta: "B3", alpha: 3),
        };

        var sql = ctx.InsertInto<DynamicRowEntity>().Values(rows).ToSql();

        // The column list is rendered once for the whole statement, ordinal-sorted ("alpha" before "beta").
        sql.Split("(Id, Name, \"alpha\", \"beta\")", StringSplitOptions.None).Should().HaveCount(2);
        sql.IndexOf("\"alpha\"", StringComparison.Ordinal).Should().BeLessThan(sql.IndexOf("\"beta\"", StringComparison.Ordinal));

        // Exactly one value tuple per row, each with the mapped values followed by the sorted dynamic ones,
        // and the parameters allocated sequentially across rows.
        sql.Should().Contain("values (@p0, @p1, @p2, @p3), (@p4, @p5, @p6, @p7), (@p8, @p9, @p10, @p11)");
        System.Text.RegularExpressions.Regex.Matches(sql, @"@p\d+").Count.Should().Be(12);
    }

    [Fact]
    public void BulkInsert_NonEmptyDynamicStore_ShouldThrow_EmptyOrNullStoreUnaffected()
    {
        using var ctx = new TestContext();

        // The bulk path writes only mapped columns, so a populated store would be silently dropped: it
        // fails closed instead.
        var nonEmpty = () => ctx.BulkInsertInto<DynamicRowEntity>()
            .Values([new DynamicRowEntity { Id = 1, Name = "x", Extra = new() { ["a"] = 1 } }])
            .ToSql();
        nonEmpty.Should().Throw<NotSupportedException>();

        // An empty or null store contributes no dynamic columns, so the bulk write is unaffected.
        var emptySql = ctx.BulkInsertInto<DynamicRowEntity>()
            .Values([new DynamicRowEntity { Id = 1, Name = "x", Extra = new() }])
            .ToSql();
        emptySql.Should().Be("insert into dynamic_row (Id, Name) values (@p0, @p1)");

        var nullSql = ctx.BulkInsertInto<DynamicRowEntity>()
            .Values([new DynamicRowEntity { Id = 2, Name = "y", Extra = null! }])
            .ToSql();
        nullSql.Should().Be("insert into dynamic_row (Id, Name) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_NonCollectionDictionary_NonEmptyStore_ShouldThrow()
    {
        using var ctx = new TestContext();

        // The store is a custom IReadOnlyDictionary that is NOT an ICollection; the non-empty probe must
        // still see it (a populated store would otherwise be silently dropped).
        var nonEmpty = () => ctx.BulkInsertInto<ReadOnlyDynamicRowEntity>()
            .Values([new ReadOnlyDynamicRowEntity { Id = 1, Name = "x", Extra = new ReadOnlyOnlyStore(new() { ["a"] = 1 }) }])
            .ToSql();
        nonEmpty.Should().Throw<NotSupportedException>();

        // An empty implementation of the same shape contributes no dynamic column: the bulk write is
        // unaffected, exactly as for a plain Dictionary.
        var emptySql = ctx.BulkInsertInto<ReadOnlyDynamicRowEntity>()
            .Values([new ReadOnlyDynamicRowEntity { Id = 1, Name = "x", Extra = new ReadOnlyOnlyStore(new()) }])
            .ToSql();
        emptySql.Should().Be("insert into readonly_dynamic_row (Id, Name) values (@p0, @p1)");

        var nullSql = ctx.BulkInsertInto<ReadOnlyDynamicRowEntity>()
            .Values([new ReadOnlyDynamicRowEntity { Id = 2, Name = "y", Extra = null! }])
            .ToSql();
        nullSql.Should().Be("insert into readonly_dynamic_row (Id, Name) values (@p0, @p1)");
    }

    [Fact]
    public void Write_StorelessMergeSqlUnchanged()
    {
        using var ctx = new TestContext();

        var merge = ctx.MergeInto<PlainRowEntity>()
            .Using(new PlainRowEntity { Id = 1, Name = "row" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        // Byte-identical to the pre-feature output: no dynamic plumbing leaks into a store-less full MERGE.
        merge.Should().Be(
            "merge into plain_row as target using (values (@p0, @p1)) as source (Id, Name) " +
            "on target.Id = source.Id " +
            "when matched then update set target.Name = source.Name " +
            "when not matched then insert (Id, Name) values (source.Id, source.Name)");
    }

    [Fact]
    public void Write_StoreKeyCollidingWithMappedColumn_ShouldThrow()
    {
        using var ctx = new TestContext();
        const string colliding = "Name";

        var insert = () => ctx.InsertInto<DynamicRowEntity>()
            .Values(new DynamicRowEntity { Id = 1, Extra = new() { [colliding] = "x" } })
            .ToSql();
        var update = () => ctx.Update<DynamicRowEntity>()
            .Set(new DynamicRowEntity { Id = 1, Extra = new() { [colliding] = "x" } });
        var merge = () => ctx.MergeInto<DynamicRowEntity>()
            .Using(new DynamicRowEntity { Id = 1, Extra = new() { [colliding] = "x" } });

        // A key that names a mapped physical column would silently shadow it; it is rejected and the
        // message names the offending key.
        insert.Should().Throw<InvalidOperationException>().WithMessage($"*{colliding}*");
        update.Should().Throw<InvalidOperationException>().WithMessage($"*{colliding}*");
        merge.Should().Throw<InvalidOperationException>().WithMessage($"*{colliding}*");
    }

    [Fact]
    public void Write_KeyEqualToRangeClrName_ShouldNotCollide()
    {
        using var ctx = new TestContext();
        var entity = new RangeDynamicRowEntity
        {
            Id = 1,
            During = new Range<int>(1, 10),
            // "During" is the range property's CLR name, not either physical bound column
            // (during_lower/during_upper). The read side strips only the physical names, so on write this
            // is an ordinary dynamic key, written as a column, never rejected as a collision.
            Extra = new() { ["During"] = 5 },
        };

        var sql = ctx.InsertInto<RangeDynamicRowEntity>().Values(entity).ToSql();

        sql.Should().Contain("(Id, during_lower, during_upper, \"During\")");
        sql.Should().Contain("\"During\"");
    }

    [Fact]
    public void Write_KeyEqualToPhysicalColumnName_ShouldThrow()
    {
        using var ctx = new TestContext();

        // The mapped property's CLR name (Renamed) differs from its physical column (physical_name). A
        // dynamic key naming the physical column would silently shadow the mapped one, so it is rejected
        // and the message names the offending key.
        var insert = () => ctx.InsertInto<RenamedDynamicRowEntity>()
            .Values(new RenamedDynamicRowEntity { Id = 1, Renamed = "x", Extra = new() { ["physical_name"] = "y" } })
            .ToSql();

        insert.Should().Throw<InvalidOperationException>().WithMessage("*physical_name*");
    }

    [Fact]
    public void Write_KeyWithEmbeddedIdentifierChar_ShouldEscapeThroughDialect()
    {
        using var ctx = new TestContext();
        var quoted = new DynamicRowEntity { Id = 1, Extra = new() { ["a\"b"] = 1 } };

        var sql = ctx.InsertInto<DynamicRowEntity>().Values(quoted).ToSql();

        // The embedded quote is doubled by QuoteIdentifier; the raw "a"b" never reaches the SQL text.
        sql.Should().Contain("\"a\"\"b\"");
        sql.Should().NotContain("\"a\"b\"");

        // Same contract on a bracket-quoting (SQL Server style) dialect: "]" becomes "]]", wrapped in [].
        using var bracketCtx = new BracketContext();
        var bracketed = new DynamicRowEntity { Id = 1, Extra = new() { ["c]d"] = 2 } };

        var bracketSql = bracketCtx.InsertInto<DynamicRowEntity>().Values(bracketed).ToSql();

        bracketSql.Should().Contain("[c]]d]");
        bracketSql.Should().NotContain("[c]d]");
    }

    [Fact]
    public void Merge_StoreOnlyEntity_ShouldNotThrowNoSource()
    {
        using var ctx = new TestContext();
        var entity = new StoreOnlyRowEntity { Id = 1, Extra = new() { ["alpha"] = 1 } };

        // The store is the only writable non-key column: the key upsert must not reject the merge for
        // having no mapped column to update, and the dynamic key must reach the SET list.
        var sql = ctx.MergeInto<StoreOnlyRowEntity>()
            .Using(entity)
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().Contain("(Id, \"alpha\")");
        sql.Should().Contain("\"alpha\" = excluded.\"alpha\"");

        // The full-MERGE branch form hits the same "no mapped selection" guard in AddBranch: the
        // selector-less update branch has no mapped column left (the key is skipped), so the dynamic
        // store must supply it. The dynamic key extends the derived source, the INSERT and the SET,
        // and never the ON match.
        var fullMerge = ctx.MergeInto<StoreOnlyRowEntity>()
            .Using(entity)
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        fullMerge.Should().Contain("as source (Id, \"alpha\")");
        fullMerge.Should().Contain("then update set target.\"alpha\" = source.\"alpha\"");
        fullMerge.Should().Contain("then insert (Id, \"alpha\") values (source.Id, source.\"alpha\")");

        var onStart = fullMerge.IndexOf(" on ", StringComparison.Ordinal) + 4;
        var whenStart = fullMerge.IndexOf(" when ", StringComparison.Ordinal);
        var on = fullMerge[onStart..whenStart];
        on.Should().Be("target.Id = source.Id");
        on.Should().NotContain("\"alpha\"");
    }

    [Fact]
    public void Merge_StoreOnlyEntity_DatabaseGeneratedKey_ShouldRejectIdentityMatchKey()
    {
        using var ctx = new TestContext();
        var entity = new IdentityStoreOnlyRowEntity { Id = 1, Extra = new() { ["alpha"] = 1 } };

        // A database-generated key cannot be a merge match key: identity columns are excluded from the
        // USING source, so OnKeys rejects it explicitly instead of rendering an ON that references a
        // source column which does not exist. This is a match-key constraint, not the source/selection
        // guard a store-only entity relaxes.
        var act = () => ctx.MergeInto<IdentityStoreOnlyRowEntity>()
            .Using(entity)
            .OnKeys();

        act.Should().Throw<NotSupportedException>().WithMessage("*database-generated*");
    }

    [Fact]
    public void Merge_StoreOnlyEntity_OnIdentityKey_ShouldBeRejected()
    {
        using var ctx = new TestContext();
        var entity = new IdentityStoreOnlyRowEntity { Id = 1, Extra = new() { ["alpha"] = 1 } };

        // The On(...) path reaches the same rejection as OnKeys: the identity key is absent from the
        // VALUES-derived source, so matching on it is not expressible. The check now lives in
        // BuildCommand (not in On(...)), because a query source projects the identity column and is valid.
        var onKeys = () => ctx.MergeInto<IdentityStoreOnlyRowEntity>().Using(entity).OnKeys();
        onKeys.Should().Throw<NotSupportedException>();

        var on = () => ctx.MergeInto<IdentityStoreOnlyRowEntity>()
            .Using(entity)
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        // The message pins the VALUES-source guard, so the test cannot pass on an unrelated
        // NotSupportedException (for example the dialect's missing conditional-branch capability).
        on.Should().Throw<NotSupportedException>().WithMessage("*is not a column of the VALUES-derived merge source*");
    }

    [Fact]
    public void Merge_StoreOnlyIdentity_OnSourceKey_ShouldThrow()
    {
        using var ctx = new TestContext();

        // The guard belongs to BuildCommand, not On(...), so it catches the store-only identity key
        // whatever the On/Using call order: the source is VALUES-derived either way and never declares
        // the generated Id column.
        var usingFirst = () => ctx.MergeInto<IdentityStoreOnlyRowEntity>()
            .Using(new IdentityStoreOnlyRowEntity { Id = 1, Extra = new() { ["alpha"] = 1 } })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        usingFirst.Should().Throw<NotSupportedException>().WithMessage("*is not a column of the VALUES-derived merge source*");

        var onFirst = () => ctx.MergeInto<IdentityStoreOnlyRowEntity>()
            .On((t, s) => t.Id == s.Id)
            .Using(new IdentityStoreOnlyRowEntity { Id = 1, Extra = new() { ["alpha"] = 1 } })
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        onFirst.Should().Throw<NotSupportedException>().WithMessage("*is not a column of the VALUES-derived merge source*");
    }

    [Fact]
    public void Merge_StoreOnlyEntity_EmptyStore_ShouldThrow()
    {
        using var ctx = new TestContext();

        // A store-only entity whose store is empty (or null) supplies no dynamic column, so a key upsert
        // has nothing to update: it must report the missing non-key column, not the misleading
        // "No source rows were specified" guard. The mapped key means the source is not actually absent.
        var emptyStore = () => ctx.MergeInto<StoreOnlyRowEntity>()
            .Using(new StoreOnlyRowEntity { Id = 1, Extra = new() })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        var emptyEx = emptyStore.Should().Throw<NotSupportedException>().Which;
        emptyEx.Message.Should().NotContain("No source rows");

        var nullStore = () => ctx.MergeInto<StoreOnlyRowEntity>()
            .Using(new StoreOnlyRowEntity { Id = 2, Extra = null! })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        var nullEx = nullStore.Should().Throw<NotSupportedException>().Which;
        nullEx.Message.Should().NotContain("No source rows");
    }

    [Fact]
    public void Merge_StoreOnlyEntity_NonGeneratedKey_OnSourceKey_ShouldRender()
    {
        using var ctx = new TestContext();
        var entity = new StoreOnlyRowEntity { Id = 1, Extra = new() { ["alpha"] = 1 } };

        // The store-only entity's key is not database-generated, so it IS carried by the
        // VALUES-derived source and an explicit On(...) match on it is expressible. The former On(...)
        // guard rejected every store-only entity (the store supplied no mapped selection) even though
        // this match is valid; it has been removed and the guard now lives in BuildCommand, which only
        // rejects source columns absent from the derived source (see the identity case below).
        var sql = ctx.MergeInto<StoreOnlyRowEntity>()
            .Using(entity)
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        // The dynamic key extends the derived source and both write lists...
        sql.Should().Contain("as source (Id, \"alpha\")");
        sql.Should().Contain("then update set target.\"alpha\" = source.\"alpha\"");
        sql.Should().Contain("then insert (Id, \"alpha\") values (source.Id, source.\"alpha\")");

        // ...while the ON match stays on the declared key, never a dynamic key column.
        var onStart = sql.IndexOf(" on ", StringComparison.Ordinal) + 4;
        var whenStart = sql.IndexOf(" when ", StringComparison.Ordinal);
        var on = sql[onStart..whenStart];
        on.Should().Be("target.Id = source.Id");
        on.Should().NotContain("\"alpha\"");
    }

    private static DynamicRowEntity DynamicRow(int id, string name, int alpha, string beta) => new()
    {
        Id = id,
        Name = name,
        // Deliberately beta before alpha: the writer ordinal-sorts the keys.
        Extra = new Dictionary<string, object?> { ["beta"] = beta, ["alpha"] = alpha },
    };
}

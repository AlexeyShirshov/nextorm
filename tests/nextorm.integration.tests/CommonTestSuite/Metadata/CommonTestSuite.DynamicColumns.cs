using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Cross-provider integration tests for the write side of the dynamic-columns store (issue #104): an
/// entity whose <see cref="DynamicColumnsAttribute"/> dictionary contributes physical columns is
/// inserted, read back and updated against a real database. UPDATE and MERGE are gated by an explicit
/// dialect capability so an unsupported provider is skipped, never silently passed.
/// <para>
/// The physical table (<c>dynamic_entity</c>) carries <c>alpha</c>, <c>beta</c> and the defaulted
/// <c>seeded</c> column, so a key omitted from the store is distinguishable from a key bound to a
/// NULL: an omitted key keeps its stored value (or takes the column default on INSERT), never NULL.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    private static int DynamicKey() => Random.Shared.Next(1_000_000, int.MaxValue);

    private static DynamicColumnsEntity DynamicRow(int id, string name, params (string Key, object? Value)[] extra)
    {
        var row = new DynamicColumnsEntity { Id = id, Name = name };
        foreach (var (key, value) in extra)
            row.Extra[key] = value;

        return row;
    }

    [Fact]
    public void DynamicColumns_Insert_ShouldPersistKeysAndReadBack()
    {
        Assert.SkipUnless(Provider.SupportsDynamicColumnsRead, Provider.DynamicColumnsReadSkipReason);

        var ctx = _sut.DataProvider;
        var id = DynamicKey();

        // Deliberately not ordinal-sorted: the writer sorts the physical keys itself.
        var row = DynamicRow(id, "first",
            ("seeded", "explicit"),
            ("beta", "B"),
            ("alpha", "A"));

        ctx.CreateInsertBuilder<DynamicColumnsEntity>().Values(row).Insert().Should().Be(1);

        var read = ctx.From<DynamicColumnsEntity>().ToList().Single(x => x.Id == id);
        read.Name.Should().Be("first");
        read.Extra["alpha"].Should().Be("A");
        read.Extra["beta"].Should().Be("B");
        read.Extra["seeded"].Should().Be("explicit");
        read.Extra.Should().NotContainKey("id");
        read.Extra.Should().NotContainKey("name");
    }

    [Fact]
    public void DynamicColumns_Insert_OmittedDefaultedColumn_ShouldUseTableDefault()
    {
        Assert.SkipUnless(Provider.SupportsDynamicColumnsRead, Provider.DynamicColumnsReadSkipReason);

        var ctx = _sut.DataProvider;
        var id = DynamicKey();

        // "seeded" is omitted from the store, so the table default ("defaulted") must apply. This is the
        // difference between a missing key and a key explicitly bound to a value.
        ctx.CreateInsertBuilder<DynamicColumnsEntity>()
            .Values(DynamicRow(id, "default", ("alpha", "A")))
            .Insert()
            .Should().Be(1);

        var read = ctx.From<DynamicColumnsEntity>().ToList().Single(x => x.Id == id);
        read.Extra["alpha"].Should().Be("A");
        read.Extra["seeded"].Should().Be("defaulted");
    }

    [Fact]
    public void DynamicColumns_Insert_ColumnOrderShouldBeIndependentOfDictionaryOrder()
    {
        Assert.SkipUnless(Provider.SupportsDynamicColumnsRead, Provider.DynamicColumnsReadSkipReason);

        var ctx = _sut.DataProvider;

        var forward = DynamicRow(1, "row", ("alpha", "A"), ("beta", "B"), ("seeded", "S"));
        var reverse = DynamicRow(2, "row", ("seeded", "S"), ("beta", "B"), ("alpha", "A"));

        var forwardSql = ctx.CreateInsertBuilder<DynamicColumnsEntity>().Values(forward).ToSql();
        var reverseSql = ctx.CreateInsertBuilder<DynamicColumnsEntity>().Values(reverse).ToSql();

        // Identical text proves the physical column order comes from the ordinal-sorted key set, not
        // from the dictionary's insertion order.
        reverseSql.Should().Be(forwardSql);
        forwardSql.IndexOf("alpha", StringComparison.Ordinal)
            .Should().BeLessThan(forwardSql.IndexOf("beta", StringComparison.Ordinal));
        forwardSql.IndexOf("beta", StringComparison.Ordinal)
            .Should().BeLessThan(forwardSql.IndexOf("seeded", StringComparison.Ordinal));
    }

    [Fact]
    public void DynamicColumns_Update_OmittedKeyShouldKeepStoredValue()
    {
        Assert.SkipUnless(Provider.SupportsDynamicColumnsRead, Provider.DynamicColumnsReadSkipReason);

        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        Assert.SkipUnless(dialect.SupportsUpdate, "This provider does not support UPDATE.");

        var id = DynamicKey();
        ctx.CreateInsertBuilder<DynamicColumnsEntity>()
            .Values(DynamicRow(id, "before", ("alpha", "keep"), ("beta", "old"), ("seeded", "kept")))
            .Insert()
            .Should().Be(1);

        // "alpha" and "seeded" are omitted from the update store: they must keep their stored values,
        // never the column default and never NULL.
        ctx.CreateUpdateBuilder<DynamicColumnsEntity>()
            .Set(DynamicRow(id, "after", ("beta", "new")))
            .Where(x => x.Id == id)
            .Update()
            .Should().Be(1);

        var read = ctx.From<DynamicColumnsEntity>().ToList().Single(x => x.Id == id);
        read.Name.Should().Be("after");
        read.Extra["beta"].Should().Be("new");
        read.Extra["alpha"].Should().Be("keep");
        read.Extra["seeded"].Should().Be("kept");
    }

    [Fact]
    public void DynamicColumns_MergeKeyUpsert_ShouldInsertThenUpdateKeys()
    {
        Assert.SkipUnless(Provider.SupportsDynamicColumnsRead, Provider.DynamicColumnsReadSkipReason);

        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        Assert.SkipUnless(
            dialect.SupportsMerge || dialect.SupportsOnConflict || dialect.SupportsOnDuplicateKey,
            "This provider has no key-upsert form (ON CONFLICT / ON DUPLICATE KEY / MERGE).");

        var id = DynamicKey();

        // Not matched -> INSERT branch; the dynamic keys extend the written column list.
        ctx.CreateMergeBuilder<DynamicColumnsEntity>()
            .Using(DynamicRow(id, "merged", ("alpha", "a1"), ("seeded", "s1")))
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        var inserted = ctx.From<DynamicColumnsEntity>().ToList().Single(x => x.Id == id);
        inserted.Name.Should().Be("merged");
        inserted.Extra["alpha"].Should().Be("a1");
        inserted.Extra["seeded"].Should().Be("s1");

        // Matched -> UPDATE branch; the keys present in the source are refreshed.
        ctx.CreateMergeBuilder<DynamicColumnsEntity>()
            .Using(DynamicRow(id, "merged2", ("alpha", "a2"), ("seeded", "s2")))
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        var updated = ctx.From<DynamicColumnsEntity>().ToList().Single(x => x.Id == id);
        updated.Name.Should().Be("merged2");
        updated.Extra["alpha"].Should().Be("a2");
        updated.Extra["seeded"].Should().Be("s2");
    }

    [Fact]
    public void DynamicColumns_FullMerge_ShouldInsertThenUpdateKeys()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;

        // SQLite has only the key-upsert form and ClickHouse no upsert at all, so a general multi-branch
        // MERGE is explicitly skipped there instead of being attempted.
        Assert.SkipUnless(dialect.SupportsMergeStatement, "This provider cannot render a general multi-branch MERGE.");

        var id = DynamicKey();

        ctx.CreateMergeBuilder<DynamicColumnsEntity>()
            .Using(DynamicRow(id, "full", ("beta", "b1"), ("seeded", "s1")))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        var inserted = ctx.From<DynamicColumnsEntity>().ToList().Single(x => x.Id == id);
        inserted.Name.Should().Be("full");
        inserted.Extra["beta"].Should().Be("b1");
        inserted.Extra["seeded"].Should().Be("s1");

        ctx.CreateMergeBuilder<DynamicColumnsEntity>()
            .Using(DynamicRow(id, "full2", ("beta", "b2"), ("seeded", "s2")))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        var updated = ctx.From<DynamicColumnsEntity>().ToList().Single(x => x.Id == id);
        updated.Name.Should().Be("full2");
        updated.Extra["beta"].Should().Be("b2");
        updated.Extra["seeded"].Should().Be("s2");
    }
}

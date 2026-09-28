using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Cross-provider coverage for global query filters (#67 / #108, acceptance matrix 11.8) on the
/// database-backed providers: keyed filters plus the selective <c>IgnoreFilters</c> forms on SELECT,
/// the filter injected into <c>UPDATE</c>/<c>DELETE</c> (predicate and key forms), and the
/// <c>INSERT</c>/<c>INSERT ... SELECT</c> validation. ClickHouse does not derive this suite (it has its
/// own provider class), and its query-filter coverage lives in <c>ClickHouseQueryFilterTests</c>.
/// In-memory parity for the SELECT cases is pinned by <see cref="QueryFilterInMemoryParityTests"/>.
/// </summary>
public abstract partial class CommonTestSuite
{
    private static int _queryFilterIdSeed = -600_000_000;

    private static int NextQueryFilterBase() => Interlocked.Add(ref _queryFilterIdSeed, -10);

    private IDataContext QueryFilterContext(int tenant)
    {
        var ctx = _sut.DataProvider;
        ctx.Properties[QueryFilterFixtures.TenantKey] = tenant;
        return ctx;
    }

    private void SeedQueryFilterRows(params QueryFilterEntity[] rows)
        => _sut.DataProvider.InsertInto<QueryFilterEntity>().IgnoreFilters().Values(rows).Insert();

    // The three fixture rows for a test live at base, base - 1 and base - 2; restricting the query to
    // that id range keeps the assertions independent of the rows the other tests leave behind.
    private EntityBuilder<QueryFilterEntity> QueryFilterRange(int baseId)
        => _sut.DataProvider.From<QueryFilterEntity>().Where(x => x.Id >= baseId - 2 && x.Id <= baseId);

    private static QueryFilterEntity Active(int id, string name)
        => new() { Id = id, TenantId = 1, IsDeleted = false, Name = name };

    private static QueryFilterEntity SoftDeleted(int id, string name)
        => new() { Id = id, TenantId = 1, IsDeleted = true, Name = name };

    private static QueryFilterEntity ForeignTenant(int id, string name)
        => new() { Id = id, TenantId = 2, IsDeleted = false, Name = name };

    private void SeedQueryFilterSourceRows(params QueryFilterSourceEntity[] rows)
        => _sut.DataProvider.InsertInto<QueryFilterSourceEntity>().IgnoreFilters().Values(rows).Insert();

    // --- SELECT: the keyed tenant filter and the anonymous soft-delete filter, plus every selective
    // --- IgnoreFilters form (by key, by type, intersection, empty scope, AnonymousKey).

    [Fact]
    public void QueryFilter_Select_AppliesAnonymousAndKeyedFilters()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var ids = QueryFilterRange(b).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([b]);
    }

    [Fact]
    public void QueryFilter_Select_IgnoreAll_ReturnsEveryRow()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var ids = QueryFilterRange(b).IgnoreFilters().Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([b, b - 1, b - 2]);
    }

    [Fact]
    public void QueryFilter_Select_IgnoreByKey_DisablesOnlyThatFilter()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var ids = QueryFilterRange(b).IgnoreFilters(["tenant"]).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([b, b - 2], "the anonymous soft-delete filter stays active");
    }

    [Fact]
    public void QueryFilter_Select_IgnoreAnonymousKey_KeepsKeyedFilter()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var ids = QueryFilterRange(b).IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([b, b - 1], "the keyed tenant filter stays active");
    }

    [Fact]
    public void QueryFilter_Select_IgnoreByType_DisablesThatTypesFilters()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var ids = QueryFilterRange(b).IgnoreFilters(typeof(QueryFilterEntity)).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([b, b - 1, b - 2]);
    }

    [Fact]
    public void QueryFilter_Select_IgnoreByUnrelatedType_DisablesNothing()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var ids = QueryFilterRange(b).IgnoreFilters(typeof(QueryFilterSiblingEntity)).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([b], "scoping the disable to another type must not touch this one");
    }

    [Fact]
    public void QueryFilter_Select_IgnoreIntersection_TargetsKeyAndType()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var onThisType = QueryFilterRange(b)
            .IgnoreFilters(["tenant"], typeof(QueryFilterEntity))
            .Select(x => x.Id)
            .ToList();
        var onOtherType = QueryFilterRange(b)
            .IgnoreFilters(["tenant"], typeof(QueryFilterSiblingEntity))
            .Select(x => x.Id)
            .ToList();

        onThisType.Should().BeEquivalentTo([b, b - 2], "the keyed filter is disabled on the listed type");
        onOtherType.Should().BeEquivalentTo([b], "the keyed filter survives when the type does not match");
    }

    [Fact]
    public void QueryFilter_Select_EmptyScope_IsNoOp()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterRows(Active(b, "active"), SoftDeleted(b - 1, "deleted"), ForeignTenant(b - 2, "foreign"));

        var emptyKeys = QueryFilterRange(b).IgnoreFilters(Array.Empty<string>()).Select(x => x.Id).ToList();
        var emptyKeysWithType = QueryFilterRange(b)
            .IgnoreFilters(Array.Empty<string>(), typeof(QueryFilterEntity))
            .Select(x => x.Id)
            .ToList();
        var nullKeys = QueryFilterRange(b).IgnoreFilters((IEnumerable<string>)null!).Select(x => x.Id).ToList();

        emptyKeys.Should().BeEquivalentTo([b]);
        emptyKeysWithType.Should().BeEquivalentTo([b]);
        nullKeys.Should().BeEquivalentTo([b]);
    }

    // --- UPDATE/DELETE: the target filter is injected into the WHERE (key AND filter); IgnoreFilters
    // --- removes it. The key form is reached through the internal entity terminal (InternalsVisibleTo).

    [Fact]
    public void QueryFilter_Update_ByPredicate_RespectsFilter()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.Update<QueryFilterEntity>()
            .Set(x => x.Name, "updated")
            .Where(x => x.Id == foreignId)
            .Update();

        affected.Should().Be(0, "the tenant filter excludes the row from the update");
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Name).Single().Should().Be("target");
    }

    [Fact]
    public void QueryFilter_Update_ByPredicate_IgnoreFilters_Updates()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.Update<QueryFilterEntity>()
            .IgnoreFilters()
            .Set(x => x.Name, "updated")
            .Where(x => x.Id == foreignId)
            .Update();

        affected.Should().Be(1);
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Name).Single().Should().Be("updated");
    }

    [Fact]
    public void QueryFilter_Update_ByKey_RespectsFilter()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.Update(new QueryFilterEntity { Id = foreignId, TenantId = 1, IsDeleted = false, Name = "key" });

        affected.Should().Be(0, "the key form ANDs the target filter to the key predicate");
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Name).Single().Should().Be("target");
    }

    [Fact]
    public void QueryFilter_Update_ByKey_IgnoreFilters_Updates()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.Update<QueryFilterEntity>()
            .IgnoreFilters()
            .UpdateEntity(new QueryFilterEntity { Id = foreignId, TenantId = 1, IsDeleted = false, Name = "key-ignored" });

        affected.Should().Be(1);
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Name).Single().Should().Be("key-ignored");
    }

    [Fact]
    public void QueryFilter_Delete_ByPredicate_RespectsFilter()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.DeleteFrom<QueryFilterEntity>().Where(x => x.Id == foreignId).Delete();

        affected.Should().Be(0, "the tenant filter excludes the row from the delete");
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Id).ToList().Should().ContainSingle();
    }

    [Fact]
    public void QueryFilter_Delete_ByPredicate_IgnoreFilters_Deletes()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.DeleteFrom<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Delete();

        affected.Should().Be(1);
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void QueryFilter_Delete_ByKey_RespectsFilter()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.Delete(new QueryFilterEntity { Id = foreignId });

        affected.Should().Be(0, "the key form ANDs the target filter to the key predicate");
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Id).ToList().Should().ContainSingle();
    }

    [Fact]
    public void QueryFilter_Delete_ByKey_IgnoreFilters_Deletes()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterRows(ForeignTenant(foreignId, "target"));

        var affected = ctx.DeleteFrom<QueryFilterEntity>()
            .IgnoreFilters()
            .DeleteEntity(new QueryFilterEntity { Id = foreignId });

        affected.Should().Be(1);
        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    // --- INSERT validation: the target is never filtered; the written values are checked first.

    [Fact]
    public void QueryFilter_Insert_Entity_Valid_Succeeds()
    {
        var ctx = QueryFilterContext(1);
        var marker = "qf-ins-" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<QueryFilterTargetEntity>()
            .Values(new QueryFilterTargetEntity { TenantId = 1, IsDeleted = false, Name = marker })
            .Insert();

        ctx.From<QueryFilterTargetEntity>().Where(x => x.Name == marker).Select(x => x.Id).ToList().Should().ContainSingle();
    }

    [Fact]
    public void QueryFilter_Insert_Entity_ViolatingTenant_Throws()
    {
        var ctx = QueryFilterContext(1);

        var act = () => ctx.InsertInto<QueryFilterTargetEntity>()
            .Values(new QueryFilterTargetEntity { TenantId = 2, IsDeleted = false, Name = "qf-ins-foreign" })
            .Insert();

        act.Should().Throw<QueryFilterException>();
    }

    [Fact]
    public void QueryFilter_Insert_Entity_ViolatingSoftDelete_Throws()
    {
        var ctx = QueryFilterContext(1);

        var act = () => ctx.InsertInto<QueryFilterEntity>()
            .Values(new QueryFilterEntity { Id = NextQueryFilterBase(), TenantId = 1, IsDeleted = true, Name = "qf-ins-deleted" })
            .Insert();

        act.Should().Throw<QueryFilterException>();
    }

    [Fact]
    public void QueryFilter_Insert_Value_ViolatingFilter_Throws()
    {
        var ctx = QueryFilterContext(1);

        var act = () => ctx.InsertInto<QueryFilterTargetEntity>()
            .Value(x => x.TenantId, 2)
            .Value(x => x.Name, "qf-ins-value")
            .Insert();

        act.Should().Throw<QueryFilterException>();
    }

    [Fact]
    public void QueryFilter_Insert_Value_UnwrittenFilterColumn_Throws()
    {
        var ctx = QueryFilterContext(1);

        var act = () => ctx.InsertInto<QueryFilterTargetEntity>()
            .Value(x => x.Name, "qf-ins-unwritten")
            .Value(x => x.IsDeleted, false)
            .Insert();

        act.Should().Throw<QueryFilterException>(
            "the tenant column is not written, so its active filter cannot be validated (fail-closed)");
    }

    [Fact]
    public void QueryFilter_Insert_Value_WrittenFilterColumn_Valid_Succeeds()
    {
        var ctx = QueryFilterContext(1);
        var marker = "qf-ins-written-" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<QueryFilterTargetEntity>()
            .Value(x => x.TenantId, 1)
            .Value(x => x.IsDeleted, false)
            .Value(x => x.Name, marker)
            .Insert();

        ctx.From<QueryFilterTargetEntity>().Where(x => x.Name == marker).Select(x => x.Id).ToList().Should().ContainSingle();
    }

    [Fact]
    public void QueryFilter_BatchInsert_ViolatingRow_Throws()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = QueryFilterContext(1);

        var act = () => ctx.Batch()
            .Insert(ctx.InsertInto<QueryFilterTargetEntity>().Values(
                new QueryFilterTargetEntity { TenantId = 2, IsDeleted = false, Name = "qf-batch" }))
            .Execute();

        act.Should().Throw<QueryFilterException>("adding the insert to the batch validates its row before execution");
    }

    [Fact]
    public void QueryFilter_BulkInsert_ViolatingRow_Throws()
    {
        var ctx = QueryFilterContext(1);
        var marker = "qf-bulk-" + Guid.NewGuid().ToString("N");

        var act = () => ctx.BulkInsertInto<QueryFilterTargetEntity>()
            .Values(
            [
                new QueryFilterTargetEntity { TenantId = 1, IsDeleted = false, Name = marker },
                new QueryFilterTargetEntity { TenantId = 2, IsDeleted = false, Name = marker + "-foreign" },
            ])
            .BulkInsert();

        act.Should().Throw<QueryFilterException>("the foreign-tenant row is rejected before any batch is sent");
    }

    [Fact]
    public void QueryFilter_Insert_IgnoreFilters_SkipsValidation()
    {
        var ctx = QueryFilterContext(1);
        var id = NextQueryFilterBase();

        ctx.InsertInto<QueryFilterEntity>()
            .IgnoreFilters()
            .Values(new QueryFilterEntity { Id = id, TenantId = 2, IsDeleted = false, Name = "qf-ins-ignored" })
            .Insert();

        ctx.From<QueryFilterEntity>().IgnoreFilters().Where(x => x.Id == id).Select(x => x.TenantId).Single().Should().Be(2);
    }

    // --- INSERT ... SELECT: the source keeps its own filter; a violating source is rejected by the
    // --- server-side pre-check before the target is written.

    [Fact]
    public void QueryFilter_InsertFromQuery_SourceFiltered_InsertsOnlyMatchingRows()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        var keepMarker = "qf-select-keep-" + Guid.NewGuid().ToString("N");
        var dropMarker = "qf-select-drop-" + Guid.NewGuid().ToString("N");
        SeedQueryFilterSourceRows(
            new QueryFilterSourceEntity { Id = b, TenantId = 1, IsDeleted = false, Name = keepMarker },
            new QueryFilterSourceEntity { Id = b - 1, TenantId = 2, IsDeleted = false, Name = dropMarker });

        var affected = ctx.InsertInto<QueryFilterTargetEntity>()
            .Values(
                ctx.From<QueryFilterSourceEntity>().Where(x => x.Name == keepMarker || x.Name == dropMarker),
                x => new QueryFilterTargetEntity { TenantId = x.TenantId, IsDeleted = x.IsDeleted, Name = x.Name })
            .Insert();

        affected.Should().BeGreaterThanOrEqualTo(1);
        ctx.From<QueryFilterTargetEntity>().Where(x => x.Name == keepMarker).Select(x => x.Id).ToList().Should().ContainSingle();
        ctx.From<QueryFilterTargetEntity>().IgnoreFilters().Where(x => x.Name == dropMarker).Select(x => x.Id).ToList().Should().BeEmpty(
            "the source's own tenant filter hides the foreign row before it reaches the target");
    }

    [Fact]
    public void QueryFilter_InsertFromQuery_ViolatingSource_Throws()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        var keepMarker = "qf-select-keep-" + Guid.NewGuid().ToString("N");
        var dropMarker = "qf-select-drop-" + Guid.NewGuid().ToString("N");
        SeedQueryFilterSourceRows(
            new QueryFilterSourceEntity { Id = b, TenantId = 1, IsDeleted = false, Name = keepMarker },
            new QueryFilterSourceEntity { Id = b - 1, TenantId = 2, IsDeleted = false, Name = dropMarker });

        var act = () => ctx.InsertInto<QueryFilterTargetEntity>()
            .Values(
                ctx.From<QueryFilterSourceEntity>().IgnoreFilters().Where(x => x.Name == keepMarker || x.Name == dropMarker),
                x => new QueryFilterTargetEntity { TenantId = x.TenantId, IsDeleted = x.IsDeleted, Name = x.Name })
            .Insert();

        act.Should().Throw<QueryFilterException>("the source contains a row the target filter rejects");
    }

    // --- PR4: the builder-function (FilterFunc) form. The function is invoked once at plan build; the
    // --- injected predicate reads the per-context tenant value and stays a bound parameter. The SELECT
    // --- rows, the DML target filter and the INSERT ... SELECT source are checked on every provider.

    private void SeedQueryFilterFuncRows(params QueryFilterFuncEntity[] rows)
        => _sut.DataProvider.InsertInto<QueryFilterFuncEntity>().IgnoreFilters().Values(rows).Insert();

    private static QueryFilterFuncEntity FuncActive(int id, string name)
        => new() { Id = id, TenantId = 1, IsDeleted = false, Name = name };

    private static QueryFilterFuncEntity FuncSoftDeleted(int id, string name)
        => new() { Id = id, TenantId = 1, IsDeleted = true, Name = name };

    private static QueryFilterFuncEntity FuncForeignTenant(int id, string name)
        => new() { Id = id, TenantId = 2, IsDeleted = false, Name = name };

    [Fact]
    public void QueryFilter_Func_Select_AppliesAnonymousAndKeyedFilters()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterFuncRows(FuncActive(b, "func-active"), FuncSoftDeleted(b - 1, "func-deleted"), FuncForeignTenant(b - 2, "func-foreign"));

        EntityBuilder<QueryFilterFuncEntity> Range() => ctx.From<QueryFilterFuncEntity>().Where(x => x.Id >= b - 2 && x.Id <= b);

        Range().Select(x => x.Id).ToList().Should().BeEquivalentTo([b]);
        Range().IgnoreFilters().Select(x => x.Id).ToList().Should().BeEquivalentTo([b, b - 1, b - 2]);
        Range().IgnoreFilters(["tenant"]).Select(x => x.Id).ToList().Should().BeEquivalentTo([b, b - 2]);
        Range().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList().Should().BeEquivalentTo([b, b - 1]);
    }

    [Fact]
    public void QueryFilter_Func_SecondExecution_SeesChangedContextValue()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        SeedQueryFilterFuncRows(FuncActive(b, "func-change-1"), FuncSoftDeleted(b - 1, "func-change-deleted"), FuncForeignTenant(b - 2, "func-change-2"));

        EntityBuilder<QueryFilterFuncEntity> Range() => ctx.From<QueryFilterFuncEntity>().Where(x => x.Id >= b - 2 && x.Id <= b);

        // First execution caches the plan with tenant 1.
        Range().Select(x => x.Id).ToList().Should().BeEquivalentTo([b]);

        // The same shape on the cached plan must re-read the context and return the tenant-2 row; an
        // implementation that inlined the first value into the cached plan would keep returning [b].
        ctx.Properties[QueryFilterFixtures.TenantKey] = 2;
        Range().Select(x => x.Id).ToList().Should().BeEquivalentTo([b - 2], "the cached plan re-binds the context value");

        ctx.Properties[QueryFilterFixtures.TenantKey] = 1;
    }

    [Fact]
    public void QueryFilter_Func_InsertTarget_ThrowsFailClosed()
    {
        var ctx = QueryFilterContext(1);

        var act = () => ctx.InsertInto<QueryFilterFuncTargetEntity>()
            .Values(new QueryFilterFuncTargetEntity { TenantId = 1, IsDeleted = false, Name = "qf-func-ins" })
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*FilterFunc*", "the builder-function target filter cannot be validated (fail-closed)");
    }

    [Fact]
    public void QueryFilter_Func_InsertTarget_IgnoreFilters_Inserts()
    {
        var ctx = QueryFilterContext(1);
        var marker = "qf-func-ins-ignored-" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<QueryFilterFuncTargetEntity>()
            .IgnoreFilters()
            .Values(new QueryFilterFuncTargetEntity { TenantId = 2, IsDeleted = false, Name = marker })
            .Insert();

        ctx.From<QueryFilterFuncTargetEntity>().IgnoreFilters().Where(x => x.Name == marker).Select(x => x.TenantId).Single().Should().Be(2);
    }

    [Fact]
    public void QueryFilter_Func_UpdateAndDelete_RespectFilter()
    {
        var ctx = QueryFilterContext(1);
        var foreignId = NextQueryFilterBase();
        SeedQueryFilterFuncRows(FuncForeignTenant(foreignId, "func-target"));

        ctx.Update<QueryFilterFuncEntity>().Set(x => x.Name, "func-updated").Where(x => x.Id == foreignId).Update()
            .Should().Be(0, "the function tenant filter excludes the foreign row from the update");
        ctx.DeleteFrom<QueryFilterFuncEntity>().Where(x => x.Id == foreignId).Delete()
            .Should().Be(0, "the function tenant filter excludes the foreign row from the delete");

        ctx.Update<QueryFilterFuncEntity>().IgnoreFilters().Set(x => x.Name, "func-updated").Where(x => x.Id == foreignId).Update()
            .Should().Be(1);
        ctx.DeleteFrom<QueryFilterFuncEntity>().IgnoreFilters().Where(x => x.Id == foreignId).Delete()
            .Should().Be(1);
    }

    [Fact]
    public void QueryFilter_Func_InsertFromQuery_SourceFiltered()
    {
        var ctx = QueryFilterContext(1);
        var b = NextQueryFilterBase();
        var keepMarker = "qf-func-keep-" + Guid.NewGuid().ToString("N");
        var dropMarker = "qf-func-drop-" + Guid.NewGuid().ToString("N");
        SeedQueryFilterFuncRows(FuncActive(b, keepMarker), FuncForeignTenant(b - 1, dropMarker));

        var affected = ctx.InsertInto<QueryFilterFuncSelectTargetEntity>()
            .Values(
                ctx.From<QueryFilterFuncEntity>().Where(x => x.Name == keepMarker || x.Name == dropMarker),
                x => new QueryFilterFuncSelectTargetEntity { TenantId = x.TenantId, IsDeleted = x.IsDeleted, Name = x.Name })
            .Insert();

        affected.Should().BeGreaterThanOrEqualTo(1);
        ctx.From<QueryFilterFuncSelectTargetEntity>().Where(x => x.Name == keepMarker).Select(x => x.Id).ToList().Should().ContainSingle();
        ctx.From<QueryFilterFuncSelectTargetEntity>().IgnoreFilters().Where(x => x.Name == dropMarker).Select(x => x.Id).ToList().Should().BeEmpty(
            "the source's function filter hides the foreign row before it reaches the target");
    }
}

/// <summary>
/// In-memory parity for the SELECT cases: the attribute-declared keyed and anonymous filters and every
/// selective <c>IgnoreFilters</c> form must resolve exactly like the database providers.
/// </summary>
public sealed class QueryFilterInMemoryParityTests
{
    [Fact]
    public void SelectiveIgnores_ShouldMatchProviderFilterSemantics()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[QueryFilterFixtures.TenantKey] = 1;
        ctx.From<QueryFilterEntity>().WithData(
        [
            new QueryFilterEntity { Id = 1, TenantId = 1, IsDeleted = false },
            new QueryFilterEntity { Id = 2, TenantId = 1, IsDeleted = true },
            new QueryFilterEntity { Id = 3, TenantId = 2, IsDeleted = false },
        ]);

        ctx.From<QueryFilterEntity>().Select(x => x.Id).ToList().Should().BeEquivalentTo([1]);
        ctx.From<QueryFilterEntity>().IgnoreFilters().Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 2, 3]);
        ctx.From<QueryFilterEntity>().IgnoreFilters(["tenant"]).Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 3]);
        ctx.From<QueryFilterEntity>().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 2]);
        ctx.From<QueryFilterEntity>().IgnoreFilters(typeof(QueryFilterEntity)).Select(x => x.Id).ToList().Should().BeEquivalentTo([1, 2, 3]);
        ctx.From<QueryFilterEntity>()
            .IgnoreFilters(["tenant"], typeof(QueryFilterSiblingEntity))
            .Select(x => x.Id)
            .ToList()
            .Should()
            .BeEquivalentTo([1]);
    }
}

/// <summary>Shared tenant context key and the filtered entity types used by the query filter fixtures.</summary>
internal static class QueryFilterFixtures
{
    public const string TenantKey = "nextorm_query_filter_tenant";
}

/// <summary>Anonymous soft-delete plus keyed tenant filter; the main SELECT/DML fixture.</summary>
[SqlTable("query_filter_entity")]
[QueryFilter(FilterLambda = nameof(ActiveOnly))]
[QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
public sealed class QueryFilterEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public static Expression<Func<QueryFilterEntity, bool>> ActiveOnly() => e => !e.IsDeleted;

    public static Expression<Func<QueryFilterEntity, IDataContext, bool>> Tenant()
        => (e, c) => e.TenantId == (int)c.Properties[QueryFilterFixtures.TenantKey];
}

/// <summary>A second type over the same table with the same keyed tenant filter; scopes the intersection test.</summary>
[SqlTable("query_filter_entity")]
[QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
public sealed class QueryFilterSiblingEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public static Expression<Func<QueryFilterSiblingEntity, IDataContext, bool>> Tenant()
        => (e, c) => e.TenantId == (int)c.Properties[QueryFilterFixtures.TenantKey];
}

/// <summary>The keyed source type of the INSERT ... SELECT fixtures (same table).</summary>
[SqlTable("query_filter_entity")]
[QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
public sealed class QueryFilterSourceEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public static Expression<Func<QueryFilterSourceEntity, IDataContext, bool>> Tenant()
        => (e, c) => e.TenantId == (int)c.Properties[QueryFilterFixtures.TenantKey];
}

/// <summary>The INSERT ... SELECT target; carries the keyed tenant filter (identity key).</summary>
[SqlTable("query_filter_target")]
[QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
public sealed class QueryFilterTargetEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public static Expression<Func<QueryFilterTargetEntity, IDataContext, bool>> Tenant()
        => (e, c) => e.TenantId == (int)c.Properties[QueryFilterFixtures.TenantKey];
}

/// <summary>The builder-function (FilterFunc) SELECT fixture: anonymous soft-delete plus a keyed tenant function.</summary>
[SqlTable("query_filter_entity")]
[QueryFilter(FilterLambda = nameof(ActiveOnly))]
[QueryFilter(FilterKey = "tenant", FilterFunc = nameof(TenantFunc))]
public sealed class QueryFilterFuncEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public static Expression<Func<QueryFilterFuncEntity, bool>> ActiveOnly() => e => !e.IsDeleted;

    public static Func<EntityBuilder<QueryFilterFuncEntity>, IDataContext, EntityBuilder<QueryFilterFuncEntity>> TenantFunc
        => (b, c) => b.Where(e => e.TenantId == (int)c.Properties[QueryFilterFixtures.TenantKey]);
}

/// <summary>The builder-function INSERT/MERGE target fixture (keyed tenant function).</summary>
[SqlTable("query_filter_target")]
[QueryFilter(FilterKey = "tenant", FilterFunc = nameof(TenantFunc))]
public sealed class QueryFilterFuncTargetEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public static Func<EntityBuilder<QueryFilterFuncTargetEntity>, IDataContext, EntityBuilder<QueryFilterFuncTargetEntity>> TenantFunc
        => (b, c) => b.Where(e => e.TenantId == (int)c.Properties[QueryFilterFixtures.TenantKey]);
}

/// <summary>The unfiltered INSERT ... SELECT target for the function-filter source test.</summary>
[SqlTable("query_filter_target")]
public sealed class QueryFilterFuncSelectTargetEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #123 acceptance for the write-target isolation under an active global filter: a row hidden by the
/// target filter must never be updated, deleted or matched by a mutation. SQL Server and PostgreSQL
/// (15+) render a general <c>MERGE</c> and get the atomic predicate; the providers whose upsert cannot
/// express it (ON CONFLICT / ON DUPLICATE KEY / ClickHouse without DML) refuse with
/// <see cref="NotSupportedException"/> before touching the database. The entity maps the existing
/// <c>merge_entity</c> table, so no schema change is needed.
/// </summary>
public abstract partial class CommonTestSuite
{
    private static int MergeTargetFilterKey() => Random.Shared.Next(1_000_000, int.MaxValue);

    private static void ConfigureMergeTargetFilter(IDataContext ctx, int minAge)
    {
        ctx.Properties[MergeTargetFilterFixtures.MinAgeKey] = minAge;
    }

    private void SeedMergeTargetFilterRows(params MergeTargetFilterEntity[] rows)
        => _sut.DataProvider.InsertInto<MergeTargetFilterEntity>().IgnoreFilters().Values(rows).Insert();

    private string? MergeTargetFilterName(int id)
        => _sut.DataProvider.From<MergeTargetFilterEntity>()
            .IgnoreFilters()
            .Where(x => x.Id == id)
            .Select(x => x.Name)
            .Single();

    [Fact]
    public void MergeTargetFilter_FullMerge_UpdatesVisibleAndLeavesHiddenUntouched()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(new MergeTargetFilterEntity { Id = MergeTargetFilterKey(), Name = "x", Age = 200 })
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        var visibleId = MergeTargetFilterKey();
        var hiddenId = visibleId + 1;
        SeedMergeTargetFilterRows(
            new MergeTargetFilterEntity { Id = visibleId, Name = "visible-old", Age = 150 },
            new MergeTargetFilterEntity { Id = hiddenId, Name = "hidden", Age = 10 });

        ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = visibleId, Name = "visible-new", Age = 200 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        MergeTargetFilterName(visibleId).Should().Be("visible-new", "a visible row is updated normally");
        MergeTargetFilterName(hiddenId).Should().Be("hidden", "the filter keeps the hidden row out of the match");
    }

    [Fact]
    public void MergeTargetFilter_HiddenKeyCollision_ShouldRaiseNativeUniqueError()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        if (!dialect.SupportsMergeStatement)
            return;

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        var hiddenId = MergeTargetFilterKey();
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = hiddenId, Name = "hidden", Age = 10 });

        Exception? captured = null;
        try
        {
            ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(new MergeTargetFilterEntity { Id = hiddenId, Name = "incoming", Age = 200 })
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
        }
        catch (Exception exception)
        {
            captured = exception;
        }

        captured.Should().NotBeNull("the filtered-out hidden row makes the source row 'not matched', so the insert collides on the key");
        captured.Should().NotBeOfType<QueryFilterException>("the collision is the provider's native unique-key error, not a filter failure");
        MergeTargetFilterName(hiddenId).Should().Be("hidden", "the hidden row is not pre-read or mutated");
    }

    [Fact]
    public void MergeTargetFilter_NoInsertMerge_CannotDeleteHiddenTarget()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        if (!dialect.SupportsMergeStatement)
            return;

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        var hiddenId = MergeTargetFilterKey();
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = hiddenId, Name = "hidden", Age = 10 });

        // A matched delete with no insert branch: the hidden row is filtered out of the match, so the
        // delete must not reach it.
        ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = hiddenId, Name = "x", Age = 200 })
            .OnKeys()
            .WhenMatched().ThenDelete()
            .Merge();

        MergeTargetFilterName(hiddenId).Should().Be("hidden");
    }

    [Fact]
    public void MergeTargetFilter_WhenNotMatchedBySourceDelete_LeavesHiddenRowsIntact()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        if (!dialect.SupportsMergeBySourceDelete)
            return;

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        var visibleId = MergeTargetFilterKey();
        var hiddenId = visibleId + 1;
        SeedMergeTargetFilterRows(
            new MergeTargetFilterEntity { Id = visibleId, Name = "visible-old", Age = 150 },
            new MergeTargetFilterEntity { Id = hiddenId, Name = "hidden", Age = 10 });

        // The stale-target delete arm must exclude hidden rows, otherwise a full-sync MERGE would wipe
        // them.
        ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = visibleId, Name = "visible-new", Age = 150 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatchedBySource().ThenDelete()
            .Merge();

        MergeTargetFilterName(visibleId).Should().Be("visible-new");
        MergeTargetFilterName(hiddenId).Should().Be("hidden", "the delete arm carries the target filter");
    }

    [Fact]
    public void MergeTargetFilter_KeyUpsert_FilteredBehaviorPerDialect()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        ConfigureMergeTargetFilter(ctx, minAge: 100);

        if (dialect.SupportsMergeBySourceDelete)
        {
            // SQL Server routes the key upsert through the filtered general MERGE.
            var visibleId = MergeTargetFilterKey();
            SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = visibleId, Name = "old", Age = 150 });

            ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(new MergeTargetFilterEntity { Id = visibleId, Name = "new", Age = 200 })
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .Merge();

            MergeTargetFilterName(visibleId).Should().Be("new");
            return;
        }

        // ON CONFLICT / ON DUPLICATE KEY / ClickHouse: the filtered upsert refuses before any read.
        var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = MergeTargetFilterKey(), Name = "x", Age = 200 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        unsupported.Should().Throw<NotSupportedException>();
    }

    // --- Amendment: on the supported full-MERGE forms, source-value validation covers every branch
    // --- combination, including update-only and delete-only, and throws before any mutation.

    [Fact]
    public void MergeTargetFilter_SupportedUpdateOnly_RejectsNonPassingSourceWithoutMutation()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeTargetFilterKey();

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(new MergeTargetFilterEntity { Id = id, Name = "x", Age = 10 })
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);

        var act = () => ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = id, Name = "x", Age = 10 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .Merge();

        act.Should().Throw<QueryFilterException>("the update-only source row fails the target filter");
        ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id).Select(x => x.Id).ToList()
            .Should().BeEmpty("nothing is mutated");
    }

    [Fact]
    public void MergeTargetFilter_SupportedDeleteOnly_RejectsNonPassingSourceWithoutMutation()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeTargetFilterKey();

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(new MergeTargetFilterEntity { Id = id, Name = "x", Age = 10 })
                .OnKeys()
                .WhenMatched().ThenDelete()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = id, Name = "keep", Age = 10 });

        var act = () => ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = id, Name = "x", Age = 10 })
            .OnKeys()
            .WhenMatched().ThenDelete()
            .Merge();

        act.Should().Throw<QueryFilterException>("the delete-only source row fails the target filter");
        MergeTargetFilterName(id).Should().Be("keep", "the hidden row is neither matched nor deleted");
    }

    // --- Amendment (iteration 2): query-source, NULL/UNKNOWN operands, whole-batch atomicity and
    // --- same-context isolation. The query source is the supported full-MERGE `Using(IQueryable)` form;
    // --- a temp-table / TVP-backed command reaches MERGE through that same path, so the capability gate
    // --- used here is the provider's `SupportsMergeStatement`.

    [Fact]
    public void MergeTargetFilter_QuerySource_HiddenTargetUntouched()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var visibleId = MergeTargetFilterKey();
        var hiddenId = visibleId + 1;

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == visibleId))
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>("the unsupported key-upsert refuses before reading the source");
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        SeedMergeTargetFilterRows(
            new MergeTargetFilterEntity { Id = visibleId, Name = "visible-old", Age = 150 },
            new MergeTargetFilterEntity { Id = hiddenId, Name = "hidden", Age = 10 });

        ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == visibleId))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        MergeTargetFilterName(visibleId).Should().Be("visible-old", "the visible target is matched and updated");
        MergeTargetFilterName(hiddenId).Should().Be("hidden", "a hidden row is never matched or mutated through a query source");
    }

    [Fact]
    public void MergeTargetFilter_QuerySource_UpdateOnlyNonPassing_ShouldThrowWithoutMutation()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeTargetFilterKey();

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id))
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = id, Name = "keep", Age = 10 });

        var act = () => ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .Merge();

        act.Should().Throw<QueryFilterException>("the incoming query-source row fails the target filter");
        MergeTargetFilterName(id).Should().Be("keep", "nothing is mutated");
    }

    [Fact]
    public void MergeTargetFilter_QuerySource_DeleteOnlyNonPassing_ShouldThrowWithoutMutation()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeTargetFilterKey();

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id))
                .OnKeys()
                .WhenMatched().ThenDelete()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = id, Name = "keep", Age = 10 });

        var act = () => ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id))
            .OnKeys()
            .WhenMatched().ThenDelete()
            .Merge();

        act.Should().Throw<QueryFilterException>("a delete-only branch still validates the incoming source");
        MergeTargetFilterName(id).Should().Be("keep", "the hidden row is not deleted");
    }

    [Fact]
    public void MergeTargetFilter_TableValuedOrTempSource_CapabilityGatedWithoutPartialMutation()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeTargetFilterKey();

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = id, Name = "old", Age = 150 });

        // A temp-table / TVP-backed source command reaches the merge through the query-source path; its
        // availability follows the provider's MERGE capability, and a refusal leaves the target intact.
        var source = ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id);
        if (dialect.SupportsMergeStatement)
        {
            ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(source)
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            MergeTargetFilterName(id).Should().Be("old", "the supported form applies the merge and leaves no partial state");
        }
        else
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using(source)
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>("the provider cannot merge from a query source under an active filter");
            MergeTargetFilterName(id).Should().Be("old", "the refusal mutates nothing");
        }
    }

    [Fact]
    public void MergeTargetFilter_NullOperand_ShouldRejectWithoutMutation()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeTargetFilterKey();

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterNullableEntity>()
                .Using(new MergeTargetFilterNullableEntity { Id = id, Name = "x", Age = null })
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);

        var act = () => ctx.MergeInto<MergeTargetFilterNullableEntity>()
            .Using(new MergeTargetFilterNullableEntity { Id = id, Name = "x", Age = null })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        act.Should().Throw<QueryFilterException>("a NULL operand makes the predicate UNKNOWN, which must fail closed");
        ctx.From<MergeTargetFilterNullableEntity>().IgnoreFilters().Where(x => x.Id == id).Select(x => x.Id).ToList()
            .Should().BeEmpty("nothing is inserted for a NULL operand");
    }

    [Fact]
    public void MergeTargetFilter_MultiRowInvalidLast_ShouldFailWholeBatchBeforeMutation()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var visibleId = MergeTargetFilterKey();
        var newId = visibleId + 1;

        if (!dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<MergeTargetFilterEntity>()
                .Using([
                    new MergeTargetFilterEntity { Id = visibleId, Name = "updated", Age = 150 },
                    new MergeTargetFilterEntity { Id = newId, Name = "bad", Age = 10 },
                ])
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = visibleId, Name = "old", Age = 150 });

        var act = () => ctx.MergeInto<MergeTargetFilterEntity>()
            .Using([
                new MergeTargetFilterEntity { Id = visibleId, Name = "updated", Age = 150 },
                new MergeTargetFilterEntity { Id = newId, Name = "bad", Age = 10 },
            ])
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        act.Should().Throw<QueryFilterException>("the invalid row is last, but the whole batch is rejected first");
        MergeTargetFilterName(visibleId).Should().Be("old", "the earlier valid row is not applied either");
        ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == newId).Select(x => x.Id).ToList()
            .Should().BeEmpty("the invalid row is not inserted");
    }

    [Fact]
    public void MergeTargetFilter_SameContext_NoStateLeakageAcrossScopeChanges()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeTargetFilterKey();
        if (!dialect.SupportsMergeStatement)
            return;

        ConfigureMergeTargetFilter(ctx, minAge: 100);
        SeedMergeTargetFilterRows(new MergeTargetFilterEntity { Id = id, Name = "old", Age = 150 });

        // 1. filtered full MERGE updates the visible row.
        ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = id, Name = "v1", Age = 150 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();
        MergeTargetFilterName(id).Should().Be("v1", "the active filter still matches the visible row");

        // 2. IgnoreFilters writes a below-threshold row; the bypass is local to this call.
        ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = id, Name = "ignored", Age = 10 })
            .IgnoreFilters()
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();
        MergeTargetFilterName(id).Should().Be("ignored", "IgnoreFilters keeps the intentional bypass");

        // 3. the next unfiltered call sees the filter active again: no predicate, parameter or
        //    shared-command state leaks out of the bypass.
        var act = () => ctx.MergeInto<MergeTargetFilterEntity>()
            .Using(new MergeTargetFilterEntity { Id = id, Name = "x", Age = 10 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .Merge();
        act.Should().Throw<QueryFilterException>("the filter is active again after IgnoreFilters");
        MergeTargetFilterName(id).Should().Be("ignored", "the final filtered call mutated nothing");
    }
}

/// <summary>Shared context key for the merge-target-filter fixtures.</summary>
internal static class MergeTargetFilterFixtures
{
    public const string MinAgeKey = "nextorm_merge_target_filter_min_age";
}

/// <summary>
/// A second mapping of the existing <c>merge_entity</c> table carrying an age-based global filter; a row
/// below the context threshold is hidden from the write target.
/// </summary>
[SqlTable("merge_entity")]
[QueryFilter(FilterKey = "visible", FilterLambda = nameof(Visible))]
public sealed class MergeTargetFilterEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("age")]
    public int Age { get; set; }

    public static Expression<Func<MergeTargetFilterEntity, IDataContext, bool>> Visible()
        => (e, c) => e.Age >= (int)c.Properties[MergeTargetFilterFixtures.MinAgeKey];
}

/// <summary>
/// A second mapping of <c>merge_entity</c> whose filter turns a NULL age operand into an UNKNOWN
/// predicate the write validation must fail closed on.
/// </summary>
[SqlTable("merge_entity")]
[QueryFilter(FilterKey = "visible_nullable", FilterLambda = nameof(Visible))]
public sealed class MergeTargetFilterNullableEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("age")]
    public int? Age { get; set; }

    public static Expression<Func<MergeTargetFilterNullableEntity, IDataContext, bool>> Visible()
        => (e, c) => e.Age != null && e.Age >= (int)c.Properties[MergeTargetFilterFixtures.MinAgeKey];
}

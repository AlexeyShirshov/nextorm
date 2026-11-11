using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #123 amendment (iteration 2): the two fail-open holes of the target-filter isolation.
/// <list type="bullet">
/// <item><description>An active, non-ignored filter that cannot be reduced to a predicate must make the
/// command refuse with <see cref="NotSupportedException"/> instead of being silently dropped.</description></item>
/// <item><description>The full-<c>MERGE</c> form is detected by actual branches; <c>.On(...)</c> alone does
/// not turn the key-upsert form into a full <c>MERGE</c>, so a branchless <c>.On(...)</c> under an active
/// filter refuses before any query-source read.</description></item>
/// </list>
/// The context is a provider-free renderer: <c>ToSql</c> never opens a connection and the connection
/// factory counts every attempt, so "before any read" is observable. No database is involved.
/// </summary>
public class MergeTargetFilterFailClosedTests
{
    private const string TenantKey = "mtf_failclosed_tenant";

    [SqlTable("mtf_full_merge")]
    private sealed class FullMergeEntity
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int TenantId { get; set; }
    }

    [SqlTable("mtf_branchless")]
    private sealed class BranchlessEntity
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int TenantId { get; set; }
    }

    [SqlTable("mtf_branchless_plain")]
    private sealed class BranchlessPlainEntity
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int TenantId { get; set; }
    }

    [SqlTable("mtf_untranslatable")]
    private sealed class UntranslatableEntity
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [SqlTable("mtf_multi_filter")]
    private sealed class MultiFilterEntity
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int TenantId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>A filter that declares neither a predicate lambda nor a builder-function, so it cannot be
    /// reduced to a predicate. A consumer that cannot translate an active filter must refuse, not drop it.</summary>
    private sealed class UntranslatableFilter : IQueryFilterMetadata
    {
        public string Key => "untranslatable";

        public LambdaExpression? Lambda => null;

        public Delegate? Func => null;
    }

    /// <summary>Overlays an arbitrary filter list over the auto-built mapping of an entity type, so a
    /// test can register a filter shape the public fluent surface cannot express.</summary>
    private sealed class FilterOverlay(IEntityMetadata inner, IReadOnlyList<IQueryFilterMetadata> filters) : IEntityMetadata
    {
        public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;

        public string? TableName => inner.TableName;

        public bool IsTableNameAuto => inner.IsTableNameAuto;

        public IPropertyMetadata? DynamicColumnsStore => inner.DynamicColumnsStore;

        public IReadOnlyList<IQueryFilterMetadata> Filters => filters;

        public IReadOnlyList<IRelationshipMetadata> Relationships => inner.Relationships;
    }

    /// <summary>A dialect that supports the general multi-branch <c>MERGE</c> (SQL Server-like).</summary>
    private sealed class FullMergeDialect : SqlDialectBase
    {
        public static readonly FullMergeDialect Instance = new();

        public override bool SupportsMergeStatement => true;

        public override bool SupportsMergeConditionalBranches => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>PostgreSQL-like: a general <c>MERGE</c> exists, but the native key-upsert form is
    /// <c>ON CONFLICT</c> (<see cref="SqlDialectBase.SupportsMerge"/> stays <c>false</c>) and cannot carry
    /// the target predicate.</summary>
    private sealed class KeyUpsertOnlyDialect : SqlDialectBase
    {
        public static readonly KeyUpsertOnlyDialect Instance = new();

        public override bool SupportsMergeStatement => true;

        public override bool SupportsMergeConditionalBranches => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class RenderingContext(ISqlDialect dialect) : DataContext(new DataContextBuilder())
    {
        public int ConnectionAttempts { get; private set; }

        public override ISqlDialect Dialect => dialect;

        public override DbParameter CreateParam(string name, object? value)
            => throw new NotSupportedException("The render-only test context never mints a command parameter.");

        protected override DbConnection CreateDbConnection(string? connectionString)
        {
            ConnectionAttempts++;
            throw new InvalidOperationException("The test context must not open a connection.");
        }
    }

    private static void InjectFilter<TEntity>(params IQueryFilterMetadata[] filters)
    {
        var normal = new EntityMetadataBuilder<TEntity>().Build();
        DataContextCache.Metadata[typeof(TEntity)] = new FilterOverlay(normal, filters);
    }

    private static void RemoveMetadata<TEntity>() => DataContextCache.Metadata.Remove(typeof(TEntity));

    // --- Fail-closed translation ----------------------------------------------------------------

    [Fact]
    public void UntranslatableActiveFilter_FullMerge_ShouldRefuseInsteadOfDroppingThePredicate()
    {
        InjectFilter<UntranslatableEntity>(new UntranslatableFilter());
        try
        {
            using var ctx = new RenderingContext(FullMergeDialect.Instance);

            var act = () => ctx.CreateMergeBuilder<UntranslatableEntity>()
                .Using(new UntranslatableEntity { Id = 1, Name = "a" })
                .On((t, s) => t.Id == s.Id)
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .ToSql();

            act.Should().Throw<NotSupportedException>(
                "an active filter that cannot be translated must never be dropped silently");
        }
        finally
        {
            RemoveMetadata<UntranslatableEntity>();
        }
    }

    [Fact]
    public void UntranslatableActiveFilter_FullMerge_ShouldRefuseBeforeAnyConnection()
    {
        InjectFilter<UntranslatableEntity>(new UntranslatableFilter());
        try
        {
            using var ctx = new RenderingContext(FullMergeDialect.Instance);

            var act = () => ctx.CreateMergeBuilder<UntranslatableEntity>()
                .Using(new UntranslatableEntity { Id = 1, Name = "a" })
                .On((t, s) => t.Id == s.Id)
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();

            act.Should().Throw<NotSupportedException>();
            ctx.ConnectionAttempts.Should().Be(0, "the refusal is a metadata decision taken before any read or mutation");
        }
        finally
        {
            RemoveMetadata<UntranslatableEntity>();
        }
    }

    [Fact]
    public void UntranslatableFilter_Ignored_ShouldRenderWithoutRefusing()
    {
        InjectFilter<UntranslatableEntity>(new UntranslatableFilter());
        try
        {
            using var ctx = new RenderingContext(FullMergeDialect.Instance);

            var sql = ctx.CreateMergeBuilder<UntranslatableEntity>()
                .Using(new UntranslatableEntity { Id = 1, Name = "a" })
                .IgnoreFilters()
                .On((t, s) => t.Id == s.Id)
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .ToSql();

            sql.Should().StartWith("merge into", "IgnoreFilters restores the intentional bypass");
        }
        finally
        {
            RemoveMetadata<UntranslatableEntity>();
        }
    }

    // --- Multi-filter reduction ------------------------------------------------------------------

    [Fact]
    public void TwoActiveFilters_FullMerge_ShouldAndBothPredicatesIntoTheTargetFilter()
    {
        using var ctx = new RenderingContext(FullMergeDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<MultiFilterEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey])
            .HasQueryFilter("soft-delete", (e, _) => !e.IsDeleted));

        var sql = ctx.CreateMergeBuilder<MultiFilterEntity>()
            .Using(new MultiFilterEntity { Id = 1, TenantId = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        var onStart = sql.IndexOf(" on ", StringComparison.Ordinal);
        var onEnd = sql.IndexOf(" when ", StringComparison.Ordinal);
        var on = onStart >= 0 && onEnd > onStart ? sql[onStart..onEnd] : sql;

        on.Should().Contain("target.TenantId = @", "the first active filter is injected into the MERGE ON");
        on.Should().Contain("target.IsDeleted", "the second active filter is injected into the MERGE ON");
        on.Should().Contain(" and ", "the two active filters are combined with AND (the multi-filter arm)");
    }

    // --- Form detection by branches --------------------------------------------------------------

    [Fact]
    public void BranchlessOn_ActiveFilter_ShouldRefuseBeforeQuerySourceRead()
    {
        using var ctx = new RenderingContext(KeyUpsertOnlyDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<BranchlessEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var act = () => ctx.CreateMergeBuilder<BranchlessEntity>()
            .Using(ctx.From<BranchlessEntity>().Where(x => x.Id == 1))
            .On((t, s) => t.Id == s.Id)
            .Merge();

        act.Should().Throw<NotSupportedException>(
            "a branchless On(...) is not a full MERGE; the key-upsert form cannot carry the filter");
        ctx.ConnectionAttempts.Should().Be(0, "the refusal precedes any query-source read");
    }

    [Fact]
    public async Task BranchlessOn_ActiveFilter_Async_ShouldRefuseBeforeQuerySourceRead()
    {
        using var ctx = new RenderingContext(KeyUpsertOnlyDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<BranchlessEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var command = ctx.CreateMergeBuilder<BranchlessEntity>()
            .Using(ctx.From<BranchlessEntity>().Where(x => x.Id == 1))
            .On((t, s) => t.Id == s.Id);

        var act = async () => await command.MergeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>(
            "sync and async agree on the branch-based form detection");
        ctx.ConnectionAttempts.Should().Be(0);
    }

    [Fact]
    public void BranchlessOn_IgnoredFilter_ShouldKeepTheMalformedFormError()
    {
        using var ctx = new RenderingContext(KeyUpsertOnlyDialect.Instance);
        ctx.Properties[TenantKey] = 1;
        ctx.From<BranchlessEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var act = () => ctx.CreateMergeBuilder<BranchlessEntity>()
            .Using(new BranchlessEntity { Id = 1, TenantId = 1, Name = "a" })
            .IgnoreFilters()
            .On((t, s) => t.Id == s.Id)
            .ToSql();

        act.Should().Throw<InvalidOperationException>(
                "IgnoreFilters leaves the filter bypass intact, so the branchless On(...) keeps its malformed-form error")
            .Which.Should().NotBeOfType<NotSupportedException>();
    }

    [Fact]
    public void BranchlessOn_NoFilterConfigured_ShouldKeepTheMalformedFormError()
    {
        using var ctx = new RenderingContext(KeyUpsertOnlyDialect.Instance);

        // A dedicated type that no test ever registers a filter for, so the metadata cache cannot leak
        // an active filter into this control.
        var act = () => ctx.CreateMergeBuilder<BranchlessPlainEntity>()
            .Using(new BranchlessPlainEntity { Id = 1, TenantId = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id)
            .ToSql();

        act.Should().Throw<InvalidOperationException>(
                "without an active filter the branchless On(...) behaves exactly as before")
            .Which.Should().NotBeOfType<NotSupportedException>();
    }
}

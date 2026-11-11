using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T324 D6a — AC03 / E03: every assignment row of <c>ApplyJoinStateTo</c> survives the flat correlated
/// APPLY seam, and the ordered joins are copied then appended exactly once.
/// <para>
/// The method under test is the shared state copy the first ordinary join also uses
/// (<c>EntityBuilder.CreateJoined</c> → <c>ApplyJoinStateTo</c>). Each case therefore builds the same
/// receiver state twice: once continued by an ordinary <c>Join</c>, once by the new flat
/// <c>CrossApply</c>, and asserts the resulting builders agree on every inventory field. This is a
/// differential parity against the ordinary path, not a new "override renders" claim.
/// </para>
/// <para>
/// Inventory coverage (<c>.pdca/T324/rv1/state-inventory.md</c>):
/// <list type="bullet">
/// <item>S1 rows 1, 4–12, 14–23, 25–28, 30 — scalars and shared collections.</item>
/// <item>S2 rows 2, 3, 29 — physical source (<c>Table</c>, <c>Query</c>, <c>SourceFrom</c>).</item>
/// <item>S3 rows 31–35 — the <c>query is null</c> conditional source overrides, asserted through
/// <c>ResolveSource()</c> on a raw-table source and through identical-failure parity on a mapped entity
/// (the override is not renderable after a join on the pre-existing ordinary path either — known and
/// accepted, see the ledger in the T324 status record).</item>
/// <item>S4 row 24 — <c>TablesInScopeHints</c> (included in S1).</item>
/// <item>S5 row 36 — ordered <c>Joins</c> copied in order and appended exactly once.</item>
/// <item>S6 row 13 — <c>SourceLockClause</c> (included in S1 through <c>ForUpdate()</c>).</item>
/// <item>S7 row 37 — the new APPLY binding metadata is recorded by the seam.</item>
/// <item>S8 — CTE declarations copied, no merge loss on the left receiver.</item>
/// </list>
/// </para>
/// </summary>
public class T324ApplyStateTests
{
    private static SqlServerDataContext CreateContext()
        => new(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

    // ---------------------------------------------------------------------------------------------
    // S1/S4/S6 — the full assignment set on the receiver is copied exactly as the ordinary join path.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Receiver_state_is_copied_exactly_like_the_ordinary_join_path()
    {
        using var ctx = CreateContext();
        var stateful = FullyStated(ctx);

        var joined = stateful.Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id);
        var applied = stateful.Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        AssertAssignmentParity(joined, applied);
    }

    // ---------------------------------------------------------------------------------------------
    // S2 — physical FROM forms: raw table, derived command and raw-SQL source are all carried.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Physical_table_source_is_carried()
    {
        using var ctx = CreateContext();
        var source = DataContextExtensions.From(ctx, "t324_apply_a");

        var joined = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id);
        var applied = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id)
            .CrossApply((Projection<TableAlias, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        joined.Table.Should().Be("t324_apply_a");
        applied.Table.Should().Be(joined.Table);
        AssertAssignmentParity(joined, applied);

        T324ApplyFixture.SqlOf(ctx, applied.Select(q => new { S1 = q.Item1["Id"].AsInt, S3 = q.Item3.Id }))
            .Should().Contain("from t324_apply_a as [t1]");
    }

    [Fact]
    public void Derived_query_source_is_carried()
    {
        using var ctx = CreateContext();
        var inner = ctx.From<T324A>().Where(a => a.Id > 0).Select(a => a);
        var source = ctx.From(inner);
        source.Query.Should().NotBeNull();

        var joined = source.Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id);
        var applied = source.Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        joined.Query.Should().BeSameAs(inner);
        applied.Query.Should().BeSameAs(joined.Query);
        AssertAssignmentParity(joined, applied);
    }

    [Fact]
    public void Raw_sql_from_source_is_carried()
    {
        using var ctx = CreateContext();
        var source = ctx.FromSql("select Id, Code from t324_apply_a");
        source.SourceFrom.Should().NotBeNull();

        var joined = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id);
        var applied = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id)
            .CrossApply((Projection<TableAlias, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        joined.SourceFrom.Should().BeSameAs(source.SourceFrom);
        applied.SourceFrom.Should().BeSameAs(joined.SourceFrom);
        AssertAssignmentParity(joined, applied);

        T324ApplyFixture.SqlOf(ctx, applied.Select(q => new { S1 = q.Item1["Id"].AsInt, S3 = q.Item3.Id }))
            .Should().Contain("from (select Id, Code from t324_apply_a) as [t1]");
    }

    // ---------------------------------------------------------------------------------------------
    // S3 — the `query is null` conditional override branch (rows 31–35).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Conditional_source_overrides_are_copied_when_the_query_is_null()
    {
        using var ctx = CreateContext();
        var source = DataContextExtensions.From(ctx, "t324_apply_a")
            .WithTableName("t324_renamed")
            .WithSchema("dbo")
            .WithDatabase("db")
            .WithServer("srv");

        var joined = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id);
        var applied = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id)
            .CrossApply((Projection<TableAlias, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        var joinedSource = joined.ResolveSource();
        var appliedSource = applied.ResolveSource();

        joinedSource.Should().NotBeNull();
        appliedSource.Should().NotBeNull();
        appliedSource.Should().BeEquivalentTo(joinedSource);
        appliedSource!.TableNameOverride.Should().Be("t324_renamed");
        appliedSource.SchemaOverride.Should().Be("dbo");
        appliedSource.DatabaseOverride.Should().Be("db");
        appliedSource.ServerOverride.Should().Be("srv");
        appliedSource.TableExpressionOverride.Should().BeNull();
        AssertAssignmentParity(joined, applied);
    }

    [Fact]
    public void Conditional_table_expression_override_is_copied_when_the_query_is_null()
    {
        using var ctx = CreateContext();
        var source = DataContextExtensions.From(ctx, "t324_apply_a").WithTableExpression("(select 1 as Id)");

        var joined = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id);
        var applied = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id)
            .CrossApply((Projection<TableAlias, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        applied.ResolveSource()!.TableExpressionOverride.Should().Be("(select 1 as Id)");
        applied.ResolveSource().Should().BeEquivalentTo(joined.ResolveSource());
    }

    [Fact]
    public void Mapped_entity_override_fails_identically_on_the_ordinary_and_applied_paths()
    {
        using var ctx = CreateContext();
        var source = ctx.From<T324A>().WithTableName("t324_renamed").WithSchema("dbo");

        var joined = source.Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id);
        var applied = source.Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        // Known and accepted: the override is not renderable after a join on the pre-existing ordinary
        // path (the joined projection type has no registered physical table). The APPLY path must fail
        // the same way — parity, not a new rendering guarantee.
        Action joinSelect = () => joined.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id });
        Action applySelect = () => applied.Select(q => new { S1 = q.Item1.Id, S3 = q.Item3.Id });

        var joinFailure = joinSelect.Should().Throw<Exception>().Which;
        var applyFailure = applySelect.Should().Throw<Exception>().Which;
        applyFailure.GetType().Should().Be(joinFailure.GetType());
        // The messages embed the receiver's projection type, which necessarily differs by arity and slot
        // set (the APPLY receiver has one more slot than the join receiver). The stable prefix — the
        // actual failure reason, before the type name — is what must be identical.
        var joinReason = StableReason(joinFailure.Message);
        var applyReason = StableReason(applyFailure.Message);
        joinReason.Should().NotBeEmpty("the failure must carry a reason before the projection type name");
        applyReason.Should().Be(joinReason);
    }

    /// <summary>Returns the message text before the first projection type name (the arity-specific tail is excluded).</summary>
    private static string StableReason(string message)
    {
        var marker = message.IndexOf("Projection`", StringComparison.Ordinal);
        return marker < 0 ? message : message[..marker];
    }

    // ---------------------------------------------------------------------------------------------
    // S5 — ordered joins are copied in order, appended exactly once, and the parent list is untouched.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Ordered_joins_are_copied_and_appended_exactly_once()
    {
        using var ctx = CreateContext();
        var three = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<T324C>(), (p, c) => p.Item1.Id == c.Id);

        three.Joins.Should().HaveCount(2);

        var applied = three.CrossApply((Projection<T324A, T324B, T324C> p) =>
            ctx.From<T324D>().Where(d => d.Id == p.Item3.Id));

        applied.Joins.Should().HaveCount(3, "the two preceding joins are carried and the APPLY is appended once");
        applied.Joins.Should().NotBeSameAs(three.Joins, "the parent's join list must never be aliased");
        applied.Joins!.Take(2).Should().Equal(three.Joins, "ordered preceding joins are copied without loss or reorder");
        applied.Joins![0].Should().BeSameAs(three.Joins![0]);
        applied.Joins![1].Should().BeSameAs(three.Joins![1]);
        applied.Joins![2].ApplySource.Should().NotBeNull("only the appended join is the APPLY");
        // The applied source is materialised from ApplySource during preparation, so the appended join's
        // physical From is still empty at construction; the two carried joins keep the receiver's sources.
        applied.Joins[2].From.Table.Should().BeNull("construction must not evaluate the APPLY source");
        applied.Joins.Take(2).Select(j => j.From.Table).Should().Equal(
            new[] { "t324_apply_b", "t324_apply_c" },
            "the carried joins keep their receiver sources in order");

        three.Joins.Should().HaveCount(2, "the rejected/continued call must not mutate the receiver");
    }

    // ---------------------------------------------------------------------------------------------
    // S7 — the seam records the new APPLY binding metadata (row 37) next to the state copy.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_seam_records_the_apply_left_slots_for_a_multi_slot_receiver()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        var applyJoin = applied.Joins!.Single(j => j.ApplySource is not null);
        applyJoin.ApplyLeftSlots.Should().NotBeNull();
        applyJoin.ApplyLeftSlots.Should().HaveCount(2, "slot 1 is the FROM source and slot 2 the first preceding join");
        applyJoin.ApplyLeftSlots!.Select(s => s.Slot).Should().Equal(1, 2);
        applyJoin.ApplyLeftSlots.Select(s => s.Owner).Should().OnlyContain(owner => ReferenceEquals(owner, applyJoin.ApplySource!.Parameters[0]));
    }

    [Fact]
    public void A_single_entity_receiver_keeps_the_legacy_null_slot_metadata()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>()
            .CrossApply(a => ctx.From<T324B>().Where(b => b.Id == a.Id));

        var applyJoin = applied.Joins!.Single(j => j.ApplySource is not null);
        applyJoin.ApplyLeftSlots.Should().BeNull("a single-entity APPLY keeps its existing resolution path");
    }

    // ---------------------------------------------------------------------------------------------
    // S8 — CTE declarations on the receiver survive the flat APPLY seam.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Receiver_ctes_are_carried_without_duplication()
    {
        using var ctx = CreateContext();
        var cteBody = ctx.From<T324A>().Where(a => a.Id > 0).Select(a => a);
        var source = ctx.With("t324_cte", cteBody).From("t324_cte");
        source.Ctes.Should().NotBeNull();

        var joined = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id);
        var applied = source.Join(ctx.From<T324B>(), (r, b) => r["Id"].AsInt == b.Id)
            .CrossApply((Projection<TableAlias, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        joined.Ctes.Should().BeSameAs(source.Ctes);
        applied.Ctes.Should().BeSameAs(joined.Ctes);
        applied.Ctes.Should().HaveCount(1);
        applied.Ctes![0].Name.Should().Be("t324_cte");
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Sets every assignment row that can be set on a plain mapped-entity receiver. The rows that need a
    /// physical source form (<c>Table</c>, <c>Query</c>, <c>SourceFrom</c>) and the conditional overrides
    /// are covered by their own cases above.
    /// </summary>
    private static EntityBuilder<T324A> FullyStated(IDataContext ctx)
    {
        var builder = ctx.From<T324A>()
            .IgnoreFilters(typeof(T324B))
            .Distinct()
            .Limit(7)
            .Offset(3)
            .ForUpdate()
            .WithTablesInScopeHint("nolock")
            .WithQuotedIdentifiers(true)
            .WithKeywordCase(KeywordCase.Upper)
            .WithNamingConvention(new SnakeCaseNamingConvention());

        builder.GroupingType = GroupingType.GroupingSets;
        builder.GroupingSets = [[0], []];
        builder.GroupByWithTotals = true;
        builder.LimitByClause = new LimitByClause((Expression<Func<T324A, int>>)(a => a.Code), 5, 1);
        builder.DistinctOnClause = new DistinctOnClause((Expression<Func<T324A, int>>)(a => a.Code));
        builder.TableSampleClause = new TableSampleClause(TableSampleMethod.System, 10, 42);
        builder.TemporalClause = TemporalClause.AsOf(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        builder.SourceLockClause = new LockClause(LockMode.Update);
        builder.IsFinal = true;
        builder.SampleRatio = 0.25;
        builder.SampleOffset = 10;
        builder.SettingsList = [new KeyValuePair<string, string>("max_threads", "2")];
        builder.PreWhereCondition = (Expression<Func<T324A, bool>>)(a => a.Id > 0);
        builder.ArrayJoins = [(Expression<Func<T324A, int[]>>)(a => new[] { a.Id })];
        builder.ArrayJoinKind = ArrayJoinKind.Left;
        builder.TableHints = ["nolock"];
        builder.IndexHints = ["IX_t324_a"];
        builder.IndexHintKind = IndexHintKind.Force;
        builder.Ctes = [new CteDefinition("t324_receiver_cte", ctx.From<T324A>().Select(a => a.Id))];
        return builder;
    }

    /// <summary>Asserts every <c>ApplyJoinStateTo</c> assignment row on the applied builder matches the joined builder.</summary>
    private static void AssertAssignmentParity<TJoined, TApplied>(
        EntityBuilder<TJoined> joined,
        EntityBuilder<TApplied> applied)
    {
        // Rows 1–12, 14–23, 25–28, 30 — scalar values and shared references assignments.
        applied.Logger.Should().BeSameAs(joined.Logger);
        applied.Table.Should().Be(joined.Table);
        applied.Query.Should().BeSameAs(joined.Query);
        applied.IsDistinct.Should().Be(joined.IsDistinct);
        applied.GroupingType.Should().Be(joined.GroupingType);
        applied.GroupingSets.Should().BeSameAs(joined.GroupingSets);
        applied.GroupByWithTotals.Should().Be(joined.GroupByWithTotals);
        applied.LimitByClause.Should().BeSameAs(joined.LimitByClause);
        applied.DistinctOnClause.Should().BeSameAs(joined.DistinctOnClause);
        applied.TableSampleClause.Should().BeSameAs(joined.TableSampleClause);
        applied.TemporalClause.Should().BeSameAs(joined.TemporalClause);
        applied.RowLockClause.Should().BeSameAs(joined.RowLockClause);
        applied.SourceLockClause.Should().BeSameAs(joined.SourceLockClause, "row 13 is carried only by the shared state copy");
        applied.IsFinal.Should().Be(joined.IsFinal);
        applied.SampleRatio.Should().Be(joined.SampleRatio);
        applied.SampleOffset.Should().Be(joined.SampleOffset);
        // SettingsList is null for most receivers: the dictionary overload of Equal throws on a null
        // expected operand, so the null branch is asserted explicitly instead of through Equal.
        if (joined.SettingsList is null)
            applied.SettingsList.Should().BeNull();
        else
            applied.SettingsList.Should().Equal(joined.SettingsList);
        applied.PreWhereCondition.Should().BeSameAs(joined.PreWhereCondition);
        applied.ArrayJoins.Should().Equal(joined.ArrayJoins);
        applied.ArrayJoinKind.Should().Be(joined.ArrayJoinKind);
        applied.TableHints.Should().BeSameAs(joined.TableHints);
        applied.IndexHints.Should().BeSameAs(joined.IndexHints);
        applied.IndexHintKind.Should().Be(joined.IndexHintKind);
        applied.TablesInScopeHints.Should().BeSameAs(joined.TablesInScopeHints);
        applied.Ctes.Should().BeSameAs(joined.Ctes);
        applied.QuoteIdentifiers.Should().Be(joined.QuoteIdentifiers);
        applied.NamingConvention.Should().BeSameAs(joined.NamingConvention);
        applied.KeywordCase.Should().Be(joined.KeywordCase);
        applied.SourceFrom.Should().BeSameAs(joined.SourceFrom);
        applied.FilterScope.Should().BeSameAs(joined.FilterScope, "the selective filter scope is carried only by the shared state copy");
    }
}

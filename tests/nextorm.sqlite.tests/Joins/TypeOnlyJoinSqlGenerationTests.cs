using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T222: the source-less (type-only) <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/<c>FullJoin</c>
/// overloads must render SQL byte-identical to the explicit-source oracle on SQLite. Covers the four
/// kinds at every receiver arity (1..7), a configured mapping that differs from convention, a
/// convention-resolved type used without prior registration, and a type the convention builder
/// rejects. These tests never open a connection. Focused selector: TypeOnlyJoinSqlGenerationTests.
/// </summary>
public class TypeOnlyJoinSqlGenerationTests
{
    [SqlTable("to_parent")]
    public sealed class ToParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("to_child")]
    public sealed class ToChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }

    // No [SqlTable]; resolved through the convention builder on first From<T>().
    public sealed class ConvChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }

    [SqlTable("to_cfg_child")]
    public sealed class CfgChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }

    // Convention metadata rejects this mapping ([DynamicColumns] on a non-dictionary property), so
    // From<Unresolvable>() fails while it registers the mapping. The type-only overload must fail at
    // the same declared stage independently of the explicit call (V04).
    public sealed class Unresolvable
    {
        public int Id { get; set; }

        [DynamicColumns]
        public int Broken { get; set; }
    }

    private static IDataContext Create() => SqliteTestContext.Create();

    // ---------- declared capability outcomes for this provider ----------
    // The expected result of every (operator, form) pair is declared here from the provider's
    // documented dialect capabilities; it is never inferred from the explicit-source call. A null
    // marker means the combination is supported and must render SQL. A non-null marker is the exact
    // NotSupportedException message the dialect documents for that refusal, asserted identically on
    // both forms.
    //
    // SQLite has no lateral/APPLY source, so CrossApply/OuterApply are refused; FULL/RIGHT joins and
    // every other operator are supported. Join hints are not supported by SQLite at all.
    private const string? UnsupportedCrossApply = "The CrossApply join is not supported by this SQL dialect";
    private const string? UnsupportedOuterApply = "The OuterApply join is not supported by this SQL dialect";
    private const string? FullJoinRefusal = null;
    private const string JoinHintRefusal = "Join hints are not supported by this SQL dialect";
    private const string? LeftJoinHintRefusal = JoinHintRefusal;
    private const string? CrossJoinHintRefusal = JoinHintRefusal;
    private const string? CrossApplyHintRefusal = JoinHintRefusal;
    private const string? OuterApplyHintRefusal = JoinHintRefusal;
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static (string? Sql, Exception? Error) TrySql<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        try
        {
            return (SqlOf(ctx, cmd), null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private static (string Name, object? Value)[] ParamsOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        return prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value)).ToArray();
    }

    // Asserts the outcome declared by the caller for this (operator, form) pair. expectedRefusal is
    // null for a supported combination: neither form may throw and SQL/parameters must match
    // byte-for-byte. Otherwise both forms must throw NotSupportedException carrying exactly the
    // documented capability marker. There is deliberately no fallback that compares arbitrary
    // exceptions, so an unrelated error can never satisfy the assertion.
    private static void AssertSameSql<T>(IDataContext ctx, QueryCommand<T> explicitQuery, QueryCommand<T> typeOnlyQuery, string? expectedRefusal = null)
    {
        var e = TrySql(ctx, explicitQuery);
        var t = TrySql(ctx, typeOnlyQuery);

        if (expectedRefusal is null)
        {
            e.Error.Should().BeNull("this combination is declared supported, so the explicit form must compile");
            t.Error.Should().BeNull("the source-less path must not reject a query the explicit path accepts");
            t.Sql.Should().Be(e.Sql, "source-less SQL must be byte-identical to the explicit-source oracle");
            var ep = ParamsOf(ctx, explicitQuery);
            var tp = ParamsOf(ctx, typeOnlyQuery);
            tp.Should().Equal(ep, "parameter list and values must be byte-identical to the explicit-source oracle");
            return;
        }

        e.Error.Should().BeOfType<NotSupportedException>("the explicit form must hit the documented capability refusal declared for this combination");
        e.Error!.Message.Should().Be(expectedRefusal, "the explicit form must carry the documented capability marker");
        t.Error.Should().BeOfType<NotSupportedException>("the source-less form must hit the same documented capability refusal");
        t.Error!.Message.Should().Be(expectedRefusal, "the source-less form must carry the same documented capability marker");
    }

    private enum Stage { None, Construction, SqlCompilation }

    // Builds the query expression (stage i) and compiles its SQL (stage ii) separately, so the failing
    // stage is observable instead of being merged into a single try.
    private static (Stage Stage, Exception? Error, string? Sql) TryPipeline<T>(IDataContext ctx, Func<QueryCommand<T>> build)
    {
        QueryCommand<T> cmd;
        try
        {
            cmd = build();
        }
        catch (Exception ex)
        {
            return (Stage.Construction, ex, null);
        }

        try
        {
            return (Stage.None, null, SqlOf(ctx, cmd));
        }
        catch (Exception ex)
        {
            return (Stage.SqlCompilation, ex, null);
        }
    }

    [Fact]
    public void Join_TypeOnly_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        var e2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        var t2 = ctx.From<ToParent>().Join<ToChild>((p, c) => p.Id == c.ParentId);
        AssertSameSql(ctx, e2.Select(p => p.Item1.Id), t2.Select(p => p.Item1.Id));

        var e3 = e2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t3 = t2.Join<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e3.Select(p => p.Item1.Id), t3.Select(p => p.Item1.Id));

        var e4 = e3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t4 = t3.Join<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e4.Select(p => p.Item1.Id), t4.Select(p => p.Item1.Id));

        var e5 = e4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t5 = t4.Join<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e5.Select(p => p.Item1.Id), t5.Select(p => p.Item1.Id));

        var e6 = e5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t6 = t5.Join<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e6.Select(p => p.Item1.Id), t6.Select(p => p.Item1.Id));

        var e7 = e6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t7 = t6.Join<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e7.Select(p => p.Item1.Id), t7.Select(p => p.Item1.Id));

        var e8 = e7.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t8 = t7.Join<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e8.Select(p => p.Item1.Id), t8.Select(p => p.Item1.Id));

    }

    [Fact]
    public void LeftJoin_TypeOnly_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        var e2 = ctx.From<ToParent>().LeftJoin(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        var t2 = ctx.From<ToParent>().LeftJoin<ToChild>((p, c) => p.Id == c.ParentId);
        AssertSameSql(ctx, e2.Select(p => p.Item1.Id), t2.Select(p => p.Item1.Id));

        var e3 = e2.LeftJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t3 = t2.LeftJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e3.Select(p => p.Item1.Id), t3.Select(p => p.Item1.Id));

        var e4 = e3.LeftJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t4 = t3.LeftJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e4.Select(p => p.Item1.Id), t4.Select(p => p.Item1.Id));

        var e5 = e4.LeftJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t5 = t4.LeftJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e5.Select(p => p.Item1.Id), t5.Select(p => p.Item1.Id));

        var e6 = e5.LeftJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t6 = t5.LeftJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e6.Select(p => p.Item1.Id), t6.Select(p => p.Item1.Id));

        var e7 = e6.LeftJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t7 = t6.LeftJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e7.Select(p => p.Item1.Id), t7.Select(p => p.Item1.Id));

        var e8 = e7.LeftJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t8 = t7.LeftJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e8.Select(p => p.Item1.Id), t8.Select(p => p.Item1.Id));

    }

    [Fact]
    public void RightJoin_TypeOnly_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        var e2 = ctx.From<ToParent>().RightJoin(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        var t2 = ctx.From<ToParent>().RightJoin<ToChild>((p, c) => p.Id == c.ParentId);
        AssertSameSql(ctx, e2.Select(p => p.Item1.Id), t2.Select(p => p.Item1.Id));

        var e3 = e2.RightJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t3 = t2.RightJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e3.Select(p => p.Item1.Id), t3.Select(p => p.Item1.Id));

        var e4 = e3.RightJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t4 = t3.RightJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e4.Select(p => p.Item1.Id), t4.Select(p => p.Item1.Id));

        var e5 = e4.RightJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t5 = t4.RightJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e5.Select(p => p.Item1.Id), t5.Select(p => p.Item1.Id));

        var e6 = e5.RightJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t6 = t5.RightJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e6.Select(p => p.Item1.Id), t6.Select(p => p.Item1.Id));

        var e7 = e6.RightJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t7 = t6.RightJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e7.Select(p => p.Item1.Id), t7.Select(p => p.Item1.Id));

        var e8 = e7.RightJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t8 = t7.RightJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e8.Select(p => p.Item1.Id), t8.Select(p => p.Item1.Id));

    }

    [Fact]
    public void FullJoin_TypeOnly_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        var e2 = ctx.From<ToParent>().FullJoin(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        var t2 = ctx.From<ToParent>().FullJoin<ToChild>((p, c) => p.Id == c.ParentId);
        AssertSameSql(ctx, e2.Select(p => p.Item1.Id), t2.Select(p => p.Item1.Id), FullJoinRefusal);

        var e3 = e2.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t3 = t2.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e3.Select(p => p.Item1.Id), t3.Select(p => p.Item1.Id), FullJoinRefusal);

        var e4 = e3.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t4 = t3.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e4.Select(p => p.Item1.Id), t4.Select(p => p.Item1.Id), FullJoinRefusal);

        var e5 = e4.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t5 = t4.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e5.Select(p => p.Item1.Id), t5.Select(p => p.Item1.Id), FullJoinRefusal);

        var e6 = e5.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t6 = t5.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e6.Select(p => p.Item1.Id), t6.Select(p => p.Item1.Id), FullJoinRefusal);

        var e7 = e6.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t7 = t6.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e7.Select(p => p.Item1.Id), t7.Select(p => p.Item1.Id), FullJoinRefusal);

        var e8 = e7.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t8 = t7.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e8.Select(p => p.Item1.Id), t8.Select(p => p.Item1.Id), FullJoinRefusal);

    }

    [Fact]
    public void ConfiguredMapping_TypeOnly_UsesConfiguredSource()
    {
        using var ctx = Create();
        ctx.From<CfgChild>(b => b.Table("to_cfg_child_custom"));

        var e = ctx.From<ToParent>().Join(ctx.From<CfgChild>(), (p, c) => p.Id == c.ParentId).Select(p => p.Item1.Id);
        var t = ctx.From<ToParent>().Join<CfgChild>((p, c) => p.Id == c.ParentId).Select(p => p.Item1.Id);

        var sql = SqlOf(ctx, t);
        sql.Should().Be(SqlOf(ctx, e));
        sql.Should().Contain("to_cfg_child_custom", "the type-only form must resolve the configured mapping");
    }

    [Fact]
    public void ConventionResolved_TypeOnly_WorksWithoutRegistration()
    {
        using var ctx = Create();

        var t = ctx.From<ToParent>().Join<ConvChild>((p, c) => p.Id == c.ParentId).Select(p => p.Item1.Id);
        var e = ctx.From<ToParent>().Join(ctx.From<ConvChild>(), (p, c) => p.Id == c.ParentId).Select(p => p.Item1.Id);

        SqlOf(ctx, t).Should().Be(SqlOf(ctx, e));
    }

    [Fact]
    public void UnresolvableType_TypeOnly_FailsAtSameStageAsExplicit()
    {
        using var ctx = Create();

        // Declared expected outcome (not read from the explicit call): the convention builder rejects
        // Unresolvable's [DynamicColumns] int property while From<Unresolvable>() registers the
        // mapping, so both forms fail at expression construction and no SQL is compiled. The pipeline
        // builds and carries the join itself, so a stage change on either form is observed.
        const Stage expectedStage = Stage.Construction;
        const string expectedMarker = "dynamic-columns store property 'Broken'";

        var explicitResult = TryPipeline(ctx, () => ctx.From<ToParent>()
            .Join(ctx.From<Unresolvable>(), (p, c) => p.Id == c.Id)
            .Select(p => p.Item1.Id));
        var typeOnlyResult = TryPipeline(ctx, () => ctx.From<ToParent>()
            .Join<Unresolvable>((p, c) => p.Id == c.Id)
            .Select(p => p.Item1.Id));

        explicitResult.Stage.Should().Be(expectedStage, "the explicit form rejects the mapping while registering the joined type");
        typeOnlyResult.Stage.Should().Be(expectedStage, "the source-less form must fail at the declared stage, independently of the explicit call");
        explicitResult.Error.Should().BeOfType<InvalidOperationException>().Which.Message
            .Should().Contain(expectedMarker, "the failure must be the documented dynamic-columns store rejection");
        typeOnlyResult.Error.Should().BeOfType<InvalidOperationException>().Which.Message
            .Should().Contain(expectedMarker, "the source-less form must fail for the same documented reason");
    }
    [Fact]
    public void CrossJoin_TypeOnly_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        // arity 1 receiver
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossJoin(ctx.From<ToChild>());
            var t = recv.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id));
        }
        // arity 2..7 receivers
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        {
            var e = r2.CrossJoin(ctx.From<ToChild>());
            var t = r2.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id));
        }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        {
            var e = r3.CrossJoin(ctx.From<ToChild>());
            var t = r3.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id));
        }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        {
            var e = r4.CrossJoin(ctx.From<ToChild>());
            var t = r4.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id));
        }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        {
            var e = r5.CrossJoin(ctx.From<ToChild>());
            var t = r5.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id));
        }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        {
            var e = r6.CrossJoin(ctx.From<ToChild>());
            var t = r6.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id));
        }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        {
            var e = r7.CrossJoin(ctx.From<ToChild>());
            var t = r7.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id));
        }
    }

    [Fact]
    public void CrossApply_TypeOnly_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossApply(ctx.From<ToChild>());
            var t = recv.CrossApply<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedCrossApply);
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.CrossApply(ctx.From<ToChild>()); var t = r2.CrossApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.CrossApply(ctx.From<ToChild>()); var t = r3.CrossApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.CrossApply(ctx.From<ToChild>()); var t = r4.CrossApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.CrossApply(ctx.From<ToChild>()); var t = r5.CrossApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.CrossApply(ctx.From<ToChild>()); var t = r6.CrossApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.CrossApply(ctx.From<ToChild>()); var t = r7.CrossApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedCrossApply); }
    }

    [Fact]
    public void OuterApply_TypeOnly_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.OuterApply(ctx.From<ToChild>());
            var t = recv.OuterApply<ToChild>();
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedOuterApply);
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.OuterApply(ctx.From<ToChild>()); var t = r2.OuterApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.OuterApply(ctx.From<ToChild>()); var t = r3.OuterApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.OuterApply(ctx.From<ToChild>()); var t = r4.OuterApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.OuterApply(ctx.From<ToChild>()); var t = r5.OuterApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.OuterApply(ctx.From<ToChild>()); var t = r6.OuterApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.OuterApply(ctx.From<ToChild>()); var t = r7.OuterApply<ToChild>(); AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), UnsupportedOuterApply); }
    }

    [Fact]
    public void Join_StringTable_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        // arity 1
        {
            var recv = ctx.From<ToParent>();
            var e = recv.Join(ctx.From("to_child"), (p, c) => p.Id == c["parent_id"].AsInt);
            var s = recv.Join("to_child", (p, c) => p.Id == c["parent_id"].AsInt);
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id));
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        {
            var e = r2.Join(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt);
            var s = r2.Join("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt);
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id));
        }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.Join(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r3.Join("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.Join(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r4.Join("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.Join(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r5.Join("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.Join(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r6.Join("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.Join(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r7.Join("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
    }

    [Fact]
    public void LeftJoin_StringTable_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.LeftJoin(ctx.From("to_child"), (p, c) => p.Id == c["parent_id"].AsInt);
            var s = recv.LeftJoin("to_child", (p, c) => p.Id == c["parent_id"].AsInt);
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id));
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.LeftJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r2.LeftJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.LeftJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r3.LeftJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.LeftJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r4.LeftJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.LeftJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r5.LeftJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.LeftJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r6.LeftJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.LeftJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r7.LeftJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
    }

    [Fact]
    public void RightJoin_StringTable_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.RightJoin(ctx.From("to_child"), (p, c) => p.Id == c["parent_id"].AsInt);
            var s = recv.RightJoin("to_child", (p, c) => p.Id == c["parent_id"].AsInt);
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id));
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.RightJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r2.RightJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.RightJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r3.RightJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.RightJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r4.RightJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.RightJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r5.RightJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.RightJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r6.RightJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.RightJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r7.RightJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
    }

    [Fact]
    public void FullJoin_StringTable_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.FullJoin(ctx.From("to_child"), (p, c) => p.Id == c["parent_id"].AsInt);
            var s = recv.FullJoin("to_child", (p, c) => p.Id == c["parent_id"].AsInt);
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), FullJoinRefusal);
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.FullJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r2.FullJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), FullJoinRefusal); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.FullJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r3.FullJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), FullJoinRefusal); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.FullJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r4.FullJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), FullJoinRefusal); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.FullJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r5.FullJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), FullJoinRefusal); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.FullJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r6.FullJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), FullJoinRefusal); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.FullJoin(ctx.From("to_child"), (p, c) => p.Item2.Id == c["parent_id"].AsInt); var s = r7.FullJoin("to_child", (p, c) => p.Item2.Id == c["parent_id"].AsInt); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), FullJoinRefusal); }
    }

    [Fact]
    public void CrossJoin_StringTable_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossJoin(ctx.From("to_child"));
            var s = recv.CrossJoin("to_child");
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id));
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.CrossJoin(ctx.From("to_child")); var s = r2.CrossJoin("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.CrossJoin(ctx.From("to_child")); var s = r3.CrossJoin("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.CrossJoin(ctx.From("to_child")); var s = r4.CrossJoin("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.CrossJoin(ctx.From("to_child")); var s = r5.CrossJoin("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.CrossJoin(ctx.From("to_child")); var s = r6.CrossJoin("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.CrossJoin(ctx.From("to_child")); var s = r7.CrossJoin("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id)); }
    }

    [Fact]
    public void CrossApply_StringTable_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossApply(ctx.From("to_child"));
            var s = recv.CrossApply("to_child");
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedCrossApply);
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.CrossApply(ctx.From("to_child")); var s = r2.CrossApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.CrossApply(ctx.From("to_child")); var s = r3.CrossApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.CrossApply(ctx.From("to_child")); var s = r4.CrossApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.CrossApply(ctx.From("to_child")); var s = r5.CrossApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.CrossApply(ctx.From("to_child")); var s = r6.CrossApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedCrossApply); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.CrossApply(ctx.From("to_child")); var s = r7.CrossApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedCrossApply); }
    }

    [Fact]
    public void OuterApply_StringTable_EqualsExplicit_AtEveryReceiverArity()
    {
        using var ctx = Create();
        {
            var recv = ctx.From<ToParent>();
            var e = recv.OuterApply(ctx.From("to_child"));
            var s = recv.OuterApply("to_child");
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedOuterApply);
        }
        var r2 = ctx.From<ToParent>().Join(ctx.From<ToChild>(), (p, c) => p.Id == c.ParentId);
        { var e = r2.OuterApply(ctx.From("to_child")); var s = r2.OuterApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r3 = r2.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r3.OuterApply(ctx.From("to_child")); var s = r3.OuterApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r4 = r3.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r4.OuterApply(ctx.From("to_child")); var s = r4.OuterApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r5 = r4.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r5.OuterApply(ctx.From("to_child")); var s = r5.OuterApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r6 = r5.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r6.OuterApply(ctx.From("to_child")); var s = r6.OuterApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedOuterApply); }
        var r7 = r6.Join(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        { var e = r7.OuterApply(ctx.From("to_child")); var s = r7.OuterApply("to_child"); AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), UnsupportedOuterApply); }
    }

    [Fact]
    public void OptionsForwarding_StringAndType_Parity()
    {
        using var ctx = Create();
        // Each block declares its expected outcome for this provider through the constants above.
        // LeftJoin with a join hint (folded on inline-hint dialects, rendered on SQL Server, rejected where hints are unsupported)
        {
            var recv = ctx.From<ToParent>();
            var e = recv.LeftJoin(ctx.From("to_child"), (p, c) => p.Id == c["parent_id"].AsInt, j => j.WithJoinHint("loop"));
            var s = recv.LeftJoin("to_child", (p, c) => p.Id == c["parent_id"].AsInt, j => j.WithJoinHint("loop"));
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), LeftJoinHintRefusal);
        }
        // CrossJoin with a join hint (CROSS carries no hint modifier, so it is refused where the dialect
        // validates the hint at the join; a dialect that folds hints into the statement comment keeps it)
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossJoin(ctx.From("to_child"), j => j.WithJoinHint("loop"));
            var s = recv.CrossJoin("to_child", j => j.WithJoinHint("loop"));
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), CrossJoinHintRefusal);
        }
        // CrossApply with a join hint (APPLY hint folding)
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossApply(ctx.From("to_child"), j => j.WithJoinHint("loop"));
            var s = recv.CrossApply("to_child", j => j.WithJoinHint("loop"));
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), CrossApplyHintRefusal);
        }
        // type-only CrossJoin with options
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossJoin(ctx.From<ToChild>(), j => j.WithJoinHint("loop"));
            var t = recv.CrossJoin<ToChild>(j => j.WithJoinHint("loop"));
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), CrossJoinHintRefusal);
        }
        // OuterApply with a join hint (APPLY hint folding)
        {
            var recv = ctx.From<ToParent>();
            var e = recv.OuterApply(ctx.From("to_child"), j => j.WithJoinHint("loop"));
            var s = recv.OuterApply("to_child", j => j.WithJoinHint("loop"));
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), s.Select(p => p.Item1.Id), OuterApplyHintRefusal);
        }
        // type-only CrossApply with options
        {
            var recv = ctx.From<ToParent>();
            var e = recv.CrossApply(ctx.From<ToChild>(), j => j.WithJoinHint("loop"));
            var t = recv.CrossApply<ToChild>(j => j.WithJoinHint("loop"));
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), CrossApplyHintRefusal);
        }
        // type-only OuterApply with options
        {
            var recv = ctx.From<ToParent>();
            var e = recv.OuterApply(ctx.From<ToChild>(), j => j.WithJoinHint("loop"));
            var t = recv.OuterApply<ToChild>(j => j.WithJoinHint("loop"));
            AssertSameSql(ctx, e.Select(p => p.Item1.Id), t.Select(p => p.Item1.Id), OuterApplyHintRefusal);
        }
    }

    [Fact]
    public void NonGenericReceiver_StringTable_Parity()
    {
        using var ctx = Create();
        var recv = new EntityBuilder(ctx, "to_parent");
        {
            var e = recv.Join(ctx.From("to_child"), (s, c) => s["id"] == c["id"]);
            var s = recv.Join("to_child", (s, c) => s["id"] == c["id"]);
            AssertSameSql(ctx, e.Select(p => 1), s.Select(p => 1));
        }
        {
            var e = recv.CrossJoin(ctx.From("to_child"));
            var s = recv.CrossJoin("to_child");
            AssertSameSql(ctx, e.Select(p => 1), s.Select(p => 1));
        }
        {
            var e = recv.CrossApply(ctx.From("to_child"));
            var s = recv.CrossApply("to_child");
            AssertSameSql(ctx, e.Select(p => 1), s.Select(p => 1), UnsupportedCrossApply);
        }
        {
            var e = recv.OuterApply(ctx.From("to_child"));
            var s = recv.OuterApply("to_child");
            AssertSameSql(ctx, e.Select(p => 1), s.Select(p => 1), UnsupportedOuterApply);
        }
    }

    [Fact]
    public void NonGenericReceiver_TypeOnly_Parity()
    {
        using var ctx = Create();
        var recv = new EntityBuilder(ctx, "to_parent");
        {
            var e = recv.CrossJoin(ctx.From<ToChild>());
            var t = recv.CrossJoin<ToChild>();
            AssertSameSql(ctx, e.Select(p => 1), t.Select(p => 1));
        }
        {
            var e = recv.CrossApply(ctx.From<ToChild>());
            var t = recv.CrossApply<ToChild>();
            AssertSameSql(ctx, e.Select(p => 1), t.Select(p => 1), UnsupportedCrossApply);
        }
        {
            var e = recv.OuterApply(ctx.From<ToChild>());
            var t = recv.OuterApply<ToChild>();
            AssertSameSql(ctx, e.Select(p => 1), t.Select(p => 1), UnsupportedOuterApply);
        }
    }
}
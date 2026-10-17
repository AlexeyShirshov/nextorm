using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T222: the source-less (type-only) <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/<c>FullJoin</c>
/// overloads must render SQL byte-identical to the explicit-source oracle on SQL Server. Covers the four
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
    // From<Unresolvable>() fails. The type-only overload must fail at exactly the same stage as the
    // explicit oracle (V04).
    public sealed class Unresolvable
    {
        public int Id { get; set; }

        [DynamicColumns]
        public int Broken { get; set; }
    }

    private static IDataContext Create() => SqlServerTestContext.Create();

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

    private static void AssertSameSql<T>(IDataContext ctx, QueryCommand<T> explicitQuery, QueryCommand<T> typeOnlyQuery)
    {
        var e = TrySql(ctx, explicitQuery);
        var t = TrySql(ctx, typeOnlyQuery);
        if (e.Error is null)
        {
            t.Error.Should().BeNull("the type-only path must not reject a query the explicit path accepts");
            t.Sql.Should().Be(e.Sql, "type-only SQL must be byte-identical to the explicit-source oracle");
        }
        else
        {
            t.Error.Should().NotBeNull("a capability-unsupported explicit join must be rejected by the type-only form too");
            t.Error!.GetType().Should().Be(e.Error.GetType());
            t.Error.Message.Should().Be(e.Error.Message);
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
        AssertSameSql(ctx, e2.Select(p => p.Item1.Id), t2.Select(p => p.Item1.Id));

        var e3 = e2.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t3 = t2.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e3.Select(p => p.Item1.Id), t3.Select(p => p.Item1.Id));

        var e4 = e3.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t4 = t3.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e4.Select(p => p.Item1.Id), t4.Select(p => p.Item1.Id));

        var e5 = e4.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t5 = t4.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e5.Select(p => p.Item1.Id), t5.Select(p => p.Item1.Id));

        var e6 = e5.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t6 = t5.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e6.Select(p => p.Item1.Id), t6.Select(p => p.Item1.Id));

        var e7 = e6.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t7 = t6.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e7.Select(p => p.Item1.Id), t7.Select(p => p.Item1.Id));

        var e8 = e7.FullJoin(ctx.From<ToChild>(), (p, c) => p.Item2.Id == c.ParentId);
        var t8 = t7.FullJoin<ToChild>((p, c) => p.Item2.Id == c.ParentId);
        AssertSameSql(ctx, e8.Select(p => p.Item1.Id), t8.Select(p => p.Item1.Id));

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

        var explicitError = Record.Exception(() =>
        {
            var b = ctx.From<ToParent>().Join(ctx.From<Unresolvable>(), (p, c) => p.Id == c.Id);
            _ = SqlOf(ctx, b.Select(p => p.Item1.Id));
        });
        var typeOnlyError = Record.Exception(() =>
        {
            var b = ctx.From<ToParent>().Join<Unresolvable>((p, c) => p.Id == c.Id);
            _ = SqlOf(ctx, b.Select(p => p.Item1.Id));
        });

        explicitError.Should().NotBeNull("the convention builder rejects this mapping");
        typeOnlyError.Should().NotBeNull("the type-only form must not turn an unresolvable source into a successful query");
        typeOnlyError!.Message.Should().Be(explicitError!.Message);
    }
}

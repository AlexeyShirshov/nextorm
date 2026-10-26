using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_postgres_tests;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T324 D6b2 — the PostgreSQL half of the E06/AC06 matrix (cells M09-M16), expanded over the
/// preceding join kind and the flat total of sources (3-8). Every case is rendered by the real
/// PostgreSQL planner over a placeholder connection string; no connection is opened.
/// <para>
/// Columns are the receiver surface (positional <c>JoinedEntityBuilder&lt;T1..Tn&gt;</c> or the generated
/// alias receiver) crossed with the lambda return form (<c>EntityBuilder&lt;T&gt;</c> or
/// <c>QueryCommand&lt;T&gt;</c>); rows are the APPLY kind (M09-M12 cross, M13-M16 outer). Each variant is a
/// homogeneous chain: every preceding join uses the same kind, which is the strongest per-kind reading of
/// "preceding Join, LeftJoin, RightJoin, FullJoin, CrossJoin". The dialect spells APPLY as an ANSI lateral join: <c>cross join lateral (…) alias</c> with no <c>ON TRUE</c> for cross, <c>left join lateral (…) alias on true</c> for outer.
/// </para>
/// <para>
/// The provider expresses every combination listed above on both surfaces — its dialect sets <c>SupportsApply = true</c> and the preceding join kinds are all supported - so the executed set is the complete cross product and this provider contributes no capability guard.
/// </para>
/// <para>
/// When <c>NEXTORM_T324_SQL_DUMP</c> names a directory the case appends one JSON line with the normalized
/// SQL, its SHA-256 and the cell identity. The dump is evidence plumbing only: without the variable the
/// assertions run unchanged and nothing is written.
/// </para>
/// </summary>

[SqlTable("t324m_a")]
public sealed class MatrixA
{
    public int Id { get; set; }

    public int Ref { get; set; }
}

[SqlTable("t324m_b")]
public sealed class MatrixB
{
    public int Id { get; set; }

    public int Ref { get; set; }
}

[SqlTable("t324m_c")]
public sealed class MatrixC
{
    public int Id { get; set; }

    public int Ref { get; set; }
}

[SqlTable("t324m_d")]
public sealed class MatrixD
{
    public int Id { get; set; }

    public int Ref { get; set; }
}

[SqlTable("t324m_e")]
public sealed class MatrixE
{
    public int Id { get; set; }

    public int Ref { get; set; }
}

[SqlTable("t324m_f")]
public sealed class MatrixF
{
    public int Id { get; set; }

    public int Ref { get; set; }
}

[SqlTable("t324m_g")]
public sealed class MatrixG
{
    public int Id { get; set; }

    public int Ref { get; set; }
}

[SqlTable("t324m_h")]
public sealed class MatrixH
{
    public int Id { get; set; }

    public int Ref { get; set; }
}


/// <summary>The preceding join kind varied by the matrix.</summary>
public enum JoinKind
{
    /// <summary><c>join</c>.</summary>
    Inner,
    /// <summary><c>left join</c>.</summary>
    Left,
    /// <summary><c>right join</c>.</summary>
    Right,
    /// <summary><c>full join</c>.</summary>
    Full,
    /// <summary><c>cross join</c>.</summary>
    Cross,
}

/// <summary>The M-cell identity of the PostgreSQL matrix.</summary>
public enum MatrixCell
{
    /// <summary>Cross lateral, positional receiver, <c>EntityBuilder</c> lambda.</summary>
    M09,
    /// <summary>Cross lateral, positional receiver, <c>QueryCommand</c> lambda.</summary>
    M10,
    /// <summary>Cross lateral, alias receiver, <c>EntityBuilder</c> lambda.</summary>
    M11,
    /// <summary>Cross lateral, alias receiver, <c>QueryCommand</c> lambda.</summary>
    M12,
    /// <summary>Outer lateral, positional receiver, <c>EntityBuilder</c> lambda.</summary>
    M13,
    /// <summary>Outer lateral, positional receiver, <c>QueryCommand</c> lambda.</summary>
    M14,
    /// <summary>Outer lateral, alias receiver, <c>EntityBuilder</c> lambda.</summary>
    M15,
    /// <summary>Outer lateral, alias receiver, <c>QueryCommand</c> lambda.</summary>
    M16,
}

/// <summary>The flat row projected by every matrix case: the FROM source, the last preceding join and the applied source.</summary>
public sealed class MatrixRow
{
    /// <summary>Slot 1, the FROM source.</summary>
    public int Left { get; set; }

    /// <summary>The last preceding join.</summary>
    public int Middle { get; set; }

    /// <summary>The applied source.</summary>
    public int Applied { get; set; }
}

public class CorrelatedApplyAfterJoinMatrixTests
{
    /// <summary>Every preceding join kind crossed with every flat total (3-8): thirty variants per cell.</summary>
    public static TheoryData<JoinKind, int> KindsAndTotals { get; } = CreateKindsAndTotals();

    private static TheoryData<JoinKind, int> CreateKindsAndTotals()
    {
        var data = new TheoryData<JoinKind, int>();
        foreach (var kind in Enum.GetValues<JoinKind>())
            for (var total = 3; total <= 8; total++)
                data.Add(kind, total);
        return data;
    }

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M09_cross_lateral_positional_entity_builder(JoinKind kind, int total) => Run(MatrixCell.M09, kind, total);

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M10_cross_lateral_positional_query_command(JoinKind kind, int total) => Run(MatrixCell.M10, kind, total);

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M11_cross_lateral_alias_entity_builder(JoinKind kind, int total) => Run(MatrixCell.M11, kind, total);

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M12_cross_lateral_alias_query_command(JoinKind kind, int total) => Run(MatrixCell.M12, kind, total);

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M13_outer_lateral_positional_entity_builder(JoinKind kind, int total) => Run(MatrixCell.M13, kind, total);

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M14_outer_lateral_positional_query_command(JoinKind kind, int total) => Run(MatrixCell.M14, kind, total);

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M15_outer_lateral_alias_entity_builder(JoinKind kind, int total) => Run(MatrixCell.M15, kind, total);

    [Theory]
    [MemberData(nameof(KindsAndTotals))]
    public void M16_outer_lateral_alias_query_command(JoinKind kind, int total) => Run(MatrixCell.M16, kind, total);

    private static void Run(MatrixCell cell, JoinKind kind, int total)
    {
        using var ctx = PostgresTestContext.Create();
        var sql = SqlOf(ctx, Build(ctx, cell, kind, total));

        AssertShape(cell, kind, total, sql);
        Dump(cell, kind, total, sql);
    }

    private static QueryCommand<MatrixRow> Build(IDataContext ctx, MatrixCell cell, JoinKind kind, int total)
    {
        var aliasReceiver = cell is MatrixCell.M11 or MatrixCell.M12 or MatrixCell.M15 or MatrixCell.M16;
        var outer = cell is MatrixCell.M13 or MatrixCell.M14 or MatrixCell.M15 or MatrixCell.M16;
        var queryForm = cell is MatrixCell.M10 or MatrixCell.M12 or MatrixCell.M14 or MatrixCell.M16;

        return aliasReceiver
            ? AliasRows(ctx, total, kind, outer, queryForm)
            : PosRows(ctx, total, kind, outer, queryForm);
    }

    private static QueryCommand<MatrixRow> PosRows(IDataContext ctx, int total, JoinKind kind, bool outer, bool queryForm) => total switch
    {
        3 => PosRows3(ctx, kind, outer, queryForm),
        4 => PosRows4(ctx, kind, outer, queryForm),
        5 => PosRows5(ctx, kind, outer, queryForm),
        6 => PosRows6(ctx, kind, outer, queryForm),
        7 => PosRows7(ctx, kind, outer, queryForm),
        _ => PosRows8(ctx, kind, outer, queryForm),
    };

    private static QueryCommand<MatrixRow> AliasRows(IDataContext ctx, int total, JoinKind kind, bool outer, bool queryForm) => total switch
    {
        3 => AliasRows3(ctx, kind, outer, queryForm),
        4 => AliasRows4(ctx, kind, outer, queryForm),
        5 => AliasRows5(ctx, kind, outer, queryForm),
        6 => AliasRows6(ctx, kind, outer, queryForm),
        7 => AliasRows7(ctx, kind, outer, queryForm),
        _ => AliasRows8(ctx, kind, outer, queryForm),
    };

    // ---------------------------------------------------------------------------------------------
    // Positional receiver: JoinedEntityBuilder&lt;T1..Tn&gt; extended by the flat correlated transitions.
    // ---------------------------------------------------------------------------------------------

    private static JoinedEntityBuilder<MatrixA, MatrixB> PosChain2(IDataContext ctx, JoinKind kind)
    {
        var a = ctx.From<MatrixA>();
        var b = ctx.From<MatrixB>();
        return kind switch
        {
            JoinKind.Inner => a.Join(b, (l, r) => l.Id == r.Id),
            JoinKind.Left => a.LeftJoin(b, (l, r) => l.Id == r.Id),
            JoinKind.Right => a.RightJoin(b, (l, r) => l.Id == r.Id),
            JoinKind.Full => a.FullJoin(b, (l, r) => l.Id == r.Id),
            JoinKind.Cross => a.CrossJoin(b),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
    private static QueryCommand<MatrixRow> PosRows3(IDataContext ctx, JoinKind kind, bool outer, bool queryForm)
        => PosApply2(ctx, PosChain2(ctx, kind), outer, queryForm);

    private static QueryCommand<MatrixRow> PosApply2(
        IDataContext ctx,
        JoinedEntityBuilder<MatrixA, MatrixB> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply((Projection<MatrixA, MatrixB> p) => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.Item1.Id).ToCommand())
                : left.CrossApply((Projection<MatrixA, MatrixB> p) => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.Item1.Id).ToCommand());
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item2.Id, Applied = q.Item3.Id });
        }

        var entityCommand = outer
            ? left.OuterApply((Projection<MatrixA, MatrixB> p) => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.Item1.Id))
            : left.CrossApply((Projection<MatrixA, MatrixB> p) => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.Item1.Id));
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item2.Id, Applied = q.Item3.Id });
    }

    private static JoinedEntityBuilder<MatrixA, MatrixB, MatrixC> PosChain3(IDataContext ctx, JoinKind kind)
    {
        var previous = PosChain2(ctx, kind);
        var c = ctx.From<MatrixC>();
        return kind switch
        {
            JoinKind.Inner => previous.Join(c, (l, r) => l.Item2.Id == r.Id),
            JoinKind.Left => previous.LeftJoin(c, (l, r) => l.Item2.Id == r.Id),
            JoinKind.Right => previous.RightJoin(c, (l, r) => l.Item2.Id == r.Id),
            JoinKind.Full => previous.FullJoin(c, (l, r) => l.Item2.Id == r.Id),
            JoinKind.Cross => previous.CrossJoin(c),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
    private static QueryCommand<MatrixRow> PosRows4(IDataContext ctx, JoinKind kind, bool outer, bool queryForm)
        => PosApply3(ctx, PosChain3(ctx, kind), outer, queryForm);

    private static QueryCommand<MatrixRow> PosApply3(
        IDataContext ctx,
        JoinedEntityBuilder<MatrixA, MatrixB, MatrixC> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC> p) => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.Item1.Id).ToCommand())
                : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC> p) => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.Item1.Id).ToCommand());
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item3.Id, Applied = q.Item4.Id });
        }

        var entityCommand = outer
            ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC> p) => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.Item1.Id))
            : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC> p) => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.Item1.Id));
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item3.Id, Applied = q.Item4.Id });
    }

    private static JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD> PosChain4(IDataContext ctx, JoinKind kind)
    {
        var previous = PosChain3(ctx, kind);
        var d = ctx.From<MatrixD>();
        return kind switch
        {
            JoinKind.Inner => previous.Join(d, (l, r) => l.Item3.Id == r.Id),
            JoinKind.Left => previous.LeftJoin(d, (l, r) => l.Item3.Id == r.Id),
            JoinKind.Right => previous.RightJoin(d, (l, r) => l.Item3.Id == r.Id),
            JoinKind.Full => previous.FullJoin(d, (l, r) => l.Item3.Id == r.Id),
            JoinKind.Cross => previous.CrossJoin(d),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
    private static QueryCommand<MatrixRow> PosRows5(IDataContext ctx, JoinKind kind, bool outer, bool queryForm)
        => PosApply4(ctx, PosChain4(ctx, kind), outer, queryForm);

    private static QueryCommand<MatrixRow> PosApply4(
        IDataContext ctx,
        JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD> p) => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.Item1.Id).ToCommand())
                : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD> p) => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.Item1.Id).ToCommand());
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item4.Id, Applied = q.Item5.Id });
        }

        var entityCommand = outer
            ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD> p) => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.Item1.Id))
            : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD> p) => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.Item1.Id));
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item4.Id, Applied = q.Item5.Id });
    }

    private static JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE> PosChain5(IDataContext ctx, JoinKind kind)
    {
        var previous = PosChain4(ctx, kind);
        var e = ctx.From<MatrixE>();
        return kind switch
        {
            JoinKind.Inner => previous.Join(e, (l, r) => l.Item4.Id == r.Id),
            JoinKind.Left => previous.LeftJoin(e, (l, r) => l.Item4.Id == r.Id),
            JoinKind.Right => previous.RightJoin(e, (l, r) => l.Item4.Id == r.Id),
            JoinKind.Full => previous.FullJoin(e, (l, r) => l.Item4.Id == r.Id),
            JoinKind.Cross => previous.CrossJoin(e),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
    private static QueryCommand<MatrixRow> PosRows6(IDataContext ctx, JoinKind kind, bool outer, bool queryForm)
        => PosApply5(ctx, PosChain5(ctx, kind), outer, queryForm);

    private static QueryCommand<MatrixRow> PosApply5(
        IDataContext ctx,
        JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE> p) => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.Item1.Id).ToCommand())
                : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE> p) => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.Item1.Id).ToCommand());
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item5.Id, Applied = q.Item6.Id });
        }

        var entityCommand = outer
            ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE> p) => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.Item1.Id))
            : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE> p) => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.Item1.Id));
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item5.Id, Applied = q.Item6.Id });
    }

    private static JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF> PosChain6(IDataContext ctx, JoinKind kind)
    {
        var previous = PosChain5(ctx, kind);
        var f = ctx.From<MatrixF>();
        return kind switch
        {
            JoinKind.Inner => previous.Join(f, (l, r) => l.Item5.Id == r.Id),
            JoinKind.Left => previous.LeftJoin(f, (l, r) => l.Item5.Id == r.Id),
            JoinKind.Right => previous.RightJoin(f, (l, r) => l.Item5.Id == r.Id),
            JoinKind.Full => previous.FullJoin(f, (l, r) => l.Item5.Id == r.Id),
            JoinKind.Cross => previous.CrossJoin(f),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
    private static QueryCommand<MatrixRow> PosRows7(IDataContext ctx, JoinKind kind, bool outer, bool queryForm)
        => PosApply6(ctx, PosChain6(ctx, kind), outer, queryForm);

    private static QueryCommand<MatrixRow> PosApply6(
        IDataContext ctx,
        JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF> p) => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.Item1.Id).ToCommand())
                : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF> p) => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.Item1.Id).ToCommand());
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item6.Id, Applied = q.Item7.Id });
        }

        var entityCommand = outer
            ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF> p) => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.Item1.Id))
            : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF> p) => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.Item1.Id));
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item6.Id, Applied = q.Item7.Id });
    }

    private static JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF, MatrixG> PosChain7(IDataContext ctx, JoinKind kind)
    {
        var previous = PosChain6(ctx, kind);
        var g = ctx.From<MatrixG>();
        return kind switch
        {
            JoinKind.Inner => previous.Join(g, (l, r) => l.Item6.Id == r.Id),
            JoinKind.Left => previous.LeftJoin(g, (l, r) => l.Item6.Id == r.Id),
            JoinKind.Right => previous.RightJoin(g, (l, r) => l.Item6.Id == r.Id),
            JoinKind.Full => previous.FullJoin(g, (l, r) => l.Item6.Id == r.Id),
            JoinKind.Cross => previous.CrossJoin(g),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
    private static QueryCommand<MatrixRow> PosRows8(IDataContext ctx, JoinKind kind, bool outer, bool queryForm)
        => PosApply7(ctx, PosChain7(ctx, kind), outer, queryForm);

    private static QueryCommand<MatrixRow> PosApply7(
        IDataContext ctx,
        JoinedEntityBuilder<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF, MatrixG> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF, MatrixG> p) => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.Item1.Id).ToCommand())
                : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF, MatrixG> p) => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.Item1.Id).ToCommand());
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item7.Id, Applied = q.Item8.Id });
        }

        var entityCommand = outer
            ? left.OuterApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF, MatrixG> p) => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.Item1.Id))
            : left.CrossApply((Projection<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF, MatrixG> p) => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.Item1.Id));
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item7.Id, Applied = q.Item8.Id });
    }


    // ---------------------------------------------------------------------------------------------
    // Alias receiver: the generated schema keeps its named slots and the APPLY marker names the added
    // one (marker last, no options); ItemN still addresses every named slot.
    // ---------------------------------------------------------------------------------------------

    private static QueryCommand<MatrixRow> AliasRows3(IDataContext ctx, JoinKind kind, bool outer, bool queryForm) => kind switch
    {
        JoinKind.Inner => AliasApply3(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).Join(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1), outer, queryForm),
        JoinKind.Left => AliasApply3(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).LeftJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1), outer, queryForm),
        JoinKind.Right => AliasApply3(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).RightJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1), outer, queryForm),
        JoinKind.Full => AliasApply3(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).FullJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1), outer, queryForm),
        JoinKind.Cross => AliasApply3(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).CrossJoin(ctx.From<MatrixB>(), Alias.MS1), outer, queryForm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static QueryCommand<MatrixRow> AliasApply3(
        IDataContext ctx,
        AliasJoin_A1_MRoot_A2_MS1<MatrixA, MatrixB> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied)
                : left.CrossApply(p => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied);
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item2.Id, Applied = q.MApplied.Id });
        }

        var entityCommand = outer
            ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.MRoot.Id), Alias.MApplied)
            : left.CrossApply(p => applied.Where(x => x.Ref == p.Item2.Id && x.Id == p.MRoot.Id), Alias.MApplied);
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item2.Id, Applied = q.MApplied.Id });
    }

    private static QueryCommand<MatrixRow> AliasRows4(IDataContext ctx, JoinKind kind, bool outer, bool queryForm) => kind switch
    {
        JoinKind.Inner => AliasApply4(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).Join(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).Join(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2), outer, queryForm),
        JoinKind.Left => AliasApply4(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).LeftJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).LeftJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2), outer, queryForm),
        JoinKind.Right => AliasApply4(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).RightJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).RightJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2), outer, queryForm),
        JoinKind.Full => AliasApply4(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).FullJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).FullJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2), outer, queryForm),
        JoinKind.Cross => AliasApply4(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).CrossJoin(ctx.From<MatrixB>(), Alias.MS1).CrossJoin(ctx.From<MatrixC>(), Alias.MS2), outer, queryForm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static QueryCommand<MatrixRow> AliasApply4(
        IDataContext ctx,
        AliasJoin_A1_MRoot_A2_MS1_A3_MS2<MatrixA, MatrixB, MatrixC> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied)
                : left.CrossApply(p => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied);
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item3.Id, Applied = q.MApplied.Id });
        }

        var entityCommand = outer
            ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.MRoot.Id), Alias.MApplied)
            : left.CrossApply(p => applied.Where(x => x.Ref == p.Item3.Id && x.Id == p.MRoot.Id), Alias.MApplied);
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item3.Id, Applied = q.MApplied.Id });
    }

    private static QueryCommand<MatrixRow> AliasRows5(IDataContext ctx, JoinKind kind, bool outer, bool queryForm) => kind switch
    {
        JoinKind.Inner => AliasApply5(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).Join(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).Join(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).Join(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3), outer, queryForm),
        JoinKind.Left => AliasApply5(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).LeftJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).LeftJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).LeftJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3), outer, queryForm),
        JoinKind.Right => AliasApply5(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).RightJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).RightJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).RightJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3), outer, queryForm),
        JoinKind.Full => AliasApply5(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).FullJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).FullJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).FullJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3), outer, queryForm),
        JoinKind.Cross => AliasApply5(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).CrossJoin(ctx.From<MatrixB>(), Alias.MS1).CrossJoin(ctx.From<MatrixC>(), Alias.MS2).CrossJoin(ctx.From<MatrixD>(), Alias.MS3), outer, queryForm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static QueryCommand<MatrixRow> AliasApply5(
        IDataContext ctx,
        AliasJoin_A1_MRoot_A2_MS1_A3_MS2_A4_MS3<MatrixA, MatrixB, MatrixC, MatrixD> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied)
                : left.CrossApply(p => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied);
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item4.Id, Applied = q.MApplied.Id });
        }

        var entityCommand = outer
            ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.MRoot.Id), Alias.MApplied)
            : left.CrossApply(p => applied.Where(x => x.Ref == p.Item4.Id && x.Id == p.MRoot.Id), Alias.MApplied);
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item4.Id, Applied = q.MApplied.Id });
    }

    private static QueryCommand<MatrixRow> AliasRows6(IDataContext ctx, JoinKind kind, bool outer, bool queryForm) => kind switch
    {
        JoinKind.Inner => AliasApply6(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).Join(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).Join(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).Join(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).Join(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4), outer, queryForm),
        JoinKind.Left => AliasApply6(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).LeftJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).LeftJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).LeftJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).LeftJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4), outer, queryForm),
        JoinKind.Right => AliasApply6(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).RightJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).RightJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).RightJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).RightJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4), outer, queryForm),
        JoinKind.Full => AliasApply6(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).FullJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).FullJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).FullJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).FullJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4), outer, queryForm),
        JoinKind.Cross => AliasApply6(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).CrossJoin(ctx.From<MatrixB>(), Alias.MS1).CrossJoin(ctx.From<MatrixC>(), Alias.MS2).CrossJoin(ctx.From<MatrixD>(), Alias.MS3).CrossJoin(ctx.From<MatrixE>(), Alias.MS4), outer, queryForm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static QueryCommand<MatrixRow> AliasApply6(
        IDataContext ctx,
        AliasJoin_A1_MRoot_A2_MS1_A3_MS2_A4_MS3_A5_MS4<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied)
                : left.CrossApply(p => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied);
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item5.Id, Applied = q.MApplied.Id });
        }

        var entityCommand = outer
            ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.MRoot.Id), Alias.MApplied)
            : left.CrossApply(p => applied.Where(x => x.Ref == p.Item5.Id && x.Id == p.MRoot.Id), Alias.MApplied);
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item5.Id, Applied = q.MApplied.Id });
    }

    private static QueryCommand<MatrixRow> AliasRows7(IDataContext ctx, JoinKind kind, bool outer, bool queryForm) => kind switch
    {
        JoinKind.Inner => AliasApply7(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).Join(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).Join(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).Join(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).Join(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).Join(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5), outer, queryForm),
        JoinKind.Left => AliasApply7(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).LeftJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).LeftJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).LeftJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).LeftJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).LeftJoin(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5), outer, queryForm),
        JoinKind.Right => AliasApply7(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).RightJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).RightJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).RightJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).RightJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).RightJoin(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5), outer, queryForm),
        JoinKind.Full => AliasApply7(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).FullJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).FullJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).FullJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).FullJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).FullJoin(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5), outer, queryForm),
        JoinKind.Cross => AliasApply7(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).CrossJoin(ctx.From<MatrixB>(), Alias.MS1).CrossJoin(ctx.From<MatrixC>(), Alias.MS2).CrossJoin(ctx.From<MatrixD>(), Alias.MS3).CrossJoin(ctx.From<MatrixE>(), Alias.MS4).CrossJoin(ctx.From<MatrixF>(), Alias.MS5), outer, queryForm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static QueryCommand<MatrixRow> AliasApply7(
        IDataContext ctx,
        AliasJoin_A1_MRoot_A2_MS1_A3_MS2_A4_MS3_A5_MS4_A6_MS5<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied)
                : left.CrossApply(p => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied);
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item6.Id, Applied = q.MApplied.Id });
        }

        var entityCommand = outer
            ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.MRoot.Id), Alias.MApplied)
            : left.CrossApply(p => applied.Where(x => x.Ref == p.Item6.Id && x.Id == p.MRoot.Id), Alias.MApplied);
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item6.Id, Applied = q.MApplied.Id });
    }

    private static QueryCommand<MatrixRow> AliasRows8(IDataContext ctx, JoinKind kind, bool outer, bool queryForm) => kind switch
    {
        JoinKind.Inner => AliasApply8(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).Join(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).Join(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).Join(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).Join(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).Join(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5).Join(ctx.From<MatrixG>(), (l, r) => l.MS5.Id == r.Id, Alias.MS6), outer, queryForm),
        JoinKind.Left => AliasApply8(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).LeftJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).LeftJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).LeftJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).LeftJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).LeftJoin(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5).LeftJoin(ctx.From<MatrixG>(), (l, r) => l.MS5.Id == r.Id, Alias.MS6), outer, queryForm),
        JoinKind.Right => AliasApply8(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).RightJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).RightJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).RightJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).RightJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).RightJoin(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5).RightJoin(ctx.From<MatrixG>(), (l, r) => l.MS5.Id == r.Id, Alias.MS6), outer, queryForm),
        JoinKind.Full => AliasApply8(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).FullJoin(ctx.From<MatrixB>(), (l, r) => l.MRoot.Id == r.Id, Alias.MS1).FullJoin(ctx.From<MatrixC>(), (l, r) => l.MS1.Id == r.Id, Alias.MS2).FullJoin(ctx.From<MatrixD>(), (l, r) => l.MS2.Id == r.Id, Alias.MS3).FullJoin(ctx.From<MatrixE>(), (l, r) => l.MS3.Id == r.Id, Alias.MS4).FullJoin(ctx.From<MatrixF>(), (l, r) => l.MS4.Id == r.Id, Alias.MS5).FullJoin(ctx.From<MatrixG>(), (l, r) => l.MS5.Id == r.Id, Alias.MS6), outer, queryForm),
        JoinKind.Cross => AliasApply8(ctx, ctx.From<MatrixA>().WithAlias(Alias.MRoot).CrossJoin(ctx.From<MatrixB>(), Alias.MS1).CrossJoin(ctx.From<MatrixC>(), Alias.MS2).CrossJoin(ctx.From<MatrixD>(), Alias.MS3).CrossJoin(ctx.From<MatrixE>(), Alias.MS4).CrossJoin(ctx.From<MatrixF>(), Alias.MS5).CrossJoin(ctx.From<MatrixG>(), Alias.MS6), outer, queryForm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static QueryCommand<MatrixRow> AliasApply8(
        IDataContext ctx,
        AliasJoin_A1_MRoot_A2_MS1_A3_MS2_A4_MS3_A5_MS4_A6_MS5_A7_MS6<MatrixA, MatrixB, MatrixC, MatrixD, MatrixE, MatrixF, MatrixG> left,
        bool outer,
        bool queryForm)
    {
        var applied = ctx.From<MatrixH>();
        if (queryForm)
        {
            var command = outer
                ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied)
                : left.CrossApply(p => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.MRoot.Id).ToCommand(), Alias.MApplied);
            return command.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item7.Id, Applied = q.MApplied.Id });
        }

        var entityCommand = outer
            ? left.OuterApply(p => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.MRoot.Id), Alias.MApplied)
            : left.CrossApply(p => applied.Where(x => x.Ref == p.Item7.Id && x.Id == p.MRoot.Id), Alias.MApplied);
        return entityCommand.Select(q => new MatrixRow { Left = q.Item1.Id, Middle = q.Item7.Id, Applied = q.MApplied.Id });
    }


    // ---------------------------------------------------------------------------------------------
    // Assertions
    // ---------------------------------------------------------------------------------------------

    private static void AssertShape(MatrixCell cell, JoinKind kind, int total, string sql)
    {
        var outer = cell is MatrixCell.M13 or MatrixCell.M14 or MatrixCell.M15 or MatrixCell.M16;
        var appliedAlias = total + 1;
        var bodyAlias = total;
        var precedingJoins = total - 2;
        var flat = sql.ToLowerInvariant();

        // Exact provider form: the ANSI lateral spelling, never the SQL Server CROSS/OUTER APPLY spelling.
        if (outer)
        {
            CountOf(flat, "left join lateral (").Should().Be(1, "the cell must render exactly one left lateral join");
            CountOf(flat, "cross join lateral").Should().Be(0, "an outer cell must not render a cross lateral join");
            CountOf(flat, "on true").Should().Be(1, "a left lateral join needs exactly one ON TRUE");
        }
        else
        {
            CountOf(flat, "cross join lateral (").Should().Be(1, "the cell must render exactly one cross lateral join");
            CountOf(flat, "left join lateral").Should().Be(0, "a cross cell must not render a left lateral join");

            // AC06 named negative: a cross lateral join must never acquire ON TRUE.
            flat.Should().NotContain("on true", "a cross join lateral must not acquire ON TRUE");
        }

        flat.Should().NotContain("apply", "this dialect renders the ANSI lateral spelling, never APPLY");

        // The preceding chain survives the APPLY: one join keyword per preceding join plus the lateral
        // join itself, and every qualified spelling is counted exactly so the chain stays homogeneous.
        CountOf(flat, " join ").Should().Be(precedingJoins + 1, "every preceding join must survive the APPLY");
        CountOf(flat, " left join ").Should().Be((kind == JoinKind.Left ? precedingJoins : 0) + (outer ? 1 : 0));
        CountOf(flat, " right join ").Should().Be(kind == JoinKind.Right ? precedingJoins : 0);
        CountOf(flat, " full join ").Should().Be(kind == JoinKind.Full ? precedingJoins : 0);
        CountOf(flat, " cross join ").Should().Be((kind == JoinKind.Cross ? precedingJoins : 0) + (outer ? 0 : 1));
        (CountOf(flat, " join ") - CountOf(flat, " left join ") - CountOf(flat, " right join ")
            - CountOf(flat, " full join ") - CountOf(flat, " cross join "))
            .Should().Be(kind == JoinKind.Inner ? precedingJoins : 0, "the chain is homogeneous in its join kind");

        // Correlation: the applied body references the last preceding occurrence ({slot total-1}) and
        // the FROM occurrence (slot 1) by their exact aliases.
        flat.Should().Contain($"t{bodyAlias}.ref = t{total - 1}.id", "the last preceding source occurrence must be addressed");
        flat.Should().Contain($"t{bodyAlias}.id = t1.id", "the FROM source occurrence must be addressed");
        flat.Should().NotContain("= ref");
        flat.Should().Contain($"as \"t{appliedAlias}\"", "the applied source needs its own alias");
        flat.Should().Contain($"t{appliedAlias}.id", "the projection must read the applied source");
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None)).DbCommand.CommandText);

    // ---------------------------------------------------------------------------------------------
    // Evidence dump (only when NEXTORM_T324_SQL_DUMP names a directory).
    // ---------------------------------------------------------------------------------------------

    private static readonly object DumpLock = new();

    private static void Dump(MatrixCell cell, JoinKind kind, int total, string sql)
    {
        var directory = Environment.GetEnvironmentVariable("NEXTORM_T324_SQL_DUMP");
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Directory.CreateDirectory(directory);
        var outerApply = cell is MatrixCell.M13 or MatrixCell.M14 or MatrixCell.M15 or MatrixCell.M16;
        var record = new Dictionary<string, object?>
        {
            ["cell"] = cell.ToString(),
            ["provider"] = "postgres",
            ["kind"] = outerApply ? "outer" : "cross",
            ["surface"] = cell is MatrixCell.M11 or MatrixCell.M12 or MatrixCell.M15 or MatrixCell.M16 ? "alias" : "positional",
            ["lambdaForm"] = cell is MatrixCell.M10 or MatrixCell.M12 or MatrixCell.M14 or MatrixCell.M16 ? "QueryCommand" : "EntityBuilder",
            ["joinKind"] = kind.ToString(),
            ["total"] = total,
            ["expectedForm"] = outerApply ? "left join lateral" : "cross join lateral",
            ["executed"] = true,
            ["sqlHash"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql))),
            ["sql"] = sql,
        };

        var line = JsonSerializer.Serialize(record);

        lock (DumpLock)
            File.AppendAllText(Path.Combine(directory, "postgres.jsonl"), line + Environment.NewLine);
    }
}

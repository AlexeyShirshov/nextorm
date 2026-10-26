using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Core.Tests;

// Fixtures for T324 D6a (issue #324, correlated CROSS/OUTER APPLY after a join). Every entity has a
// distinct table name and the two mapped columns Id and Code, so a correlation that resolves to the
// wrong source occurrence renders a visibly different qualified column pair.

[SqlTable("t324_apply_a")]
public sealed class T324A
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_b")]
public sealed class T324B
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_c")]
public sealed class T324C
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_d")]
public sealed class T324D
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_e")]
public sealed class T324E
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_f")]
public sealed class T324F
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_g")]
public sealed class T324G
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_h")]
public sealed class T324H
{
    public int Id { get; set; }

    public int Code { get; set; }
}

[SqlTable("t324_apply_i")]
public sealed class T324I
{
    public int Id { get; set; }

    public int Code { get; set; }
}

/// <summary>Flat row type used where a command result type must be named (clone/reuse assertions).</summary>
public sealed class T324FlatRow
{
    public int S1 { get; set; }

    public int S2 { get; set; }

    public int S3 { get; set; }
}

/// <summary>
/// Sentinel for the non-evaluation requirement: a ninth positional APPLY must be refused without
/// compiling or invoking the source lambda, so this method would record and throw if it were evaluated.
/// </summary>
public static class T324ApplySentinel
{
    /// <summary>Number of times a rejected APPLY source body was evaluated.</summary>
    public static int Calls { get; private set; }

    /// <summary>Resets the counter between tests (the suite is serialized).</summary>
    public static void Reset() => Calls = 0;

    /// <summary>Records the evaluation and throws, so a leaked evaluation cannot pass silently.</summary>
    /// <param name="value">The correlated operand.</param>
    /// <returns>Never returns.</returns>
    public static int Probe(int value)
    {
        Calls++;
        throw new InvalidOperationException($"the APPLY source body was evaluated (operand {value})");
    }
}

internal static class T324ApplyFixture
{
    public const string SqlServerConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    public static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    public static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    /// <summary>Returns the compile-time type of the argument, used to assert the static result shape.</summary>
    public static Type StaticTypeOf<T>(T value)
    {
        _ = value;
        return typeof(T);
    }

    public static JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G> BuildSeven(IDataContext ctx)
        => ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<T324C>(), (p, c) => p.Item1.Id == c.Id)
            .Join(ctx.From<T324D>(), (p, d) => p.Item2.Id == d.Id)
            .Join(ctx.From<T324E>(), (p, e) => p.Item3.Id == e.Id)
            .Join(ctx.From<T324F>(), (p, f) => p.Item4.Id == f.Id)
            .Join(ctx.From<T324G>(), (p, g) => p.Item5.Id == g.Id);

    public static JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H> BuildEight(IDataContext ctx)
        => BuildSeven(ctx).Join(ctx.From<T324H>(), (p, h) => p.Item6.Id == h.Id);

    public static Expression<Func<Projection<T324A, T324B>, bool>> IntPredicate()
        => p => p.Item1.Id > p.Item2.Id;
}

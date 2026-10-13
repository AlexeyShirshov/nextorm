using System;
using NextORM.Core;

// T222 (#222) CHECK#3 escalation probe (compile-positive): the flat arity-8 scope bound is NOT a
// compile-time rejection. After eight source-less joins the receiver is the terminal
// JoinedEntityBuilder<E1..E8>; a ninth source-less join compiles through the inherited generic-base
// EntityBuilder<Projection<E1..E8>>.Join<TJoinEntity>(...) member and wraps the accumulated
// eight-table projection as Item1.
namespace T222.Compat.Ninth;

public sealed class E1 { public int Id { get; set; } }
public sealed class E2 { public int Id { get; set; } }
public sealed class E3 { public int Id { get; set; } }
public sealed class E4 { public int Id { get; set; } }
public sealed class E5 { public int Id { get; set; } }
public sealed class E6 { public int Id { get; set; } }
public sealed class E7 { public int Id { get; set; } }
public sealed class E8 { public int Id { get; set; } }
public sealed class E9 { public int Id { get; set; } }

public static class NinthJoin
{
    private static void Touch<T>(T value) => GC.KeepAlive(value);

    public static void ChainsNinthSourceLessJoin(IDataContext ctx)
    {
        var joined8 = ctx.From<E1>()
            .Join<E2>((a, b) => a.Id == b.Id)
            .Join<E3>((p, c) => c.Id == 0)
            .Join<E4>((p, c) => c.Id == 0)
            .Join<E5>((p, c) => c.Id == 0)
            .Join<E6>((p, c) => c.Id == 0)
            .Join<E7>((p, c) => c.Id == 0)
            .Join<E8>((p, c) => c.Id == 0);

        // Terminal arity-8 receiver: compiles via the inherited generic-base member (not a new overload).
        JoinedEntityBuilder<Projection<E1, E2, E3, E4, E5, E6, E7, E8>, E9> joined9 =
            joined8.Join<E9>((p, c) => c.Id == 0);

        Touch(joined9);
    }
}

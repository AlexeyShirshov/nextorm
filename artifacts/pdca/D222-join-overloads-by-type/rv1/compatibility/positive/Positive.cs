using System;
using NextORM.Core;

// T222 (#222) rv=1 compatibility fixture — compile-positive.
// Exercises all 28 new source-less join overloads (4 kinds x receiver arities 1..7)
// and asserts the resulting projection arity through explicit typed locals. It also
// covers omitted options, explicit null options and a non-null options callback.
namespace T222.Compat;

public sealed class E1 { public int Id { get; set; } }
public sealed class E2 { public int Id { get; set; } }
public sealed class E3 { public int Id { get; set; } }
public sealed class E4 { public int Id { get; set; } }
public sealed class E5 { public int Id { get; set; } }
public sealed class E6 { public int Id { get; set; } }
public sealed class E7 { public int Id { get; set; } }
public sealed class E8 { public int Id { get; set; } }

public static class Positive
{
    private static void Touch<T>(T value) => GC.KeepAlive(value);

    public static void CompileAll28(IDataContext ctx)
    {
        // --- Join: receiver arities 1..7 ---
        JoinedEntityBuilder<E1, E2> ji2 = ctx.From<E1>().Join<E2>((a, b) => a.Id == b.Id);
        JoinedEntityBuilder<E1, E2, E3> ji3 = ji2.Join<E3>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4> ji4 = ji3.Join<E4>((p, c) => c.Id == 0, null);
        JoinedEntityBuilder<E1, E2, E3, E4, E5> ji5 = ji4.Join<E5>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6> ji6 = ji5.Join<E6>((p, c) => c.Id == 0, o => { _ = o; });
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7> ji7 = ji6.Join<E7>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7, E8> ji8 = ji7.Join<E8>((p, c) => c.Id == 0);
        Touch(ji8);

        // --- LeftJoin: receiver arities 1..7 ---
        JoinedEntityBuilder<E1, E2> ll2 = ctx.From<E1>().LeftJoin<E2>((a, b) => a.Id == b.Id, null);
        JoinedEntityBuilder<E1, E2, E3> ll3 = ll2.LeftJoin<E3>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4> ll4 = ll3.LeftJoin<E4>((p, c) => c.Id == 0, o => { _ = o; });
        JoinedEntityBuilder<E1, E2, E3, E4, E5> ll5 = ll4.LeftJoin<E5>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6> ll6 = ll5.LeftJoin<E6>((p, c) => c.Id == 0, null);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7> ll7 = ll6.LeftJoin<E7>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7, E8> ll8 = ll7.LeftJoin<E8>((p, c) => c.Id == 0);
        Touch(ll8);

        // --- RightJoin: receiver arities 1..7 ---
        JoinedEntityBuilder<E1, E2> ri2 = ctx.From<E1>().RightJoin<E2>((a, b) => a.Id == b.Id);
        JoinedEntityBuilder<E1, E2, E3> ri3 = ri2.RightJoin<E3>((p, c) => c.Id == 0, null);
        JoinedEntityBuilder<E1, E2, E3, E4> ri4 = ri3.RightJoin<E4>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5> ri5 = ri4.RightJoin<E5>((p, c) => c.Id == 0, o => { _ = o; });
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6> ri6 = ri5.RightJoin<E6>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7> ri7 = ri6.RightJoin<E7>((p, c) => c.Id == 0, null);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7, E8> ri8 = ri7.RightJoin<E8>((p, c) => c.Id == 0);
        Touch(ri8);

        // --- FullJoin: receiver arities 1..7 ---
        JoinedEntityBuilder<E1, E2> fi2 = ctx.From<E1>().FullJoin<E2>((a, b) => a.Id == b.Id, o => { _ = o; });
        JoinedEntityBuilder<E1, E2, E3> fi3 = fi2.FullJoin<E3>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4> fi4 = fi3.FullJoin<E4>((p, c) => c.Id == 0, null);
        JoinedEntityBuilder<E1, E2, E3, E4, E5> fi5 = fi4.FullJoin<E5>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6> fi6 = fi5.FullJoin<E6>((p, c) => c.Id == 0, o => { _ = o; });
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7> fi7 = fi6.FullJoin<E7>((p, c) => c.Id == 0);
        JoinedEntityBuilder<E1, E2, E3, E4, E5, E6, E7, E8> fi8 = fi7.FullJoin<E8>((p, c) => c.Id == 0);
        Touch(fi8);
    }
}

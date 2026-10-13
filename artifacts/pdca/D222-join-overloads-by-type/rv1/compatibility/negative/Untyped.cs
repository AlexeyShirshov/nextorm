using NextORM.Core;

// T222 (#222) rv=1 compatibility fixture — compile-negative (V22).
// An untyped lambda whose join entity type cannot be inferred must not silently pick
// another API: the source-less overload is the only one-argument candidate, so type
// inference fails with CS0411. The expected diagnostic is asserted by run.sh.
namespace T222.Compat.Neg;

public sealed class N1 { public int Id { get; set; } }

public static class Untyped
{
    public static void MustNotInfer(IDataContext ctx)
    {
        _ = ctx.From<N1>().Join((left, right) => true);
    }
}

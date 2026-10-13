#nullable disable

using NextORM.Core;

// T222 (#222) rv=1 compatibility fixture — compile-positive (V24, observed).
// The invalid all-null argument pair on a joined receiver was expected to become ambiguous between
// the existing source-explicit overload and the new (predicate, options) overload. The actual
// compiler result is that it still compiles (one candidate wins overload resolution), i.e. the new
// overloads introduce no new ambiguity here. With nullable analysis on, the null literals trip
// CS8625 (warnings-as-errors), so nullable is disabled to observe the overload-resolution result.
namespace T222.Compat.NegAllNull;

public sealed class A1 { public int Id { get; set; } }
public sealed class A2 { public int Id { get; set; } }

public static class AllNull
{
    public static void Ambiguous(IDataContext ctx)
    {
        var joined = ctx.From<A1>().Join<A2>((a, b) => a.Id == b.Id);
        _ = joined.Join<A1>(null, null);
    }
}

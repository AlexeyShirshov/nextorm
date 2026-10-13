#nullable disable
using NextORM.Core;

namespace Probe;

public sealed class A1 { public int Id { get; set; } }
public sealed class A2 { public int Id { get; set; } }

public static class AllNull
{
    public static void Probe(IDataContext ctx)
    {
        var joined = ctx.From<A1>().Join(ctx.From<A2>(), (a, b) => a.Id == b.Id);
        _ = joined.Join<A1>(null, null);
    }
}

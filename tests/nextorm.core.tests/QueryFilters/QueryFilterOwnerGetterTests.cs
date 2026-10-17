using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the exact owner-getter identity contract of the EF Core query-filter bridge: only the method
/// the bridge registers is treated as the owner getter that core parameterises against the live
/// context. Any other static helper over <see cref="QueryFilterContext.Context"/> is a native filter and
/// must keep its own translation path.
/// </summary>
public class QueryFilterOwnerGetterTests
{
    [Fact]
    public void NativeStaticContextHelperIsNotOwnerGetter()
    {
        using var ctx = new InMemoryDataContext();
        var host = new QueryFilterContext(ctx);
        var contextAccess = Expression.Property(Expression.Constant(host), nameof(QueryFilterContext.Context));
        var nativeHelper = typeof(QueryFilterOwnerGetterTests).GetMethod(
            nameof(NativeHelper), BindingFlags.NonPublic | BindingFlags.Static)!;

        var helperCall = Expression.Call(nativeHelper, contextAccess);

        QueryFilterContextAccessor.IsOwnerGetter(helperCall).Should().BeFalse(
            "only the exact owner-getter registered by the EF Core bridge is recognised; a native static helper over Context keeps native semantics");
    }

    [Fact]
    public void BuildSourceName_TruncatedMethodCallChainsCollide()
    {
        var receiver = Expression.Parameter(typeof(Probe), "receiver");
        var getCall = Expression.Call(receiver, typeof(Probe).GetMethod(nameof(Probe.Get))!);
        var id = Expression.Property(getCall, nameof(Probe.Id));
        var other = Expression.Property(getCall, nameof(Probe.Other));

        // The walk stops at the first method call, so two chains over different receivers that share the
        // method and leaf names collapse to one source name; different leaves stay distinct.
        QueryFilterContextAccessor.BuildSourceName(id).Should().Be("Get_Id");
        QueryFilterContextAccessor.BuildSourceName(other).Should().Be("Get_Other");
        QueryFilterContextAccessor.BuildSourceName(id).Should().NotBe(QueryFilterContextAccessor.BuildSourceName(other));
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public int Other { get; set; }

        public Probe Get() => this;
    }

    private static int NativeHelper(IDataContext context) => context.Properties.Count;
}

using System.Linq;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.Core.Tests;

public class TableGuardSweepTests
{
    [SqlTable("t")]
    public sealed class E { public int Id { get; set; } }

    private static IDataContext Ctx() => new InMemoryDataContext();

    private static void ExpectGuard(Action act, string? table)
    {
        if (table is null)
            act.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("table");
        else
            act.Should().ThrowExactly<ArgumentException>().Which.ParamName.Should().Be("table");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DataContextExtensions_From_BothOverloads_Guard(string? table)
    {
        var ctx = Ctx();
        ExpectGuard(() => ctx.From(table!), table);
        if (table is not null)
        {
            // second overload: guard before options runs
            bool optionsRan = false;
            ExpectGuard(() => ctx.From(table!, _ => { optionsRan = true; }), table);
            optionsRan.Should().BeFalse("guard must run before options");
        }
        else
        {
            bool optionsRan = false;
            ExpectGuard(() => ctx.From(table!, _ => { optionsRan = true; }), table);
            optionsRan.Should().BeFalse("guard must run before options");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EntityBuilder_Generic_StringJoins_Guard(string? table)
    {
        var ctx = Ctx();
        var b = ctx.From<E>();
        ExpectGuard(() => b.Join(table!, (e, t) => true), table);
        ExpectGuard(() => b.LeftJoin(table!, (e, t) => true), table);
        ExpectGuard(() => b.RightJoin(table!, (e, t) => true), table);
        ExpectGuard(() => b.FullJoin(table!, (e, t) => true), table);
        ExpectGuard(() => b.CrossJoin(table!), table);
        ExpectGuard(() => b.CrossApply(table!), table);
        ExpectGuard(() => b.OuterApply(table!), table);
    }

    private static JoinedEntityBuilder<TableAlias, TableAlias> BuildArity2(IDataContext ctx)
        => ctx.From("a").Join("b", (t1, t2) => true);

    private static JoinedEntityBuilder<TableAlias, TableAlias, TableAlias> BuildArity3(IDataContext ctx)
        => BuildArity2(ctx).Join("c", (p, t) => true);

    private static JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias> BuildArity4(IDataContext ctx)
        => BuildArity3(ctx).Join("d", (p, t) => true);

    private static JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> BuildArity5(IDataContext ctx)
        => BuildArity4(ctx).Join("e", (p, t) => true);

    private static JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> BuildArity6(IDataContext ctx)
        => BuildArity5(ctx).Join("f", (p, t) => true);

    private static JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> BuildArity7(IDataContext ctx)
        => BuildArity6(ctx).Join("g", (p, t) => true);

    private static JoinedEntityBuilder<TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias, TableAlias> BuildArity8(IDataContext ctx)
        => BuildArity7(ctx).Join("h", (p, t) => true);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void JoinedEntityBuilder_AllArityOps_Guard(int arity)
    {
        var ctx = Ctx();
        object baseBuilder = arity switch
        {
            2 => BuildArity2(ctx),
            3 => BuildArity3(ctx),
            4 => BuildArity4(ctx),
            5 => BuildArity5(ctx),
            6 => BuildArity6(ctx),
            7 => BuildArity7(ctx),
            8 => BuildArity8(ctx),
            _ => throw new InvalidOperationException()
        };

        // We need to call string overloads on the concrete JoinedEntityBuilder type. Since the static
        // type here is the base JoinedEntityBuilder, the string overloads are declared on the derived
        // generic. Use reflection to invoke each op method with a string table.
        var type = baseBuilder.GetType();
        string[] ops = { "Join", "LeftJoin", "RightJoin", "FullJoin", "CrossJoin", "CrossApply", "OuterApply" };
        foreach (var op in ops)
        {
            foreach (var table in new string?[] { null, "", "   " })
            {
                var method = type.GetMethod(op, new[] { typeof(string) }) 
                    ?? type.GetMethod(op, new[] { typeof(string), typeof(Action<JoinOptions>) });
                // Join/Left/Right/Full take (string, Expression, Action?) not (string) only; Cross* take (string, Action?)
                // Resolve the correct overload:
                var mi = type.GetMethods().FirstOrDefault(m => m.Name == op && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string));
                mi.Should().NotBeNull($"method {op} not found on {type.Name}");
                var args = new object?[mi!.GetParameters().Length];
                args[0] = table;
                Action act = () =>
                {
                    try { mi.Invoke(baseBuilder, args); }
                    catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is not null)
                    {
                        throw tie.InnerException;
                    }
                };
                ExpectGuard(act, table);
            }
        }
    }

    // --- VM-9 / VM-10 / AC-5 preservation (focused) ---

    private static void ExpectGuardNamed(Action act, string? table, string paramName)
    {
        if (table is null)
            act.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be(paramName);
        else
            act.Should().ThrowExactly<ArgumentException>().Which.ParamName.Should().Be(paramName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EntityBuilder_Generic_Ctor_Guards(string? table)
    {
        var ctx = Ctx();
        ExpectGuardNamed(() => { var _ = new EntityBuilder<TableAlias>(ctx, table!); }, table, "table");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CteQuery_From_Guards(string? table)
    {
        var ctx = Ctx();
        var cte = new CteQuery(ctx, Array.Empty<CteDefinition>());
        ExpectGuardNamed(() => cte.From(table!), table, "cteName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MutationCteQuery_FromTable_Guards(string? table)
    {
        var ctx = Ctx();
        var m = new MutationCteQuery<int>(ctx, Array.Empty<CteDefinition>(), "m");
        ExpectGuardNamed(() => m.FromTable(table!), table, "cteName");
    }

    [Fact]
    public void DataContextExtensions_From_OptionsCallback_InvokedOnce_OnValidName()
    {
        var ctx = Ctx();
        int count = 0;
        var builder = ctx.From("users", _ => { count++; });
        builder.Should().NotBeNull();
        count.Should().Be(1, "options callback must run exactly once for a valid table name");
    }

    // AC-5 valid-name preservation
    [Fact]
    public void Ac5_ValidTableNames_Construct()
    {
        var ctx = Ctx();
        ctx.From("users").Should().NotBeNull();
        var b = ctx.From<E>();
        b.Join("users", (e, t) => true).Should().NotBeNull();
        b.LeftJoin("users", (e, t) => true).Should().NotBeNull();
        b.CrossJoin("users").Should().NotBeNull();
        b.CrossApply("users").Should().NotBeNull();
        b.OuterApply("users").Should().NotBeNull();
        var ng = ctx.From("a");
        ng.Join("users", (t1, t2) => true).Should().NotBeNull();
        ng.CrossJoin("users").Should().NotBeNull();
        var j2 = ctx.From("a").Join("b", (t1, t2) => true);
        j2.Join("users", (p, t) => true).Should().NotBeNull();
    }

    [Fact]
    public void Ac5_Cte_From_ValidNames()
    {
        var ctx = Ctx();
        var cte = new CteQuery(ctx, Array.Empty<CteDefinition>());
        cte.From("users").Should().NotBeNull();
        var readCte = new CteDefinition("read", ctx.From<SimpleEntity>().Select(x => new { x.Id }));
        var m = new MutationCteQuery<int>(ctx, new[] { readCte }, "m");
        m.FromTable("read").Should().NotBeNull();
    }

    [Fact]
    public void Ac5_DeferredNull_Preserved()
    {
        var ctx = Ctx();
        // EntityBuilder(IDataContext) leaves _table null legitimately
        var b = new EntityBuilder<E>(ctx);
        b.Should().NotBeNull();
        // CloneImp on a null-table builder must not throw
        var clone = b.Clone();
        clone.Should().NotBeNull();
        // non-generic deferred-null
        var ng = new EntityBuilder(ctx);
        ng.Should().NotBeNull();
        ng.Clone().Should().NotBeNull();
    }

    [Fact]
    public void Ac5_DirectCtor_ValidName()
    {
        var ctx = Ctx();
        new EntityBuilder<TableAlias>(ctx, "users").Should().NotBeNull();
    }

    [Fact]
    public void Ac5_AssertionStyle_And_Scoping_Present()
    {
        typeof(NextORM.Core.Tests.AssertionStyleTests).Should().NotBeNull();
        typeof(NextORM.Core.Tests.IntegrationSuiteScopingTests).Should().NotBeNull();
    }

}

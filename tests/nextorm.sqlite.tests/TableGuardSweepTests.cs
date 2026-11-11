using System;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite.Tests;

namespace NextORM.Sqlite.Tests;

public class TableGuardSweepTests
{
    [SqlTable("t")]
    public sealed class E { public int Id { get; set; } }

    private static SqliteDataContext Ctx() => SqliteTestContext.CreateSqlite();

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
    public void DataContext_From_Concrete_Guards(string? table)
    {
        using var ctx = Ctx();
        DataContext dc = ctx;
        ExpectGuard(() => dc.From(table!), table);
    }

    [Fact]
    public void DataContext_From_Concrete_ValidName_ReturnsBuilder()
    {
        using var ctx = Ctx();
        DataContext dc = ctx;
        dc.From("users").Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EntityBuilder_NonGeneric_StringJoins_Guard(string? table)
    {
        using var ctx = Ctx();
        DataContext dc = ctx;
        EntityBuilder b = dc.From("a");
        ExpectGuard(() => b.Join(table!, (t1, t2) => true), table);
        ExpectGuard(() => b.LeftJoin(table!, (t1, t2) => true), table);
        ExpectGuard(() => b.RightJoin(table!, (t1, t2) => true), table);
        ExpectGuard(() => b.FullJoin(table!, (t1, t2) => true), table);
        ExpectGuard(() => b.CrossJoin(table!), table);
        ExpectGuard(() => b.CrossApply(table!), table);
        ExpectGuard(() => b.OuterApply(table!), table);
    }

    [Fact]
    public void EntityBuilder_NonGeneric_StringJoins_ValidName_ReturnsBuilder()
    {
        using var ctx = Ctx();
        DataContext dc = ctx;
        EntityBuilder b = dc.From("a");
        b.Join("users", (t1, t2) => true).Should().NotBeNull();
        b.LeftJoin("users", (t1, t2) => true).Should().NotBeNull();
        b.RightJoin("users", (t1, t2) => true).Should().NotBeNull();
        b.FullJoin("users", (t1, t2) => true).Should().NotBeNull();
        b.CrossJoin("users").Should().NotBeNull();
        b.CrossApply("users").Should().NotBeNull();
        b.OuterApply("users").Should().NotBeNull();
    }
}

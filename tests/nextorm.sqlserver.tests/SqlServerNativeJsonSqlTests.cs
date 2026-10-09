using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D177.2 SQL-lowering for the native JSON fast-path: the native clone must render the trailing
/// <c>FOR JSON PATH</c> clause and must alias every projected column to its exact JSON property name
/// (case-sensitively), so a mapped physical column such as <c>id</c> still surfaces as <c>Id</c>.
/// These tests never open a connection.
/// </summary>
public class SqlServerNativeJsonSqlTests
{
    internal static DbPreparedQueryCommand<T> PrepareNative<T>(IDataContext ctx, QueryCommand<T> command, bool includeNullValues = false)
        => PrepareNative(ctx, command, out _, includeNullValues);

    private static DbPreparedQueryCommand<T> PrepareNative<T>(IDataContext ctx, QueryCommand<T> command, out QueryCommand<T> clone, bool includeNullValues = false)
    {
        clone = (QueryCommand<T>)command.Clone();
        clone.DocumentMode = true;
        clone.ResetPreparation();
        clone.JsonShapeMode = true;
        clone.JsonShape = null;
        clone.ForJsonClause = new ForJsonClause(ForJsonMode.Path, null, IncludeNullValues: includeNullValues);
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(clone, createEnumerator: false, storeInCache: false, CancellationToken.None);
    }

    [Fact]
    public void NativeClone_ShouldRenderForJsonPathWithExactAliases()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Int, x.String })).DbCommand.CommandText;

        sql.Should().EndWith("for json path");
        // Exact, case-sensitive aliases: the physical id/column names differ from the JSON property
        // names, so FOR JSON would otherwise emit "id"/"nullableint" instead of "Id"/"Int".
        sql.Should().Contain("id as [Id]");
        sql.Should().Contain("nullableint as [Int]");
        sql.Should().Contain("somestring as [String]");
    }

    [Fact]
    public void NativeClone_ShouldRenderIncludeNullValuesWhenRequested()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id).Select(x => new { x.Id }), includeNullValues: true)
            .DbCommand.CommandText;

        sql.Should().EndWith("for json path, include_null_values");
    }

    [Fact]
    public void EligibleFlatPlan_ShouldBeNativeAndScalarOrTypedPlanManaged()
    {
        using var ctx = SqlServerTestContext.Create();

        PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Int, x.String }), out var flatCommand);
        var flatPlan = JsonShapePlan.Build(flatCommand.SelectList, flatCommand.OneColumn, new JsonStreamOptions(), shape: null);
        JsonNativeStream.IsEligible(flatPlan, flatPlan.Options).Should().BeTrue();

        PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id).Select(x => x.Id), out var scalarCommand);
        var scalarPlan = JsonShapePlan.Build(scalarCommand.SelectList, scalarCommand.OneColumn, new JsonStreamOptions(), shape: null);
        JsonNativeStream.IsEligible(scalarPlan, scalarPlan.Options).Should().BeFalse();
    }

    [Fact]
    public void RawAccessorProjection_ShouldStayManaged()
    {
        // A raw named-table accessor has no mapped provider type: the managed reader inspects the runtime
        // field type and narrows, while FOR JSON would emit the raw value. It must not be native.
        using var ctx = SqlServerTestContext.Create();

        PrepareNative(ctx, ctx.From("complex_entity").Select(x => new { Narrow = x["m"].AsInt }), out var rawCommand);
        var rawPlan = JsonShapePlan.Build(rawCommand.SelectList, rawCommand.OneColumn, new JsonStreamOptions(), shape: null);
        JsonNativeStream.IsEligible(rawPlan, rawPlan.Options).Should().BeFalse();
    }

    [Fact]
    public void DirectMemberProjection_ShouldStayNative()
    {
        using var ctx = SqlServerTestContext.Create();

        PrepareNative(ctx, ctx.From<IComplexEntity>().Select(x => new { x.Id }), out var directCommand);
        var directPlan = JsonShapePlan.Build(directCommand.SelectList, directCommand.OneColumn, new JsonStreamOptions(), shape: null);
        JsonNativeStream.IsEligible(directPlan, directPlan.Options).Should().BeTrue();
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
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

    // A1: a supported non-TableAlias/TableColumn method-call projection (string.ToUpper) is a direct
    // pass-through per JsonShapePlan.IsDirectProjection and an admitted string, so it must be selected
    // natively and the native clone must render the trailing FOR JSON PATH (not the managed row path).
    [Fact]
    public void SupportedMethodCallProjection_ShouldBeNativeAndRenderForJson()
    {
        using var ctx = SqlServerTestContext.Create();

        var prepared = PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { V = x.String!.ToUpper() }), out var methodCallCommand);

        var plan = JsonShapePlan.Build(methodCallCommand.SelectList, methodCallCommand.OneColumn, new JsonStreamOptions(), shape: null);
        plan.Columns[0].IsDirectPassThrough.Should().BeTrue(
            "a method call whose declaring type is not TableAlias/TableColumn is a direct pass-through");
        JsonNativeStream.IsEligible(plan, plan.Options).Should().BeTrue(
            "the admitted string method-call projection must be selected natively");

        var sql = prepared.DbCommand.CommandText;
        sql.Should().Contain("upper(somestring) as [V]");
        sql.Should().EndWith("for json path");
    }

    // T208 (PDCA=T208): a native-eligible object projection containing a projected parameter must alias
    // the select item with the exact projected-member name; otherwise FOR JSON PATH (which takes the
    // property name from the SQL alias) emits a document without that property. Pre-fix the item is a
    // bare "@norm_p0" (no alias), so this assertion is RED.
    [Fact]
    public void ProjectedParameter_ShouldAliasWithExactPropertyName()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Param = SqlFunctions.Parameter<int>(0) })).DbCommand.CommandText;

        // The positive assertion is the negative control: a bare "@norm_p0" select item fails it.
        sql.Should().Contain("@norm_p0 as [Param]");
        sql.Should().EndWith("for json path");
    }

    // T208 (PDCA=T208) row 7: a projected reference-type Parameter<string> must carry the exact alias
    // just like the int form. The alias is independent of the bound value, so the same SQL-generation
    // assertion covers a nonempty and a null value (the live SQL Server test binds both).
    [Fact]
    public void ProjectedStringParameter_ShouldAliasWithExactPropertyName()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Param = SqlFunctions.Parameter<string>(0) })).DbCommand.CommandText;

        sql.Should().Contain("@norm_p0 as [Param]");
        sql.Should().EndWith("for json path");
    }

    // T208 (PDCA=T208) row 6: the pre-fix red state is a bare unaliased "@norm_pN" select item. The
    // positive assertion above is the live negative control; this SQL-generation assertion pins it too,
    // without fabricating the unnamed-column SqlException that the fix makes unreproducible.
    [Fact]
    public void ProjectedParameter_BareSelectItemWithoutAlias_ShouldBeAbsent()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = PrepareNative(ctx, ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Param = SqlFunctions.Parameter<int>(0) })).DbCommand.CommandText;

        sql.Should().Contain("@norm_p0 as [Param]");
        Regex.IsMatch(sql, @"@norm_p0\s*(?:,|from\b|for\b)", RegexOptions.IgnoreCase)
            .Should().BeFalse("a bare parameter select item without its exact alias is the pre-fix red state");
    }

    // T208 (PDCA=T208) row 5: the alias signal is not native-specific -- a projected parameter now also
    // carries its exact member alias in an ordinary (non-FOR-JSON) SELECT. That is the accepted design
    // consequence of reusing the existing alias contract (outer-query re-resolution and the aggregate
    // translators already alias this way); the SQL stays valid and the parameter stays un-aliased in WHERE.
    [Fact]
    public void ProjectedParameter_OrdinarySelect_ShouldAliasAndRemainValid()
    {
        using var ctx = SqlServerTestContext.Create();

        var command = ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Param = SqlFunctions.Parameter<int>(0) });

        var sql = PrepareOrdinary(ctx, command);

        sql.Should().Contain("@norm_p0 as [Param]");
        sql.Should().NotContain("for json");
    }

    private static string PrepareOrdinary<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, CancellationToken.None)).DbCommand.CommandText;

    // T208 (PDCA=T208): the alias flag is projection-scoped (a fresh visitor per select item). A parameter
    // used only inside WHERE must stay unaliased: "@norm_p1", never "... as [x]".
    [Fact]
    public void WhereParameter_ShouldNotCarryAProjectionAlias()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = PrepareNative(ctx, ctx.From<IComplexEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<long>(1))
            .Select(x => new { x.Id, Param = SqlFunctions.Parameter<int>(0) })).DbCommand.CommandText;

        sql.Should().Contain("@norm_p0 as [Param]");

        var wherePart = sql[sql.IndexOf("where", StringComparison.OrdinalIgnoreCase)..];
        wherePart.Should().Contain("@norm_p1");
        wherePart.Should().NotContain("as [", "a WHERE-only parameter must not carry a projection alias");
    }

    // R15: a command that already carries a ForJsonClause must not be rewritten to the native streaming
    // clause. PrepareJsonStream is the private selection seam WriteJson calls; invoking it directly
    // exercises the guard without a live connection. The caller's clause must be preserved exactly once
    // and the managed row writer chosen, so no second FOR JSON wrapping is attached.
    [Fact]
    public void PreExistingForJsonClause_ShouldNotAttachSecondNativeRewrite()
    {
        using var ctx = SqlServerTestContext.Create();
        var command = ctx.From<IComplexEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Int })
            .WithForJson(ForJsonMode.Path, root: "existing", includeNullValues: true);

        var (prepared, rowWriter, _, native) = PrepareJsonStream((DataContext)ctx, command, new JsonStreamOptions());

        native.Should().BeFalse("an already-attached ForJsonClause must prevent the native rewrite");
        rowWriter.Should().NotBeNull("the managed row writer is used when native is not selected");

        var sql = prepared.DbCommand.CommandText;
        CountOccurrences(sql, "for json").Should().Be(1,
            "the caller's clause must not be duplicated by a second native rewrite");
        sql.Should().EndWith("for json path, root('existing'), include_null_values");

        command.ForJsonClause.GetValueOrDefault().Root.Should().Be("existing", "the caller's command is never mutated");
        command.ForJsonClause.GetValueOrDefault().IncludeNullValues.Should().BeTrue();
    }

    // Invokes the private DataContext.PrepareJsonStream (the guard-bearing selection seam) so the test can
    // assert native-vs-managed selection and the prepared SQL without opening a connection. The named tuple
    // surfaces as ValueTuple fields Item1..Item4.
    private static (DbPreparedQueryCommand<TResult> Prepared, JsonRowWriter? RowWriter, JsonShapePlan Plan, bool Native) PrepareJsonStream<TResult>(
        DataContext ctx, QueryCommand<TResult> command, JsonStreamOptions options)
    {
        var method = typeof(DataContext)
            .GetMethod("PrepareJsonStream", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(TResult));
        var result = method.Invoke(ctx, [command, options, CancellationToken.None])!;
        var type = result.GetType();
        return (
            (DbPreparedQueryCommand<TResult>)type.GetField("Item1")!.GetValue(result)!,
            (JsonRowWriter?)type.GetField("Item2")!.GetValue(result),
            (JsonShapePlan)type.GetField("Item3")!.GetValue(result)!,
            (bool)type.GetField("Item4")!.GetValue(result)!);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

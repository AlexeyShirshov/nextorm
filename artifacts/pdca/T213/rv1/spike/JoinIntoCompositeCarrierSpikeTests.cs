using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T213 D02-P1 bounded reconnaissance spike (harness only, no runtime change): proves whether a
/// <c>Projection&lt;TJunction, long&gt;</c> link item with a windowed <c>Item2</c> flows through the
/// entity-item projection expander (<c>QueryPreparer.TryExpandEntityItem</c>) and the row materializer
/// (<c>RowMaterializerBuilder.BuildProjectionItems</c>) when the derived link command is used as a
/// <c>FromExpression</c>.
/// </summary>
[Collection("Query plan cache")]
public class JoinIntoCompositeCarrierSpikeTests
{
    public JoinIntoCompositeCarrierSpikeTests()
    {
        DataContextCache.Clear();
    }

    [Fact]
    public void ProjectionJunctionLong_AsDerivedLinkItem_ShouldExpandIntoJunctionColumnsAndOccurrence()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.From<SpikeCarrierJunction>();
        ctx.From<SpikeCarrierParent>();

        var outer = BuildPairSql(ctx);
        var prepared = (dynamic)ctx.GetPreparedQueryCommand((dynamic)outer, false, false, CancellationToken.None);
        string sql = (string)prepared.DbCommand.CommandText;

        // If the carrier flows, the outer pair command reads the junction's two parent-FK columns and
        // the row_number occurrence from the derived link item.
        sql.Should().Contain("spike_junction", "the derived junction link source must be rendered");
    }

    [Fact]
    public void DerivedLinkCommand_ProjectionJunctionLong_ShouldRenderWindowOverJunctionColumns()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.From<SpikeCarrierJunction>();

        var derived = BuildDerivedLinkCommand(ctx);
        var prepared = (dynamic)ctx.GetPreparedQueryCommand((dynamic)derived, false, false, CancellationToken.None);
        var sql = (string)prepared.DbCommand.CommandText;

        sql.Should().Contain("row_number", "the derived link item carries the windowed occurrence");
    }

    private static QueryCommand BuildDerivedLinkCommand(IDataContext ctx)
    {
        var junctionType = typeof(SpikeCarrierJunction);
        var linkType = typeof(Projection<,>).MakeGenericType(junctionType, typeof(long));

        var parameter = Expression.Parameter(junctionType, "j");
        var rowNumberMethod = typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.row_number), Type.EmptyTypes)!;
        var overMethod = typeof(WindowFunction<int>).GetMethod(
            nameof(WindowFunction<int>.Over),
            [typeof(Expression<Func<object?>>), typeof(Expression<Func<object?>>), typeof(WindowFrame)])!;

        var rowNumber = Expression.Call(Expression.Default(typeof(CommonFunctions)), rowNumberMethod);
        var occurrence = Expression.Call(
            rowNumber,
            overMethod,
            Expression.Quote(BuildKeyLambda(parameter, nameof(SpikeCarrierJunction.ParentKey1))),
            Expression.Quote(BuildKeyLambda(parameter, nameof(SpikeCarrierJunction.ChildKey))),
            Expression.Constant(null, typeof(WindowFrame)));

        var body = Expression.MemberInit(
            Expression.New(linkType),
            Expression.Bind(linkType.GetProperty("Item1")!, parameter),
            Expression.Bind(linkType.GetProperty("Item2")!, Expression.Convert(occurrence, typeof(long))));

        var selector = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(junctionType, linkType),
            body,
            parameter);

        var command = ctx.CreateCommand(linkType, new QueryDefinition
        {
            Exp = selector,
            SrcType = junctionType,
            ProjectionType = linkType,
        });
        command.From = new FromExpression("spike_junction", false, false);
        return command;
    }

    private static QueryCommand BuildPairSql(IDataContext ctx)
    {
        var derived = BuildDerivedLinkCommand(ctx);
        var linkType = derived.GetType().GetGenericArguments()[0];
        var parentType = typeof(SpikeCarrierParent);
        var outerType = typeof(Projection<,>).MakeGenericType(parentType, linkType);

        var parentParameter = Expression.Parameter(parentType, "p");
        var linkParameter = Expression.Parameter(linkType, "l");
        var condition = Expression.Lambda(Expression.Constant(true), parentParameter, linkParameter);

        var linkJoin = new JoinExpression(condition, JoinType.Left)
        {
            From = new FromExpression(derived),
        };

        var outer = ctx.CreateCommand(outerType, new QueryDefinition
        {
            SrcType = parentType,
            ProjectionType = outerType,
            Joins = [linkJoin],
        });
        outer.From = new FromExpression("spike_parent", false, false);
        return outer;
    }

    private static LambdaExpression BuildKeyLambda(ParameterExpression parameter, string property)
        => Expression.Lambda<Func<object?>>(
            Expression.Convert(Expression.Property(parameter, property), typeof(object)));
}

[SqlTable("spike_parent")]
public sealed class SpikeCarrierParent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }
}

[SqlTable("spike_junction")]
public sealed class SpikeCarrierJunction
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_key1")]
    public int ParentKey1 { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_key2")]
    public int ParentKey2 { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("child_key")]
    public int ChildKey { get; set; }
}

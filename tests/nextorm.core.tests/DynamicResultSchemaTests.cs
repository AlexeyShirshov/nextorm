using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public class DynamicResultSchemaTests
{
    public interface ILeadingRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("a")]
        int A { get; set; }
    }

    public interface IAliasRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("a")]
        int A { get; set; }
    }

    private static class LeadingTvf
    {
        [SqlTableFunction("values", ResultSchema = TableFunctionSchema.LeadingArgument)]
        public static IQueryable<ILeadingRow> Values(string tuples) => throw new NotSupportedException();
    }

    private static class AliasTvf
    {
        [SqlTableFunction("jsonb_to_record", ResultSchema = TableFunctionSchema.AliasColumnList)]
        public static IQueryable<IAliasRow> Record(object? json) => throw new NotSupportedException();
    }

    [Fact]
    public void ResultSchema_ShouldDefaultToNone()
    {
        var attribute = new SqlTableFunctionAttribute("tvf");

        attribute.ResultSchema.Should().Be(TableFunctionSchema.None);
    }

    [Fact]
    public void ResultSchema_ShouldBeSettable()
    {
        var attribute = new SqlTableFunctionAttribute("values")
        {
            ResultSchema = TableFunctionSchema.LeadingArgument
        };

        attribute.ResultSchema.Should().Be(TableFunctionSchema.LeadingArgument);
    }

    [Fact]
    public void Create_WithLeadingSchema_ShouldResolveRowTypeAndPlacement()
    {
        var call = (MethodCallExpression)Expression.Call(
            typeof(LeadingTvf).GetMethod(nameof(LeadingTvf.Values))!,
            Expression.Constant("(1)"));

        var function = TableFunctionExpression.Create(call);

        function.ResultType.Should().Be(typeof(ILeadingRow));
        function.ResultSchema.Should().Be(TableFunctionSchema.LeadingArgument);
    }

    [Fact]
    public void Create_WithAliasSchema_ShouldResolveRowTypeAndPlacement()
    {
        var call = (MethodCallExpression)Expression.Call(
            typeof(AliasTvf).GetMethod(nameof(AliasTvf.Record))!,
            Expression.Constant(null, typeof(object)));

        var function = TableFunctionExpression.Create(call);

        function.ResultType.Should().Be(typeof(IAliasRow));
        function.ResultSchema.Should().Be(TableFunctionSchema.AliasColumnList);
    }

    [Fact]
    public void Create_WithoutSchema_ShouldLeaveRowTypeUnset()
    {
        var method = typeof(LeadingTvf).GetMethod(nameof(LeadingTvf.Values))!;
        var call = (MethodCallExpression)Expression.Call(method, Expression.Constant("(1)"));

        var function = new TableFunctionExpression("plain", null, call);

        function.ResultType.Should().BeNull();
        function.ResultSchema.Should().Be(TableFunctionSchema.None);
    }
}

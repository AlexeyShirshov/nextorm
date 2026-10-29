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
        public static TvfEntityBuilder<ILeadingRow> Values(string tuples) => new("values", null, null, null, Array.Empty<int>(), TableFunctionSchema.LeadingArgument);
    }

    private static class AliasTvf
    {
        public static TvfEntityBuilder<IAliasRow> Record(object? json) => new("jsonb_to_record", null, null, null, Array.Empty<int>(), TableFunctionSchema.AliasColumnList);
    }

    [Fact]
    public void ResultSchema_ShouldDefaultToNone()
    {
        var descriptor = new TvfEntityBuilder<ILeadingRow>("tvf");

        descriptor.ResultSchema.Should().Be(TableFunctionSchema.None);
    }

    [Fact]
    public void Descriptor_ShouldCarryTheResultSchema()
    {
        var descriptor = new TvfEntityBuilder<ILeadingRow>(
            "values", null, null, null, Array.Empty<int>(), TableFunctionSchema.LeadingArgument);

        descriptor.ResultSchema.Should().Be(TableFunctionSchema.LeadingArgument);
    }

    [Fact]
    public void CreateRuntime_WithLeadingSchema_ShouldCarryRowTypeAndPlacement()
    {
        var call = (MethodCallExpression)Expression.Call(
            typeof(LeadingTvf).GetMethod(nameof(LeadingTvf.Values))!,
            Expression.Constant("(1)"));
        var descriptor = LeadingTvf.Values("(1)");

        var function = TableFunctionExpression.CreateRuntime(
            descriptor.Name,
            descriptor.Schema,
            descriptor.WithClause,
            descriptor.CallClause,
            null,
            call,
            typeof(ILeadingRow),
            descriptor.ResultSchema);

        function.ResultType.Should().Be(typeof(ILeadingRow));
        function.ResultSchema.Should().Be(TableFunctionSchema.LeadingArgument);
    }

    [Fact]
    public void CreateRuntime_WithAliasSchema_ShouldCarryRowTypeAndPlacement()
    {
        var call = (MethodCallExpression)Expression.Call(
            typeof(AliasTvf).GetMethod(nameof(AliasTvf.Record))!,
            Expression.Constant(null, typeof(object)));
        var descriptor = AliasTvf.Record(null);

        var function = TableFunctionExpression.CreateRuntime(
            descriptor.Name,
            descriptor.Schema,
            descriptor.WithClause,
            descriptor.CallClause,
            null,
            call,
            typeof(IAliasRow),
            descriptor.ResultSchema);

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

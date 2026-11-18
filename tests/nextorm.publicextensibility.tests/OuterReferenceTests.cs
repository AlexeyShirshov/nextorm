using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using Xunit;

namespace NextORM.PublicExtensibility.Tests;

public sealed class OuterReferenceTests
{
    [Fact]
    public void Create_ShouldRegisterOuterExpressionsAndPreserveTheirTypes()
    {
        using var context = new PostgresDataContext(
            "Host=localhost;Database=nextorm;Username=nextorm", new DataContextBuilder());
        IQueryRegistry registry = context.From<Row>().Select(row => row.Id);
        Expression<Func<Row, int>> outerId = row => row.Id;
        Expression<Func<Row, int?>> outerParentId = row => row.ParentId;

        var id = OuterReference.Create(registry, outerId.Body);
        var parentId = OuterReference.Create(registry, outerParentId.Body);

        id.Type.Should().Be(typeof(int));
        parentId.Type.Should().Be(typeof(int?));
        registry.OuterReferences.Should().Equal(outerId.Body, outerParentId.Body);
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
    }
}

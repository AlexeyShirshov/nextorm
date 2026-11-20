using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Core.Tests;

public sealed class InterfaceEntityTests
{
    [Fact]
    public void HelperTypeArgumentRegistersInheritedPropertiesAndWritableStorage()
    {
        // The helper has no reference to a NextORM API. Its generic argument alone roots the model.
        var entityType = Model<IHelperEntity>();
        var shape = GeneratedInterfaceEntity.Find(entityType);
        shape.Should().NotBeNull();

        var metadata = new EntityMetadataBuilder<IHelperEntity>().AutoBuild();
        metadata.Properties.Select(p => (p.ColumnName, p.IsKey))
            .Should().Contain(("entity_id", true)).And.Contain(("name", false));

        var values = metadata.Properties.Select((p, i) => new SelectExpression(p.PropertyInfo.PropertyType)
        {
            Index = i,
            PropertyName = p.PropertyInfo.Name,
            PropertyInfo = p.PropertyInfo,
        }).ToArray();
        var input = Expression.Parameter(typeof(int));
        var expression = RowMaterializerBuilder.Build(entityType, input, values, ignoreColumns: false, column =>
            column.PropertyName == "Id" ? input : Expression.Constant(column.PropertyName == "Name" ? "initial" : "init"));
        var mapper = Expression.Lambda<Func<int, IHelperEntity>>(expression, input).Compile();

        var first = mapper(7);
        var second = mapper(9);
        first.Id.Should().Be(7);
        first.Name.Should().Be("initial");
        first.Tag.Should().Be("init");
        first.Name = "changed";
        second.Id.Should().Be(9);
        second.Name.Should().Be("initial");
    }

    [Fact]
    public void InMemoryIdentityAndCompositeKeepInterfaceRows()
    {
        using var context = new InMemoryDataContext();
        context.From<IMemoryEntity>().WithData([new MemoryRow { Id = 1 }, new MemoryRow { Id = 2 }]);
        context.From<IMemoryOther>().WithData([new OtherRow { Id = 1 }]);

        context.From<IMemoryEntity>().OrderBy(x => x.Id).Select(x => x).ToList()
            .Select(x => x.Id).Should().Equal(1, 2);

        var rows = context.From<IMemoryEntity>().LeftJoin(context.From<IMemoryOther>(), (a, b) => a.Id == b.Id)
            .OrderBy(x => x.Item1.Id).ToList();
        rows.Select(x => x.Item1.Id).Should().Equal(1, 2);
        rows[0].Item2.Id.Should().Be(1);
        rows[1].Item2.Should().BeNull();
    }

    [Fact]
    public void ComposedGenericArgumentFindsBothInterfacesWithoutAnOrmCall()
    {
        var arguments = Model<Projection<ICompositeFirst, ICompositeSecond>>().GenericTypeArguments;
        arguments.Select(GeneratedInterfaceEntity.Find).Should().OnlyContain(shape => shape != null);
    }

    private static Type Model<T>() => typeof(T);
}

public interface IHelperKey
{
    [Key, Column("entity_id")]
    int Id { get; }
}

internal interface IHelperEntity : IHelperKey
{
    [Column("name")]
    string Name { get; set; }

    string Tag { get; init; }
}

internal interface ICompositeFirst
{
    int Key { get; }
}

internal interface ICompositeSecond
{
    string Label { get; }
}

internal interface IMemoryEntity
{
    int Id { get; }
}

internal interface IMemoryOther
{
    int Id { get; }
}

internal sealed class MemoryRow : IMemoryEntity
{
    public int Id { get; init; }
}

internal sealed class OtherRow : IMemoryOther
{
    public int Id { get; init; }
}

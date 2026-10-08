using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Unit coverage for the streaming discriminator in the compiled row-mapper cache key and for the
/// buffered-mapper guard on a streaming LOB projection. The streaming terminal path does not compile
/// a mapper (it prepares with <c>sequentialAccess: true</c>), so these tests exercise the key and the
/// guard directly.
/// </summary>
public class RowMapperFactoryStreamingKeyTests
{
    [Fact]
    public void HasStreamingColumns_ShouldBeFalseForBufferedAndNull()
    {
        var buffered = new SelectExpression(typeof(byte[])) { Index = 0 };

        RowMapperFactory.HasStreamingColumns([buffered]).Should().BeFalse();
        RowMapperFactory.HasStreamingColumns(null).Should().BeFalse();
    }

    [Fact]
    public void HasStreamingColumns_ShouldBeTrueForStreamingColumn()
    {
        var streaming = new SelectExpression(typeof(Stream)) { Index = 0, IsLobStreaming = true };

        RowMapperFactory.HasStreamingColumns([streaming]).Should().BeTrue();
    }

    [Fact]
    public void MapperCacheKey_StreamingFlag_ShouldSeparateCacheEntries()
    {
        // Same provider/result/SQL/signature/one-column shape, only the streaming flag differs.
        var bufferedKey = new MapperCacheKey(typeof(object), typeof(byte[]), "select data from t", 42, true, Streaming: false);
        var streamingKey = new MapperCacheKey(typeof(object), typeof(byte[]), "select data from t", 42, true, Streaming: true);

        bufferedKey.Should().NotBe(streamingKey);

        MapperCache.Clear();
        try
        {
            MapperCache.Add(bufferedKey, (Func<IDataRecord, byte[]>)(_ => []));

            MapperCache.TryGet(bufferedKey, out _).Should().BeTrue();
            MapperCache.TryGet(streamingKey, out _).Should().BeFalse();
        }
        finally
        {
            MapperCache.Clear();
        }
    }

    [Fact]
    public void BuildSignature_ShouldDistinguishProjectionItemMembers()
    {
        // #173: the compiled-mapper key hashes the projection-item member, so two commands identical
        // except for their target member must not alias the same compiled mapper.
        var id = typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!;
        var name = typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Name))!;
        var first = Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), id));
        var second = Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), name));

        BuildSignature(first).Should().NotBe(BuildSignature(second));
        BuildSignature(first).Should().Be(BuildSignature(
            Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), id))));
    }

    [Fact]
    public void BuildSignature_ShouldNotConflateSameMemberNameOnDifferentDeclaringTypes()
    {
        // #173 variant matrix: member identity is the PropertyInfo, not the name alone. Two same-named
        // members on different declaring types must keep the mapper signature distinct even when the
        // entity type and slot are equal, so the compiled-mapper key cannot alias them.
        var first = Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity),
            typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!));
        var second = Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity),
            typeof(PlanEqualityOtherEntity).GetProperty(nameof(PlanEqualityOtherEntity.Id))!));

        BuildSignature(first).Should().NotBe(BuildSignature(second));
    }

    [Fact]
    public void BuildSignature_ShouldDistinguishNullFromNonNullProjectionItemMember()
    {
        // A null member is a supported constructor position and must hash apart from a named member.
        var id = typeof(PlanEqualityMemberEntity).GetProperty(nameof(PlanEqualityMemberEntity.Id))!;
        var named = Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), id));
        var ctorPosition = Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), null));

        BuildSignature(named).Should().NotBe(BuildSignature(ctorPosition));
        BuildSignature(ctorPosition).Should().Be(BuildSignature(
            Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), null))));
    }

    [Fact]
    public void BuildSignature_ShouldDistinguishProjectionItemEntityTypes()
    {
        // Guard: the retained entity-type discriminator (RowMapperFactory.cs:475) still separates
        // same-slot item shapes whose entity type differs.
        var first = Column(new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));
        var second = Column(new ProjectionEntityItem(0, typeof(PlanEqualityMemberEntity), null));

        BuildSignature(first).Should().NotBe(BuildSignature(second));
    }

    [Fact]
    public void BuildSignature_ShouldDistinguishProjectionItemSlots()
    {
        // Guard: the retained slot discriminator separates same-type items in different slots.
        var first = Column(new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));
        var second = Column(new ProjectionEntityItem(1, typeof(PlanEqualityEntity), null));

        BuildSignature(first).Should().NotBe(BuildSignature(second));
    }

    [Fact]
    public void BuildSignature_ShouldDistinguishScalarColumnFromProjectionItem()
    {
        // Guard: the `ProjectionItem == null` short-circuit (RowMapperFactory.cs:475,478) must keep a
        // scalar column distinct from an item column, while two scalar columns remain equal.
        var scalar = ScalarColumn(0);
        var entity = Column(new ProjectionEntityItem(0, typeof(PlanEqualityEntity), null));

        BuildSignature(scalar).Should().NotBe(BuildSignature(entity));
        BuildSignature(scalar).Should().Be(BuildSignature(ScalarColumn(0)));
    }

    [Fact]
    public void BuildSignature_ShouldApplyMemberIdentityToValueTypeEntityItems()
    {
        // #173 variant matrix: the mapper key follows the same member identity for value-type
        // entities — a different target member separates the signature, an equal member shares it.
        var id = typeof(PlanEqualityValueEntity).GetProperty(nameof(PlanEqualityValueEntity.Id))!;
        var name = typeof(PlanEqualityValueEntity).GetProperty(nameof(PlanEqualityValueEntity.Name))!;
        var first = Column(new ProjectionEntityItem(0, typeof(PlanEqualityValueEntity), id));
        var second = Column(new ProjectionEntityItem(0, typeof(PlanEqualityValueEntity), name));

        BuildSignature(first).Should().NotBe(BuildSignature(second));
        BuildSignature(first).Should().Be(BuildSignature(
            Column(new ProjectionEntityItem(0, typeof(PlanEqualityValueEntity), id))));
    }

    [Fact]
    public void BuildKey_ShouldDistinguishProjectionItemMembers()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<SimpleEntity>().Select(x => x.Id);
        command.PrepareCommand(CancellationToken.None);

        var id = typeof(SimpleEntity).GetProperty(nameof(SimpleEntity.Id))!;
        var other = typeof(MapperKeyAlternate).GetProperty(nameof(MapperKeyAlternate.Value))!;

        command.SelectList![0].ProjectionItem = new ProjectionEntityItem(0, typeof(SimpleEntity), id);
        var first = BuildKey(command, "select id from simple");
        command.SelectList![0].ProjectionItem = new ProjectionEntityItem(0, typeof(SimpleEntity), other);
        var second = BuildKey(command, "select id from simple");

        first.Should().NotBe(second, "the mapper key must include the projection-item member");
    }

    private static SelectExpression Column(ProjectionEntityItem item)
        => new(typeof(int))
        {
            Index = 0,
            PropertyName = "Id",
            ProjectionItem = item,
        };

    private static SelectExpression ScalarColumn(int index)
        => new(typeof(int))
        {
            Index = index,
            PropertyName = "Id",
        };

    private static int BuildSignature(params SelectExpression[] selectList)
    {
        var method = typeof(RowMapperFactory).GetMethod("BuildSignature", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)method.Invoke(null, [selectList])!;
    }

    private static MapperCacheKey BuildKey<TResult>(QueryCommand<TResult> command, string? sql)
    {
        var method = typeof(RowMapperFactory)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "BuildKey" && m.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(TResult));
        return (MapperCacheKey)method.Invoke(null, [command, sql, typeof(object), false])!;
    }

    private sealed class MapperKeyAlternate
    {
        public int Value { get; set; }
    }

    [Fact]
    public void MapColumn_StreamingColumn_ShouldThrowNotSupported()
    {
        var column = new SelectExpression(typeof(Stream)) { Index = 0, PropertyName = "Data", IsLobStreaming = true };
        var param = Expression.Parameter(typeof(IDataRecord));

        var act = () => RowMapperFactory.MapColumn(column, param);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*streaming LOB column*ToAsyncEnumerable*");
    }

    [Fact]
    public void MapStreamingColumn_ShouldReadTheSequentialAccessAccessor()
    {
        var column = new SelectExpression(typeof(Stream)) { Index = 2, PropertyName = "Data", IsLobStreaming = true };
        var param = Expression.Parameter(typeof(IDataRecord));

        var body = RowMapperFactory.MapStreamingColumn(column, param);

        // The streaming accessor is the reader's GetStream(ordinal), not the buffered MapColumn.
        body.ToString().Should().Contain("GetStream");
        body.Type.Should().Be(typeof(Stream));
    }

    [Fact]
    public void MapStreamingColumn_TextReader_ShouldReadTheSequentialAccessAccessor()
    {
        var column = new SelectExpression(typeof(TextReader)) { Index = 1, PropertyName = "Body", IsLobStreaming = true };
        var param = Expression.Parameter(typeof(IDataRecord));

        var body = RowMapperFactory.MapStreamingColumn(column, param);

        body.ToString().Should().Contain("GetTextReader");
        body.Type.Should().Be(typeof(TextReader));
    }
}

using System.Data;
using System.Linq.Expressions;
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

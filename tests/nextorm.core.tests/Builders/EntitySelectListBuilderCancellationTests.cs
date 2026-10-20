using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T207 red regressions for <see cref="EntitySelectListBuilder.Build"/>: a cancelled build must surface
/// <see cref="OperationCanceledException"/> instead of silently returning a truncated column array, and
/// an active or non-cancellable token must still return the complete ordered projection. The pre-cancelled
/// case is the RED arm against the unmodified build; the other two are controls that already pass.
/// </summary>
public class EntitySelectListBuilderCancellationTests
{
    public EntitySelectListBuilderCancellationTests()
    {
        DataContextCache.Clear();
    }

    private static IEntityMetadata Metadata() => new EntityMetadataBuilder<T207Entity>().Build();

    [Fact]
    public void T207_Build_PreCancelled_ThrowsOperationCanceled()
    {
        var metadata = Metadata();

        Action act = () => EntitySelectListBuilder.Build(
            typeof(T207Entity), metadata, new CancellationToken(canceled: true));

        act.Should().Throw<OperationCanceledException>(
            "a cancelled build must surface cancellation, not an empty or partial column array");
    }

    [Fact]
    public void T207_Build_NotCancelled_ReturnsCompleteColumns()
    {
        var metadata = Metadata();

        using var cts = new CancellationTokenSource();

        var (columns, completed) = EntitySelectListBuilder.Build(typeof(T207Entity), metadata, cts.Token);

        completed.Should().BeTrue();
        AssertComplete(metadata, columns);
    }

    [Fact]
    public void T207_Build_NonCancellable_ReturnsCompleteColumns()
    {
        var metadata = Metadata();

        var (columns, completed) = EntitySelectListBuilder.Build(typeof(T207Entity), metadata, CancellationToken.None);

        completed.Should().BeTrue();
        AssertComplete(metadata, columns);
    }

    private static void AssertComplete(IEntityMetadata metadata, SelectExpression[] columns)
    {
        columns.Should().HaveCount(metadata.Properties.Count);
        columns.Select(c => c.PropertyName).Should().Equal(metadata.Properties.Select(p => p.PropertyInfo.Name));
        columns.Select(c => c.PropertyInfo!.Name).Should().Equal(metadata.Properties.Select(p => p.PropertyInfo.Name));
        columns.Select(c => c.Index).Should().Equal(Enumerable.Range(0, columns.Length));
    }
}

/// <summary>An entity with nonempty mapped properties for the direct-builder T207 tests.</summary>
public sealed class T207Entity
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int Age { get; set; }
}

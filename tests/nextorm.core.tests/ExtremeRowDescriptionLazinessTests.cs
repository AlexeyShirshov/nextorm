using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Contract of the factory-backed <see cref="ExtremeRowDescription.Payload"/> seam added by #155: the
/// factory is never invoked before the payload is read, is memoized across repeated reads (invoked at
/// most once, including under concurrent readers), and the record's value semantics compare the exposed
/// shape rather than the memoization field. The internal factory constructor is exercised directly via
/// <c>InternalsVisibleTo</c>; no production counter is involved.
/// </summary>
public class ExtremeRowDescriptionLazinessTests
{
    private static ExtremeRowRenderColumn Column()
        => new(typeof(int), isNullable: false, isDirectMappedColumn: true, usesConverter: false);

    private static ExtremeRowRenderColumn[] Payload() => [Column()];

    [Fact]
    public void FactoryPayload_ShouldNotMaterializeUntilRead()
    {
        var calls = 0;
        var description = new ExtremeRowDescription(
            isMax: true,
            keys: [Column()],
            groups: [],
            payloadFactory: () =>
            {
                calls++;
                return Payload();
            });

        calls.Should().Be(0, "constructing the description must not build the payload");

        var first = description.Payload;

        calls.Should().Be(1);
        first.Should().ContainSingle();

        var second = description.Payload;

        calls.Should().Be(1, "repeated reads must hit the memoized payload");
        ReferenceEquals(first, second).Should().BeTrue("the same instance must be returned");
    }

    [Fact]
    public void EagerPayloadConstructor_ShouldBehaveAsAFactoryBackedDescription()
    {
        var calls = 0;
        var payload = Payload();
        var description = new ExtremeRowDescription(
            isMax: false,
            keys: [Column()],
            groups: [Column()],
            payloadFactory: () =>
            {
                calls++;
                return payload;
            });

        description.Payload.Should().BeSameAs(payload);
        description.Payload.Should().BeSameAs(payload);
        calls.Should().Be(1);
    }

    [Fact]
    public void RepeatedConcurrentReads_ShouldInvokeTheFactoryExactlyOnce()
    {
        var calls = 0;
        var description = new ExtremeRowDescription(
            isMax: true,
            keys: [Column()],
            groups: [],
            payloadFactory: () =>
            {
                Interlocked.Increment(ref calls);
                return Payload();
            });

        var results = new IReadOnlyList<ExtremeRowRenderColumn>[64];
        Parallel.For(0, results.Length, i => results[i] = description.Payload);

        calls.Should().Be(1, "Lazy with ExecutionAndPublication must publish one value to all readers");
        results.Should().OnlyContain(r => ReferenceEquals(r, results[0]));
    }

    [Fact]
    public void EagerAndFactoryBacked_WithSameExposedValues_ShouldBeEqualAndHashAlike()
    {
        var keys = new[] { Column() };
        var groups = Array.Empty<ExtremeRowRenderColumn>();
        var payload = Payload();

        var eager = new ExtremeRowDescription(true, keys, groups, payload);
        var factory = new ExtremeRowDescription(true, keys, groups, () => payload);

        factory.Should().Be(eager, "the memoization field must not participate in equality");
        factory.GetHashCode().Should().Be(eager.GetHashCode());
    }

    [Fact]
    public void WithExpression_ShouldCarryTheSameLazyPayloadWithoutReinvokingTheFactory()
    {
        var calls = 0;
        var description = new ExtremeRowDescription(
            isMax: true,
            keys: [Column()],
            groups: [],
            payloadFactory: () =>
            {
                calls++;
                return Payload();
            });

        var copy = description with { };

        calls.Should().Be(0, "copying a record must not materialize the payload");
        copy.Payload.Should().BeSameAs(description.Payload);
        calls.Should().Be(1);
        copy.IsMax.Should().BeTrue("the copy carries the exposed values");
    }
}

using FluentAssertions;

namespace nextorm.core.tests;

public class CommandBuilderTests
{
    private readonly Entity<ISimpleEntity> _builder;

    public CommandBuilderTests(Entity<ISimpleEntity> builder)
    {
        _builder = builder;
    }
    [Fact]
    public void Select_ShouldReturnCommand()
    {
        var r = _builder.Select(entity => new { entity.Id });

        r.Should().NotBeNull();
    }
    // [Fact]
    // public void GetHashCode_ShouldBeEquals()
    // {
    //     var r = _builder.Select(entity => new { entity.Id });
    //     r.PrepareCommand(CancellationToken.None);

    //     r.Should().NotBeNull();

    //     var r2 = _builder.Select(entity => new { entity.Id });
    //     r2.PrepareCommand(CancellationToken.None);

    //     r2.Should().NotBeNull();

    //     r.Should().Be(r2);

    //     r.Should().BeEquivalentTo(r2);

    //     r.GetHashCode().Should().Be(r2.GetHashCode());

    //     var r3 = _builder.Select(entity => new { i = entity.Id + 1 });
    //     r3.PrepareCommand(CancellationToken.None);

    //     r.Should().NotBe(r3);

    //     r.GetHashCode().Should().NotBe(r3.GetHashCode());
    // }
}
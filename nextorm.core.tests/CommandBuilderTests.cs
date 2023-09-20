using FluentAssertions;

namespace nextorm.core.tests;

public class CommandBuilderTests
{
    private readonly CommandBuilder<ISimpleEntity> _builder;

    public CommandBuilderTests(CommandBuilder<ISimpleEntity> builder)
    {
        _builder = builder;
    }
    [Fact]
    public void Select_ShouldReturnCommand()
    {
        var r = _builder.Select(entity=>new {entity.Id});

        r.Should().NotBeNull();
    }
}
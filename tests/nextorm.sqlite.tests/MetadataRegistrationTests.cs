using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// From/table resolution must fail with an actionable error when the entity metadata was never
/// registered, instead of a NotImplementedException thrown by a placeholder hook.
/// </summary>
public class MetadataRegistrationTests
{
    private sealed class UnregisteredEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public void GetFrom_ForUnregisteredType_ShouldThrowBuildSqlCommandException()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.GetFrom(typeof(UnregisteredEntity), null);

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage($"*{nameof(UnregisteredEntity)}*");
    }
}

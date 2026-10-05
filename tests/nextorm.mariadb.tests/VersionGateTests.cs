using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// Version-gated <c>UPDATE ... RETURNING</c> on MariaDB (13.0+). <c>INSERT</c>/<c>DELETE</c>
/// <c>RETURNING</c> stays unsupported at every version, and — unlike PostgreSQL — the same concrete
/// context type may be used with several versions (no type guard).
/// </summary>
public class VersionGateTests
{
    [Fact]
    public void UpdateReturning_Unset_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        Action act = () => ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UpdateReturning_V12_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create(new Version(12, 1));

        Action act = () => ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UpdateReturning_V13_ShouldEmitReturning()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0));

        ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("update merge_entity set name = @p0 where id = 1 returning id, name, age, total");
    }

    [Fact]
    public void UpdateReturning_V13Patch_ShouldEmitReturning()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0, 2));

        ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Contain(" returning id, name, age, total");
    }

    [Fact]
    public void UpdateReturning_Later_ShouldEmitReturning()
    {
        using var ctx = MariaDbTestContext.Create(new Version(14, 0));

        ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Contain(" returning ");
    }

    [Fact]
    public void InsertReturning_V13_ShouldStillThrow()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0));

        var builder = ctx.CreateInsertBuilder<IMergeEntity>()
            .Value(x => x.Name, "a")
            .Returning();

        Action act = () => builder.ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DeleteReturning_V13_ShouldStillThrow()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0));

        Action act = () => ctx.CreateDeleteBuilder<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NoPostgresStyleTypeGuard_Applies()
    {
        // The same concrete MariaDbDataContext type may be bound to two different versions; the
        // PostgreSQL one-version-per-context-type guard must not apply here.
        using (var old = MariaDbTestContext.CreateMariaDb(new Version(12, 1)))
            old.Dialect.Should().NotBeNull();

        using (var newer = MariaDbTestContext.CreateMariaDb(new Version(13, 0)))
            newer.Dialect.Should().NotBeNull();
    }

    [Fact]
    public void ServerVersionSnapshot_ShouldAgreeWithTheDialect()
    {
        // The versioned constructor must forward the configured version to the base snapshot, not only
        // to the dialect: otherwise DataContext.ServerVersion would stay null while the dialect is
        // version-gated (the protected snapshot would disagree with the dialect).
        using (var versioned = new ServerVersionExposingContext(new Version(13, 0)))
        {
            versioned.ExposedServerVersion.Should().Be(new Version(13, 0));
            versioned.Dialect.SupportsUpdateReturning.Should().BeTrue();
        }

        using (var unset = new ServerVersionExposingContext(null))
        {
            unset.ExposedServerVersion.Should().BeNull();
            unset.Dialect.SupportsUpdateReturning.Should().BeFalse();
        }
    }

    private sealed class ServerVersionExposingContext : MariaDbDataContext
    {
        public ServerVersionExposingContext(Version? serverVersion)
            : base("Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm", new DataContextBuilder(), serverVersion)
        {
        }

        public Version? ExposedServerVersion => ServerVersion;
    }
}

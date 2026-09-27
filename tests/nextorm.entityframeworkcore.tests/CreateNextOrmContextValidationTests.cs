using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <c>CreateNextOrmContext</c> validates its input and the EF provider *before* it touches the
/// connection: a null context fails with <see cref="ArgumentNullException"/>, and a non-relational
/// (InMemory) or look-alike provider name fails with the documented unsupported-provider
/// <see cref="InvalidOperationException"/> rather than EF's relational error. Provider names match
/// exactly, so a name that merely contains "Npgsql" is rejected.
/// </summary>
public sealed class CreateNextOrmContextValidationTests : EfCoreMetadataCleanup
{
    [Fact]
    public void CreateNextOrmContext_ShouldThrow_WhenDbContextIsNull()
    {
        var act = () => ((DbContext)null!).CreateNextOrmContext();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateNextOrmContext_ShouldThrowUnsupportedProvider_ForEfInMemory()
    {
        using var db = new InMemoryContext();

        var act = () => db.CreateNextOrmContext();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Microsoft.EntityFrameworkCore.InMemory*")
            .WithMessage("*not supported*");
    }

    [Fact]
    public void CreateNextOrmContext_ShouldThrow_WhenProviderNameIsOnlyALookAlike()
    {
        using var db = new LookAlikeProviderContext();

        var act = () => db.CreateNextOrmContext();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Contoso.EntityFrameworkCore.NpgsqlAdapter*")
            .WithMessage("*not supported*");
    }

    private sealed class InMemoryContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInMemoryDatabase("nextorm-validation");
    }

    private sealed class LookAlikeProviderContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseInMemoryDatabase("nextorm-lookalike")
                .ReplaceService<IDatabaseProvider, LookAlikeDatabaseProvider>();
    }

    private sealed class LookAlikeDatabaseProvider : IDatabaseProvider
    {
        public string Name => "Contoso.EntityFrameworkCore.NpgsqlAdapter";

        public string Version => "1.0.0";

        public bool IsConfigured(IDbContextOptions options) => true;
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <see cref="NextOrmModelMapper.Register"/> projects the EF Core model into nextorm's process-wide
/// metadata cache, so nextorm SQL addresses the table and columns declared by EF (not the CLR names).
/// SQL generation needs no database.
/// </summary>
public sealed class NextOrmModelMapperTests : EfCoreMetadataCleanup
{
    [Fact]
    public void Register_ShouldMapCustomTableAndColumnNames()
    {
        using var db = new MappedContext();

        NextOrmModelMapper.Register(db.Model);

        using var ctx = db.CreateNextOrmContext();
        var sql = NextOrmSql.Of(ctx, ctx.From<MappedRow>().Select(x => new { x.Id, x.Name }));

        sql.Should().Be("select selected_id as 'Id', selected_name as 'Name' from mapped_table");
        sql.Should().NotContain("mapped_row");
    }

    [Fact]
    public void Register_ShouldMapWhereClauseColumns()
    {
        using var db = new MappedContext();

        NextOrmModelMapper.Register(db.Model);

        using var ctx = db.CreateNextOrmContext();
        var name = "a";
        var sql = NextOrmSql.Of(ctx, ctx.From<MappedRow>().Where(x => x.Name == name).Select(x => x.Id));

        sql.Should().Contain("selected_id");
        sql.Should().Contain("selected_name = $name");
    }

    [Fact]
    public void Register_ShouldThrow_WhenModelIsNull()
    {
        var act = () => NextOrmModelMapper.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class MappedContext : DbContext
    {
        public DbSet<MappedRow> Rows => Set<MappedRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MappedRow>(entity =>
            {
                entity.ToTable("mapped_table");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("selected_id");
                entity.Property(x => x.Name).HasColumnName("selected_name");
            });
        }
    }

    private sealed class MappedRow
    {
        public long Id { get; set; }
        public string? Name { get; set; }
    }
}

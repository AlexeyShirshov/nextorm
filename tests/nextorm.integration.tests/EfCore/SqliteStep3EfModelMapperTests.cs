using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NextORM.Core;
using NextORM.EntityFrameworkCore;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 2 — the EF Core model mapper on live SQLite (<c>src/nextorm.entityframeworkcore/
/// NextOrmModelMapper.cs</c>, 58 uncovered arms in v4): publication and the live query it enables,
/// idempotent re-registration (<c>ComputeFingerprint</c>), the two conflict branches
/// (<c>IsSameMapping</c> / <c>IsSamePropertyMapping</c>), and the merge of an EF model's filters onto a
/// mapping published outside the bridge (<c>MergeWithExisting</c>, anonymous additive + named-key
/// collision). Two EF models are built per CLR type with <see cref="ModelBuilder"/>, so the mapper sees
/// exactly the "second model for the same type" shapes the cache promises to reject or merge.
/// </summary>
[Collection("EF query filter lifecycle")]
public sealed class SqliteStep3EfModelMapperTests
{
    /// <summary>A mapping published by <c>Register</c> is used by a live nextorm query on the same file.</summary>
    [Fact]
    public void Register_ShouldPublishMappingAndServeLiveQuery()
    {
        var path = NewPath();
        try
        {
            CreateDatabase(
                path,
                "create table s3_ef_a (id integer primary key, name text null, value int not null);",
                "insert into s3_ef_a (id, name, value) values (1, 'a', 10), (2, 'b', 20);");

            using var db = new ContextA(Options<ContextA>(path));
            NextOrmModelMapper.Register(db.Model);

            using var next = NextContext(path);
            var rows = next.From<S3EfRowA>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name, x.Value })
                .ToList();

            rows.Should().HaveCount(2);
            rows[0].Should().Be(new { Id = 1, Name = "a", Value = 10 });
            rows[1].Value.Should().Be(20);

            var act = () => NextOrmModelMapper.Register(db.Model);
            act.Should().NotThrow("repeating the identical bridge registration is idempotent");

            next.From<S3EfRowA>().Count().Should().Be(2, "the mapping is still the published one");
        }
        finally
        {
            Delete(path);
        }
    }

    /// <summary>A second model with a different column layout for the same CLR type is a conflict.</summary>
    [Fact]
    public void Register_DifferentColumnLayout_ShouldThrow()
    {
        using var first = new ContextB1(Options<ContextB1>());
        using var second = new ContextB2(Options<ContextB2>());

        NextOrmModelMapper.Register(first.Model);

        var act = () => NextOrmModelMapper.Register(second.Model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{typeof(S3EfRowB)}' is already mapped to a different table/column layout*");
    }

    /// <summary>A second model with the same layout but a different filter shape is a conflict too.</summary>
    [Fact]
    public void Register_ChangedFilterShape_ShouldThrow()
    {
        using var first = new ContextC1(Options<ContextC1>());
        using var second = new ContextC2(Options<ContextC2>());

        NextOrmModelMapper.Register(first.Model);

        var act = () => NextOrmModelMapper.Register(second.Model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already bridged from an EF Core model with a different query-filter or mapping shape*");
    }

    /// <summary>
    /// An EF model layers its filters onto a mapping published outside the bridge: anonymous filters are
    /// additive, a named filter is kept, the explicit table wins, and both filters drive the live query.
    /// </summary>
    [Fact]
    public void Register_OnExplicitMapping_ShouldMergeFiltersAndExecute()
    {
        var path = NewPath();
        try
        {
            CreateDatabase(
                path,
                "create table s3_ef_d (id integer primary key, tenant_id int not null, is_deleted integer not null, value int not null);",
                "insert into s3_ef_d (id, tenant_id, is_deleted, value) values "
                + "(1, 1, 0, 150), (2, 1, 0, 50), (3, 1, 1, 150), (4, 2, 0, 150);");

            using var next = NextContext(path);
            _ = next.From<S3EfRowD>();

            using var db = new ContextD(Options<ContextD>());
            NextOrmModelMapper.Register(db.Model);

            var metadata = DataContextCache.Metadata[typeof(S3EfRowD)];
            metadata.TableName.Should().Be("s3_ef_d", "the explicitly published mapping stays authoritative");
            metadata.Filters.Should().HaveCount(2, "the core anonymous filter plus the EF anonymous filter");

            var ids = next.From<S3EfRowD>()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            ids.Should().Equal(new[] { 1 }, "only the active tenant-1 row with value >= 100 survives both filters");
        }
        finally
        {
            Delete(path);
        }
    }

    /// <summary>An imported named filter colliding with a filter registered outside the bridge is refused by key.</summary>
    [Fact]
    public void Register_NamedFilterCollision_ShouldThrow()
    {
        using var next = NextContext(NewPath());
        _ = next.From<S3EfRowE>();

        using var db = new ContextE(Options<ContextE>());
        var act = () => NextOrmModelMapper.Register(db.Model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*query filter 'tenant'*already registered outside the EF Core bridge*");
    }

    /// <summary>A named EF filter on a mapping published without filters is kept and drives the query.</summary>
    [Fact]
    public void Register_NamedFilterOnUnfilteredMapping_ShouldBeKept()
    {
        var path = NewPath();
        try
        {
            CreateDatabase(
                path,
                "create table s3_ef_f (id integer primary key, tenant_id int not null);",
                "insert into s3_ef_f (id, tenant_id) values (1, 1), (2, 2);");

            using var next = NextContext(path);
            _ = next.From<S3EfRowF>();

            using var db = new ContextF(Options<ContextF>());
            NextOrmModelMapper.Register(db.Model);

            var filters = DataContextCache.Metadata[typeof(S3EfRowF)].Filters;
            filters.Should().ContainSingle();
            filters[0].Key.Should().Be("ef_named", "a named import without a collision is preserved by key");

            next.From<S3EfRowF>().OrderBy(x => x.Id).Select(x => x.Id).ToList()
                .Should().Equal(new[] { 1 }, "the imported named filter restricts the live query to tenant 1");
        }
        finally
        {
            Delete(path);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Fixture helpers
    // ---------------------------------------------------------------------------------------------
    private static string NewPath() => Path.Combine(Path.GetTempPath(), $"nextorm.s3ef.{Guid.NewGuid():N}.db");

    private static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static DbContextOptions<TContext> Options<TContext>(string? path = null)
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseSqlite($"Data Source='{path ?? NewPath()}';Pooling=False")
            .Options;

    private static SqliteDataContext NextContext(string path)
        => new($"Data Source='{path}';Pooling=False", new DataContextBuilder());

    private static void CreateDatabase(string path, params string[] statements)
    {
        using var connection = new SqliteConnection($"Data Source='{path}';Pooling=False");
        connection.Open();
        foreach (var sql in statements)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // CLR types and EF models
    // ---------------------------------------------------------------------------------------------

    /// <summary>Row of the publication fact.</summary>
    public sealed class S3EfRowA
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Value { get; set; }
    }

    /// <summary>Row of the different-layout conflict fact.</summary>
    public sealed class S3EfRowB
    {
        public int Id { get; set; }

        public int Value { get; set; }
    }

    /// <summary>Row of the changed-filter conflict fact.</summary>
    public sealed class S3EfRowC
    {
        public int Id { get; set; }

        public int Value { get; set; }
    }

    /// <summary>Row mapped outside the bridge first, then merged with an EF model's filters.</summary>
    [SqlTable("s3_ef_d")]
    [QueryFilter(FilterLambda = nameof(NotDeleted))]
    public sealed class S3EfRowD
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("value")]
        public int Value { get; set; }

        public static Expression<Func<S3EfRowD, bool>> NotDeleted() => row => !row.IsDeleted;
    }

    /// <summary>Row whose explicit named filter collides with the EF model's named filter.</summary>
    [SqlTable("s3_ef_e")]
    [QueryFilter(FilterKey = "tenant", FilterLambda = nameof(Tenant))]
    public sealed class S3EfRowE
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        public static Expression<Func<S3EfRowE, bool>> Tenant() => row => row.TenantId == 1;
    }

    private sealed class ContextA(DbContextOptions<ContextA> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowA>(entity =>
            {
                entity.ToTable("s3_ef_a");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Name).HasColumnName("name");
                entity.Property(row => row.Value).HasColumnName("value");
            });
    }

    private sealed class ContextB1(DbContextOptions<ContextB1> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowB>(entity =>
            {
                entity.ToTable("s3_ef_b");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
            });
    }

    private sealed class ContextB2(DbContextOptions<ContextB2> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowB>(entity =>
            {
                entity.ToTable("s3_ef_b");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("payload");
            });
    }

    private sealed class ContextC1(DbContextOptions<ContextC1> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowC>(entity =>
            {
                entity.ToTable("s3_ef_c");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter(row => row.Value >= 100);
            });
    }

    private sealed class ContextC2(DbContextOptions<ContextC2> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowC>(entity =>
            {
                entity.ToTable("s3_ef_c");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter(row => row.Value >= 200);
            });
    }

    /// <summary>EF model of the merge fact: a different table name plus an anonymous and a named filter.</summary>
    private sealed class ContextD(DbContextOptions<ContextD> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowD>(entity =>
            {
                entity.ToTable("s3_ef_d_ef_alias");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.IsDeleted).HasColumnName("is_deleted");
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter(row => row.Value >= 100 && row.TenantId == 1);
            });
    }

    /// <summary>EF model of the collision fact.</summary>
    private sealed class ContextE(DbContextOptions<ContextE> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowE>(entity =>
            {
                entity.ToTable("s3_ef_e");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter("tenant", row => row.TenantId == 2);
            });
    }

    /// <summary>Row mapped without filters, then given one named EF filter.</summary>
    [SqlTable("s3_ef_f")]
    public sealed class S3EfRowF
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    /// <summary>EF model of the kept-named-filter fact.</summary>
    private sealed class ContextF(DbContextOptions<ContextF> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<S3EfRowF>(entity =>
            {
                entity.ToTable("s3_ef_f");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.HasQueryFilter("ef_named", row => row.TenantId == 1);
            });
    }
}

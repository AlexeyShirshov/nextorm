using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextORM.EntityFrameworkCore;

namespace NextORM.Integration.Tests;

/// <summary>
/// Live branch coverage for <c>src/nextorm.entityframeworkcore/EfQueryFilterTranslator.cs</c> (cycle 8,
/// unit D2) executed against a file-based SQLite database. Every model is declared with the public EF
/// Core 10 <c>HasQueryFilter</c> surface, published through
/// <see cref="NextOrmModelMapper.Register"/> by <c>DbContext.CreateNextOrmContext()</c>, and the
/// supported filters are asserted by the row sets a live nextorm query returns over the same database
/// (never by rendered SQL). Unsupported EF shapes are asserted as the documented fail-fast
/// <see cref="NotSupportedException"/> raised while the bridge is created.
///
/// <para>
/// Targeted arms: <c>Translate</c> (:49-133 — the root walk, the empty-filter early return, named-key
/// uniqueness, context provenance and the key/parameter/body guards), <c>ResolveKey</c> (:83-90,
/// anonymous and named keys plus the whitespace-key rejection), <c>TranslateFilter</c> (:95-133) and
/// <c>OwnerRewriter</c> (:152-291 — owner constants and their member chain, system static members,
/// closure/static captures, <c>EF.Property</c>, <c>EF.Functions</c>, nested queries and navigation
/// members).
/// </para>
///
/// <para>
/// Recorded, never asserted. EF Core 10 keeps one slot per filter key, so a second
/// <c>HasQueryFilter("tenant", ...)</c> replaces the first instead of reaching the translator's
/// duplicate-key guard (the live fact below proves the replacement). The no-expression branch
/// (:98-99) is unreachable: <c>HasQueryFilter(key, null)</c> removes the slot (the live fact below
/// proves the slot disappears). The non-bool body branch (:131-132) is unreachable too: the typed
/// overload only accepts an <c>Expression&lt;Func&lt;TEntity, bool&gt;&gt;</c>, and the
/// <c>LambdaExpression</c> overload is validated by EF before the bridge sees it (observed:
/// "must accept a single parameter of type '&lt;entity&gt;' and return 'bool'" from
/// <c>EntityType.SetQueryFilter</c>).
/// <c>EntityParameterAdapter</c> (:141-144) and the non-assignable-parameter guard (:106-108) are
/// unreachable through the public model for the same reason: the mapper rejects every mapped
/// inheritance hierarchy before <c>Translate</c> runs
/// (<c>NextOrmModelMapper.RegisterAndCollect</c>:74-76), and EF Core itself rejects a filter whose
/// declared parameter type is not exactly the entity type. <c>QueryRootExpression</c>
/// (:225-226) and <c>InvocationExpression</c> (:220-221) cannot appear in a filter lambda written in
/// C#, and the non-scalar literal branch (:165-166) needs a hand-built constant. Line :208 (a
/// <c>DbFunctions</c>-declared method) is reached through a <c>DbFunctions</c> object override such
/// as <c>EF.Functions.GetHashCode()</c> (asserted below); the ordinary <c>EF.Functions.Like</c> call
/// is an extension method and is rejected by the nested-reference guard (:214-215) asserted below.
/// </para>
/// </summary>
[Collection("EF query filter lifecycle")]
public sealed class Cycle8EfFilterBridgeTests
{
    private static int StaticTenant = 1;

    // ---------------------------------------------------------------------------------------------
    // Supported filters: live row sets over one EF model.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NamedOwnerAndAnonymousFilters_FilterLiveRowsAndRereadOwner()
    {
        using var fixture = new SqliteFixture<PrimaryContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.NamedRows.AddRange(
            new C8NamedRow { TenantId = 1, Value = 10 },
            new C8NamedRow { TenantId = 2, Value = 20 },
            new C8NamedRow { TenantId = 1, Value = 30 });
        db.AnonymousRows.AddRange(
            new C8AnonymousRow { IsDeleted = false, Value = 1 },
            new C8AnonymousRow { IsDeleted = true, Value = 2 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        var efNamed = db.NamedRows.OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var nextNamed = next.From<C8NamedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        efNamed.Should().HaveCount(2);
        nextNamed.Should().Equal(efNamed);

        var efAnonymous = db.AnonymousRows.OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var nextAnonymous = next.From<C8AnonymousRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        efAnonymous.Should().HaveCount(1);
        nextAnonymous.Should().Equal(efAnonymous);

        // The owner read is live: changing the DbContext member re-reads on the same cached context.
        db.TenantId = 2;
        var tenantTwo = next.From<C8NamedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        tenantTwo.Should().Equal(db.NamedRows.OrderBy(row => row.Id).Select(row => row.Id).ToList());
        tenantTwo.Should().HaveCount(1);
        tenantTwo.Should().NotEqual(efNamed);
    }

    [Fact]
    public void EntityWithoutFilters_ReturnsEveryRow()
    {
        using var fixture = new SqliteFixture<PrimaryContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.NoFilterRows.AddRange(
            new C8NoFilterRow { Value = 1 },
            new C8NoFilterRow { Value = 2 },
            new C8NoFilterRow { Value = 3 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        next.From<C8NoFilterRow>().OrderBy(row => row.Value).Select(row => row.Value).ToList()
            .Should().Equal(1, 2, 3);
    }

    [Fact]
    public void TrueAndFalsePredicates_FilterLiveRows()
    {
        using var fixture = new SqliteFixture<PrimaryContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.TruthRows.AddRange(
            new C8TruthRow { Enabled = true, Value = 1 },
            new C8TruthRow { Enabled = false, Value = 2 });
        db.AlwaysRows.AddRange(
            new C8AlwaysRow { Value = 3 },
            new C8AlwaysRow { Value = 4 });
        db.ValueRows.AddRange(
            new C8ValueRow { Value = -1 },
            new C8ValueRow { Value = 0 },
            new C8ValueRow { Value = 5 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        next.From<C8TruthRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(1);
        next.From<C8AlwaysRow>().OrderBy(row => row.Value).Select(row => row.Value).ToList().Should().Equal(3, 4);
        next.From<C8ValueRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(5);
    }

    [Fact]
    public void NullablePredicates_FilterLiveRows()
    {
        using var fixture = new SqliteFixture<PrimaryContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.NullableRows.AddRange(
            new C8NullableRow { OptIn = null, Note = 5, Value = 1 },
            new C8NullableRow { OptIn = false, Note = null, Value = 2 },
            new C8NullableRow { OptIn = true, Note = null, Value = 3 },
            new C8NullableRow { OptIn = true, Note = 7, Value = 4 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        next.From<C8NullableRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(4);
    }

    [Fact]
    public void SupportedLiterals_FilterLiveRows()
    {
        using var fixture = new SqliteFixture<PrimaryContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.LiteralRows.AddRange(
            new C8LiteralRow { Kind = C8Kind.Active, Name = "ok", Ratio = 3.2, Value = 1 },
            new C8LiteralRow { Kind = C8Kind.Active, Name = "blocked", Ratio = 5.0, Value = 2 },
            new C8LiteralRow { Kind = C8Kind.Active, Name = string.Empty, Ratio = 5.0, Value = 3 },
            new C8LiteralRow { Kind = C8Kind.Inactive, Name = "ok", Ratio = 5.0, Value = 4 },
            new C8LiteralRow { Kind = C8Kind.Active, Name = "ok", Ratio = 0.5, Value = 5 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        next.From<C8LiteralRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(1);
    }

    [Fact]
    public void ByteArrayNullCheck_FiltersLiveRows()
    {
        using var fixture = new SqliteFixture<PrimaryContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.ByteRows.AddRange(
            new C8ByteRow { Hash = null, Value = 1 },
            new C8ByteRow { Hash = [1, 2], Value = 2 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        next.From<C8ByteRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(2);
    }

    // ---------------------------------------------------------------------------------------------
    // Public entry point and key resolution.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RegisterEntryPoint_ThenBridgeReusesMapping_LiveRows()
    {
        using var fixture = new SqliteFixture<RegisterOnlyContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.Rows.Add(new C8RegisterRow { Value = 7 });
        db.SaveChanges();

        NextOrmModelMapper.Register(db.Model);

        using var next = db.CreateNextOrmContext();

        next.From<C8RegisterRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(7);
    }

    [Fact]
    public void DuplicateNamedKey_LastDeclarationWins()
    {
        using var fixture = new SqliteFixture<DuplicateKeyContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.Rows.AddRange(
            new C8DupRow { TenantId = 1, Value = 1 },
            new C8DupRow { TenantId = 2, Value = 2 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        // EF Core keeps one slot per key: the second declaration replaced the first, so the duplicate
        // guard in EfQueryFilterTranslator.ResolveKey is not reachable through the public API.
        next.From<C8DupRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(2);
    }

    [Fact]
    public void NullFilterExpression_RemovesTheSlotInsteadOfReachingTheTranslator()
    {
        using var fixture = new SqliteFixture<NullSlotContext>();
        var db = fixture.Db;
        db.Database.EnsureCreated();

        db.Rows.AddRange(
            new C8NullSlotRow { TenantId = 1, Value = 1 },
            new C8NullSlotRow { TenantId = 2, Value = 2 });
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();

        // HasQueryFilter(key, null) removes the configured slot, so the no-expression guard in
        // EfQueryFilterTranslator.TranslateFilter (:98-99) cannot be reached through the public API.
        next.From<C8NullSlotRow>().OrderBy(row => row.Id).Select(row => row.Value).ToList().Should().Equal(1, 2);
    }

    [Fact]
    public void WhitespaceNamedKey_IsRejected()
    {
        using var fixture = new SqliteFixture<WhitespaceKeyContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*non-empty key*");
    }

    // ---------------------------------------------------------------------------------------------
    // Owner rewriter rejections.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ClosureCapture_IsRejected()
    {
        using var fixture = new SqliteFixture<ClosureCaptureContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*closure/local capture*");
    }

    [Fact]
    public void StaticMemberCapture_IsRejected()
    {
        using var fixture = new SqliteFixture<StaticCaptureContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*static-member capture*");
    }

    [Fact]
    public void EfProperty_IsRejected()
    {
        using var fixture = new SqliteFixture<EfPropertyContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*EF.Property*");
    }

    [Fact]
    public void EfFunctions_IsRejected()
    {
        using var fixture = new SqliteFixture<EfFunctionsContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*EF.Functions*");
    }

    [Fact]
    public void NestedQuery_IsRejected()
    {
        using var fixture = new SqliteFixture<NestedQueryContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*nested query*");
    }

    [Fact]
    public void ReferenceNavigation_IsRejected()
    {
        using var fixture = new SqliteFixture<ReferenceNavigationContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*navigation member 'Child'*");
    }

    [Fact]
    public void CollectionNavigation_IsRejected()
    {
        using var fixture = new SqliteFixture<CollectionNavigationContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*navigation member 'Children'*");
    }

    [Fact]
    public void EfFunctionsObjectMember_IsRejected()
    {
        using var fixture = new SqliteFixture<EfFunctionsObjectMemberContext>();
        var db = fixture.Db;

        FluentActions.Invoking(() =>
        {
            using var context = db.CreateNextOrmContext();
        }).Should().Throw<NotSupportedException>().WithMessage("*EF.Functions*");
    }

    // ---------------------------------------------------------------------------------------------
    // Fixture and entities.
    // ---------------------------------------------------------------------------------------------

    /// <summary>One fresh file-backed SQLite database per fact; the EF context owns the connection.</summary>
    private sealed class SqliteFixture<TContext> : IDisposable
        where TContext : DbContext
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"nextorm.c8ef.{Guid.NewGuid():N}.db");

        public SqliteFixture()
            => Db = (TContext)Activator.CreateInstance(typeof(TContext), _path)!;

        public TContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();

            if (File.Exists(_path))
                File.Delete(_path);
        }
    }

    private enum C8Kind
    {
        Inactive = 0,
        Active = 1,
    }

    private sealed class C8NamedRow
    {
        public long Id { get; set; }

        public int TenantId { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8AnonymousRow
    {
        public long Id { get; set; }

        public bool IsDeleted { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8NoFilterRow
    {
        public long Id { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8TruthRow
    {
        public long Id { get; set; }

        public bool Enabled { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8AlwaysRow
    {
        public long Id { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8ValueRow
    {
        public long Id { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8NullableRow
    {
        public long Id { get; set; }

        public bool? OptIn { get; set; }

        public int? Note { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8LiteralRow
    {
        public long Id { get; set; }

        public C8Kind Kind { get; set; }

        public string Name { get; set; } = string.Empty;

        public double Ratio { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8ByteRow
    {
        public long Id { get; set; }

        public byte[]? Hash { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8RegisterRow
    {
        public long Id { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8DupRow
    {
        public long Id { get; set; }

        public int TenantId { get; set; }

        public int Value { get; set; }
    }

    private sealed class C8GuardRow
    {
        public long Id { get; set; }

        public int TenantId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class C8NavRow
    {
        public long Id { get; set; }

        public long? ChildId { get; set; }

        public C8NavChild? Child { get; set; }
    }

    private sealed class C8NavChild
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class C8TreeRow
    {
        public long Id { get; set; }

        public List<C8TreeChild> Children { get; set; } = [];
    }

    private sealed class C8TreeChild
    {
        public long Id { get; set; }

        public long ParentId { get; set; }
    }

    private sealed class C8OtherRow
    {
        public long Id { get; set; }
    }

    private sealed class C8NullSlotRow
    {
        public long Id { get; set; }

        public int TenantId { get; set; }

        public int Value { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // EF models.
    // ---------------------------------------------------------------------------------------------

    private sealed class PrimaryContext : DbContext
    {
        public PrimaryContext(string path)
            : base(new DbContextOptionsBuilder<PrimaryContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        public int TenantId { get; set; } = 1;

        public DbSet<C8NamedRow> NamedRows => Set<C8NamedRow>();

        public DbSet<C8AnonymousRow> AnonymousRows => Set<C8AnonymousRow>();

        public DbSet<C8NoFilterRow> NoFilterRows => Set<C8NoFilterRow>();

        public DbSet<C8TruthRow> TruthRows => Set<C8TruthRow>();

        public DbSet<C8AlwaysRow> AlwaysRows => Set<C8AlwaysRow>();

        public DbSet<C8ValueRow> ValueRows => Set<C8ValueRow>();

        public DbSet<C8NullableRow> NullableRows => Set<C8NullableRow>();

        public DbSet<C8LiteralRow> LiteralRows => Set<C8LiteralRow>();

        public DbSet<C8ByteRow> ByteRows => Set<C8ByteRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8NamedRow>(entity =>
            {
                entity.ToTable("c8_named");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });

            modelBuilder.Entity<C8AnonymousRow>(entity =>
            {
                entity.ToTable("c8_anon");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => !row.IsDeleted);
            });

            modelBuilder.Entity<C8NoFilterRow>(entity =>
            {
                entity.ToTable("c8_plain");
                entity.HasKey(row => row.Id);
            });

            modelBuilder.Entity<C8TruthRow>(entity =>
            {
                entity.ToTable("c8_truth");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => row.Enabled);
            });

            modelBuilder.Entity<C8AlwaysRow>(entity =>
            {
                entity.ToTable("c8_always");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => true);
            });

            modelBuilder.Entity<C8ValueRow>(entity =>
            {
                entity.ToTable("c8_value");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => row.Value > 0);
            });

            modelBuilder.Entity<C8NullableRow>(entity =>
            {
                entity.ToTable("c8_nullable");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => row.OptIn == true && row.Note != null);
            });

            modelBuilder.Entity<C8LiteralRow>(entity =>
            {
                entity.ToTable("c8_literal");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => row.Kind == C8Kind.Active && row.Name != "blocked" && row.Name != string.Empty && row.Ratio > 1.0);
            });

            modelBuilder.Entity<C8ByteRow>(entity =>
            {
                entity.ToTable("c8_bytes");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => row.Hash != null);
            });
        }
    }

    private sealed class RegisterOnlyContext : DbContext
    {
        public RegisterOnlyContext(string path)
            : base(new DbContextOptionsBuilder<RegisterOnlyContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        public DbSet<C8RegisterRow> Rows => Set<C8RegisterRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8RegisterRow>(entity =>
            {
                entity.ToTable("c8_register");
                entity.HasKey(row => row.Id);
            });
        }
    }

    private sealed class DuplicateKeyContext : DbContext
    {
        public DuplicateKeyContext(string path)
            : base(new DbContextOptionsBuilder<DuplicateKeyContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        public DbSet<C8DupRow> Rows => Set<C8DupRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8DupRow>(entity =>
            {
                entity.ToTable("c8_dup");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter("tenant", row => row.TenantId == 1);
                entity.HasQueryFilter("tenant", row => row.TenantId == 2);
            });
        }
    }

    private sealed class WhitespaceKeyContext : DbContext
    {
        public WhitespaceKeyContext(string path)
            : base(new DbContextOptionsBuilder<WhitespaceKeyContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8GuardRow>(entity =>
            {
                entity.ToTable("c8_guard");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(" ", row => row.TenantId == 1);
            });
        }
    }

    private sealed class ClosureCaptureContext : DbContext
    {
        public ClosureCaptureContext(string path)
            : base(new DbContextOptionsBuilder<ClosureCaptureContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tenant = 1;
            modelBuilder.Entity<C8GuardRow>(entity =>
            {
                entity.ToTable("c8_guard");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => row.TenantId == tenant);
            });
        }
    }

    private sealed class StaticCaptureContext : DbContext
    {
        public StaticCaptureContext(string path)
            : base(new DbContextOptionsBuilder<StaticCaptureContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8GuardRow>(entity =>
            {
                entity.ToTable("c8_guard");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => row.TenantId == StaticTenant);
            });
        }
    }

    private sealed class EfPropertyContext : DbContext
    {
        public EfPropertyContext(string path)
            : base(new DbContextOptionsBuilder<EfPropertyContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8GuardRow>(entity =>
            {
                entity.ToTable("c8_guard");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => EF.Property<int>(row, nameof(C8GuardRow.TenantId)) == 1);
            });
        }
    }

    private sealed class EfFunctionsContext : DbContext
    {
        public EfFunctionsContext(string path)
            : base(new DbContextOptionsBuilder<EfFunctionsContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8GuardRow>(entity =>
            {
                entity.ToTable("c8_guard");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => EF.Functions.Like(row.Name, "a%"));
            });
        }
    }

    private sealed class NestedQueryContext : DbContext
    {
        public NestedQueryContext(string path)
            : base(new DbContextOptionsBuilder<NestedQueryContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        public DbSet<C8OtherRow> Others => Set<C8OtherRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8GuardRow>(entity =>
            {
                entity.ToTable("c8_guard");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => Others.Any(other => other.Id == row.TenantId));
            });

            modelBuilder.Entity<C8OtherRow>(entity =>
            {
                entity.ToTable("c8_other");
                entity.HasKey(row => row.Id);
            });
        }
    }

    private sealed class ReferenceNavigationContext : DbContext
    {
        public ReferenceNavigationContext(string path)
            : base(new DbContextOptionsBuilder<ReferenceNavigationContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8NavRow>(entity =>
            {
                entity.ToTable("c8_nav");
                entity.HasKey(row => row.Id);
                entity.HasOne(row => row.Child).WithMany().HasForeignKey(row => row.ChildId);
                entity.HasQueryFilter(row => row.Child != null);
            });

            modelBuilder.Entity<C8NavChild>(entity =>
            {
                entity.ToTable("c8_nav_child");
                entity.HasKey(row => row.Id);
            });
        }
    }

    private sealed class CollectionNavigationContext : DbContext
    {
        public CollectionNavigationContext(string path)
            : base(new DbContextOptionsBuilder<CollectionNavigationContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8TreeRow>(entity =>
            {
                entity.ToTable("c8_tree");
                entity.HasKey(row => row.Id);
                entity.HasMany(row => row.Children).WithOne().HasForeignKey(child => child.ParentId);
                entity.HasQueryFilter(row => row.Children.Count > 0);
            });

            modelBuilder.Entity<C8TreeChild>(entity =>
            {
                entity.ToTable("c8_tree_child");
                entity.HasKey(row => row.Id);
            });
        }
    }

    private sealed class NullSlotContext : DbContext
    {
        public NullSlotContext(string path)
            : base(new DbContextOptionsBuilder<NullSlotContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        public DbSet<C8NullSlotRow> Rows => Set<C8NullSlotRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8NullSlotRow>(entity =>
            {
                entity.ToTable("c8_nullslot");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter("tenant", row => row.TenantId == 1);
                entity.HasQueryFilter("tenant", (System.Linq.Expressions.LambdaExpression)null!);
            });
        }
    }

    private sealed class EfFunctionsObjectMemberContext : DbContext
    {
        public EfFunctionsObjectMemberContext(string path)
            : base(new DbContextOptionsBuilder<EfFunctionsObjectMemberContext>().UseSqlite($"Data Source={path}").Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<C8GuardRow>(entity =>
            {
                entity.ToTable("c8_guard");
                entity.HasKey(row => row.Id);
                entity.HasQueryFilter(row => EF.Functions.GetHashCode() > 0 && row.TenantId == 1);
            });
        }
    }
}

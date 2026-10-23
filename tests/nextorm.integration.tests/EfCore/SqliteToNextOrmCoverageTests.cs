using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.EntityFrameworkCore;

namespace NextORM.Integration.Tests;

/// <summary>
/// Live EF-Core-SQLite branch coverage for <see cref="NextOrmQueryableExtensions"/>: both public
/// <c>ToNextOrm</c> overloads, the whole supported operator set (Where, OrderBy, ThenBy, Skip, Take,
/// Distinct, AsNoTracking/TagWith and IgnoreQueryFilters) and every rejection chain a plain LINQ query
/// can reach. Each fact executes at least one real nextorm query on the EF Core SQLite database before
/// it asserts the translated rows or the documented <see cref="NotSupportedException"/>.
/// </summary>
/// <remarks>
/// No synthetic expression trees are built here: only guards reachable by composing ordinary LINQ
/// operators on the EF queryable are covered. Four guards are unreachable through the public API and
/// stay with the unit suites: a root entity type mismatch and a non-queryable operator with a colliding
/// name need a hand-built tree or a forged <see cref="DbSet{TEntity}"/>; the two <c>ThenBy</c> guards
/// (before a primary ordering, after paging) cannot be reached because <c>Queryable.ThenBy</c> validates
/// that its receiver expression is an ordered one, so an <c>IOrderedQueryable</c> node always has an
/// <c>OrderBy</c> inside it and never a <c>Skip</c>/<c>Take</c>; the non-constant count guard cannot be
/// reached because <c>Queryable.Skip</c>/<c>Take</c> always carry a constant <see cref="int"/>, and the
/// null filter-key collection is rejected by EF Core before the bridge sees it.
/// </remarks>
[Collection("EF query filter lifecycle")]
public sealed class SqliteToNextOrmCoverageTests
{
    /// <summary>
    /// A real nextorm query through the <see cref="DbSet{TEntity}"/> overload; its result proves the
    /// SQLite database is live before the fact goes on to assert a rejection.
    /// </summary>
    private static void AssertLive(TocContext db)
        => db.Tracks.ToNextOrm().OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L, 4L, 5L);

    [Fact]
    public void DbSetOverload_WhereAndOrderBy_ShouldReturnMatchingRows()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;

        var actual = db.Tracks.ToNextOrm().Where(x => x.Plays >= 30).OrderBy(x => x.Id).Select(x => x.Id).ToList();

        db.Tracks.AsNoTracking().Count().Should().Be(5);
        actual.Should().Equal(1L, 3L, 4L);
    }

    [Fact]
    public void ComposedOverload_WhereAndOrderByDescending_ShouldMatchEfQuery()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        var expected = db.Tracks.Where(x => x.Name == "beta").OrderByDescending(x => x.Plays)
            .Select(x => x.Id).ToList();
        var actual = db.Tracks.Where(x => x.Name == "beta").OrderByDescending(x => x.Plays)
            .ToNextOrm(db).Select(x => x.Id).ToList();

        expected.Should().Equal(5L, 2L);
        actual.Should().Equal(expected);
    }

    [Fact]
    public void ComposedOverload_ThenByVariants_ShouldApplyBothKeys()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        var expectedAscending = db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Plays).Select(x => x.Id).ToList();
        var actualAscending = db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Plays)
            .ToNextOrm(db).Select(x => x.Id).ToList();

        actualAscending.Should().Equal(expectedAscending);
        actualAscending.Should().Equal(3L, 1L, 2L, 5L, 4L);

        var expectedDescending = db.Tracks.OrderByDescending(x => x.Name).ThenByDescending(x => x.Plays)
            .Select(x => x.Id).ToList();
        var actualDescending = db.Tracks.OrderByDescending(x => x.Name).ThenByDescending(x => x.Plays)
            .ToNextOrm(db).Select(x => x.Id).ToList();

        actualDescending.Should().Equal(expectedDescending);
    }

    [Fact]
    public void ComposedOverload_SkipThenTake_ShouldPage()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        // Skip followed by Take is the one paging order the bridge accepts (Take after Skip is the
        // documented rejection): the pair must still return the EF page.
        var expected = db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(1).Take(2)
            .Select(x => x.Id).ToList();
        var actual = db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(1).Take(2)
            .ToNextOrm(db).Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
        actual.Should().HaveCount(2);
    }

    [Fact]
    public void ComposedOverload_Distinct_ShouldDeduplicate()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        db.Database.ExecuteSqlRaw(
            "insert into toc_duplicate (duplicate_name, duplicate_plays) values ('dup', 1), ('dup', 1), ('other', 2)");

        AssertLive(db);

        var expected = db.Duplicates.Distinct().Select(x => x.Name).OrderBy(x => x).ToList();
        var actual = db.Duplicates.Distinct().ToNextOrm(db).OrderBy(x => x.Name).Select(x => x.Name).ToList();

        actual.Should().Equal(expected);
        actual.Should().Equal("dup", "other");
    }

    [Fact]
    public void ComposedOverload_NoTrackingAndTagWithVariants_ShouldBeStripped()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        db.Tracks.AsNoTracking().Where(x => x.Plays > 10).TagWith("probe")
            .ToNextOrm(db).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(1L, 3L, 4L, 5L);

        db.Tracks.AsNoTrackingWithIdentityResolution().Where(x => x.Plays > 10)
            .ToNextOrm(db).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(1L, 3L, 4L, 5L);
    }

    [Fact]
    public void ComposedOverload_IgnoreQueryFiltersVariants_ShouldBeAccepted()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        // The filterless model has no filter to disable, so each form only has to be accepted and
        // translated: the parameterless form, an empty key list, a key list with blank entries (which
        // are normalized away) and a null key list all leave the rows untouched.
        db.Tracks.IgnoreQueryFilters().ToNextOrm(db).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(1L, 2L, 3L, 4L, 5L);

        db.Tracks.IgnoreQueryFilters(Array.Empty<string>()).ToNextOrm(db).OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(1L, 2L, 3L, 4L, 5L);

        db.Tracks.IgnoreQueryFilters(new[] { "", "  ", "named" }).ToNextOrm(db)
            .OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(1L, 2L, 3L, 4L, 5L);
    }

    [Fact]
    public void AsTracking_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.AsTracking().ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'AsTracking'*read-only*");
    }

    [Fact]
    public void WhereAfterSkip_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Skip(1).Where(x => x.Plays > 0).ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'Where'*after 'Skip' or 'Take'*");
    }

    [Fact]
    public void WhereAfterTake_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Take(2).Where(x => x.Plays > 0).ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'Where'*after 'Skip' or 'Take'*");
    }

    [Fact]
    public void SecondPrimaryOrderBy_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.OrderBy(x => x.Id).OrderByDescending(x => x.Name).ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'OrderByDescending'*second primary ordering*");
    }

    [Fact]
    public void OrderByAfterPaging_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Skip(1).OrderBy(x => x.Id).ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'OrderBy'*after 'Skip' or 'Take'*");
    }

    [Fact]
    public void OrderingKeysWithClientEnumeration_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        var plays = new[] { 10, 20, 30 };

        // Both the primary and the secondary ordering key run the same lambda guard; a client-side
        // enumeration inside either key cannot be translated.
        FluentActions.Invoking(() => db.Tracks.OrderBy(x => Enumerable.Contains(plays, x.Plays)).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'OrderBy'*nested query, subquery*");

        FluentActions.Invoking(() => db.Tracks.OrderBy(x => x.Id).ThenBy(x => Enumerable.Contains(plays, x.Plays)).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'ThenBy'*nested query, subquery*");
    }

    [Fact]
    public void CollectionNavigationMember_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Artists.Where(x => x.Tracks.Count > 0).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*member access*");
    }

    [Fact]
    public void DoubleSkip_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Skip(1).Skip(2).ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'Skip'*more than once*");
    }

    [Fact]
    public void SkipAfterTake_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Take(2).Skip(1).ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'Skip'*after 'Take'*");
    }

    [Fact]
    public void DoubleTake_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Take(2).Take(3).ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'Take'*more than once*");
    }

    [Fact]
    public void DoubleDistinct_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Distinct().Distinct().ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'Distinct'*more than once*");
    }

    [Fact]
    public void DistinctAfterPaging_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Skip(1).Distinct().ToNextOrm(db))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'Distinct'*after 'Skip' or 'Take'*");
    }

    [Fact]
    public void UnsupportedQueryableOperators_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Select(x => x.Name).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'Select'*");

        FluentActions.Invoking(() => db.Tracks.Cast<TocTrack>().ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'Cast'*");

        FluentActions.Invoking(() => db.Tracks.Concat(db.Tracks).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'Concat'*");

        FluentActions.Invoking(() => db.Tracks.GroupBy(x => x.Name, (name, group) => new { Name = name }).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'GroupBy'*");

        FluentActions.Invoking(() => db.Tracks.Join(db.Tracks, outer => outer.Id, inner => inner.Id, (outer, inner) => outer).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'Join'*");
    }

    [Fact]
    public void UnsupportedEfCoreOperators_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Include(x => x.Artist).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'Include'*");

        FluentActions.Invoking(() => db.Tracks.IgnoreAutoIncludes().ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*'IgnoreAutoIncludes'*");
    }

    [Fact]
    public void NonDbSetRoots_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.FromSqlRaw("select * from toc_track").ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*Only DbSet<T> roots*");

        FluentActions.Invoking(() => new[] { "alpha", "beta" }.AsQueryable().Where(x => x.Length > 1).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*Only DbSet<T> roots*");
    }

    [Fact]
    public void LambdaControlFlow_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        FluentActions.Invoking(() => db.Tracks.Where(x => db.Tracks.Any(y => y.Id == x.Id)).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*nested query, subquery*");

        FluentActions.Invoking(() => db.Tracks.Where(x => Enumerable.Contains(new[] { 1, 2, 3 }, x.Plays)).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*nested query, subquery*");

        FluentActions.Invoking(() => db.Tracks.Where(x => EF.Property<int>(x, "Plays") > 10).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*EF.Property*");

        FluentActions.Invoking(() => db.Tracks.Where(x => EF.Functions.Like(x.Name, "a%")).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*EF.Functions*");
    }

    [Fact]
    public void LambdaMembersAndInvocation_ShouldBeRejected()
    {
        using var fixture = new SqliteFixture();
        var db = fixture.Db;
        AssertLive(db);

        Func<TocTrack, bool> isAlpha = x => x.Name == "alpha";

        FluentActions.Invoking(() => db.Tracks.Where(x => x.Artist!.Name == "alpha").ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*member access*");

        FluentActions.Invoking(() => db.Tracks.OrderBy(x => x.Artist!.Name).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*member access*");

        FluentActions.Invoking(() => db.Tracks.Where(x => isAlpha(x)).ToNextOrm(db))
            .Should().Throw<NotSupportedException>().WithMessage("*invocation expression*");
    }

    /// <summary>
    /// A file-backed EF Core SQLite database seeded with the five track rows every fact asserts on.
    /// </summary>
    private sealed class SqliteFixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"nextorm.ef.toc.{Guid.NewGuid():N}.db");

        public SqliteFixture()
        {
            Options = new DbContextOptionsBuilder<TocContext>().UseSqlite($"Data Source={_path}").Options;
            Db = new TocContext(Options);
            Db.Database.EnsureCreated();

            Db.Tracks.AddRange(
                new TocTrack { Name = "alpha", Plays = 50 },
                new TocTrack { Name = "beta", Plays = 10 },
                new TocTrack { Name = "alpha", Plays = 30 },
                new TocTrack { Name = "gamma", Plays = 40 },
                new TocTrack { Name = "beta", Plays = 20 });
            Db.SaveChanges();
        }

        public DbContextOptions<TocContext> Options { get; }

        public TocContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();

            if (File.Exists(_path))
                File.Delete(_path);
        }
    }

    private sealed class TocContext(DbContextOptions<TocContext> options) : DbContext(options)
    {
        public DbSet<TocTrack> Tracks => Set<TocTrack>();

        public DbSet<TocArtist> Artists => Set<TocArtist>();

        public DbSet<TocDuplicate> Duplicates => Set<TocDuplicate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TocDuplicate>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("toc_duplicate");
                entity.Property(x => x.Name).HasColumnName("duplicate_name");
                entity.Property(x => x.Plays).HasColumnName("duplicate_plays");
            });

            modelBuilder.Entity<TocArtist>(entity =>
            {
                entity.ToTable("toc_artist");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("artist_id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("artist_name");
            });

            modelBuilder.Entity<TocTrack>(entity =>
            {
                entity.ToTable("toc_track");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("track_id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("track_name");
                entity.Property(x => x.Plays).HasColumnName("track_plays");
                entity.Property(x => x.ArtistId).HasColumnName("artist_id");
                entity.HasOne(x => x.Artist).WithMany(x => x.Tracks).HasForeignKey(x => x.ArtistId);
            });
        }
    }

    private sealed class TocTrack
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Plays { get; set; }

        public long? ArtistId { get; set; }

        public TocArtist? Artist { get; set; }
    }

    private sealed class TocArtist
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<TocTrack> Tracks { get; set; } = new List<TocTrack>();
    }

    private sealed class TocDuplicate
    {
        public string Name { get; set; } = string.Empty;

        public int Plays { get; set; }
    }
}

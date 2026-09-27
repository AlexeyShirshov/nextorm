using System.Collections;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore.Tests;

/// <summary>
/// <c>ToNextOrm</c> translates the supported operators of an EF Core <c>IQueryable&lt;T&gt;</c> into a
/// nextorm query that runs on the EF connection, and rejects every operator it cannot translate without
/// a silent client-side fallback.
/// </summary>
public sealed class ToNextOrmTests : EfCoreMetadataCleanup
{
    [Fact]
    public async Task DbSetOverload_ShouldMatchEfQuery()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var expected = await db.Tracks.OrderBy(x => x.Id).Select(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.ToNextOrm().OrderBy(x => x.Id).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
        actual.Should().HaveCount(5);
    }

    [Fact]
    public async Task ComposedOverload_ShouldMatchEfQuery()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        const string marker = "alpha";

        var expected = await db.Tracks.Where(x => x.Name == marker).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Plays }).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.Where(x => x.Name == marker).ToNextOrm(db).OrderBy(x => x.Id).ToList()
            .Select(x => new { x.Id, x.Name, x.Plays }).ToList();

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task OrderBy_ThenBy_ShouldApplyBothKeys()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var expected = await db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Plays)
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Plays).ToNextOrm(db).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
        actual.Should().Equal(3L, 1L, 2L, 5L, 4L);
    }

    [Fact]
    public async Task OrderByDescending_ThenByDescending_ShouldApplyBothKeys()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var expected = await db.Tracks.OrderByDescending(x => x.Name).ThenByDescending(x => x.Plays)
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.OrderByDescending(x => x.Name).ThenByDescending(x => x.Plays).ToNextOrm(db).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
    }

    [Fact]
    public async Task SkipTake_ShouldReturnPageInOrder()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var expected = await db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(1).Take(2)
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(1).Take(2).ToNextOrm(db).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
        actual.Should().HaveCount(2);
    }

    [Fact]
    public async Task Distinct_ShouldReturnDistinctRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        db.Database.ExecuteSqlRaw(
            "insert into duplicate_entity (duplicate_name, duplicate_plays) values ('dup', 1), ('dup', 1), ('other', 2)");

        var expected = await db.Duplicates.Distinct().OrderBy(x => x.Name)
            .Select(x => new { x.Name, x.Plays }).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Duplicates.Distinct().ToNextOrm(db).OrderBy(x => x.Name).ToList()
            .Select(x => new { x.Name, x.Plays }).ToList();

        actual.Should().Equal(expected);
        actual.Should().HaveCount(2);
    }

    [Fact]
    public async Task AsNoTrackingAndTagWith_ShouldBeStripped()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var expected = await db.Tracks.AsNoTracking().Where(x => x.Plays > 10).TagWith("probe")
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.AsNoTracking().Where(x => x.Plays > 10).TagWith("probe").ToNextOrm(db).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
    }

    [Fact]
    public void ToNextOrm_ShouldRenderNextOrmSqlNotEfSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();

        var efSql = db.Tracks.Where(x => x.Plays > 10).ToQueryString();
        var builder = db.Tracks.Where(x => x.Plays > 10).ToNextOrm(db);

        using var ctx = db.GetNextOrmContext();
        var nextormSql = NextOrmSql.Of(ctx, builder.ToCommand());

        efSql.Should().Contain("SELECT");
        nextormSql.Should().StartWith("select ");
        nextormSql.Should().Contain("from track_entity");
        nextormSql.Should().NotContain("SELECT");
    }

    [Fact]
    public void Where_ShouldRenderServerSideFilter()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();

        var builder = db.Tracks.Where(x => x.Plays > 10).ToNextOrm(db);

        using var ctx = db.GetNextOrmContext();
        var sql = NextOrmSql.Of(ctx, builder.ToCommand());

        sql.Should().Contain("where");
        sql.Should().Contain("track_plays");
    }

    [Fact]
    public void Select_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Select(x => x.Name).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*Select*");
    }

    [Fact]
    public void Include_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Include(x => x.Artist).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*Include*");
    }

    [Fact]
    public void GroupBy_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.GroupBy(x => x.Name, (name, group) => new { Name = name, Count = group.Count() }).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*GroupBy*");
    }

    [Fact]
    public void Join_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Join(db.Tracks, outer => outer.Id, inner => inner.Id, (outer, inner) => new { outer.Id }).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*Join*");
    }

    [Fact]
    public void SecondPrimaryOrdering_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.OrderBy(x => x.Id).OrderByDescending(x => x.Name).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*OrderByDescending*");
    }

    [Fact]
    public void ThenBy_WithoutPrimary_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var parameter = Expression.Parameter(typeof(TrackRow), "x");
        var keySelector = Expression.Lambda<Func<TrackRow, object>>(
            Expression.Convert(Expression.Property(parameter, nameof(TrackRow.Name)), typeof(object)),
            parameter);

        var ordered = new StubOrderedQueryable<TrackRow>();
        var thenBy = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.ThenBy),
            new[] { typeof(TrackRow), typeof(object) },
            Expression.Constant(ordered, typeof(IOrderedQueryable<TrackRow>)),
            Expression.Quote(keySelector));

        var query = new StubQueryable<TrackRow>(thenBy);

        var act = () => query.ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*ThenBy*");
    }

    [Fact]
    public void EfProperty_InLambda_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Where(x => EF.Property<int>(x, "Plays") > 10).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*EF.Property*");
    }

    [Fact]
    public void NestedSubquery_InLambda_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Where(x => db.Tracks.Any(y => y.Id == x.Id)).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*subquer*");
    }

    [Fact]
    public void NonDbSetRoot_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => new[] { "alpha", "beta" }.AsQueryable().Where(x => x.Length > 1).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*DbSet*");
    }

    [Fact]
    public void AsTracking_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.AsTracking().ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*AsTracking*");
    }

    [Fact]
    public async Task AsNoTrackingWithIdentityResolution_ShouldBeStripped()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var expected = await db.Tracks.AsNoTrackingWithIdentityResolution().Where(x => x.Plays > 10)
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.AsNoTrackingWithIdentityResolution().Where(x => x.Plays > 10).ToNextOrm(db).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
    }

    [Fact]
    public void IgnoreQueryFilters_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.IgnoreQueryFilters().ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*IgnoreQueryFilters*");
    }

    [Fact]
    public void FromSqlRawRoot_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.FromSqlRaw("select * from track_entity").ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*DbSet*");
    }

    [Fact]
    public void RootEntityTypeMismatch_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();

        var root = ((IQueryable<TrackRow>)db.Tracks).Expression;
        var query = new StubQueryable<ArtistRow>(root);

        var act = () => query.ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*ArtistRow*");
    }

    [Fact]
    public void SameNamedNonQueryableOperator_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();

        var parameter = Expression.Parameter(typeof(TrackRow), "x");
        var predicate = Expression.Lambda<Func<TrackRow, bool>>(
            Expression.GreaterThan(Expression.Property(parameter, nameof(TrackRow.Plays)), Expression.Constant(10)),
            parameter);

        var fakeWhere = Expression.Call(
            typeof(ToNextOrmTests),
            nameof(FakeWhere),
            new[] { typeof(TrackRow) },
            ((IQueryable<TrackRow>)db.Tracks).Expression,
            Expression.Quote(predicate));

        var act = () => new StubQueryable<TrackRow>(fakeWhere).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*Where*");
    }

    [Fact]
    public void NavigationMember_InPredicate_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Where(x => x.Artist!.Name == "alpha").ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*member access*");
    }

    [Fact]
    public void NavigationMember_InOrderingKey_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.OrderBy(x => x.Artist!.Name).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*member access*");
    }

    [Fact]
    public void BareNavigation_InPredicate_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Where(x => x.Artist == null).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*member access*");
    }

    [Fact]
    public void CastedNavigationMember_InPredicate_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Where(x => ((ArtistRow)x.Artist!).Name == "alpha").ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*member access*");
    }

    [Fact]
    public void ConditionalNavigationMember_InPredicate_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Tracks.Where(x => (x.Plays > 14 ? x.Artist : null) == null).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*member access*");
    }

    [Fact]
    public void CollectionNavigation_InPredicate_ShouldThrowNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Artists.Where(a => a.Tracks.Count > 0).ToNextOrm(db);

        act.Should().Throw<NotSupportedException>().WithMessage("*member access*");
    }

    [Fact]
    public void UserStructMember_InPredicate_ShouldPassGuard()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Artists.Where(a => a.Rating.Score > 0).ToNextOrm(db);

        act.Should().NotThrow<NotSupportedException>();
    }

    [Fact]
    public void NullableUserStructMember_InPredicate_ShouldPassGuard()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);

        var act = () => db.Artists.Where(a => a.OptionalRating!.Value.Score > 0).ToNextOrm(db);

        act.Should().NotThrow<NotSupportedException>();
    }

    [Fact]
    public void NullableHasValue_InPredicate_ShouldPassGuard()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();

        var act = () => db.Tracks.Where(x => x.ArtistId.HasValue).ToNextOrm(db);

        act.Should().NotThrow<NotSupportedException>();
    }

    [Fact]
    public async Task NullableValue_InPredicate_ShouldMatchEfQuery()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);
        db.Database.ExecuteSqlRaw("insert into track_artist (artist_id, artist_name, artist_rating) values (7, 'alpha', 0)");
        db.Database.ExecuteSqlRaw("update track_entity set artist_id = 7 where track_plays >= 30");

        var expected = await db.Tracks.Where(x => x.ArtistId!.Value > 0).OrderBy(x => x.Id)
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.Where(x => x.ArtistId!.Value > 0).ToNextOrm(db).OrderBy(x => x.Id).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
        actual.Should().HaveCount(3);
    }

    [Fact]
    public async Task SystemMemberChain_InOrderingKey_ShouldMatchEfQuery()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var expected = await db.Tracks.OrderBy(x => x.Name.Length).ThenBy(x => x.Id)
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.OrderBy(x => x.Name.Length).ThenBy(x => x.Id).ToNextOrm(db).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
        actual.Should().HaveCount(5);
    }

    [Fact]
    public async Task ClosureMember_InPredicate_ShouldMatchEfQuery()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        await SeedAsync(db);

        var marker = new Threshold { Value = 30 };

        var expected = await db.Tracks.Where(x => x.Plays >= marker.Value).OrderBy(x => x.Id)
            .Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

        var actual = db.Tracks.Where(x => x.Plays >= marker.Value).ToNextOrm(db).OrderBy(x => x.Id).ToList()
            .Select(x => x.Id).ToList();

        actual.Should().Equal(expected);
        actual.Should().HaveCount(3);
    }

    private static IQueryable<T> FakeWhere<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) => source;

    private sealed class Threshold
    {
        public int Value { get; set; }
    }

    private static MusicContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MusicContext>()
            .UseSqlite(connection)
            .Options;

        return new MusicContext(options);
    }

    private static async Task SeedAsync(MusicContext db)
    {
        db.Tracks.AddRange(
            new TrackRow { Name = "alpha", Plays = 50 },
            new TrackRow { Name = "beta", Plays = 10 },
            new TrackRow { Name = "alpha", Plays = 30 },
            new TrackRow { Name = "gamma", Plays = 40 },
            new TrackRow { Name = "beta", Plays = 20 });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class StubQueryable<T>(System.Linq.Expressions.Expression expression) : IQueryable<T>
    {
        public Type ElementType => typeof(T);

        public System.Linq.Expressions.Expression Expression { get; } = expression;

        public IQueryProvider Provider => throw new NotSupportedException();

        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class StubOrderedQueryable<T> : IOrderedQueryable<T>
    {
        public Type ElementType => typeof(T);

        public System.Linq.Expressions.Expression Expression => throw new NotSupportedException();

        public IQueryProvider Provider => throw new NotSupportedException();

        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class MusicContext(DbContextOptions<MusicContext> options) : DbContext(options)
    {
        public DbSet<TrackRow> Tracks => Set<TrackRow>();

        public DbSet<ArtistRow> Artists => Set<ArtistRow>();

        public DbSet<DuplicateRow> Duplicates => Set<DuplicateRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DuplicateRow>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("duplicate_entity");
                entity.Property(x => x.Name).HasColumnName("duplicate_name");
                entity.Property(x => x.Plays).HasColumnName("duplicate_plays");
            });

            modelBuilder.Entity<ArtistRow>(entity =>
            {
                entity.ToTable("track_artist");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("artist_id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("artist_name");
                entity.Property(x => x.Rating).HasColumnName("artist_rating")
                    .HasConversion(rating => rating.Score, score => new Rating(score));
            });

            modelBuilder.Entity<TrackRow>(entity =>
            {
                entity.ToTable("track_entity");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("track_id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("track_name");
                entity.Property(x => x.Plays).HasColumnName("track_plays");
                entity.Property(x => x.ArtistId).HasColumnName("artist_id");
                entity.HasOne(x => x.Artist).WithMany(a => a.Tracks).HasForeignKey(x => x.ArtistId);
            });
        }
    }

    private sealed class TrackRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Plays { get; set; }

        public long? ArtistId { get; set; }

        public ArtistRow? Artist { get; set; }
    }

    private sealed class ArtistRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Rating Rating { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Rating? OptionalRating { get; set; }

        public ICollection<TrackRow> Tracks { get; set; } = new List<TrackRow>();
    }

    private readonly struct Rating(int score)
    {
        public int Score { get; } = score;
    }

    private sealed class DuplicateRow
    {
        public string Name { get; set; } = string.Empty;

        public int Plays { get; set; }
    }
}

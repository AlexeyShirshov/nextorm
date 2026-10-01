using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextORM.EntityFrameworkCore;

namespace NextORM.Integration.Tests;

/// <summary>
/// Serializes the lifecycle tests that clear the process-wide <see cref="DataContextCache"/> or change
/// its sliding expiration with every other integration collection, so a global eviction can never race a
/// concurrent test's metadata.
/// </summary>
[CollectionDefinition("EF query filter lifecycle", DisableParallelization = true)]
public sealed class EfQueryFilterLifecycleCollection;

/// <summary>
/// Live-provider lifecycle matrix (D4.7) for the EF Core query-filter bridge. A bridge-bound context
/// that loses its imported filter metadata — through <see cref="DataContextCache.Clear"/> or a
/// deterministic sliding-expiration eviction — must fail closed on the next query (zero SQL, no widened
/// rows) while a kept previously-prepared command never widens; a fresh/rebound context restores the
/// tenant filter, and two owners of different tenants stay isolated across the whole lifecycle.
/// SQLite runs without a container; PostgreSQL, SQL Server and MySQL run against their container.
/// </summary>
[Collection("EF query filter lifecycle")]
public sealed class EfCoreQueryFilterLifecycleTests
{
    private const string SqliteProvider = "SQLite";
    private const string PostgresProvider = "PostgreSQL";
    private const string SqlServerProvider = "SqlServer";
    private const string MySqlProvider = "MySQL";

    private const string LifecycleTable = "ef_qf_lifecycle";

    public static TheoryData<string> LiveProviders => new()
    {
        SqliteProvider,
        PostgresProvider,
        SqlServerProvider,
        MySqlProvider,
    };

    [Theory]
    [MemberData(nameof(LiveProviders))]
    public void Lifecycle_ClearOrEvict_ColdWarmPrepared_Live(string provider)
    {
        SkipUnlessAvailable(provider);
        using var sqlite = provider == SqliteProvider ? new SqliteFixture() : null;
        var options = sqlite is not null ? sqlite.Options : ServerOptions(provider);

        // --- Metadata loss by explicit DataContextCache.Clear() ---
        using (var db = new LifecycleContext(options, tenantId: 1))
        {
            RecreateFilterSchema(db, provider);
            RecreateData(db);

            using var next = db.CreateNextOrmContext();

            AssertColdWarmFiltered(next);

            // A command prepared before the loss; it must never widen afterwards.
            var kept = next.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id);
            kept.ToList().Should().Equal(1, 3);

            DataContextCache.Clear();

            // The guard fires while the command is built, before any DbCommand is opened: zero SQL runs.
            var act = () => next.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
            act.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");

            // A command prepared before the loss must also fail closed with the documented exception;
            // it must never widen to unfiltered rows.
            var keptAct = () => kept.ToList();
            keptAct.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");
        }

        // --- Metadata loss by deterministic sliding-expiration eviction ---
        var previousTtl = DataContextCache.CacheSlidingExpiration;
        // Drop every entry published while the previous arm or the schema/data setup ran, so this arm
        // arms its sliding window before publishing and the eviction is measured from its own entry.
        DataContextCache.Clear();
        DataContextCache.CacheSlidingExpiration = TimeSpan.FromMilliseconds(1000);
        try
        {
            using var db = new LifecycleContext(options, tenantId: 1);
            RecreateFilterSchema(db, provider);
            RecreateData(db);

            // Created while the sliding window is armed, so the published metadata entries are timed.
            using var next = db.CreateNextOrmContext();

            AssertColdWarmFiltered(next);

            var kept = next.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id);
            kept.ToList().Should().Equal(1, 3);

            Thread.Sleep(1500);

            var act = () => next.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
            act.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");

            var keptAfterEviction = ExecuteKeptOrFailClosed(kept);
            if (keptAfterEviction is not null)
                keptAfterEviction.Should().Equal(new long[] { 1, 3 }, "a kept prepared command must keep its tenant filter");
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previousTtl;
            DataContextCache.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(LiveProviders))]
    public void FreshContext_RebindRestoresFilters(string provider)
    {
        SkipUnlessAvailable(provider);
        using var sqlite = provider == SqliteProvider ? new SqliteFixture() : null;
        var options = sqlite is not null ? sqlite.Options : ServerOptions(provider);

        using var db = new LifecycleContext(options, tenantId: 1);
        RecreateFilterSchema(db, provider);
        RecreateData(db);

        using (var poisoned = db.CreateNextOrmContext())
        {
            poisoned.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1, 3);

            DataContextCache.Clear();

            var act = () => poisoned.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
            act.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");
        }

        // Re-create/re-register through the bridge: the metadata is republished and the filter is back.
        using var rebound = db.CreateNextOrmContext();
        rebound.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1, 3);

        DataContextCache.Clear();
    }

    [Theory]
    [MemberData(nameof(LiveProviders))]
    public void Lifecycle_OwnerIsolation(string provider)
    {
        SkipUnlessAvailable(provider);
        using var sqlite = provider == SqliteProvider ? new SqliteFixture() : null;
        var options = sqlite is not null ? sqlite.Options : ServerOptions(provider);

        using var ownerOne = new LifecycleContext(options, tenantId: 1);
        RecreateFilterSchema(ownerOne, provider);
        RecreateData(ownerOne);
        using var ownerTwo = new LifecycleContext(options, tenantId: 2);

        using (var nextOne = ownerOne.CreateNextOrmContext())
        using (var nextTwo = ownerTwo.CreateNextOrmContext())
        {
            nextOne.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(1, 3);
            nextTwo.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(2);

            DataContextCache.Clear();

            var actOne = () => nextOne.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
            var actTwo = () => nextTwo.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
            actOne.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");
            actTwo.Should().Throw<InvalidOperationException>().WithMessage("*unfiltered*");
        }

        // Both owners rebind to their own tenant; no cross-tenant leak across the lifecycle.
        using var reboundOne = ownerOne.CreateNextOrmContext();
        using var reboundTwo = ownerTwo.CreateNextOrmContext();

        var idsOne = reboundOne.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var idsTwo = reboundTwo.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();

        idsOne.Should().Equal(1, 3);
        idsTwo.Should().Equal(2);
        idsOne.Should().NotIntersectWith(idsTwo);

        DataContextCache.Clear();
    }

    // A kept command whose plan is already prepared: it either executes its filtered plan (the plan
    // cache is untouched by a sliding eviction) or fails before returning anything once the metadata is
    // gone. It must never widen; a returned row set, when there is one, stays the tenant's.
    private static IReadOnlyList<long>? ExecuteKeptOrFailClosed(QueryCommand<long> kept)
    {
        try
        {
            return kept.ToList();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void AssertColdWarmFiltered(IDataContext next)
    {
        var cold = next.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var warm = next.From<LifecycleRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();

        cold.Should().Equal(1, 3);
        warm.Should().Equal(1, 3);
    }

    private static void RecreateData(LifecycleContext db)
    {
        db.Rows.IgnoreQueryFilters().ExecuteDelete();
        db.SaveChanges();

        db.Rows.AddRange(
            new LifecycleRow { TenantId = 1, Value = 10 },
            new LifecycleRow { TenantId = 2, Value = 20 },
            new LifecycleRow { TenantId = 1, Value = 30 });
        db.SaveChanges();
    }

    /// <summary>
    /// Creates the lifecycle table with explicit provider DDL, dropping it first, so the suite is
    /// deterministic on the reused server databases (see the bridge suite's helper for the rationale).
    /// </summary>
    private static void RecreateFilterSchema(LifecycleContext db, string provider)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = provider switch
            {
                PostgresProvider =>
                    $"drop table if exists {LifecycleTable}; " +
                    $"create table {LifecycleTable} (id bigint generated by default as identity primary key, tenant_id integer not null, value integer not null);",
                SqlServerProvider =>
                    $"if object_id('{LifecycleTable}','U') is not null drop table {LifecycleTable}; " +
                    $"create table {LifecycleTable} (id bigint identity(1,1) primary key, tenant_id int not null, value int not null);",
                MySqlProvider =>
                    $"drop table if exists {LifecycleTable}; " +
                    $"create table {LifecycleTable} (id bigint not null auto_increment primary key, tenant_id int not null, value int not null);",
                _ =>
                    $"drop table if exists {LifecycleTable}; " +
                    $"create table {LifecycleTable} (id integer not null primary key autoincrement, tenant_id integer not null, value integer not null);",
            };
            command.ExecuteNonQuery();
        }
        finally
        {
            if (openedHere)
                connection.Close();
        }
    }

    private static void SkipUnlessAvailable(string provider)
    {
        if (provider == SqliteProvider)
            return;

        var (available, failure) = provider switch
        {
            PostgresProvider => (PostgresContainer.IsAvailable, PostgresContainer.Failure),
            SqlServerProvider => (SqlServerContainer.IsAvailable, SqlServerContainer.Failure),
            _ => (MySqlContainer.IsAvailable, MySqlContainer.Failure),
        };

        Assert.SkipUnless(available, failure ?? $"{provider} is not available.");
    }

    private static DbContextOptions<LifecycleContext> ServerOptions(string provider)
    {
        var builder = new DbContextOptionsBuilder<LifecycleContext>();
        return provider switch
        {
            PostgresProvider => builder.UseNpgsql(PostgresContainer.ConnectionString).Options,
            SqlServerProvider => builder.UseSqlServer(SqlServerContainer.ConnectionString).Options,
            _ => builder.UseMySQL(MySqlContainer.ConnectionString).Options,
        };
    }

    private sealed class SqliteFixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"nextorm.efqf.lifecycle.{Guid.NewGuid():N}.db");

        public SqliteFixture()
            => Options = new DbContextOptionsBuilder<LifecycleContext>()
                .UseSqlite($"Data Source={_path}")
                .Options;

        public DbContextOptions<LifecycleContext> Options { get; }

        public void Dispose()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
    }

    private sealed class LifecycleRow
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public int Value { get; set; }
    }

    private sealed class LifecycleContext(DbContextOptions<LifecycleContext> options, int tenantId) : DbContext(options)
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<LifecycleRow> Rows => Set<LifecycleRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LifecycleRow>(entity =>
            {
                entity.ToTable(LifecycleTable);
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextORM.EntityFrameworkCore;

namespace NextORM.Integration.Tests;

/// <summary>
/// EF Core 10 query-filter bridge against every relational adapter: EF's own filtered query and the
/// nextorm query over the same EF model must return the same rows for a named and an anonymous filter,
/// two owners of one model with different tenants must see their own rows, a warmed (cached) execution
/// must re-read the changed owner value, a named ignore must preserve the anonymous filter, and the
/// bridge context must still share EF's transaction. SQLite runs without a container; PostgreSQL,
/// SQL Server and MySQL run when their container (or connection string) is available and explicitly
/// skip otherwise.
/// </summary>
public sealed class EfCoreQueryFilterBridgeTests
{
    private const string SqliteProvider = "SQLite";
    private const string PostgresProvider = "PostgreSQL";
    private const string SqlServerProvider = "SqlServer";
    private const string MySqlProvider = "MySQL";

    private const string NamedTable = "ef_qf_named";
    private const string AnonymousTable = "ef_qf_anon";

    public static TheoryData<string> ServerProviders => new() { PostgresProvider, SqlServerProvider, MySqlProvider };

    [Fact]
    public void Sqlite_EfAndNextOrm_AgreeOnNamedAndAnonymousFilters()
    {
        using var fixture = new SqliteFixture();
        AssertNamedAndAnonymousAgree(fixture.Options, SqliteProvider);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public void Server_EfAndNextOrm_AgreeOnNamedAndAnonymousFilters(string provider)
    {
        SkipUnlessAvailable(provider);
        AssertNamedAndAnonymousAgree(ServerOptions(provider), provider);
    }

    [Fact]
    public void Sqlite_TwoOwners_DifferentTenants_ReturnDifferentRows()
    {
        using var fixture = new SqliteFixture();
        AssertTwoOwnersSeeTheirOwnRows(fixture.Options, SqliteProvider);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public void Server_TwoOwners_DifferentTenants_ReturnDifferentRows(string provider)
    {
        SkipUnlessAvailable(provider);
        AssertTwoOwnersSeeTheirOwnRows(ServerOptions(provider), provider);
    }

    [Fact]
    public void Sqlite_WarmedExecution_RereadsChangedTenant()
    {
        using var fixture = new SqliteFixture();
        AssertWarmedExecutionRereads(fixture.Options, SqliteProvider);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public void Server_WarmedExecution_RereadsChangedTenant(string provider)
    {
        SkipUnlessAvailable(provider);
        AssertWarmedExecutionRereads(ServerOptions(provider), provider);
    }

    [Fact]
    public void Sqlite_NamedIgnore_PreservesAnonymousFilter()
    {
        using var fixture = new SqliteFixture();
        AssertNamedIgnorePreservesAnonymous(fixture.Options, SqliteProvider);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public void Server_NamedIgnore_PreservesAnonymousFilter(string provider)
    {
        SkipUnlessAvailable(provider);
        AssertNamedIgnorePreservesAnonymous(ServerOptions(provider), provider);
    }

    [Fact]
    public async Task Sqlite_BridgeContext_SeesUncommittedRow_InsideEfTransaction()
    {
        using var fixture = new SqliteFixture();
        await AssertTransactionStillWorks(fixture.Options, SqliteProvider);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public async Task Server_BridgeContext_SeesUncommittedRow_InsideEfTransaction(string provider)
    {
        SkipUnlessAvailable(provider);
        await AssertTransactionStillWorks(ServerOptions(provider), provider);
    }

    private static void AssertNamedAndAnonymousAgree(DbContextOptions<EfQueryFilterContext> options, string provider)
    {
        using var db = new EfQueryFilterContext(options, tenantId: 1);
        RecreateFilterSchema(db, provider);
        RecreateData(db);

        using var next = db.CreateNextOrmContext();

        // A direct From<T>() on the bridge context carries the imported named filter.
        var efNamed = db.NamedRows.OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var nextNamed = next.From<NamedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        efNamed.Should().NotBeEmpty();
        nextNamed.Should().Equal(efNamed);
        db.NamedRows.Count().Should().Be(efNamed.Count);

        // The anonymous filter is imported too; the soft-deleted row is hidden from both.
        var efAnonymous = db.AnonymousRows.OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var nextAnonymous = next.From<AnonymousRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        efAnonymous.Should().HaveCount(1);
        nextAnonymous.Should().Equal(efAnonymous);
    }

    private static void AssertTwoOwnersSeeTheirOwnRows(DbContextOptions<EfQueryFilterContext> options, string provider)
    {
        using var tenantOne = new EfQueryFilterContext(options, tenantId: 1);
        RecreateFilterSchema(tenantOne, provider);
        RecreateData(tenantOne);

        using var tenantTwo = new EfQueryFilterContext(options, tenantId: 2);

        using var nextOne = tenantOne.CreateNextOrmContext();
        using var nextTwo = tenantTwo.CreateNextOrmContext();

        var expectedOne = tenantOne.NamedRows.OrderBy(row => row.Id).Select(row => row.Id).ToList();
        var expectedTwo = tenantTwo.NamedRows.OrderBy(row => row.Id).Select(row => row.Id).ToList();

        expectedOne.Should().HaveCount(2);
        expectedTwo.Should().HaveCount(1);
        expectedOne.Should().NotIntersectWith(expectedTwo);

        nextOne.From<NamedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(expectedOne);
        nextTwo.From<NamedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList().Should().Equal(expectedTwo);
    }

    private static void AssertWarmedExecutionRereads(DbContextOptions<EfQueryFilterContext> options, string provider)
    {
        using var db = new EfQueryFilterContext(options, tenantId: 1);
        RecreateFilterSchema(db, provider);
        RecreateData(db);

        using var next = db.CreateNextOrmContext();

        var tenantOneIds = next.From<NamedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();
        tenantOneIds.Should().HaveCount(2);

        // The plan is now cached with the shape; the filter must re-read the live owner on the next run.
        db.TenantId = 2;
        var tenantTwoIds = next.From<NamedRow>().OrderBy(row => row.Id).Select(row => row.Id).ToList();

        var expectedTwo = db.NamedRows.OrderBy(row => row.Id).Select(row => row.Id).ToList();
        tenantTwoIds.Should().Equal(expectedTwo);
        tenantTwoIds.Should().NotEqual(tenantOneIds);
    }

    private static void AssertNamedIgnorePreservesAnonymous(DbContextOptions<EfQueryFilterContext> options, string provider)
    {
        using var db = new EfQueryFilterContext(options, tenantId: 1);
        RecreateFilterSchema(db, provider);
        RecreateData(db);

        using var next = db.CreateNextOrmContext();

        // Ignoring the named key removes the tenant filter from the named entity...
        next.From<NamedRow>().IgnoreFilters(["tenant"]).OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().HaveCount(3, "the named tenant filter is disabled");

        // ...but the anonymous soft-delete filter stays active on the anonymous entity.
        next.From<AnonymousRow>().IgnoreFilters(["tenant"]).OrderBy(row => row.Id).Select(row => row.Id).ToList()
            .Should().HaveCount(1, "a named ignore cannot select the anonymous filter");
    }

    private static async Task AssertTransactionStillWorks(DbContextOptions<EfQueryFilterContext> options, string provider)
    {
        using var db = new EfQueryFilterContext(options, tenantId: 1);
        RecreateFilterSchema(db, provider);
        db.NamedRows.IgnoreQueryFilters().ExecuteDelete();

        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = Random.Shared.Next(1_000_000, 9_000_000);
        db.NamedRows.Add(new NamedRow { TenantId = 1, Value = marker });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var next = db.CreateNextOrmContext();
        next.From<NamedRow>().Where(row => row.Value == marker).Select(row => row.Id).ToList().Should().ContainSingle();

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        next.From<NamedRow>().Where(row => row.Value == marker).Select(row => row.Id).ToList().Should().BeEmpty();
    }

    private static void RecreateData(EfQueryFilterContext db)
    {
        db.NamedRows.IgnoreQueryFilters().ExecuteDelete();
        db.AnonymousRows.IgnoreQueryFilters().ExecuteDelete();
        db.SaveChanges();

        db.NamedRows.AddRange(
            new NamedRow { TenantId = 1, Value = 10 },
            new NamedRow { TenantId = 2, Value = 20 },
            new NamedRow { TenantId = 1, Value = 30 });
        db.AnonymousRows.AddRange(
            new AnonymousRow { IsDeleted = false, Value = 1 },
            new AnonymousRow { IsDeleted = true, Value = 2 });
        db.SaveChanges();
    }

    /// <summary>
    /// Creates the two bridge tables with explicit provider DDL, dropping them first. The server
    /// databases are reused between runs, so <c>EnsureCreated</c> is a no-op once any other table
    /// exists and the bridge tables would never be created; this makes the schema deterministic and
    /// the suite idempotent on a dirty shared database.
    /// </summary>
    private static void RecreateFilterSchema(EfQueryFilterContext db, string provider)
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
                    $"drop table if exists {NamedTable}; " +
                    $"drop table if exists {AnonymousTable}; " +
                    $"create table {NamedTable} (id bigint generated by default as identity primary key, tenant_id integer not null, value integer not null); " +
                    $"create table {AnonymousTable} (id bigint generated by default as identity primary key, is_deleted boolean not null, value integer not null);",
                SqlServerProvider =>
                    $"if object_id('{NamedTable}','U') is not null drop table {NamedTable}; " +
                    $"if object_id('{AnonymousTable}','U') is not null drop table {AnonymousTable}; " +
                    $"create table {NamedTable} (id bigint identity(1,1) primary key, tenant_id int not null, value int not null); " +
                    $"create table {AnonymousTable} (id bigint identity(1,1) primary key, is_deleted bit not null, value int not null);",
                MySqlProvider =>
                    $"drop table if exists {NamedTable}; " +
                    $"drop table if exists {AnonymousTable}; " +
                    $"create table {NamedTable} (id bigint not null auto_increment primary key, tenant_id int not null, value int not null); " +
                    $"create table {AnonymousTable} (id bigint not null auto_increment primary key, is_deleted tinyint(1) not null, value int not null);",
                // SQLite: a fresh temp file per run, but drop-and-recreate keeps the contract uniform.
                _ =>
                    $"drop table if exists {NamedTable}; " +
                    $"drop table if exists {AnonymousTable}; " +
                    $"create table {NamedTable} (id integer not null primary key autoincrement, tenant_id integer not null, value integer not null); " +
                    $"create table {AnonymousTable} (id integer not null primary key autoincrement, is_deleted integer not null, value integer not null);",
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
        var (available, failure) = provider switch
        {
            PostgresProvider => (PostgresContainer.IsAvailable, PostgresContainer.Failure),
            SqlServerProvider => (SqlServerContainer.IsAvailable, SqlServerContainer.Failure),
            _ => (MySqlContainer.IsAvailable, MySqlContainer.Failure),
        };

        Assert.SkipUnless(available, failure ?? $"{provider} is not available.");
    }

    private static DbContextOptions<EfQueryFilterContext> ServerOptions(string provider)
    {
        var builder = new DbContextOptionsBuilder<EfQueryFilterContext>();
        return provider switch
        {
            PostgresProvider => builder.UseNpgsql(PostgresContainer.ConnectionString).Options,
            SqlServerProvider => builder.UseSqlServer(SqlServerContainer.ConnectionString).Options,
            _ => builder.UseMySQL(MySqlContainer.ConnectionString).Options,
        };
    }

    private sealed class SqliteFixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"nextorm.efqf.{Guid.NewGuid():N}.db");

        public SqliteFixture()
            => Options = new DbContextOptionsBuilder<EfQueryFilterContext>()
                .UseSqlite($"Data Source={_path}")
                .Options;

        public DbContextOptions<EfQueryFilterContext> Options { get; }

        public void Dispose()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
    }

    private sealed class NamedRow
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public int Value { get; set; }
    }

    private sealed class AnonymousRow
    {
        public long Id { get; set; }
        public bool IsDeleted { get; set; }
        public int Value { get; set; }
    }

    private sealed class EfQueryFilterContext(DbContextOptions<EfQueryFilterContext> options, int tenantId) : DbContext(options)
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<NamedRow> NamedRows => Set<NamedRow>();

        public DbSet<AnonymousRow> AnonymousRows => Set<AnonymousRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NamedRow>(entity =>
            {
                entity.ToTable("ef_qf_named");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });

            modelBuilder.Entity<AnonymousRow>(entity =>
            {
                entity.ToTable("ef_qf_anon");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(row => row.IsDeleted).HasColumnName("is_deleted");
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter(row => !row.IsDeleted);
            });
        }
    }
}

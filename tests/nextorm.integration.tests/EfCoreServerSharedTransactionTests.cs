using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NextORM.Core;
using NextORM.EntityFrameworkCore;

namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the EF Core shared-connection/shared-transaction contract on the server providers, through
/// the public bridge <c>db.CreateNextOrmContext()</c> only. Each case is parameterised by provider and
/// explicitly skips (never passes silently) when its container is not configured or cannot start.
/// The schema is created before <c>BeginTransaction</c> because MySQL-style providers auto-commit DDL;
/// the table is dedicated to this suite so it cannot race the shared fixtures.
/// </summary>
public sealed class EfCoreServerSharedTransactionTests
{
    public static TheoryData<string> ServerProviders => new() { PostgresProvider, SqlServerProvider };

    private const string PostgresProvider = "PostgreSQL";
    private const string SqlServerProvider = "SqlServer";

    private const string TableName = "ef_shared_tx";

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public async Task NextOrmContext_EnlistsInEfTransaction_SeesUncommittedRows(string provider)
    {
        SkipUnlessAvailable(provider);

        await using var db = CreateContext(provider);
        RecreateSchema(db, provider);

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = Marker();
        db.Rows.Add(new EfServerRow { Name = marker, Age = 5 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The discriminator: read before EF commits.
        using var next = db.CreateNextOrmContext();

        // Direct identity assertion: Npgsql/SqlClient would let nextorm read EF's uncommitted rows even
        // if the bridge never enlisted, because the connection's current transaction is ambient. The
        // enlisted transaction must be EF's very DbTransaction, not a nextorm-started one.
        var efDbTransaction = db.Database.CurrentTransaction!.GetDbTransaction();
        ((ITransactionManager)next).CurrentTransaction.Should().BeSameAs(efDbTransaction);

        next.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(5);

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public async Task EfWrite_ThenCommit_NextOrmSeesPersisted(string provider)
    {
        SkipUnlessAvailable(provider);

        await using var db = CreateContext(provider);
        RecreateSchema(db, provider);

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = Marker();
        db.Rows.Add(new EfServerRow { Name = marker, Age = 23 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // EF's own uncommitted row is visible to nextorm before EF commits.
        using (var next = db.CreateNextOrmContext())
        {
            next.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(23);
        }

        await efTransaction.CommitAsync(TestContext.Current.CancellationToken);

        // The commit is EF's; a fresh nextorm context sees the row retained.
        using var after = db.CreateNextOrmContext();
        after.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(23);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public void NextOrmContext_BorrowsEfDbConnection(string provider)
    {
        SkipUnlessAvailable(provider);

        using var db = CreateContext(provider);
        RecreateSchema(db, provider);

        using var next = db.CreateNextOrmContext();

        // No second connection is created: nextorm runs on the very DbConnection EF owns.
        var nextormConnection = ((DataContext)next).GetConnection();
        ReferenceEquals(db.Database.GetDbConnection(), nextormConnection).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public async Task EfRollback_DiscardsRowsForNextOrmContext(string provider)
    {
        SkipUnlessAvailable(provider);

        await using var db = CreateContext(provider);
        RecreateSchema(db, provider);

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = Marker();
        db.Rows.Add(new EfServerRow { Name = marker, Age = 7 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var next = db.CreateNextOrmContext();
        next.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(7);

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);

        // The rollback is EF's; nextorm drops the completed transaction lazily and sees nothing.
        next.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public async Task NextOrmInsert_InsideEfTransaction_VisibleToEf_ThenCommits(string provider)
    {
        SkipUnlessAvailable(provider);

        await using var db = CreateContext(provider);
        RecreateSchema(db, provider);

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = Marker();
        using (var next = db.CreateNextOrmContext())
        {
            next.InsertInto<EfServerRow>()
                .Value(x => x.Name, marker)
                .Value(x => x.Age, 11)
                .Insert();
        }

        // nextorm wrote inside EF's transaction, so EF sees the row.
        var seenByEf = await db.Rows.AsNoTracking().Where(x => x.Name == marker).Select(x => x.Age).SingleAsync(TestContext.Current.CancellationToken);
        seenByEf.Should().Be(11);

        await efTransaction.CommitAsync(TestContext.Current.CancellationToken);

        using var after = db.CreateNextOrmContext();
        after.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(11);
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public async Task NextOrmInsert_InsideEfTransaction_VisibleToEf_ThenRollsBack(string provider)
    {
        SkipUnlessAvailable(provider);

        await using var db = CreateContext(provider);
        RecreateSchema(db, provider);

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = Marker();
        using (var next = db.CreateNextOrmContext())
        {
            next.InsertInto<EfServerRow>()
                .Value(x => x.Name, marker)
                .Value(x => x.Age, 13)
                .Insert();
        }

        var seenByEf = await db.Rows.AsNoTracking().Where(x => x.Name == marker).Select(x => x.Age).SingleAsync(TestContext.Current.CancellationToken);
        seenByEf.Should().Be(13);

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);

        using var after = db.CreateNextOrmContext();
        after.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ServerProviders))]
    public async Task DisposeNextOrmContext_LeavesEfConnectionAndTransactionAlive(string provider)
    {
        SkipUnlessAvailable(provider);

        await using var db = CreateContext(provider);
        RecreateSchema(db, provider);

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var first = Marker();
        db.Rows.Add(new EfServerRow { Name = first, Age = 17 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using (var next = db.CreateNextOrmContext())
        {
            next.From<EfServerRow>().Where(x => x.Name == first).Select(x => x.Age).ToList().Should().Equal(17);
        }

        // Disposing the nextorm context leaves EF's connection open and EF's transaction active.
        db.Database.GetDbConnection().State.Should().Be(ConnectionState.Open);
        db.Database.CurrentTransaction.Should().NotBeNull();

        var second = Marker();
        db.Rows.Add(new EfServerRow { Name = second, Age = 19 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using (var next = db.CreateNextOrmContext())
        {
            next.From<EfServerRow>().Where(x => x.Name == second).Select(x => x.Age).ToList().Should().Equal(19);
        }

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Converts an accidentally all-skipped acceptance run into a failure: when the run is pointed at a
    /// container runtime (<c>DOCKER_HOST</c>) or explicitly opted in
    /// (<c>NEXTORM_REQUIRE_EF_CONTAINERS=1</c>), both server providers must actually be available and
    /// therefore executed. Without this guard the per-case <see cref="Assert.SkipUnless(bool, string)"/>
    /// calls make a skipped run indistinguishable from a green one.
    /// </summary>
    [Fact]
    public void EfCoreServerSharedTransaction_RequiredProvidersMustExecute()
    {
        if (!AcceptanceRunExpected())
            Assert.Skip("Set DOCKER_HOST or NEXTORM_REQUIRE_EF_CONTAINERS=1 to require the PostgreSQL and SQL Server containers.");

        PostgresContainer.IsAvailable.Should().BeTrue(
            PostgresContainer.Failure ?? "PostgreSQL is not available; the EF shared-transaction cases would all skip.");
        SqlServerContainer.IsAvailable.Should().BeTrue(
            SqlServerContainer.Failure ?? "SQL Server is not available; the EF shared-transaction cases would all skip.");
    }

    private static bool AcceptanceRunExpected() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"))
        || Environment.GetEnvironmentVariable("NEXTORM_REQUIRE_EF_CONTAINERS") == "1";

    private static string Marker() => "efs_" + Guid.NewGuid().ToString("N");

    private static void SkipUnlessAvailable(string provider)
    {
        if (provider == PostgresProvider)
            Assert.SkipUnless(PostgresContainer.IsAvailable, PostgresContainer.Failure ?? "PostgreSQL is not available.");
        else
            Assert.SkipUnless(SqlServerContainer.IsAvailable, SqlServerContainer.Failure ?? "SQL Server is not available.");
    }

    private static EfServerContext CreateContext(string provider)
    {
        var options = provider == PostgresProvider
            ? new DbContextOptionsBuilder<EfServerContext>().UseNpgsql(PostgresContainer.ConnectionString).Options
            : new DbContextOptionsBuilder<EfServerContext>().UseSqlServer(SqlServerContainer.ConnectionString).Options;

        return new EfServerContext(options);
    }

    private static void RecreateSchema(EfServerContext db, string provider)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = provider == PostgresProvider
                ? $"drop table if exists {TableName}; " +
                  $"create table {TableName} (id bigint generated by default as identity primary key, name varchar(100), age integer);"
                : $"if object_id('{TableName}','U') is not null drop table {TableName}; " +
                  $"create table {TableName} (id bigint identity(1,1) primary key, name nvarchar(100), age int);";
            command.ExecuteNonQuery();
        }
        finally
        {
            if (openedHere)
                connection.Close();
        }
    }

    private sealed class EfServerContext(DbContextOptions<EfServerContext> options) : DbContext(options)
    {
        public DbSet<EfServerRow> Rows => Set<EfServerRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EfServerRow>(entity =>
            {
                entity.ToTable(TableName);
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(x => x.Name).HasColumnName("name");
                entity.Property(x => x.Age).HasColumnName("age");
            });
        }
    }

    private sealed class EfServerRow
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
    }
}

using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using NextORM.Core;
using NextORM.EntityFrameworkCore;

namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the EF Core shared-connection/shared-transaction contract on the server providers, through
/// the public bridge <c>db.CreateNextOrmContext()</c> only. The shared transaction cases are
/// parameterised by provider; driver-specific cases (for example the Oracle temp-table batch fact)
/// are provider-specific <c>[Fact]</c>s and run only on their own provider. Every case explicitly
/// skips (never passes silently) when its container is not configured or cannot start.
/// The schema is created before <c>BeginTransaction</c> because MySQL-style providers auto-commit DDL;
/// the table is dedicated to this suite so it cannot race the shared fixtures.
/// </summary>
[Collection("EF query filter lifecycle")]
public sealed class EfCoreServerSharedTransactionTests
{
    public static TheoryData<string> ServerProviders => new() { PostgresProvider, SqlServerProvider, MySqlProvider };

    private const string PostgresProvider = "PostgreSQL";
    private const string SqlServerProvider = "SqlServer";
    private const string MySqlProvider = "MySQL";

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
    /// Exercises the command-aware parameter path against the Oracle EF Core provider's driver: the
    /// borrowed connection is a <c>MySql.Data.MySqlClient.MySqlConnection</c>, so every nextorm parameter
    /// must be minted by its own command (a <c>MySqlConnector</c> parameter is rejected by MySql.Data's
    /// parameter collection). This is the MySQL-only regression test for the D2 core change.
    /// </summary>
    [Fact]
    public async Task ExecutesParameterizedQueryWithOracleDriver()
    {
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        await using var db = CreateContext(MySqlProvider);
        RecreateSchema(db, MySqlProvider);

        // The bridge borrowed the Oracle driver's connection, not MySqlConnector's.
        db.Database.GetDbConnection().Should().BeOfType<MySqlConnection>();

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var marker = Marker();
        db.Rows.Add(new EfServerRow { Name = marker, Age = 29 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var next = db.CreateNextOrmContext();

        // Parameterised read against the uncommitted EF row.
        next.From<EfServerRow>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(29);

        // Parameterised write; its parameter is minted by the Oracle command.
        var inserted = "efs_p_" + Guid.NewGuid().ToString("N");
        next.InsertInto<EfServerRow>().Value(x => x.Name, inserted).Value(x => x.Age, 31).Insert();
        next.From<EfServerRow>().Where(x => x.Name == inserted).Select(x => x.Age).ToList().Should().Equal(31);

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The raw/stored-procedure counterpart of <see cref="ExecutesParameterizedQueryWithOracleDriver"/>:
    /// a parameterized <c>ExecuteRaw</c> and an <c>ExecuteProcedure</c> call must also bind through the
    /// Oracle <c>MySql.Data</c> command. Those paths mint parameters through the provider's procedure
    /// hook rather than the mutation path, so this is the regression test for making the procedure/raw
    /// path command-aware. The table and the procedure are created before <c>BeginTransaction</c>
    /// because MySQL DDL auto-commits (the procedure name is dropped and recreated per run).
    /// </summary>
    [Fact]
    public async Task ExecutesParameterizedRawAndProcedureWithOracleDriver()
    {
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        await using var db = CreateContext(MySqlProvider);
        RecreateSchema(db, MySqlProvider);

        var procedure = "efs_p_" + Guid.NewGuid().ToString("N")[..20];
        RecreateProcedure(db, procedure);

        // The bridge borrowed the Oracle driver's connection, not MySqlConnector's.
        db.Database.GetDbConnection().Should().BeOfType<MySqlConnection>();

        await using var efTransaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        using var next = db.CreateNextOrmContext();

        // Raw text command with a bound parameter, executed over the Oracle command inside EF's transaction.
        var value = Random.Shared.Next(1, 1_000_000);
        using (var raw = next.ExecuteRaw("select @v + 1 as value", [new ProcedureParameter("v", value)]))
            raw.Read<int>().Should().Equal(value + 1);

        // Stored procedure with an input parameter and a result set, on the same command/transaction.
        using (var result = next.ExecuteProcedure(procedure, [new ProcedureParameter("p_id", 7, DbType: DbType.Int32)]))
            result.Read<int>().Should().Equal(7);

        await efTransaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Oracle's <c>MySql.Data</c> driver cannot create a <c>DbBatch</c>, so a temp-table batch on the
    /// borrowed EF connection must take the <c>;</c>-joined fallback and execute on that driver. This is
    /// the batch-side counterpart of <see cref="ExecutesParameterizedQueryWithOracleDriver"/> and is
    /// gated on the MySQL container with an explicit skip. The batch is deliberately parameterized: the
    /// captures in both statements are minted by the joined fallback's own command through the
    /// command-aware factory, which is exactly the path a 2-arg <c>MySqlConnector</c> factory would have
    /// broken (its parameter is rejected by <c>MySql.Data</c>'s parameter collection).
    /// </summary>
    [Fact]
    public void OracleDriver_TempTableBatch_FallsBackToJoinedCommand()
    {
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        using var db = CreateContext(MySqlProvider);
        RecreateSchema(db, MySqlProvider);

        var marker = Marker();
        var entity = new EfServerRow { Name = marker, Age = 37 };
        db.Rows.Add(entity);
        db.SaveChanges();

        using var next = db.CreateNextOrmContext();
        var connection = ((DataContext)next).GetConnection();

        // MySql.Data exposes no DbBatch: the batch path must go through the joined-command fallback.
        Assert.False(connection.CanCreateBatch, "batch path requires command-aware minting");

        var name = "efs_batch_" + Guid.NewGuid().ToString("N")[..20];
        var rows = next.Batch()
            .CreateTempTable(name, next.From("ef_shared_tx")
                .Where(t => t["name"].AsString == marker)
                .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") }))
            .Query(next.From(name)
                .Where(t => t["id"].AsInt == (int)entity.Id)
                .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") }))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be((int)entity.Id);
        rows[0].Name.Should().Be(marker);
    }

    /// <summary>
    /// Converts an accidentally all-skipped acceptance run into a failure: when the run is pointed at a
    /// container runtime (<c>DOCKER_HOST</c>) or explicitly opted in
    /// (<c>NEXTORM_REQUIRE_EF_CONTAINERS=1</c>), all three server providers must actually be available
    /// and therefore executed. Without this guard the per-case
    /// <see cref="Assert.SkipUnless(bool, string)"/> calls make a skipped run indistinguishable from a
    /// green one.
    /// </summary>
    [Fact]
    public void EfCoreServerSharedTransaction_RequiredProvidersMustExecute()
    {
        if (!AcceptanceRunExpected())
            Assert.Skip("Set DOCKER_HOST or NEXTORM_REQUIRE_EF_CONTAINERS=1 to require the PostgreSQL, SQL Server and MySQL containers.");

        PostgresContainer.IsAvailable.Should().BeTrue(
            PostgresContainer.Failure ?? "PostgreSQL is not available; the EF shared-transaction cases would all skip.");
        SqlServerContainer.IsAvailable.Should().BeTrue(
            SqlServerContainer.Failure ?? "SQL Server is not available; the EF shared-transaction cases would all skip.");
        MySqlContainer.IsAvailable.Should().BeTrue(
            MySqlContainer.Failure ?? "MySQL is not available; the EF shared-transaction cases would all skip.");
    }

    private static bool AcceptanceRunExpected() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"))
        || Environment.GetEnvironmentVariable("NEXTORM_REQUIRE_EF_CONTAINERS") == "1";

    private static string Marker() => "efs_" + Guid.NewGuid().ToString("N");

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

    private static EfServerContext CreateContext(string provider)
    {
        // Oracle's provider (MySql.EntityFrameworkCore) is deliberately used for MySQL rather than
        // Pomelo: its MySql.Data driver rejects MySqlConnector parameters, so it is the strictest
        // exercise of the command-aware parameter path.
        var options = provider switch
        {
            PostgresProvider => new DbContextOptionsBuilder<EfServerContext>()
                .UseNpgsql(PostgresContainer.ConnectionString).Options,
            SqlServerProvider => new DbContextOptionsBuilder<EfServerContext>()
                .UseSqlServer(SqlServerContainer.ConnectionString).Options,
            _ => new DbContextOptionsBuilder<EfServerContext>()
                .UseMySQL(MySqlContainer.ConnectionString).Options,
        };

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
            command.CommandText = provider switch
            {
                PostgresProvider =>
                    $"drop table if exists {TableName}; " +
                    $"create table {TableName} (id bigint generated by default as identity primary key, name varchar(100), age integer);",
                SqlServerProvider =>
                    $"if object_id('{TableName}','U') is not null drop table {TableName}; " +
                    $"create table {TableName} (id bigint identity(1,1) primary key, name nvarchar(100), age int);",
                // MySQL/MariaDB auto-commit DDL, so the caller creates the schema before BeginTransaction.
                _ =>
                    $"drop table if exists {TableName}; " +
                    $"create table {TableName} (id bigint not null auto_increment primary key, name varchar(100), age int);",
            };
            command.ExecuteNonQuery();
        }
        finally
        {
            if (openedHere)
                connection.Close();
        }
    }

    /// <summary>
    /// Creates the MySQL stored procedure used by
    /// <see cref="ExecutesParameterizedRawAndProcedureWithOracleDriver"/> before <c>BeginTransaction</c>:
    /// MySQL DDL auto-commits, so it cannot be created inside the EF transaction. Two commands are used
    /// because the suite must not rely on multi-statement batches.
    /// </summary>
    private static void RecreateProcedure(EfServerContext db, string procedure)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            connection.Open();

        try
        {
            using (var drop = connection.CreateCommand())
            {
                drop.CommandText = $"drop procedure if exists {procedure}";
                drop.ExecuteNonQuery();
            }

            using var create = connection.CreateCommand();
            create.CommandText = $"create procedure {procedure}(in p_id int) begin select p_id as value; end";
            create.ExecuteNonQuery();
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

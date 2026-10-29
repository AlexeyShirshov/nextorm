using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// Savepoints through the <see cref="ITransactionManager"/> role: the real role → <see cref="DataContext"/>
/// → <c>DbConnectionManager</c> → <see cref="DbTransaction"/> wiring, exercised against a recording fake
/// connection and transaction so every operation, form, guard and name boundary is observable without a
/// database. The capability delegate is supplied exactly as a provider does it, through
/// <see cref="ISqlDialect"/>.
/// </summary>
public class SavepointTests
{
    // ------------------------------------------------------------------------------------------------
    // Delegation: all three operations, sync and async.
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("create", "Save")]
    [InlineData("rollback", "RollbackTo")]
    [InlineData("release", "Release")]
    public void SavepointOperation_Sync_DelegatesToTheActiveTransaction(string operation, string expectedDriverCall)
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        var transaction = ctx.Transactions.BeginTransaction();

        switch (operation)
        {
            case "create":
                ctx.Transactions.CreateSavepoint("sp_1");
                break;
            case "rollback":
                ctx.Transactions.RollbackToSavepoint("sp_1");
                break;
            default:
                ctx.Transactions.ReleaseSavepoint("sp_1");
                break;
        }

        ctx.Transactions.CurrentTransaction.Should().BeSameAs(transaction);
        ctx.RecordedTransaction.Operations.Should().Equal(expectedDriverCall);
        ctx.RecordedTransaction.Names.Should().Equal("sp_1");
        ctx.RecordedTransaction.Disposed.Should().BeFalse("a savepoint never ends the surrounding transaction");
    }

    [Theory]
    [InlineData("create", "SaveAsync")]
    [InlineData("rollback", "RollbackToAsync")]
    [InlineData("release", "ReleaseAsync")]
    public async Task SavepointOperation_Async_DelegatesToTheActiveTransaction(string operation, string expectedDriverCall)
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        var transaction = ctx.Transactions.BeginTransaction();

        switch (operation)
        {
            case "create":
                await ctx.Transactions.CreateSavepointAsync("sp_1", TestContext.Current.CancellationToken);
                break;
            case "rollback":
                await ctx.Transactions.RollbackToSavepointAsync("sp_1", TestContext.Current.CancellationToken);
                break;
            default:
                await ctx.Transactions.ReleaseSavepointAsync("sp_1", TestContext.Current.CancellationToken);
                break;
        }

        ctx.Transactions.CurrentTransaction.Should().BeSameAs(transaction);
        ctx.RecordedTransaction.Operations.Should().Equal(expectedDriverCall);
        ctx.RecordedTransaction.Names.Should().Equal("sp_1");
    }

    // ------------------------------------------------------------------------------------------------
    // Async cancellation token forwarding.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateSavepointAsync_ForwardsTheCallerTokenToTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();
        using var cts = new CancellationTokenSource();

        await ctx.Transactions.CreateSavepointAsync("sp_1", cts.Token);

        ctx.RecordedTransaction.Tokens.Should().ContainSingle().Which.Should().Be(cts.Token);
    }

    [Fact]
    public async Task RollbackToSavepointAsync_ForwardsTheCallerTokenToTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();
        using var cts = new CancellationTokenSource();

        await ctx.Transactions.RollbackToSavepointAsync("sp_1", cts.Token);

        ctx.RecordedTransaction.Tokens.Should().ContainSingle().Which.Should().Be(cts.Token);
    }

    [Fact]
    public async Task ReleaseSavepointAsync_ForwardsTheCallerTokenToTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();
        using var cts = new CancellationTokenSource();

        await ctx.Transactions.ReleaseSavepointAsync("sp_1", cts.Token);

        ctx.RecordedTransaction.Tokens.Should().ContainSingle().Which.Should().Be(cts.Token);
    }

    [Fact]
    public async Task SavepointAsync_WithoutAToken_ForwardsTheDefaultToken()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        // Deliberately omits the token: this case proves the parameter's default value reaches the
        // driver. xUnit1051 would require the test token here, which would hide that path.
#pragma warning disable xUnit1051
        await ctx.Transactions.CreateSavepointAsync("sp_1");
#pragma warning restore xUnit1051

        ctx.RecordedTransaction.Tokens.Should().ContainSingle().Which.Should().Be(default(CancellationToken));
    }

    // ------------------------------------------------------------------------------------------------
    // Extend-only API: a third-party implementation keeps compiling and inherits the six DIM bodies.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void DefaultInterfaceMethods_ThrowNotSupportedNamingTheOperationAndTheRole()
    {
        ITransactionManager manager = new ThirdPartyTransactionManager();

        var syncCases = new (string Operation, Action Call)[]
        {
            ("CreateSavepoint", () => manager.CreateSavepoint("sp")),
            ("RollbackToSavepoint", () => manager.RollbackToSavepoint("sp")),
            ("ReleaseSavepoint", () => manager.ReleaseSavepoint("sp")),
        };

        foreach (var (operation, call) in syncCases)
            call.Should().Throw<NotSupportedException>().WithMessage($"*{operation}*ITransactionManager*");
    }

    [Fact]
    public async Task DefaultInterfaceMethods_Async_ThrowNotSupportedNamingTheOperationAndTheRole()
    {
        ITransactionManager manager = new ThirdPartyTransactionManager();

        var asyncCases = new (string Operation, Func<Task> Call)[]
        {
            ("CreateSavepointAsync", async () => await manager.CreateSavepointAsync("sp")),
            ("RollbackToSavepointAsync", async () => await manager.RollbackToSavepointAsync("sp")),
            ("ReleaseSavepointAsync", async () => await manager.ReleaseSavepointAsync("sp")),
        };

        foreach (var (operation, call) in asyncCases)
            await call.Should().ThrowAsync<NotSupportedException>().WithMessage($"*{operation}*ITransactionManager*");
    }

    [Fact]
    public void UnmodifiedThirdPartyDialect_ReportsNoSavepoints()
    {
        ISqlDialect dialect = SavepointlessDialect.Instance;

        dialect.SupportsTransactions.Should().BeTrue();
        dialect.SupportsSavepoints.Should().BeFalse("the base-class default is fail-closed");
    }

    [Fact]
    public void DataContext_ExposesNoPublicSavepointMethods()
    {
        typeof(DataContext).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name.Contains("Savepoint", StringComparison.Ordinal))
            .Should().BeEmpty("the operations are reached through the ITransactionManager role only");
    }

    // ------------------------------------------------------------------------------------------------
    // Ordered guards: transaction support → savepoint capability → active transaction → name → driver.
    // Each earlier guard must win over every later one and leave the driver untouched.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void Guard1_TransactionsUnsupported_ThrowsBeforeCapabilityActiveStateAndName()
    {
        using var ctx = new SavepointContext(TransactionlessDialect.Instance);

        var act = () => ctx.Transactions.CreateSavepoint(null!);

        act.Should().Throw<NotSupportedException>().WithMessage("*transaction*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void Guard2_SavepointsUnsupported_ThrowsBeforeActiveStateAndName()
    {
        using var ctx = new SavepointContext(SavepointlessDialect.Instance);
        ctx.Transactions.BeginTransaction();

        var act = () => ctx.Transactions.CreateSavepoint(null!);

        act.Should().Throw<NotSupportedException>().WithMessage("*savepoint*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task Guard2_SavepointsUnsupported_Async_ThrowsBeforeActiveStateAndName()
    {
        using var ctx = new SavepointContext(SavepointlessDialect.Instance);

        var act = async () => await ctx.Transactions.ReleaseSavepointAsync("sp_1");

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*savepoint*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void Guard3_NoActiveTransaction_ThrowsBeforeTheNameCheck()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);

        var act = () => ctx.Transactions.RollbackToSavepoint(null!);

        act.Should().Throw<InvalidOperationException>().WithMessage("*active transaction*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task Guard3_NoActiveTransaction_Async_ThrowsBeforeTheNameCheck()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);

        var act = async () => await ctx.Transactions.ReleaseSavepointAsync("sp_1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active transaction*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void Guard4_InvalidName_ThrowsBeforeTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        var act = () => ctx.Transactions.CreateSavepoint("bad name");

        act.Should().Throw<ArgumentException>();
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------------
    // Transaction lifecycle: completed, detached and enlisted.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void CompletedTransaction_ThrowsInsteadOfCallingTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        var transaction = ctx.Transactions.BeginTransaction();
        transaction.Commit();

        var act = () => ctx.Transactions.CreateSavepoint("sp_1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*active transaction*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void DisposedTransaction_ThrowsInsteadOfCallingTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        var transaction = ctx.Transactions.BeginTransaction();
        transaction.Dispose();

        var act = () => ctx.Transactions.ReleaseSavepoint("sp_1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*active transaction*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void DetachedTransaction_ThrowsInsteadOfCallingTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        var transaction = ctx.RecordedTransaction;
        ctx.Transactions.UseTransaction(transaction);
        ctx.Transactions.UseTransaction(null);

        var act = () => ctx.Transactions.ReleaseSavepoint("sp_1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*active transaction*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void EnlistedTransaction_ReceivesSavepointsWithoutChangingOwnership()
    {
        var ctx = new SavepointContext(SqliteDialect.Instance);
        var transaction = ctx.RecordedTransaction;
        ctx.Transactions.UseTransaction(transaction);

        ctx.Transactions.CreateSavepoint("sp_1");
        ctx.Transactions.RollbackToSavepoint("sp_1");
        ctx.Transactions.ReleaseSavepoint("sp_1");

        ctx.Transactions.CurrentTransaction.Should().BeSameAs(transaction);
        transaction.Operations.Should().Equal("Save", "RollbackTo", "Release");
        transaction.Names.Should().Equal("sp_1", "sp_1", "sp_1");

        // The context never owns an enlisted transaction: disposing it must not commit, roll back or
        // dispose the caller's transaction.
        ctx.Dispose();
        transaction.Disposed.Should().BeFalse();
        transaction.Operations.Should().NotContain(["Commit", "Rollback"]);
    }

    [Fact]
    public async Task EnlistedTransaction_Async_ReceivesSavepoints()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        var transaction = ctx.RecordedTransaction;
        ctx.Transactions.UseTransaction(transaction);

        await ctx.Transactions.CreateSavepointAsync("sp_1", TestContext.Current.CancellationToken);
        await ctx.Transactions.RollbackToSavepointAsync("sp_1", TestContext.Current.CancellationToken);
        await ctx.Transactions.ReleaseSavepointAsync("sp_1", TestContext.Current.CancellationToken);

        ctx.Transactions.CurrentTransaction.Should().BeSameAs(transaction);
        transaction.Operations.Should().Equal("SaveAsync", "RollbackToAsync", "ReleaseAsync");
        transaction.Tokens.Should().OnlyContain(token => token == TestContext.Current.CancellationToken);
    }

    // ------------------------------------------------------------------------------------------------
    // Name policy: boundaries, invalid characters, duplicate pass-through.
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("a")]
    [InlineData("A")]
    [InlineData("_")]
    [InlineData("_sp_1")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345")]
    public void SavepointName_Valid_IsDelegatedUnchanged(string name)
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        ctx.Transactions.CreateSavepoint(name);

        ctx.RecordedTransaction.Names.Should().Equal(name);
    }

    [Fact]
    public void SavepointName_Of32Characters_IsDelegatedUnchanged()
    {
        var name = new string('a', 32);
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        ctx.Transactions.CreateSavepoint(name);

        ctx.RecordedTransaction.Names.Should().Equal(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1sp")]
    [InlineData(" sp")]
    [InlineData("sp ")]
    [InlineData("sp name")]
    [InlineData("sp\tname")]
    [InlineData("sp-name")]
    [InlineData("sp.name")]
    [InlineData("sp'name")]
    [InlineData("sp\"name")]
    [InlineData("sp;drop")]
    [InlineData("sp\u00e9")]
    [InlineData("sp\u4e2d")]
    public void SavepointName_Invalid_ThrowsArgumentExceptionBeforeTheDriver(string name)
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        var act = () => ctx.Transactions.CreateSavepoint(name);

        act.Should().Throw<ArgumentException>();
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void SavepointName_Null_ThrowsArgumentNullExceptionBeforeTheDriver()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        var act = () => ctx.Transactions.CreateSavepoint(null!);

        act.Should().Throw<ArgumentNullException>();
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void SavepointName_LongerThan32Characters_ThrowsArgumentException()
    {
        var name = new string('a', 33);
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        var act = () => ctx.Transactions.CreateSavepoint(name);

        act.Should().Throw<ArgumentException>().WithMessage("*32*");
        ctx.RecordedTransaction.SavepointOperations.Should().BeEmpty();
    }

    [Fact]
    public void DuplicateValidNames_AreDelegatedUnchanged()
    {
        using var ctx = new SavepointContext(SqliteDialect.Instance);
        ctx.Transactions.BeginTransaction();

        ctx.Transactions.CreateSavepoint("dup");
        ctx.Transactions.CreateSavepoint("dup");

        ctx.RecordedTransaction.Operations.Should().Equal("Save", "Save");
        ctx.RecordedTransaction.Names.Should().Equal("dup", "dup");
    }

    // ------------------------------------------------------------------------------------------------
    // Provider edge: SQL Server release is the driver's no-op, not ours.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void SqlServerRelease_IsDelegatedToTheDriver()
    {
        using var ctx = new SavepointContext(SqlServerDialect.Instance);
        ctx.Transactions.BeginTransaction();

        ctx.Transactions.ReleaseSavepoint("sp_1");

        ctx.RecordedTransaction.Operations.Should().Equal("Release");
        ctx.RecordedTransaction.Names.Should().Equal("sp_1");
    }

    // ------------------------------------------------------------------------------------------------
    // Fixtures: a real context and manager over a recording connection/transaction. The dialect is a
    // real built-in one (or a minimal third-party-style dialect for the capability negatives).
    // ------------------------------------------------------------------------------------------------

    private sealed class SavepointContext : DataContext
    {
        private readonly RecordingConnection _connection;

        public SavepointContext(ISqlDialect dialect)
            : this(dialect, new RecordingConnection())
        {
        }

        private SavepointContext(ISqlDialect dialect, RecordingConnection connection)
            : base(null, connection, new DataContextBuilder())
        {
            Dialect = dialect;
            _connection = connection;
        }

        public override ISqlDialect Dialect { get; }

        public RecordingTransaction RecordedTransaction => _connection.Transaction;

        public ITransactionManager Transactions => this;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => _connection;
    }

    /// <summary>A third-party implementation written before savepoints existed: it implements only the abstract members.</summary>
    private sealed class ThirdPartyTransactionManager : ITransactionManager
    {
        public DbTransaction? CurrentTransaction => null;

        public DbTransaction BeginTransaction() => throw new NotSupportedException();

        public DbTransaction BeginTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        public Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void UseTransaction(DbTransaction? transaction)
        {
        }
    }

    /// <summary>A minimal third-party dialect that supports transactions but not savepoints (the DIM default).</summary>
    private sealed class SavepointlessDialect : SqlDialectBase
    {
        internal static readonly SavepointlessDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A minimal third-party dialect with no transaction at all (ClickHouse shape).</summary>
    private sealed class TransactionlessDialect : SqlDialectBase
    {
        internal static readonly TransactionlessDialect Instance = new();

        public override bool SupportsTransactions => false;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class RecordingConnection : DbConnection
    {
        public RecordingConnection()
        {
            Transaction = new RecordingTransaction(this);
        }

        public RecordingTransaction Transaction { get; }

        public bool IsOpen { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => IsOpen ? ConnectionState.Open : ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() => IsOpen = false;

        public override void Open() => IsOpen = true;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => Transaction;

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    /// <summary>
    /// Records every savepoint call the manager makes. A completed transaction reports a null
    /// <see cref="DbConnection"/> and no longer usable, exactly like a provider's finished transaction,
    /// which is what the manager's lazy completion check keys on.
    /// </summary>
    private sealed class RecordingTransaction : DbTransaction
    {
        private readonly RecordingConnection _connection;
        private bool _completed;

        public RecordingTransaction(RecordingConnection connection) => _connection = connection;

        public List<string> Operations { get; } = [];

        public List<string> Names { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public bool Disposed { get; private set; }

        /// <summary>Savepoint operations only, so a lifecycle assertion can prove the driver was untouched.</summary>
        public IEnumerable<string> SavepointOperations =>
            Operations.Where(operation => operation is "Save" or "SaveAsync" or "RollbackTo" or "RollbackToAsync" or "Release" or "ReleaseAsync");

        public override IsolationLevel IsolationLevel => IsolationLevel.Unspecified;

        protected override DbConnection? DbConnection => _completed ? null : _connection;

        public override void Commit()
        {
            Operations.Add("Commit");
            _completed = true;
        }

        public override void Rollback()
        {
            Operations.Add("Rollback");
            _completed = true;
        }

        public override void Save(string savepointName) => Record(nameof(Save), savepointName);

        public override Task SaveAsync(string savepointName, CancellationToken cancellationToken = default)
        {
            Record(nameof(SaveAsync), savepointName);
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public override void Rollback(string savepointName) => Record("RollbackTo", savepointName);

        public override Task RollbackAsync(string savepointName, CancellationToken cancellationToken = default)
        {
            Record("RollbackToAsync", savepointName);
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public override void Release(string savepointName) => Record(nameof(Release), savepointName);

        public override Task ReleaseAsync(string savepointName, CancellationToken cancellationToken = default)
        {
            Record(nameof(ReleaseAsync), savepointName);
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            _completed = true;
            base.Dispose(disposing);
        }

        private void Record(string operation, string name)
        {
            Operations.Add(operation);
            Names.Add(name);
        }
    }
}

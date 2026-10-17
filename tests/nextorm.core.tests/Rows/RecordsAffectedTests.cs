using System.Collections;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the shared affected-row metadata/lifetime contract of <see cref="ProcedureResult.RecordsAffected"/>
/// and <see cref="BatchResult.RecordsAffected"/> on a substituted <see cref="DbDataReader"/>: the exact
/// get-only <c>int</c> surface, the <c>-1</c> sentinel while the reader is live, the pre-/post-release
/// capture preference, the sync/async/early/repeated release paths and a non-throwing cached getter.
/// Real-driver totals are covered by the provider suites (<c>RawCommandTests</c>,
/// <c>BatchMultipleResultTests</c>) and by the integration suite.
/// </summary>
public class RecordsAffectedTests
{
    // --- Fake driver reader: the metadata getter can differ before/after release and can throw. ---

    private sealed class FakeRecordsReader : DbDataReader
    {
        private bool _closed;

        /// <summary>The value reported while the reader is still open.</summary>
        public int PreClose { get; set; } = -1;

        /// <summary>The value reported once the reader has been released.</summary>
        public int PostClose { get; set; } = -1;

        /// <summary>When set, reading the metadata before release throws this instance.</summary>
        public Exception? PreError { get; set; }

        /// <summary>When set, reading the metadata after release throws this instance.</summary>
        public Exception? PostError { get; set; }

        /// <summary>When set, releasing the reader throws this instance (a cleanup error).</summary>
        public Exception? DisposeError { get; set; }

        public int DisposeCount { get; private set; }

        public override int FieldCount => 0;
        public override int Depth => 0;
        public override bool HasRows => false;
        public override bool IsClosed => _closed;

        public override int RecordsAffected
        {
            get
            {
                if (_closed)
                {
                    if (PostError is not null)
                        throw PostError;

                    return PostClose;
                }

                if (PreError is not null)
                    throw PreError;

                return PreClose;
            }
        }

        public override object this[int ordinal] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();
        public override bool Read() => false;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override bool NextResult() => false;
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override string GetName(int ordinal) => throw new NotSupportedException();
        public override int GetOrdinal(string name) => throw new NotSupportedException();
        public override Type GetFieldType(int ordinal) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
        public override object GetValue(int ordinal) => throw new NotSupportedException();
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => throw new NotSupportedException();
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override string GetString(int ordinal) => throw new NotSupportedException();
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_closed)
            {
                _closed = true;
                DisposeCount++;
                if (DisposeError is not null)
                    throw DisposeError;
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            if (!_closed)
            {
                _closed = true;
                DisposeCount++;
                if (DisposeError is not null)
                    return ValueTask.FromException(DisposeError);
            }

            return ValueTask.CompletedTask;
        }
    }

    // A tracking command: the owner's async release must dispose it, and the contract test below
    // observes that through the counter (a removed disposal is otherwise invisible).
    private sealed class FakeRecordsCommand : DbCommand
    {
        public int DisposeCount { get; private set; }
        public int DisposeAsyncCount { get; private set; }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }

    // A DataContext only needs the abstract provider hooks overridden; nothing here touches a
    // connection, so the metadata contract can be exercised without a database.
    private sealed class FakeContext : DataContext
    {
        public FakeContext()
            : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => throw new NotSupportedException();

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    private static (ProcedureResult Result, CommandReaderOwner Owner, FakeRecordsReader Reader) CreateResult(int preClose = -1, int postClose = -1)
    {
        var reader = new FakeRecordsReader { PreClose = preClose, PostClose = postClose };
        var owner = new CommandReaderOwner(null!, reader);
        return (new ProcedureResult(new FakeContext(), owner), owner, reader);
    }

    // --- C1: exact surface on both types. ---

    [Fact]
    public void RecordsAffected_OnBothResultTypes_IsAGetOnlyInt()
    {
        foreach (var type in new[] { typeof(ProcedureResult), typeof(BatchResult) })
        {
            var property = type.GetProperty(nameof(ProcedureResult.RecordsAffected));

            property.Should().NotBeNull();
            property!.PropertyType.Should().Be(typeof(int));
            property.CanWrite.Should().BeFalse();
            property.SetMethod.Should().BeNull();
        }
    }

    [Fact]
    public void BatchResult_ExposesTheConstructorTotal_NotTheResultSetCount()
    {
        IList[] sets = [new List<int> { 1, 2, 3 }, new List<int>()];
        var result = new BatchResult(sets, [typeof(int), typeof(int)], [["a"], ["b"]], recordsAffected: 5);

        result.ResultSetCount.Should().Be(2);
        result.RecordsAffected.Should().Be(5);
    }

    [Fact]
    public void BatchResult_SentinelIsPreserved()
    {
        var result = new BatchResult([new List<int>()], [typeof(int)], [["a"]], recordsAffected: -1);

        result.RecordsAffected.Should().Be(-1);
    }

    // --- C2: availability. ---

    [Fact]
    public void BeforeRelease_ReturnsSentinel_AfterDispose_ReturnsCapturedTotal()
    {
        var (result, owner, _) = CreateResult(preClose: 3, postClose: 3);

        result.RecordsAffected.Should().Be(-1);

        owner.Dispose();

        result.RecordsAffected.Should().Be(3);
    }

    [Fact]
    public void DifferingPreAndPostClose_PrefersThePostReleaseValue()
    {
        var (result, owner, _) = CreateResult(preClose: 2, postClose: 5);

        owner.Dispose();

        result.RecordsAffected.Should().Be(5);
    }

    [Fact]
    public void PostReleaseValueUnavailable_KeepsThePreReleaseValue()
    {
        var (result, owner, _) = CreateResult(preClose: 7, postClose: -1);

        owner.Dispose();

        result.RecordsAffected.Should().Be(7);
    }

    [Fact]
    public void ZeroTotal_IsAPublishedValue_NotTheSentinel()
    {
        var (result, owner, _) = CreateResult(preClose: 0, postClose: 0);

        owner.Dispose();

        result.RecordsAffected.Should().Be(0);
    }

    [Fact]
    public async Task CloseReader_PublishesTheTotal_PreferringThePostReleaseValue()
    {
        var (result, owner, reader) = CreateResult(preClose: 2, postClose: 5);

        owner.CloseReader();

        owner.IsReaderClosed.Should().BeTrue();
        // The post-release value wins when the driver only finalizes the total on Close.
        result.RecordsAffected.Should().Be(5);

        // A repeated CloseReader has no reader left: it must be a no-op, not a second capture.
        owner.CloseReader();
        result.RecordsAffected.Should().Be(5);
        reader.DisposeCount.Should().Be(1);

        // The command-only disposal path must not lose or re-publish the value.
        await owner.DisposeAsync();

        result.RecordsAffected.Should().Be(5);
        reader.DisposeCount.Should().Be(1);
    }

    // --- C3: lifetime. ---

    [Fact]
    public async Task AsyncRelease_PrefersThePostReleaseValue()
    {
        var (result, owner, _) = CreateResult(preClose: 2, postClose: 6);

        await owner.DisposeAsync();

        result.RecordsAffected.Should().Be(6);
    }

    [Fact]
    public async Task EarlyDisposeWithoutAnyRead_CapturesTheTotal()
    {
        var (result, owner, reader) = CreateResult(preClose: 9, postClose: 9);

        // No Read/NextResult was issued: releasing early must still publish the driver total.
        reader.FieldCount.Should().Be(0);
        await owner.DisposeAsync();

        result.RecordsAffected.Should().Be(9);
    }

    [Fact]
    public async Task RepeatedReleaseAndGetter_AreIdempotent_AndDoNotThrow()
    {
        var (result, owner, reader) = CreateResult(preClose: 8, postClose: 8);

        owner.Dispose();
        owner.Dispose();
        await owner.DisposeAsync();

        reader.DisposeCount.Should().Be(1);
        result.RecordsAffected.Should().Be(8);

        // The getter is a cached read: it never touches the released reader.
        var act = () => result.RecordsAffected;
        act.Should().NotThrow();
        result.RecordsAffected.Should().Be(8);
    }

    [Fact]
    public void DisposeError_IsNotMasked_AndTheSnapshotSurvives()
    {
        var (result, owner, reader) = CreateResult(preClose: 3, postClose: 3);
        reader.DisposeError = new InvalidOperationException("cleanup failed");

        var act = () => owner.Dispose();

        act.Should().Throw<InvalidOperationException>().WithMessage("cleanup failed");
        result.RecordsAffected.Should().Be(3);
    }

    [Fact]
    public async Task AsyncDisposeError_IsNotMasked()
    {
        var (_, owner, reader) = CreateResult(preClose: 3, postClose: 3);
        reader.DisposeError = new InvalidOperationException("async cleanup failed");

        var act = async () => await owner.DisposeAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("async cleanup failed");
    }

    [Fact]
    public async Task AsyncRelease_DisposesTheCommand()
    {
        var reader = new FakeRecordsReader { PreClose = 4, PostClose = 4 };
        var command = new FakeRecordsCommand();
        var owner = new CommandReaderOwner(command, reader);
        using var result = new ProcedureResult(new FakeContext(), owner);

        await owner.DisposeAsync();

        // The command is released by the async path too (not only the reader).
        command.DisposeAsyncCount.Should().Be(1);
        result.RecordsAffected.Should().Be(4);
    }

    // --- Additional variants: metadata that throws is unavailable, not fatal. ---

    [Fact]
    public void UnavailableMetadata_IsTheSentinel_AndNeverThrows()
    {
        var (result, owner, reader) = CreateResult();
        reader.PreError = new NotSupportedException("no metadata");
        reader.PostError = new ObjectDisposedException(nameof(FakeRecordsReader));

        owner.Dispose();

        var act = () => result.RecordsAffected;
        act.Should().NotThrow();
        result.RecordsAffected.Should().Be(-1);
    }

    [Fact]
    public void InvalidOperationOnRead_IsTheSentinel_AndThePostValueStillWins()
    {
        var (result, owner, reader) = CreateResult();
        reader.PreError = new InvalidOperationException("metadata is not available yet");
        reader.PostClose = 7;

        owner.Dispose();

        result.RecordsAffected.Should().Be(7);
    }
}

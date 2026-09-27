using System.Collections;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Coverage for the <c>ToDataReader</c>/<c>ToDataReaderAsync</c> terminals and the internal
/// <see cref="LobDataReader"/> wrapper. The in-memory provider rejects the terminals (it cannot
/// stream); the wrapper is exercised over a substituted <see cref="DbDataReader"/> to pin that it
/// is transparent, that disposal is idempotent, that the opening token still cancels reads, and
/// that a locator dialect and a provider without sequential access are rejected before execution.
/// </summary>
public class LobDataReaderTests
{
    private readonly IDataContext _ctx;

    public LobDataReaderTests(IDataContext ctx)
    {
        _ctx = ctx;
    }

    // --- Terminal gate: the in-memory provider is not relational, so it cannot open a reader. ---

    [Fact]
    public void ToDataReader_InMemory_ShouldThrowNotSupported()
    {
        var command = _ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => new { it.Id, it.Data });

        var act = () => command.ToDataReader();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ToDataReaderAsync_InMemory_ShouldThrowNotSupported()
    {
        var command = _ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => new { it.Id, it.Data });

        var act = async () => await command.ToDataReaderAsync();

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void ToDataReader_AfterContextDispose_ShouldThrowObjectDisposed()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => new { it.Id, it.Data });
        ctx.Dispose();

        var act = () => command.ToDataReader();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task ToDataReaderAsync_AfterContextDispose_ShouldThrowObjectDisposed()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => new { it.Id, it.Data });
        ctx.Dispose();

        var act = async () => await command.ToDataReaderAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // --- Dialect gate: rejected before the provider is touched. The fake context throws
    // InvalidOperationException from its provider seam, so a NotSupportedException proves the
    // terminal never reached execution. ---

    [Fact]
    public void ToDataReader_WithoutSequentialAccess_ShouldThrowNotSupportedBeforeExecution()
        => AssertGate(
            new TestDialect(sequentialAccess: false, locator: null),
            BuildCommand,
            command => command.ToDataReader());

    [Fact]
    public async Task ToDataReaderAsync_WithoutSequentialAccess_ShouldThrowNotSupportedBeforeExecution()
        => await AssertGateAsync(
            new TestDialect(sequentialAccess: false, locator: null),
            BuildCommand,
            command => command.ToDataReaderAsync());

    [Fact]
    public void ToDataReader_WithLocatorDialect_ShouldThrowNotSupportedBeforeExecution()
        => AssertGate(
            new TestDialect(sequentialAccess: true, locator: "rowid"),
            BuildCommand,
            command => command.ToDataReader());

    [Fact]
    public async Task ToDataReaderAsync_WithLocatorDialect_ShouldThrowNotSupportedBeforeExecution()
        => await AssertGateAsync(
            new TestDialect(sequentialAccess: true, locator: "rowid"),
            BuildCommand,
            command => command.ToDataReaderAsync());

    private static void AssertGate<TResult>(
        ISqlDialect dialect,
        Func<FakeDialectContext, QueryCommand<TResult>> build,
        Action<QueryCommand<TResult>> act)
    {
        using var ctx = new FakeDialectContext(dialect);
        var command = build(ctx);

        var invoke = () => act(command);

        invoke.Should().Throw<NotSupportedException>();
    }

    private static async Task AssertGateAsync<TResult>(
        ISqlDialect dialect,
        Func<FakeDialectContext, QueryCommand<TResult>> build,
        Func<QueryCommand<TResult>, Task> act)
    {
        using var ctx = new FakeDialectContext(dialect);
        var command = build(ctx);

        var invoke = async () => await act(command);

        await invoke.Should().ThrowAsync<NotSupportedException>();
    }

    private static QueryCommand<byte[]> BuildCommand(FakeDialectContext ctx) =>
        ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => it.Data!);

    private sealed class TestDialect(bool sequentialAccess, string? locator) : SqlDialectBase
    {
        public override bool SupportsSequentialAccess => sequentialAccess;

        public override string? LobLocatorColumn => locator;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class FakeDialectContext(ISqlDialect dialect) : DataContext(new DataContextBuilder())
    {
        public override ISqlDialect Dialect => dialect;

        public override DbParameter CreateParam(string name, object? value) => throw new InvalidOperationException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new InvalidOperationException();
    }

    // --- Wrapper transparency over a substituted DbDataReader. ---

    private static (LobDataReader Reader, FakeDbDataReader Inner) CreateReader(
        string[]? names = null,
        Type[]? types = null,
        object?[][]? rows = null)
    {
        names ??= ["id", "name"];
        types ??= [typeof(long), typeof(string)];
        rows ??=
        [
            [1L, "alpha"],
            [2L, "beta"],
            [3L, null],
        ];

        var inner = new FakeDbDataReader(names, types, rows);
        var owner = new CommandReaderOwner(null!, inner);
        return (new LobDataReader(owner, CancellationToken.None), inner);
    }

    private static (LobDataReader Reader, FakeDbDataReader Inner) CreateCancelledReader(CancellationToken cancellationToken)
    {
        var inner = new FakeDbDataReader(["id", "name"], [typeof(long), typeof(string)], [[1L, "alpha"]]);
        var owner = new CommandReaderOwner(null!, inner);
        return (new LobDataReader(owner, cancellationToken), inner);
    }

    [Fact]
    public void LobDataReader_Metadata_ShouldDelegateToInner()
    {
        var (reader, _) = CreateReader();
        using var _ = reader;

        reader.FieldCount.Should().Be(2);
        reader.Depth.Should().Be(0);
        reader.HasRows.Should().BeTrue();
        reader.RecordsAffected.Should().Be(-1);
        reader.GetName(0).Should().Be("id");
        reader.GetName(1).Should().Be("name");
        reader.GetOrdinal("name").Should().Be(1);
        reader.GetFieldType(0).Should().Be(typeof(long));
        reader.GetDataTypeName(1).Should().Be("String");
    }

    [Fact]
    public void LobDataReader_ReadTwoColumnsAcrossRows_ShouldReturnValuesInOrder()
    {
        var (reader, _) = CreateReader();
        using var _ = reader;

        var rows = new List<(long Id, string? Name)>();
        while (reader.Read())
            rows.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));

        rows.Should().Equal((1L, "alpha"), (2L, "beta"), (3L, null));
    }

    [Fact]
    public void LobDataReader_ValuesAndIndexers_ShouldDelegateToInner()
    {
        var (reader, _) = CreateReader();
        using var _ = reader;
        reader.Read().Should().BeTrue();

        reader.GetValue(0).Should().Be(1L);
        reader[0].Should().Be(1L);
        reader["name"].Should().Be("alpha");

        var values = new object[2];
        reader.GetValues(values).Should().Be(2);
        values.Should().Equal(1L, "alpha");
    }

    [Fact]
    public void LobDataReader_StreamAndTextReader_ShouldDelegateToInner()
    {
        var (reader, inner) = CreateReader(
            names: ["payload", "body"],
            types: [typeof(byte[]), typeof(string)],
            rows: [[new byte[] { 1, 2, 3 }, "hello"]]);

        using var _ = reader;
        reader.Read().Should().BeTrue();

        using var stream = reader.GetStream(0);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.ToArray().Should().Equal(1, 2, 3);

        using var text = reader.GetTextReader(1);
        text.ReadToEnd().Should().Be("hello");

        inner.StreamCalls.Should().Be(1);
        inner.TextReaderCalls.Should().Be(1);
    }

    [Fact]
    public async Task LobDataReader_ReadAsync_ShouldDelegateToInner()
    {
        var (reader, _) = CreateReader();
        await using var _ = reader;

        (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        reader.GetString(1).Should().Be("alpha");
    }

    [Fact]
    public void LobDataReader_Dispose_ShouldBeIdempotent()
    {
        var (reader, inner) = CreateReader();

        reader.Dispose();
        reader.Dispose();

        inner.DisposeCount.Should().Be(1);
        var act = () => reader.Read();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task LobDataReader_DisposeAsync_ShouldBeIdempotent()
    {
        var (reader, inner) = CreateReader();

        await reader.DisposeAsync();
        await reader.DisposeAsync();

        inner.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task LobDataReader_DisposeThenDisposeAsync_ShouldBeIdempotent()
    {
        var (reader, inner) = CreateReader();

        reader.Dispose();
        await reader.DisposeAsync();

        inner.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void LobDataReader_Close_ShouldReleaseInner()
    {
        var (reader, inner) = CreateReader();

        reader.Close();
        reader.Close();

        inner.DisposeCount.Should().Be(1);
        var act = () => reader.Read();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task LobDataReader_CloseThenDisposeAsync_ShouldBeIdempotent()
    {
        var (reader, inner) = CreateReader();

        reader.Close();
        await reader.DisposeAsync();

        inner.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void LobDataReader_CancelledBeforeRead_ShouldThrowOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (reader, _) = CreateCancelledReader(cts.Token);
        using var _ = reader;

        var act = () => reader.Read();

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task LobDataReader_CancelledBeforeReadAsync_ShouldThrowOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (reader, _) = CreateCancelledReader(cts.Token);
        await using var _ = reader;

        var act = async () => await reader.ReadAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LobDataReader_ReadAsyncCanceledByPerCallToken_ShouldThrowOperationCanceled()
    {
        var (reader, _) = CreateReader();
        await using var _ = reader;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await reader.ReadAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class FakeDbDataReader(string[] names, Type[] types, object?[][] rows) : DbDataReader
    {
        private int _rowIndex = -1;
        private bool _disposed;

        public int DisposeCount { get; private set; }
        public int StreamCalls { get; private set; }
        public int TextReaderCalls { get; private set; }

        public override int FieldCount => names.Length;
        public override int Depth => 0;
        public override bool HasRows => rows.Length > 0;
        public override bool IsClosed => _disposed;
        public override int RecordsAffected => -1;

        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read()
        {
            _rowIndex++;
            return _rowIndex < rows.Length;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read());
        }

        public override bool NextResult() => false;

        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public override string GetName(int ordinal) => names[ordinal];

        public override int GetOrdinal(string name) => Array.IndexOf(names, name);

        public override Type GetFieldType(int ordinal) => types[ordinal];

        public override string GetDataTypeName(int ordinal) => types[ordinal].Name;

        public override object GetValue(int ordinal) => rows[_rowIndex][ordinal]!;

        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, names.Length);
            Array.Copy(rows[_rowIndex], values, count);
            return count;
        }

        public override bool IsDBNull(int ordinal) => rows[_rowIndex][ordinal] is null;

        public override bool GetBoolean(int ordinal) => (bool)rows[_rowIndex][ordinal]!;

        public override byte GetByte(int ordinal) => (byte)rows[_rowIndex][ordinal]!;

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        {
            var data = (byte[])rows[_rowIndex][ordinal]!;
            var count = (int)Math.Min(length, data.Length - dataOffset);
            if (buffer is not null)
                Array.Copy(data, dataOffset, buffer, bufferOffset, count);
            return count;
        }

        public override char GetChar(int ordinal) => (char)rows[_rowIndex][ordinal]!;

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        {
            var data = (string)rows[_rowIndex][ordinal]!;
            var count = (int)Math.Min(length, data.Length - dataOffset);
            if (buffer is not null)
                data.CopyTo((int)dataOffset, buffer, bufferOffset, count);
            return count;
        }

        public override DateTime GetDateTime(int ordinal) => (DateTime)rows[_rowIndex][ordinal]!;

        public override decimal GetDecimal(int ordinal) => (decimal)rows[_rowIndex][ordinal]!;

        public override double GetDouble(int ordinal) => (double)rows[_rowIndex][ordinal]!;

        public override float GetFloat(int ordinal) => (float)rows[_rowIndex][ordinal]!;

        public override Guid GetGuid(int ordinal) => (Guid)rows[_rowIndex][ordinal]!;

        public override short GetInt16(int ordinal) => (short)rows[_rowIndex][ordinal]!;

        public override int GetInt32(int ordinal) => (int)rows[_rowIndex][ordinal]!;

        public override long GetInt64(int ordinal) => (long)rows[_rowIndex][ordinal]!;

        public override string GetString(int ordinal) => (string)rows[_rowIndex][ordinal]!;

        public override Stream GetStream(int ordinal)
        {
            StreamCalls++;
            return new MemoryStream((byte[])rows[_rowIndex][ordinal]!);
        }

        public override TextReader GetTextReader(int ordinal)
        {
            TextReaderCalls++;
            return new StringReader((string)rows[_rowIndex][ordinal]!);
        }

        public override IEnumerator GetEnumerator() => rows.GetEnumerator();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                DisposeCount++;
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

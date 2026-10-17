using System.Collections;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Command ownership of <see cref="ResultSetEnumerator{TResult}"/> (#101 P1). The transient
/// streaming-row command created by the planner is not stored in the plan cache, so the enumerator must
/// release it together with the reader on every disposal path. A cached plan's command, by contrast,
/// must never be disposed by an enumerator.
/// </summary>
public class ResultSetEnumeratorOwnershipTests
{
    private static DbPreparedQueryCommand<int> Prepared(TrackingDbCommand command)
        => new(command, _ => 0, new PreparedCommandOptions());

    [Fact]
    public void OwnedCommand_Dispose_ShouldDisposeOnce()
    {
        var command = new TrackingDbCommand();
        var enumerator = new ResultSetEnumerator<int>(Prepared(command));
        enumerator.OwnCommand();

        enumerator.Dispose();
        enumerator.Dispose();

        command.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task OwnedCommand_DisposeAsync_ShouldDisposeOnce()
    {
        var command = new TrackingDbCommand();
        var enumerator = new ResultSetEnumerator<int>(Prepared(command));
        enumerator.OwnCommand();

        await enumerator.DisposeAsync();
        await enumerator.DisposeAsync();

        command.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task OwnedCommand_DisposeThenDisposeAsync_ShouldDisposeOnce()
    {
        var command = new TrackingDbCommand();
        var enumerator = new ResultSetEnumerator<int>(Prepared(command));
        enumerator.OwnCommand();

        enumerator.Dispose();
        await enumerator.DisposeAsync();

        command.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void NonOwnedCommand_Dispose_ShouldNotDisposeTheCachedCommand()
    {
        var command = new TrackingDbCommand();
        var enumerator = new ResultSetEnumerator<int>(Prepared(command));

        enumerator.Dispose();

        command.DisposeCount.Should().Be(0);
    }

    private sealed class TrackingDbCommand : DbCommand
    {
        private readonly EmptyDbParameterCollection _parameters = new();

        public int DisposeCount { get; private set; }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;
            base.Dispose(disposing);
        }
    }

    private sealed class EmptyDbParameterCollection : DbParameterCollection
    {
        public override int Count => 0;
        public override object SyncRoot => this;
        public override int Add(object value) => throw new NotSupportedException();
        public override void AddRange(Array values) => throw new NotSupportedException();
        public override void Clear() { }
        public override bool Contains(object value) => false;
        public override bool Contains(string? value) => false;
        public override void CopyTo(Array array, int index) { }
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override int IndexOf(object value) => -1;
        public override int IndexOf(string? parameterName) => -1;
        public override void Insert(int index, object value) => throw new NotSupportedException();
        public override void Remove(object value) => throw new NotSupportedException();
        public override void RemoveAt(int index) => throw new NotSupportedException();
        public override void RemoveAt(string? parameterName) => throw new NotSupportedException();
        protected override DbParameter GetParameter(int index) => throw new NotSupportedException();
        protected override DbParameter GetParameter(string? parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => throw new NotSupportedException();
        protected override void SetParameter(string? parameterName, DbParameter value) => throw new NotSupportedException();
    }
}

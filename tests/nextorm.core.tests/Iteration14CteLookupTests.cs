using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Iteration 14 proposal 2 (CTE lookup allocation): the data-modifying CTE detection
/// (<see cref="QueryCommand.HasDataModifyingCte"/>) must scan a prepared/hoisted, flat CTE list without
/// allocating a visited <see cref="HashSet{T}"/>, while keeping nested-DML detection, reference-identity
/// cycle termination and the call-local plan-cache bypass intact.
/// </summary>
[Collection("Query cache controls")]
public class Iteration14CteLookupTests
{
    public sealed class CteNumberRow
    {
        public int n { get; set; }
    }

    [Fact]
    public void CteWarmReuse_AllocatesZero()
    {
        using var ctx = new InMemoryDataContext();

        var command = ctx
            .With("recent", ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("recent")
            .Select(t => new { id = t["id"].AsInt });

        // Prepare (hoist) once so the CTE list is the flat, normalized shape the warm lookup sees.
        command.PrepareCommand(false, CancellationToken.None);

        // Measured directly: the per-lookup cost of the detection on an already-prepared flat CTE
        // command, which is exactly the allocation the old eager HashSet removed from this path.
        for (var i = 0; i < 100; i++)
            _ = command.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = command.HasDataModifyingCte;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0, "a warm, flat CTE lookup must not allocate");
    }

    [Fact]
    public void RecursiveCteWarmReuse_AllocatesZero()
    {
        using var ctx = new InMemoryDataContext();

        var anchor = ctx.From<SimpleEntity>().Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var command = ctx
            .WithRecursive("nums", anchor.UnionAll(step))
            .From("nums")
            .Select(t => new CteNumberRow { n = t["n"].AsInt });

        command.PrepareCommand(false, CancellationToken.None);

        for (var i = 0; i < 100; i++)
            _ = command.HasDataModifyingCte;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = command.HasDataModifyingCte;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0, "a warm, flat recursive CTE lookup must not allocate");
    }

    [Fact]
    public void NestedMutation_DisablesReadReuse()
    {
        using var ctx = new DmlCteTestContext();
        ctx.PurgeQueryCache();

        // The DML CTE is declared on the body of the outer read CTE, so the top-level command's own
        // list has no mutation and only the recursive fallback finds it.
        var insert = ctx.CreateInsertBuilder<SimpleEntity>().Value(x => x.Id, 1).Returning(x => new { x.Id });
        var mutationScope = ctx.With("ins", insert);
        var inner = mutationScope.From("ins").Select(r => new { r.Id });
        var outer = ctx.With("o", inner).From("o").Select(t => t["Id"].AsInt);

        outer.HasDataModifyingCte.Should().BeTrue();

        var first = ctx.GetPreparedQueryCommand(outer, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(outer, createEnumerator: false, storeInCache: true, CancellationToken.None);

        ReferenceEquals(first, second).Should()
            .BeFalse("a nested data-modifying CTE must bypass the plan cache call-locally");
        outer.Cache.Should().BeTrue("the sticky Cache flag must never be cleared");
    }

    [Fact]
    public void CyclicNestedGraph_TerminatesAndFindsMutation()
    {
        using var ctx = new InMemoryDataContext();

        var a = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var b = ctx.From<SimpleEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });

        // a -> b -> a: a reference-identity cycle the traversal must terminate on.
        a.Ctes = new[] { new CteDefinition("b", b) };
        b.Ctes = new[] { new CteDefinition("a", a) };

        var root = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        root.Ctes = new[] { new CteDefinition("a", a) };

        root.HasDataModifyingCte.Should().BeFalse("a mutation-free declaration cycle detects nothing and terminates");
    }

    [Fact]
    public void CyclicNestedGraph_WithDeepMutation_TerminatesAndFindsMutation()
    {
        using var ctx = new DmlCteTestContext();

        var insert = ctx.CreateInsertBuilder<SimpleEntity>().Value(x => x.Id, 1).Returning(x => new { x.Id });
        var mutationShape = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        var mutation = new CteDefinition("m", mutationShape, new CteMutation(insert.BuildMutationCommand()));

        var a = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var b = ctx.From<SimpleEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });

        // a -> b -> {m, a}: deep mutation plus a cycle back to the root's declaration.
        a.Ctes = new[] { new CteDefinition("b", b) };
        b.Ctes = new[] { mutation, new CteDefinition("a", a) };

        var root = ctx.From<SimpleEntity>().Select(x => new { x.Id });
        root.Ctes = new[] { new CteDefinition("a", a) };

        root.HasDataModifyingCte.Should().BeTrue("a mutation nested below a declaration cycle must still be found");
    }

    // ------------------------------------------------------------------------------------------------
    // Provider-free DML-capable context: lets the planner gate run without a database, mirroring the
    // minimal fake context used by TypedCteTests.
    // ------------------------------------------------------------------------------------------------

    private sealed class DmlCteTestDialect : SqlDialectBase
    {
        internal static readonly DmlCteTestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }

        public override bool SupportsDataModifyingCtes => true;
    }

    private sealed class DmlCteTestContext : DataContext
    {
        private readonly FakeConnection _connection = new();

        public DmlCteTestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => DmlCteTestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => new FakeParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString) => _connection;
    }

    private sealed class FakeParameter(string name) : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class FakeConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeCommand();
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly FakeParameterCollection _parameters = new();

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = null!;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        [AllowNull]
        protected override DbTransaction DbTransaction { get; set; } = null!;

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbParameter CreateDbParameter() => new FakeParameter(string.Empty);

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value!);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string? value) => _items.Any(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string? parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string? parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string? parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string? parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
                Add(value);
            else
                _items[index] = value;
        }
    }
}

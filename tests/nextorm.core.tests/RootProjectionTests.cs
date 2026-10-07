using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Issue #160 Phase 2 core coverage for the root projection: the dim-1 <see cref="Projection{T1}"/>
/// planner path, the <see cref="IExtendableProjection"/> slot chain up to the arity-8 cap, the
/// documented <c>As&lt;TResult&gt;</c> overflow, the runtime root-alias guard (misuse / in-memory
/// fail-closed) and plan reuse. Runs against a provider-free <see cref="TestContext"/> (fake dialect)
/// so the planner and SQL renderer are exercised without a database, plus the core in-memory provider
/// for the fail-closed guard.
/// <para>
/// Runs in the "Query cache controls" collection (serialized, parallelization disabled) because the
/// plan-reuse cases read the process-wide <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class RootProjectionTests
{
    [SqlTable("orders")]
    private sealed class Order
    {
        [Key]
        public int Id { get; set; }

        [Column("buyer_id")]
        public long BuyerId { get; set; }
    }

    [SqlTable("person")]
    private sealed class Person
    {
        [Key]
        public long Id { get; set; }
    }

    /// <summary>Hand-written root projection: names slot 1 lexically, like the generated surface.</summary>
    private sealed class RootProjection<T> : Projection<T>
    {
        [JoinSlot(1)]
        public T Root => throw new NotSupportedException();
    }

    /// <summary>Hand-written root+join projection: root is slot 1, the joined table is slot 2.</summary>
    private sealed class RootJoinProjection<T1, T2> : Projection<T1, T2>
    {
        [JoinSlot(2)]
        public T2 Buyer => throw new NotSupportedException();
    }

    private static EntityBuilder<RootProjection<Order>> Rooted(TestContext ctx) =>
        ctx.From<Order>().AliasRoot<EntityBuilder<RootProjection<Order>>, RootProjection<Order>>(
            static dc => new EntityBuilder<RootProjection<Order>>(dc));

    // ------------------------------------------------------------------ dimension / Extend --------

    [Fact]
    public void Dim1_projection_reports_dimension_one_and_extend_yields_slot_two()
    {
        IExtendableProjection projection = new Projection<Order> { Item1 = new Order { Id = 7 } };

        typeof(Projection<Order>).TryGetProjectionDimension(out var dim).Should().BeTrue();
        dim.Should().Be(1);

        var extended = projection.Extend("second");

        extended.Should().BeOfType<Projection<Order, string>>();
        var pair = (Projection<Order, string>)extended;
        pair.Item1.Id.Should().Be(7);
        pair.Item2.Should().Be("second");
    }

    [Fact]
    public void Extend_chain_walks_dimension_one_to_eight_and_preserves_every_item()
    {
        IProjection current = new Projection<int> { Item1 = 1 };

        for (var next = 2; next <= 8; next++)
        {
            current = ((IExtendableProjection)current).Extend(next);

            current.GetType().TryGetProjectionDimension(out var dim).Should().BeTrue();
            dim.Should().Be(next);
            for (var slot = 1; slot <= next; slot++)
            {
                current.GetType().GetProperty("Item" + slot)!.GetValue(current).Should().Be(slot);
            }
        }

        // The arity-8 projection is the documented cap: it is a projection but no longer extendable.
        current.Should().BeAssignableTo<IProjection>();
        current.Should().NotBeAssignableTo<IExtendableProjection>();
    }

    [Fact]
    public void Max_arity_projection_cannot_absorb_a_ninth_item()
    {
        var capped = new Projection<int, int, int, int, int, int, int, int>();

        (capped is IExtendableProjection).Should().BeFalse();
        typeof(Projection<int, int, int, int, int, int, int, int>)
            .Should().NotBeAssignableTo<IExtendableProjection>();
        typeof(Projection<int, int, int, int, int, int, int, int>)
            .Should().BeAssignableTo<IProjection>();
    }

    [Fact]
    public void As_overflow_escape_is_available_on_the_accumulated_builder()
    {
        // Documented escape: at the cap, project into a named type with EntityBuilder<T>.As<TResult>
        // and continue from the derived table; the generic method is the contract that stays usable.
        var asMethod = typeof(EntityBuilder<Order>).GetMethods()
            .Single(m => m.Name == "As" && m.IsGenericMethodDefinition);

        asMethod.IsGenericMethodDefinition.Should().BeTrue();
        asMethod.GetParameters().Should().HaveCount(1);
        asMethod.GetParameters()[0].ParameterType.IsGenericType.Should().BeTrue();
        asMethod.GetParameters()[0].ParameterType.GetGenericTypeDefinition()
            .Should().Be(typeof(System.Linq.Expressions.Expression<>));
    }

    // ------------------------------------------------------------------ planning ------------------

    [Fact]
    public void Dim1_root_projection_plans_and_maps_the_root_slot_to_t1()
    {
        using var ctx = new TestContext();

        var sql = SqlOf(ctx, Rooted(ctx).Select(p => p.Root.Id));

        // A single-table root needs no alias, but the source must stay the physical table and slot 1
        // must resolve to its column (no nested projection, no flattening).
        sql.Should().Be("select Id from orders");
        sql.Should().NotContain("select * from (");
    }

    [Fact]
    public void Root_projection_with_a_join_maps_root_to_t1_and_join_to_t2()
    {
        using var ctx = new TestContext();
        var people = ctx.From<Person>();

        var joined = Rooted(ctx).JoinAlias<EntityBuilder<RootJoinProjection<Order, Person>>, RootJoinProjection<Order, Person>, Person>(
            static dc => new EntityBuilder<RootJoinProjection<Order, Person>>(dc),
            people,
            (a, b) => a.Root.BuyerId == b.Id);

        var sql = SqlOf(ctx, joined.Select(p => p.Buyer.Id));

        sql.Should().Contain("orders as 't1'");
        sql.Should().Contain("join person as 't2'");
        sql.Should().Contain("on t1.buyer_id = t2.Id");
    }

    [Fact]
    public void Alias_root_is_refused_when_the_receiver_already_has_a_join()
    {
        using var ctx = new TestContext();
        var joined = ctx.From<Order>().Join(ctx.From<Person>(), (o, p) => o.BuyerId == p.Id);

        // Negative: '.WithAlias' is root-only; the runtime seam keeps the same guard as a failsafe.
        Action act = () => joined.AliasRoot<EntityBuilder<RootProjection<Order>>, RootProjection<Order>>(
            static dc => new EntityBuilder<RootProjection<Order>>(dc));

        act.Should().Throw<NotSupportedException>().WithMessage("*before any Join*");
    }

    // ------------------------------------------------------------------ in-memory refusal ---------

    [Fact]
    public void Root_alias_fails_closed_on_the_in_memory_provider()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<Order>().AliasRoot<EntityBuilder<RootProjection<Order>>, RootProjection<Order>>(
            static dc => new EntityBuilder<RootProjection<Order>>(dc));

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory provider*");
    }

    [Fact]
    public void Pure_positional_in_memory_query_is_not_refused()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<Order>().Join(ctx.From<Person>(), (o, p) => o.BuyerId == p.Id).Select(p => p.Item2.Id);

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------ cache / plan --------------

    [Fact]
    public void Repeated_root_projection_preparation_reuses_the_plan_without_sticky_cache_mutation()
    {
        using var ctx = new TestContext();
        var command = Rooted(ctx).Select(p => p.Root.Id);
        command.Cache.Should().BeTrue();

        var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);

        second.Should().BeSameAs(first);
        command.Cache.Should().BeTrue();
    }

    [Fact]
    public void Different_root_projection_shapes_do_not_share_a_plan()
    {
        using var ctx = new TestContext();

        var ids = SqlOf(ctx, Rooted(ctx).Select(p => p.Root.Id));
        var buyerIds = SqlOf(ctx, Rooted(ctx).Select(p => p.Root.BuyerId));

        ids.Should().NotBe(buyerIds);
    }

    // ------------------------------------------------------------------ provider-free context -----

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override bool SupportsRawSqlSource => true;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => new FakeParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString) => new FakeConnection();
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

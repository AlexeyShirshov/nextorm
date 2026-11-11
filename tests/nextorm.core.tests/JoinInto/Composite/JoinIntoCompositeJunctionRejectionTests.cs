using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D03 (#223) update of the T213 D04-R rejection oracle: the composite-junction many-to-many
/// <c>JoinInto</c> SQL link source is now <b>supported</b> on the entity-item path, so the formerly
/// rejected shape must prepare without failing closed and without opening a connection. Only the
/// genuinely malformed shapes remain rejected at preparation: a junction whose foreign keys do not cover
/// every composite key component still fails closed deterministically. The scalar junction path is a
/// control: it is not rejected.
/// <para>
/// Runs in the "Query cache controls" collection because it writes the process-wide
/// <see cref="DataContextCache"/> and must not race other metadata tests.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class JoinIntoCompositeJunctionRejectionTests
{
    public JoinIntoCompositeJunctionRejectionTests()
    {
        DataContextCache.Clear();
    }

    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    public sealed class CompositeJunctionParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<CompositeJunctionChild> Children { get; set; } = new List<CompositeJunctionChild>();
    }

    public sealed class CompositeJunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class CompositeJunctionLink
    {
        public int ParentTenantId { get; set; }
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    private static void RegisterCompositeJunctionParent()
    {
        var parentProps = Props<CompositeJunctionParent>();
        var childProps = Props<CompositeJunctionChild>();
        var linkProps = Props<CompositeJunctionLink>();
        var parentTenantKey = Prop(parentProps, nameof(CompositeJunctionParent.TenantId)).PropertyInfo;
        var parentKey = Prop(parentProps, nameof(CompositeJunctionParent.Id)).PropertyInfo;
        var childKey = Prop(childProps, nameof(CompositeJunctionChild.Id)).PropertyInfo;
        var navigation = typeof(CompositeJunctionParent).GetProperty(nameof(CompositeJunctionParent.Children));

        DataContextCache.Metadata[typeof(CompositeJunctionParent)] = new EntityMetadata(
            typeof(CompositeJunctionParent).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(CompositeJunctionParent),
                    typeof(CompositeJunctionChild),
                    navigation,
                    isCollection: true,
                    [childKey],
                    [parentTenantKey, parentKey],
                    typeof(CompositeJunctionChild),
                    typeof(CompositeJunctionParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(CompositeJunctionLink),
                        ParentKey = [parentTenantKey, parentKey],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(CompositeJunctionLink.ParentTenantId)).PropertyInfo,
                            Prop(linkProps, nameof(CompositeJunctionLink.ParentId)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(CompositeJunctionLink.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    [Fact]
    public void CompositeJunctionManyToMany_SqlPath_ShouldPrepareWithoutOpeningConnectionOrExecutingSql()
    {
        RegisterCompositeJunctionParent();
        using var ctx = new TrackingContext();

        // D03 (#223): the composite-junction link source is now supported on the entity-item path, so
        // building the declaration must no longer fail closed. The junction declares two distinct
        // foreign-key components that pair positionally with the two-component parent key. Preparation
        // must render the composite link SQL without opening a connection or executing anything: it
        // allocates the command's connection holder but never opens it, so the assertion is zero opened
        // connections and zero executed commands.
        var builder = ctx.From<CompositeJunctionParent>()
            .JoinInto(ctx.From<CompositeJunctionChild>(), (p, c) => true, p => p.Children);

        ctx.ConnectionsRequested.Should().Be(0, "declaring the composite-junction relationship must not allocate a connection");

        Action act = () => ctx.GetPreparedQueryCommand(
            builder.ToCommand(), createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

        act.Should().NotThrow("the composite-junction M:N link source is supported again (#223)");
        ctx.ConnectionsOpened.Should().Be(0, "preparation renders the SQL without opening the connection");
        ctx.CommandsExecuted.Should().Be(0, "preparation must not execute any SQL");
    }

    public sealed class OrderedJunctionParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<OrderedJunctionChild> Children { get; set; } = new List<OrderedJunctionChild>();
    }

    public sealed class OrderedJunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class OrderedJunctionLink
    {
        public int ParentTenant { get; set; }
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    /// <summary>
    /// A several-times-injected relationship whose <see cref="PrincipalKey"/> order is supplied
    /// independently of the junction's declared key order, so the test can present a same-typed
    /// colliding/reversed declaration the concrete <c>RelationshipMetadata</c> cannot express.
    /// </summary>
    private sealed class SwappedKeyManyToMany : IRelationshipMetadata
    {
        private readonly RelationshipJunctionMetadata _junction;

        public SwappedKeyManyToMany(
            PropertyInfo? navigation,
            Type relatedType,
            IReadOnlyList<IPropertyMetadata> principalKey,
            IReadOnlyList<IPropertyMetadata> foreignKey,
            RelationshipJunctionMetadata junction)
        {
            Navigation = navigation;
            RelatedType = relatedType;
            PrincipalKey = principalKey;
            ForeignKey = foreignKey;
            _junction = junction;
        }

        public RelationshipKind Kind => RelationshipKind.ManyToMany;

        public Type DeclaringType => typeof(OrderedJunctionParent);

        public Type RelatedType { get; }

        public PropertyInfo? Navigation { get; }

        public bool IsCollection => true;

        public IReadOnlyList<IPropertyMetadata> ForeignKey { get; }

        public IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }

        public IRelationshipMetadata? Inverse => null;

        public RelationshipJunctionMetadata? Junction => _junction;
    }

    [Fact]
    public void CompositeJunctionReversedPrincipalKeyOrder_ContradictingJunctionDeclaration_ShouldFailClosedBeforeConnection()
    {
        // AC01 negative rule: reversing the declared component order must not silently produce a
        // valid-looking wrong result. Two same-typed (int) parent key components are declared with the
        // relationship's PrincipalKey in the reversed order [Id, TenantId] while the junction's declared
        // correspondence pairs [TenantId, Id]. Under a blind positional pairing this renders a
        // self-consistent but cross-joining predicate (Id = ParentTenant, TenantId = ParentId) and
        // silently links the wrong parents; the declared correspondence cannot be established, so it must
        // fail closed during preparation, before any connection.
        var parentProps = Props<OrderedJunctionParent>();
        var childProps = Props<OrderedJunctionChild>();
        var linkProps = Props<OrderedJunctionLink>();
        var tenant = Prop(parentProps, nameof(OrderedJunctionParent.TenantId));
        var id = Prop(parentProps, nameof(OrderedJunctionParent.Id));
        var childKey = Prop(childProps, nameof(OrderedJunctionChild.Id));
        var navigation = typeof(OrderedJunctionParent).GetProperty(nameof(OrderedJunctionParent.Children));

        var junction = new RelationshipJunctionMetadata(
            typeof(OrderedJunctionLink),
            parentKey: [tenant, id],
            childKey: [childKey],
            junctionParentForeignKey:
            [
                Prop(linkProps, nameof(OrderedJunctionLink.ParentTenant)),
                Prop(linkProps, nameof(OrderedJunctionLink.ParentId)),
            ],
            junctionChildForeignKey: [Prop(linkProps, nameof(OrderedJunctionLink.ChildId))]);

        DataContextCache.Metadata[typeof(OrderedJunctionParent)] = new EntityMetadata(
            typeof(OrderedJunctionParent).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new SwappedKeyManyToMany(
                    navigation,
                    typeof(OrderedJunctionChild),
                    principalKey: [id, tenant],
                    foreignKey: [childKey],
                    junction),
            ]);

        using var ctx = new TrackingContext();

        Action act = () => ctx.From<OrderedJunctionParent>()
            .JoinInto(ctx.From<OrderedJunctionChild>(), (p, c) => true, p => p.Children);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*component correspondence cannot be established*");
        ctx.ConnectionsRequested.Should().Be(0, "the component-correspondence check runs at declaration, before any connection");
    }

    public sealed class ScalarJunctionParent
    {
        public int Id { get; set; }
        public ICollection<ScalarJunctionChild> Children { get; set; } = new List<ScalarJunctionChild>();
    }

    public sealed class ScalarJunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class ScalarJunctionLink
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    [Fact]
    public void ScalarJunctionManyToMany_SqlPath_ShouldNotBeRejected()
    {
        using var ctx = new TrackingContext();
        ctx.From<ScalarJunctionParent>(b => b.HasManyThrough<ScalarJunctionChild, ScalarJunctionLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        ctx.From<ScalarJunctionChild>();

        // The rejection is specific to a composite junction key: a scalar junction still builds its
        // derived row_number() SQL link source.
        Action act = () => ctx.From<ScalarJunctionParent>()
            .JoinInto(ctx.From<ScalarJunctionChild>(), (p, c) => true, p => p.Children);

        act.Should().NotThrow("the scalar junction link source is not rejected");
    }

    [Fact]
    public void CompositeJunctionWhoseForeignKeysDoNotCoverEveryKeyComponent_ShouldThrowDeterministically()
    {
        // AC01 negative: a composite relationship whose junction foreign key covers fewer components than
        // the announced key must fail closed at declaration, never degrade into a partial-key join. The
        // relationship announces a two-component principal key ([TenantId, Id]) while the junction parent
        // foreign key has one component. The former fixture expressed the second key component by repeating
        // Id, which the shared structural validator now rejects earlier as a duplicate component; a
        // distinct two-component key is the malformed shape this test means to present, and it is rejected
        // by the same structural arity rule the #223 coverage guard used to enforce alone.
        var parentProps = Props<CompositeJunctionParent>();
        var childProps = Props<CompositeJunctionChild>();
        var linkProps = Props<CompositeJunctionLink>();
        var tenant = Prop(parentProps, nameof(CompositeJunctionParent.TenantId)).PropertyInfo;
        var parentKey = Prop(parentProps, nameof(CompositeJunctionParent.Id)).PropertyInfo;
        var childKey = Prop(childProps, nameof(CompositeJunctionChild.Id)).PropertyInfo;
        var navigation = typeof(CompositeJunctionParent).GetProperty(nameof(CompositeJunctionParent.Children));

        DataContextCache.Metadata[typeof(CompositeJunctionParent)] = new EntityMetadata(
            typeof(CompositeJunctionParent).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(CompositeJunctionParent),
                    typeof(CompositeJunctionChild),
                    navigation,
                    isCollection: true,
                    [childKey],
                    [tenant, parentKey],
                    typeof(CompositeJunctionChild),
                    typeof(CompositeJunctionParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(CompositeJunctionLink),
                        ParentKey = [tenant, parentKey],
                        ChildKey = [childKey],
                        JunctionParentForeignKey = [Prop(linkProps, nameof(CompositeJunctionLink.ParentId)).PropertyInfo],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(CompositeJunctionLink.ChildId)).PropertyInfo],
                    }),
            ]);

        using var ctx = new TrackingContext();

        Action act = () => ctx.From<CompositeJunctionParent>()
            .JoinInto(ctx.From<CompositeJunctionChild>(), (p, c) => true, p => p.Children);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*2 parent-key component(s) but 1 parent junction foreign-key component(s)*");
        ctx.ConnectionsRequested.Should().Be(0, "a malformed junction key is rejected at declaration; no SQL is executed");
    }

    // ---------------------------------------------------------------------------------------------
    // Provider-free SQL context: it records connection allocation/open and command execution so a test
    // can prove a declaration/guard path touches no connection and a supported preparation opens none
    // and executes no SQL.
    // ---------------------------------------------------------------------------------------------

    private sealed class TrackingContext : DataContext
    {
        public TrackingContext() : base(new DataContextBuilder())
        {
        }

        /// <summary>Connections allocated for command holders (a command needs one but need not open it).</summary>
        public int ConnectionsRequested { get; private set; }

        /// <summary>Connections actually opened; neither a fail-closed guard nor a supported preparation opens one.</summary>
        public int ConnectionsOpened { get; private set; }

        /// <summary>Commands executed; no assertion path must execute SQL.</summary>
        public int CommandsExecuted { get; private set; }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => new TrackingParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString)
        {
            ConnectionsRequested++;
            return new TrackingConnection(this);
        }

        private sealed class TrackingConnection(TrackingContext owner) : DbConnection
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

            public override void Open() => owner.ConnectionsOpened++;

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

            protected override DbCommand CreateDbCommand() => new TrackingCommand(owner);
        }

        private sealed class TrackingCommand(TrackingContext owner) : DbCommand
        {
            private readonly TrackingParameterCollection _parameters = new();

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

            protected override DbParameter CreateDbParameter() => new TrackingParameter(string.Empty);

            public override void Cancel()
            {
            }

            public override int ExecuteNonQuery() => Fail();

            public override object ExecuteScalar() => Fail();

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => FailReader();

            public override void Prepare()
            {
            }

            private int Fail()
            {
                owner.CommandsExecuted++;
                throw new NotSupportedException("no SQL must be executed for a composite-junction M:N JoinInto assertion");
            }

            private DbDataReader FailReader()
            {
                owner.CommandsExecuted++;
                throw new NotSupportedException("no SQL must be executed for a composite-junction M:N JoinInto assertion");
            }
        }

        private sealed class TrackingParameterCollection : DbParameterCollection
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

            public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);

            public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

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

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override bool SupportsRawSqlSource => true;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TrackingParameter(string name) : DbParameter
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
}

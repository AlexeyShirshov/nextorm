using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Per-join options applied while the join is declared, through the trailing
/// <c>Action&lt;JoinOptions&gt;</c> parameter of the <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/
/// <c>FullJoin</c>/<c>SemiJoin</c>/<c>AntiJoin</c>/<c>PasteJoin</c>/<c>CrossJoin</c>/<c>CrossApply</c>/
/// <c>OuterApply</c> and <c>JoinInto</c> overloads
/// (for example <c>a.Join(b, (x, y) =&gt; x.Id == y.Id, j =&gt; j.WithStrictness(JoinStrictness.Any))</c>).
/// The values are copied into the resulting join, so the options object is not retained. The raw
/// named-table (<c>TableAlias</c>) <c>CrossJoin</c>/<c>CrossApply</c>/<c>OuterApply</c> overloads take
/// no options: those joins cannot carry the modifiers described here.
/// </summary>
public sealed class JoinOptions
{
    internal JoinStrictness? Strictness { get; private set; }
    internal bool IsGlobal { get; private set; }
    internal string? JoinHint { get; private set; }
    internal IReadOnlyList<string>? TableHints { get; private set; }
    internal bool CartesianWarningSuppressed { get; private set; }

    /// <summary>
    /// The relationship configured locally for this join, or <see langword="null"/> to fall back to the
    /// declared relationship metadata. A local configuration fully replaces the metadata for the calling
    /// <c>JoinInto</c> and works without registration.
    /// </summary>
    internal RelationshipMetadata? Relationship { get; private set; }

    /// <summary>
    /// Applies a ClickHouse join modifier (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>) to this join, for
    /// example <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>. Requires a dialect that supports it
    /// (see <see cref="ISqlDialect.SupportsJoinStrictness"/>).
    /// </summary>
    /// <param name="strictness">The strictness modifier to apply to this join.</param>
    /// <returns>This instance, to allow chaining.</returns>
    internal JoinOptions WithStrictness(JoinStrictness strictness)
    {
        Strictness = strictness;
        return this;
    }

    /// <summary>
    /// Marks this join as the ClickHouse <c>GLOBAL</c> variant (the right-hand side is resolved once
    /// and broadcast, for distributed queries), for example <c>j =&gt; j.Global()</c>. Requires a
    /// dialect that supports it (see <see cref="ISqlDialect.SupportsGlobalJoin"/>).
    /// </summary>
    /// <returns>This instance, to allow chaining.</returns>
    internal JoinOptions Global()
    {
        IsGlobal = true;
        return this;
    }

    /// <summary>
    /// Attaches a provider-specific hint to this join, for example SQL Server
    /// <c>j =&gt; j.WithJoinHint("loop")</c> which renders <c>inner loop join</c>. On
    /// PostgreSQL/MySQL/MariaDB the hint is folded into the statement-level <c>/*+ ... */</c> comment
    /// (append the aliases yourself, e.g. <c>NestLoop(t1 t2)</c>); a dialect that supports neither form
    /// rejects the command with <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="hint">The join hint text; must be non-empty.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="hint"/> is null, empty or whitespace.</exception>
    public JoinOptions WithJoinHint(string hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            throw new ArgumentException("A join hint must be a non-empty string.", nameof(hint));

        JoinHint = hint;
        return this;
    }

    /// <summary>
    /// Suppresses the <c>JoinInto.MultipleCollections</c> warning that is emitted when a query declares
    /// two or more collection navigations (including many-to-many declarations), whose joins multiply
    /// the parent rows. Suppression applies only to this join, but the warning is silenced for the whole
    /// query as soon as any declared join sets it. The flag is never part of the plan key and never
    /// throws; it only affects the diagnostic.
    /// </summary>
    /// <returns>This instance, to allow chaining.</returns>
    public JoinOptions SuppressCartesianWarning()
    {
        CartesianWarningSuppressed = true;
        return this;
    }

    /// <summary>
    /// Attaches table-level hints to this join, for example SQL Server
    /// <c>j =&gt; j.WithJoinTableHint("nolock")</c> which renders a <c>WITH (nolock)</c> clause on that
    /// joined table only. This is the per-join counterpart of <see cref="FromOptions.WithTableHint"/> and
    /// is distinct from the optimizer join hint <see cref="WithJoinHint(string)"/>; unlike
    /// <see cref="EntityBuilder{TEntity}.WithTablesInScopeHint"/> it never leaks to the other physical
    /// tables. Hints are rendered verbatim, so only pass trusted values; null/blank entries are ignored,
    /// and a call that supplies only ignored entries leaves the hints unchanged. Requires a dialect that
    /// supports table hints (see <see cref="ISqlDialect.SupportsTableHints"/>) and a physical-table join
    /// source (an APPLY, derived-table, table-valued-function or XML/pivot join source is rejected).
    /// </summary>
    /// <param name="hints">The table hints to apply to the joined table (for example <c>nolock</c>).</param>
    /// <returns>This instance, to allow chaining.</returns>
    public JoinOptions WithJoinTableHint(params string[] hints)
    {
        // Normalize blank-only input to null: a call that supplies no usable hint must not clear hints
        // attached by an earlier call, mirroring FromOptions.WithTableHint.
        var filtered = hints is { Length: > 0 }
            ? hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray()
            : [];

        if (filtered.Length > 0)
            TableHints = filtered;

        return this;
    }

    /// <summary>
    /// Configures the relationship for a one-to-one <c>JoinInto</c> call locally, without declaring it in
    /// metadata: the parent-side key on <typeparamref name="TParent"/> and the unique foreign key on
    /// <typeparamref name="TChild"/>. The configuration fully replaces the declared relationship for this
    /// call.
    /// </summary>
    /// <typeparam name="TParent">The parent entity type of the calling <c>JoinInto</c>.</typeparam>
    /// <typeparam name="TChild">The child entity type of the calling <c>JoinInto</c>.</typeparam>
    /// <typeparam name="TKey">The property type shared by the parent key and the child foreign key.</typeparam>
    /// <param name="parentKey">Selects the parent-side key property, for example <c>p =&gt; p.Id</c>.</param>
    /// <param name="childForeignKey">Selects the foreign-key property on the child, for example <c>c =&gt; c.ParentId</c>.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A relationship was already configured on this instance.</exception>
    public JoinOptions OneToOne<TParent, TChild, TKey>(
        Expression<Func<TParent, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childForeignKey)
        where TChild : class
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(childForeignKey);
        EnsureNoRelationshipConfigured();

        var parentKeyProperty = ResolveSelectedProperty(parentKey, nameof(parentKey));
        var childForeignKeyProperty = ResolveSelectedProperty(childForeignKey, nameof(childForeignKey));

        Relationship = new RelationshipMetadata(
            RelationshipKind.OneToOne,
            typeof(TParent),
            typeof(TChild),
            navigation: null,
            isCollection: false,
            childForeignKeyProperty,
            parentKeyProperty,
            typeof(TChild),
            typeof(TParent));

        return this;
    }

    /// <summary>
    /// Configures a many-to-many relationship for a collection <c>JoinInto</c> call locally, without
    /// declaring it in metadata: the keys on both principal sides and the two junction foreign keys that
    /// reference them. The configuration fully replaces the declared relationship for this call.
    /// </summary>
    /// <typeparam name="TParent">The parent entity type of the calling <c>JoinInto</c>.</typeparam>
    /// <typeparam name="TChild">The child entity type of the calling <c>JoinInto</c>.</typeparam>
    /// <typeparam name="TJunction">The junction (link) entity type.</typeparam>
    /// <typeparam name="TParentKey">The property type shared by the parent key and the junction parent foreign key.</typeparam>
    /// <typeparam name="TChildKey">The property type shared by the child key and the junction child foreign key.</typeparam>
    /// <param name="parentKey">Selects the parent-side key property, for example <c>p =&gt; p.Id</c>.</param>
    /// <param name="junctionParentForeignKey">Selects the junction foreign key referencing the parent key, for example <c>l =&gt; l.ParentId</c>.</param>
    /// <param name="childKey">Selects the child-side key property, for example <c>c =&gt; c.Id</c>.</param>
    /// <param name="junctionChildForeignKey">Selects the junction foreign key referencing the child key, for example <c>l =&gt; l.ChildId</c>.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A relationship was already configured on this instance.</exception>
    /// <exception cref="NotSupportedException">A key does not match its junction foreign key's type.</exception>
    public JoinOptions ManyToMany<TParent, TChild, TJunction, TParentKey, TChildKey>(
        Expression<Func<TParent, TParentKey>> parentKey,
        Expression<Func<TJunction, TParentKey>> junctionParentForeignKey,
        Expression<Func<TChild, TChildKey>> childKey,
        Expression<Func<TJunction, TChildKey>> junctionChildForeignKey)
        where TChild : class
        where TJunction : class
    {
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(junctionParentForeignKey);
        ArgumentNullException.ThrowIfNull(childKey);
        ArgumentNullException.ThrowIfNull(junctionChildForeignKey);
        EnsureNoRelationshipConfigured();

        var parentKeyProperty = ResolveSelectedProperty(parentKey, nameof(parentKey));
        var junctionParentForeignKeyProperty = ResolveSelectedProperty(junctionParentForeignKey, nameof(junctionParentForeignKey));
        var childKeyProperty = ResolveSelectedProperty(childKey, nameof(childKey));
        var junctionChildForeignKeyProperty = ResolveSelectedProperty(junctionChildForeignKey, nameof(junctionChildForeignKey));

        RelationshipResolver.ValidateJunctionKeyType(typeof(TParent).Name, nameof(parentKey), parentKeyProperty, nameof(junctionParentForeignKey), junctionParentForeignKeyProperty);
        RelationshipResolver.ValidateJunctionKeyType(typeof(TParent).Name, nameof(childKey), childKeyProperty, nameof(junctionChildForeignKey), junctionChildForeignKeyProperty);

        Relationship = new RelationshipMetadata(
            RelationshipKind.ManyToMany,
            typeof(TParent),
            typeof(TChild),
            navigation: null,
            isCollection: true,
            childKeyProperty,
            parentKeyProperty,
            typeof(TChild),
            typeof(TParent),
            new RelationshipJunctionDeclaration
            {
                JunctionType = typeof(TJunction),
                ParentKey = [parentKeyProperty],
                ChildKey = [childKeyProperty],
                JunctionParentForeignKey = [junctionParentForeignKeyProperty],
                JunctionChildForeignKey = [junctionChildForeignKeyProperty],
            });

        return this;
    }

    private void EnsureNoRelationshipConfigured()
    {
        if (Relationship is not null)
            throw new InvalidOperationException(
                "A JoinInto call accepts at most one relationship configuration; a repeated or mixed local configuration is not supported.");
    }

    private static PropertyInfo ResolveSelectedProperty(LambdaExpression selector, string parameterName)
    {
        var body = selector.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            body = conversion.Operand;

        return body is MemberExpression { Member: PropertyInfo property }
            ? property
            : throw new NotSupportedException($"The '{parameterName}' selector must select a property.");
    }
}

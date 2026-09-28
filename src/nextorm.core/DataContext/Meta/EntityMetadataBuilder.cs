using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent builder, passed to <c>From&lt;T&gt;(...)</c>, that declares the table name and column
/// mappings of an entity type.
/// </summary>
public class EntityMetadataBuilder<T>
{
    private readonly IList<EntityPropertyBuilder<T>> _props = new List<EntityPropertyBuilder<T>>();
    private readonly List<IQueryFilterMetadata> _filters = new();
    private readonly List<RelationshipDeclaration> _relationships = new();
    private string? _tableName;

    /// <summary>
    /// Builds the metadata from the mappings declared on this builder, auto-deriving the table name
    /// and, when no properties were declared, the property mappings from the CLR type. Declaring even
    /// one property fluently maps <b>only</b> the declared properties: the CLR attributes on the other
    /// properties are not auto-built (they would otherwise need merging with the fluent overrides).
    /// </summary>
    /// <returns>The resolved entity metadata.</returns>
    public IEntityMetadata Build()
    {
        var (tableName, isTableNameAuto) = string.IsNullOrEmpty(_tableName)
            ? AutoBuildTableName()
            : (_tableName, false);

        var relationships = BuildRelationships(includeFluent: true);
        var navigationProperties = GetNavigationProperties(relationships);

        var properties = _props.Count == 0
            ? AutoBuildProperties(out var store, navigationProperties)
            : BuildDeclaredProperties(out store, navigationProperties);

        return CreateMetadata(tableName, isTableNameAuto, properties, store, BuildFilters(includeFluent: true), relationships);
    }
    /// <summary>
    /// Builds the metadata entirely by reflecting over the CLR type, ignoring any table or property
    /// mappings declared on this builder. Attribute-declared relationships are still read (they are part
    /// of the CLR type); fluent relationship declarations are not.
    /// </summary>
    /// <returns>The auto-derived entity metadata.</returns>
    public IEntityMetadata AutoBuild()
    {
        var relationships = BuildRelationships(includeFluent: false);
        var navigationProperties = GetNavigationProperties(relationships);

        var propsMeta = AutoBuildProperties(out var store, navigationProperties);

        var (tableName, isTableNameAuto) = AutoBuildTableName();

        return CreateMetadata(tableName, isTableNameAuto, propsMeta, store, BuildFilters(includeFluent: false), relationships);
    }

    private List<IPropertyMetadata> BuildDeclaredProperties(out IPropertyMetadata? dynamicColumnsStore, HashSet<PropertyInfo> navigationProperties)
    {
        var list = new List<IPropertyMetadata>(_props.Count);
        dynamicColumnsStore = null;
        for (var i = 0; i < _props.Count; i++)
        {
            var property = _props[i].Build();
            if (navigationProperties.Contains(property.PropertyInfo))
                throw new InvalidOperationException($"The navigation property '{typeof(T).Name}.{property.PropertyInfo.Name}' participates in a declared relationship and cannot also be mapped as a column.");
            if (property.IsDynamicColumnsStore)
            {
                if (dynamicColumnsStore is not null)
                    throw new InvalidOperationException($"The entity {typeof(T).Name} declares more than one dynamic-columns store.");
                dynamicColumnsStore = property;
                continue;
            }

            list.Add(property);
        }

        return list;
    }

    private IEntityMetadata CreateMetadata(string? tableName, bool isTableNameAuto, List<IPropertyMetadata> properties, IPropertyMetadata? dynamicColumnsStore, IReadOnlyList<IQueryFilterMetadata> filters, IReadOnlyList<IRelationshipMetadata> relationships)
    {
        dynamicColumnsStore ??= FindAttributeStore(properties);
        if (dynamicColumnsStore is not null)
            ValidateDynamicColumnsStore(typeof(T), dynamicColumnsStore.PropertyInfo);

        return new EntityMetadata(tableName, properties, isTableNameAuto, dynamicColumnsStore, filters, relationships);
    }

    private IReadOnlyList<IQueryFilterMetadata> BuildFilters(bool includeFluent)
    {
        var filters = new List<IQueryFilterMetadata>();

        if (includeFluent)
            filters.AddRange(_filters);

        foreach (var attribute in typeof(T).GetCustomAttributes<QueryFilterAttribute>(true))
        {
            var lambda = ResolveFilterLambda(attribute.FilterLambda);
            if (lambda is not null)
                filters.Add(new QueryFilterMetadata(null, lambda));
        }

        return filters;
    }

    private static LambdaExpression? ResolveFilterLambda(string? memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            throw new InvalidOperationException($"The {nameof(QueryFilterAttribute)} on {typeof(T).Name} must name a static member in {nameof(QueryFilterAttribute.FilterLambda)}.");

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var members = typeof(T).GetMember(memberName, flags);
        if (members.Length == 0)
            throw new InvalidOperationException($"The static member '{memberName}' named by {nameof(QueryFilterAttribute)} was not found on {typeof(T).Name}.");

        var value = members[0] switch
        {
            PropertyInfo property => property.GetValue(null),
            FieldInfo field => field.GetValue(null),
            MethodInfo method when method.GetParameters().Length == 0 => method.Invoke(null, null),
            _ => throw new InvalidOperationException($"The member '{memberName}' named by {nameof(QueryFilterAttribute)} on {typeof(T).Name} must be a static field, a static property or a parameterless static method."),
        };

        return value as LambdaExpression
            ?? throw new InvalidOperationException($"The static member '{memberName}' named by {nameof(QueryFilterAttribute)} on {typeof(T).Name} must return a {nameof(LambdaExpression)}.");
    }

    private static IPropertyMetadata? FindAttributeStore(List<IPropertyMetadata> properties)
    {
        var entityType = typeof(T);
        var props = entityType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? storeProperty = null;
        foreach (var prop in props)
        {
            var intProp = FindInterfaceProperty(entityType, prop);
            var attr = prop.GetCustomAttribute<DynamicColumnsAttribute>(true) ?? intProp?.GetCustomAttribute<DynamicColumnsAttribute>(true);
            if (attr is null)
                continue;

            if (storeProperty is not null)
                throw new InvalidOperationException($"The entity {entityType.Name} declares more than one dynamic-columns store.");

            storeProperty = prop;
        }

        if (storeProperty is null)
            return null;

        foreach (var existing in properties)
        {
            if (existing.PropertyInfo == storeProperty)
                throw new InvalidOperationException($"The dynamic-columns store property '{storeProperty.Name}' of {entityType.Name} is declared with {nameof(DynamicColumnsAttribute)} and cannot also have a fluent column mapping.");
        }

        return new PropertyMetadata { ColumnName = storeProperty.Name, PropertyInfo = storeProperty, IsColumnNameAuto = true, IsDynamicColumnsStore = true };
    }

    private static void ValidateDynamicColumnsStore(Type entityType, PropertyInfo property)
    {
        if (property.GetSetMethod() is null)
            throw new InvalidOperationException($"The dynamic-columns store property '{property.Name}' of {entityType.Name} must have a public setter.");

        if (!DynamicColumnsTypeFacts.IsStoreType(property.PropertyType))
            throw new InvalidOperationException($"The dynamic-columns store property '{property.Name}' of {entityType.Name} must be a string-keyed dictionary of object? (for example Dictionary<string, object?>, IDictionary<string, object?> or IReadOnlyDictionary<string, object?>).");
    }

    private static List<IPropertyMetadata> AutoBuildProperties(out IPropertyMetadata? dynamicColumnsStore, HashSet<PropertyInfo>? navigationProperties = null)
    {
        var propsMeta = new List<IPropertyMetadata>();
        dynamicColumnsStore = null;

        var entityType = typeof(T);

        var props = entityType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance).ToArray();
        for (var (idx, cnt) = (0, props.Length); idx < cnt; idx++)
        {
            var prop = props[idx];
            if (prop is null) continue;

            // A property that participates in a declared relationship is a navigation, not a column:
            // it is exposed through IEntityMetadata.Relationships and never mapped. Only declared
            // navigations are skipped; an ordinary property keeps its current mapping.
            if (navigationProperties is not null && navigationProperties.Contains(prop))
                continue;

            // Attributes are declared on the interface for interface-mapped entities, so a property
            // without a direct mapping falls back to its matching interface property (mirrors the
            // former ColumnAttribute-only lookup).
            var intProp = FindInterfaceProperty(entityType, prop);
            var dynamicColumnsAttr = prop.GetCustomAttribute<DynamicColumnsAttribute>(true) ?? intProp?.GetCustomAttribute<DynamicColumnsAttribute>(true);
            if (dynamicColumnsAttr is not null)
            {
                if (dynamicColumnsStore is not null)
                    throw new InvalidOperationException($"The entity {entityType.Name} declares more than one dynamic-columns store.");
                ValidateDynamicColumnsStore(entityType, prop);
                dynamicColumnsStore = new PropertyMetadata { ColumnName = prop.Name, PropertyInfo = prop, IsColumnNameAuto = true, IsDynamicColumnsStore = true };
                continue;
            }

            if (!prop.CanWrite)
                continue;

            var colAttr = prop.GetCustomAttribute<ColumnAttribute>(true) ?? intProp?.GetCustomAttribute<ColumnAttribute>(true);
            var keyAttr = prop.GetCustomAttribute<KeyAttribute>(true) ?? intProp?.GetCustomAttribute<KeyAttribute>(true);
            var generatedAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>(true) ?? intProp?.GetCustomAttribute<DatabaseGeneratedAttribute>(true);
            var durationAttr = prop.GetCustomAttribute<DurationAttribute>(true) ?? intProp?.GetCustomAttribute<DurationAttribute>(true);
            var collationAttr = prop.GetCustomAttribute<CollationAttribute>(true) ?? intProp?.GetCustomAttribute<CollationAttribute>(true);
            var decimalPrecisionAttr = prop.GetCustomAttribute<DecimalPrecisionAttribute>(true) ?? intProp?.GetCustomAttribute<DecimalPrecisionAttribute>(true);
            var valueConverterAttr = prop.GetCustomAttribute<ValueConverterAttribute>(true) ?? intProp?.GetCustomAttribute<ValueConverterAttribute>(true);
            var jsonColumnAttr = prop.GetCustomAttribute<JsonColumnAttribute>(true) ?? intProp?.GetCustomAttribute<JsonColumnAttribute>(true);
            var rangeColumnsAttr = prop.GetCustomAttribute<RangeColumnsAttribute>(true) ?? intProp?.GetCustomAttribute<RangeColumnsAttribute>(true);
            var generated = generatedAttr?.DatabaseGeneratedOption ?? DatabaseGeneratedOption.None;

            if (valueConverterAttr is not null && jsonColumnAttr is not null)
                throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} cannot be mapped with both {nameof(ValueConverterAttribute)} and {nameof(JsonColumnAttribute)}.");

            if (rangeColumnsAttr is not null)
            {
                if (valueConverterAttr is not null || jsonColumnAttr is not null)
                    throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} cannot be mapped with both {nameof(RangeColumnsAttribute)} and a value/JSON converter.");

                if (!RangeTypeFacts.IsRange(prop.PropertyType))
                    throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} is mapped with {nameof(RangeColumnsAttribute)} but its type {prop.PropertyType} is not Range<T>.");

                if (!string.IsNullOrEmpty(colAttr?.Name))
                    throw new InvalidOperationException($"Property '{prop.Name}' of {entityType.Name} cannot be mapped with both {nameof(ColumnAttribute)} and {nameof(RangeColumnsAttribute)}; the range pair declares its own column names.");
            }

            // The value/JSON converter owns the provider representation, so the precision/scale pair is
            // validated against the type actually bound for the column, not the model property type: a
            // non-decimal model converted to decimal is accepted, a decimal model converted to text or a
            // JSON column is rejected.
            var converter = jsonColumnAttr is not null
                ? JsonColumnConverterFactory.Create(prop.PropertyType, jsonColumnAttr.Storage)
                : valueConverterAttr?.Create(prop.PropertyType);

            DecimalPrecisionRules.Validate(converter?.ProviderType ?? prop.PropertyType, prop.Name, decimalPrecisionAttr?.Precision, decimalPrecisionAttr?.Scale);

            var columnName = rangeColumnsAttr is not null
                ? rangeColumnsAttr.LowerColumn
                : !string.IsNullOrEmpty(colAttr?.Name) ? colAttr!.Name! : prop.Name;
            propsMeta.Add(new PropertyMetadata
            {
                ColumnName = columnName,
                PropertyInfo = prop,
                IsColumnNameAuto = rangeColumnsAttr is null && string.IsNullOrEmpty(colAttr?.Name),
                IsKey = keyAttr is not null,
                IsIdentity = generated == DatabaseGeneratedOption.Identity,
                IsComputed = generated == DatabaseGeneratedOption.Computed,
                DurationUnit = durationAttr?.Unit,
                DurationPrecision = durationAttr?.Precision ?? 0,
                DecimalPrecision = decimalPrecisionAttr?.Precision,
                DecimalScale = decimalPrecisionAttr?.Scale,
                Collation = collationAttr?.Name,
                Converter = converter,
                RangeColumns = rangeColumnsAttr?.ToMetadata(),
            });
        }

        InferKeyIfMissing(propsMeta, entityType.Name);

        return propsMeta;
    }

    private static PropertyInfo? FindInterfaceProperty(Type entityType, PropertyInfo prop)
    {
        if (entityType.IsInterface || prop.GetMethod is null)
            return null;

        foreach (var interf in entityType.GetInterfaces())
        {
            var intMap = entityType.GetInterfaceMap(interf);

            var implIdx = Array.IndexOf(intMap.TargetMethods, prop.GetMethod);
            if (implIdx >= 0)
            {
                var intMethod = intMap.InterfaceMethods[implIdx];
                var intProp = interf.GetProperties().FirstOrDefault(p => p.GetMethod == intMethod);
                if (intProp is not null)
                    return intProp;
            }
        }

        return null;
    }

    // Key convention when no property is declared with [Key]/.Key(): the property named "Id" or
    // "<TypeName>Id". Insert does not depend on the key, but update/delete (todo_update/todo_delete)
    // address a row through it.
    private static void InferKeyIfMissing(List<IPropertyMetadata> props, string typeName)
    {
        foreach (var p in props)
        {
            if (p.IsKey)
                return;
        }

        var idName = typeName + "Id";
        foreach (var candidate in new[] { "Id", idName })
        {
            foreach (var p in props)
            {
                if (string.Equals(p.PropertyInfo.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    ((PropertyMetadata)p).IsKey = true;
                    return;
                }
            }
        }
    }

    private static (string? TableName, bool IsAuto) AutoBuildTableName()
    {
        var entityType = typeof(T);
        string? tableName = entityType.Name;
        var isAuto = true;

        var sqlTableAttr = entityType.GetCustomAttribute<SqlTableAttribute>(true);

        if (sqlTableAttr is not null)
        {
            tableName = sqlTableAttr.Name;
            isAuto = false;
        }
        else
        {
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>(true);

            if (tableAttr is not null)
            {
                tableName = tableAttr.Name;
                isAuto = false;
            }
        }

        foreach (var interf in entityType.GetInterfaces())
        {
            sqlTableAttr = interf.GetCustomAttribute<SqlTableAttribute>(true);

            if (sqlTableAttr is not null)
            {
                tableName = sqlTableAttr.Name;
                isAuto = false;
            }
            else
            {
                var tableAttr = interf.GetCustomAttribute<TableAttribute>(true);

                if (tableAttr is not null)
                {
                    tableName = tableAttr.Name;
                    isAuto = false;
                }
            }
        }

        return (tableName, isAuto);
    }

    /// <summary>
    /// Declares a fluent mapping for the property selected by <paramref name="propertySelector"/>.
    /// </summary>
    /// <param name="propertySelector">Selects the property to map; the expression must produce a <see cref="PropertyInfo"/>.</param>
    /// <returns>A builder for the selected property's column mapping.</returns>
    public EntityPropertyBuilder<T> Property(Expression<Func<T, object>> propertySelector)
    {
        var pb = new EntityPropertyBuilder<T>(propertySelector);
        _props.Add(pb);
        return pb;
    }
    /// <summary>
    /// Overrides the table name for the entity instead of deriving it from the CLR type or attributes.
    /// </summary>
    /// <param name="tableName">The table name to map the entity to.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityMetadataBuilder<T> Table(string tableName)
    {
        _tableName = tableName;
        return this;
    }

    /// <summary>
    /// Declares the principal side of a one-to-many relationship: a collection navigation on this
    /// entity and the foreign-key property on the related (dependent) type. The principal key is taken
    /// from this entity's key metadata; it is resolved lazily and must exist by the time the keys are
    /// read.
    /// </summary>
    /// <typeparam name="TChild">The dependent entity type.</typeparam>
    /// <typeparam name="TKey">The property type shared by the foreign key and the principal key.</typeparam>
    /// <param name="navigation">Selects the collection navigation on this entity, for example <c>p =&gt; p.Children</c>.</param>
    /// <param name="foreignKey">Selects the foreign-key property on <typeparamref name="TChild"/>, for example <c>c =&gt; c.ParentId</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector does not select a property, or the navigation is not a collection of <typeparamref name="TChild"/>.</exception>
    public EntityMetadataBuilder<T> HasMany<TChild, TKey>(
        Expression<Func<T, ICollection<TChild>>> navigation,
        Expression<Func<TChild, TKey>> foreignKey)
        where TChild : class
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(foreignKey);

        var navigationProperty = ResolveSelectedProperty(navigation, nameof(navigation));
        if (!typeof(IEnumerable<TChild>).IsAssignableFrom(navigationProperty.PropertyType))
            throw new NotSupportedException($"The navigation '{typeof(T).Name}.{navigationProperty.Name}' must be a collection of {typeof(TChild).Name}.");

        var foreignKeyProperty = ResolveSelectedProperty(foreignKey, nameof(foreignKey));
        _relationships.Add(RelationshipDeclaration.Create(typeof(T), typeof(TChild), isCollection: true, navigationProperty, foreignKeyProperty, principalKey: null));

        return this;
    }

    /// <summary>
    /// Declares the principal side of a one-to-many relationship for an entity whose navigation is not
    /// modelled as a property: the foreign key is selected on the dependent type and the principal key
    /// on this entity, and no navigation is attached.
    /// </summary>
    /// <typeparam name="TChild">The dependent entity type.</typeparam>
    /// <typeparam name="TKey">The property type shared by the foreign key and the principal key.</typeparam>
    /// <param name="foreignKey">Selects the foreign-key property on <typeparamref name="TChild"/>, for example <c>c =&gt; c.ParentId</c>.</param>
    /// <param name="principalKey">Selects the principal-key property on this entity, for example <c>p =&gt; p.Id</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector does not select a property.</exception>
    public EntityMetadataBuilder<T> HasMany<TChild, TKey>(
        Expression<Func<TChild, TKey>> foreignKey,
        Expression<Func<T, TKey>> principalKey)
        where TChild : class
    {
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(principalKey);

        var foreignKeyProperty = ResolveSelectedProperty(foreignKey, nameof(foreignKey));
        var principalKeyProperty = ResolveSelectedProperty(principalKey, nameof(principalKey));
        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: true, foreignKeyProperty, principalKeyProperty));

        return this;
    }

    /// <summary>
    /// Declares the dependent side of a many-to-one relationship: a reference navigation on this entity
    /// and the foreign-key property on this same entity. The principal key is taken from the related
    /// entity's key metadata; it is resolved lazily and must exist by the time the keys are read.
    /// </summary>
    /// <typeparam name="TChild">The principal entity type.</typeparam>
    /// <typeparam name="TKey">The property type shared by the foreign key and the principal key.</typeparam>
    /// <param name="navigation">Selects the reference navigation on this entity, for example <c>c =&gt; c.Parent</c>.</param>
    /// <param name="foreignKey">Selects the foreign-key property on this entity, for example <c>c =&gt; c.ParentId</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector does not select a property, or the navigation is a value type or <see cref="string"/>.</exception>
    public EntityMetadataBuilder<T> HasOne<TChild, TKey>(
        Expression<Func<T, TChild?>> navigation,
        Expression<Func<T, TKey>> foreignKey)
        where TChild : class
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(foreignKey);

        var navigationProperty = ResolveSelectedProperty(navigation, nameof(navigation));
        if (navigationProperty.PropertyType == typeof(string) || navigationProperty.PropertyType.IsValueType)
            throw new NotSupportedException($"The navigation '{typeof(T).Name}.{navigationProperty.Name}' must be a reference-typed property.");

        var foreignKeyProperty = ResolveSelectedProperty(foreignKey, nameof(foreignKey));
        _relationships.Add(RelationshipDeclaration.Create(typeof(T), typeof(TChild), isCollection: false, navigationProperty, foreignKeyProperty, principalKey: null));

        return this;
    }

    /// <summary>
    /// Declares the dependent side of a many-to-one relationship for an entity whose navigation is not
    /// modelled as a property: the foreign key is selected on this entity and the principal key on the
    /// related type, and no navigation is attached.
    /// </summary>
    /// <typeparam name="TChild">The principal entity type.</typeparam>
    /// <typeparam name="TKey">The property type shared by the foreign key and the principal key.</typeparam>
    /// <param name="foreignKey">Selects the foreign-key property on this entity, for example <c>c =&gt; c.ParentId</c>.</param>
    /// <param name="principalKey">Selects the principal-key property on <typeparamref name="TChild"/>, for example <c>p =&gt; p.Id</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector does not select a property.</exception>
    public EntityMetadataBuilder<T> HasOne<TChild, TKey>(
        Expression<Func<T, TKey>> foreignKey,
        Expression<Func<TChild, TKey>> principalKey)
        where TChild : class
    {
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(principalKey);

        var foreignKeyProperty = ResolveSelectedProperty(foreignKey, nameof(foreignKey));
        var principalKeyProperty = ResolveSelectedProperty(principalKey, nameof(principalKey));
        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: false, foreignKeyProperty, principalKeyProperty));

        return this;
    }

    /// <summary>
    /// Declares a global query filter that is applied to every query in which the entity participates
    /// (the primary source, joins and subqueries) unless the query calls <c>IgnoreFilters</c>. A
    /// repeated call on this builder adds another predicate; the declared filters are combined with
    /// <c>and</c>. The mapping is registered once per entity type: a later <c>From&lt;T&gt;(...)</c>
    /// configuration for the same type is ignored, so the first registration wins.
    /// </summary>
    /// <param name="filter">The filter predicate over the entity.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filters.Add(new QueryFilterMetadata(null, filter));
        return this;
    }

    /// <summary>
    /// Declares a global query filter that is applied to every query in which the entity participates
    /// (the primary source, joins and subqueries) unless the query calls <c>IgnoreFilters</c>. The
    /// predicate receives the executing <see cref="IDataContext"/> so it can read per-context state
    /// such as a tenant identifier. A repeated call on this builder adds another predicate; the
    /// declared filters are combined with <c>and</c>. The mapping is registered once per entity type:
    /// a later <c>From&lt;T&gt;(...)</c> configuration for the same type is ignored, so the first
    /// registration wins.
    /// </summary>
    /// <param name="filter">The filter predicate over the entity and the executing data context.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filters.Add(new QueryFilterMetadata(null, filter));
        return this;
    }

    // Fluent declarations come first, so a fluent relationship wins over an attribute on the same
    // navigation; the first declaration for a navigation property wins.
    private List<IRelationshipMetadata> BuildRelationships(bool includeFluent)
    {
        var declarations = new List<RelationshipDeclaration>();
        if (includeFluent)
            declarations.AddRange(_relationships);

        AddAttributeDeclarations(typeof(T), declarations);

        var result = new List<IRelationshipMetadata>(declarations.Count);
        var seen = new HashSet<PropertyInfo>();
        for (var i = 0; i < declarations.Count; i++)
        {
            var declaration = declarations[i];
            if (declaration.Navigation is not null && !seen.Add(declaration.Navigation))
                continue;

            result.Add(new RelationshipMetadata(
                declaration.Kind,
                typeof(T),
                declaration.RelatedType,
                declaration.Navigation,
                declaration.IsCollection,
                declaration.ForeignKey,
                declaration.PrincipalKey,
                declaration.DependentType,
                declaration.PrincipalType));
        }

        return result;
    }

    private static HashSet<PropertyInfo> GetNavigationProperties(List<IRelationshipMetadata> relationships)
    {
        var navigationProperties = new HashSet<PropertyInfo>(relationships.Count);
        for (var i = 0; i < relationships.Count; i++)
        {
            if (relationships[i].Navigation is PropertyInfo navigation)
                navigationProperties.Add(navigation);
        }

        return navigationProperties;
    }

    private static void AddAttributeDeclarations(Type entityType, List<RelationshipDeclaration> declarations)
    {
        var properties = entityType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var attribute = property.GetCustomAttribute<RelationshipAttribute>(true);
            if (attribute is null)
                continue;

            if (string.IsNullOrWhiteSpace(attribute.ForeignKey))
                throw new NotSupportedException($"The {nameof(RelationshipAttribute)} on '{entityType.Name}.{property.Name}' must name the foreign-key property in {nameof(RelationshipAttribute.ForeignKey)}.");

            if (TryGetCollectionElementType(property.PropertyType, out var elementType))
            {
                var foreignKey = elementType.GetProperty(attribute.ForeignKey, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    ?? throw new NotSupportedException($"The {nameof(RelationshipAttribute)} on '{entityType.Name}.{property.Name}' refers to the foreign key '{attribute.ForeignKey}' which was not found on {elementType.Name}.");
                declarations.Add(RelationshipDeclaration.Create(entityType, elementType, isCollection: true, property, foreignKey, principalKey: null));
            }
            else
            {
                var relatedType = property.PropertyType;
                if (relatedType == typeof(string) || relatedType.IsValueType)
                    throw new NotSupportedException($"The {nameof(RelationshipAttribute)} on '{entityType.Name}.{property.Name}' must be placed on a collection or a reference-typed navigation.");

                var foreignKey = entityType.GetProperty(attribute.ForeignKey, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    ?? throw new NotSupportedException($"The {nameof(RelationshipAttribute)} on '{entityType.Name}.{property.Name}' refers to the foreign key '{attribute.ForeignKey}' which was not found on {entityType.Name}.");
                declarations.Add(RelationshipDeclaration.Create(entityType, relatedType, isCollection: false, property, foreignKey, principalKey: null));
            }
        }
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        // string implements IEnumerable<char>, but it is a scalar, not a navigation collection; without
        // this guard a [Relationship] on a string property would be treated as a collection of char.
        if (type == typeof(string))
        {
            elementType = null!;
            return false;
        }

        if (TryGetGenericEnumerableElement(type, out elementType))
            return true;

        foreach (var @interface in type.GetInterfaces())
        {
            if (TryGetGenericEnumerableElement(@interface, out elementType))
                return true;
        }

        elementType = null!;
        return false;
    }

    private static bool TryGetGenericEnumerableElement(Type type, out Type elementType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    private static PropertyInfo ResolveSelectedProperty(LambdaExpression selector, string parameterName)
    {
        // Resolve the outermost member the selector addresses (x => x.A.B selects B, not A). A visitor
        // that records the last-visited member would resolve the innermost one (A) and silently bind the
        // wrong property; unwrapping conversions reaches the selected member directly.
        var body = selector.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            body = conversion.Operand;

        return body is MemberExpression { Member: PropertyInfo property }
            ? property
            : throw new NotSupportedException($"The '{parameterName}' selector on {typeof(T).Name} must select a property.");
    }

    private sealed class RelationshipDeclaration
    {
        public required RelationshipKind Kind { get; init; }
        public required Type RelatedType { get; init; }
        public required bool IsCollection { get; init; }
        public required PropertyInfo? Navigation { get; init; }
        public required PropertyInfo ForeignKey { get; init; }
        public PropertyInfo? PrincipalKey { get; init; }
        public required Type DependentType { get; init; }
        public required Type PrincipalType { get; init; }

        public static RelationshipDeclaration Create(Type declaringType, Type relatedType, bool isCollection, PropertyInfo navigation, PropertyInfo foreignKey, PropertyInfo? principalKey)
            => new()
            {
                Kind = isCollection ? RelationshipKind.OneToMany : RelationshipKind.ManyToOne,
                RelatedType = relatedType,
                IsCollection = isCollection,
                Navigation = navigation,
                ForeignKey = foreignKey,
                PrincipalKey = principalKey,
                DependentType = isCollection ? relatedType : declaringType,
                PrincipalType = isCollection ? declaringType : relatedType,
            };

        public static RelationshipDeclaration CreateFallback(Type declaringType, Type relatedType, bool isCollection, PropertyInfo foreignKey, PropertyInfo principalKey)
            => new()
            {
                Kind = isCollection ? RelationshipKind.OneToMany : RelationshipKind.ManyToOne,
                RelatedType = relatedType,
                IsCollection = isCollection,
                Navigation = null,
                ForeignKey = foreignKey,
                PrincipalKey = principalKey,
                DependentType = isCollection ? relatedType : declaringType,
                PrincipalType = isCollection ? declaringType : relatedType,
            };
    }
}

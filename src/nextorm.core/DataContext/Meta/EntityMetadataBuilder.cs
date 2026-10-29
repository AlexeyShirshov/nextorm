using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Fluent builder, passed to <c>From&lt;T&gt;(...)</c>, that declares the table name and column
/// mappings of an entity type.
/// </summary>
public class EntityMetadataBuilder<T>
{
    private readonly IList<EntityPropertyBuilder<T>> _props = new List<EntityPropertyBuilder<T>>();
    private readonly List<(string? Key, LambdaExpression? Lambda, Delegate? Func)> _filters = new();
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
        // Named filters occupy a slot: a repeated key replaces the earlier filter and a null lambda
        // removes it. Anonymous filters are additive — several anonymous predicates are combined with
        // `and`. Fluent declarations come first, so an attribute with the same key replaces the fluent
        // one (the fluent declaration order is the resolution order). Attributes are applied from the
        // base type down to the most derived one, so a derived declaration always wins a same-key
        // conflict against a base declaration (`derived overrides base`); a repeated key on the same
        // type is rejected because attribute order is not guaranteed (see EnumerateFilterAttributes).
        var filters = new List<IQueryFilterMetadata>();

        if (includeFluent)
        {
            foreach (var (key, lambda, func) in _filters)
                ApplyFilterDeclaration(filters, key, lambda, func);
        }

        foreach (var attribute in EnumerateFilterAttributes(typeof(T)))
        {
            var hasLambda = !string.IsNullOrWhiteSpace(attribute.FilterLambda);
            var hasFunc = !string.IsNullOrWhiteSpace(attribute.FilterFunc);

            if (hasLambda && hasFunc)
                throw new InvalidOperationException($"The {nameof(QueryFilterAttribute)} on {typeof(T).Name} must set either {nameof(QueryFilterAttribute.FilterLambda)} or {nameof(QueryFilterAttribute.FilterFunc)}, not both.");

            if (!hasLambda && !hasFunc)
            {
                if (string.IsNullOrWhiteSpace(attribute.FilterKey))
                    throw new InvalidOperationException($"The {nameof(QueryFilterAttribute)} on {typeof(T).Name} must name a static member in {nameof(QueryFilterAttribute.FilterLambda)} or {nameof(QueryFilterAttribute.FilterFunc)}.");

                ApplyFilterDeclaration(filters, attribute.FilterKey, null, null);
                continue;
            }

            if (hasLambda)
                ApplyFilterDeclaration(filters, attribute.FilterKey, ResolveFilterLambda(attribute.FilterLambda), null);
            else
                ApplyFilterDeclaration(filters, attribute.FilterKey, null, ResolveFilterFunc(attribute.FilterFunc));
        }

        return filters;
    }

    // Reflection does not guarantee the order of the attributes on one type, so "the last declaration
    // wins" cannot be made deterministic for two attributes that share a key on the same type. Such a
    // duplicate is rejected with a metadata exception instead. Attributes are still applied from the base
    // type down to the most derived one, so a same-key attribute on a derived type deterministically
    // overrides the inherited one (`derived overrides base`).
    private static IEnumerable<QueryFilterAttribute> EnumerateFilterAttributes(Type entityType)
    {
        var chain = new List<Type>();
        for (var type = entityType; type is not null && type != typeof(object); type = type.BaseType)
            chain.Add(type);

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            HashSet<string>? keys = null;
            foreach (var attribute in chain[i].GetCustomAttributes<QueryFilterAttribute>(inherit: false))
            {
                // Anonymous declarations (no key, or a whitespace key normalised to the anonymous slot)
                // are additive, so several on one type are valid; only a repeated named key is ambiguous.
                if (!string.IsNullOrWhiteSpace(attribute.FilterKey))
                {
                    keys ??= new HashSet<string>(StringComparer.Ordinal);
                    if (!keys.Add(attribute.FilterKey))
                        throw new InvalidOperationException(
                            $"The entity type '{chain[i].Name}' declares more than one {nameof(QueryFilterAttribute)} with the filter key '{attribute.FilterKey}'. The runtime does not guarantee attribute declaration order, so a repeated key on the same type has no deterministic winner; declare the key once, or override it on a derived type.");
                }

                yield return attribute;
            }
        }
    }

    private static void ApplyFilterDeclaration(List<IQueryFilterMetadata> filters, string? key, LambdaExpression? lambda, Delegate? func)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            if (lambda is not null || func is not null)
                filters.Add(new QueryFilterMetadata(QueryFilters.AnonymousKey, lambda, func));
            return;
        }

        var index = filters.FindIndex(f => string.Equals(f.Key, key, StringComparison.Ordinal));
        if (lambda is null && func is null)
        {
            if (index >= 0)
                filters.RemoveAt(index);
            return;
        }

        var metadata = new QueryFilterMetadata(key, lambda, func);
        if (index >= 0)
            filters[index] = metadata;
        else
            filters.Add(metadata);
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

    // Resolves the builder-function declaration named by QueryFilterAttribute.FilterFunc. Unlike
    // ResolveFilterLambda the delegate's entity type is not required to be exactly T: an attribute
    // inherited from a base type resolves its member through the derived type and may return a function
    // over the base entity, which is applied to the derived source by the usual parameter re-rooting.
    private static Delegate? ResolveFilterFunc(string? memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            throw new InvalidOperationException($"The {nameof(QueryFilterAttribute)} on {typeof(T).Name} must name a static member in {nameof(QueryFilterAttribute.FilterFunc)}.");

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

        if (value is not Delegate func)
            throw new InvalidOperationException($"The static member '{memberName}' named by {nameof(QueryFilterAttribute)} on {typeof(T).Name} must return a {nameof(Delegate)}.");

        var delegateType = func.GetType();
        var typeArguments = delegateType.GetGenericArguments();
        if (!delegateType.IsGenericType
            || delegateType.GetGenericTypeDefinition() != typeof(Func<,,>)
            || typeArguments.Length != 3
            || !typeArguments[0].IsGenericType
            || typeArguments[0].GetGenericTypeDefinition() != typeof(EntityBuilder<>)
            || typeArguments[1] != typeof(IDataContext)
            || typeArguments[2] != typeArguments[0])
            throw new InvalidOperationException($"The static member '{memberName}' named by {nameof(QueryFilterAttribute)} on {typeof(T).Name} must return a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>.");

        return func;
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
        if (entityType.IsValueType)
            throw new InvalidOperationException($"A dynamic-columns store is not supported on the value-type entity {entityType.Name}; use a reference type.");

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
    /// read. The selector may select a scalar property or a flat anonymous object; the components keep
    /// the selector order.
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
        var navigationProperty = ResolveCollectionNavigation(navigation, typeof(TChild), nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(TChild), nameof(foreignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.Create(typeof(T), typeof(TChild), isCollection: true, navigationProperty, foreignKeys, principalKeys: null));

        return this;
    }

    /// <summary>
    /// Declares a one-to-many relationship with an explicit principal key: a collection navigation on
    /// this entity, the foreign-key components on the dependent type and the principal-key components
    /// on this entity.
    /// </summary>
    /// <typeparam name="TChild">The dependent entity type.</typeparam>
    /// <param name="navigation">Selects the collection navigation on this entity, for example <c>p =&gt; p.Children</c>.</param>
    /// <param name="foreignKey">Selects the foreign-key components on <typeparamref name="TChild"/>, for example <c>c =&gt; new { c.TenantId, c.ParentId }</c>.</param>
    /// <param name="principalKey">Selects the principal-key components on this entity, for example <c>p =&gt; new { p.TenantId, p.Id }</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector, or the navigation is not a collection of <typeparamref name="TChild"/>.</exception>
    public EntityMetadataBuilder<T> HasMany<TChild>(
        Expression<Func<T, ICollection<TChild>>> navigation,
        Expression<Func<TChild, object?>> foreignKey,
        Expression<Func<T, object?>> principalKey)
        where TChild : class
    {
        var navigationProperty = ResolveCollectionNavigation(navigation, typeof(TChild), nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(TChild), nameof(foreignKey), relationshipName);
        var principalKeys = ParseKeySelector(principalKey, typeof(T), nameof(principalKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.Create(typeof(T), typeof(TChild), isCollection: true, navigationProperty, foreignKeys, principalKeys));

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
        var relationshipName = RelationshipName(typeof(T), null);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(TChild), nameof(foreignKey), relationshipName);
        var principalKeys = ParseKeySelector(principalKey, typeof(T), nameof(principalKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: true, foreignKeys, principalKeys));

        return this;
    }

    /// <summary>
    /// Declares a one-to-many relationship without a navigation and with an explicit principal key: the
    /// foreign-key components are selected on the dependent type and the principal-key components on
    /// this entity.
    /// </summary>
    /// <typeparam name="TChild">The dependent entity type.</typeparam>
    /// <param name="foreignKey">Selects the foreign-key components on <typeparamref name="TChild"/>.</param>
    /// <param name="principalKey">Selects the principal-key components on this entity.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector.</exception>
    // Lower priority than the legacy scalar overload: an object-typed selector call must keep
    // binding to the legacy signature, which forwards into the same shared parser.
    [OverloadResolutionPriority(-1)]
    public EntityMetadataBuilder<T> HasMany<TChild>(
        Expression<Func<TChild, object?>> foreignKey,
        Expression<Func<T, object?>> principalKey)
        where TChild : class
    {
        var relationshipName = RelationshipName(typeof(T), null);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(TChild), nameof(foreignKey), relationshipName);
        var principalKeys = ParseKeySelector(principalKey, typeof(T), nameof(principalKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: true, foreignKeys, principalKeys));

        return this;
    }

    /// <summary>
    /// Declares a one-to-many relationship without a navigation, a foreign key and the default
    /// principal key: the principal key is the full key of this entity, in entity-metadata order.
    /// </summary>
    /// <typeparam name="TChild">The dependent entity type.</typeparam>
    /// <param name="foreignKey">Selects the foreign-key components on <typeparamref name="TChild"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="foreignKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The selector is not a supported key selector, or its arity does not match the full principal key.</exception>
    public EntityMetadataBuilder<T> HasMany<TChild>(Expression<Func<TChild, object?>> foreignKey)
        where TChild : class
    {
        var relationshipName = RelationshipName(typeof(T), null);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(TChild), nameof(foreignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: true, foreignKeys, principalKeys: null));

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
        var navigationProperty = ResolveReferenceNavigation(navigation, nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(T), nameof(foreignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.Create(typeof(T), typeof(TChild), isCollection: false, navigationProperty, foreignKeys, principalKeys: null));

        return this;
    }

    /// <summary>
    /// Declares a many-to-one relationship with an explicit principal key: a reference navigation on
    /// this entity, the foreign-key components on this entity and the principal-key components on the
    /// related (principal) type.
    /// </summary>
    /// <typeparam name="TChild">The principal entity type.</typeparam>
    /// <param name="navigation">Selects the reference navigation on this entity, for example <c>c =&gt; c.Parent</c>.</param>
    /// <param name="foreignKey">Selects the foreign-key components on this entity, for example <c>c =&gt; new { c.TenantId, c.ParentId }</c>.</param>
    /// <param name="principalKey">Selects the principal-key components on <typeparamref name="TChild"/>, for example <c>p =&gt; new { p.TenantId, p.Id }</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector, or the navigation is a value type or <see cref="string"/>.</exception>
    public EntityMetadataBuilder<T> HasOne<TChild>(
        Expression<Func<T, TChild?>> navigation,
        Expression<Func<T, object?>> foreignKey,
        Expression<Func<TChild, object?>> principalKey)
        where TChild : class
    {
        var navigationProperty = ResolveReferenceNavigation(navigation, nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(T), nameof(foreignKey), relationshipName);
        var principalKeys = ParseKeySelector(principalKey, typeof(TChild), nameof(principalKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.Create(typeof(T), typeof(TChild), isCollection: false, navigationProperty, foreignKeys, principalKeys));

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
        var relationshipName = RelationshipName(typeof(T), null);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(T), nameof(foreignKey), relationshipName);
        var principalKeys = ParseKeySelector(principalKey, typeof(TChild), nameof(principalKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: false, foreignKeys, principalKeys));

        return this;
    }

    /// <summary>
    /// Declares a many-to-one relationship without a navigation and with an explicit principal key: the
    /// foreign-key components are selected on this entity and the principal-key components on the
    /// related (principal) type.
    /// </summary>
    /// <typeparam name="TChild">The principal entity type.</typeparam>
    /// <param name="foreignKey">Selects the foreign-key components on this entity.</param>
    /// <param name="principalKey">Selects the principal-key components on <typeparamref name="TChild"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector.</exception>
    // Lower priority than the legacy scalar overload: an object-typed selector call must keep
    // binding to the legacy signature, which forwards into the same shared parser.
    [OverloadResolutionPriority(-1)]
    public EntityMetadataBuilder<T> HasOne<TChild>(
        Expression<Func<T, object?>> foreignKey,
        Expression<Func<TChild, object?>> principalKey)
        where TChild : class
    {
        var relationshipName = RelationshipName(typeof(T), null);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(T), nameof(foreignKey), relationshipName);
        var principalKeys = ParseKeySelector(principalKey, typeof(TChild), nameof(principalKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: false, foreignKeys, principalKeys));

        return this;
    }

    /// <summary>
    /// Declares a many-to-one relationship without a navigation, a foreign key and the default
    /// principal key: the principal key is the full key of <typeparamref name="TChild"/>, in
    /// entity-metadata order.
    /// </summary>
    /// <typeparam name="TChild">The principal entity type.</typeparam>
    /// <param name="foreignKey">Selects the foreign-key components on this entity.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="foreignKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The selector is not a supported key selector, or its arity does not match the full principal key.</exception>
    public EntityMetadataBuilder<T> HasOne<TChild>(Expression<Func<T, object?>> foreignKey)
        where TChild : class
    {
        var relationshipName = RelationshipName(typeof(T), null);
        var foreignKeys = ParseKeySelector(foreignKey, typeof(T), nameof(foreignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateFallback(typeof(T), typeof(TChild), isCollection: false, foreignKeys, principalKeys: null));

        return this;
    }

    /// <summary>
    /// Declares a one-to-one relationship: a reference navigation on this (principal) entity, the
    /// principal key on this entity and the unique foreign key on the related (dependent) type. The
    /// uniqueness of the foreign key is a declaration-time trust: the core neither inspects the schema
    /// or the data nor imposes a uniqueness constraint, so a non-unique foreign key is accepted when
    /// the metadata is built. The only runtime guard is per parent through <c>JoinInto</c>: two distinct
    /// non-null child identities for one parent throw <see cref="InvalidOperationException"/>, while
    /// repeated identical rows and keyless/null-identity repeats are tolerated (the first child wins).
    /// This is the principal counterpart of the <c>HasOne</c> overload, whose foreign key lives on the
    /// declaring type.
    /// </summary>
    /// <typeparam name="TChild">The related (dependent) entity type.</typeparam>
    /// <typeparam name="TKey">The property type shared by the principal key and the foreign key.</typeparam>
    /// <param name="navigation">Selects the reference navigation on this entity, for example <c>p =&gt; p.Profile</c>.</param>
    /// <param name="parentKey">Selects the principal-key property on this entity, for example <c>p =&gt; p.Id</c>.</param>
    /// <param name="childForeignKey">Selects the foreign-key property on <typeparamref name="TChild"/>, for example <c>c =&gt; c.ParentId</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector does not select a property, or the navigation is a value type or <see cref="string"/>.</exception>
    public EntityMetadataBuilder<T> HasOneToOne<TChild, TKey>(
        Expression<Func<T, TChild?>> navigation,
        Expression<Func<T, TKey>> parentKey,
        Expression<Func<TChild, TKey>> childForeignKey)
        where TChild : class
    {
        var navigationProperty = ResolveReferenceNavigation(navigation, nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var principalKeys = ParseKeySelector(parentKey, typeof(T), nameof(parentKey), relationshipName);
        var foreignKeys = ParseKeySelector(childForeignKey, typeof(TChild), nameof(childForeignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateOneToOne(typeof(T), typeof(TChild), navigationProperty, foreignKeys, principalKeys));

        return this;
    }

    /// <summary>
    /// Declares a one-to-one relationship with explicit key components: a reference navigation on this
    /// (principal) entity, the principal-key components on this entity and the foreign-key components
    /// on the related (dependent) type. Uniqueness of the foreign key remains a declaration-time
    /// trust; the core does not inspect the schema.
    /// </summary>
    /// <typeparam name="TChild">The related (dependent) entity type.</typeparam>
    /// <param name="navigation">Selects the reference navigation on this entity, for example <c>p =&gt; p.Profile</c>.</param>
    /// <param name="parentKey">Selects the principal-key components on this entity, for example <c>p =&gt; new { p.TenantId, p.Id }</c>.</param>
    /// <param name="childForeignKey">Selects the foreign-key components on <typeparamref name="TChild"/>, for example <c>c =&gt; new { c.TenantId, c.OwnerId }</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector, or the navigation is a value type or <see cref="string"/>.</exception>
    // Lower priority than the legacy scalar overload: an object-typed selector call must keep
    // binding to the legacy signature, which forwards into the same shared parser.
    [OverloadResolutionPriority(-1)]
    public EntityMetadataBuilder<T> HasOneToOne<TChild>(
        Expression<Func<T, TChild?>> navigation,
        Expression<Func<T, object?>> parentKey,
        Expression<Func<TChild, object?>> childForeignKey)
        where TChild : class
    {
        var navigationProperty = ResolveReferenceNavigation(navigation, nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var principalKeys = ParseKeySelector(parentKey, typeof(T), nameof(parentKey), relationshipName);
        var foreignKeys = ParseKeySelector(childForeignKey, typeof(TChild), nameof(childForeignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateOneToOne(typeof(T), typeof(TChild), navigationProperty, foreignKeys, principalKeys));

        return this;
    }

    /// <summary>
    /// Declares a one-to-one relationship without an explicit principal key: a reference navigation on
    /// this (principal) entity and the foreign-key components on the related (dependent) type. The
    /// principal key is the full key of this entity, in entity-metadata order.
    /// </summary>
    /// <typeparam name="TChild">The related (dependent) entity type.</typeparam>
    /// <typeparam name="TKey">The property type of the child foreign key.</typeparam>
    /// <param name="navigation">Selects the reference navigation on this entity, for example <c>p =&gt; p.Profile</c>.</param>
    /// <param name="childForeignKey">Selects the foreign-key component(s) on <typeparamref name="TChild"/>, for example <c>c =&gt; new { c.TenantId, c.OwnerId }</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector, or the navigation is a value type or <see cref="string"/>.</exception>
    public EntityMetadataBuilder<T> HasOneToOne<TChild, TKey>(
        Expression<Func<T, TChild?>> navigation,
        Expression<Func<TChild, TKey>> childForeignKey)
        where TChild : class
    {
        var navigationProperty = ResolveReferenceNavigation(navigation, nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var foreignKeys = ParseKeySelector(childForeignKey, typeof(TChild), nameof(childForeignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateOneToOne(typeof(T), typeof(TChild), navigationProperty, foreignKeys, principalKeys: null));

        return this;
    }

    /// <summary>
    /// Declares a many-to-many relationship: a collection navigation on this entity, the keys on both
    /// principal sides and the junction (link) entity's foreign keys that reference them. The junction is
    /// explicit; the core does not infer it. The navigation participates in a declared relationship and is
    /// excluded from <c>Properties</c>. The keys are resolved lazily, so the junction and the child type
    /// may be registered later.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <typeparam name="TJunction">The junction (link) entity type.</typeparam>
    /// <typeparam name="TParentKey">The property type shared by the parent key and the junction parent foreign key.</typeparam>
    /// <typeparam name="TChildKey">The property type shared by the child key and the junction child foreign key.</typeparam>
    /// <param name="navigation">Selects the collection navigation on this entity, for example <c>p =&gt; p.Children</c>.</param>
    /// <param name="parentKey">Selects the parent-side key property on this entity, for example <c>p =&gt; p.Id</c>.</param>
    /// <param name="junctionParentForeignKey">Selects the junction foreign key referencing the parent key, for example <c>l =&gt; l.ParentId</c>.</param>
    /// <param name="childKey">Selects the child-side key property, for example <c>c =&gt; c.Id</c>.</param>
    /// <param name="junctionChildForeignKey">Selects the junction foreign key referencing the child key, for example <c>l =&gt; l.ChildId</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector does not select a property, the navigation is not a collection of <typeparamref name="TChild"/>, or a key does not match its junction foreign key's type.</exception>
    public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction, TParentKey, TChildKey>(
        Expression<Func<T, IEnumerable<TChild>>> navigation,
        Expression<Func<T, TParentKey>> parentKey,
        Expression<Func<TJunction, TParentKey>> junctionParentForeignKey,
        Expression<Func<TChild, TChildKey>> childKey,
        Expression<Func<TJunction, TChildKey>> junctionChildForeignKey)
        where TChild : class
        where TJunction : class
    {
        var navigationProperty = ResolveCollectionNavigation(navigation, typeof(TChild), nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var parentKeys = ParseKeySelector(parentKey, typeof(T), nameof(parentKey), relationshipName);
        var junctionParentForeignKeys = ParseKeySelector(junctionParentForeignKey, typeof(TJunction), nameof(junctionParentForeignKey), relationshipName);
        var childKeys = ParseKeySelector(childKey, typeof(TChild), nameof(childKey), relationshipName);
        var junctionChildForeignKeys = ParseKeySelector(junctionChildForeignKey, typeof(TJunction), nameof(junctionChildForeignKey), relationshipName);

        ValidateJunctionKeyTypes(typeof(T).Name, nameof(parentKey), parentKeys, nameof(junctionParentForeignKey), junctionParentForeignKeys);
        ValidateJunctionKeyTypes(typeof(T).Name, nameof(childKey), childKeys, nameof(junctionChildForeignKey), junctionChildForeignKeys);

        _relationships.Add(RelationshipDeclaration.CreateManyToMany(
            typeof(T),
            typeof(TChild),
            typeof(TJunction),
            navigationProperty,
            parentKeys,
            junctionParentForeignKeys,
            childKeys,
            junctionChildForeignKeys));

        return this;
    }

    /// <summary>
    /// Declares a many-to-many relationship with explicit key components on both legs: a collection
    /// navigation on this entity, the parent-side key on this entity, the junction foreign key that
    /// references it, the child-side key on the related type and the junction foreign key that
    /// references that. Each pair must have the same number of components and matching positional
    /// types.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <typeparam name="TJunction">The junction (link) entity type.</typeparam>
    /// <param name="navigation">Selects the collection navigation on this entity, for example <c>p =&gt; p.Children</c>.</param>
    /// <param name="parentKey">Selects the parent-side key components on this entity.</param>
    /// <param name="junctionParentForeignKey">Selects the junction components that reference <paramref name="parentKey"/>.</param>
    /// <param name="childKey">Selects the child-side key components on <typeparamref name="TChild"/>.</param>
    /// <param name="junctionChildForeignKey">Selects the junction components that reference <paramref name="childKey"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector, the navigation is not a collection of <typeparamref name="TChild"/>, or a key leg does not match its junction foreign key's arity or types.</exception>
    public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction>(
        Expression<Func<T, IEnumerable<TChild>>> navigation,
        Expression<Func<T, object?>> parentKey,
        Expression<Func<TJunction, object?>> junctionParentForeignKey,
        Expression<Func<TChild, object?>> childKey,
        Expression<Func<TJunction, object?>> junctionChildForeignKey)
        where TChild : class
        where TJunction : class
    {
        var navigationProperty = ResolveCollectionNavigation(navigation, typeof(TChild), nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var parentKeys = ParseKeySelector(parentKey, typeof(T), nameof(parentKey), relationshipName);
        var junctionParentForeignKeys = ParseKeySelector(junctionParentForeignKey, typeof(TJunction), nameof(junctionParentForeignKey), relationshipName);
        var childKeys = ParseKeySelector(childKey, typeof(TChild), nameof(childKey), relationshipName);
        var junctionChildForeignKeys = ParseKeySelector(junctionChildForeignKey, typeof(TJunction), nameof(junctionChildForeignKey), relationshipName);

        ValidateJunctionKeyTypes(typeof(T).Name, nameof(parentKey), parentKeys, nameof(junctionParentForeignKey), junctionParentForeignKeys);
        ValidateJunctionKeyTypes(typeof(T).Name, nameof(childKey), childKeys, nameof(junctionChildForeignKey), junctionChildForeignKeys);

        _relationships.Add(RelationshipDeclaration.CreateManyToMany(
            typeof(T),
            typeof(TChild),
            typeof(TJunction),
            navigationProperty,
            parentKeys,
            junctionParentForeignKeys,
            childKeys,
            junctionChildForeignKeys));

        return this;
    }

    /// <summary>
    /// Declares a many-to-many relationship with the default full principal keys on both sides: a
    /// collection navigation on this entity, the junction foreign key that references this entity's
    /// full key and the junction foreign key that references the related type's full key. Both keys are
    /// taken from entity metadata in property order.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <typeparam name="TJunction">The junction (link) entity type.</typeparam>
    /// <param name="navigation">Selects the collection navigation on this entity, for example <c>p =&gt; p.Children</c>.</param>
    /// <param name="junctionParentForeignKey">Selects the junction components that reference this entity's key.</param>
    /// <param name="junctionChildForeignKey">Selects the junction components that reference the related type's key.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">A selector is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">A selector is not a supported key selector, the navigation is not a collection of <typeparamref name="TChild"/>, or a default key's arity does not match its junction foreign key.</exception>
    public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction>(
        Expression<Func<T, IEnumerable<TChild>>> navigation,
        Expression<Func<TJunction, object?>> junctionParentForeignKey,
        Expression<Func<TJunction, object?>> junctionChildForeignKey)
        where TChild : class
        where TJunction : class
    {
        var navigationProperty = ResolveCollectionNavigation(navigation, typeof(TChild), nameof(navigation));
        var relationshipName = RelationshipName(typeof(T), navigationProperty);
        var junctionParentForeignKeys = ParseKeySelector(junctionParentForeignKey, typeof(TJunction), nameof(junctionParentForeignKey), relationshipName);
        var junctionChildForeignKeys = ParseKeySelector(junctionChildForeignKey, typeof(TJunction), nameof(junctionChildForeignKey), relationshipName);

        _relationships.Add(RelationshipDeclaration.CreateManyToManyDefaults(
            typeof(T),
            typeof(TChild),
            typeof(TJunction),
            navigationProperty,
            junctionParentForeignKeys,
            junctionChildForeignKeys));

        return this;
    }

    private static PropertyInfo ResolveCollectionNavigation(LambdaExpression? navigation, Type childType, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(navigation, parameterName);

        var navigationProperty = ResolveSelectedProperty(navigation, parameterName);
        var collectionType = typeof(IEnumerable<>).MakeGenericType(childType);
        if (!collectionType.IsAssignableFrom(navigationProperty.PropertyType))
            throw new NotSupportedException($"The navigation '{typeof(T).Name}.{navigationProperty.Name}' must be a collection of {childType.Name}.");

        return navigationProperty;
    }

    private static PropertyInfo ResolveReferenceNavigation(LambdaExpression? navigation, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(navigation, parameterName);

        var navigationProperty = ResolveSelectedProperty(navigation, parameterName);
        if (navigationProperty.PropertyType == typeof(string) || navigationProperty.PropertyType.IsValueType)
            throw new NotSupportedException($"The navigation '{typeof(T).Name}.{navigationProperty.Name}' must be a reference-typed property.");

        return navigationProperty;
    }

    private static IReadOnlyList<PropertyInfo> ParseKeySelector(LambdaExpression? selector, Type entityType, string selectorName, string relationshipName)
        => RelationshipKeySelectorParser.Parse(selector, entityType, selectorName, relationshipName);

    private static void ValidateJunctionKeyTypes(
        string entityName,
        string keyName,
        IReadOnlyList<PropertyInfo> keys,
        string foreignKeyName,
        IReadOnlyList<PropertyInfo> foreignKeys)
    {
        if (keys.Count != foreignKeys.Count)
            throw new NotSupportedException(
                $"The many-to-many relationship declared on '{entityName}' has {keys.Count} {keyName} component(s) but {foreignKeys.Count} {foreignKeyName} component(s); the two sides of a junction leg must have the same arity.");

        for (var i = 0; i < keys.Count; i++)
            RelationshipResolver.ValidateJunctionKeyType(entityName, keyName, keys[i], foreignKeyName, foreignKeys[i], i, keys.Count);
    }

    private static string RelationshipName(Type declaringType, PropertyInfo? navigation)
        => navigation is null ? declaringType.Name : $"{declaringType.Name}.{navigation.Name}";

    /// <summary>
    /// Declares an anonymous global query filter that is applied to every query in which the entity
    /// participates (the primary source, joins and subqueries) unless the query calls
    /// <c>IgnoreFilters</c>. A repeated anonymous call on this builder adds another predicate; the
    /// declared anonymous filters are combined with <c>and</c>. The mapping is registered once per
    /// entity type: a later <c>From&lt;T&gt;(...)</c> configuration for the same type is ignored, so the
    /// first registration wins.
    /// </summary>
    /// <param name="filter">The filter predicate over the entity.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filters.Add((null, filter, null));
        return this;
    }

    /// <summary>
    /// Declares an anonymous global query filter that is applied to every query in which the entity
    /// participates (the primary source, joins and subqueries) unless the query calls
    /// <c>IgnoreFilters</c>. The predicate receives the executing <see cref="IDataContext"/> so it can
    /// read per-context state such as a tenant identifier. A repeated anonymous call on this builder
    /// adds another predicate; the declared anonymous filters are combined with <c>and</c>. The mapping
    /// is registered once per entity type: a later <c>From&lt;T&gt;(...)</c> configuration for the same
    /// type is ignored, so the first registration wins.
    /// </summary>
    /// <param name="filter">The filter predicate over the entity and the executing data context.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filters.Add((null, filter, null));
        return this;
    }

    /// <summary>
    /// Declares a named global query filter: the key occupies a slot so the filter can be targeted by
    /// the key-based <c>IgnoreFilters</c> overload. A repeated call with the same key <b>replaces</b>
    /// the earlier filter; passing a <see langword="null"/> <paramref name="filter"/> removes the slot.
    /// The predicate receives the executing <see cref="IDataContext"/>. The mapping is registered once
    /// per entity type: a later <c>From&lt;T&gt;(...)</c> configuration for the same type is ignored, so
    /// the first registration wins.
    /// </summary>
    /// <param name="filterKey">The filter key; null, empty or whitespace declares an anonymous filter.</param>
    /// <param name="filter">The context-aware filter predicate, or <see langword="null"/> to remove the named slot.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityMetadataBuilder<T> HasQueryFilter(string filterKey, Expression<Func<T, IDataContext, bool>>? filter)
    {
        _filters.Add((filterKey, filter, null));
        return this;
    }

    // Lower overload priority: the two-parameter function has the same shape as the context-aware
    // predicate overload, so a `null` argument (which the keyed predicate overload treats as
    // slot-removal) would otherwise be ambiguous between the two. The function form still binds for a
    // lambda that returns the builder.
    /// <summary>
    /// Declares an anonymous global query filter in the builder-function form. The function receives a
    /// builder over the entity and the executing <see cref="IDataContext"/>, and must express the filter
    /// by calling <c>Where</c>; only that predicate is merged into every query in which the entity
    /// participates. The function is invoked once while the plan is built. Values that must stay
    /// parameterised (so the plan can be cached) have to be read from the <see cref="IDataContext"/> or
    /// supplied through <c>SqlFunctions.Parameter&lt;T&gt;(idx)</c>; snapshotting a runtime value into a
    /// local before the <c>Where</c> call, or changing anything other than <c>Where</c>, is rejected.
    /// A repeated anonymous call on this builder adds another filter. The mapping is registered once per
    /// entity type: a later <c>From&lt;T&gt;(...)</c> configuration for the same type is ignored, so the
    /// first registration wins.
    /// </summary>
    /// <param name="filter">The filter function that appends the predicate with <c>Where</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    [OverloadResolutionPriority(-1)]
    public EntityMetadataBuilder<T> HasQueryFilter(Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filters.Add((null, null, filter));
        return this;
    }

    // Lower overload priority so `HasQueryFilter(key, null)` keeps resolving to the keyed predicate
    // overload (null removes the named slot) instead of becoming ambiguous with this function overload.
    /// <summary>
    /// Declares a named global query filter in the builder-function form: the key occupies a slot so the
    /// filter can be targeted by the key-based <c>IgnoreFilters</c> overload. A repeated call with the
    /// same key <b>replaces</b> the earlier filter; passing a <see langword="null"/>
    /// <paramref name="filter"/> removes the slot. The function receives the executing
    /// <see cref="IDataContext"/> and must express the filter by calling <c>Where</c>; only that predicate
    /// is merged. The mapping is registered once per entity type: a later <c>From&lt;T&gt;(...)</c>
    /// configuration for the same type is ignored, so the first registration wins.
    /// </summary>
    /// <param name="filterKey">The filter key; null, empty or whitespace declares an anonymous filter.</param>
    /// <param name="filter">The filter function that appends the predicate with <c>Where</c>, or <see langword="null"/> to remove the named slot.</param>
    /// <returns>This builder, for chaining.</returns>
    [OverloadResolutionPriority(-1)]
    public EntityMetadataBuilder<T> HasQueryFilter(string filterKey, Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>? filter)
    {
        _filters.Add((filterKey, null, filter));
        return this;
    }

    // Fluent declarations come first, so a fluent relationship wins over an attribute on the same
    // navigation; the first declaration for a navigation property wins. A shadowed attribute is not
    // parsed or validated: the fluent declaration owns that navigation entirely.
    private List<IRelationshipMetadata> BuildRelationships(bool includeFluent)
    {
        var declarations = new List<RelationshipDeclaration>();
        var declaredNavigations = new HashSet<PropertyInfo>();
        if (includeFluent)
        {
            for (var i = 0; i < _relationships.Count; i++)
            {
                declarations.Add(_relationships[i]);
                if (_relationships[i].Navigation is { } navigation)
                    declaredNavigations.Add(navigation);
            }
        }

        AddAttributeDeclarations(typeof(T), declarations, declaredNavigations);

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
                declaration.ForeignKeys,
                declaration.PrincipalKeys,
                declaration.DependentType,
                declaration.PrincipalType,
                declaration.Junction));
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

    private static void AddAttributeDeclarations(Type entityType, List<RelationshipDeclaration> declarations, HashSet<PropertyInfo> declaredNavigations)
    {
        var properties = entityType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            if (declaredNavigations.Contains(property))
                continue;

            var attribute = property.GetCustomAttribute<RelationshipAttribute>(true);
            if (attribute is null)
                continue;

            var navigationName = $"{entityType.Name}.{property.Name}";
            ValidateAttributeForeignKeyForm(attribute, navigationName);

            if (TryGetCollectionElementType(property.PropertyType, out var elementType))
            {
                var foreignKeys = ResolveAttributeMembers(elementType, attribute, foreignKeyRole: true, navigationName);
                var principalKeys = attribute.PrincipalKeys is null
                    ? null
                    : ResolveAttributeMembers(entityType, attribute, foreignKeyRole: false, navigationName);
                ValidateAttributeArity(navigationName, foreignKeys, principalKeys);
                declarations.Add(RelationshipDeclaration.Create(entityType, elementType, isCollection: true, property, foreignKeys, principalKeys));
            }
            else
            {
                var relatedType = property.PropertyType;
                if (relatedType == typeof(string) || relatedType.IsValueType)
                    throw new NotSupportedException($"The {nameof(RelationshipAttribute)} on '{navigationName}' must be placed on a collection or a reference-typed navigation.");

                var foreignKeys = ResolveAttributeMembers(entityType, attribute, foreignKeyRole: true, navigationName);
                var principalKeys = attribute.PrincipalKeys is null
                    ? null
                    : ResolveAttributeMembers(relatedType, attribute, foreignKeyRole: false, navigationName);
                ValidateAttributeArity(navigationName, foreignKeys, principalKeys);
                declarations.Add(RelationshipDeclaration.Create(entityType, relatedType, isCollection: false, property, foreignKeys, principalKeys));
            }
        }
    }

    private static void ValidateAttributeForeignKeyForm(RelationshipAttribute attribute, string navigationName)
    {
        var hasLegacyForeignKey = !string.IsNullOrWhiteSpace(attribute.ForeignKey);
        var hasForeignKeyArray = attribute.ForeignKeys is not null;

        if (hasLegacyForeignKey && hasForeignKeyArray)
            throw new NotSupportedException(
                $"The {nameof(RelationshipAttribute)} on '{navigationName}' sets both {nameof(RelationshipAttribute.ForeignKey)} and {nameof(RelationshipAttribute.ForeignKeys)}; declare exactly one foreign-key form.");

        if (!hasLegacyForeignKey && !hasForeignKeyArray)
            throw new NotSupportedException(
                $"The {nameof(RelationshipAttribute)} on '{navigationName}' must name the foreign-key property in {nameof(RelationshipAttribute.ForeignKey)} or {nameof(RelationshipAttribute.ForeignKeys)}.");

        if (attribute.ForeignKeys is { Length: 0 })
            throw new NotSupportedException(
                $"The {nameof(RelationshipAttribute)} on '{navigationName}' declares an empty {nameof(RelationshipAttribute.ForeignKeys)} array; name at least one member or omit the property.");

        if (attribute.PrincipalKeys is { Length: 0 })
            throw new NotSupportedException(
                $"The {nameof(RelationshipAttribute)} on '{navigationName}' declares an empty {nameof(RelationshipAttribute.PrincipalKeys)} array; omit the property to use the full principal key.");
    }

    private static IReadOnlyList<PropertyInfo> ResolveAttributeMembers(Type owner, RelationshipAttribute attribute, bool foreignKeyRole, string navigationName)
    {
        var role = foreignKeyRole ? "foreign key" : "principal key";
        if (foreignKeyRole && attribute.ForeignKeys is null)
            return new[] { ResolveAttributeMember(owner, attribute.ForeignKey!, role, position: 1, count: 1, navigationName) };

        var names = foreignKeyRole ? attribute.ForeignKeys! : attribute.PrincipalKeys!;
        var resolved = new PropertyInfo[names.Length];
        var seen = new HashSet<PropertyInfo>(names.Length);
        for (var i = 0; i < names.Length; i++)
        {
            var member = ResolveAttributeMember(owner, names[i], role, i + 1, names.Length, navigationName);
            if (!seen.Add(member))
                throw new NotSupportedException(
                    $"The {nameof(RelationshipAttribute)} on '{navigationName}' repeats the {role} '{member.Name}' at position {i + 1} of {names.Length}.");
            resolved[i] = member;
        }

        return resolved;
    }

    private static PropertyInfo ResolveAttributeMember(Type owner, string name, string role, int position, int count, string navigationName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new NotSupportedException(
                $"The {nameof(RelationshipAttribute)} on '{navigationName}' has an empty {role} name at position {position} of {count}.");

        return owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            ?? throw new NotSupportedException(
                $"The {nameof(RelationshipAttribute)} on '{navigationName}' refers to the {role} '{name}' at position {position} of {count}, which was not found on {owner.Name}.");
    }

    private static void ValidateAttributeArity(string navigationName, IReadOnlyList<PropertyInfo> foreignKeys, IReadOnlyList<PropertyInfo>? principalKeys)
    {
        if (principalKeys is not null && principalKeys.Count != foreignKeys.Count)
            throw new NotSupportedException(
                $"The {nameof(RelationshipAttribute)} on '{navigationName}' declares {foreignKeys.Count} foreign-key component(s) but {principalKeys.Count} principal-key component(s); the arities must match.");
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
        public required IReadOnlyList<PropertyInfo>? ForeignKeys { get; init; }
        public IReadOnlyList<PropertyInfo>? PrincipalKeys { get; init; }
        public required Type DependentType { get; init; }
        public required Type PrincipalType { get; init; }
        public RelationshipJunctionDeclaration? Junction { get; init; }

        public static RelationshipDeclaration Create(
            Type declaringType,
            Type relatedType,
            bool isCollection,
            PropertyInfo navigation,
            IReadOnlyList<PropertyInfo> foreignKeys,
            IReadOnlyList<PropertyInfo>? principalKeys)
            => new()
            {
                Kind = isCollection ? RelationshipKind.OneToMany : RelationshipKind.ManyToOne,
                RelatedType = relatedType,
                IsCollection = isCollection,
                Navigation = navigation,
                ForeignKeys = foreignKeys,
                PrincipalKeys = principalKeys,
                DependentType = isCollection ? relatedType : declaringType,
                PrincipalType = isCollection ? declaringType : relatedType,
            };

        public static RelationshipDeclaration CreateOneToOne(
            Type declaringType,
            Type relatedType,
            PropertyInfo navigation,
            IReadOnlyList<PropertyInfo> foreignKeys,
            IReadOnlyList<PropertyInfo>? principalKeys)
            => new()
            {
                Kind = RelationshipKind.OneToOne,
                RelatedType = relatedType,
                IsCollection = false,
                Navigation = navigation,
                ForeignKeys = foreignKeys,
                PrincipalKeys = principalKeys,
                // The foreign key lives on the related type; the declaring type holds the principal key.
                DependentType = relatedType,
                PrincipalType = declaringType,
            };

        public static RelationshipDeclaration CreateFallback(
            Type declaringType,
            Type relatedType,
            bool isCollection,
            IReadOnlyList<PropertyInfo> foreignKeys,
            IReadOnlyList<PropertyInfo>? principalKeys)
            => new()
            {
                Kind = isCollection ? RelationshipKind.OneToMany : RelationshipKind.ManyToOne,
                RelatedType = relatedType,
                IsCollection = isCollection,
                Navigation = null,
                ForeignKeys = foreignKeys,
                PrincipalKeys = principalKeys,
                DependentType = isCollection ? relatedType : declaringType,
                PrincipalType = isCollection ? declaringType : relatedType,
            };

        public static RelationshipDeclaration CreateManyToMany(
            Type declaringType,
            Type relatedType,
            Type junctionType,
            PropertyInfo navigation,
            IReadOnlyList<PropertyInfo> parentKeys,
            IReadOnlyList<PropertyInfo> junctionParentForeignKeys,
            IReadOnlyList<PropertyInfo> childKeys,
            IReadOnlyList<PropertyInfo> junctionChildForeignKeys)
            => new()
            {
                Kind = RelationshipKind.ManyToMany,
                RelatedType = relatedType,
                IsCollection = true,
                Navigation = navigation,
                // The exposed foreign key is the child-side key; the parent-side key is the principal key.
                // The junction's own foreign keys live in the junction descriptor.
                ForeignKeys = childKeys,
                PrincipalKeys = parentKeys,
                DependentType = relatedType,
                PrincipalType = declaringType,
                Junction = new RelationshipJunctionDeclaration
                {
                    JunctionType = junctionType,
                    ParentKey = parentKeys,
                    ChildKey = childKeys,
                    JunctionParentForeignKey = junctionParentForeignKeys,
                    JunctionChildForeignKey = junctionChildForeignKeys,
                },
            };

        public static RelationshipDeclaration CreateManyToManyDefaults(
            Type declaringType,
            Type relatedType,
            Type junctionType,
            PropertyInfo navigation,
            IReadOnlyList<PropertyInfo> junctionParentForeignKeys,
            IReadOnlyList<PropertyInfo> junctionChildForeignKeys)
            => new()
            {
                Kind = RelationshipKind.ManyToMany,
                RelatedType = relatedType,
                IsCollection = true,
                Navigation = navigation,
                ForeignKeys = null,
                PrincipalKeys = null,
                DependentType = relatedType,
                PrincipalType = declaringType,
                Junction = new RelationshipJunctionDeclaration
                {
                    JunctionType = junctionType,
                    ParentKey = null,
                    ChildKey = null,
                    JunctionParentForeignKey = junctionParentForeignKeys,
                    JunctionChildForeignKey = junctionChildForeignKeys,
                },
            };
    }
}

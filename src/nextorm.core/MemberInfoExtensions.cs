using System.Collections.Concurrent;
using System.Reflection;
using System.Collections.Generic;

namespace NextORM.Core;
/// <summary>
/// Helpers for resolving a member's column name and for looking up its metadata in the shared entity
/// metadata cache.
/// </summary>
public static class MemberInfoExtensions
{
    // Entity metadata is registered once per type (DataContextCache.Metadata only builds on a miss),
    // so the resolved column name is stable for a given naming convention for the lifetime of the
    // process. Caching it turns the per-member metadata lookup + linear scan into a dictionary read
    // on the SQL-build path.
    private static readonly ConcurrentDictionary<(PropertyInfo, INamingConvention?, Type?), string> _columnNames = new();

    /// <summary>
    /// Drops the cached column names, all of them or only those of <paramref name="entityType"/>. Called
    /// when the process-wide metadata cache is cleared and when an already-published mapping is replaced
    /// by a configured one (the many-to-many junction auto-publish), so a column name resolved against
    /// the superseded metadata cannot be served afterwards.
    /// </summary>
    /// <param name="entityType">The owning entity type to invalidate, or <see langword="null"/> for all.</param>
    internal static void ClearColumnNames(Type? entityType = null)
    {
        if (entityType is null)
        {
            _columnNames.Clear();
            return;
        }

        foreach (var key in _columnNames.Keys)
        {
            if (key.Item1.DeclaringType == entityType
                || key.Item1.ReflectedType == entityType
                || key.Item3 == entityType)
                _columnNames.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Column name registered for the member in the shared entity metadata cache, or an empty string
    /// when the metadata does not declare it. Auto-derived names are translated through
    /// <paramref name="convention"/>; declared names are returned verbatim.
    /// </summary>
    /// <param name="mi">The CLR member to resolve.</param>
    /// <param name="convention">The naming convention applied to auto-derived names, or <see langword="null"/>.</param>
    public static string GetPropertyColumnName(this MemberInfo mi, INamingConvention? convention = null)
        => GetPropertyColumnName(mi, convention, null);

    /// <summary>
    /// Column name registered for the member in the shared entity metadata cache, or an empty string
    /// when the metadata does not declare it. Auto-derived names are translated through
    /// <paramref name="convention"/>; declared names are returned verbatim.
    /// </summary>
    /// <param name="mi">The CLR member to resolve.</param>
    /// <param name="convention">The naming convention applied to auto-derived names, or <see langword="null"/>.</param>
    /// <param name="entityType">
    /// The entity type the member is accessed through. Required for a member inherited from a base class:
    /// the member is reflected against the base type, while the mapping (and any fluent column override)
    /// belongs to the derived entity. When <see langword="null"/>, the reflected and declaring types are
    /// used as a fallback.
    /// </param>
    internal static string GetPropertyColumnName(this MemberInfo mi, INamingConvention? convention, Type? entityType)
    {
        if (mi is PropertyInfo pi)
        {
            var key = (pi, convention, entityType);

            if (_columnNames.TryGetValue(key, out var cached))
                return cached;

            // A member of a lambda over a derived entity is reflected against the type that declares
            // it (DeclaringType), not the entity type, and metadata is keyed by the entity type. When
            // the caller names the entity type, its mapping is authoritative: a property absent from it
            // is not mapped, even if the base type has a mapping of its own.
            IPropertyMetadata? prop = null;
            var resolved = false;

            if (entityType is not null && DataContextCache.Metadata.TryGetValue(entityType, out _))
            {
                resolved = DataContextCache.Metadata.TryGetValue(entityType, pi, out prop);
            }
            else
            {
                if (pi.ReflectedType is { } reflected)
                    resolved = DataContextCache.Metadata.TryGetValue(reflected, pi, out prop);

                if (!resolved && pi.DeclaringType is { } declaring && declaring != pi.ReflectedType)
                    resolved = DataContextCache.Metadata.TryGetValue(declaring, pi, out prop);
            }

            if (resolved)
            {
                var name = prop!.ColumnName;
                if (prop.IsColumnNameAuto && convention is not null)
                    name = convention.ColumnName(name);

                _columnNames.TryAdd(key, name);
                return name;
            }

            if (entityType is not null && DataContextCache.Metadata.ContainsKey(entityType))
                _columnNames.TryAdd(key, string.Empty);
        }

        return string.Empty;
    }

    /// <summary>
    /// Whether two <see cref="PropertyInfo"/> instances denote the same declared property. Reflection
    /// can return distinct instances for the same inherited declaration; identity is therefore based
    /// on the declaring type and property signature, not the reflection object or reflected type.
    /// </summary>
    internal static bool SameProperty(PropertyInfo left, PropertyInfo right)
        => ReferenceEquals(left, right)
            || PropertyIdentity.From(left).Equals(PropertyIdentity.From(right));

    internal readonly struct PropertyIdentity : IEquatable<PropertyIdentity>
    {
        private readonly Type _declaringType;
        private readonly string _name;
        private readonly Type _propertyType;
        private readonly Type[] _indexParameterTypes;

        private PropertyIdentity(Type declaringType, string name, Type propertyType, Type[] indexParameterTypes)
        {
            _declaringType = declaringType;
            _name = name;
            _propertyType = propertyType;
            _indexParameterTypes = indexParameterTypes;
        }

        internal static PropertyIdentity From(PropertyInfo property)
        {
            var parameters = property.GetIndexParameters();
            var types = new Type[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                types[i] = parameters[i].ParameterType;

            return new PropertyIdentity(property.DeclaringType ?? property.ReflectedType!, property.Name, property.PropertyType, types);
        }

        public bool Equals(PropertyIdentity other)
        {
            if (_declaringType != other._declaringType || _name != other._name || _propertyType != other._propertyType
                || _indexParameterTypes.Length != other._indexParameterTypes.Length)
                return false;

            for (var i = 0; i < _indexParameterTypes.Length; i++)
            {
                if (_indexParameterTypes[i] != other._indexParameterTypes[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is PropertyIdentity other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_declaringType);
            hash.Add(_name);
            hash.Add(_propertyType);
            for (var i = 0; i < _indexParameterTypes.Length; i++)
                hash.Add(_indexParameterTypes[i]);
            return hash.ToHashCode();
        }
    }
    /// <summary>
    /// Looks up the metadata for <paramref name="pi"/> in the entity metadata registered for
    /// <paramref name="type"/>. Because the metadata is keyed by type, the property is matched by
    /// scanning that entity's properties rather than by a direct dictionary lookup.
    /// </summary>
    /// <param name="dic">Entity metadata cache keyed by entity type.</param>
    /// <param name="type">The entity type whose metadata should be searched.</param>
    /// <param name="pi">The property to find in the entity's metadata.</param>
    /// <param name="prop">When this method returns <see langword="true"/>, the matching property metadata; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the type and property are both found; otherwise <see langword="false"/>.</returns>
    public static bool TryGetValue(this IDictionary<Type, IEntityMetadata> dic, Type type, PropertyInfo pi, out IPropertyMetadata? prop)
    {
        if (dic.TryGetValue(type, out var entity))
        {
            foreach (var item in entity.Properties)
            {
                if (SameProperty(item.PropertyInfo, pi))
                {
                    prop = item;
                    return true;
                }
            }
        }
        prop = null;
        return false;
    }
}

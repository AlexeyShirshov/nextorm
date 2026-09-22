using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Helpers for classifying runtime types and comparing them while building queries.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Determines whether the type is a compiler-generated anonymous type (its name starts with
    /// <c>&lt;&gt;f__AnonymousType</c>).
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for an anonymous type; otherwise <see langword="false"/>.</returns>
    public static bool IsAnonymous(this Type type) => type.IsSealed
        && type.IsGenericType
        && (type.Attributes & TypeAttributes.NotPublic) != 0
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        && type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal);
    /// <summary>
    /// Determines whether the type is a compiler-generated closure (display class) created to hold
    /// variables captured by a lambda.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for a closure type; otherwise <see langword="false"/>.</returns>
    public static bool IsClosure(this Type type) => type.IsSealed
        && (type.Attributes & TypeAttributes.NotPublic) != 0
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        && type.Name.StartsWith("<>c__DisplayClass", StringComparison.Ordinal);
    /// <summary>
    /// Determines whether the type is a tuple type, identified by a generic name starting with
    /// <c>Tuple`</c>.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for a tuple type; otherwise <see langword="false"/>.</returns>
    public static bool IsTuple(this Type type) => type.IsGenericType
        && (type.Attributes & TypeAttributes.NotPublic) != 0
        && type.Name.StartsWith("Tuple`", StringComparison.Ordinal);

    /// <summary>
    /// Tries to get the number of columns a projection type materializes. A projection is a generic
    /// type implementing <see cref="IProjection"/>; its dimension is its number of generic arguments.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="dim">When this method returns <see langword="true"/>, the projection's generic arity; otherwise <c>0</c>.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is a projection; otherwise <see langword="false"/>.</returns>
    public static bool TryGetProjectionDimension(this Type type, out int dim)
    {
        //const string m = "NextORM.Core.Projection`";
        dim = 0;
        if (type.IsGenericType && type.IsAssignableTo(typeof(IProjection)))
        {
            dim = type.GetGenericArguments().Length;
            return true;
        }
        return false;
    }
    /// <summary>
    /// Determines whether the two types are assignment-compatible in either direction. Equal types and
    /// types assignable to one another match; a nullable type is also compared through its underlying
    /// type.
    /// </summary>
    /// <param name="x">The first type.</param>
    /// <param name="y">The second type.</param>
    /// <returns><see langword="true"/> when the types are considered similar; otherwise <see langword="false"/>.</returns>
    public static bool Similar(this Type x, Type y)
    {
        if (y is null) return false;

        if (x == y
        || x.IsAssignableFrom(y)
        || x.IsAssignableTo(y))
            return true;

        if (x.IsGenericType && x.GetGenericTypeDefinition() == typeof(Nullable<>) && x.GenericTypeArguments[0].Similar(y))
            return true;

        if (y.IsGenericType && y.GetGenericTypeDefinition() == typeof(Nullable<>) && y.GenericTypeArguments[0].Similar(x))
            return true;

        return false;
    }
    /// <summary>
    /// Determines whether the type maps to a single scalar SQL value, i.e. it has a type code other
    /// than <see cref="System.TypeCode.Object"/>.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is scalar; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsScalar(this Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Object => false,
            _ => true
        };
    }
}
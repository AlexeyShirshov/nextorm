using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public static class TypeExtensions
{
    public static bool IsAnonymous(this Type type) => type.IsSealed
        && type.IsGenericType
        && type.Attributes.HasFlag(TypeAttributes.NotPublic)
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        && type.Name.StartsWith("<>f__AnonymousType");
    public static bool IsClosure(this Type type) => type.IsSealed
        && type.Attributes.HasFlag(TypeAttributes.NotPublic)
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        && type.Name.StartsWith("<>c__DisplayClass");
    public static bool IsTuple(this Type type) => type.IsGenericType
        && type.Attributes.HasFlag(TypeAttributes.NotPublic)
        && type.Name.StartsWith("Tuple`");

    public static bool TryGetProjectionDimension(this Type type, out int dim)
    {
        //const string m = "nextorm.core.Projection`";
        dim = 0;
        if (type.IsGenericType && type.IsAssignableTo(typeof(IProjection)))
        {
            dim = type.GetGenericArguments().Length;
            return true;
        }
        return false;
    }
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
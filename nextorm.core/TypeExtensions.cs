using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public static class TypeExtensions
{
    public static bool IsAnonymous(this Type type)=>type.IsSealed 
        && type.IsGenericType 
        && type.Attributes.HasFlag(TypeAttributes.NotPublic) 
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        && type.Name.StartsWith("<>f__AnonymousType");
    public static bool IsClosure(this Type type)=>type.IsSealed 
        && type.Attributes.HasFlag(TypeAttributes.NotPublic) 
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        && type.Name.StartsWith("<>c__DisplayClass");

    public static bool TryGetProjectionDimension(this Type type, out int dim)
    {
        const string m = "nextorm.core.Projection`";
        dim = 0;
        if (type.FullName!.StartsWith(m))
        {
            dim = int.Parse(type.FullName[m.Length..type.FullName!.IndexOf("[", m.Length)]);
            return true;
        }
        return false;
    }
}
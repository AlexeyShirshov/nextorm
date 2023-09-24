using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public static class TypeExtensions
{
    public static bool IsAnonymous(this Type type)=>type.IsSealed 
        && type.IsGenericType 
        && type.Attributes.HasFlag(TypeAttributes.NotPublic) 
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false);
    public static bool IsClosure(this Type type)=>type.IsSealed 
        && type.Attributes.HasFlag(TypeAttributes.NotPublic) 
        && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        && type.Name.StartsWith("<>c__DisplayClass");
}
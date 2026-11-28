using System.Runtime.CompilerServices;

namespace NextORM.Core;

internal static class PlanObjectIdentity
{
    private static readonly ConditionalWeakTable<object, object> Tokens = new();

    // The token has no back-reference to the service and cannot invoke its equality implementation.
    internal static object? For(object? value) => value is null ? null : Tokens.GetValue(value, static _ => new object());

    internal static object? ForNamingConvention(INamingConvention? convention)
        => convention is SnakeCaseNamingConvention ? typeof(SnakeCaseNamingConvention) : For(convention);
}

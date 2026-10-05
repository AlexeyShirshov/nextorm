// Polyfill required for C# 9 init-only setters (records/record structs) on netstandard2.0.
namespace System.Runtime.CompilerServices
{
    /// <summary>Compiler-required marker type for init-only property setters.</summary>
    internal static class IsExternalInit
    {
    }
}

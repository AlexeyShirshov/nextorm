using Microsoft.CodeAnalysis;

namespace NextORM.Core.SourceGenerator;

internal readonly record struct InterfaceEntityCandidate(INamedTypeSymbol Type, bool EntityPosition, Location Location)
{
    internal static bool IsClosed(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsClosed(array.ElementType);
            case INamedTypeSymbol { IsUnboundGenericType: false, TypeKind: not TypeKind.Error } named:
                foreach (var argument in named.TypeArguments)
                {
                    if (!IsClosed(argument))
                    {
                        return false;
                    }
                }

                return named.ContainingType is null || IsClosed(named.ContainingType);
            default:
                return false;
        }
    }
}

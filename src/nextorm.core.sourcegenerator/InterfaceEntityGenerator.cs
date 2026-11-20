using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NextORM.Core.SourceGenerator;

[Generator]
internal sealed class InterfaceEntityGenerator : IIncrementalGenerator
{
    private const string Registry = "NextORM.Core.GeneratedInterfaceEntity";

    private static readonly DiagnosticDescriptor Unsupported = new(
        "NORMGEN010", "Unsupported NextORM interface entity",
        "Interface '{0}' cannot be materialized by NextORM: {1}",
        "NextORM.InterfaceEntity", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is GenericNameSyntax { IsMissing: false },
            static (syntax, token) =>
            {
                var name = (GenericNameSyntax)syntax.Node;
                var found = ImmutableArray.CreateBuilder<InterfaceEntityCandidate>();
                var binding = syntax.SemanticModel.GetSymbolInfo(name, token).Symbol;
                foreach (var argument in name.TypeArgumentList.Arguments)
                {
                    var type = syntax.SemanticModel.GetTypeInfo(argument, token).Type;
                    var entityPosition = IsEntityPosition(binding)
                        || (type is not null && IsMaterialized(name, type, syntax.SemanticModel, token));
                    Collect(type, entityPosition, argument.GetLocation(), found);
                }

                return found.ToImmutable();
            });

        context.RegisterSourceOutput(context.CompilationProvider.Combine(candidates.Collect()),
            static (output, pair) => Emit(output, pair.Left, pair.Right));
    }

    private static bool IsMaterialized(GenericNameSyntax name, ITypeSymbol type, SemanticModel semanticModel, CancellationToken token)
    {
        SyntaxNode node = name;
        while (node.Parent is MemberAccessExpressionSyntax or InvocationExpressionSyntax)
        {
            node = node.Parent;
            if (node is InvocationExpressionSyntax invocation
                && semanticModel.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol method
                && method.Name is "ToList" or "ToListAsync" or "ToArray" or "ToArrayAsync"
                    or "First" or "FirstAsync" or "FirstOrDefault" or "FirstOrDefaultAsync"
                    or "Single" or "SingleAsync" or "SingleOrDefault" or "SingleOrDefaultAsync"
                    or "ToEnumerable" or "ToAsyncEnumerable" or "Read" or "ReadAsync"
                && IsNextormSymbol(method)
                && IncludesType(method.ReturnType, type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IncludesType(ITypeSymbol result, ITypeSymbol type)
    {
        if (SymbolEqualityComparer.Default.Equals(result, type))
        {
            return true;
        }

        return result switch
        {
            IArrayTypeSymbol array => IncludesType(array.ElementType, type),
            INamedTypeSymbol named => named.TypeArguments.Any(argument => IncludesType(argument, type)),
            _ => false,
        };
    }

    private static void Collect(ITypeSymbol? type, bool entityPosition, Location location,
        ImmutableArray<InterfaceEntityCandidate>.Builder found)
    {
        if (type is IArrayTypeSymbol array)
        {
            Collect(array.ElementType, entityPosition, location, found);
            return;
        }

        if (type is not INamedTypeSymbol named)
        {
            return;
        }

        if (named.TypeKind == TypeKind.Interface && InterfaceEntityCandidate.IsClosed(named))
        {
            found.Add(new InterfaceEntityCandidate(named, entityPosition, location));
            // An inherited member can also be resolved using its declaring interface as the metadata key.
            foreach (var parent in named.AllInterfaces)
            {
                found.Add(new InterfaceEntityCandidate(parent, false, location));
            }
        }

        var nestedEntityPosition = entityPosition || IsEntityPosition(named);
        foreach (var argument in named.TypeArguments)
        {
            Collect(argument, nestedEntityPosition, location, found);
        }
    }

    private static bool IsEntityPosition(ISymbol? binding)
    {
        var definition = binding switch
        {
            IMethodSymbol method => (method.ReducedFrom ?? method).OriginalDefinition,
            INamedTypeSymbol type => (ISymbol)type.OriginalDefinition,
            _ => null,
        };

        return definition?.Name is "Returning" or "Projection" && IsNextormSymbol(definition);
    }

    private static bool IsNextormSymbol(ISymbol symbol) =>
        symbol.ContainingAssembly?.GetTypeByMetadataName(Registry) is not null;

    private static void Emit(SourceProductionContext output, Compilation compilation,
        ImmutableArray<ImmutableArray<InterfaceEntityCandidate>> batches)
    {
        if (compilation.GetTypeByMetadataName(Registry) is null)
        {
            return;
        }

        var work = new Dictionary<INamedTypeSymbol, InterfaceEntityCandidate>(SymbolEqualityComparer.Default);
        foreach (var batch in batches)
        {
            foreach (var candidate in batch)
            {
                if (!work.TryGetValue(candidate.Type, out var previous)
                    || (candidate.EntityPosition && !previous.EntityPosition))
                {
                    work[candidate.Type] = candidate;
                }
            }
        }

        var models = new List<InterfaceEntityModel>();
        foreach (var candidate in work.Values.OrderBy(static x => InterfaceEntityModel.TypeName(x.Type), StringComparer.Ordinal))
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            if (InterfaceEntityModel.TryDescribe(candidate.Type, compilation, out var failure) is { } model)
            {
                models.Add(model);
            }
            else if (candidate.EntityPosition)
            {
                output.ReportDiagnostic(Diagnostic.Create(Unsupported, candidate.Location, candidate.Type.ToDisplayString(), failure));
            }
        }

        if (models.Count == 0)
        {
            return;
        }

        var source = new StringBuilder("""
            // <auto-generated/>
            #nullable enable
            namespace NextORM.Core.Generated;

            file static class __NextOrmInterfaceEntities
            {
                [global::System.Runtime.CompilerServices.ModuleInitializer]
                internal static void Initialize()
                {

            """);
        for (var i = 0; i < models.Count; i++)
        {
            models[i].EmitRegistration(source, i);
        }

        source.AppendLine("    }").AppendLine("}");
        for (var i = 0; i < models.Count; i++)
        {
            models[i].EmitImplementation(source, i);
        }

        output.AddSource("NextORM.InterfaceEntities.g.cs", source.ToString());
    }
}

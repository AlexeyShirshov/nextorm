using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NextORM.Core.SourceGenerator;

internal sealed class InterfaceEntityModel(
    string entityName, List<List<IPropertySymbol>> properties)
{
    private static readonly SymbolDisplayFormat NullableTypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    internal static string TypeName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    internal static InterfaceEntityModel? TryDescribe(INamedTypeSymbol type, Compilation compilation, out string failure)
    {
        var properties = new List<List<IPropertySymbol>>();
        var propertiesByName = new Dictionary<string, List<IPropertySymbol>>(StringComparer.Ordinal);
        if (type.IsFileLocal || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
        {
            failure = "the interface is not accessible to generated code";
            return null;
        }

        // The most-derived declaration precedes inherited declarations for each logical property.
        foreach (var owner in type.AllInterfaces.Insert(0, type))
        {
            foreach (var member in owner.GetMembers())
            {
                if (member is IMethodSymbol { AssociatedSymbol: IPropertySymbol } or INamedTypeSymbol)
                {
                    continue;
                }

                if (member is not IPropertySymbol
                    {
                        IsStatic: false, IsIndexer: false, ReturnsByRef: false, ReturnsByRefReadonly: false,
                        DeclaredAccessibility: Accessibility.Public, GetMethod.DeclaredAccessibility: Accessibility.Public
                    } property
                    || property.SetMethod is { DeclaredAccessibility: not Accessibility.Public })
                {
                    failure = $"member '{member.Name}' is not a public instance property with a getter";
                    return null;
                }

                if (property.Type.TypeKind is TypeKind.Interface or TypeKind.Pointer or TypeKind.FunctionPointer
                    || property.Type.IsRefLikeType || !compilation.IsSymbolAccessibleWithin(property.Type, compilation.Assembly))
                {
                    failure = $"property '{property.Name}' must have an accessible, non-interface storage type";
                    return null;
                }

                if (!propertiesByName.TryGetValue(property.Name, out var declarations))
                {
                    declarations = new List<IPropertySymbol>();
                    propertiesByName.Add(property.Name, declarations);
                    properties.Add(declarations);
                }
                else if (!SymbolEqualityComparer.Default.Equals(declarations[0].Type, property.Type))
                {
                    failure = $"property '{property.Name}' is declared with conflicting types";
                    return null;
                }

                declarations.Add(property);
            }
        }

        if (properties.Count == 0)
        {
            failure = "the interface declares no readable instance properties";
            return null;
        }

        failure = "";
        return new InterfaceEntityModel(TypeName(type), properties);
    }

    internal void EmitRegistration(StringBuilder source, int model)
    {
        source.Append("        global::NextORM.Core.GeneratedInterfaceEntity.Register<")
            .Append(entityName).Append(">(typeof(__NextOrmInterfaceEntity").Append(model).AppendLine("), static () =>");
        source.AppendLine("        new global::System.Reflection.PropertyInfo[]");
        source.AppendLine("        {");
        foreach (var declarations in properties)
        {
            var property = declarations.FirstOrDefault(static p => p.GetAttributes().Length > 0) ?? declarations[0];
            source.Append("            typeof(").Append(TypeName(property.ContainingType)).Append(").GetProperty(")
                .Append(SymbolDisplay.FormatLiteral(property.Name, quote: true))
                .AppendLine(", global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.DeclaredOnly)!,");
        }

        source.AppendLine("        });");
    }

    internal void EmitImplementation(StringBuilder source, int model)
    {
        source.Append("file sealed class __NextOrmInterfaceEntity").Append(model).Append(" : ").AppendLine(entityName);
        source.AppendLine("{");
        source.Append("    public __NextOrmInterfaceEntity").Append(model).AppendLine("() { }");
        var slot = 0;
        foreach (var declarations in properties)
        {
            source.Append("    public ").Append(declarations[0].Type.ToDisplayString(NullableTypeFormat))
                .Append(" Slot").Append(slot).AppendLine(" { get; set; } = default!;");
            foreach (var property in declarations)
            {
                source.Append("    ").Append(property.Type.ToDisplayString(NullableTypeFormat)).Append(' ')
                    .Append(TypeName(property.ContainingType)).Append(".@").Append(property.Name)
                    .Append(" { get => Slot").Append(slot).Append("; ");
                if (property.SetMethod is { } setter)
                {
                    source.Append(setter.IsInitOnly ? "init" : "set")
                        .Append(" => Slot").Append(slot).AppendLine(" = value!; }");
                }
                else
                {
                    source.AppendLine("}");
                }
            }

            slot++;
        }

        source.AppendLine("}");
    }
}

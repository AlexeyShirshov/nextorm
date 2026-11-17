using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NextORM.Core.SourceGenerator;

/// <summary>
/// Emits compile-time row materializers for every type a project can materialize a row into.
/// <para>
/// The output is one provider class per assembly plus a module initializer that registers it in
/// <c>NextORM.Core.RowMaterializerRegistry</c>. For each covered type the provider builds a
/// <c>Func&lt;IDataRecord, T&gt;</c> that reads the projected columns with the same typed accessors
/// the expression path would emit and binds them positionally to the longest public constructor when
/// the column count matches it, otherwise by member name on a parameterless constructor.
/// </para>
/// <para>
/// Coverage needs no annotation: every non-generic class and struct declared in the compilation is
/// considered, and so is every anonymous type the source creates (matched by shape, because an
/// anonymous type has no name to write). <c>[RowMaterializer]</c> and <c>[SqlTable]</c> remain
/// supported as documentation of intent, but they no longer change what is generated.
/// </para>
/// <para>
/// The provider is deliberately conservative: it claims a shape only when every column it receives
/// maps to a member of the expected type, and it returns <see langword="null"/> otherwise, leaving the
/// built-in expression path in place. Types whose members use a column type outside the plain
/// <c>IDataRecord</c> getter set are not covered at all.
/// </para>
/// </summary>
[Generator]
internal sealed class RowMaterializerGenerator : IIncrementalGenerator
{
    private const string GeneratedNamespace = "NextORM.Core.Generated";
    private const string ProviderName = "__NextOrmRowMaterializerProvider";

    /// <summary>
    /// Numeric column types whose read goes through a field-type dispatch helper (see
    /// <see cref="NumericReaders"/>): a provider may report the row's storage type narrower or wider
    /// than the projected CLR type (SQL Server int column projected as long, and the reverse), and the
    /// built-in mapper handles that through the provider's column mapping. A generated materializer has
    /// no provider to ask, so it reads the runtime field type and widens with the typed Convert.
    /// </summary>
    private static readonly Dictionary<string, string> NumericReads = new(StringComparer.Ordinal)
    {
        ["System.Byte"] = "ReadByte",
        ["System.Int16"] = "ReadInt16",
        ["System.Int32"] = "ReadInt32",
        ["System.Int64"] = "ReadInt64",
        ["System.Single"] = "ReadSingle",
        ["System.Double"] = "ReadDouble",
        ["System.Decimal"] = "ReadDecimal",
    };

    /// <summary>Column CLR type to the reader accessor that reads it, mirroring the accessor table.</summary>
    private static readonly Dictionary<string, string> Accessors = new(StringComparer.Ordinal)
    {
        ["System.Int32"] = "GetInt32",
        ["System.Int64"] = "GetInt64",
        ["System.Int16"] = "GetInt16",
        ["System.Byte"] = "GetByte",
        ["System.Boolean"] = "GetBoolean",
        ["System.Single"] = "GetFloat",
        ["System.Double"] = "GetDouble",
        ["System.Decimal"] = "GetDecimal",
        ["System.String"] = "GetString",
        ["System.Guid"] = "GetGuid",
        ["System.DateTime"] = "GetDateTime",
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Every declared type is a candidate; Describe decides whether a materializer can be emitted for
        // it, so an annotation is not required and a type the generator cannot reproduce is simply not
        // covered (the built-in expression path stays).
        var declared = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!);

        // An anonymous type has no declaration and no name, so it is collected from the object-creation
        // expressions in the source and matched later by shape (see EmitAnonymous).
        var anonymous = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is AnonymousObjectCreationExpressionSyntax,
                static (ctx, _) => ctx.SemanticModel.GetTypeInfo((AnonymousObjectCreationExpressionSyntax)ctx.Node).Type as INamedTypeSymbol)
            .Where(static symbol => symbol is { IsAnonymousType: true })
            .Select(static (symbol, _) => symbol!);

        context.RegisterSourceOutput(
            declared.Collect().Combine(anonymous.Collect()),
            static (output, pair) => Emit(output, pair.Left, pair.Right));
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> declared, ImmutableArray<INamedTypeSymbol> anonymous)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var covered = new List<CoveredType>();
        foreach (var symbol in declared)
        {
            if (!seen.Add(symbol) || Describe(symbol) is not { } type)
                continue;
            covered.Add(type);
        }

        covered.Sort(static (left, right) => string.CompareOrdinal(left.FullName, right.FullName));

        var seenShapes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var shapes = new List<CoveredAnonymous>();
        foreach (var symbol in anonymous)
        {
            if (!seenShapes.Add(symbol) || DescribeAnonymous(symbol) is not { } shape)
                continue;
            shapes.Add(shape);
        }

        shapes.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

        if (covered.Count == 0 && shapes.Count == 0)
            return;

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.Append("namespace ").Append(GeneratedNamespace).AppendLine(";");
        source.AppendLine();
        source.Append("internal sealed class ").Append(ProviderName)
            .AppendLine(" : global::NextORM.Core.IRowMaterializerProvider");
        source.AppendLine("{");
        source.Append("    public static readonly ").Append(ProviderName).AppendLine(" Instance = new();");

        for (var i = 0; i < shapes.Count; i++)
        {
            source.AppendLine();
            source.Append("    // Anonymous type #").Append(i).Append(": ").AppendLine(shapes[i].Key);
            source.Append("    private static readonly global::System.Type AnonymousShape").Append(i)
                .Append(" = new { ").Append(shapes[i].ShapeExpression).AppendLine(" }.GetType();");
        }

        source.AppendLine();
        source.AppendLine("    public global::System.Delegate? TryCreate(");
        source.AppendLine("        global::System.Type resultType,");
        source.AppendLine("        global::System.ReadOnlySpan<global::NextORM.Core.RowMaterializerColumn> columns)");
        source.AppendLine("    {");

        for (var i = 0; i < covered.Count; i++)
        {
            source.Append("        if (resultType == typeof(global::").Append(covered[i].FullName).AppendLine("))");
            source.Append("            return Create").Append(i).AppendLine("(columns);");
        }

        for (var i = 0; i < shapes.Count; i++)
        {
            source.Append("        if (resultType == AnonymousShape").Append(i).AppendLine(")");
            source.Append("            return CreateAnonymous").Append(i).AppendLine("(columns);");
        }

        source.AppendLine();
        source.AppendLine("        return null;");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    private static bool IsRuntimeWidenedNumeric(global::System.Type type)");
        source.AppendLine("    {");
        source.AppendLine("        type = global::System.Nullable.GetUnderlyingType(type) ?? type;");
        source.AppendLine("        return type == typeof(byte) || type == typeof(short) || type == typeof(int)");
        source.AppendLine("            || type == typeof(long) || type == typeof(float) || type == typeof(double)");
        source.AppendLine("            || type == typeof(decimal);");
        source.AppendLine("    }");

        for (var i = 0; i < covered.Count; i++)
        {
            source.AppendLine();
            EmitFactory(source, covered[i], i);
        }

        for (var i = 0; i < shapes.Count; i++)
        {
            source.AppendLine();
            EmitAnonymousFactory(source, shapes[i], i);
        }

        if (NeedsNumericReaders(covered, shapes))
        {
            source.AppendLine();
            source.Append(NumericReaders);
        }

        source.AppendLine();
        source.AppendLine("    private static int Find(");
        source.AppendLine("        global::System.ReadOnlySpan<global::NextORM.Core.RowMaterializerColumn> columns,");
        source.AppendLine("        string name)");
        source.AppendLine("    {");
        source.AppendLine("        for (var i = 0; i < columns.Length; i++)");
        source.AppendLine("        {");
        source.AppendLine("            if (columns[i].Name == name)");
        source.AppendLine("                return i;");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        return -1;");
        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("internal static class __NextOrmRowMaterializerRegistration");
        source.AppendLine("{");
        source.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        source.AppendLine("    internal static void Register() =>");
        source.Append("        global::NextORM.Core.RowMaterializerRegistry.Register(")
            .Append(GeneratedNamespace).Append('.').Append(ProviderName).AppendLine(".Instance);");
        source.AppendLine("}");

        context.AddSource("NextORM.RowMaterializers.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void EmitFactory(StringBuilder source, CoveredType type, int index)
    {
        source.Append("    private static global::System.Delegate? Create").Append(index)
            .AppendLine("(global::System.ReadOnlySpan<global::NextORM.Core.RowMaterializerColumn> columns)");
        source.AppendLine("    {");

        // Narrow JIT fallback, only for a type whose public constructors are all parameterless: the
        // expression fallback then builds the shape through member-init. A type with an alternate
        // (longer) public constructor keeps the generated route even for one column, because the core
        // constructor resolver would select that longer constructor and fail on a one-column shape.
        if (type.ParameterlessOnly && !type.IsValueType)
        {
            source.AppendLine("        if (global::System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported");
            source.AppendLine("            && columns.Length == 1 && IsRuntimeWidenedNumeric(columns[0].Type))");
            source.AppendLine("            return null;");
            source.AppendLine();
        }

        if (type.ConstructorParameters is { } parameters)
        {
            source.Append("        if (columns.Length == ").Append(parameters.Length).AppendLine(")");
            source.AppendLine("        {");
            for (var i = 0; i < parameters.Length; i++)
            {
                source.Append("            if (columns[").Append(i).Append("].Type != typeof(global::")
                    .Append(parameters[i].TypeName).AppendLine("))");
                source.AppendLine("                return null;");
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                source.Append("            var p").Append(i).Append(" = columns[").Append(i).AppendLine("].Ordinal;");
            }

            source.Append("            return (global::System.Func<global::System.Data.IDataRecord, global::")
                .Append(type.FullName).Append(">)(r => new global::").Append(type.FullName).Append('(');
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                    source.Append(", ");
                EmitRead(source, parameters[i], "p" + i);
            }

            source.AppendLine("));");
            source.AppendLine("        }");
            source.AppendLine();
        }

        if (type.PropertiesUsable && type.Properties.Count > 0)
        {
            for (var i = 0; i < type.Properties.Count; i++)
            {
                source.Append("        var m").Append(i).Append(" = Find(columns, \"").Append(type.Properties[i].Name)
                    .AppendLine("\");");
            }

            // Every projected column must belong to a property of this type; otherwise the shape is
            // not ours to claim and the expression path keeps it.
            source.AppendLine("        for (var i = 0; i < columns.Length; i++)");
            source.AppendLine("        {");
            source.Append("            var known = ");
            for (var i = 0; i < type.Properties.Count; i++)
            {
                if (i > 0)
                    source.Append(" || ");
                source.Append("columns[i].Name == \"").Append(type.Properties[i].Name).Append('"');
            }

            source.AppendLine(";");
            source.AppendLine("            if (!known)");
            source.AppendLine("                return null;");
            source.AppendLine("        }");
            source.AppendLine();

            for (var i = 0; i < type.Properties.Count; i++)
            {
                source.Append("        if (m").Append(i).Append(" >= 0 && columns[m").Append(i)
                    .Append("].Type != typeof(global::").Append(type.Properties[i].TypeName).AppendLine("))");
                source.AppendLine("            return null;");
            }

            source.AppendLine();
            source.Append("        return (global::System.Func<global::System.Data.IDataRecord, global::")
                .Append(type.FullName).Append(">)(r => new global::").Append(type.FullName).AppendLine("()");
            source.AppendLine("        {");
            for (var i = 0; i < type.Properties.Count; i++)
            {
                var property = type.Properties[i];
                source.Append("            @").Append(property.Name).Append(" = m").Append(i).Append(" >= 0 ? ");
                EmitRead(source, property, "m" + i);
                source.AppendLine(" : default!,");
            }

            source.AppendLine("        });");
            source.AppendLine("    }");
            return;
        }

        source.AppendLine("        return null;");
        source.AppendLine("    }");
    }

    /// <summary>
    /// Emits the factory for one anonymous shape. The shape type is identified by the processor field
    /// emitted earlier: an anonymous type with the same property names, types and order is the same
    /// type in this assembly, so comparing against that field is an exact shape check. The materializer
    /// itself is a lambda with a natural delegate type, which is why it needs no name for the shape.
    /// </summary>
    private static void EmitAnonymousFactory(StringBuilder source, CoveredAnonymous shape, int index)
    {
        source.Append("    private static global::System.Delegate? CreateAnonymous").Append(index)
            .AppendLine("(global::System.ReadOnlySpan<global::NextORM.Core.RowMaterializerColumn> columns)");
        source.AppendLine("    {");
        source.Append("        if (columns.Length != ").Append(shape.Properties.Length).AppendLine(")");
        source.AppendLine("            return null;");

        for (var i = 0; i < shape.Properties.Length; i++)
        {
            source.Append("        if (columns[").Append(i).Append("].Type != typeof(global::")
                .Append(shape.Properties[i].TypeName).AppendLine("))");
            source.AppendLine("            return null;");
        }

        for (var i = 0; i < shape.Properties.Length; i++)
        {
            source.Append("        var p").Append(i).Append(" = columns[").Append(i).AppendLine("].Ordinal;");
        }

        source.AppendLine();
        source.Append("        var materializer = (global::System.Data.IDataRecord r) => new { ");
        for (var i = 0; i < shape.Properties.Length; i++)
        {
            if (i > 0)
                source.Append(", ");
            source.Append('@').Append(shape.Properties[i].Name).Append(" = ");
            EmitRead(source, shape.Properties[i], "p" + i);
        }

        source.AppendLine(" };");
        source.AppendLine("        return materializer;");
        source.AppendLine("    }");
    }

    private static void EmitRead(StringBuilder source, MemberShape member, string ordinal)
    {
        var call = NumericReads.TryGetValue(member.TypeName, out var helper)
            ? helper + "(r, " + ordinal + ")"
            : "r." + Accessors[member.TypeName] + "(" + ordinal + ")";

        if (member.IsReference)
        {
            source.Append("(r.IsDBNull(").Append(ordinal).Append(") ? null! : ").Append(call).Append(')');
            return;
        }

        if (member.IsNullableValueType)
        {
            source.Append("(r.IsDBNull(").Append(ordinal).Append(") ? (").Append(member.NullableTypeName)
                .Append(")null : ").Append(call).Append(')');
            return;
        }

        source.Append(call);
    }

    private static bool NeedsNumericReaders(List<CoveredType> covered, List<CoveredAnonymous> shapes)
    {
        foreach (var type in covered)
        {
            if (type.ConstructorParameters is { } parameters)
            {
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (NumericReads.ContainsKey(parameters[i].TypeName))
                        return true;
                }
            }

            foreach (var property in type.Properties)
            {
                if (NumericReads.ContainsKey(property.TypeName))
                    return true;
            }
        }

        foreach (var shape in shapes)
        {
            for (var i = 0; i < shape.Properties.Length; i++)
            {
                if (NumericReads.ContainsKey(shape.Properties[i].TypeName))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The per-declared-type numeric readers. A provider may report the row storage type narrower or
    /// wider than the projected CLR type (SQL Server returns an int for a column projected as long, and
    /// a bigint for one projected as int), and the built-in mapper absorbs that through the provider's
    /// column mapping. A generated materializer has no provider to ask, so it inspects the runtime field
    /// type and applies the same typed Convert the mapper would; a type outside the numeric set falls
    /// back to the boxed GetValue conversion, and a decimal column keeps GetDecimal so a float/double
    /// storage or a driver-native decimal (ClickHouseDecimal) matches the materialized value.
    /// </summary>
    private const string NumericReaders = """
    private static byte ReadByte(global::System.Data.IDataRecord r, int ordinal)
    {
        var field = r.GetFieldType(ordinal);
        if (field == typeof(byte)) return r.GetByte(ordinal);
        if (field == typeof(short)) return global::System.Convert.ToByte(r.GetInt16(ordinal));
        if (field == typeof(int)) return global::System.Convert.ToByte(r.GetInt32(ordinal));
        if (field == typeof(long)) return global::System.Convert.ToByte(r.GetInt64(ordinal));
        if (field == typeof(float)) return global::System.Convert.ToByte(r.GetFloat(ordinal));
        if (field == typeof(double)) return global::System.Convert.ToByte(r.GetDouble(ordinal));
        if (field == typeof(decimal)) return global::System.Convert.ToByte(r.GetDecimal(ordinal));
        return global::System.Convert.ToByte(r.GetValue(ordinal));
    }

    private static short ReadInt16(global::System.Data.IDataRecord r, int ordinal)
    {
        var field = r.GetFieldType(ordinal);
        if (field == typeof(short)) return r.GetInt16(ordinal);
        if (field == typeof(byte)) return global::System.Convert.ToInt16(r.GetByte(ordinal));
        if (field == typeof(int)) return global::System.Convert.ToInt16(r.GetInt32(ordinal));
        if (field == typeof(long)) return global::System.Convert.ToInt16(r.GetInt64(ordinal));
        if (field == typeof(float)) return global::System.Convert.ToInt16(r.GetFloat(ordinal));
        if (field == typeof(double)) return global::System.Convert.ToInt16(r.GetDouble(ordinal));
        if (field == typeof(decimal)) return global::System.Convert.ToInt16(r.GetDecimal(ordinal));
        return global::System.Convert.ToInt16(r.GetValue(ordinal));
    }

    private static int ReadInt32(global::System.Data.IDataRecord r, int ordinal)
    {
        var field = r.GetFieldType(ordinal);
        if (field == typeof(int)) return r.GetInt32(ordinal);
        if (field == typeof(byte)) return global::System.Convert.ToInt32(r.GetByte(ordinal));
        if (field == typeof(short)) return global::System.Convert.ToInt32(r.GetInt16(ordinal));
        if (field == typeof(long)) return global::System.Convert.ToInt32(r.GetInt64(ordinal));
        if (field == typeof(float)) return global::System.Convert.ToInt32(r.GetFloat(ordinal));
        if (field == typeof(double)) return global::System.Convert.ToInt32(r.GetDouble(ordinal));
        if (field == typeof(decimal)) return global::System.Convert.ToInt32(r.GetDecimal(ordinal));
        return global::System.Convert.ToInt32(r.GetValue(ordinal));
    }

    private static long ReadInt64(global::System.Data.IDataRecord r, int ordinal)
    {
        var field = r.GetFieldType(ordinal);
        if (field == typeof(long)) return r.GetInt64(ordinal);
        if (field == typeof(byte)) return global::System.Convert.ToInt64(r.GetByte(ordinal));
        if (field == typeof(short)) return global::System.Convert.ToInt64(r.GetInt16(ordinal));
        if (field == typeof(int)) return global::System.Convert.ToInt64(r.GetInt32(ordinal));
        if (field == typeof(float)) return global::System.Convert.ToInt64(r.GetFloat(ordinal));
        if (field == typeof(double)) return global::System.Convert.ToInt64(r.GetDouble(ordinal));
        if (field == typeof(decimal)) return global::System.Convert.ToInt64(r.GetDecimal(ordinal));
        return global::System.Convert.ToInt64(r.GetValue(ordinal));
    }

    private static float ReadSingle(global::System.Data.IDataRecord r, int ordinal)
    {
        var field = r.GetFieldType(ordinal);
        if (field == typeof(float)) return r.GetFloat(ordinal);
        if (field == typeof(byte)) return global::System.Convert.ToSingle(r.GetByte(ordinal));
        if (field == typeof(short)) return global::System.Convert.ToSingle(r.GetInt16(ordinal));
        if (field == typeof(int)) return global::System.Convert.ToSingle(r.GetInt32(ordinal));
        if (field == typeof(long)) return global::System.Convert.ToSingle(r.GetInt64(ordinal));
        if (field == typeof(double)) return global::System.Convert.ToSingle(r.GetDouble(ordinal));
        if (field == typeof(decimal)) return global::System.Convert.ToSingle(r.GetDecimal(ordinal));
        return global::System.Convert.ToSingle(r.GetValue(ordinal));
    }

    private static double ReadDouble(global::System.Data.IDataRecord r, int ordinal)
    {
        var field = r.GetFieldType(ordinal);
        if (field == typeof(double)) return r.GetDouble(ordinal);
        if (field == typeof(byte)) return global::System.Convert.ToDouble(r.GetByte(ordinal));
        if (field == typeof(short)) return global::System.Convert.ToDouble(r.GetInt16(ordinal));
        if (field == typeof(int)) return global::System.Convert.ToDouble(r.GetInt32(ordinal));
        if (field == typeof(long)) return global::System.Convert.ToDouble(r.GetInt64(ordinal));
        if (field == typeof(float)) return global::System.Convert.ToDouble(r.GetFloat(ordinal));
        if (field == typeof(decimal)) return global::System.Convert.ToDouble(r.GetDecimal(ordinal));
        return global::System.Convert.ToDouble(r.GetValue(ordinal));
    }

    private static decimal ReadDecimal(global::System.Data.IDataRecord r, int ordinal)
    {
        var field = r.GetFieldType(ordinal);
        if (field == typeof(decimal) || field == typeof(float) || field == typeof(double)) return r.GetDecimal(ordinal);
        if (field == typeof(byte)) return global::System.Convert.ToDecimal(r.GetByte(ordinal));
        if (field == typeof(short)) return global::System.Convert.ToDecimal(r.GetInt16(ordinal));
        if (field == typeof(int)) return global::System.Convert.ToDecimal(r.GetInt32(ordinal));
        if (field == typeof(long)) return global::System.Convert.ToDecimal(r.GetInt64(ordinal));
        return r.GetDecimal(ordinal);
    }

""";

    private static CoveredType? Describe(INamedTypeSymbol symbol)
    {
        if (symbol.IsAbstract || symbol.IsStatic || symbol.IsGenericType || symbol.IsFileLocal || symbol.IsRefLikeType)
            return null;
        if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            return null;
        if (!IsAccessibleInsideAssembly(symbol))
            return null;

        // A type nested in a generic type has no closed name the generated typeof/cast could write.
        for (var container = symbol.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.IsGenericType)
                return null;
        }

        var fullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);

        // Member-init is usable only when every settable property reads from a plain accessor; an
        // unsupported property disables that branch without dropping the constructor branch. Inherited
        // properties are listed after the declared ones (the most derived declaration wins a name clash),
        // because the built-in member-init binds an inherited property just like a declared one.
        var propertiesUsable = true;
        var properties = new List<MemberShape>();
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        for (var type = symbol; type is not null; type = type.BaseType)
        {
            if (type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType)
                break;

            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.SetMethod is not { DeclaredAccessibility: Accessibility.Public })
                    continue;
                if (!propertyNames.Add(property.Name))
                    continue;

                if (DescribeMember(property.Type, property.Name) is { } shape)
                    properties.Add(shape);
                else
                    propertiesUsable = false;
            }

            // A required member that the emitted initializer does not set would make the generated
            // `new T { ... }` fail to compile, so that branch is dropped instead.
            foreach (var required in type.GetMembers().Where(static m => (m as IPropertySymbol)?.IsRequired == true || (m as IFieldSymbol)?.IsRequired == true))
            {
                if (!propertyNames.Contains(required.Name))
                    propertiesUsable = false;
            }
        }

        var constructors = symbol.InstanceConstructors
            .Where(static c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .ToList();
        var longestLength = constructors.Count == 0 ? 0 : constructors.Max(static c => c.Parameters.Length);
        var longest = constructors.FirstOrDefault(c => c.Parameters.Length == longestLength);
        var ambiguous = constructors.Count(c => c.Parameters.Length == longestLength) > 1;

        MemberShape[]? parameters = null;
        if (!ambiguous && longestLength > 0 && longest is not null)
        {
            var shapes = new MemberShape[longestLength];
            for (var i = 0; i < longestLength; i++)
            {
                var shape = DescribeMember(longest.Parameters[i].Type, longest.Parameters[i].Name);
                if (shape is null)
                    return null;
                shapes[i] = shape;
            }

            parameters = shapes;
        }

        var hasParameterless = constructors.Any(static c => c.Parameters.Length == 0);
        if (parameters is null && !(propertiesUsable && hasParameterless))
            return null;

        var parameterlessOnly = constructors.Count > 0 && constructors.All(static c => c.Parameters.Length == 0);
        return new CoveredType(fullName, parameters, properties, propertiesUsable && hasParameterless,
            symbol.IsValueType, parameterlessOnly);
    }

    /// <summary>
    /// Describes one anonymous shape, or returns <see langword="null"/> when a property type is outside
    /// the plain accessor set (such a shape is never offered to the provider).
    /// </summary>
    private static CoveredAnonymous? DescribeAnonymous(INamedTypeSymbol symbol)
    {
        var properties = symbol.GetMembers().OfType<IPropertySymbol>().ToArray();
        if (properties.Length == 0)
            return null;

        var shapes = new MemberShape[properties.Length];
        var expression = new StringBuilder();
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            if (DescribeMember(property.Type, property.Name) is not { } shape)
                return null;

            shapes[i] = shape;
            if (i > 0)
                expression.Append(", ");
            expression.Append('@').Append(property.Name).Append(" = default(").Append(shape.TypeName).Append(')');
        }

        var key = string.Join("|", shapes.Select(static s => s.Name + ":" + s.TypeName + (s.IsNullableValueType ? "?" : string.Empty)));
        return new CoveredAnonymous(key, shapes, expression.ToString());
    }

    /// <summary>
    /// Whether the type and every type that contains it can be named from generated code in the same
    /// assembly: a private nested type (or a type inside one) is not reachable from another class.
    /// </summary>
    private static bool IsAccessibleInsideAssembly(INamedTypeSymbol symbol)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return false;
        }

        return true;
    }

    private static MemberShape? DescribeMember(ITypeSymbol type, string name)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments[0] is { } underlying)
        {
            // int?, long?, Guid? ...: read the underlying value and lift it.
            var underlyingName = CanonicalName(underlying);
            return underlyingName is not null && Accessors.ContainsKey(underlyingName)
                ? new MemberShape(name, underlyingName, IsReference: false, IsNullableValueType: true, NullableTypeName: underlyingName + "?")
                : null;
        }

        var typeName = CanonicalName(type);
        if (typeName is null || !Accessors.ContainsKey(typeName))
            return null;

        return new MemberShape(name, typeName, type.IsReferenceType, IsNullableValueType: false, NullableTypeName: typeName);
    }

    /// <summary>
    /// The fully qualified name the generated code writes, or <see langword="null"/> for a type the
    /// plain accessor table does not cover. C# keywords are mapped to their metadata names because the
    /// emitter writes <c>typeof(global::…)</c> and casts.
    /// </summary>
    private static string? CanonicalName(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Int32 => "System.Int32",
        SpecialType.System_Int64 => "System.Int64",
        SpecialType.System_Int16 => "System.Int16",
        SpecialType.System_Byte => "System.Byte",
        SpecialType.System_Boolean => "System.Boolean",
        SpecialType.System_Single => "System.Single",
        SpecialType.System_Double => "System.Double",
        SpecialType.System_Decimal => "System.Decimal",
        SpecialType.System_String => "System.String",
        SpecialType.System_DateTime => "System.DateTime",
        SpecialType.None => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
        _ => null,
    };

    private sealed record CoveredType(
        string FullName,
        MemberShape[]? ConstructorParameters,
        List<MemberShape> Properties,
        bool PropertiesUsable,
        bool IsValueType,
        bool ParameterlessOnly);

    private sealed record CoveredAnonymous(
        string Key,
        MemberShape[] Properties,
        string ShapeExpression);

    private sealed record MemberShape(
        string Name,
        string TypeName,
        bool IsReference,
        bool IsNullableValueType,
        string NullableTypeName);
}

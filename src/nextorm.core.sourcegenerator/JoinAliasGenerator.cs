using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NextORM.Core.SourceGenerator;

/// <summary>
/// Production incremental source generator for issue #113 (join alias projection).
/// <para>
/// For every fluent join call whose last argument is the generated <c>Alias.&lt;Name&gt;</c> marker, the
/// generator reconstructs the ordered alias chain, emits a typed lexical projection/builder pair
/// (<c>AliasProjection_</c>/<c>AliasJoin_</c>, both <c>public</c>) and an extension method that mirrors
/// the operator name and forwards the matching <c>NextORM.Core.JoinType</c>. All output lands in
/// the reserved namespace <c>NextORM.Generated.&lt;normalized-assembly-name&gt;</c> together with the
/// public marker class <c>Alias</c>.
/// </para>
/// <para>
/// The pipeline is incremental: a cheap syntactic predicate selects candidate call sites, a transform
/// binds them to a value-equatable model (strings/spans only), and the collected models plus the
/// assembly name drive a single <c>RegisterSourceOutput</c>. Emitted names/members depend only on the
/// ordered alias schema and entity type parameters — never on file order or line numbers (all
/// collections are sorted ordinally before emission).
/// </para>
/// </summary>
[Generator]
internal sealed class JoinAliasGenerator : IIncrementalGenerator
{
    /// <summary>Maximum projection arity supported by <c>Projection&lt;T1..T8&gt;</c>.</summary>
    private const int MaxSlots = 8;

    /// <summary>Root of the reserved generated namespace; the consumer assembly name is appended.</summary>
    private const string GeneratedNamespaceRoot = "NextORM.Generated";

    /// <summary>Stable diagnostic id prefix for every rule this generator reports.</summary>
    private const string DiagnosticIdPrefix = "NORMGEN";

    private const string DiagnosticCategory = "NextORM.JoinAlias";

    private static readonly HashSet<string> JoinOperators = new(StringComparer.Ordinal)
    {
        "Join", "LeftJoin", "RightJoin", "FullJoin", "CrossJoin", "CrossApply", "OuterApply"
    };

    private static readonly HashSet<string> ConditionlessOperators = new(StringComparer.Ordinal)
    {
        "CrossJoin", "CrossApply", "OuterApply"
    };

    private static readonly DiagnosticDescriptor DuplicateAlias = new(
        DiagnosticIdPrefix + "001",
        "Duplicate join alias",
        "The join alias '{0}' is already used in this projection",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Aliases are compile-time slot names; every alias in one resulting projection must be unique.");

    private static readonly DiagnosticDescriptor AliasCollision = new(
        DiagnosticIdPrefix + "002",
        "Join alias collides with a generated member",
        "The join alias '{0}' collides with generated member '{1}'",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An alias must not collide with the retained ItemN members or with another member generated for an alias.");

    private static readonly DiagnosticDescriptor InvalidAliasIdentifier = new(
        DiagnosticIdPrefix + "003",
        "Join alias is not a valid identifier",
        "The join alias '{0}' is not valid under the generated-name escaping policy",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Alias names must be valid C# identifiers and must not contain '_', which is reserved for composing generated type names.");

    private static readonly DiagnosticDescriptor ArityExceeded = new(
        DiagnosticIdPrefix + "004",
        "Alias projection exceeds the maximum arity",
        "The alias projection would have {0} slots; the maximum supported by Projection<T1..T8> is 8",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A ninth slot is rejected; project the accumulated join into a named type and continue from there.");

    private static readonly DiagnosticDescriptor UnapprovedAliasArgument = new(
        DiagnosticIdPrefix + "005",
        "Alias argument is not in the approved form",
        "The join alias argument must use the generated form 'Alias.<Name>'",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only the generated Alias marker class may supply an alias argument.");

    private static readonly DiagnosticDescriptor AssemblyNameNotNormalizable = new(
        DiagnosticIdPrefix + "006",
        "Assembly name cannot be normalized to a namespace",
        "The source generator cannot derive a namespace from assembly name '{0}': {1}",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The consumer assembly name must contain at least one usable character and normalize to a valid C# identifier.");

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName);

        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsCandidate(node),
            static (ctx, ct) => Transform(ctx, ct))
            .Collect();

        context.RegisterSourceOutput(
            assemblyName.Combine(candidates),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    // ---------------------------------------------------------------------------------------------
    // Pipeline: predicate, transform and chain resolution.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Cheap syntactic predicate: a fluent call to one of the seven join operators whose last argument
    /// is a member access rooted at the identifier <c>Alias</c>. Nothing is bound here.
    /// </summary>
    private static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name is not SimpleNameSyntax name || !JoinOperators.Contains(name.Identifier.ValueText)) return false;
        if (name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count != 1) return false;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count is not (2 or 3)) return false;

        return IsAliasRootedMemberAccess(arguments[arguments.Count - 1].Expression);
    }

    /// <summary>True when the expression is a member-access chain whose root identifier is <c>Alias</c>.</summary>
    private static bool IsAliasRootedMemberAccess(ExpressionSyntax expression)
    {
        while (expression is MemberAccessExpressionSyntax memberAccess) expression = memberAccess.Expression;
        return expression is IdentifierNameSyntax identifier
            && string.Equals(identifier.Identifier.ValueText, "Alias", StringComparison.Ordinal);
    }

    /// <summary>
    /// Binds a candidate call site to a value-equatable <see cref="Candidate"/>. Returns <c>null</c> for
    /// shapes the generator intentionally does not handle (for example a correlated APPLY lambda source,
    /// whose joined type cannot be bound before the generated overload exists).
    /// </summary>
    private static Candidate? Transform(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = (SimpleNameSyntax)memberAccess.Name;
        var operation = name.Identifier.ValueText;
        var arguments = invocation.ArgumentList.Arguments;
        var aliasExpression = arguments[arguments.Count - 1].Expression;
        var aliasLocation = ToAliasLocation(aliasExpression.GetLocation());

        if (TryReadAliasArgument(aliasExpression, out var alias))
        {
            if (!JoinOperators.Contains(operation)) return null;

            var conditionless = ConditionlessOperators.Contains(operation);
            if (conditionless ? arguments.Count != 2 : arguments.Count != 3) return null;

            if (!TryGetJoinedType(invocation, name, semanticModel, ct, out var joined)) return null;

            var receiver = ResolveChain(memberAccess.Expression, semanticModel, ct);
            if (receiver is null) return null;

            var steps = new AliasStep[receiver.Steps.Count + 1];
            for (var i = 0; i < receiver.Steps.Count; i++) steps[i] = receiver.Steps[i];
            steps[receiver.Steps.Count] = new AliasStep(alias, Display(joined), operation, conditionless, aliasLocation);
            return new Candidate(true, alias, new ChainModel(receiver.BaseType, new EquatableArray<AliasStep>(steps)), aliasLocation);
        }

        // The argument is member-access rooted at 'Alias' (per the predicate) but not the approved
        // 'Alias.<identifier>' form, e.g. 'Alias.Buyer<int>' or 'Alias.Buyer.Approver'.
        return new Candidate(false, string.Empty, null, aliasLocation);
    }

    /// <summary>Rebuilds the ordered alias chain ending at the receiver expression.</summary>
    private static ChainModel? ResolveChain(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken ct)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return ResolveChain(parenthesized.Expression, semanticModel, ct);

            case InvocationExpressionSyntax invocation:
                if (TryReadAliasStep(invocation, semanticModel, ct, out var receiver, out var step))
                {
                    var previous = ResolveChain(receiver, semanticModel, ct);
                    if (previous is null) return null;

                    var steps = new AliasStep[previous.Steps.Count + 1];
                    for (var i = 0; i < previous.Steps.Count; i++) steps[i] = previous.Steps[i];
                    steps[previous.Steps.Count] = step;
                    return new ChainModel(previous.BaseType, new EquatableArray<AliasStep>(steps));
                }

                break;

            case IdentifierNameSyntax identifier:
                return ResolveIdentifier(identifier, semanticModel, ct);
        }

        if (semanticModel.GetTypeInfo(expression, ct).Type is INamedTypeSymbol builder)
            return ResolveBuilderSymbol(builder);

        return null;
    }

    /// <summary>
    /// Recovers the chain root the generator understands from a builder type: a plain
    /// <c>EntityBuilder&lt;T&gt;</c>. A receiver that accumulated positional joins
    /// (<c>JoinedEntityBuilder&lt;T1..Tn&gt;</c>) is intentionally not recognized, because
    /// positional-to-alias chaining is unsupported: aliases are a self-contained chained API.
    /// </summary>
    private static ChainModel? ResolveBuilderSymbol(INamedTypeSymbol builder)
    {
        if (builder.Name == "EntityBuilder"
            && builder.TypeArguments.Length == 1
            && builder.TypeArguments[0] is { } entity)
        {
            return new ChainModel(Display(entity), EquatableArray<AliasStep>.Empty);
        }

        return null;
    }

    /// <summary>Reads one alias step from an invocation, returning its receiver so the chain can be walked.</summary>
    private static bool TryReadAliasStep(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken ct,
        out ExpressionSyntax receiver,
        out AliasStep step)
    {
        receiver = null!;
        step = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name is not SimpleNameSyntax name || !JoinOperators.Contains(name.Identifier.ValueText)) return false;

        var operation = name.Identifier.ValueText;
        var arguments = invocation.ArgumentList.Arguments;
        var conditionless = ConditionlessOperators.Contains(operation);
        if (conditionless ? arguments.Count != 2 : arguments.Count != 3) return false;

        var aliasExpression = arguments[arguments.Count - 1].Expression;
        if (!TryReadAliasArgument(aliasExpression, out var alias)) return false;
        if (!TryGetJoinedType(invocation, name, semanticModel, ct, out var joined)) return false;

        receiver = memberAccess.Expression;
        step = new AliasStep(alias, Display(joined), operation, conditionless, ToAliasLocation(aliasExpression.GetLocation()));
        return true;
    }

    /// <summary>Resolves a local variable initializer or a parameter's generated builder type.</summary>
    private static ChainModel? ResolveIdentifier(IdentifierNameSyntax identifier, SemanticModel semanticModel, CancellationToken ct)
    {
        var symbol = semanticModel.GetSymbolInfo(identifier, ct).Symbol;
        var reference = symbol?.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null) return null;

        if (symbol is ILocalSymbol && reference.GetSyntax(ct) is VariableDeclaratorSyntax declarator && declarator.Initializer is not null)
            return ResolveChain(declarator.Initializer.Value, semanticModel, ct);

        if (symbol is IParameterSymbol && reference.GetSyntax(ct) is ParameterSyntax parameter && parameter.Type is not null)
            return ResolveFromTypeSyntax(parameter.Type, semanticModel, ct);

        return null;
    }

    /// <summary>
    /// Recovers a chain from a written generated builder type (<c>AliasJoin_&lt;suffix&gt;&lt;...&gt;</c>).
    /// The suffix is unambiguous because alias names may not contain '_' (see the escaping policy).
    /// </summary>
    private static ChainModel? ResolveFromTypeSyntax(TypeSyntax type, SemanticModel semanticModel, CancellationToken ct)
    {
        var generic = GetRightmostGenericName(type);
        if (generic is not null && generic.Identifier.ValueText.StartsWith("AliasJoin_", StringComparison.Ordinal))
        {
            var aliasText = generic.Identifier.ValueText.Substring("AliasJoin_".Length);
            if (aliasText.Length == 0) return null;

            var aliases = aliasText.Split('_');
            var typeArguments = generic.TypeArgumentList.Arguments;
            if (typeArguments.Count != aliases.Length + 1) return null;
            if (semanticModel.GetTypeInfo(typeArguments[0], ct).Type is not INamedTypeSymbol baseType) return null;

            var steps = new AliasStep[aliases.Length];
            for (var i = 0; i < aliases.Length; i++)
            {
                if (semanticModel.GetTypeInfo(typeArguments[i + 1], ct).Type is not INamedTypeSymbol joined) return null;
                steps[i] = new AliasStep(aliases[i], Display(joined), "Join", false, default);
            }

            return new ChainModel(Display(baseType), new EquatableArray<AliasStep>(steps));
        }

        if (semanticModel.GetTypeInfo(type, ct).Type is INamedTypeSymbol builder)
            return ResolveBuilderSymbol(builder);

        return null;
    }

    /// <summary>Returns the right-most generic name of a possibly qualified type syntax.</summary>
    private static GenericNameSyntax? GetRightmostGenericName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic,
        QualifiedNameSyntax qualified => GetRightmostGenericName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetRightmostGenericName(alias.Name),
        _ => null
    };

    /// <summary>
    /// Resolves the joined entity type: from the explicit generic type argument when present, otherwise
    /// from an <c>EntityBuilder&lt;T&gt;</c> source argument (type-argument inference at the call site).
    /// </summary>
    private static bool TryGetJoinedType(
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax name,
        SemanticModel semanticModel,
        CancellationToken ct,
        out ITypeSymbol joined)
    {
        joined = null!;

        if (name is GenericNameSyntax generic)
        {
            if (generic.TypeArgumentList.Arguments.Count != 1) return false;
            var explicitType = semanticModel.GetTypeInfo(generic.TypeArgumentList.Arguments[0], ct).Type;
            if (explicitType is null) return false;
            joined = explicitType;
            return true;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0) return false;
        if (semanticModel.GetTypeInfo(arguments[0].Expression, ct).Type is INamedTypeSymbol source
            && source.Name == "EntityBuilder"
            && source.TypeArguments.Length == 1)
        {
            joined = source.TypeArguments[0];
            return true;
        }

        return false;
    }

    /// <summary>Reads the approved <c>Alias.&lt;identifier&gt;</c> argument form.</summary>
    private static bool TryReadAliasArgument(ExpressionSyntax expression, out string alias)
    {
        alias = string.Empty;
        if (expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression is IdentifierNameSyntax owner
            && string.Equals(owner.Identifier.ValueText, "Alias", StringComparison.Ordinal)
            && memberAccess.Name is IdentifierNameSyntax name)
        {
            alias = name.Identifier.ValueText;
            return true;
        }

        return false;
    }

    private static string Display(ITypeSymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    // ---------------------------------------------------------------------------------------------
    // Output: validation, diagnostics and emission.
    // ---------------------------------------------------------------------------------------------

    private static void Execute(SourceProductionContext context, string? assemblyName, ImmutableArray<Candidate?> candidates)
    {
        if (!TryNormalizeNamespace(assemblyName, out var generatedNamespace, out var reason))
        {
            context.ReportDiagnostic(Diagnostic.Create(AssemblyNameNotNormalizable, Location.None, assemblyName ?? "<null>", reason));
            return;
        }

        var diagnostics = new List<Diagnostic>();
        var approved = new List<Candidate>();
        foreach (var candidate in candidates)
        {
            if (candidate is null) continue;
            if (!candidate.Approved)
            {
                diagnostics.Add(Diagnostic.Create(UnapprovedAliasArgument, ToLocation(candidate.Location)));
                continue;
            }

            approved.Add(candidate);
        }

        if (approved.Count == 0)
        {
            Report(context, diagnostics);
            return;
        }

        // One member per discovered alias name; deterministic order.
        var aliasLocations = new Dictionary<string, AliasLocation>(StringComparer.Ordinal);
        foreach (var candidate in approved)
        {
            if (!aliasLocations.ContainsKey(candidate.Alias)) aliasLocations[candidate.Alias] = candidate.Location;
        }

        var aliasNames = aliasLocations.Keys.OrderBy(static a => a, StringComparer.Ordinal).ToList();
        var suppressedAliases = ValidateAliasMembers(aliasNames, aliasLocations, diagnostics);

        // Distinct chains, keyed by the full ordered schema.
        var chains = new Dictionary<string, ChainModel>(StringComparer.Ordinal);
        foreach (var candidate in approved) chains[ChainKey(candidate.Chain!)] = candidate.Chain!;

        var validChains = new List<ChainModel>();
        foreach (var entry in chains.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            var chain = entry.Value;
            var usesSuppressedAlias = false;
            for (var i = 0; i < chain.Steps.Count; i++)
            {
                if (suppressedAliases.Contains(chain.Steps[i].Alias))
                {
                    usesSuppressedAlias = true;
                    break;
                }
            }

            if (usesSuppressedAlias) continue;
            if (ValidateChain(chain, diagnostics)) validChains.Add(chain);
        }

        // Builder/projection types are generic over the entity types and depend on the ordered alias
        // sequence (which fixes the projection arity and the 1-based slot of every alias), including
        // every prefix a valid chain relies on.
        var sequences = new Dictionary<string, EmittedType>(StringComparer.Ordinal);
        foreach (var chain in validChains)
        {
            for (var count = 1; count <= chain.Steps.Count; count++)
            {
                var aliases = new string[count];
                for (var i = 0; i < count; i++) aliases[i] = chain.Steps[i].Alias;
                var arity = 1 + count;
                sequences[string.Join("_", aliases) + "#" + arity] = new EmittedType(arity, aliases);
            }
        }

        var emitter = new StringBuilder();
        emitter.AppendLine("// <auto-generated />");
        emitter.AppendLine("#nullable disable");
        emitter.AppendLine("#pragma warning disable CS1591");
        emitter.AppendLine("namespace " + generatedNamespace);
        emitter.AppendLine("{");

        AppendAliasMarker(emitter, generatedNamespace, aliasNames.Where(alias => !suppressedAliases.Contains(alias)).ToList());
        AppendTypes(emitter, generatedNamespace, sequences);
        AppendExtensions(emitter, generatedNamespace, validChains);

        emitter.AppendLine("}");
        context.AddSource("JoinAlias.g.cs", SourceText.From(emitter.ToString(), Encoding.UTF8));
        Report(context, diagnostics);
    }

    /// <summary>
    /// Validates the single generated <c>Alias</c> class: every name must be a valid identifier under
    /// the escaping policy and must not collide with another member generated in that class. Returns the
    /// set of aliases that must not be emitted.
    /// </summary>
    private static HashSet<string> ValidateAliasMembers(
        List<string> aliasNames,
        Dictionary<string, AliasLocation> aliasLocations,
        List<Diagnostic> diagnostics)
    {
        var suppressed = new HashSet<string>(StringComparer.Ordinal);
        var memberOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var alias in aliasNames)
        {
            if (!IsAliasIdentifierValid(alias))
            {
                diagnostics.Add(Diagnostic.Create(InvalidAliasIdentifier, ToLocation(aliasLocations[alias]), alias));
                suppressed.Add(alias);
                continue;
            }

            if (string.Equals(alias, "Alias", StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic.Create(AliasCollision, ToLocation(aliasLocations[alias]), alias, "Alias"));
                suppressed.Add(alias);
                continue;
            }

            var collidingMember = FindCollision(memberOwners, alias);
            if (collidingMember is not null)
            {
                diagnostics.Add(Diagnostic.Create(AliasCollision, ToLocation(aliasLocations[alias]), alias, collidingMember));
                suppressed.Add(alias);
                continue;
            }

            memberOwners[alias] = alias;
            memberOwners[alias + "Marker"] = alias;
        }

        return suppressed;
    }

    private static string? FindCollision(Dictionary<string, string> memberOwners, string alias)
    {
        if (memberOwners.ContainsKey(alias)) return alias;
        var marker = alias + "Marker";
        if (memberOwners.ContainsKey(marker)) return marker;
        return null;
    }

    /// <summary>
    /// Escaping policy: an alias must be a valid C# identifier and must not contain '_' (reserved for
    /// composing generated type names). Reserved keywords are legal and emitted with an '@' escape.
    /// </summary>
    private static bool IsAliasIdentifierValid(string alias)
    {
        if (alias.Length == 0) return false;
        if (alias.IndexOf('_') >= 0) return false;
        return SyntaxFacts.IsValidIdentifier(alias) || SyntaxFacts.GetKeywordKind(alias) != SyntaxKind.None;
    }

    /// <summary>Validates one chain; returns true when it can be emitted.</summary>
    private static bool ValidateChain(ChainModel chain, List<Diagnostic> diagnostics)
    {
        var steps = chain.Steps;
        var arity = steps.Count + 1;
        var valid = true;

        if (arity > MaxSlots)
        {
            diagnostics.Add(Diagnostic.Create(ArityExceeded, ToLocation(steps[steps.Count - 1].Location), arity));
            valid = false;
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var alias = steps[i].Alias;

            if (IsItemAlias(alias, arity))
            {
                diagnostics.Add(Diagnostic.Create(AliasCollision, ToLocation(steps[i].Location), alias, alias));
                valid = false;
            }

            for (var j = 0; j < i; j++)
            {
                if (string.Equals(steps[j].Alias, alias, StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic.Create(DuplicateAlias, ToLocation(steps[i].Location), alias));
                    valid = false;
                    break;
                }
            }
        }

        return valid;
    }

    private static bool IsItemAlias(string alias, int arity)
    {
        if (!alias.StartsWith("Item", StringComparison.Ordinal)) return false;
        var digits = alias.Substring("Item".Length);
        if (digits.Length == 0) return false;
        for (var i = 0; i < digits.Length; i++)
        {
            if (digits[i] < '0' || digits[i] > '9') return false;
        }

        return int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var position)
            && position >= 1 && position <= arity;
    }

    private static void AppendAliasMarker(StringBuilder emitter, string generatedNamespace, List<string> aliases)
    {
        emitter.AppendLine("    public static class Alias");
        emitter.AppendLine("    {");

        foreach (var alias in aliases)
        {
            emitter.AppendLine("        public sealed class " + alias + "Marker { }");
        }

        foreach (var alias in aliases)
        {
            emitter.AppendLine("        public static " + alias + "Marker " + Escape(alias) + " { get; } = new " + alias + "Marker();");
        }

        emitter.AppendLine("    }");
    }

    private static void AppendTypes(StringBuilder emitter, string generatedNamespace, Dictionary<string, EmittedType> sequences)
    {
        foreach (var sequence in sequences.OrderBy(static kv => kv.Key, StringComparer.Ordinal).Select(static kv => kv.Value))
        {
            var suffix = string.Join("_", sequence.Aliases);
            var arity = sequence.Arity;
            var typeParameters = string.Join(", ", Enumerable.Range(1, arity).Select(static i => "T" + i));
            var projection = generatedNamespace + ".AliasProjection_" + suffix + "<" + typeParameters + ">";
            var builder = generatedNamespace + ".AliasJoin_" + suffix + "<" + typeParameters + ">";

            emitter.AppendLine("    public class AliasProjection_" + suffix + "<" + typeParameters + "> : global::NextORM.Core.Projection<" + typeParameters + ">");
            emitter.AppendLine("    {");
            for (var i = 0; i < sequence.Aliases.Length; i++)
            {
                var position = 2 + i;
                emitter.AppendLine("        [global::NextORM.Core.JoinSlot(" + position + ")]");
                emitter.AppendLine("        public T" + position + " " + Escape(sequence.Aliases[i]) + " => throw new global::System.NotSupportedException();");
            }

            emitter.AppendLine("    }");

            emitter.AppendLine("    public class AliasJoin_" + suffix + "<" + typeParameters + "> : global::NextORM.Core.EntityBuilder<" + projection + ">");
            emitter.AppendLine("    {");
            emitter.AppendLine("        public AliasJoin_" + suffix + "(global::NextORM.Core.IDataContext dataProvider) : base(dataProvider) { }");
            emitter.AppendLine("    }");
        }
    }

    private static void AppendExtensions(StringBuilder emitter, string generatedNamespace, List<ChainModel> validChains)
    {
        emitter.AppendLine("    public static class JoinAliasExtensions");
        emitter.AppendLine("    {");

        foreach (var chain in validChains.OrderBy(static c => ChainKey(c), StringComparer.Ordinal))
        {
            var steps = chain.Steps;
            var count = steps.Count;
            var aliases = new string[count];
            for (var i = 0; i < count; i++) aliases[i] = steps[i].Alias;
            var suffix = string.Join("_", aliases);
            var last = steps[count - 1];

            // The first alias join starts from the plain entity source; every later one extends a
            // previously generated AliasJoin_* builder. Aliases are a self-contained chained API: a
            // positional prefix is never part of a chain.
            string receiverType;
            string conditionArgumentType;
            if (count == 1)
            {
                receiverType = "global::NextORM.Core.EntityBuilder<" + chain.BaseType + ">";
                conditionArgumentType = chain.BaseType;
            }
            else
            {
                var receiverSuffix = string.Join("_", aliases.Take(count - 1));
                var receiverArguments = new List<string> { chain.BaseType };
                for (var i = 0; i < count - 1; i++) receiverArguments.Add(steps[i].JoinedType);
                var receiverArgumentsText = string.Join(", ", receiverArguments);
                receiverType = generatedNamespace + ".AliasJoin_" + receiverSuffix + "<" + receiverArgumentsText + ">";
                conditionArgumentType = generatedNamespace + ".AliasProjection_" + receiverSuffix + "<" + receiverArgumentsText + ">";
            }

            var returnArguments = new List<string> { chain.BaseType };
            for (var i = 0; i < count - 1; i++) returnArguments.Add(steps[i].JoinedType);
            returnArguments.Add("TJoin");
            var returnArgumentsText = string.Join(", ", returnArguments);
            var returnType = generatedNamespace + ".AliasJoin_" + suffix + "<" + returnArgumentsText + ">";
            var projectionType = generatedNamespace + ".AliasProjection_" + suffix + "<" + returnArgumentsText + ">";
            var markerType = generatedNamespace + ".Alias." + last.Alias + "Marker";
            var joinType = "global::NextORM.Core.JoinType." + JoinTypeName(last.Operator);

            emitter.AppendLine("        public static " + returnType + " " + last.Operator + "<TJoin>(");
            emitter.AppendLine("            this " + receiverType + " self,");
            emitter.AppendLine("            global::NextORM.Core.EntityBuilder<TJoin> _,");
            if (!last.Conditionless)
            {
                emitter.AppendLine("            global::System.Linq.Expressions.Expression<global::System.Func<" + conditionArgumentType + ", TJoin, bool>> condition,");
            }

            emitter.AppendLine("            " + markerType + " marker)");
            emitter.AppendLine("            => self.JoinAlias<" + returnType + ", " + projectionType + ", TJoin>(");
            emitter.AppendLine("                static dc => new " + returnType + "(dc),");
            emitter.AppendLine("                _,");
            if (!last.Conditionless)
            {
                emitter.AppendLine("                condition,");
            }

            emitter.AppendLine("                " + joinType + ");");
        }

        emitter.AppendLine("    }");
    }

    /// <summary>Operator-to-<c>NextORM.Core.JoinType</c> mapping for the seven projection operators.</summary>
    private static string JoinTypeName(string operation) => operation switch
    {
        "Join" => "Inner",
        "LeftJoin" => "Left",
        "RightJoin" => "Right",
        "FullJoin" => "Full",
        "CrossJoin" => "Cross",
        "CrossApply" => "CrossApply",
        "OuterApply" => "OuterApply",
        _ => "Inner"
    };

    private static string Escape(string alias)
        => SyntaxFacts.GetKeywordKind(alias) != SyntaxKind.None ? "@" + alias : alias;

    private static string ChainKey(ChainModel chain)
    {
        var builder = new StringBuilder(chain.BaseType);
        for (var i = 0; i < chain.Steps.Count; i++)
        {
            var step = chain.Steps[i];
            builder.Append('|').Append(step.Operator).Append(':').Append(step.Alias).Append(':').Append(step.JoinedType);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Normalizes the consumer assembly name to a single C# namespace segment: every character that is
    /// not a letter, digit or '_' becomes '_', a leading digit is prefixed with '_' and a reserved
    /// keyword is prefixed with '_'.
    /// </summary>
    private static bool TryNormalizeNamespace(string? assemblyName, out string generatedNamespace, out string reason)
    {
        generatedNamespace = string.Empty;
        reason = string.Empty;

        if (assemblyName is null || assemblyName.Trim().Length == 0)
        {
            reason = "assembly name is empty";
            return false;
        }

        var builder = new StringBuilder(assemblyName.Length);
        foreach (var ch in assemblyName)
        {
            var usable = ch == '_' || (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
            builder.Append(usable ? ch : '_');
        }

        if (builder.Length == 0)
        {
            reason = "assembly name has no usable characters";
            return false;
        }

        if (builder[0] >= '0' && builder[0] <= '9') builder.Insert(0, '_');
        var segment = builder.ToString();
        if (SyntaxFacts.GetKeywordKind(segment) != SyntaxKind.None) segment = "_" + segment;

        generatedNamespace = GeneratedNamespaceRoot + "." + segment;
        return true;
    }

    private static AliasLocation ToAliasLocation(Location location)
    {
        var lineSpan = location.GetLineSpan();
        return new AliasLocation(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, lineSpan.Span);
    }

    private static Location ToLocation(AliasLocation location)
        => Location.Create(location.FilePath, location.Span, location.LineSpan);

    private static void Report(SourceProductionContext context, List<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics) context.ReportDiagnostic(diagnostic);
    }

    // ---------------------------------------------------------------------------------------------
    // Value-equatable models (strings, spans and primitives only: safe for incremental caching).
    // ---------------------------------------------------------------------------------------------

    private readonly record struct AliasLocation(string FilePath, TextSpan Span, LinePositionSpan LineSpan);

    private sealed record AliasStep(string Alias, string JoinedType, string Operator, bool Conditionless, AliasLocation Location);

    /// <summary>
    /// One ordered alias chain: the base entity type and the alias steps. Aliases are a self-contained
    /// chained API, so the chain never carries a positional prefix — a positional join must not be mixed
    /// into an alias chain.
    /// </summary>
    private sealed record ChainModel(string BaseType, EquatableArray<AliasStep> Steps);

    /// <summary>Projection/builder type to emit: its generic arity and aliases.</summary>
    private sealed record EmittedType(int Arity, string[] Aliases);

    private sealed record Candidate(bool Approved, string Alias, ChainModel? Chain, AliasLocation Location);

    /// <summary>Immutable array wrapper with structural equality (ImmutableArray uses reference equality).</summary>
    private readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
        where T : class, IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

        private readonly T[] _items;

        public EquatableArray(T[] items) => _items = items;

        public int Count => _items.Length;

        public T this[int index] => _items[index];

        public bool Equals(EquatableArray<T> other)
        {
            if (_items.Length != other._items.Length) return false;
            for (var i = 0; i < _items.Length; i++)
            {
                if (!_items[i].Equals(other._items[i])) return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var hash = 17;
            for (var i = 0; i < _items.Length; i++) hash = (hash * 31) + _items[i].GetHashCode();
            return hash;
        }
    }
}

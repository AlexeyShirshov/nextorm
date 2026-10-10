using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NextORM.Core.SourceGenerator;

/// <summary>
/// Production incremental source generator for issue #113 (join alias projection) and issue #160
/// (free mixing of positional and alias joins plus the root alias).
/// <para>
/// The chain model is an ordered list of slots. Slot 1 is the root source (optionally named through
/// <c>.WithAlias(Alias.X)</c>); every later slot is either a positional join (no name) or an alias
/// join (trailing <c>Alias.&lt;Name&gt;</c> marker). For every used schema the generator emits a typed
/// lexical projection/builder pair (<c>AliasProjection_&lt;suffix&gt;</c>/<c>AliasJoin_&lt;suffix&gt;</c>,
/// both <c>public</c>) whose suffix encodes every slot (<c>P{slot}</c> for positional, <c>A{slot}_{name}</c>
/// for alias). Alias steps are emitted as extension methods (the trailing marker makes the inherited
/// positional overload inapplicable); positional steps after an alias step are emitted as generated
/// <c>new</c> instance methods on the builder (an extension method cannot shadow an applicable instance
/// method). Both kinds route through the existing <c>JoinAlias</c> seam. All output lands in the
/// reserved namespace <c>NextORM.Generated.&lt;normalized-assembly-name&gt;</c> together with the public
/// marker class <c>Alias</c>.
/// </para>
/// <para>
/// The pipeline is incremental: a cheap syntactic predicate selects candidate call sites, a transform
/// binds them to a value-equatable model (strings/spans only), and the collected models plus the
/// assembly name drive a single <c>RegisterSourceOutput</c>. Emitted names/members depend only on the
/// ordered slot schema and entity type parameters — never on file order or line numbers.
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

    /// <summary>
    /// The seven join operators in a stable emission order. A <see cref="HashSet{T}"/> iteration order is
    /// not a contract, and the synthetic root-alias positional transitions must generate identical output
    /// for identical inputs (deterministic incremental output), so they are iterated through this array.
    /// </summary>
    private static readonly string[] JoinOperatorOrder =
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

    private static readonly DiagnosticDescriptor AliasExtensionCollision = new(
        DiagnosticIdPrefix + "007",
        "Join alias extension signature collision",
        "Two distinct join-alias chains emit extension signature '{0}' with different bodies",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A generated extension signature must identify exactly one body; a same-signature/different-body pair would mask a chain-specific difference (issue #206 class) and must fail loudly instead of being silently merged.");

    private static readonly DiagnosticDescriptor WithAliasNotRoot = new(
        DiagnosticIdPrefix + "008",
        "WithAlias applied to a non-root source",
        "WithAlias names the root source (slot 1); the alias '{0}' is applied after a join or a previous root alias",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "WithAlias is root-only: it must be the first alias step on a plain root source (From<T>, From(string), FromSql, From(Cte<T>), temp table, table function, From(QueryCommand<T>) or a builder), never after a join or a second WithAlias.");

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
    /// Cheap syntactic predicate. A candidate is one of the seven join operators that either (a) ends
    /// in an <c>Alias</c>-rooted argument (an alias step) or (b) is itself invoked on another join call
    /// (a positional step that may extend an alias chain). A plain positional join over a source or a
    /// variable is not a candidate: it cannot introduce an alias.
    /// </summary>
    private static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name is not SimpleNameSyntax name) return false;

        var arguments = invocation.ArgumentList.Arguments;

        if (string.Equals(name.Identifier.ValueText, "WithAlias", StringComparison.Ordinal))
            return arguments.Count == 1 && IsAliasRootedMemberAccess(arguments[0].Expression);

        if (!JoinOperators.Contains(name.Identifier.ValueText)) return false;
        if (name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count != 1) return false;

        if (arguments.Count is < 1 or > 4) return false;

        if (IsAliasRootedMemberAccess(arguments[arguments.Count - 1].Expression)) return true;

        // A positional step can extend an alias chain held in a variable (for example
        // `var a = root.Join<X>(..., Alias.X); a.Join(y, ...)`). The receiver is then an identifier
        // rather than a syntactic invocation, so the call site cannot be recognised syntactically; the
        // semantic transform resolves the identifier's type/initializer and only emits a transition when
        // the receiver chain actually carries an alias (a pure positional chain stays untouched).
        return memberAccess.Expression is InvocationExpressionSyntax or ParenthesizedExpressionSyntax or IdentifierNameSyntax;
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
        var lastExpression = arguments[arguments.Count - 1].Expression;
        var location = ToAliasLocation(lastExpression.GetLocation());

        if (string.Equals(operation, "WithAlias", StringComparison.Ordinal))
        {
            // Root alias (issue #160, Phase 2): '.WithAlias(Alias.X)' names slot 1 on a plain root
            // source. The receiver must be a bare root builder; any join or previous root alias is a
            // misuse reported as NORMGEN008 (the runtime seam keeps the same guard as a failsafe).
            if (!TryReadAliasArgument(lastExpression, out var rootAlias))
                return new Candidate(false, string.Empty, null, location);

            var rootReceiver = ResolveChain(memberAccess.Expression, semanticModel, ct);
            if (rootReceiver is null) return null;

            if (rootReceiver.RootAlias is not null || rootReceiver.Steps.Count > 0)
                return new Candidate(true, rootAlias, null, location, RootMisuse: true);

            return new Candidate(true, rootAlias, rootReceiver with { RootAlias = rootAlias }, location);
        }

        if (IsAliasRootedMemberAccess(lastExpression))
        {
            if (!TryReadAliasArgument(lastExpression, out var alias))
            {
                // 'Alias.<identifier>' is the approved form; anything else rooted at Alias (for example
                // 'Alias.Buyer<int>' or 'Alias.Buyer.Approver') is a hard error.
                return new Candidate(false, string.Empty, null, location);
            }

            if (!JoinOperators.Contains(operation)) return null;

            var conditionless = ConditionlessOperators.Contains(operation);
            if (conditionless ? arguments.Count != 2 : arguments.Count != 3) return null;

            if (!TryGetJoinedType(invocation, name, semanticModel, ct, out var joined, out var isCte)) return null;

            var receiver = ResolveChain(memberAccess.Expression, semanticModel, ct);
            if (receiver is null) return null;

            // Slot is assigned by Append (root is slot 1, this step is the next one).
            var step = new ChainStep(StepKind.Alias, alias, Display(joined), operation, conditionless, isCte, 0, location);
            return new Candidate(true, alias, Append(receiver, step), location);
        }

        // A positional step. It only matters when it extends a chain that already carries an alias; a
        // pure positional chain is handled by the core engine and is left untouched.
        if (!JoinOperators.Contains(operation)) return null;

        var positionalConditionless = ConditionlessOperators.Contains(operation);
        if (positionalConditionless ? arguments.Count is < 1 or > 2 : arguments.Count is < 2 or > 3) return null;

        var receiverChain = ResolveChain(memberAccess.Expression, semanticModel, ct);
        if (receiverChain is null || !receiverChain.HasAlias) return null;

        if (!TryGetJoinedType(invocation, name, semanticModel, ct, out var positionalJoined, out var positionalIsCte)) return null;

        // Slot is assigned by Append (root is slot 1, this step is the next one).
        var positionalStep = new ChainStep(StepKind.Positional, string.Empty, Display(positionalJoined), operation, positionalConditionless, positionalIsCte, 0, location);
        return new Candidate(true, string.Empty, Append(receiverChain, positionalStep), location);
    }

    private static ChainModel Append(ChainModel chain, ChainStep step)
    {
        var steps = new ChainStep[chain.Steps.Count + 1];
        for (var i = 0; i < chain.Steps.Count; i++) steps[i] = chain.Steps[i];

        // Defect A (slot model): root is slot 1 and every actual joined source consumes exactly the
        // next absolute slot, whether or not it is named; naming the root never consumes a slot, so
        // the first joined source is always slot 2. Append derives the slot from the receiver chain
        // instead of trusting the call site, so generator (here) and runtime (JoinSlot attribute)
        // use one explicit, contiguous model.
        steps[chain.Steps.Count] = step with { Slot = chain.Steps.Count + 2 };
        return chain with { Steps = new EquatableArray<ChainStep>(steps) };
    }

    /// <summary>Rebuilds the ordered slot chain ending at the receiver expression.</summary>
    private static ChainModel? ResolveChain(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken ct)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return ResolveChain(parenthesized.Expression, semanticModel, ct);

            case InvocationExpressionSyntax invocation:
                if (TryReadRootAlias(invocation, out var rootReceiver, out var rootAlias))
                {
                    var rooted = ResolveChain(rootReceiver, semanticModel, ct);
                    if (rooted is null || rooted.RootAlias is not null || rooted.Steps.Count > 0) return null;
                    return rooted with { RootAlias = rootAlias };
                }

                if (TryReadStep(invocation, semanticModel, ct, out var receiver, out var step))
                {
                    var previous = ResolveChain(receiver, semanticModel, ct);
                    if (previous is null) return null;

                    return Append(previous, step);
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
    /// <c>EntityBuilder&lt;T&gt;</c> (slot 1), or a positional prefix accumulated in the core
    /// <c>JoinedEntityBuilder&lt;T1..Tn&gt;</c> (slot 1 plus n-1 positional slots). Generated
    /// <c>AliasJoin_*</c> types are recovered by <see cref="ResolveFromTypeSyntax"/>.
    /// </summary>
    private static ChainModel? ResolveBuilderSymbol(INamedTypeSymbol builder)
    {
        if (builder.Name == "EntityBuilder"
            && builder.TypeArguments.Length == 1
            && builder.TypeArguments[0] is { } entity)
        {
            return new ChainModel(Display(entity), null, EquatableArray<ChainStep>.Empty);
        }

        if (builder.Name == "JoinedEntityBuilder" && builder.TypeArguments.Length is >= 2 and <= MaxSlots)
        {
            var steps = new ChainStep[builder.TypeArguments.Length - 1];
            for (var i = 1; i < builder.TypeArguments.Length; i++)
            {
                // A positional prefix already wrote its slots into the builder type: the root argument
                // is slot 1 and each later type argument consumes the next absolute slot.
                steps[i - 1] = new ChainStep(StepKind.Positional, string.Empty, Display(builder.TypeArguments[i]), "Join", false, false, i + 1, default);
            }

            return new ChainModel(Display(builder.TypeArguments[0]), null, new EquatableArray<ChainStep>(steps));
        }

        return null;
    }

    /// <summary>
    /// Reads a root-alias step (<c>.WithAlias(Alias.X)</c>) from an invocation, returning the receiver
    /// expression and the alias name. Unlike a join step it carries no operator or joined type: it names
    /// the existing slot 1, so the chain model stores it as <see cref="ChainModel.RootAlias"/>.
    /// </summary>
    private static bool TryReadRootAlias(InvocationExpressionSyntax invocation, out ExpressionSyntax receiver, out string alias)
    {
        receiver = null!;
        alias = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name is not SimpleNameSyntax name
            || !string.Equals(name.Identifier.ValueText, "WithAlias", StringComparison.Ordinal)) return false;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != 1) return false;
        if (!TryReadAliasArgument(arguments[0].Expression, out alias)) return false;

        receiver = memberAccess.Expression;
        return true;
    }

    /// <summary>Reads one slot step (alias or positional) from an invocation, returning its receiver.</summary>
    private static bool TryReadStep(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken ct,
        out ExpressionSyntax receiver,
        out ChainStep step)
    {
        receiver = null!;
        step = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name is not SimpleNameSyntax name || !JoinOperators.Contains(name.Identifier.ValueText)) return false;

        var operation = name.Identifier.ValueText;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0) return false;

        var conditionless = ConditionlessOperators.Contains(operation);
        var lastExpression = arguments[arguments.Count - 1].Expression;
        var lastIsAlias = IsAliasRootedMemberAccess(lastExpression);

        if (lastIsAlias)
        {
            if (!TryReadAliasArgument(lastExpression, out var alias)) return false;
            if (conditionless ? arguments.Count != 2 : arguments.Count != 3) return false;
            if (!TryGetJoinedType(invocation, name, semanticModel, ct, out var joined, out var isCte)) return false;

            receiver = memberAccess.Expression;
            // Slot is assigned by Append (root is slot 1, this step is the next one).
            step = new ChainStep(StepKind.Alias, alias, Display(joined), operation, conditionless, isCte, 0, ToAliasLocation(lastExpression.GetLocation()));
            return true;
        }

        if (conditionless ? arguments.Count is < 1 or > 2 : arguments.Count is < 2 or > 3) return false;
        if (!TryGetJoinedType(invocation, name, semanticModel, ct, out var positionalJoined, out var positionalIsCte)) return false;

        receiver = memberAccess.Expression;
        // Slot is assigned by Append (root is slot 1, this step is the next one).
        step = new ChainStep(StepKind.Positional, string.Empty, Display(positionalJoined), operation, conditionless, positionalIsCte, 0, ToAliasLocation(lastExpression.GetLocation()));
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
    /// The suffix encodes every slot as <c>P{slot}</c> or <c>A{slot}_{name}</c>; the leading kind letter
    /// makes the parse unambiguous (an alias name may not contain '_').
    /// </summary>
    private static ChainModel? ResolveFromTypeSyntax(TypeSyntax type, SemanticModel semanticModel, CancellationToken ct)
    {
        var generic = GetRightmostGenericName(type);
        if (generic is not null && generic.Identifier.ValueText.StartsWith("AliasJoin_", StringComparison.Ordinal))
        {
            var suffix = generic.Identifier.ValueText.Substring("AliasJoin_".Length);
            if (suffix.Length == 0) return null;
            if (!TryParseSuffix(suffix, out var tokens)) return null;

            var typeArguments = generic.TypeArgumentList.Arguments;
            if (typeArguments.Count != tokens.Count) return null;
            if (semanticModel.GetTypeInfo(typeArguments[0], ct).Type is not INamedTypeSymbol baseType) return null;

            var rootAlias = tokens[0].Kind == StepKind.Alias ? tokens[0].Name : null;
            var steps = new ChainStep[tokens.Count - 1];
            for (var i = 1; i < tokens.Count; i++)
            {
                if (semanticModel.GetTypeInfo(typeArguments[i], ct).Type is not INamedTypeSymbol joined) return null;
                // Defect A: carry the absolute slot parsed from the written type, so a chain recovered
                // from a builder type is identical to one recovered from the fluent expression.
                steps[i - 1] = new ChainStep(tokens[i].Kind, tokens[i].Name, Display(joined), "Join", false, false, tokens[i].Slot, default);
            }

            return new ChainModel(Display(baseType), rootAlias, new EquatableArray<ChainStep>(steps));
        }

        if (semanticModel.GetTypeInfo(type, ct).Type is INamedTypeSymbol builder)
            return ResolveBuilderSymbol(builder);

        return null;
    }

    /// <summary>Parses a slot-encoded suffix into (kind, name, slot) tokens; slot must equal 1-based index.</summary>
    private static bool TryParseSuffix(string suffix, out List<(StepKind Kind, string Name, int Slot)> tokens)
    {
        tokens = new List<(StepKind, string, int)>();
        var parts = suffix.Split('_');
        var index = 0;
        while (index < parts.Length)
        {
            var part = parts[index];
            if (part.Length < 2) return false;

            var kind = part[0];
            if (!int.TryParse(part.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot < 1)
                return false;

            if (kind == 'P')
            {
                tokens.Add((StepKind.Positional, string.Empty, slot));
                index += 1;
            }
            else if (kind == 'A')
            {
                if (index + 1 >= parts.Length) return false;
                var alias = parts[index + 1];
                if (alias.Length == 0) return false;
                tokens.Add((StepKind.Alias, alias, slot));
                index += 2;
            }
            else
            {
                return false;
            }

            if (tokens[tokens.Count - 1].Slot != tokens.Count) return false;
        }

        return tokens.Count > 0;
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
    /// from an <c>EntityBuilder&lt;T&gt;</c> or typed CTE <c>Cte&lt;T&gt;</c> source argument (type-argument
    /// inference at the call site). <paramref name="isCte"/> reports whether the joined source is a typed
    /// CTE descriptor.
    /// </summary>
    private static bool TryGetJoinedType(
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax name,
        SemanticModel semanticModel,
        CancellationToken ct,
        out ITypeSymbol joined,
        out bool isCte)
    {
        joined = null!;
        isCte = false;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0) return false;

        if (name is GenericNameSyntax generic)
        {
            if (generic.TypeArgumentList.Arguments.Count != 1) return false;
            var explicitType = semanticModel.GetTypeInfo(generic.TypeArgumentList.Arguments[0], ct).Type;
            if (explicitType is null) return false;
            joined = explicitType;
            isCte = IsCteSource(arguments[0].Expression, semanticModel, ct);
            return true;
        }

        if (semanticModel.GetTypeInfo(arguments[0].Expression, ct).Type is INamedTypeSymbol source
            && source.TypeArguments.Length == 1
            && (IsCteSource(source, semanticModel) || source.Name == "EntityBuilder"))
        {
            joined = source.TypeArguments[0];
            isCte = IsCteSource(source, semanticModel);
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when the joined source argument is a typed CTE descriptor <c>Cte&lt;T&gt;</c> by symbol identity.
    /// A simple name match would misrecognize an unrelated user type named <c>Cte</c> (R159-10 negative).
    /// </summary>
    private static bool IsCteSource(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken ct)
        => semanticModel.GetTypeInfo(expression, ct).Type is INamedTypeSymbol named
            && IsCteSource(named, semanticModel);

    /// <summary>
    /// True when <paramref name="type"/> is the <c>NextORM.Core.Cte&lt;T&gt;</c> descriptor, compared by
    /// symbol identity rather than its simple name.
    /// </summary>
    private static bool IsCteSource(INamedTypeSymbol type, SemanticModel semanticModel)
        => type.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(
                type.ConstructedFrom,
                semanticModel.Compilation.GetTypeByMetadataName("NextORM.Core.Cte`1"));

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
            if (candidate.RootMisuse)
            {
                diagnostics.Add(Diagnostic.Create(WithAliasNotRoot, ToLocation(candidate.Location), candidate.Alias));
                continue;
            }

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
            if (candidate.Alias.Length == 0) continue;
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
            var usesSuppressedAlias = chain.RootAlias is not null && suppressedAliases.Contains(chain.RootAlias);
            if (!usesSuppressedAlias)
            {
                for (var i = 0; i < chain.Steps.Count; i++)
                {
                    if (chain.Steps[i].Kind == StepKind.Alias && suppressedAliases.Contains(chain.Steps[i].Alias))
                    {
                        usesSuppressedAlias = true;
                        break;
                    }
                }
            }

            if (usesSuppressedAlias) continue;

            // A root alias colliding with a retained ItemN member (for example Alias.Item1) is the same
            // class of collision as for a join alias and is rejected with NORMGEN002.
            if (chain.RootAlias is not null && IsItemAlias(chain.RootAlias, chain.Steps.Count + 1))
            {
                diagnostics.Add(Diagnostic.Create(AliasCollision, ToLocation(aliasLocations[chain.RootAlias]), chain.RootAlias, chain.RootAlias));
                continue;
            }

            if (ValidateChain(chain, diagnostics)) validChains.Add(chain);
        }

        var rootAliases = validChains
            .Where(static c => c.RootAlias is not null)
            .Select(static c => c.RootAlias!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static a => a, StringComparer.Ordinal)
            .ToList();

        // Collect every used schema (root + prefixes up to the full chain) and every observed transition.
        var schemas = new Dictionary<string, SchemaInfo>(StringComparer.Ordinal);
        var aliasTransitions = new Dictionary<string, AliasTransition>(StringComparer.Ordinal);
        var positionalTransitions = new Dictionary<string, PositionalTransition>(StringComparer.Ordinal);

        foreach (var chain in validChains)
        {
            for (var count = 0; count <= chain.Steps.Count; count++)
            {
                var schema = BuildSchema(chain, count);
                schemas[schema.Suffix] = schema;

                if (count >= chain.Steps.Count) continue;

                var step = chain.Steps[count];
                if (step.Kind == StepKind.Alias)
                {
                    // Alias extensions carry the concrete entity types in their receiver/return signature
                    // (only TJoin is a method type parameter), so distinct base-type instantiations of the
                    // same base-agnostic schema are distinct methods.
                    var key = ChainKey(chain) + ">>" + count;
                    aliasTransitions[key] = new AliasTransition(schema, step, chain, count);
                }
                else if (schema.HasAlias)
                {
                    // Generated positional overloads live on the base-agnostic generic builder and use its
                    // own type parameters, so one method per (schema, operator, conditionlessness) suffices.
                    var key = schema.Suffix + ">>P:" + step.Operator + ":" + (step.Conditionless ? "1" : "0");
                    positionalTransitions[key] = new PositionalTransition(schema, step);
                }
            }
        }

        // Root alias (Phase 2): '.WithAlias(Alias.X)' names slot 1 on a plain root. A subsequent
        // positional join carries no marker to select a generated extension, and the inherited
        // EntityBuilder<TEntity>.Join/Apply instance method is applicable, so it must be hidden by a
        // generated 'new' instance method on the root receiver. Unlike a join-anchored positional step,
        // the call site cannot be observed when the receiver is stored in a variable, so the full
        // operator set is emitted for every root alias. This keeps one positional-after-alias mechanism
        // (RenderPositionalMethods) for both an alias step and a root alias.
        foreach (var rootAlias in rootAliases)
        {
            var rootChain = new ChainModel(string.Empty, rootAlias, EquatableArray<ChainStep>.Empty);
            var rootSchema = BuildSchema(rootChain, 0);
            schemas[rootSchema.Suffix] = rootSchema;

            foreach (var operation in JoinOperatorOrder)
            {
                var conditionless = ConditionlessOperators.Contains(operation);
                var step = new ChainStep(StepKind.Positional, string.Empty, string.Empty, operation, conditionless, false, 2, default);
                var nextSchema = BuildSchema(Append(rootChain, step), 1);
                schemas[nextSchema.Suffix] = nextSchema;

                var key = rootSchema.Suffix + ">>P:" + operation + ":" + (conditionless ? "1" : "0");
                if (!positionalTransitions.ContainsKey(key))
                    positionalTransitions[key] = new PositionalTransition(rootSchema, step);
            }
        }

        var emitter = new StringBuilder();
        emitter.AppendLine("// <auto-generated />");
        emitter.AppendLine("#nullable disable");
        emitter.AppendLine("#pragma warning disable CS1591");
        emitter.AppendLine("namespace " + generatedNamespace);
        emitter.AppendLine("{");

        AppendAliasMarker(emitter, generatedNamespace, aliasNames.Where(alias => !suppressedAliases.Contains(alias)).ToList());
        AppendTypes(emitter, generatedNamespace, schemas, positionalTransitions);
        AppendExtensions(emitter, generatedNamespace, aliasTransitions, rootAliases, diagnostics);

        emitter.AppendLine("}");
        context.AddSource("JoinAlias.g.cs", SourceText.From(emitter.ToString(), Encoding.UTF8));
        Report(context, diagnostics);
    }

    /// <summary>Builds the schema (suffix, arity and alias members) for the root plus the first <paramref name="count"/> steps.</summary>
    private static SchemaInfo BuildSchema(ChainModel chain, int count)
    {
        var tokens = new List<string>(count + 1);
        tokens.Add(chain.RootAlias is null ? "P1" : "A1_" + chain.RootAlias);

        var members = ImmutableArray.CreateBuilder<AliasMember>();
        if (chain.RootAlias is not null) members.Add(new AliasMember(chain.RootAlias, 1));

        for (var i = 0; i < count; i++)
        {
            var step = chain.Steps[i];

            // Defect A: the suffix and member slot come from the step's absolute slot (root is slot 1,
            // first joined source is slot 2), never from the loop index, so the emitted surface and the
            // runtime JoinSlot attribute resolve the same ordinal.
            var slot = step.Slot;
            if (step.Kind == StepKind.Alias)
            {
                tokens.Add("A" + slot + "_" + step.Alias);
                members.Add(new AliasMember(step.Alias, slot));
            }
            else
            {
                tokens.Add("P" + slot);
            }
        }

        var suffix = string.Join("_", tokens);
        return new SchemaInfo(suffix, count + 1, chain.RootAlias is not null || members.Count > 0, members.ToImmutable());
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
            if (steps[i].Kind != StepKind.Alias) continue;
            var alias = steps[i].Alias;

            if (IsItemAlias(alias, arity))
            {
                diagnostics.Add(Diagnostic.Create(AliasCollision, ToLocation(steps[i].Location), alias, alias));
                valid = false;
            }

            if (chain.RootAlias is not null && string.Equals(chain.RootAlias, alias, StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic.Create(DuplicateAlias, ToLocation(steps[i].Location), alias));
                valid = false;
            }

            for (var j = 0; j < i; j++)
            {
                if (steps[j].Kind != StepKind.Alias) continue;
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

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var position)
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

    private static void AppendTypes(
        StringBuilder emitter,
        string generatedNamespace,
        Dictionary<string, SchemaInfo> schemas,
        Dictionary<string, PositionalTransition> positionalTransitions)
    {
        var byReceiver = positionalTransitions.Values
            .GroupBy(static transition => transition.Receiver.Suffix, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.OrderBy(static t => t.Step.Operator, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

        foreach (var schema in schemas.Values.Where(static s => s.HasAlias).OrderBy(static s => s.Suffix, StringComparer.Ordinal))
        {
            var typeParameters = TypeParameters(schema.Arity);
            var projection = generatedNamespace + ".AliasProjection_" + schema.Suffix + "<" + typeParameters + ">";
            var builder = generatedNamespace + ".AliasJoin_" + schema.Suffix + "<" + typeParameters + ">";

            emitter.AppendLine("    public class AliasProjection_" + schema.Suffix + "<" + typeParameters + "> : global::NextORM.Core.Projection<" + typeParameters + ">");
            emitter.AppendLine("    {");
            foreach (var member in schema.AliasMembers)
            {
                emitter.AppendLine("        [global::NextORM.Core.JoinSlot(" + member.Slot + ")]");
                emitter.AppendLine("        public T" + member.Slot + " " + Escape(member.Name) + " => throw new global::System.NotSupportedException();");
            }

            emitter.AppendLine("    }");
            emitter.AppendLine();

            emitter.AppendLine("    public class AliasJoin_" + schema.Suffix + "<" + typeParameters + "> : global::NextORM.Core.EntityBuilder<" + projection + ">");
            emitter.AppendLine("    {");
            emitter.AppendLine("        public AliasJoin_" + schema.Suffix + "(global::NextORM.Core.IDataContext dataProvider) : base(dataProvider) { }");

            if (byReceiver.TryGetValue(schema.Suffix, out var transitions))
            {
                foreach (var transition in transitions)
                {
                    RenderPositionalMethods(emitter, generatedNamespace, transition);
                }
            }

            emitter.AppendLine("    }");
            emitter.AppendLine();
        }
    }

    /// <summary>
    /// Renders the generated <c>new</c> instance overloads that extend an alias receiver with one
    /// positional slot. Both the <c>EntityBuilder&lt;TJoin&gt;</c> and the <c>Cte&lt;TJoin&gt;</c> source
    /// shapes are emitted so the generated overloads hide the inherited positional overloads of the
    /// same signature and every mixed chain routes through the <c>JoinAlias</c> seam.
    /// </summary>
    private static void RenderPositionalMethods(StringBuilder emitter, string generatedNamespace, PositionalTransition transition)
    {
        var schema = transition.Receiver;
        var step = transition.Step;
        var receiverTypeParameters = TypeParameters(schema.Arity);
        var receiverProjection = generatedNamespace + ".AliasProjection_" + schema.Suffix + "<" + receiverTypeParameters + ">";

        var nextSuffix = schema.Suffix + "_P" + (schema.Arity + 1);
        var nextTypeParameters = receiverTypeParameters + ", TJoin";
        var returnType = generatedNamespace + ".AliasJoin_" + nextSuffix + "<" + nextTypeParameters + ">";
        var projectionType = generatedNamespace + ".AliasProjection_" + nextSuffix + "<" + nextTypeParameters + ">";
        var conditionType = "global::System.Linq.Expressions.Expression<global::System.Func<" + receiverProjection + ", TJoin, bool>>";
        var joinType = "global::NextORM.Core.JoinType." + JoinTypeName(step.Operator);
        var conditionless = step.Conditionless;

        // EntityBuilder<TJoin> source (with the optional-options overload for the conditionless operators).
        emitter.AppendLine("        public new " + returnType + " " + step.Operator + "<TJoin>(");
        emitter.AppendLine("            global::NextORM.Core.EntityBuilder<TJoin> _,");
        if (!conditionless) emitter.AppendLine("            " + conditionType + " condition,");
        emitter.AppendLine("            global::System.Action<global::NextORM.Core.JoinOptions> options = null)");
        emitter.AppendLine("            => JoinAlias<" + returnType + ", " + projectionType + ", TJoin>(");
        emitter.AppendLine("                static dc => new " + returnType + "(dc),");
        emitter.AppendLine("                _,");
        if (!conditionless) emitter.AppendLine("                condition,");
        emitter.AppendLine("                " + joinType + ", options);");

        // Cte<TJoin> source. The parameterless-options form is emitted separately so it also hides the
        // inherited one-argument CTE overload.
        if (conditionless)
        {
            emitter.AppendLine("        public new " + returnType + " " + step.Operator + "<TJoin>(");
            emitter.AppendLine("            global::NextORM.Core.Cte<TJoin> cte)");
            emitter.AppendLine("            => JoinAlias<" + returnType + ", " + projectionType + ", TJoin>(");
            emitter.AppendLine("                static dc => new " + returnType + "(dc),");
            emitter.AppendLine("                cte,");
            emitter.AppendLine("                " + joinType + ", null);");
        }

        emitter.AppendLine("        public new " + returnType + " " + step.Operator + "<TJoin>(");
        emitter.AppendLine("            global::NextORM.Core.Cte<TJoin> cte,");
        if (!conditionless) emitter.AppendLine("            " + conditionType + " condition,");
        emitter.AppendLine("            global::System.Action<global::NextORM.Core.JoinOptions> options = null)");
        emitter.AppendLine("            => JoinAlias<" + returnType + ", " + projectionType + ", TJoin>(");
        emitter.AppendLine("                static dc => new " + returnType + "(dc),");
        emitter.AppendLine("                cte,");
        if (!conditionless) emitter.AppendLine("                condition,");
        emitter.AppendLine("                " + joinType + ", options);");
    }

    private static void AppendExtensions(
        StringBuilder emitter,
        string generatedNamespace,
        Dictionary<string, AliasTransition> aliasTransitions,
        List<string> rootAliases,
        List<Diagnostic> diagnostics)
    {
        // #159 C1: two distinct chains can legitimately emit the same extension method when they differ
        // only in an intermediate step's source kind. Render every method in full, collapse the
        // byte-identical duplicates to one, and reject a same-signature/different-body pair instead of
        // silently masking a chain-specific difference (the issue #206 class of divergence).
        var seenSignatures = new Dictionary<string, string>(StringComparer.Ordinal);
        var methods = new List<string>();

        foreach (var transition in aliasTransitions.Values.OrderBy(static t => t.Receiver.Suffix, StringComparer.Ordinal).ThenBy(static t => t.Step.Alias, StringComparer.Ordinal))
        {
            var rendered = new StringBuilder();
            var signature = RenderAliasExtension(rendered, generatedNamespace, transition);
            var method = rendered.ToString();

            if (seenSignatures.TryGetValue(signature, out var existing))
            {
                if (!string.Equals(existing, method, StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic.Create(AliasExtensionCollision, Location.None, signature));
                }

                continue;
            }

            seenSignatures[signature] = method;
            methods.Add(method);
        }

        // Root alias: one generic extension per root name. It is generic over the entity type, so a
        // single method covers every root source (mapped entity, raw table, FromSql, CTE, temp table,
        // table function, QueryCommand, builder) uniformly; the runtime AliasRoot seam preserves the
        // source state. Emitted after the join overloads; signatures are unique per root name.
        foreach (var rootAlias in rootAliases)
        {
            var rendered = new StringBuilder();
            var signature = RenderWithAliasExtension(rendered, generatedNamespace, rootAlias);
            if (seenSignatures.ContainsKey(signature)) continue;
            var method = rendered.ToString();
            seenSignatures[signature] = method;
            methods.Add(method);
        }

        emitter.AppendLine("    public static class JoinAliasExtensions");
        emitter.AppendLine("    {");

        foreach (var method in methods) emitter.Append(method);

        emitter.AppendLine("    }");
    }

    /// <summary>
    /// Renders the generic root-alias extension <c>WithAlias&lt;T&gt;(this EntityBuilder&lt;T&gt;, Alias.XMarker)</c>
    /// and returns its signature. The generated <c>WithAlias</c> is generic over the receiver's entity type, so
    /// one method covers every root source; the core <c>AliasRoot</c> seam does the state-preserving re-root and
    /// keeps the in-memory/root-only guards. A <c>null</c>/<c>default</c> marker is rejected immediately.
    /// </summary>
    private static string RenderWithAliasExtension(StringBuilder emitter, string generatedNamespace, string alias)
    {
        var builderType = generatedNamespace + ".AliasJoin_A1_" + alias + "<T>";
        var projectionType = generatedNamespace + ".AliasProjection_A1_" + alias + "<T>";
        var markerType = generatedNamespace + ".Alias." + alias + "Marker";

        emitter.AppendLine("        public static " + builderType + " WithAlias<T>(");
        emitter.AppendLine("            this global::NextORM.Core.EntityBuilder<T> self,");
        emitter.AppendLine("            " + markerType + " marker)");
        emitter.AppendLine("        {");
        emitter.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(self);");
        emitter.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(marker);");
        emitter.AppendLine("            return self.AliasRoot<" + builderType + ", " + projectionType + ">(");
        emitter.AppendLine("                static dc => new " + builderType + "(dc));");
        emitter.AppendLine("        }");

        return "WithAlias<T>(" + markerType + ") -> " + builderType;
    }

    /// <summary>
    /// Renders one alias extension method into <paramref name="emitter"/> and returns its C# signature.
    /// The receiver is the accumulated schema: it is a core <c>EntityBuilder&lt;T&gt;</c> (no positional
    /// prefix), a core <c>JoinedEntityBuilder&lt;T1..Tn&gt;</c> (an unaliased positional prefix), or the
    /// generated <c>AliasJoin_*</c> builder for a prefix that already carries an alias.
    /// </summary>
    private static string RenderAliasExtension(StringBuilder emitter, string generatedNamespace, AliasTransition transition)
    {
        var schema = transition.Receiver;
        var chain = transition.Chain;
        var count = transition.Count;
        var step = transition.Step;

        string receiverType;
        string conditionArgumentType;
        var concreteArguments = new List<string> { chain.BaseType };
        for (var i = 0; i < count; i++) concreteArguments.Add(chain.Steps[i].JoinedType);
        var concreteArgumentsText = string.Join(", ", concreteArguments);

        if (schema.HasAlias)
        {
            receiverType = generatedNamespace + ".AliasJoin_" + schema.Suffix + "<" + concreteArgumentsText + ">";
            conditionArgumentType = generatedNamespace + ".AliasProjection_" + schema.Suffix + "<" + concreteArgumentsText + ">";
        }
        else if (count == 0)
        {
            receiverType = "global::NextORM.Core.EntityBuilder<" + chain.BaseType + ">";
            conditionArgumentType = chain.BaseType;
        }
        else
        {
            receiverType = "global::NextORM.Core.JoinedEntityBuilder<" + concreteArgumentsText + ">";
            conditionArgumentType = "global::NextORM.Core.Projection<" + concreteArgumentsText + ">";
        }

        var nextSchema = BuildSchema(chain, count + 1);
        var returnArguments = new List<string>(concreteArguments) { "TJoin" };
        var returnArgumentsText = string.Join(", ", returnArguments);
        var returnType = generatedNamespace + ".AliasJoin_" + nextSchema.Suffix + "<" + returnArgumentsText + ">";
        var projectionType = generatedNamespace + ".AliasProjection_" + nextSchema.Suffix + "<" + returnArgumentsText + ">";
        var markerType = generatedNamespace + ".Alias." + step.Alias + "Marker";
        var joinType = "global::NextORM.Core.JoinType." + JoinTypeName(step.Operator);
        var sourceIsCte = step.IsCte;
        var sourceParameterType = sourceIsCte
            ? "global::NextORM.Core.Cte<TJoin>"
            : "global::NextORM.Core.EntityBuilder<TJoin>";
        var conditionParameterType = "global::System.Linq.Expressions.Expression<global::System.Func<"
            + conditionArgumentType + ", TJoin, bool>>";

        emitter.AppendLine("        public static " + returnType + " " + step.Operator + "<TJoin>(");
        emitter.AppendLine("            this " + receiverType + " self,");
        emitter.AppendLine("            " + sourceParameterType + (sourceIsCte ? " cte," : " _,"));

        if (!step.Conditionless)
        {
            emitter.AppendLine("            " + conditionParameterType + " condition,");
        }

        emitter.AppendLine("            " + markerType + " marker)");
        emitter.AppendLine("            => self.JoinAlias<" + returnType + ", " + projectionType + ", TJoin>(");
        emitter.AppendLine("                static dc => new " + returnType + "(dc),");
        emitter.AppendLine(sourceIsCte ? "                cte," : "                _,");
        if (!step.Conditionless)
        {
            emitter.AppendLine("                condition,");
        }

        emitter.AppendLine("                " + joinType + ");");

        return step.Operator + "<TJoin>(" + receiverType + ", " + sourceParameterType + ", "
            + (step.Conditionless ? string.Empty : conditionParameterType + ", ")
            + markerType + ") -> " + returnType;
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

    private static string TypeParameters(int arity) => string.Join(", ", Enumerable.Range(1, arity).Select(static i => "T" + i));

    private static string Escape(string alias)
        => SyntaxFacts.GetKeywordKind(alias) != SyntaxKind.None ? "@" + alias : alias;

    private static string ChainKey(ChainModel chain)
    {
        // Defect A identity: the ordered schema is keyed by absolute slot, kind, operator, name,
        // joined type and CTE distinction. Two chains that agree on every step but assigned it a
        // different absolute slot stay distinct, so ordinal resolution cannot silently collide.
        var builder = new StringBuilder(chain.BaseType);
        builder.Append("::").Append(chain.RootAlias ?? string.Empty);
        for (var i = 0; i < chain.Steps.Count; i++)
        {
            var step = chain.Steps[i];
            builder.Append('|').Append(step.Slot).Append(':').Append(step.Operator).Append(':').Append(step.Kind == StepKind.Alias ? "A" : "P").Append(':').Append(step.Alias).Append(':').Append(step.JoinedType).Append(':').Append(step.IsCte ? 'c' : 'e');
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

    private enum StepKind
    {
        Positional,
        Alias
    }

    private readonly record struct AliasLocation(string FilePath, TextSpan Span, LinePositionSpan LineSpan);

    /// <summary>
    /// One step of a slot chain. <see cref="Slot"/> is the absolute 1-based slot the step occupies
    /// (the root is slot 1, so a step recorded here is always slot 2 or higher); it is part of the
    /// chain identity so generator and runtime resolve ordinals against the same explicit model.
    /// </summary>
    private sealed record ChainStep(StepKind Kind, string Alias, string JoinedType, string Operator, bool Conditionless, bool IsCte, int Slot, AliasLocation Location);

    /// <summary>
    /// One ordered slot chain: slot 1 is the base entity (optionally named by <see cref="RootAlias"/>);
    /// each step is a positional or alias slot. The scheme is a single model for mixing both kinds.
    /// </summary>
    private sealed record ChainModel(string BaseType, string? RootAlias, EquatableArray<ChainStep> Steps)
    {
        public bool HasAlias
        {
            get
            {
                if (RootAlias is not null) return true;
                for (var i = 0; i < Steps.Count; i++)
                {
                    if (Steps[i].Kind == StepKind.Alias) return true;
                }

                return false;
            }
        }
    }

    /// <summary>An emitted generated type: its slot suffix, arity and alias members (name + 1-based slot).</summary>
    private sealed record SchemaInfo(string Suffix, int Arity, bool HasAlias, ImmutableArray<AliasMember> AliasMembers);

    private sealed record AliasMember(string Name, int Slot);

    private sealed record AliasTransition(SchemaInfo Receiver, ChainStep Step, ChainModel Chain, int Count);

    private sealed record PositionalTransition(SchemaInfo Receiver, ChainStep Step);

    private sealed record Candidate(bool Approved, string Alias, ChainModel? Chain, AliasLocation Location, bool RootMisuse = false);

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

using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// The collaborators a SQL build needs for one command, bundled so that statement assembly
/// (<c>SqlBuilder</c>) and source rendering (<see cref="SqlSourceRenderer"/>) share them
/// without either type owning the other's concerns. Immutable: a build only reads these and mutates
/// the referenced <see cref="Params"/> / <see cref="ColumnsProvider"/> state. Derive a variant with
/// <c>with</c> (for example a CTE rendered with a fresh columns provider).
/// </summary>
internal readonly record struct SqlBuildContext
{
    /// <summary>Creates a context from a visitor's construction-time options.</summary>
    internal SqlBuildContext(VisitorOptions options)
    {
        Dialect = options.Dialect;
        ParamMode = options.ParamMode;
        Params = options.Params;
        ColumnsProvider = options.ColumnsProvider;
        QueryProvider = options.QueryProvider;
        ParameterProvider = options.ParameterProvider;
        AliasProvider = options.AliasProvider;
        Logger = options.Logger;
        QuoteIdentifiers = options.QuoteIdentifiers;
        NamingConvention = options.NamingConvention;
        IncludeNestedSources = options.IncludeNestedSources;
        this.KeywordCase = options.KeywordCase;
        ParameterNamePrefix = options.ParameterNamePrefix;
    }

    internal ISqlDialect Dialect { get; init; }
    internal bool ParamMode { get; init; }
    internal bool SequentialAccess { get; init; }
    internal List<Parameter> Params { get; init; }
    internal IColumnsProvider ColumnsProvider { get; init; }
    internal IQueryRegistry QueryProvider { get; init; }
    internal IParameterProvider ParameterProvider { get; init; }
    internal IAliasProvider? AliasProvider { get; init; }
    internal ILogger? Logger { get; init; }
    internal bool QuoteIdentifiers { get; init; }
    internal INamingConvention? NamingConvention { get; init; }
    internal bool IncludeNestedSources { get; init; }
    /// <summary>
    /// When <see langword="true"/> the command being rendered must not emit its own <c>WITH</c>: its
    /// declarations have already been hoisted into the enclosing statement's top-level <c>WITH</c>
    /// (see <see cref="CteHoister"/>). Set only while rendering the body of a hoisted declaration.
    /// </summary>
    internal bool SuppressCtes { get; init; }

    /// <summary>
    /// When <see langword="true"/> the rendered select list is a typed CTE declaration body: every
    /// projected column must carry an explicit alias matching its projection property name, compared
    /// case-sensitively. A typed read resolves members to those property names, so a mapped column
    /// differing from the property name only by case (for example <c>id</c> vs <c>Id</c>) still needs
    /// the alias — PostgreSQL folded unquoted names are otherwise unreachable under the quoted name.
    /// </summary>
    internal bool ExactProjectionAliases { get; init; }

    /// <summary>
    /// Renders the body of a data-modifying CTE whose command is a single-table <c>UPDATE</c> or
    /// <c>DELETE</c> (an <c>INSERT</c> body is rendered directly by <see cref="SqlSourceRenderer"/>).
    /// Set on the context that renders a statement which may declare such a CTE, so the body is emitted
    /// with the enclosing statement's parameter provider and accumulator. When it is <see langword="null"/>
    /// the renderer rejects an UPDATE/DELETE body rather than emitting invalid SQL.
    /// </summary>
    internal Func<CteMutation, SqlBuildContext, string>? RenderMutationBody { get; init; }

    internal KeywordCase KeywordCase { get; init; }
    internal string ParameterNamePrefix { get; init; }

    internal WhereExpressionVisitor CreateWhereVisitor(Type entityType, int dim, bool dontNeedAlias = false)
        => new(new VisitorOptions(entityType, Dialect, ColumnsProvider, dim, AliasProvider, ParameterProvider, QueryProvider, dontNeedAlias, ParamMode, Params, Logger) { QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, IncludeNestedSources = IncludeNestedSources, KeywordCase = KeywordCase, ParameterNamePrefix = ParameterNamePrefix });

    internal BaseExpressionVisitor CreateColumnVisitor(Type entityType, int dim, bool dontNeedAlias)
        => new(new VisitorOptions(entityType, Dialect, ColumnsProvider, dim, AliasProvider, ParameterProvider, QueryProvider, dontNeedAlias, ParamMode, Params, Logger) { QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, IncludeNestedSources = IncludeNestedSources, KeywordCase = KeywordCase, ParameterNamePrefix = ParameterNamePrefix });
}

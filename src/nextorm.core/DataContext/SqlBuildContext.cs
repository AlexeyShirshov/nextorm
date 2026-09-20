using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// The collaborators a SQL build needs for one command, bundled so that statement assembly
/// (<see cref="SqlBuilder"/>) and source rendering (<see cref="SqlSourceRenderer"/>) share them
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
    }

    internal ISqlDialect Dialect { get; init; }
    internal bool ParamMode { get; init; }
    internal List<Parameter> Params { get; init; }
    internal IColumnsProvider ColumnsProvider { get; init; }
    internal IQueryRegistry QueryProvider { get; init; }
    internal IParameterProvider ParameterProvider { get; init; }
    internal IAliasProvider? AliasProvider { get; init; }
    internal ILogger? Logger { get; init; }

    internal WhereExpressionVisitor CreateWhereVisitor(Type entityType, int dim)
        => new(new VisitorOptions(entityType, Dialect, ColumnsProvider, dim, AliasProvider, ParameterProvider, QueryProvider, false, ParamMode, Params, Logger));

    internal BaseExpressionVisitor CreateColumnVisitor(Type entityType, int dim, bool dontNeedAlias)
        => new(new VisitorOptions(entityType, Dialect, ColumnsProvider, dim, AliasProvider, ParameterProvider, QueryProvider, dontNeedAlias, ParamMode, Params, Logger));
}

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace NextORM.Core;

/// <summary>
/// The construction-time collaborators of <see cref="BaseExpressionVisitor"/> (and of the visitors
/// derived from it) grouped into a single value, replacing the long constructor parameter list.
/// </summary>
/// <remarks>
/// Immutable after construction. The record shape allows derived visitors and internal translators to
/// create a variant with <c>with</c> (for example a child visitor with a different
/// <see cref="Dim"/>) without repeating every collaborator.
/// </remarks>
public sealed record VisitorOptions(
    Type EntityType,
    ISqlDialect Dialect,
    IColumnsProvider ColumnsProvider,
    int Dim,
    IAliasProvider? AliasProvider,
    IParameterProvider ParameterProvider,
    IQueryRegistry QueryProvider,
    bool DontNeedAlias,
    bool ParamMode,
    List<Parameter> Params,
    ILogger? Logger,
    ObjectPool<StringBuilder>? SbPool = null)
{
    /// <summary>
    /// Whether physical identifiers (table and column names) are quoted with the dialect's delimiter.
    /// Declared as an init-only property (rather than a positional parameter) so the record's primary
    /// constructor stays source- and binary-compatible for existing callers. Derived visitors carry it
    /// through <c>with</c>.
    /// </summary>
    public bool QuoteIdentifiers { get; init; }

    /// <summary>
    /// Convention applied to auto-derived table and column names, or <see langword="null"/> to emit
    /// them as-is. Declared as an init-only property so the record's primary constructor stays
    /// source- and binary-compatible for existing callers. Derived visitors carry it through
    /// <c>with</c>.
    /// </summary>
    public INamingConvention? NamingConvention { get; init; }

    /// <summary>
    /// When <see langword="true"/> the source lookups also consider the entries of nested commands
    /// that have already finished rendering. A derived query exposes its output columns by re-rendering
    /// the projection expression, which references the nested command's own sources; those entries are
    /// otherwise hidden from the enclosing command (see <c>IColumnsProvider.PopSourceScope</c>).
    /// </summary>
    public bool IncludeNestedSources { get; init; }

    /// <summary>
    /// The letter case in which the visitor emits SQL keywords. Declared as an init-only property so the
    /// record's primary constructor stays source- and binary-compatible; a build stamps it from the
    /// command's resolved <c>KeywordCase</c>.
    /// </summary>
    public KeywordCase KeywordCase { get; init; }

    /// <summary>
    /// A prefix prepended to the name of a parameter produced for a captured member (a closure
    /// variable/field), so a batch can render several statements that capture variables of the same
    /// name without their placeholders colliding. Empty for a standalone statement. Declared as an
    /// init-only property so the record's primary constructor stays source-compatible.
    /// </summary>
    public string ParameterNamePrefix { get; init; } = string.Empty;

    /// <summary>
    /// The render-local capture of planner-owned parameter occurrences, or <see langword="null"/> when
    /// this render is not producing a parameter template. Internal: it threads the engine's occurrence
    /// metadata through the visitors without widening the public surface. Declared as an init-only
    /// property for the same source-compatibility reason as the other derived members.
    /// </summary>
    internal SqlParameterTemplateCapture? Capture { get; init; }

    /// <summary>
    /// The lowering of the mutation statement this visitor is rendering a fragment of, or
    /// <see langword="null"/> for a visitor that does not take part in a mutation render. A visitor that
    /// carries one routes every placeholder of a planner-owned binding through it, so the lowered
    /// occurrences of the statement's <c>SET</c>/<c>WHERE</c> fragments accumulate on the same instance as
    /// the statements the mutation builder emits itself (D4g). Declared as an init-only property for the
    /// same source-compatibility reason as the other derived members.
    /// </summary>
    internal MutationParameterLowering? MutationLowering { get; init; }
}

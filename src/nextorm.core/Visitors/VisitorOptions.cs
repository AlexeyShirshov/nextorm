using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace nextorm.core;

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
    IParamProvider ParamProvider,
    IQueryProvider QueryProvider,
    bool DontNeedAlias,
    bool ParamMode,
    List<Param> Params,
    ILogger? Logger,
    ObjectPool<StringBuilder>? SbPool = null);

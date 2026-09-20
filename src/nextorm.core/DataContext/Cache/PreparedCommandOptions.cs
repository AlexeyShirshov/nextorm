using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// The non-generic setup of a <see cref="DbPreparedQueryCommand{TResult}"/>. The mapping delegate is
/// not part of this value because it is generic in the result type.
/// </summary>
/// <param name="SingleRow">Whether the command returns at most one row.</param>
/// <param name="Sql">The statement text when the command overrides the generated SQL, otherwise <c>null</c>.</param>
/// <param name="NoParams">Whether the command has no parameters at all.</param>
/// <param name="NeedsParamRefresh">Whether parameter values must be refreshed before each execution.</param>
public readonly record struct PreparedCommandOptions(
    bool SingleRow = false,
    string? Sql = null,
    bool NoParams = false,
    bool NeedsParamRefresh = false);

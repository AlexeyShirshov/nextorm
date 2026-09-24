using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// One <c>SET column = value</c> assignment of an <see cref="UpdateJoinCommand"/>. The left-hand side
/// is a member access on the joined projection targeting the first (target) table; the right-hand side
/// is a captured constant (bound as a parameter) or an arbitrary expression rendered by the shared
/// expression pipeline (so it may reference any joined table's columns, always qualified by alias).
/// </summary>
internal sealed class UpdateJoinAssignment
{
    private UpdateJoinAssignment(Expression target, UpdateValueKind kind, object? constant, Expression? value)
    {
        Target = target;
        Kind = kind;
        Constant = constant;
        Value = value;
    }

    /// <summary>Creates an assignment writing a captured constant value, bound as a parameter.</summary>
    /// <param name="target">The target-table member access written by this assignment.</param>
    /// <param name="value">The captured CLR value, or <see langword="null"/> for SQL <c>NULL</c>.</param>
    public static UpdateJoinAssignment FromConstant(Expression target, object? value) => new(target, UpdateValueKind.Constant, value, null);

    /// <summary>Creates an assignment writing an arbitrary value expression.</summary>
    /// <param name="target">The target-table member access written by this assignment.</param>
    /// <param name="value">The value expression, rendered by the shared expression pipeline.</param>
    public static UpdateJoinAssignment FromExpression(Expression target, Expression value) => new(target, UpdateValueKind.Expression, null, value);

    /// <summary>The target-table member access written by this assignment (for example <c>p.Item1.Name</c>).</summary>
    public Expression Target { get; }

    /// <summary>How the right-hand side produces its value.</summary>
    public UpdateValueKind Kind { get; }

    /// <summary>The captured CLR value when <see cref="Kind"/> is <see cref="UpdateValueKind.Constant"/>; otherwise <see langword="null"/>.</summary>
    public object? Constant { get; }

    /// <summary>The value expression when <see cref="Kind"/> is <see cref="UpdateValueKind.Expression"/>; otherwise <see langword="null"/>.</summary>
    public Expression? Value { get; }
}

/// <summary>
/// A multi-table <c>UPDATE</c> command: the target entity's rows are changed when they match a prepared
/// joined query, and the new values may read the joined tables. The target is the first table of the
/// join chain; the join conditions and the optional <c>WHERE</c> are carried by <see cref="Source"/>.
/// Rendered natively per provider: PostgreSQL/SQLite <c>UPDATE ... FROM ... WHERE</c>, SQL Server
/// <c>UPDATE &lt;alias&gt; ... FROM &lt;target&gt; JOIN ...</c>, MySQL/MariaDB <c>UPDATE ... JOIN ... SET</c>.
/// </summary>
internal sealed class UpdateJoinCommand : MutationCommand
{
    /// <summary>Creates an update-by-join command.</summary>
    /// <param name="targetType">The CLR type of the target table whose rows are updated (the first join source).</param>
    /// <param name="source">The prepared joined query whose source, joins and condition drive the update.</param>
    /// <param name="assignments">The <c>SET</c> assignments.</param>
    public UpdateJoinCommand(Type targetType, QueryCommand source, IReadOnlyList<UpdateJoinAssignment> assignments)
        : base(SqlStatementType.UpdateJoin, targetType)
    {
        Source = source;
        Assignments = assignments;
    }

    /// <summary>The prepared joined query: its <c>FROM</c> is the target, its joins and condition select the rows to change.</summary>
    public QueryCommand Source { get; }

    /// <summary>The <c>SET</c> assignments.</summary>
    public IReadOnlyList<UpdateJoinAssignment> Assignments { get; }
}

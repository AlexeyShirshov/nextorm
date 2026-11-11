using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The fixed-name refusals of the SQL Server function surface: eleven <c>SqlFunctions.SqlServer</c>
/// members that cannot be exposed faithfully and therefore throw <see cref="NotSupportedException"/>
/// with one stable diagnostic per member, before any SQL is rendered and before any ADO.NET command is
/// created.
/// <para>
/// This is a closed, hand-maintained table, not a capability framework: the members exist so the
/// refusal is reachable and testable, the body throws, the translator refuses the exact
/// <see cref="MethodInfo"/> before visiting its arguments, and the SQL-builder command boundary
/// preflights the command so a refusal wins even when the call sits inside a larger projection.
/// </para>
/// <para>
/// The reasons are of two kinds and the text keeps them apart: a <b>scope/CLR limitation</b>
/// (<c>SESSION_CONTEXT</c> returns <c>sql_variant</c>; <c>@@ROWCOUNT</c>/<c>@@ERROR</c> are
/// previous-statement scoped; the six <c>ERROR_*</c> names need a <c>CATCH</c> block) and an
/// <b>observed engine rejection</b> (<c>ANY_VALUE</c> is absent from SQL Server build
/// <c>17.0.5005.3</c>).
/// </para>
/// </summary>
internal static class SqlServerFunctionRefusals
{
    /// <summary>One refused member: the CLR member name, the SQL token and the reason after the colon.</summary>
    private readonly record struct Refusal(string Name, string Token, string Reason);

    private const string SessionContextReason =
        "returns sql_variant, for which nextorm has no faithful CLR result contract; "
        + "no lossy cast substitution is performed.";

    private const string SessionIdReason =
        "is not a SQL Server engine function; the documented function belongs to Azure Synapse. "
        + "SQL Server exposes session identity through @@SPID or sys.dm_exec_sessions.session_id; "
        + "no substitution is performed.";

    private const string StatementScopeReason =
        "describes the previous statement and is statement-scoped; "
        + "a per-row query projection cannot expose that value faithfully.";

    private const string CatchScopeReason =
        "requires a SQL Server CATCH-block scope; outside CATCH it returns NULL, "
        + "and nextorm does not establish that scope.";

    private const string AnyValueReason =
        "SQL Server build 17.0.5005.3 rejected this function with Msg 195, "
        + "including with preview features enabled; "
        + "no supported SQL Server execution contract is established.";

    private static readonly Refusal[] Refusals =
    [
        new(nameof(SqlServerFunctions.session_context), "SESSION_CONTEXT", SessionContextReason),
        new(nameof(SqlServerFunctions.session_id), "SESSION_ID", SessionIdReason),
        new(nameof(SqlServerFunctions.rowcount), "@@ROWCOUNT", StatementScopeReason),
        new(nameof(SqlServerFunctions.error), "@@ERROR", StatementScopeReason),
        new(nameof(SqlServerFunctions.error_number), "ERROR_NUMBER", CatchScopeReason),
        new(nameof(SqlServerFunctions.error_severity), "ERROR_SEVERITY", CatchScopeReason),
        new(nameof(SqlServerFunctions.error_state), "ERROR_STATE", CatchScopeReason),
        new(nameof(SqlServerFunctions.error_line), "ERROR_LINE", CatchScopeReason),
        new(nameof(SqlServerFunctions.error_message), "ERROR_MESSAGE", CatchScopeReason),
        new(nameof(SqlServerFunctions.error_procedure), "ERROR_PROCEDURE", CatchScopeReason),
        new(nameof(SqlServerFunctions.any_value), "ANY_VALUE", AnyValueReason)
    ];

    /// <summary>
    /// The exact diagnostic of a refused member: <c>SqlFunctions.SqlServer.&lt;member&gt; (&lt;TOKEN&gt;)
    /// is refused: &lt;reason&gt;</c>. Only called with one of the fixed member names.
    /// </summary>
    /// <param name="member">The CLR member name of the refused <see cref="SqlServerFunctions"/> member.</param>
    internal static string Diagnostic(string member)
    {
        for (var i = 0; i < Refusals.Length; i++)
        {
            if (Refusals[i].Name == member)
                return Format(Refusals[i]);
        }

        throw new ArgumentOutOfRangeException(nameof(member), member, "No such refused SQL Server function.");
    }

    /// <summary>
    /// True when <paramref name="method"/> is one of the eleven refused members, with the diagnostic in
    /// <paramref name="diagnostic"/>. The identity is the declaring type plus the fixed member name, so a
    /// same-named method on another type is never caught.
    /// </summary>
    internal static bool TryGetDiagnostic(MethodInfo? method, out string diagnostic)
    {
        diagnostic = string.Empty;

        if (method is null || method.DeclaringType != typeof(SqlServerFunctions))
            return false;

        var name = method.Name;

        for (var i = 0; i < Refusals.Length; i++)
        {
            if (Refusals[i].Name == name)
            {
                diagnostic = Format(Refusals[i]);
                return true;
            }
        }

        return false;
    }

    /// <summary>Throws the exact diagnostic when <paramref name="method"/> is a refused member.</summary>
    internal static void ThrowIfRefused(MethodInfo? method)
    {
        if (TryGetDiagnostic(method, out var diagnostic))
            throw new NotSupportedException(diagnostic);
    }

    /// <summary>
    /// SQL-builder command-boundary preflight: walks the command's clause expressions and throws the
    /// refused member's diagnostic before anything is rendered. Only the method-call node is inspected —
    /// a refused call is rejected before its arguments are visited, let alone evaluated. Nested lambdas
    /// mark another command's scope (a subquery is preflighted by its own <c>MakeSelect</c>), so the walk
    /// stops at them.
    /// </summary>
    internal static void ValidateCommand(QueryCommand command)
    {
        var collector = new RefusedCallCollector();

        if (command.SelectList is { } selectList)
        {
            for (var i = 0; i < selectList.Length; i++)
                collector.Visit(UnwrapClause(selectList[i].Expression));
        }

        collector.Visit(UnwrapClause(command.PreparedCondition ?? command.Condition));
        collector.Visit(UnwrapClause(command.PreparedPreWhere ?? command.PreWhere));
        collector.Visit(UnwrapClause(command.PreparedHaving ?? command.Having));

        if (command.Sorting is { } sorting)
        {
            for (var i = 0; i < sorting.Length; i++)
                collector.Visit(UnwrapClause(sorting[i].PreparedExpression ?? sorting[i].SortExpression));
        }

        if (command.GroupingList is { } grouping)
        {
            for (var i = 0; i < grouping.Length; i++)
                collector.Visit(UnwrapClause(grouping[i].Expression));
        }

        if (command.Joins is { } joins)
        {
            for (var i = 0; i < joins.Length; i++)
                collector.Visit(UnwrapClause(joins[i].JoinCondition));
        }
    }

    private static string Format(in Refusal refusal)
        => $"SqlFunctions.SqlServer.{refusal.Name} ({refusal.Token}) is refused: {refusal.Reason}";

    /// <summary>Unwraps the quote/lambda wrapper of a prepared clause and of a sort key.</summary>
    private static Expression UnwrapClause(Expression? expression)
    {
        if (expression is null)
            return Expression.Empty();

        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            expression = quote.Operand;

        return expression is LambdaExpression lambda ? lambda.Body : expression;
    }

    /// <summary>Throws on the first refused member; never walks into a nested command's lambda scope.</summary>
    private sealed class RefusedCallCollector : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            ThrowIfRefused(node.Method);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node) => node;
    }
}

using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Fail-closed validation shared by the SQL lowering (<see cref="SqlBuilder"/>) and the in-memory
/// evaluator (<see cref="InMemoryExtremeRow"/>) of <c>SelectWhereMax</c>/<c>SelectWhereMin</c>.
/// <para>
/// The window-ranked lowering can express a single physical source with a WHERE condition and a
/// projection only. Every other query modifier would be silently dropped (or applied at the wrong
/// stage), so it is rejected here, before any SQL is assembled or any row is produced. The same
/// checks run in the SQL and the parameter pass, and in the in-memory path, so no provider can
/// diverge on which combinations are accepted.
/// </para>
/// </summary>
internal static class ExtremeRowCompatibility
{
    /// <summary>
    /// Throws a <see cref="BuildSqlCommandException"/> for every modifier carried by
    /// <paramref name="cmd"/> that the extreme-row lowering cannot express. State-only: it reads the
    /// command, never the SQL text, so it is safe in both the SQL and the parameter pass.
    /// </summary>
    public static void EnsureModifiersCompatible(QueryCommand cmd)
    {
        if (cmd.Ctes is { Count: > 0 })
            throw NotCompatible("common table expressions (WITH)");

        if (cmd.Having is not null || cmd.PreparedHaving is not null)
            throw NotCompatible("HAVING");

        if (cmd.Windows is { Count: > 0 })
            throw NotCompatible("named windows");

        if (ContainsWindowFunction(cmd.SelectList))
            throw NotCompatible("window functions (OVER)");

        if (cmd.GroupingList is { Length: > 0 } || cmd.GroupBy is not null)
            throw NotCompatible("GROUP BY");

        if (cmd.Joins is { Length: > 0 })
            throw NotCompatible("joins");

        if (cmd.LimitBy is not null)
            throw NotCompatible("LIMIT BY");

        if (cmd.ArrayJoinExpressions is { Count: > 0 })
            throw NotCompatible("ARRAY JOIN");

        if (cmd.PreWhere is not null)
            throw NotCompatible("PREWHERE");

        if (!cmd.Paging.IsEmpty)
            throw NotCompatible("paging");

        if (cmd.DistinctOn is not null)
            throw NotCompatible("DISTINCT ON");

        if (cmd.UnionQuery is not null)
            throw NotCompatible("a set operation");

        if (cmd.TableSample is not null)
            throw NotCompatible("TABLESAMPLE");

        if (cmd.Temporal is not null)
            throw NotCompatible("the FOR SYSTEM_TIME clause");

        if (cmd.RowLock is not null)
            throw NotCompatible("row locking (FOR UPDATE/FOR SHARE)");

        if (cmd.TableHints is { Count: > 0 })
            throw NotCompatible("table hints");

        if (cmd.IndexHints is { Count: > 0 })
            throw NotCompatible("index hints");

        if (cmd.TablesInScopeHints is { Count: > 0 })
            throw NotCompatible("tables-in-scope hints");

        if (cmd.Hints is { Count: > 0 })
            throw NotCompatible("query hints");

        if (cmd.Final)
            throw NotCompatible("FINAL");

        if (cmd.SampleRatio is not null)
            throw NotCompatible("SAMPLE");

        if (cmd.Settings is { Count: > 0 })
            throw NotCompatible("SETTINGS");

        if (cmd.ForJsonClause is not null || cmd.ForXmlClause is not null)
            throw NotCompatible("FOR JSON/FOR XML");
    }

    private static BuildSqlCommandException NotCompatible(string modifier)
        => new($"SelectWhereMax/SelectWhereMin cannot be combined with {modifier}.");

    /// <summary>
    /// True when any prepared select column projects a <see cref="WindowFunction{T}"/> call (an
    /// <c>OVER</c> clause). The lowering computes its own row numbering over the source; a user window
    /// function would silently be evaluated over the already-filtered derived table instead, so it is
    /// rejected rather than moved.
    /// </summary>
    private static bool ContainsWindowFunction(SelectExpression[]? selectList)
    {
        if (selectList is null) return false;

        for (var i = 0; i < selectList.Length; i++)
        {
            if (selectList[i].Expression is { } expression && WindowFunctionDetector.Contains(expression))
                return true;
        }

        return false;
    }

    private sealed class WindowFunctionDetector : ExpressionVisitor
    {
        internal static bool Contains(Expression expression)
        {
            var visitor = new WindowFunctionDetector();
            visitor.Visit(expression);
            return visitor._found;
        }

        private bool _found;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;
            if (declaringType is { IsGenericType: true }
                && declaringType.GetGenericTypeDefinition() == typeof(WindowFunction<>))
            {
                _found = true;
                return node;
            }

            return base.VisitMethodCall(node);
        }
    }
}

using System.Linq.Expressions;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// Translates a captured collection indexed from the query (<c>dict[column]</c>, <c>list[column]</c>,
/// an array index). A key that does not depend on the row is a client value and folds to a parameter;
/// a key that references a query column is rendered as a portable
/// <c>CASE WHEN key = @k THEN @v … END</c> over the collection's entries.
/// </summary>
internal static class DictionaryLookupTranslator
{
    /// <summary>
    /// Translates a captured-collection lookup. Returns <c>false</c> when the node is not one, so the
    /// caller can fall through to the normal member/method handling.
    /// </summary>
    /// <param name="visitor">The visitor rendering the expression.</param>
    /// <param name="node">The indexer expression (index node, <c>get_Item</c> call or array index).</param>
    /// <returns><see langword="true"/> when the node was translated.</returns>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, Expression node)
    {
        if (!DictionaryLookup.TryGetLookup(node, out var collectionExp, out var keyExp))
            return false;

        if (!keyExp.Has<ParameterExpression>())
        {
            // dict[const] is fully determined on the client; evaluating the whole lookup keeps a missing
            // key surfacing as the usual KeyNotFoundException instead of a driver error.
            visitor.EmitFoldedParameter(node);
            return true;
        }

        var entries = ResolveEntries(visitor, node, collectionExp);

        if (entries.Count == 0)
            throw new NotSupportedException(
                "The captured collection indexed by a query expression is empty, so the lookup has no SQL form.");

        // The key may itself reference captured locals (dict[column + bias]); collecting them in both
        // passes, before the key/value parameters, keeps a cached plan's parameter order identical.
        if (visitor.IsParamMode)
        {
            visitor.Visit(keyExp);
            EmitBranches(visitor, string.Empty, entries, null);
        }
        else
        {
            var key = visitor.VisitToString(keyExp);
            visitor.Builder!.Append(visitor.Dialect.MakeCase(
                BuildCase(visitor, key, entries),
                TypeFacts.IsBoolean(node.Type),
                visitor.IsPredicateContext,
                visitor.KeywordCase));
        }

        return true;
    }

    /// <summary>
    /// Returns the entries to render, preferring the ones evaluated while preparing the condition (so
    /// the SQL and parameter passes agree and a cached plan is only reused for the same shape). A
    /// lookup outside the prepared condition of a cacheable command is refused: its shape is not part
    /// of the plan key, so a cached plan could bind the wrong number of parameters.
    /// </summary>
    private static List<LookupEntry> ResolveEntries(BaseExpressionVisitor visitor, Expression node, Expression collectionExp)
    {
        if (visitor.QueryProvider is QueryCommand command)
        {
            if (command.LookupPartitions is { } partitions && partitions.TryGetValue(node, out var cached))
                return cached;

            if (command.Cache && command.ShapeScanned)
                throw new NotSupportedException(
                    "A captured collection indexed by a query expression is supported only in a WHERE or pre-where condition.");
        }

        return DictionaryLookup.Evaluate(collectionExp, visitor.QueryProvider);
    }

    /// <summary>Builds the <c>CASE WHEN key = @kN THEN @vN … END</c> text for an already-rendered key, registering each key/value as a parameter.</summary>
    private static string BuildCase(BaseExpressionVisitor visitor, string key, List<LookupEntry> entries)
    {
        visitor.NeedAliasForColumn = true;

        var caseBuilder = visitor.BuilderPool.Get();
        try
        {
            caseBuilder.Append(visitor.Kw("case"));
            EmitBranches(visitor, key, entries, caseBuilder);
            caseBuilder.Append(visitor.Kw(" end"));
            return caseBuilder.ToString();
        }
        finally
        {
            visitor.BuilderPool.Return(caseBuilder);
        }
    }

    /// <summary>Registers the key/value parameters in the entry order; appends the branch text when a builder is supplied.</summary>
    private static void EmitBranches(BaseExpressionVisitor visitor, string key, List<LookupEntry> entries, StringBuilder? caseBuilder)
    {
        for (var (i, cnt) = (0, entries.Count); i < cnt; i++)
        {
            var keyName = visitor.ParameterProvider.GetParamName();
            var valueName = visitor.ParameterProvider.GetParamName();
            visitor.Params.Add(new Parameter(keyName, entries[i].Key));
            visitor.Params.Add(new Parameter(valueName, entries[i].Value));

            if (caseBuilder is null)
                continue;

            caseBuilder.Append(visitor.Kw(" when ")).Append(key).Append(" = ").Append(visitor.Dialect.MakeParam(keyName))
                .Append(visitor.Kw(" then ")).Append(visitor.Dialect.MakeParam(valueName));
        }
    }
}

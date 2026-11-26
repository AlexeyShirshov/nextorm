using System.Linq.Expressions;

namespace NextORM.Core;

public partial class QueryCommand
{
    // If preparation left an authored clause intact, the next matching command already owns
    // its executable form. Keep only comparison syntax for that clause in the descriptor.
    // More complex preparations keep the existing executable snapshot for now.
    internal (RawShape Shape, QueryCommand Recipe, bool UseCurrentWhere, bool UseCurrentSorting)? TryDetachRawClauses(RawShape shape)
    {
        if (shape.Closures is not { Length: > 0 } || !SupportsRawWhereReplay(shape))
            return null;

        var condition = shape.Condition;
        if (!ReferenceEquals(condition, _condition)
            || (condition is not null && !HasUnchangedClause(condition, PreparedCondition)))
            return null;

        var visitor = new RawClauseCaptureVisitor();
        visitor.Visit(shape.Exp);

        if (_selectList is { } columns)
        {
            foreach (var column in columns)
            {
                if (column.Converter is not null)
                    return null;
                visitor.Visit(column.Expression);
            }
        }

        if (!visitor.Supported)
            return null;

        visitor.RemoveReceivers = true;
        var comparisonCondition = (LambdaExpression?)visitor.Visit(condition);
        var useCurrentWhere = !ReferenceEquals(comparisonCondition, condition);
        var comparisonSorting = shape.Sorting;
        var useCurrentSorting = false;
        if (shape.Sorting is { } authored && _sorting is { } prepared && authored.Length == prepared.Length)
        {
            for (var i = 0; i < authored.Length && visitor.Supported; i++)
            {
                ref readonly var sort = ref authored[i];
                var expression = visitor.Visit(sort.SortExpression);
                if (ReferenceEquals(expression, sort.SortExpression))
                    continue;

                comparisonSorting = useCurrentSorting ? comparisonSorting! : (Sorting[])authored.Clone();
                comparisonSorting[i] = new Sorting(in sort, expression, null);
                useCurrentSorting = true;
            }

            if (useCurrentSorting)
            {
                // Applying a replay uses the whole fresh sort array. Every key must therefore be
                // unchanged, including siblings that didn't contain captures and ordinal keys.
                for (var i = 0; i < authored.Length; i++)
                {
                    var sort = authored[i].SortExpression;
                    if (authored[i].PreparedExpression is not null
                        || authored[i].Direction != prepared[i].Direction
                        || authored[i].NullsPosition != prepared[i].NullsPosition
                        || authored[i].ColumnIndex != prepared[i].ColumnIndex
                        || !ReferenceEquals(sort, prepared[i].SortExpression)
                        || !HasUnchangedClause(sort, prepared[i].PreparedExpression))
                        return null;
                }
            }
        }
        if (!useCurrentSorting)
        {
            visitor.RemoveReceivers = false;
            visitor.CheckSorts(shape.Sorting);
            visitor.CheckSorts(_sorting);
        }

        if (!visitor.Supported || (!useCurrentWhere && !useCurrentSorting))
            return null;

        var recipe = CloneForCache();
        if (useCurrentWhere)
            recipe.PreparedCondition = null;
        if (useCurrentSorting)
            recipe._sorting = null;

        return (shape.WithComparisonClauses(comparisonCondition, comparisonSorting), recipe, useCurrentWhere, useCurrentSorting);
    }

    private static bool HasUnchangedClause(Expression? authored, Expression? prepared)
        // The correlated visitor recreates the lambda wrapper even for an unchanged body. Require
        // the same binder and original leaves, not structural equality with another expression tree.
        => ReferenceEquals(authored, prepared)
            || (authored is LambdaExpression { Parameters: [var parameter] } original
                && prepared is LambdaExpression { Parameters: [var preparedParameter] } normalized
                && ReferenceEquals(parameter, preparedParameter)
                && original.Type == normalized.Type
                && HasUnchangedClauseBody(original.Body, normalized.Body));

    private static bool HasUnchangedClauseBody(Expression authored, Expression prepared)
    {
        if (ReferenceEquals(authored, prepared))
            return true;

        // ORDER BY boxes a value key and the visitor recreates that built-in conversion.
        if (authored is UnaryExpression { NodeType: ExpressionType.Convert, Method: null, Type: var type } box
            && type == typeof(object)
            && prepared is UnaryExpression { NodeType: ExpressionType.Convert, Method: null } preparedBox
            && preparedBox.Type == type)
            return HasUnchangedClauseBody(box.Operand, preparedBox.Operand);

        // Normalization also recreates ordinary && and || nodes. Only ignore that rebuilding for
        // built-in bool operators; a lifted or user-defined operator, or any rewritten leaf, falls
        // back to the existing recipe rather than substituting a different computation.
        return authored is BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.OrElse, Method: null, IsLifted: false } left
            && left.Type == typeof(bool)
            && prepared is BinaryExpression { Method: null, IsLifted: false } right
            && right.Type == typeof(bool)
            && left.NodeType == right.NodeType
            && HasUnchangedClauseBody(left.Left, right.Left)
            && HasUnchangedClauseBody(left.Right, right.Right);
    }

    private bool SupportsRawWhereReplay(RawShape shape)
        => shape.Joins is null && shape.Ctes is null && shape.Union is null && shape.Components.IsEmpty
            && shape.Group is null && shape.Having is null && shape.PreWhere is null
            && shape.PerParentPartition is null && shape.ArrayJoins is null && shape.Windows is null
            && shape.LimitBy is null && shape.DistinctOn is null && shape.ExtremeRow is null
            && IsPlainRecipeSource(shape.From) && IsPlainRecipeSource(_from)
            && _joins is null && _ctes is null && _union is null
            && _components.IsEmpty && OuterRegistry is null
            && NavigationPaths is null && NavigationChain is null && JunctionChildFilter is null
            && _groupExp is null && _having is null && _preWhere is null && PerParentPartition is null
            && _arrayJoins is null && _windows is null && LimitBy is null && DistinctOn is null && ExtremeRow is null
            && (NamingConvention is null or SnakeCaseNamingConvention)
            && (ResolvedNamingConvention is null or SnakeCaseNamingConvention)
            && Prep is
            {
                ReferencedQueries: null, OuterRefs: null, OuterRefBindings: null,
                PreparedComponents: null, EffectiveGroupingKeys: null, EffectiveHaving: null,
                EffectiveSourcePredicate: null, GroupingList: null or { Length: 0 },
                LimitByColumns: null or { Length: 0 }, DistinctOnColumns: null or { Length: 0 },
                ExtremeRowColumns: null or { Length: 0 }, ExtremeRowGroupByColumns: null or { Length: 0 },
                PerParentPartitionColumns: null or { Length: 0 }, PreparedArrayJoin: null, PreparedHaving: null,
                PreparedPreWhere: null, LookupPartitions: null, WhereValueListShape: null,
                PreWhereValueListShape: null
            };

    private static bool IsPlainRecipeSource(FromExpression? from)
        => IsPlainTableSource(from)
            && from?.LinqSource is null && from?.DerivedFragment is null && from?.SourceBinding is null;

    private sealed class RawClauseCaptureVisitor : ExpressionVisitor
    {
        internal bool Supported { get; private set; } = true;
        internal bool RemoveReceivers { get; set; }

        internal bool CheckSorts(Sorting[]? sorts)
        {
            if (sorts is not null)
            {
                foreach (var sort in sorts)
                {
                    Visit(sort.SortExpression);
                    Visit(sort.PreparedExpression);
                }
            }

            return Supported;
        }

        public override Expression? Visit(Expression? node)
        {
            if (node is null || !Supported)
                return node;
            if (node is LambdaExpression or BinaryExpression or UnaryExpression or ParameterExpression
                or MemberExpression or ConstantExpression or NewExpression or MethodCallExpression
                or ConditionalExpression or MemberInitExpression or NewArrayExpression or DefaultExpression)
                return base.Visit(node);

            Supported = false;
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // Same comparison rule as the normalized SQL key: the member and declared receiver
            // type remain in the syntax; the raw descriptor separately verifies receiver tokens.
            if (RemoveReceivers && node.Expression is ConstantExpression { Value: not null and not string } receiver
                && !receiver.Type.IsValueType && !typeof(QueryCommand).IsAssignableFrom(node.Type))
                return node.Update(Expression.Constant(null, receiver.Type));

            return base.VisitMember(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is { } value && value is not string && !value.GetType().IsPrimitive && !value.GetType().IsEnum)
                Supported = false;
            return node;
        }
    }
}

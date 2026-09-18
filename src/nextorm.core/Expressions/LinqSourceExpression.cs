using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// In-memory-only <c>FROM</c> source produced by <c>SelectMany</c>/<c>GroupJoin</c>. It is not a
/// <see cref="QueryCommand"/> because the source is a computed flatten/grouping over another command
/// rather than a stored query. The in-memory provider materialises it; the SQL providers reject it
/// with <see cref="NotSupportedException"/> (see <c>SqlBuilder.MakeFrom</c>).
/// </summary>
internal sealed class LinqSourceExpression
{
    /// <summary>The command whose rows feed the operator (the outer sequence).</summary>
    public required QueryCommand OuterCommand { get; init; }
    /// <summary>The inner command of a <c>GroupJoin</c>; <c>null</c> for <c>SelectMany</c>.</summary>
    public QueryCommand? InnerCommand { get; init; }
    /// <summary>The element type of <see cref="OuterCommand"/>.</summary>
    public required Type OuterType { get; init; }
    /// <summary>The element type produced by the operator (what the returned builder projects).</summary>
    public required Type ResultType { get; init; }
    /// <summary>The element type yielded by the <c>SelectMany</c> collection selector.</summary>
    public Type? CollectionType { get; init; }
    /// <summary>The element type of <see cref="InnerCommand"/>.</summary>
    public Type? InnerType { get; init; }
    /// <summary>The key type of a <c>GroupJoin</c>.</summary>
    public Type? KeyType { get; init; }
    /// <summary><c>Func&lt;TOuter, IEnumerable&lt;TCollection&gt;&gt;</c> for <c>SelectMany</c>.</summary>
    public LambdaExpression? CollectionSelector { get; init; }
    /// <summary><c>Func&lt;TOuter, TInner, TResult&gt;</c> or <c>Func&lt;TOuter, IEnumerable&lt;TInner&gt;, TResult&gt;</c>.</summary>
    public LambdaExpression? ResultSelector { get; init; }
    /// <summary><c>Func&lt;TOuter, TKey&gt;</c> for <c>GroupJoin</c>.</summary>
    public LambdaExpression? OuterKeySelector { get; init; }
    /// <summary><c>Func&lt;TInner, TKey&gt;</c> for <c>GroupJoin</c>.</summary>
    public LambdaExpression? InnerKeySelector { get; init; }
    /// <summary>True for <c>GroupJoin</c>, false for <c>SelectMany</c>.</summary>
    public bool IsGroupJoin { get; init; }
}

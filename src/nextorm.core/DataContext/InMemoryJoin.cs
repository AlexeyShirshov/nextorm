using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Join axis of the in-memory provider: evaluates an <c>INNER</c>/<c>LEFT</c>/<c>RIGHT</c>/
/// <c>FULL</c>/<c>CROSS</c> join row by row over the context's registered data. The
/// reflection-addressed <see cref="InMemoryDataContext"/> entry point (<c>miLoopJoin</c>) stays a thin
/// wrapper and delegates here; the projection rows come from
/// <see cref="InMemoryProjectionFactory"/>. Body is moved verbatim from <see cref="InMemoryDataContext"/>
/// (F13 follow-up).
/// </summary>
internal static class InMemoryJoin
{
    public static IEnumerable<TResult> LoopJoin<TLeft, TRight, TResult>(InMemoryDataContext context, QueryCommand queryCommand, IEnumerable<TLeft>? leftData, JoinExpression join, int dim)
    {
        if (join.From.TableFunction is not null)
            throw new NotSupportedException("Table-valued function sources are not supported by the in-memory provider.");

        if (join.Strictness is not JoinStrictness.Default)
            throw new NotSupportedException($"The {join.Strictness} join modifier is not supported by the in-memory provider.");

        if (join.IsGlobal)
            throw new NotSupportedException("The GLOBAL join modifier is not supported by the in-memory provider.");

        if (leftData is null)
        {
            //var dataPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TLeft>(Array.Empty<TLeft>().AsEnumerable()));
            if (!context.Data.TryGetValue(typeof(TLeft), out var vl))
            {
                vl = Array.Empty<TLeft>();
            }
            leftData = (IEnumerable<TLeft>)vl!;
        }

        //var joinPayload = queryCommand.GetNotNullOrAddPayload(() => new InMemoryDataPayload<TRight>(Array.Empty<TRight>().AsEnumerable()));
        if (!context.Data.TryGetValue(typeof(TRight), out var vr))
        {
            vr = Array.Empty<TRight>();
        }
        var joinPayload = (IEnumerable<TRight>)vr!;

        var res = new List<TResult>();

        switch (join.JoinType)
        {
            case JoinType.Cross:
            case JoinType.FullCross:
                foreach (var item in leftData)
                {
                    foreach (var itemInner in joinPayload)
                    {
                        res.Add((TResult)InMemoryProjectionFactory.CreateProjection(item, itemInner, dim));
                    }
                }
                break;

            case JoinType.Inner:
                {
                    var condition = InMemoryProjectionFactory.CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var item in leftData)
                    {
                        foreach (var itemInner in joinPayload)
                        {
                            if (condition(item, itemInner))
                            {
                                res.Add((TResult)InMemoryProjectionFactory.CreateProjection(item, itemInner, dim));
                            }
                        }
                    }
                }
                break;

            case JoinType.Left:
                {
                    var condition = InMemoryProjectionFactory.CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var item in leftData)
                    {
                        var matched = false;
                        foreach (var itemInner in joinPayload)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                res.Add((TResult)InMemoryProjectionFactory.CreateProjection(item, itemInner, dim));
                            }
                        }

                        if (!matched)
                            res.Add((TResult)InMemoryProjectionFactory.CreateProjection(item, default(TRight)!, dim));
                    }
                }
                break;

            case JoinType.Right:
                {
                    var condition = InMemoryProjectionFactory.CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var itemInner in joinPayload)
                    {
                        var matched = false;
                        foreach (var item in leftData)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                res.Add((TResult)InMemoryProjectionFactory.CreateProjection(item, itemInner, dim));
                            }
                        }

                        if (!matched)
                            res.Add((TResult)InMemoryProjectionFactory.CreateProjection(default(TLeft)!, itemInner, dim));
                    }
                }
                break;

            case JoinType.Full:
                {
                    var condition = InMemoryProjectionFactory.CompileJoinCondition<TLeft, TRight>(join);

                    foreach (var item in leftData)
                    {
                        var matched = false;
                        foreach (var itemInner in joinPayload)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                res.Add((TResult)InMemoryProjectionFactory.CreateProjection(item, itemInner, dim));
                            }
                        }

                        if (!matched)
                            res.Add((TResult)InMemoryProjectionFactory.CreateProjection(item, default(TRight)!, dim));
                    }

                    foreach (var itemInner in joinPayload)
                    {
                        var matched = false;
                        foreach (var item in leftData)
                        {
                            if (condition(item, itemInner))
                            {
                                matched = true;
                                break;
                            }
                        }

                        if (!matched)
                            res.Add((TResult)InMemoryProjectionFactory.CreateProjection(default(TLeft)!, itemInner, dim));
                    }
                }
                break;

            default:
                throw new NotSupportedException(join.JoinType.ToString());
        }

        return res;
    }
}

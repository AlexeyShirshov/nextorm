using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Flattens a <c>params</c> argument array, keeping the leading fixed arguments in front
/// of the flattened values. The C# compiler wraps the arguments of a <c>params</c> call in a single
/// <see cref="NewArrayExpression"/>; this turns it back into the items (shared by the cross-provider
/// scalar and the extended scalar translators).
/// </summary>
internal static class ArgumentFlattener
{
    internal static IReadOnlyList<Expression> Flatten(IReadOnlyList<Expression> args, int leading)
    {
        if (leading < args.Count && args[leading] is NewArrayExpression { Expressions: var expressions })
        {
            if (leading == 0)
                return expressions;

            var items = new List<Expression>(leading + expressions.Count);
            for (var (i, cnt) = (0, leading); i < cnt; i++)
                items.Add(args[i]);

            items.AddRange(expressions);
            return items;
        }

        return args;
    }
}

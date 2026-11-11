using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the portable binary functions of <see cref="CommonFunctions"/> —
/// <c>base64_encode</c>, <c>base64_decode</c> and <c>crypt_gen_random</c> — through the dialect's
/// per-name capability pair (<see cref="ISqlDialect.SupportsBase64Encode"/>,
/// <see cref="ISqlDialect.SupportsBase64Decode"/>, <see cref="ISqlDialect.SupportsCryptGenRandom"/>
/// and their <c>Make*</c> renderers). Only the enumerated members of <see cref="CommonFunctions"/>
/// are enrolled and the declaring type is checked by identity, so a same-named provider-only member
/// falls through to its own translator. A provider without the capability fails closed with the
/// unsupported-provider message instead of emitting SQL it cannot execute.
/// </summary>
internal static class PortableBinaryFunctionTranslator
{
    /// <summary>The portable binary members, matched by CLR name inside <see cref="CommonFunctions"/>.</summary>
    private static readonly HashSet<string> FunctionNames = new(StringComparer.Ordinal)
    {
        nameof(CommonFunctions.base64_encode),
        nameof(CommonFunctions.base64_decode),
        nameof(CommonFunctions.crypt_gen_random)
    };

    /// <summary>The largest length the portable <c>crypt_gen_random</c> domain promises.</summary>
    private const int MaxCryptLength = 1024;

    /// <summary>Translates a portable binary call; returns <c>false</c> when it is not one.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(CommonFunctions) || !FunctionNames.Contains(node.Method.Name))
            return false;

        var name = node.Method.Name;

        RequireSingleArgument(name, node.Arguments.Count);

        var argument = node.Arguments[0];

        if (name == nameof(CommonFunctions.crypt_gen_random))
            ValidateCryptLength(argument);

        var supported = name switch
        {
            nameof(CommonFunctions.base64_encode) => visitor.Dialect.SupportsBase64Encode,
            nameof(CommonFunctions.base64_decode) => visitor.Dialect.SupportsBase64Decode,
            _ => visitor.Dialect.SupportsCryptGenRandom
        };

        if (!supported)
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(argument);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = visitor.VisitToString(argument);
        visitor.Builder!.Append(name switch
        {
            nameof(CommonFunctions.base64_encode) => visitor.Dialect.MakeBase64Encode(rendered),
            nameof(CommonFunctions.base64_decode) => visitor.Dialect.MakeBase64Decode(rendered),
            _ => visitor.Dialect.MakeCryptGenRandom(rendered)
        });
        return true;
    }

    /// <summary>Rejects an argument list that does not carry exactly one operand.</summary>
    internal static void RequireSingleArgument(string name, int argumentCount)
    {
        if (argumentCount != 1)
            throw new ArgumentException($"The {name} function takes exactly one argument.", nameof(argumentCount));
    }

    /// <summary>
    /// Rejects a constant <c>crypt_gen_random</c> length outside the portable 1–1024 domain before any
    /// SQL is emitted. A NULL constant and a non-constant length are accepted: the former renders the
    /// typed NULL, the latter is a runtime value outside the promised domain. Any other constant type
    /// is rejected as well.
    /// </summary>
    private static void ValidateCryptLength(Expression argument)
    {
        if (TypeFacts.UnwrapConvert(argument) is not ConstantExpression constant)
            return;

        if (constant.Value is null)
            return;

        if (constant.Value is int length && length is >= 1 and <= MaxCryptLength)
            return;

        throw new ArgumentOutOfRangeException(
            nameof(argument),
            constant.Value,
            $"crypt_gen_random length must be a constant between 1 and {MaxCryptLength} bytes.");
    }
}

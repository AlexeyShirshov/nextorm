namespace nextorm.core;

/// <summary>
/// Names of the runtime parameters that <c>NORM.Param(index)</c> refers to.
/// <para>
/// The name is a contract, not an implementation detail: it is emitted into the SQL text and
/// then used to look the parameter up in <c>DbCommand.Parameters</c>, so the prefix must stay
/// stable. It lives here rather than on <c>DbContext</c> so that the SQL-building collaborators
/// do not have to reach into the data context for it.
/// </para>
/// </summary>
internal static class NormParam
{
    public const string Prefix = "norm_p";

    private static readonly ParamNameCache _names = new(Prefix);

    public static string GetName(int index) => _names.Get(index);

    public static bool IsName(string name) =>
        name.StartsWith(Prefix, StringComparison.Ordinal) &&
        int.TryParse(name.AsSpan(Prefix.Length), out _);
}

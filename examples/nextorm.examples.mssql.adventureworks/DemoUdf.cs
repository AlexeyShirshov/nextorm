using NextORM.Core;

namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>
/// T-SQL <c>FORMAT</c> has no cross-provider LINQ surface (the template languages differ per provider);
/// the documented way to use it is a provider-specific <see cref="SqlFunctionAttribute"/> UDF. The body
/// is never executed — the attribute only tells the translator to emit <c>format(...)</c>.
/// </summary>
public static class DemoUdf
{
    [SqlFunction("format")]
    public static string Format(DateTime? value, string format) => throw new NotSupportedException();
}

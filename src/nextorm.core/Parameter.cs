namespace NextORM.Core;

/// <summary>
/// Name/value pair for a single command parameter.
/// </summary>
/// <remarks>
/// Renamed from <c>Param</c> (<c>Parameter</c> is the full word). See
/// <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-13.
/// </remarks>
public class Parameter(string name, object? value)
{
    public string Name { get; set; } = name;
    public object? Value { get; set; } = value;
}

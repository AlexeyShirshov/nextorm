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

    /// <summary>
    /// True when <see cref="Value"/> is fully determined by the query shape (an inline value list or a
    /// constant-folded expression), so a cached plan does not have to copy it into the command on every
    /// execution. A captured collection/variable is not stable: its value can change while the plan key
    /// stays the same. Internal: this is an engine optimization, not part of the public contract.
    /// </summary>
    internal bool Stable { get; set; }
}

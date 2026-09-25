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
    /// <summary>
    /// The placeholder name (without the provider's parameter prefix) used to bind
    /// <see cref="Value"/> in the generated SQL.
    /// </summary>
    public string Name { get; set; } = name;
    /// <summary>
    /// The value bound to <see cref="Name"/>, or <see langword="null"/> for a SQL <c>NULL</c>.
    /// </summary>
    public object? Value { get; set; } = value;

    /// <summary>
    /// True when <see cref="Value"/> is fully determined by the query shape (an inline value list or a
    /// constant-folded expression), so a cached plan does not have to copy it into the command on every
    /// execution. A captured collection/variable is not stable: its value can change while the plan key
    /// stays the same. Internal: this is an engine optimization, not part of the public contract.
    /// </summary>
    internal bool Stable { get; set; }

    /// <summary>
    /// The captured expression this parameter was produced from (a closure member access), or
    /// <see langword="null"/> for a parameter that does not bound a captured value. Used to register a
    /// captured local once per rendered statement, so every occurrence reuses the same placeholder.
    /// Internal: an engine bookkeeping detail, not part of the public contract.
    /// </summary>
    internal ExpressionKey? CapturedKey { get; set; }
}

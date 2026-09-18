namespace nextorm.core;

/// <summary>
/// Ordered list of the parameters of a single command.
/// </summary>
/// <remarks>
/// Deriving a public collection type from <see cref="List{T}"/> exposes mutable collection state in
/// the API surface; prefer returning <see cref="IReadOnlyList{T}"/>.
/// </remarks>
public class ParamList : List<Param>
{

}

/// <summary>
/// Name/value pair for a single command parameter.
/// </summary>
/// <remarks>
/// <c>Param</c> abbreviates <c>Parameter</c>; the recommended name is <c>Parameter</c>. The mutable
/// setters also make the value changeable after creation. See <c>API-NAMING-REVIEW.md</c> finding P1-13.
/// </remarks>
public class Param(string name, object? value)
{
    public string Name { get; set; } = name;
    public object? Value { get; set; } = value;
}
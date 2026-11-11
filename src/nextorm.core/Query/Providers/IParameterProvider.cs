namespace NextORM.Core;

/// <summary>
/// Allocates parameter names (for example <c>p0</c>, <c>p1</c>, ...) while a command is built.
/// </summary>
/// <remarks>
/// Renamed from <c>IParamProvider</c> (<c>Param</c> abbreviated <c>Parameter</c>).
/// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-13.
/// </remarks>
public interface IParameterProvider
{
    /// <summary>
    /// Returns the next unused parameter name for the command being built.
    /// </summary>
    /// <returns>A unique parameter name such as <c>p0</c>.</returns>
    string GetParamName();
}

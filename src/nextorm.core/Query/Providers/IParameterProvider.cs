namespace NextORM.Core;

/// <summary>
/// Allocates parameter names (for example <c>p0</c>, <c>p1</c>, ...) while a command is built.
/// </summary>
public interface IParameterProvider
{
    /// <summary>
    /// Returns the next unused parameter name for the command being built.
    /// </summary>
    /// <returns>A unique parameter name such as <c>p0</c>.</returns>
    string GetParamName();
}

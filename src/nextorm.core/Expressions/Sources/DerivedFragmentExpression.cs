namespace NextORM.Core;

/// <summary>
/// An opaque derived-table source whose body is produced by a render delegate rather than by a
/// <see cref="QueryCommand"/> subquery. The delegate returns the SQL text in SQL mode and performs the
/// same expression walk (collecting parameters) in parameter mode, emitting nothing. Used by the
/// portable <c>One</c> lowering to render its windowed inner as <c>(...)</c> through the common
/// <c>MakeSelect</c> outer. The fragment carries the entity type it exposes and is identified by
/// reference for plan-cache purposes (it never enters a keyed command's source identity).
/// </summary>
internal sealed class DerivedFragmentExpression(Type entityType, Func<SqlBuildContext, string?> render)
{
    /// <summary>The entity type exposed by the fragment; registered as a physical-shaped source.</summary>
    internal Type EntityType { get; } = entityType;

    /// <summary>Renders the fragment: SQL text in SQL mode, parameter walk (no text) in parameter mode.</summary>
    internal Func<SqlBuildContext, string?> Render { get; } = render;
}

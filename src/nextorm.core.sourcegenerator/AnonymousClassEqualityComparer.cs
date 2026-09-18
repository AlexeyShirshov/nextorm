using Microsoft.CodeAnalysis;

namespace nextorm.core.sourcegenerator;

/// <summary>
/// Incremental source generator (currently an empty stub).
/// </summary>
/// <remarks>
/// The name is misleading: the type is an <see cref="IIncrementalGenerator"/>, not an equality
/// comparer. Rename to <c>AnonymousClassGenerator</c> (or remove if unused) and make it
/// <c>internal</c>. See <c>API-NAMING-REVIEW.md</c> finding P2-25.
/// </remarks>
[Generator]
public class AnonymousClassEqualityComparer : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}

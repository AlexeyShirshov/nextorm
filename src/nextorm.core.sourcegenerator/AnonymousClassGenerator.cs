using Microsoft.CodeAnalysis;

namespace NextORM.Core.SourceGenerator;

/// <summary>
/// Incremental source generator (currently an empty stub).
/// </summary>
/// <remarks>
/// Renamed from <c>AnonymousClassEqualityComparer</c> (the old name claimed an equality comparer that
/// never existed) and made <c>internal</c>. See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P2-25.
/// </remarks>
[Generator]
internal sealed class AnonymousClassGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}

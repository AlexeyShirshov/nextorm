using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Declares the trimming requirement the binary-node rebuilders have on the numeric operand types.
/// <para>
/// The pipeline rebuilds binary expressions from operands whose types are only known at run time
/// (<c>CorrelatedQueryExpressionVisitor</c> and the other rewriters call
/// <c>Expression.MakeBinary(nodeType, left, right)</c>). For a <see cref="decimal"/> operand that call
/// resolves the operator through reflection on the operand type. The library never names the operators
/// itself — the operands are expression-tree nodes, not values — so without a declared requirement the
/// trimmer removes them and the rebuild fails with
/// <c>The binary operator Divide is not defined for the types 'System.Decimal'/'System.Nullable&lt;System.Decimal&gt;'</c>
/// (the defect reported by the AOT probes for #370).
/// </para>
/// <para>
/// Passing the literal <c>typeof(decimal)</c> through a parameter annotated with
/// <see cref="DynamicallyAccessedMembersAttribute"/> states that requirement to the trimmer. This is the
/// annotation mechanism, not a suppression and not a rooting switch: no diagnostic is silenced, no
/// assembly or member is rooted wholesale, and nothing about the consumer's configuration changes.
/// The discarded <c>_ =</c> result is intentional — the trimmer reads the annotation at the call site,
/// it does not need the returned array.
/// </para>
/// <para>
/// Scope: this contract covers <see cref="decimal"/> only, because the literal type is what makes the
/// requirement statically analysable. Operators over a <em>user-supplied</em> runtime type cannot be
/// covered this way without annotating the public surface (a public-API change) or rooting broadly;
/// those shapes wait for the compile-time generator.
/// </para>
/// </summary>
internal static class NumericOperators
{
    /// <summary>
    /// Called from <see cref="TypeFacts"/>'s static initialization, which every query preparation
    /// reaches, so the declared requirement is part of the reachable program.
    /// </summary>
    internal static void Preserve() => _ = PublicMethods(typeof(decimal));

    private static MethodInfo[] PublicMethods(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
        => type.GetMethods();
}

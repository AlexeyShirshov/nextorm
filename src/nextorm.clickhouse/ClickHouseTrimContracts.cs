using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NextORM.ClickHouse;

/// <summary>
/// The trimming contract <c>ClickHouse.Driver</c> 1.5.0 needs before it can resolve a parameter type.
/// </summary>
/// <remarks>
/// <para>
/// <c>ClickHouse.Driver.Types.TypeConverter</c>'s static constructor registers the driver's CLR type
/// mappings. One registration constructs a <see cref="Tuple{T1,T2}"/> of <see cref="double"/> for the
/// native <c>Point</c> shape (<c>Tuple(Float64, Float64)</c>), and
/// <c>TupleType.BuildTupleFactory</c> resolves that constructor by reflection. The static constructor is
/// reached from <c>ParameterTypeResolution.ResolveTypeName</c>, so <b>every parameterized query</b>
/// touches it. In a trimmed or native image the constructor is removed (the driver's reflection over it
/// is not annotated), the static constructor throws, and the failure is cached by the runtime: every
/// later parameterized query fails with an opaque <c>TypeInitializationException</c>.
/// </para>
/// <para>
/// Passing the literal closed type through a parameter annotated with
/// <see cref="DynamicallyAccessedMembersAttribute"/> states the requirement to the trimmer, exactly like
/// the decimal-operator contract in <c>NextORM.Core.Visitors.NumericOperators</c>. This is the
/// annotation mechanism, not a suppression and not a rooting switch: no diagnostic is silenced, no
/// assembly or member is rooted wholesale, and nothing about the consumer's configuration changes. The
/// discarded <c>_ =</c> result is intentional — the trimmer reads the annotation at the call site.
/// </para>
/// <para>
/// Scope: the fixed <c>Tuple&lt;double, double&gt;</c> registration only. A tuple-shaped <em>result
/// column</em> of any other element types is constructed by the driver per row and stays unsupported in
/// a native image; <c>ClickHouseDataContext.MapColumnExpression</c> refuses it up front with an
/// actionable message (see <c>docs/specs/design/D370-stream-a-clickhouse-tuples.md</c>).
/// </para>
/// </remarks>
internal static class ClickHouseTrimContracts
{
    /// <summary>Declares the driver's parameter-type-resolution reflection dependency.</summary>
    internal static void PreserveParameterTupleRegistration() => _ = PublicConstructors(typeof(Tuple<double, double>));

    private static ConstructorInfo[] PublicConstructors(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
        => type.GetConstructors();
}

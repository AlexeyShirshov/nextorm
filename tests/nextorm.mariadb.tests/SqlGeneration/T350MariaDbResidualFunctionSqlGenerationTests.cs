using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// One T350 residual name as a SQL-generation case: the canonical inventory identity, the MariaDB fragment
/// the production path must emit, and the behaviour the row must produce — a rendered fragment for a
/// delivered name, or the exact pre-SQL refusal text for a name the shared surface cannot express.
/// </summary>
/// <param name="Id">Stable inventory id (the row id of <c>.pdca/T350/rv1/inventory.md</c>).</param>
/// <param name="Name">Canonical vendor name.</param>
/// <param name="Family">Inventory family.</param>
/// <param name="ExpectedSqlFragment">The exact fragment the rendered SQL must contain.</param>
/// <param name="Behaviour"><c>delivered</c>, or <c>refusal: &lt;exact reason&gt;</c>.</param>
internal sealed record T350SqlCase(
    string Id,
    string Name,
    string Family,
    string ExpectedSqlFragment,
    string Behaviour);

/// <summary>
/// T350 (issue #284) residual MariaDB names, SQL-generation side. The table is the single source of the
/// delivered/refused contract: one row per name and one fact that iterates it, so a name cannot be
/// delivered without a pinned fragment or refused without its exact reason text.
/// <para>
/// S2 scaffolding block: the table is deliberately empty — this block adds no surface member, so there is
/// no fragment to pin yet. Each later block appends the rows it delivers or refuses and the fact below
/// then covers them without a second code path. These tests never open a connection; the live counterpart
/// is <c>T350MariaDbFunctionLiveMatrixTests</c> in the integration project.
/// </para>
/// </summary>
public class T350MariaDbResidualFunctionSqlGenerationTests
{
    private static readonly T350SqlCase[] Cases =
    [
    ];

    [Fact]
    public void EveryResidualRow_ShouldCarryEitherItsFragmentOrItsRefusal()
    {
        Cases.Select(@case => @case.Id).Should().OnlyHaveUniqueItems("one row per inventory id");
        Cases.Select(@case => @case.Name).Should().OnlyHaveUniqueItems("one row per canonical name");

        foreach (var @case in Cases)
        {
            @case.Family.Should().NotBeNullOrWhiteSpace($"{@case.Id} must name its inventory family");

            if (@case.Behaviour == "delivered")
            {
                @case.ExpectedSqlFragment.Should().NotBeNullOrWhiteSpace(
                    $"{@case.Id}/{@case.Name} is delivered, so its rendered fragment must be pinned verbatim");
            }
            else
            {
                @case.Behaviour.Should().StartWith(
                    "refusal: ",
                    $"{@case.Id}/{@case.Name} must be either delivered or refused with its exact reason text");
                @case.Behaviour["refusal: ".Length..].Should().NotBeNullOrWhiteSpace(
                    $"{@case.Id}/{@case.Name} must carry the refusal reason the capability gate throws");
            }
        }
    }
}

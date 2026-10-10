using System.Text;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// D160 reconnaissance probes (issue #160, cycle N=2). Test-only: they characterise the baseline
/// positional join seam on HEAD <c>0495ee84</c> without touching product code.
///
/// <para>
/// The R160-02' oracle is the observation that a positional <c>Join</c> deliberately omits
/// <see cref="EntityBuilder{TEntity}.SourceEntityType"/> and
/// <see cref="EntityBuilder{TEntity}.BindArrayJoinElement"/> when it copies state onto the joined
/// builder, while <c>Clone</c>/<c>CopyTo</c> preserve both. These probes prove that oracle is
/// obtainable on the baseline with <c>InternalsVisibleTo("nextorm.core.tests")</c>.
/// </para>
/// </summary>
public class JoinAliasMixingSeamTests
{
    private const string DefaultEvidenceRoot = "artifacts/pdca/D160/rv1/N2/revised";
    // Committed fixture (tracked): the frozen positional expression corpus captured at HEAD 0495ee84.
    // It must NOT live under artifacts/ (gitignored) — a CI checkout only has tracked files.
    private const string DefaultBaselineRoot = "tests/nextorm.core.tests/Baselines/D160/positional";

    private static readonly string[] Cases =
    [
        "01-inner", "02-left", "03-right", "04-full", "05-cross",
        "06-three-join-chain", "07-where-between-joins", "08-positional-prefix-as",
    ];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "nextorm.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static string ResolveDir(string? envValue, string defaultRelative)
    {
        var value = string.IsNullOrWhiteSpace(envValue) ? defaultRelative : envValue!;
        return Path.IsPathRooted(value) ? value : Path.Combine(RepoRoot(), value);
    }

    private static string EvidenceRoot()
        => ResolveDir(Environment.GetEnvironmentVariable("D160_EVIDENCE_DIR"), DefaultEvidenceRoot);

    private static string BaselineRoot()
        => ResolveDir(Environment.GetEnvironmentVariable("D160_BASELINE_DIR"), DefaultBaselineRoot);

    // -----------------------------------------------------------------------------------------------
    // P1 — the primary oracle: positional Join drops SourceEntityType/BindArrayJoinElement; Clone keeps.
    // -----------------------------------------------------------------------------------------------
    [Trait("D160Probe", "P1")]
    [Fact]
    public void Positional_join_drops_source_entity_type_and_bind_flag_but_clone_preserves()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.From<SimpleEntity>();

        // (1) set the internal array-join source seam on the plain From<X>() builder.
        source.SourceEntityType = typeof(SimpleEntity);
        source.BindArrayJoinElement = true;

        // (1b) prove the setup took.
        source.SourceEntityType.Should().Be(typeof(SimpleEntity));
        source.BindArrayJoinElement.Should().BeTrue();

        // (2) call the public positional Join.
        var joined = source.Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id);

        // (3) the joined builder must NOT inherit either seam (see ApplyJoinStateTo, which omits them).
        joined.SourceEntityType.Should().BeNull();
        joined.BindArrayJoinElement.Should().BeFalse();

        // (4) control arm: Clone/CopyTo preserves both.
        var clone = source.Clone();
        clone.SourceEntityType.Should().Be(typeof(SimpleEntity));
        clone.BindArrayJoinElement.Should().BeTrue();

        // and the copy is independent of the joined builder.
        joined.SourceEntityType.Should().BeNull();
        joined.BindArrayJoinElement.Should().BeFalse();
    }

    // -----------------------------------------------------------------------------------------------
    // P2 — expression identity of a frozen positional-only corpus (core / in-memory surface).
    // -----------------------------------------------------------------------------------------------
    [Trait("D160Probe", "P2")]
    [Fact]
    public void Positional_chain_expression_identity_is_frozen_and_deterministic()
    {
        using var ctx = new InMemoryDataContext();
        var dir = Path.Combine(EvidenceRoot(), "positional");
        Directory.CreateDirectory(dir);

        var simple = ctx.From<SimpleEntity>();
        var other = ctx.From<ConventionalEntity>();
        var other2 = ctx.From<ConventionalEntity>();

        Capture(dir, "01-inner", simple.Join(other, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        Capture(dir, "02-left", simple.LeftJoin(other, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        Capture(dir, "03-right", simple.RightJoin(other, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        Capture(dir, "04-full", simple.FullJoin(other, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        Capture(dir, "05-cross", simple.CrossJoin(other).Select(p => p.Item2.Id));
        Capture(dir, "06-three-join-chain", simple
            .Join(other, (a, b) => a.Id == b.Id)
            .Join(other2, (p, c) => p.Item1.Id == c.Id)
            .Select(p => p.Item2.Id));
        Capture(dir, "07-where-between-joins", simple
            .Where(a => a.Id > 0)
            .Join(other, (a, b) => a.Id == b.Id)
            .Select(p => p.Item2.Id));
        Capture(dir, "08-positional-prefix-as", simple
            .Join(other, (a, b) => a.Id == b.Id)
            .As(p => new { p.Item1.Id, p.Item2.Name })
            .Select(x => x.Id));

        foreach (var name in Cases)
        {
            var file = Path.Combine(dir, name + ".txt");
            File.Exists(file).Should().BeTrue();
            new FileInfo(file).Length.Should().BeGreaterThan(0);
            CompareToBaseline(BaselineRoot(), dir, name);
        }
    }

    /// <summary>
    /// Reads the frozen baseline expression <c>&lt;name&gt;.txt</c> and asserts the freshly generated
    /// revised bytes are identical. Any length or byte difference fails the test.
    /// </summary>
    private static void CompareToBaseline(string baselineDir, string revisedDir, string name)
    {
        var baselinePath = Path.Combine(baselineDir, name + ".txt");
        var revisedPath = Path.Combine(revisedDir, name + ".txt");

        File.Exists(baselinePath)
            .Should().BeTrue($"the frozen baseline positional expression corpus must exist at '{baselinePath}'");

        var baselineBytes = File.ReadAllBytes(baselinePath);
        var revisedBytes = File.ReadAllBytes(revisedPath);
        revisedBytes.Should().Equal(baselineBytes, $"{name}: positional expression must be byte-identical to the baseline");
    }

    private static void Capture(string dir, string name, QueryCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("case=").Append(name).Append('\n');
        sb.Append("projection=").Append(command.ProjectionExpression?.ToString() ?? "<none>").Append('\n');
        sb.Append("condition=").Append(command.Condition?.ToString() ?? "<none>").Append('\n');
        var joins = command.Joins;
        sb.Append("join_count=").Append(joins?.Length ?? 0).Append('\n');
        if (joins is not null)
        {
            for (var i = 0; i < joins.Length; i++)
            {
                sb.Append("join[").Append(i).Append("]=")
                    .Append(joins[i].JoinType)
                    .Append(";cond=").Append(joins[i].JoinCondition?.ToString() ?? "<none>")
                    .Append('\n');
            }
        }

        File.WriteAllBytes(Path.Combine(dir, name + ".txt"), Encoding.UTF8.GetBytes(sb.ToString()));
    }

    // -----------------------------------------------------------------------------------------------
    // P3 — positional Join returns a core JoinedEntityBuilder and does NOT throw the alias refusal.
    // -----------------------------------------------------------------------------------------------
    [Trait("D160Probe", "P3")]
    [Fact]
    public void Positional_join_returns_core_joined_builder_and_does_not_alias_refuse()
    {
        using var ctx = new InMemoryDataContext();
        var source = ctx.From<SimpleEntity>();

        JoinedEntityBuilder<SimpleEntity, ConventionalEntity> joined = null!;
        var ex = Record.Exception(
            () => joined = source.Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id));

        ex.Should().BeNull("a positional Join on a non-projection source must not be refused");
        joined.Should().NotBeNull();
        joined.Should().BeOfType<JoinedEntityBuilder<SimpleEntity, ConventionalEntity>>();
        joined.GetType().Namespace.Should().Be("NextORM.Core");
    }
}

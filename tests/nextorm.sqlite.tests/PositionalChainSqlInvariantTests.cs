using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D160 R160-02' oracle (issue #160, cycle N=2). Test-only: it regenerates the frozen positional-only
/// join chain on the current (revised) tree and compares the generated SQL <b>byte-for-byte</b> against
/// the committed baseline corpus captured at HEAD <c>0495ee84</c> under
/// <c>tests/nextorm.sqlite.tests/Baselines/D160/positional/sqlite</c>. It is a genuine baseline&lt;-&gt;revised
/// comparator, not a self-read: a single differing byte fails the test.
///
/// <para>
/// Inputs are env-driven: <c>D160_EVIDENCE_DIR</c> is the revised artifact root (default
/// <c>artifacts/pdca/D160/rv1/N2/revised</c>, so SQL lands in <c>&lt;root&gt;/positional/sqlite</c>) and
/// <c>D160_BASELINE_DIR</c> is the frozen baseline root (default
/// <c>tests/nextorm.sqlite.tests/Baselines/D160/positional</c>). Relative values resolve against the repo root.
/// </para>
/// </summary>
public class PositionalChainSqlInvariantTests
{
    private const string DefaultEvidenceRoot = "artifacts/pdca/D160/rv1/N2/revised";
    // Committed fixture (tracked): the frozen positional SQL corpus captured at HEAD 0495ee84.
    // It must NOT live under artifacts/ (gitignored) — a CI checkout only has tracked files.
    private const string DefaultBaselineRoot = "tests/nextorm.sqlite.tests/Baselines/D160/positional";

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

    /// <summary>
    /// Raw, un-normalised <c>CommandText</c>: the corpus is compared byte-for-byte, so aliases and
    /// ordinals must survive exactly as the renderer produced them.
    /// </summary>
    private static string RawSqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    private static void Capture(string dir, string name, string sql)
    {
        Directory.CreateDirectory(dir);
        var bytes = Encoding.UTF8.GetBytes(sql);
        File.WriteAllBytes(Path.Combine(dir, name + ".sql"), bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        File.WriteAllBytes(
            Path.Combine(dir, name + ".sha256"),
            Encoding.UTF8.GetBytes(hash + "  " + name + ".sql" + "\n"));
    }

    /// <summary>
    /// Reads the frozen baseline <c>&lt;name&gt;.sql</c> and asserts the freshly generated revised bytes
    /// are identical (SQL and its recorded SHA-256 sidecar). Any length or byte difference fails.
    /// </summary>
    private static void CompareToBaseline(string baselineDir, string revisedDir, string name)
    {
        var baselineSqlPath = Path.Combine(baselineDir, name + ".sql");
        var revisedSqlPath = Path.Combine(revisedDir, name + ".sql");

        File.Exists(baselineSqlPath)
            .Should().BeTrue($"the frozen baseline positional corpus must exist at '{baselineSqlPath}'");
        File.Exists(revisedSqlPath)
            .Should().BeTrue($"the revised positional corpus must have been regenerated at '{revisedSqlPath}'");

        var baselineBytes = File.ReadAllBytes(baselineSqlPath);
        var revisedBytes = File.ReadAllBytes(revisedSqlPath);
        revisedBytes.Should().Equal(baselineBytes, $"{name}: positional SQL must be byte-identical to the baseline");

        var baselineHash = File.ReadAllBytes(Path.Combine(baselineDir, name + ".sha256"));
        var revisedHash = File.ReadAllBytes(Path.Combine(revisedDir, name + ".sha256"));
        revisedHash.Should().Equal(baselineHash, $"{name}: positional SQL SHA-256 sidecar must match the baseline");
    }

    // -----------------------------------------------------------------------------------------------
    // P2 — regenerate the frozen positional corpus and byte-compare it against the baseline.
    // -----------------------------------------------------------------------------------------------
    [Trait("D160Probe", "P2")]
    [Fact]
    public void Positional_chain_sql_is_byte_identical_to_the_frozen_baseline()
    {
        using var ctx = SqliteTestContext.Create();
        var dir = Path.Combine(EvidenceRoot(), "positional", "sqlite");
        var baselineDir = Path.Combine(BaselineRoot(), "sqlite");

        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();
        var complex2 = ctx.From<IComplexEntity>();

        var inner = RawSqlOf(ctx, simple.Join(complex, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        var left = RawSqlOf(ctx, simple.LeftJoin(complex, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        var right = RawSqlOf(ctx, simple.RightJoin(complex, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        var full = RawSqlOf(ctx, simple.FullJoin(complex, (a, b) => a.Id == b.Id).Select(p => p.Item2.Id));
        var cross = RawSqlOf(ctx, simple.CrossJoin(complex).Select(p => p.Item2.Id));
        var chain = RawSqlOf(ctx, simple
            .Join(complex, (a, b) => a.Id == b.Id)
            .Join(complex2, (p, c) => p.Item1.Id == c.Id)
            .Select(p => p.Item2.Id));
        var whereBetween = RawSqlOf(ctx, simple
            .Where(a => a.Id > 0)
            .Join(complex, (a, b) => a.Id == b.Id)
            .Select(p => p.Item2.Id));
        var prefixAs = RawSqlOf(ctx, simple
            .Join(complex, (a, b) => a.Id == b.Id)
            .As(p => new { p.Item1.Id, p.Item2.String })
            .Select(x => x.Id));

        Capture(dir, "01-inner", inner);
        Capture(dir, "02-left", left);
        Capture(dir, "03-right", right);
        Capture(dir, "04-full", full);
        Capture(dir, "05-cross", cross);
        Capture(dir, "06-three-join-chain", chain);
        Capture(dir, "07-where-between-joins", whereBetween);
        Capture(dir, "08-positional-prefix-as", prefixAs);

        foreach (var name in Cases)
            CompareToBaseline(baselineDir, dir, name);
    }

    // -----------------------------------------------------------------------------------------------
    // P3 — positional CROSS/OUTER APPLY fail closed on SQLite (no lateral source).
    // -----------------------------------------------------------------------------------------------
    [Trait("D160Probe", "P3")]
    [Fact]
    public void Positional_cross_apply_fails_closed_on_sqlite()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => RawSqlOf(ctx, simple.CrossApply(complex).Select(p => p.Item2.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply*");
    }

    [Trait("D160Probe", "P3")]
    [Fact]
    public void Positional_outer_apply_fails_closed_on_sqlite()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => RawSqlOf(ctx, simple.OuterApply(complex).Select(p => p.Item2.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*OuterApply*");
    }
}

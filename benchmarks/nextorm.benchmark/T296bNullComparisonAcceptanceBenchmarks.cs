using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T296b (#296) focused, measurement-only performance harness for the approved total nullable
/// comparison contract. It exposes no production API: the seven scenarios below are built from the
/// ordinary public query surface and executed against a private file-backed SQLite database that
/// this class seeds itself with fixed, deterministic data (non-null comparison operands so the
/// baseline and the candidate observe identical result sets).
///
/// <para>
/// Each scenario runs in two modes. <b>Cached</b> rebuilds the query command and prepares it through
/// the plan cache (<c>storeInCache: true</c>), so a measured operation is "construct + plan lookup +
/// parameter refresh + execute". <b>Non-cached</b> does the same with <c>storeInCache: false</c> and
/// therefore re-renders the SQL on every operation — the plan's "prepared/non-cached" side. Neither
/// mode touches the sticky <c>QueryCommand.Cache</c> flag.
/// </para>
///
/// <para>
/// The constructor verifies the expected cardinality of every scenario against the seed rules
/// before any measurement, so a wrong result fails the run instead of producing a number. When
/// <c>NEXTORM_T296B_PERF_DUMP</c> names a file, the generated SQL of every scenario is appended
/// there (diagnostics only; never on the measured path) for the <c>EXPLAIN QUERY PLAN</c> capture.
/// </para>
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T296bNullComparisonAcceptanceBenchmarks
{
    private const int Iterations = 50;
    private const int RowCount = 2000;
    private const int ParentCount = 200;

    // Captured operands. A static field keeps the expression-tree root constant, exactly like a
    // closure field; the value never changes, so cached and non-cached runs see the same inputs.
    private static readonly long CapturedId = 42;
    private static readonly int? CapturedInt = 42;
    private static readonly string? CapturedString = "s17";

    private readonly SqliteDataContext _ctx;

    public T296bNullComparisonAcceptanceBenchmarks()
    {
        var databasePath = Environment.GetEnvironmentVariable("NEXTORM_T296B_BENCH_DB");
        if (string.IsNullOrEmpty(databasePath))
            databasePath = Path.Combine(Path.GetTempPath(), "nextorm.t296b.perf.db");

        _ctx = new SqliteDataContext($"Data Source='{databasePath}'", new DataContextBuilder());
        _ctx.PurgeQueryCache();
        Seed();
        VerifyExpectedResults();
        DumpSql();
    }

    // -------------------------------------------------------------------------------------------
    // Scenario 1: plain non-nullable equality WHERE control.
    // -------------------------------------------------------------------------------------------

    /// <summary>Control: captured non-nullable <c>long</c> equality on the primary key.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long Control_Equality_Cached() => Run(NewControlQuery, cached: true, rows => rows.Sum(r => r.Id));

    /// <summary>Control, non-cached preparation.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long Control_Equality_NonCached() => Run(NewControlQuery, cached: false, rows => rows.Sum(r => r.Id));

    // -------------------------------------------------------------------------------------------
    // Scenario 2: captured nullable integer equality WHERE.
    // -------------------------------------------------------------------------------------------

    /// <summary>Captured <c>int?</c> equality, cached plan.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CapturedIntEquality_Cached() => Run(NewIntEqualityQuery, cached: true, rows => rows.Sum(r => r.Id));

    /// <summary>Captured <c>int?</c> equality, non-cached preparation.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CapturedIntEquality_NonCached() => Run(NewIntEqualityQuery, cached: false, rows => rows.Sum(r => r.Id));

    // -------------------------------------------------------------------------------------------
    // Scenario 3: captured nullable integer inequality WHERE (three-arm total form).
    // -------------------------------------------------------------------------------------------

    /// <summary>Captured <c>int?</c> inequality, cached plan.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CapturedIntInequality_Cached() => Run(NewIntInequalityQuery, cached: true, rows => rows.Sum(r => r.Id));

    /// <summary>Captured <c>int?</c> inequality, non-cached preparation.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CapturedIntInequality_NonCached() => Run(NewIntInequalityQuery, cached: false, rows => rows.Sum(r => r.Id));

    // -------------------------------------------------------------------------------------------
    // Scenario 4: captured nullable string equality WHERE.
    // -------------------------------------------------------------------------------------------

    /// <summary>Captured <c>string?</c> equality, cached plan.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CapturedStringEquality_Cached() => Run(NewStringEqualityQuery, cached: true, rows => rows.Sum(r => r.Id));

    /// <summary>Captured <c>string?</c> equality, non-cached preparation.</summary>
    /// <returns>Sum of the returned ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CapturedStringEquality_NonCached() => Run(NewStringEqualityQuery, cached: false, rows => rows.Sum(r => r.Id));

    // -------------------------------------------------------------------------------------------
    // Scenario 5: correlated captured nullable comparison inside EXISTS.
    // -------------------------------------------------------------------------------------------

    /// <summary>Correlated <c>Exists</c> over a captured <c>int?</c>, cached plan.</summary>
    /// <returns>Sum of the returned parent ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CorrelatedExists_Cached() => Run(NewCorrelatedQuery, cached: true, rows => rows.Sum(r => r.Id));

    /// <summary>Correlated <c>Exists</c> over a captured <c>int?</c>, non-cached preparation.</summary>
    /// <returns>Sum of the returned parent ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long CorrelatedExists_NonCached() => Run(NewCorrelatedQuery, cached: false, rows => rows.Sum(r => r.Id));

    // -------------------------------------------------------------------------------------------
    // Scenario 6: boolean SELECT projection of a captured nullable comparison.
    // -------------------------------------------------------------------------------------------

    /// <summary>Boolean <c>SELECT</c> projection, cached plan.</summary>
    /// <returns>Number of rows whose projected predicate is true.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long BooleanSelect_Cached() => Run(NewBooleanSelectQuery, cached: true, rows => rows.Count(r => r.Match));

    /// <summary>Boolean <c>SELECT</c> projection, non-cached preparation.</summary>
    /// <returns>Number of rows whose projected predicate is true.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long BooleanSelect_NonCached() => Run(NewBooleanSelectQuery, cached: false, rows => rows.Count(r => r.Match));

    // -------------------------------------------------------------------------------------------
    // Scenario 7: captured nullable comparison in JOIN ON.
    // -------------------------------------------------------------------------------------------

    /// <summary>Captured <c>int?</c> comparison in the ON clause, cached plan.</summary>
    /// <returns>Sum of the matched child ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long JoinOn_Cached() => Run(NewJoinQuery, cached: true, rows => rows.Sum(r => r.Child));

    /// <summary>Captured <c>int?</c> comparison in the ON clause, non-cached preparation.</summary>
    /// <returns>Sum of the matched child ids.</returns>
    [Benchmark]
    [BenchmarkCategory("t296b-focused")]
    public long JoinOn_NonCached() => Run(NewJoinQuery, cached: false, rows => rows.Sum(r => r.Child));

    // -------------------------------------------------------------------------------------------
    // Query builders and the measured loop.
    // -------------------------------------------------------------------------------------------

    private QueryCommand<T296bBenchIdRow> NewControlQuery()
        => _ctx.From<T296bBenchRow>()
            .Where(x => x.Id == CapturedId)
            .Select(x => new T296bBenchIdRow { Id = x.Id });

    private QueryCommand<T296bBenchIdRow> NewIntEqualityQuery()
        => _ctx.From<T296bBenchRow>()
            .Where(x => x.N == CapturedInt)
            .Select(x => new T296bBenchIdRow { Id = x.Id });

    private QueryCommand<T296bBenchIdRow> NewIntInequalityQuery()
        => _ctx.From<T296bBenchRow>()
            .Where(x => x.N != CapturedInt)
            .Select(x => new T296bBenchIdRow { Id = x.Id });

    private QueryCommand<T296bBenchIdRow> NewStringEqualityQuery()
        => _ctx.From<T296bBenchRow>()
            .Where(x => x.S == CapturedString)
            .Select(x => new T296bBenchIdRow { Id = x.Id });

    private QueryCommand<T296bBenchIdRow> NewCorrelatedQuery()
    {
        var parents = _ctx.From<T296bBenchParent>();
        var children = _ctx.From<T296bBenchChild>();
        return parents
            .Where(p => SqlFunctions.Sql.exists(children.Where(c => c.ParentId == p.Id && c.Amount == CapturedInt)))
            .Select(p => new T296bBenchIdRow { Id = p.Id });
    }

    private QueryCommand<T296bBenchMatchRow> NewBooleanSelectQuery()
        => _ctx.From<T296bBenchRow>()
            // The pre-T296b baseline cannot materialise a projection whose predicate is NULL, so the
            // boolean-SELECT case is measured over non-null rows only: identical inputs on both
            // revisions and identical result sets. Null-row materialisation (A23) is proven by the
            // live SQLite/SQL Server tests, not by this timing harness.
            .Where(x => x.N != null)
            .Select(x => new T296bBenchMatchRow { Id = x.Id, Match = x.N == CapturedInt });

    private QueryCommand<T296bBenchPairRow> NewJoinQuery()
    {
        var parents = _ctx.From<T296bBenchParent>();
        var children = _ctx.From<T296bBenchChild>();
        return parents
            .Join(children, (p, c) => p.Id == c.ParentId && c.Amount == CapturedInt)
            .Select(p => new T296bBenchPairRow { Parent = p.Item1.Id, Child = p.Item2.Id });
    }

    /// <summary>
    /// One measured operation set: rebuild the command, prepare it through the plan cache or without
    /// it, execute it against SQLite and reduce the rows to a checksum (used by BDN, never folded).
    /// </summary>
    private long Run<T>(Func<QueryCommand<T>> build, bool cached, Func<IEnumerable<T>, long> reduce)
    {
        long total = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var command = build();
            var prepared = _ctx.GetPreparedQueryCommand(
                command, createEnumerator: false, storeInCache: cached, CancellationToken.None);
            total += reduce(_ctx.ToList(prepared));
        }

        return total;
    }

    // -------------------------------------------------------------------------------------------
    // Data seeding and result verification.
    // -------------------------------------------------------------------------------------------

    private void Seed()
    {
        var builder = new StringBuilder();
        builder.AppendLine("drop table if exists t296b_bench_child;");
        builder.AppendLine("drop table if exists t296b_bench_parent;");
        builder.AppendLine("drop table if exists t296b_bench_row;");
        builder.AppendLine("create table t296b_bench_row (id integer not null primary key, n integer null, s text null);");
        builder.Append("insert into t296b_bench_row (id, n, s) values ");
        for (var i = 1; i <= RowCount; i++)
        {
            var n = i % 7 == 0 ? "null" : (i % 50).ToString();
            var s = i % 11 == 0 ? "null" : $"'s{i % 40}'";
            builder.Append(i == 1 ? string.Empty : ",").Append($"({i}, {n}, {s})");
        }

        builder.AppendLine(";");
        builder.AppendLine("create table t296b_bench_parent (id integer not null primary key);");
        builder.Append("insert into t296b_bench_parent (id) values ");
        for (var i = 1; i <= ParentCount; i++)
            builder.Append(i == 1 ? string.Empty : ",").Append($"({i})");
        builder.AppendLine(";");
        builder.AppendLine("create table t296b_bench_child (id integer not null primary key, parent_id integer not null, amount integer null);");
        builder.Append("insert into t296b_bench_child (id, parent_id, amount) values ");
        for (var i = 1; i <= RowCount; i++)
        {
            var amount = i % 9 == 0 ? "null" : (i % 50).ToString();
            builder.Append(i == 1 ? string.Empty : ",").Append($"({i}, {i % ParentCount + 1}, {amount})");
        }

        builder.AppendLine(";");
        builder.AppendLine("create index ix_t296b_bench_row_n on t296b_bench_row (n);");
        builder.AppendLine("create index ix_t296b_bench_row_s on t296b_bench_row (s);");
        builder.AppendLine("create index ix_t296b_bench_child_parent on t296b_bench_child (parent_id);");
        builder.AppendLine("create index ix_t296b_bench_child_amount on t296b_bench_child (amount);");

        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand(builder.ToString());
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Fails the harness when any scenario returns a cardinality that disagrees with the seed rules,
    /// so a measured run can never report a timing for a wrong result.
    /// </summary>
    private void VerifyExpectedResults()
    {
        var expectedIntMatches = Enumerable.Range(1, RowCount).Count(i => i % 7 != 0 && i % 50 == 42);
        var expectedStringMatches = Enumerable.Range(1, RowCount).Count(i => i % 11 != 0 && i % 40 == 17);
        // Pre-T296b (52297fa8) inequality was a bare `<>`: NULL database rows never matched. The
        // total contract includes them. The harness therefore carries the baseline expectation only
        // when the baseline-export run asks for it (NEXTORM_T296B_PERF_BASELINE=1); the candidate
        // default is the approved total cardinality, so a wrong result still fails the run.
        var baselineSemantics = Environment.GetEnvironmentVariable("NEXTORM_T296B_PERF_BASELINE") == "1";
        var expectedIntNonMatches = baselineSemantics
            ? Enumerable.Range(1, RowCount).Count(i => i % 7 != 0 && i % 50 != 42)
            : RowCount - expectedIntMatches;
        var expectedCorrelatedParents = Enumerable.Range(1, RowCount)
            .Where(i => i % 9 != 0 && i % 50 == 42)
            .Select(i => i % ParentCount + 1)
            .Distinct()
            .Count();
        var expectedJoinMatches = Enumerable.Range(1, RowCount).Count(i => i % 9 != 0 && i % 50 == 42);

        AssertResult("control", 1, Execute(NewControlQuery, cached: true, rows => rows.Count()));
        AssertResult("int equality", expectedIntMatches, Execute(NewIntEqualityQuery, cached: true, rows => rows.Count()));
        AssertResult("int inequality", expectedIntNonMatches, Execute(NewIntInequalityQuery, cached: true, rows => rows.Count()));
        AssertResult("string equality", expectedStringMatches, Execute(NewStringEqualityQuery, cached: true, rows => rows.Count()));
        AssertResult("correlated exists", expectedCorrelatedParents, Execute(NewCorrelatedQuery, cached: true, rows => rows.Count()));
        AssertResult("boolean select", expectedIntMatches, Execute(NewBooleanSelectQuery, cached: true, rows => rows.Count(r => r.Match)));
        AssertResult("join on", expectedJoinMatches, Execute(NewJoinQuery, cached: true, rows => rows.Count()));

        // Non-cached preparation must observe the identical rows; it re-renders the SQL each time.
        AssertResult("control (non-cached)", 1, Execute(NewControlQuery, cached: false, rows => rows.Count()));
        AssertResult("int equality (non-cached)", expectedIntMatches, Execute(NewIntEqualityQuery, cached: false, rows => rows.Count()));
        AssertResult("int inequality (non-cached)", expectedIntNonMatches, Execute(NewIntInequalityQuery, cached: false, rows => rows.Count()));
        AssertResult("string equality (non-cached)", expectedStringMatches, Execute(NewStringEqualityQuery, cached: false, rows => rows.Count()));
        AssertResult("correlated exists (non-cached)", expectedCorrelatedParents, Execute(NewCorrelatedQuery, cached: false, rows => rows.Count()));
        AssertResult("boolean select (non-cached)", expectedIntMatches, Execute(NewBooleanSelectQuery, cached: false, rows => rows.Count(r => r.Match)));
        AssertResult("join on (non-cached)", expectedJoinMatches, Execute(NewJoinQuery, cached: false, rows => rows.Count()));
    }

    private int Execute<T>(Func<QueryCommand<T>> build, bool cached, Func<IEnumerable<T>, int> count)
    {
        var command = build();
        var prepared = _ctx.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: cached, CancellationToken.None);
        return count(_ctx.ToList(prepared));
    }

    private static void AssertResult(string scenario, int expected, int actual)
    {
        if (expected != actual)
            throw new InvalidOperationException(
                $"T296b benchmark harness: scenario '{scenario}' returned {actual} rows, expected {expected}.");
    }

    /// <summary>
    /// Writes the generated SQL of every scenario to <c>NEXTORM_T296B_PERF_DUMP</c> when set; the
    /// EXPLAIN QUERY PLAN diagnostic is produced from this text against the same seeded database.
    /// </summary>
    private void DumpSql()
    {
        var path = Environment.GetEnvironmentVariable("NEXTORM_T296B_PERF_DUMP");
        if (string.IsNullOrEmpty(path))
            return;

        var builder = new StringBuilder();
        Dump(builder, "control", NewControlQuery());
        Dump(builder, "int-equality", NewIntEqualityQuery());
        Dump(builder, "int-inequality", NewIntInequalityQuery());
        Dump(builder, "string-equality", NewStringEqualityQuery());
        Dump(builder, "correlated-exists", NewCorrelatedQuery());
        Dump(builder, "boolean-select", NewBooleanSelectQuery());
        Dump(builder, "join-on", NewJoinQuery());
        File.AppendAllText(path, builder.ToString());
    }

    private void Dump<T>(StringBuilder builder, string scenario, QueryCommand<T> query)
    {
        var prepared = (DbPreparedQueryCommand<T>)_ctx.GetPreparedQueryCommand(
            query, createEnumerator: false, storeInCache: false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText.Replace('\r', ' ').Replace('\n', ' ');
        builder.Append("case=").Append(scenario).Append(" sql=").Append(sql).Append('\n');
    }
}

/// <summary>Benchmark fixture row with nullable integer and string columns.</summary>
[SqlTable("t296b_bench_row")]
public interface T296bBenchRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("n")]
    int? N { get; set; }

    [Column("s")]
    string? S { get; set; }
}

/// <summary>Benchmark fixture parent row.</summary>
[SqlTable("t296b_bench_parent")]
public interface T296bBenchParent
{
    [Key]
    [Column("id")]
    long Id { get; set; }
}

/// <summary>Benchmark fixture child row with a nullable amount.</summary>
[SqlTable("t296b_bench_child")]
public interface T296bBenchChild
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("parent_id")]
    long ParentId { get; set; }

    [Column("amount")]
    int? Amount { get; set; }
}

/// <summary>Id-only projection row.</summary>
public sealed class T296bBenchIdRow
{
    public long Id { get; set; }
}

/// <summary>Boolean projection row.</summary>
public sealed class T296bBenchMatchRow
{
    public long Id { get; set; }

    public bool Match { get; set; }
}

/// <summary>Join pair projection row.</summary>
public sealed class T296bBenchPairRow
{
    public long Parent { get; set; }

    public long Child { get; set; }
}

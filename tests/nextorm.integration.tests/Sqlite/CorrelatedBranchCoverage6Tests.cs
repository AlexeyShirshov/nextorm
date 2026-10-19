using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Correlated-visitor and aggregate-terminal branch coverage (cycle 6, cluster D5) executed against a
/// live SQLite database. Every fact drives a real correlated query over class-owned marker rows of
/// <c>co6_parent</c> / <c>co6_child</c> / <c>co6_note</c> (dropped, created and seeded by this class's
/// own DDL in the constructor) and asserts returned values, including the childless parent by value.
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>AggregateTerminalRewriter.AggregateMethodFor</c>
/// (:56) and <c>CorrelatedQueryExpressionVisitor.IsAggregateTerminal</c> (:414) for the four variance
/// terminals <c>Stdev</c>/<c>Stdevp</c>/<c>Var</c>/<c>Varp</c>, which no earlier cycle drove inside a
/// correlated subquery; <c>VisitMethodCall</c> (:169) for the non-aggregate builder terminals
/// (<c>Any</c>/<c>FirstOrDefault</c>) that must NOT be rewritten as aggregates, for the deeper
/// correlation marker (a note subquery referencing the grandparent parameter) and for the trace-level
/// cache-miss log (:316, :364). <c>VisitBinary</c> (:1684) is probed with the remaining correlated
/// binary shapes (arithmetic, nested AndAlso/OrElse, three-valued nullable equality, string
/// concatenation and captured locals of every numeric/string/null shape).
/// </para>
///
/// <para>
/// Overlap avoided: <c>CorrelatedTerminalBranchCoverage5Tests</c> owns the collection terminals and
/// Count property, nullable/non-nullable and null-polarity comparisons, AndAlso/OrElse, captured
/// locals, the A-&gt;A-&gt;B-&gt;A value loop and typed Min/Max/Sum/Avg terminals;
/// <c>CorrelatedBranchCoverage2Tests</c> owns the Debug-level cache-miss log and the non-correlated
/// <c>Any()</c> probe. No reference-navigation hop appears in this class: #304 stays a blocker.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class CorrelatedBranchCoverage6Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists co6_note;
        drop table if exists co6_child;
        drop table if exists co6_parent;
        create table co6_parent
        (
            id integer primary key,
            name text not null,
            bucket int null,
            qty int not null
        );
        create table co6_child
        (
            id integer primary key,
            parent_id int not null,
            amount int null,
            weight int not null,
            tag text null
        );
        create table co6_note
        (
            id integer primary key,
            parent_id int not null,
            body text not null
        );
        -- 6001: two children and two notes; 6002: one child with NULL amount/tag and no note;
        -- 6003: no child at all but one note; 6004: one child and one note.
        insert into co6_parent (id, name, bucket, qty) values
            (6001, 'alpha', 1, 10),
            (6002, 'beta', null, 20),
            (6003, 'gamma', 2, 30),
            (6004, 'delta', 1, 5);
        insert into co6_child (id, parent_id, amount, weight, tag) values
            (6101, 6001, 5, 3, 'aa'),
            (6102, 6001, 7, 4, 'ab'),
            (6103, 6002, null, 6, null),
            (6104, 6004, 9, 9, 'd');
        insert into co6_note (id, parent_id, body) values
            (6201, 6001, 'n1'),
            (6202, 6001, 'n2'),
            (6203, 6003, 'n3'),
            (6204, 6004, 'n4');
        """;

    public CorrelatedBranchCoverage6Tests()
    {
        ExecuteRaw(Ddl);

        // The collection navigations must be registered before any mapping of the parent type, so the
        // members are recognized as navigations and not as physical columns.
        Context.From<Co6Note>();
        Context.From<Co6Child>();
        Context.From<Co6Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
    }

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------------------------------------
    // 1. AggregateTerminalRewriter: the sample-variance terminals (Stdev, Var) in a projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>AggregateMethodFor</c> (:56) takes its <c>Stdev</c> and <c>Var</c> arms
    /// (<c>IsAggregateTerminal</c> :414 returns <c>true</c> for both) inside a PROJECTION. The exact
    /// values pin the sample flavour: the two weights 3/4 of 6001 give variance 0.5, the single-child
    /// parents cannot produce a sample variance and the childless 6003 has an empty set (NULL).
    /// </summary>
    [Fact]
    public void VarianceSampleAggregatesInCorrelatedProjection_ShouldReturnExactValues()
    {
        var rows = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                StdevWeight = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Stdev(c => (double?)c.Weight),
                VarWeight = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Var(c => (double?)c.Weight),
            })
            .ToList();

        rows.Should().HaveCount(4);

        rows[0].Id.Should().Be(6001);
        rows[0].StdevWeight.Should().BeApproximately(0.7071067811865476, 1e-12, "sqrt(0.5) over the weights 3 and 4");
        rows[0].VarWeight.Should().BeApproximately(0.5, 1e-12, "((3-3.5)^2 + (4-3.5)^2) / 1");

        rows[1].Id.Should().Be(6002);
        rows[1].StdevWeight.Should().BeNull("a one-row sample has no sample variance");
        rows[1].VarWeight.Should().BeNull();

        rows[2].Id.Should().Be(6003);
        rows[2].StdevWeight.Should().BeNull("the childless parent has an empty set");
        rows[2].VarWeight.Should().BeNull();

        rows[3].Id.Should().Be(6004);
        rows[3].StdevWeight.Should().BeNull("a one-row sample has no sample variance");
        rows[3].VarWeight.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------------
    // 2. AggregateTerminalRewriter: the population-variance terminals (Stdevp, Varp) in a projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>AggregateMethodFor</c> (:56) takes its <c>Stdevp</c> and <c>Varp</c> arms
    /// inside a PROJECTION. The population flavour is defined for one row (0), so the single-child
    /// parents differ from the childless 6003 (NULL) by value.
    /// </summary>
    [Fact]
    public void VariancePopulationAggregatesInCorrelatedProjection_ShouldReturnExactValues()
    {
        var rows = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                StdevpWeight = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Stdevp(c => (double?)c.Weight),
                VarpWeight = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Varp(c => (double?)c.Weight),
            })
            .ToList();

        rows.Should().HaveCount(4);

        rows[0].StdevpWeight.Should().BeApproximately(0.5, 1e-12, "sqrt(0.25) over the weights 3 and 4");
        rows[0].VarpWeight.Should().BeApproximately(0.25, 1e-12);

        rows[1].StdevpWeight.Should().BeApproximately(0.0, 1e-12, "a population variance is defined for one row");
        rows[1].VarpWeight.Should().BeApproximately(0.0, 1e-12);

        rows[2].StdevpWeight.Should().BeNull("the childless parent has an empty set");
        rows[2].VarpWeight.Should().BeNull();

        rows[3].StdevpWeight.Should().BeApproximately(0.0, 1e-12);
        rows[3].VarpWeight.Should().BeApproximately(0.0, 1e-12);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. The variance arms over a NULLABLE selector type
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the nullable selector type reaches the same four rewriter arms, so the
    /// aggregate method is closed over <c>double?</c> and the SQL aggregate ignores the NULL amount
    /// of 6002 (all-NULL set) instead of counting it - the value matrix separates the all-NULL
    /// set (NULL) from the one-value set (NULL for the sample flavour, 0 for the population one).
    /// </summary>
    [Fact]
    public void VarianceAggregatesOverNullableColumn_ShouldIgnoreNullAmounts()
    {
        var rows = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                StdevAmount = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Stdev(c => (double?)c.Amount),
                VarpAmount = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Varp(c => (double?)c.Amount),
            })
            .ToList();

        rows.Should().HaveCount(4);

        rows[0].StdevAmount.Should().BeApproximately(1.4142135623730951, 1e-12, "sqrt(2) over 5 and 7");
        rows[0].VarpAmount.Should().BeApproximately(1.0, 1e-12);

        rows[1].StdevAmount.Should().BeNull("the only amount of 6002 is NULL, so the set is empty for the aggregate");
        rows[1].VarpAmount.Should().BeNull();

        rows[2].StdevAmount.Should().BeNull();
        rows[2].VarpAmount.Should().BeNull();

        rows[3].StdevAmount.Should().BeNull("a one-row sample has no sample variance");
        rows[3].VarpAmount.Should().BeApproximately(0.0, 1e-12, "the single amount 9 forms a one-row population");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Non-aggregate builder terminals must not be rewritten as aggregates
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>IsAggregateTerminal</c> (:414) returns <c>false</c> for <c>Any</c> and
    /// <c>FirstOrDefault</c>, so <c>VisitMethodCall</c> takes the non-aggregate builder-terminal branch
    /// (:299) - once with a receiver that references the outer parameter and once with a receiver that
    /// does not (the cycle-5 class only drove collection terminals) - and the scalar-subquery branch
    /// (:335) for the projected <c>Select(...).FirstOrDefault()</c>.
    /// </summary>
    [Fact]
    public void NonAggregateBuilderTerminalsInProjection_ShouldReturnTheRightRows()
    {
        var rows = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                HasChild = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Any(),
                AnyWide = Context.From<Co6Child>().Where(c => c.Weight > 0).Any(),
                FirstTag = Context.From<Co6Child>().Where(c => c.ParentId == p.Id).Select(c => c.Tag).FirstOrDefault(),
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(6001, 6002, 6003, 6004);
        rows.Select(r => r.HasChild).Should().Equal(true, true, false, true);
        rows.Should().OnlyContain(r => r.AnyWide, "every child row has a positive weight, so the non-correlated subquery is true");
        rows.Select(r => r.FirstTag).Should().Equal(new string?[] { "aa", null, null, "d" });
    }

    // ---------------------------------------------------------------------------------------------
    // 5. A deeper correlation marker inside the projected scalar subquery
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the note subquery references both its immediate outer child (<c>c.Id</c>)
    /// and the grandparent parent (<c>p.Id</c>), so the second-level visit sees an outer-reference marker
    /// and takes the marker arm of <c>VisitMethodCall</c> (:335). The value matrix proves the note
    /// condition is evaluated per child (6002 has a child but no note, so it projects the default).
    /// </summary>
    [Fact]
    public void CorrelatedScalarSubquery_WithGrandparentMarkerInNestedExists_ShouldEvaluatePerChild()
    {
        var rows = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                FirstWeight = Context.From<Co6Child>()
                    .Where(c => c.ParentId == p.Id
                        && SqlFunctions.Sql.exists(
                            Context.From<Co6Note>().Where(n => n.ParentId == p.Id && n.Id > c.Id)))
                    .Select(c => (int?)c.Weight)
                    .FirstOrDefault(),
            })
            .ToList();

        rows.Should().HaveCount(4);
        rows.Select(r => r.FirstWeight).Should().Equal(new int?[] { 3, null, null, 9 },
            "6002 has a child but no note, 6003 has no child at all, and 6004's note exists");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. VisitBinary: arithmetic over correlated scalars and a nested logical tree
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the correlated predicate combines an arithmetic tree over an inner and an
    /// outer scalar (<c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>) and a nested <c>AndAlso</c>/<c>OrElse</c>
    /// tree with four correlated comparisons, so <c>VisitBinary</c> (:1684) rebuilds the logical
    /// operands and leaves the arithmetic to the base visit. The exact rows separate the NULL amount
    /// (nothing matches) from the arithmetic that only 6001 satisfies.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_WithArithmeticAndNestedLogicalTree_ShouldMatchExpectedParents()
    {
        var arithmetic = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id
                    && (c.Amount + p.Qty) - c.Weight * 2 > 0
                    && c.Amount / 2 <= c.Weight)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        arithmetic.Should().Equal(new[] { 6001 },
            "(5 + 10) - 6 and (7 + 10) - 8 are positive; the NULL amount never matches and 9 + 5 - 18 is negative");

        var logical = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id
                    && ((c.Amount > 0 && c.Weight < p.Qty) || (c.Tag == "d" && p.Bucket == 1)))))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        logical.Should().Equal(new[] { 6001, 6004 },
            "the first disjunct holds for 6001 and the second only for 6004");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. VisitBinary: three-valued equality between two nullable correlated scalars
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>==</c> and <c>!=</c> compare two NULLABLE scalars (inner amount, outer
    /// bucket) that are NULL on both sides for 6002, so the SQL three-valued logic decides both
    /// directions: the <c>==</c> query returns no row for the NULL pair and <c>!=</c> also drops it.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_NullableAgainstNullable_ShouldFollowThreeValuedLogic()
    {
        var equal = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && c.Amount == p.Bucket)))
            .Select(p => p.Id)
            .ToList();
        equal.Should().BeEmpty("no amount equals the bucket, and NULL = NULL is unknown rather than true");

        var notEqual = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && c.Amount != p.Bucket)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        notEqual.Should().Equal(new[] { 6001, 6004 }, "the NULL pair of 6002 is unknown in both directions");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. VisitBinary: string concatenation and captured locals of every shape
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the correlated predicate concatenates an inner and an outer string
    /// (<c>c.Tag + p.Name</c>, the string-<c>Add</c> rebuild of <c>VisitBinary</c> :1725) and compares
    /// it with a constant, then binds captured locals of every shape - <c>int</c>, <c>long</c>,
    /// <c>double</c>, <c>string</c> and a nullable <c>null</c> - each against a correlated column.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_WithStringConcatAndCapturedLocals_ShouldMatchEachShape()
    {
        var concat = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && c.Tag + p.Name == "aaalpha")))
            .Select(p => p.Id)
            .ToList();
        concat.Should().Equal(new[] { 6001 }, "'aa' || 'alpha' is the projected constant");

        var floorInt = 4;
        var overInt = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && c.Amount > floorInt)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        overInt.Should().Equal(new[] { 6001, 6004 });

        var floorLong = 9L;
        var overLong = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && (long)c.Weight >= floorLong)))
            .Select(p => p.Id)
            .ToList();
        overLong.Should().Equal(new[] { 6004 });

        var ratio = 5.5;
        var overDouble = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && (double?)c.Weight > ratio)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
        overDouble.Should().Equal(new[] { 6002, 6004 });

        var tag = "aa";
        var byTag = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && c.Tag == tag)))
            .Select(p => p.Id)
            .ToList();
        byTag.Should().Equal(new[] { 6001 });

        int? capturedNull = null;
        var byNull = Context.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6004)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<Co6Child>().Where(c => c.ParentId == p.Id && c.Amount == capturedNull)))
            .Select(p => p.Id)
            .ToList();
        byNull.Should().BeEmpty(
            "a captured null is bound as a parameter, so `amount = @p` is unknown even for 6002 (unlike the literal `== null`)");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. VisitMethodCall: the trace-level cache-miss log
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a context built with a Trace-level logger records the trace arm of the
    /// subquery cache miss (<c>VisitMethodCall</c> :316 and :364) for both a non-correlated builder
    /// terminal and a projected scalar subquery, while the correlated scalar still evaluates per outer
    /// row. The Debug-level counterpart is owned by <c>CorrelatedBranchCoverage2Tests</c>.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_WithTraceLogger_ShouldRecordTheTraceCacheMiss()
    {
        var recording = new TraceRecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(recording);
        });
        using var ctx = SqliteTestProvider.Instance.CreateContext(loggerFactory);

        ctx.From<Co6Note>();
        ctx.From<Co6Child>();
        ctx.From<Co6Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));

        var rows = ctx.From<Co6Parent>()
            .Where(p => p.Id >= 6001 && p.Id <= 6002)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                AnyWide = ctx.From<Co6Child>().Where(c => c.Weight > 0).Any(),
                FirstAmount = ctx.From<Co6Child>()
                    .Where(c => c.ParentId == p.Id)
                    .Select(c => (int?)c.Amount)
                    .FirstOrDefault(),
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(6001, 6002);
        rows.Should().OnlyContain(r => r.AnyWide);
        rows.Select(r => r.FirstAmount).Should().Equal(new int?[] { 5, null });
        recording.Messages.Should().Contain(m => m.Contains("Subquery expression miss", StringComparison.Ordinal),
            "the Trace logger records the subquery expression cache miss");
    }

    /// <summary>Parent marker row of the class-owned <c>co6_parent</c> table.</summary>
    [SqlTable("co6_parent")]
    public sealed class Co6Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("bucket")]
        public int? Bucket { get; set; }

        [Column("qty")]
        public int Qty { get; set; }

        public ICollection<Co6Child> Children { get; } = new List<Co6Child>();

        public ICollection<Co6Note> Notes { get; } = new List<Co6Note>();
    }

    /// <summary>Child marker row of the class-owned <c>co6_child</c> table.</summary>
    [SqlTable("co6_child")]
    public sealed class Co6Child
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }

        [Column("weight")]
        public int Weight { get; set; }

        [Column("tag")]
        public string? Tag { get; set; }
    }

    /// <summary>Note marker row of the class-owned <c>co6_note</c> table (the second collection).</summary>
    [SqlTable("co6_note")]
    public sealed class Co6Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("body")]
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>Collects diagnostic messages and enables every level, including Trace.</summary>
    private sealed class TraceRecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                    return _messages.ToArray();
            }
        }

        public ILogger CreateLogger(string categoryName) => new TraceRecordingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class TraceRecordingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (messages)
                    messages.Add(formatter(state, exception));
            }
        }
    }
}

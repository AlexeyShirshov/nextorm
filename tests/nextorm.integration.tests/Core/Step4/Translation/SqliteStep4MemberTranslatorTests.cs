using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 3 — the residual member-access and projection shapes of
/// <c>src/nextorm.core/Visitors/Functions/MemberTranslator.cs</c> plus <c>BaseExpressionVisitor</c> on live
/// SQLite: <c>string.Length</c> in both value and predicate position, the CLR <c>DateTime</c> parts,
/// the server-side <c>DateTime.Now</c>/<c>UtcNow</c>, closure constants and a captured list, nested
/// anonymous projections, the reference-tuple element, a key/value-pair constructor projection and a
/// record-struct constructor projection.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4MemberTranslatorTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_mem_row (id integer primary key, label text null, dt datetime null);
        delete from s4_mem_row;
        insert into s4_mem_row (id, label, dt) values
            (1, 'alpha', '2024-03-15 12:30:15'),
            (2, null, null),
            (3, 'abc', '2023-01-02 03:04:05');
        """;

    public SqliteStep4MemberTranslatorTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary><c>string.Length</c> renders as a length function in both value and predicate position.</summary>
    [Fact]
    public void StringLength_ShouldTranslateAsValueAndPredicate()
    {
        var rows = Context.From<S4MemRow>()
            .Where(x => x.Label!.Length > 3)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Length = x.Label!.Length })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Should().Be(new { Id = 1, Length = 5 });
    }

    /// <summary>
    /// A nested anonymous projection has no reader accessor for the inner type, so the materializer
    /// refuses it before SQL and names both the property and its type.
    /// </summary>
    [Fact]
    public void NestedAnonymousProjection_ShouldBeRefusedByTheMaterializer()
    {
        var act = () => Context.From<S4MemRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                x.Id,
                Inner = new { x.Label, NameLength = x.Label!.Length }
            })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Property 'Inner'*not supported*");
    }

    /// <summary>The CLR <c>DateTime</c> parts translate to the provider's date-part expressions.</summary>
    [Fact]
    public void DateTimeParts_ShouldTranslate()
    {
        var r = Context.From<S4MemRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = x.Dt!.Value.Year,
                Month = x.Dt!.Value.Month,
                Day = x.Dt!.Value.Day,
                Hour = x.Dt!.Value.Hour,
                Minute = x.Dt!.Value.Minute,
                Second = x.Dt!.Value.Second,
                DayOfYear = x.Dt!.Value.DayOfYear
            })
            .Single();

        r.Year.Should().Be(2024);
        r.Month.Should().Be(3);
        r.Day.Should().Be(15);
        r.Hour.Should().Be(12);
        r.Minute.Should().Be(30);
        r.Second.Should().Be(15);
        r.DayOfYear.Should().Be(75);
    }

    /// <summary>The server clock is rendered, never captured from the client.</summary>
    [Fact]
    public void ServerDateTimes_ShouldBeRenderedByTheProvider()
    {
        var r = Context.From<S4MemRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Now = DateTime.Now,
                UtcNow = DateTime.UtcNow
            })
            .Single();

        r.Now.Year.Should().BeGreaterThanOrEqualTo(2024);
        r.UtcNow.Year.Should().BeGreaterThanOrEqualTo(2024);
    }

    /// <summary>A captured scalar is bound as a parameter, not inlined as SQL text.</summary>
    [Fact]
    public void ClosureConstants_ShouldBeBound()
    {
        var label = "alpha";

        var rows = Context.From<S4MemRow>()
            .Where(x => x.Label == label)
            .Select(x => new { x.Id, Mirror = label })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(1);
        rows[0].Mirror.Should().Be("alpha");
    }

    /// <summary>A captured list is translated into the value-list <c>IN</c> predicate.</summary>
    [Fact]
    public void CapturedList_ShouldTranslateTheInPredicate()
    {
        var ids = new[] { 1, 3 };

        var rows = Context.From<S4MemRow>()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(1, 3);
    }

    /// <summary>A reference-tuple element access projects one component.</summary>
    [Fact]
    public void TupleElement_ShouldProjectOneComponent()
    {
        var rows = Context.From<S4MemRow>()
            .OrderBy(x => x.Id)
            .Select(x => Tuple.Create(x.Id, x.Label).Item2)
            .ToList();

        rows.Should().Equal("alpha", null, "abc");
    }

    /// <summary>A key/value-pair constructor is materialized as an ordinary projection type.</summary>
    [Fact]
    public void KeyValuePairProjection_ShouldMaterialize()
    {
        var rows = Context.From<S4MemRow>()
            .Where(x => x.Id == 1)
            .Select(x => new KeyValuePair<int, string?>(x.Id, x.Label))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Key.Should().Be(1);
        rows[0].Value.Should().Be("alpha");
    }

    /// <summary>A record struct is materialized through its positional constructor.</summary>
    [Fact]
    public void RecordStructProjection_ShouldMaterialize()
    {
        var rows = Context.From<S4MemRow>()
            .Where(x => x.Id == 3)
            .Select(x => new S4MemStruct(x.Id, x.Label))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(3);
        rows[0].Label.Should().Be("abc");
    }

    /// <summary>Marker source of the member-translation facts.</summary>
    [SqlTable("s4_mem_row")]
    public sealed class S4MemRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("dt")]
        public DateTime? Dt { get; set; }
    }

    /// <summary>Positional record-struct projection type.</summary>
    public readonly record struct S4MemStruct(int Id, string? Label);
}

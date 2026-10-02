using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    public sealed class CteNumberRow
    {
        public int n { get; set; }
    }

    [Fact]
    public void Cte_NonRecursive_FilteredAndJoinedWithTable_ShouldReturnData()
    {
        var complex = _sut.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });

        var rows = _sut.DataProvider
            .With("recent", complex)
            .From("recent")
            .Join(_sut.SimpleEntity, (c, s) => c["id"].AsInt == s.Id)
            .Where(p => p.Item2.Id > 0)
            .Select(p => new { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id })
            .ToList();

        // complex_entity has ids 1..3; filtering id > 1 leaves 2 and 3, which match simple_entity.
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Id == 2 || r.Id == 3);
        rows.Should().OnlyContain(r => r.SimpleId == r.Id);
    }

    [Fact]
    public void Cte_Recursive_ShouldProduceNumberSeries()
    {
        var ctx = _sut.DataProvider;

        var anchor = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);

        var rows = ctx
            .WithRecursive("nums", body)
            .From("nums")
            .Select(t => new CteNumberRow { n = t["n"].AsInt })
            .ToList();

        rows.Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3, 4, 5);
    }

    /// <summary>
    /// A CTE whose body is itself a query carrying a CTE (a query built as <c>With(...).From(...)</c>) must
    /// be hoisted into one top-level <c>WITH</c> and execute against every provider, including SQL Server,
    /// whose T-SQL forbids a <c>WITH</c> nested inside a derived table.
    /// </summary>
    [Fact]
    public void Cte_Nested_ShouldHoistAndReturnData()
    {
        var ctx = _sut.DataProvider;

        var inner = ctx.With("i", _sut.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });

        var rows = ctx
            .With("o", inner)
            .From("o")
            .Select(t => new { id = t["id"].AsInt })
            .ToList();

        // complex_entity has ids 1..3; the inner CTE keeps 2 and 3 and the outer one passes them through.
        rows.Select(r => r.id).OrderBy(id => id).Should().Equal(2, 3);
    }

    // ---------------------------------------------------------------------------------------------
    // Typed ordinary CTE (#146 slice A): the descriptor read through From(cte) on every provider.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Typed read plus an outer reselect: the shape survives the CTE boundary.</summary>
    [Fact]
    public void Cte_Typed_OuterReselect_ShouldReturnData()
    {
        var ctx = _sut.DataProvider;

        var recent = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.RequiredString })
            .AsCte("typed_recent");

        var rows = ctx.From(recent)
            .Select(r => new { r.Id, r.RequiredString })
            .ToList();

        rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
        rows.Single(r => r.Id == 1).RequiredString.Should().Be("sdf");
    }

    /// <summary>A value converter survives both a direct and a reselected typed read of the CTE.</summary>
    [Fact]
    public void Cte_Typed_ConverterPreserved_DirectAndReselected()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var textType = ProbeTextType(dialect);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        ExecuteValueConverters(ctx, $"create table value_converter_probe (id bigint, state {textType}, at {textType}){engine}");

        try
        {
            ctx.CreateInsertBuilder<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 1, State = ProbeState.Active, At = null })
                .Insert();
            ctx.CreateInsertBuilder<IValueConverterProbe>()
                .Values(new ValueConverterProbe { Id = 2, State = ProbeState.Closed, At = null })
                .Insert();

            var probe = ctx.From<IValueConverterProbe>().ToCommand().AsCte("typed_probe");

            // Direct whole-shape reselect keeps the converter.
            var direct = ctx.From(probe).Select(x => new { x.Id, x.State }).ToList();
            direct.Single(r => r.Id == 1).State.Should().Be(ProbeState.Active);
            direct.Single(r => r.Id == 2).State.Should().Be(ProbeState.Closed);

            // A scalar member reselect reads the same converted value, without a second conversion.
            var states = ctx.From(probe).Where(x => x.Id == 1).Select(x => x.State).ToList();
            states.Should().Equal(ProbeState.Active);
        }
        finally
        {
            ExecuteValueConverters(ctx, "drop table if exists value_converter_probe");
        }
    }

    /// <summary>Several heterogeneous typed CTEs, one of them depending on another, hoist in order.</summary>
    [Fact]
    public void Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData()
    {
        var ctx = _sut.DataProvider;

        var dependency = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 1)
            .Select(c => new { c.Id, c.RequiredString })
            .AsCte("typed_dep");
        var consumer = ctx.From(dependency)
            .Select(d => new { d.Id })
            .AsCte("typed_consumer");
        var other = ctx.From<IComplexEntity>()
            .Where(c => c.Id < 9)
            .Select(c => new { c.Id, c.Int })
            .AsCte("typed_other");

        var rows = ctx.From(consumer)
            .Join(ctx.From(other), (a, b) => a.Id == b.Id)
            .Select(p => new { p.Item1.Id })
            .ToList();

        // dependency (ids 2,3) feeds the consumer; 'other' (ids 1..3) intersects on 2 and 3.
        rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(2, 3);
    }

    /// <summary>A self-join of one typed CTE materialises both sides through distinct aliases.</summary>
    [Fact]
    public void Cte_Typed_SelfJoin_ShouldReturnBothSides()
    {
        var ctx = _sut.DataProvider;

        var cte = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.RequiredString })
            .AsCte("typed_self");

        var rows = ctx.From(cte)
            .Join(ctx.From(cte), (a, b) => a.Id == b.Id)
            .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
            .ToList();

        rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
        rows.Single(r => r.Id == 3).RequiredString.Should().Be("34mfs");
    }

    /// <summary>A predicate on the defining body restricts the CTE; the outer read adds its own.</summary>
    [Fact]
    public void Cte_Typed_BodyFilter_ShouldStayInsideTheBody()
    {
        var ctx = _sut.DataProvider;

        var filtered = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 1)
            .Select(c => new { c.Id })
            .AsCte("typed_filtered");

        // Without re-stating the predicate, the outer read already sees only the rows the body kept.
        ctx.From(filtered).Select(r => r.Id).ToList().OrderBy(id => id).Should().Equal(2, 3);

        // An additional outer predicate narrows further; the body restriction is not lost.
        ctx.From(filtered).Where(r => r.Id > 2).Select(r => r.Id).ToList().Should().Equal(3);
    }

    /// <summary>A consumer typed CTE built from a producer typed CTE reads through the dependency.</summary>
    [Fact]
    public void Cte_Typed_ChainedConsumer_ShouldReadThroughProducer()
    {
        var ctx = _sut.DataProvider;

        var inner = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 1)
            .Select(c => new { c.Id, c.RequiredString })
            .AsCte("typed_inner");
        var outer = ctx.From(inner)
            .Select(i => new { i.Id })
            .AsCte("typed_outer");

        var rows = ctx.From(outer).Select(r => r.Id).ToList();

        rows.OrderBy(id => id).Should().Equal(2, 3);
    }
}

using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// T243/D243 safeguard: the SQL scalar fast path is a <see cref="DataContext"/>-owned artifact store,
/// so the in-memory provider must stay on its own executor and keep returning the same values. These
/// tests pin that premise (<see cref="InMemoryDataContext"/> is not a <see cref="DataContext"/>) and
/// the aggregate results themselves, including a nullable column and an empty source.
/// </summary>
public class T243WarmScalarArtifactInMemoryTests
{
    [Fact]
    public void Context_ShouldNotBeASqlDataContext()
    {
        using var ctx = new InMemoryDataContext();

        // The fast path is gated on `builder.DataProvider is DataContext`; the in-memory provider is a
        // separate implementation and therefore always keeps its ordinary executor.
        ctx.Should().NotBeAssignableTo<DataContext>();
    }

    [Fact]
    public void Count_And_LongCount_ShouldStayCorrectOnRepeatedCalls()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Data[typeof(SimpleEntity)] = new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } };

        ctx.From<SimpleEntity>().Count().Should().Be(2);
        ctx.From<SimpleEntity>().Count().Should().Be(2);
        ctx.From<SimpleEntity>().LongCount().Should().Be(2L);
        ctx.From<SimpleEntity>().LongCount().Should().Be(2L);
    }

    [Fact]
    public void Sum_ShouldStayCorrect_IncludingANullableColumn()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Data[typeof(NullableRow)] = new[]
        {
            new NullableRow { Id = 1, Amount = 10 },
            new NullableRow { Id = 2, Amount = null },
            new NullableRow { Id = 3, Amount = 20 },
        };

        ctx.From<NullableRow>().Sum(x => x.Id).Should().Be(6);
        ctx.From<NullableRow>().Sum(x => x.Amount).Should().Be(30);
        ctx.From<NullableRow>().Sum(x => x.Amount).Should().Be(30);
    }

    [Fact]
    public void Sum_OnAnEmptySource_ShouldKeepTheInMemoryContract()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Data[typeof(NullableRow)] = Array.Empty<NullableRow>();

        ctx.From<NullableRow>().Count().Should().Be(0);
        ctx.From<NullableRow>().LongCount().Should().Be(0L);
        ctx.From<NullableRow>().Sum(x => x.Id).Should().Be(0);

        // The in-memory provider documents SUM over an empty set as 0 even for a nullable result (the
        // SQL providers return NULL instead); the fast path is never involved here, so this only pins
        // that the old executor is untouched.
        ctx.From<NullableRow>().Sum(x => x.Amount).Should().Be(0);
    }

    public sealed class NullableRow
    {
        public int Id { get; set; }

        public int? Amount { get; set; }
    }
}

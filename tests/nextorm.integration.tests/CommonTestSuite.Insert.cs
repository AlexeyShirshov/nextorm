using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static string InsertMarker() => "ins_" + Guid.NewGuid().ToString("N");

    [Fact]
    public void Insert_Value_ShouldPersistRow()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var affected = ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 42)
            .Insert();

        affected.Should().BeGreaterThanOrEqualTo(0);

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Age.Should().Be(42);
    }

    [Fact]
    public void Insert_Entity_ShouldExcludeGeneratedColumns()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        ctx.InsertInto<IInsertEntity>()
            .Values(new InsertEntity { Id = 123456, Name = marker, Age = 7 })
            .Insert();

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Age.Should().Be(7);
    }

    [Fact]
    public void Insert_Batch_ShouldPersistEveryRow()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = marker, Age = 1 },
                new InsertEntity { Name = marker, Age = 2 },
            ])
            .Insert();

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().HaveCount(2);
    }

    [Fact]
    public void Insert_FromQuery_ShouldPersistSelectedRows()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 10)
            .Insert();

        var affected = ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<IInsertEntity>().Where(x => x.Name == marker), x => new { x.Name, x.Age })
            .Insert();

        affected.Should().BeGreaterThanOrEqualTo(1);

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().HaveCount(2);
    }

    [Fact]
    public void Insert_FromQuery_ReturningSingle_OnMultipleRows_ShouldThrow()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = marker, Age = 1 },
                new InsertEntity { Name = marker, Age = 2 },
            ])
            .Insert();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<IInsertEntity>().Where(x => x.Name == marker), x => new { x.Name, x.Age })
            .Returning(x => new { x.Age })
            .Single();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReturningIdentity_Function_ShouldReturnGeneratedKey()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var id = ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 3)
            .ReturningIdentity<long>()
            .Single();

        id.Should().BeGreaterThan(0);

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void ReturningKey_ShouldReturnGeneratedKey()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var id = ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 4)
            .ReturningKey<long>()
            .Single();

        id.Should().BeGreaterThan(0);

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void ReturningIdentitySelector_ShouldReturnGeneratedKey()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return generated columns on the insert.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var id = ctx.InsertInto<InsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 5)
            .ReturningIdentity(x => x.Id)
            .Single();

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReturningKey_Batch_ShouldReturnEveryKey()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return generated columns on the insert.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var ids = ctx.InsertInto<InsertEntity>()
            .Values([
                new InsertEntity { Name = marker, Age = 1 },
                new InsertEntity { Name = marker, Age = 2 },
            ])
            .ReturningKey<long>()
            .ToList();

        ids.Should().HaveCount(2);
        ids.Should().OnlyContain(id => id > 0);
    }

    [Fact]
    public void Insert_Returning_ShouldReturnWrittenRow()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var row = ctx.InsertInto<InsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 42)
            .Returning()
            .Single();

        row.Id.Should().BeGreaterThan(0);
        row.Name.Should().Be(marker);
        row.Age.Should().Be(42);
    }

    [Fact]
    public void Insert_ReturningProjection_ShouldReturnProjection()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var row = ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 7)
            .Returning(x => new { x.Id, x.Name })
            .Single();

        row.Id.Should().BeGreaterThan(0);
        row.Name.Should().Be(marker);
    }

    [Fact]
    public void Insert_ReturningBatch_ShouldReturnEveryRow()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var rows = ctx.InsertInto<InsertEntity>()
            .Values([
                new InsertEntity { Name = marker, Age = 1 },
                new InsertEntity { Name = marker, Age = 2 },
            ])
            .Returning()
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Age).Should().BeEquivalentTo([1, 2]);
        rows.Should().OnlyContain(r => r.Id > 0 && r.Name == marker);
    }

    [Fact]
    public void ReturningConversionProjection_ShouldMaterialize()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var age = ctx.InsertInto<InsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 7)
            .Returning(x => (long)x.Age)
            .Single();

        age.Should().Be(7L);
    }

    [Fact]
    public void ReturningKey_NullableKeyType_ShouldMaterialize()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;

        var id = ctx.InsertInto<InsertEntity>()
            .Value(x => x.Name, InsertMarker())
            .ReturningKey<long?>()
            .Single();

        id.Should().NotBeNull();
        id!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Insert_Returning_ShouldThrow_WhenProviderCannot()
    {
        Assert.SkipUnless(!Provider.SupportsInsertReturning, "This provider can return inserted rows.");

        var ctx = _sut.DataProvider;

        var act = () => ctx.InsertInto<InsertEntity>()
            .Value(x => x.Name, InsertMarker())
            .Returning()
            .Single();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Insert_ReturningWholeEntity_OnInterface_ShouldThrow()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, InsertMarker())
            .Returning()
            .Single();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ValuesMapping_ShouldPersistEveryRow()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var sources = new[] { new InsertSource(marker, 1), new InsertSource(marker, 2) };

        ctx.InsertInto<IInsertEntity>()
            .Values(sources, s => new { s.Name, s.Age })
            .Insert();

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Age).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void ValuesMapping_EntityInitializer_ShouldPersistEveryRow()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var sources = new[] { new InsertSource(marker, 4), new InsertSource(marker, 5) };

        ctx.InsertInto<InsertEntity>()
            .Values(sources, s => new InsertEntity { Name = s.Name, Age = s.Age })
            .Insert();

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Age).Should().BeEquivalentTo([4, 5]);
    }

    [Fact]
    public void Values_ColumnSequence_ShouldPersistEveryRow()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var nameA = marker + "a";
        var nameB = marker + "b";

        ctx.InsertInto<IInsertEntity>()
            .Values(x => x.Name, new[] { nameA, nameB })
            .Values(x => x.Age, new[] { 6, 7 })
            .Insert();

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == nameA || x.Name == nameB)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Age).Should().BeEquivalentTo([6, 7]);
    }
}

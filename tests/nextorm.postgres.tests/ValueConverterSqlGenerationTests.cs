using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

public enum SqlGenState
{
    Unknown,
    Active,
    Closed,
}

public sealed class BracketConverter : ValueConverter<string, string>
{
    public override string? ConvertToProvider(string? model) => model is null ? null : "[" + model + "]";

    public override string? ConvertFromProvider(string? provider) => provider?.Trim('[', ']');
}

[SqlTable("converter_entity")]
public interface IConverterEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("state")]
    [ValueConverter(typeof(EnumToStringConverter<SqlGenState>))]
    SqlGenState State { get; set; }

    [Column("name")]
    [ValueConverter(typeof(BracketConverter))]
    string? Name { get; set; }
}

public sealed class ConverterEntity : IConverterEntity
{
    public int Id { get; set; }
    public SqlGenState State { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Verifies that a value-converted property is planned like any other column and that the write seam
/// converts its value to the provider representation. No database connection is used.
/// </summary>
public class ValueConverterSqlGenerationTests
{
    [Fact]
    public void Insert_WithConvertedProperty_ShouldRenderColumn()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<ConverterEntity>()
            .Values(new ConverterEntity { Id = 1, State = SqlGenState.Active })
            .ToSql()
            .Should().Contain("state");
    }

    [Fact]
    public void UpdateSet_WithConvertedProperty_ShouldRenderColumn()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Update<ConverterEntity>()
            .Set(x => x.State, SqlGenState.Active)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Contain("state");
    }

    [Fact]
    public void WriteSeam_ShouldConvertToProviderValue()
    {
        using var ctx = PostgresTestContext.Create();

        var property = new EntityMetadataBuilder<ConverterEntity>().Build().Properties
            .Single(p => p.PropertyInfo.Name == nameof(ConverterEntity.State));
        var method = typeof(DataContext).Assembly.GetType("NextORM.Core.DurationStorage")!
            .GetMethod("ToParameterValue", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = method.Invoke(null, [SqlGenState.Active, property, ((DataContext)ctx).Dialect]);

        result.Should().Be("Active");
    }

    [Fact]
    public void Where_WithStaticConvertedConstant_ShouldBindProviderValue()
    {
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(ctx, ctx.From<ConverterEntity>().Where(x => x.State == SqlGenState.Active).ToCommand());

        prepared.DbCommand.CommandText.Should().Contain("state = @");
        new[] { prepared.DbCommand.Parameters[0].Value }.Should().Equal("Active");
    }

    [Fact]
    public void Where_WithCapturedConvertedConstant_ShouldBindProviderValue()
    {
        using var ctx = PostgresTestContext.Create();
        var state = SqlGenState.Closed;

        var prepared = Prepare(ctx, ctx.From<ConverterEntity>().Where(x => x.State == state).ToCommand());

        new[] { prepared.DbCommand.Parameters[0].Value }.Should().Equal("Closed");
    }

    [Fact]
    public void Where_WithConvertedConstantOnLeft_ShouldBindProviderValue()
    {
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(ctx, ctx.From<ConverterEntity>().Where(x => SqlGenState.Active == x.State).ToCommand());

        new[] { prepared.DbCommand.Parameters[0].Value }.Should().Equal("Active");
    }

    [Fact]
    public void Where_WithConvertedValueList_ShouldBindProviderValues()
    {
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(ctx, ctx.From<ConverterEntity>()
            .Where(x => new[] { SqlGenState.Active, SqlGenState.Closed }.Contains(x.State))
            .ToCommand());

        prepared.DbCommand.CommandText.Should().Contain("state in (");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).Should().Equal("Active", "Closed");
    }

    [Fact]
    public void Select_ConvertedProperty_ShouldCarryConverterIntoMaterialization()
    {
        using var ctx = PostgresTestContext.Create();

        var cmd = ctx.From<ConverterEntity>().Where(x => x.Id > 0).Select(x => x.State);
        Prepare(ctx, cmd);

        cmd.SelectList!.Should().ContainSingle();
        cmd.SelectList[0].Converter.Should().NotBeNull();
        cmd.SelectList[0].ProviderType.Should().Be<string>();
    }

    [Fact]
    public void Select_AnonymousConvertedProperty_ShouldCarryConverterIntoMaterialization()
    {
        using var ctx = PostgresTestContext.Create();

        var cmd = ctx.From<ConverterEntity>().Where(x => x.Id > 0).Select(x => new { x.State });
        Prepare(ctx, cmd);

        cmd.SelectList!.Single(c => c.PropertyName == nameof(ConverterEntity.State)).Converter.Should().NotBeNull();
    }

    [Fact]
    public void Where_WithConvertedColumnComparedToColumnExpression_ShouldNotConvertConstants()
    {
        using var ctx = PostgresTestContext.Create();

        // The value side references a column, so it must render as ordinary SQL: the constant 1 in
        // "x.Id + 1" is not converted through the State converter.
        var prepared = Prepare(ctx, ctx.From<ConverterEntity>().Where(x => (int)x.State == x.Id + 1).ToCommand());

        prepared.DbCommand.CommandText.Should().NotContain("'");
        prepared.DbCommand.Parameters.Cast<DbParameter>()
            .Should().NotContain(p => Equals(p.Value, "Active") || Equals(p.Value, "Closed"));
    }

    [Fact]
    public void Where_WithCompoundConvertedValue_ShouldConvertTheWholeValueOnce()
    {
        using var ctx = PostgresTestContext.Create();
        var a = 1;
        var b = 1;

        // (a + b) is a single value (2 -> Closed), not two operands to convert and concatenate: the
        // converter must run once on the folded result.
        var prepared = Prepare(ctx, ctx.From<ConverterEntity>().Where(x => (int)x.State == a + b).ToCommand());

        prepared.DbCommand.CommandText.Should().Contain("state = @");
        new[] { prepared.DbCommand.Parameters[0].Value }.Should().Equal("Closed");
    }

    [Fact]
    public void Where_WithConvertedConditionalBranches_ShouldConvertBranchConstants()
    {
        using var ctx = PostgresTestContext.Create();

        // The branches depend on a column, so they are rendered as a CASE by a cloned visitor; the
        // converter has to survive the clone, or the branch values bind their raw enum form.
        var prepared = Prepare(ctx, ctx.From<ConverterEntity>()
            .Where(x => x.State == (x.Id > 0 ? SqlGenState.Active : SqlGenState.Closed))
            .ToCommand());

        prepared.DbCommand.CommandText.Should().Contain("case when");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value)
            .Should().Contain("Active").And.Contain("Closed");
    }

    [Fact]
    public void GroupBy_ConvertedProperty_ShouldCarryConverterIntoMaterialization()
    {
        using var ctx = PostgresTestContext.Create();

        // The grouping key is re-projected through the normal select path, so it must carry the
        // converter (the GROUP BY list is only rendered, never materialized).
        var cmd = ctx.From<ConverterEntity>().GroupBy(x => x.State).Select(x => new { x.State });
        Prepare(ctx, cmd);

        cmd.SelectList!.Single(c => c.PropertyName == nameof(ConverterEntity.State)).Converter.Should().NotBeNull();
    }

    [Fact]
    public void Where_WithStringConcatValue_ShouldConvertTheWholeConcatenation()
    {
        using var ctx = PostgresTestContext.Create();
        var a = "A";
        var b = "B";

        // a + b is one value; a bracket converter must see "AB" once, not "[A]" concatenated with "[B]".
        var prepared = Prepare(ctx, ctx.From<ConverterEntity>().Where(x => x.Name == a + b).ToCommand());

        prepared.DbCommand.CommandText.Should().Contain("name = @");
        new[] { prepared.DbCommand.Parameters[0].Value }.Should().Equal("[AB]");
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
}

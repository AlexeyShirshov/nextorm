using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// T306 (#306) boundary guards around whole-entity projection preparation: an empty prepared select
/// list must fail closed with the existing typed SQL-build exception instead of reaching the renderer,
/// a null select list keeps the existing wildcard star behaviour, and a whole-object conversion to a
/// non-entity / unrelated interface type never becomes an identity projection. No connection is opened.
/// </summary>
public class T306IdentityBoundaryTests
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

    private static IDataContext CreateContext()
        => new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void EmptySelectList_ShouldThrowTheTypedSqlBuildExceptionBeforeRender()
    {
        using var ctx = CreateContext();

        // An empty whole-object shape prepares an empty (non-null) select list. Before the fix the
        // renderer appended no columns and then stripped two characters from "select ", emitting the
        // malformed "selec" head; the prepared empty list must instead be rejected fail-closed.
        Action act = () => SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => new { }));

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void NullSelectList_ShouldKeepTheWildcardStarBehaviour()
    {
        using var ctx = CreateContext();
        var cmd = ctx.From<EntityParent>().OrderBy(x => x.Id).ToCommand();
        _ = SqlOf(ctx, cmd);

        // Preparation always stores a non-null list, so the null/wildcard arm of the renderer is
        // exercised on a prepared command with the prepared list cleared. It must keep rendering the
        // existing "select *" head (the branch the empty-list fix must not disturb).
        typeof(QueryCommand).GetField("_selectList", InstanceFields)!.SetValue(cmd, null);

        var build = new SqlBuildContext
        {
            Dialect = SqliteDialect.Instance,
            ParamMode = false,
            Params = [],
            ColumnsProvider = new DefaultColumnsProvider(),
            QueryProvider = cmd,
            ParameterProvider = new DefaultParameterProvider(),
            AliasProvider = new DefaultAliasProvider(),
        };
        var sql = new SqlBuilder(in build).MakeSelect(cmd)!.Replace("\r\n", "\n");

        sql.Should().Be("select *\n from entity_parent\n order by id");
    }

    [Fact]
    public void NonEntityWholeObjectConversion_ShouldNotBecomeAnIdentityProjection()
    {
        using var ctx = CreateContext();

        // `(object)x` unwraps to the source parameter expression, so a fix that accepts any unwrapped
        // parameter as identity would silently project the whole entity into a non-entity result type.
        Action act = () => SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => (object)x));

        act.Should().Throw<DataContextException>(
            "a whole-object conversion to a non-entity type must fail closed, never become identity");
    }

    [Fact]
    public void UnrelatedInterfaceConversion_ShouldStayRejected()
    {
        using var ctx = CreateContext();

        Action act = () => SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => (IT306View)x));

        act.Should().Throw<DataContextException>(
            "an interface that is not the registered entity mapping is not an identity projection");
    }
}

/// <summary>An unrelated, unmapped interface the mapped entity happens to implement.</summary>
public interface IT306View
{
    int Id { get; set; }
    string? Name { get; set; }
}

/// <summary>The mapped single source (columns <c>id</c>, <c>name</c>) used by the boundary guards.</summary>
[SqlTable("entity_parent")]
public sealed class EntityParent : IT306View
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

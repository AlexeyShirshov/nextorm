using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 2 — the container-option and validation surface of
/// <c>src/nextorm.core/Query/Json/JsonShapePlan.cs</c> and the managed JSON stream
/// (<c>WriteJson</c>/<c>WriteJsonAsync</c>) on live SQLite: the <c>Root</c> wrapper, <c>WriteIndented</c>,
/// the naming policy, NDJSON, the asynchronous terminal and the option-combination guards.
/// <para>
/// Every accepted path compares the emitted document with the same rows serialised by
/// <see cref="JsonSerializer"/>, or parses it and compares the tree; every refusal asserts the exception
/// type plus the exact <c>[mode-options]</c> marker the validator carries.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4JsonStreamOptionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_js_row (id integer primary key, label text null, amount int null);
        delete from s4_js_row;
        insert into s4_js_row (id, label, amount) values
            (9101, 'a', 10),
            (9102, null, null),
            (9103, 'c', 30);
        """;

    public SqliteStep4JsonStreamOptionTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>The <c>Root</c> option wraps the array in one named object member.</summary>
    [Fact]
    public void WriteJson_WithRoot_ShouldWrapTheArray()
    {
        var rows = Context.From<S4JsRow>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Label })
            .ToList();
        var expected = JsonSerializer.Serialize(new { items = rows });

        var actual = WriteJson(
            Context.From<S4JsRow>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Label }),
            new JsonStreamOptions { Root = "items" });

        actual.Should().Be(expected);
    }

    /// <summary><c>Root</c> and <c>IgnoreNull</c> compose: the wrapper stays and the null member disappears.</summary>
    [Fact]
    public void WriteJson_WithRootAndIgnoreNull_ShouldOmitNullMembers()
    {
        var actual = WriteJson(
            Context.From<S4JsRow>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Label }),
            new JsonStreamOptions { Root = "items", IgnoreNull = true });

        var document = JsonNode.Parse(actual)!.AsObject();
        var items = document["items"]!.AsArray();

        items.Should().HaveCount(3);
        items[1]!.AsObject().Should().ContainKey("Id").And.NotContainKey("Label");
    }

    /// <summary>The indented document carries the same tree as the compact one, with line breaks.</summary>
    [Fact]
    public void WriteJson_WriteIndented_ShouldKeepTheSameTree()
    {
        var compact = WriteJson(
            Context.From<S4JsRow>().OrderBy(x => x.Id).Select(x => new { x.Id, Score = x.Amount }),
            new JsonStreamOptions());
        var indented = WriteJson(
            Context.From<S4JsRow>().OrderBy(x => x.Id).Select(x => new { x.Id, Score = x.Amount }),
            new JsonStreamOptions { WriteIndented = true });

        indented.Should().Contain("\n", "indented output spans multiple lines");
        indented.Should().Contain("  ");
        JsonNode.DeepEquals(JsonNode.Parse(compact), JsonNode.Parse(indented)).Should().BeTrue();
    }

    /// <summary>The naming policy renames every object member of the document.</summary>
    [Fact]
    public void WriteJson_WithNamingPolicy_ShouldRenameTheMembers()
    {
        var actual = WriteJson(
            Context.From<S4JsRow>().OrderBy(x => x.Id).Select(x => new { RowId = x.Id, RowLabel = x.Label }),
            new JsonStreamOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        actual.Should().Contain("\"row_id\"");
        actual.Should().Contain("\"row_label\"");
        actual.Should().NotContain("\"RowId\"");
    }

    /// <summary>The asynchronous terminal emits the same document as the synchronous one.</summary>
    [Fact]
    public async Task WriteJsonAsync_WithRoot_ShouldMatchTheSynchronousDocument()
    {
        var sync = WriteJson(
            Context.From<S4JsRow>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Amount }),
            new JsonStreamOptions { Root = "rows" });

        using var buffer = new MemoryStream();
        await Context.From<S4JsRow>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Amount })
            .WriteJsonAsync(buffer, new JsonStreamOptions { Root = "rows" }, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(buffer.ToArray()).Should().Be(sync);
    }

    /// <summary>NDJSON has no wrapper, so the <c>Root</c> option is refused by name.</summary>
    [Fact]
    public void WriteJson_NdJsonWithRoot_ShouldRefuseTheCombination()
    {
        var act = () => WriteJson(
            Context.From<S4JsRow>().Select(x => new { x.Id }),
            new JsonStreamOptions { Mode = JsonStreamMode.NdJson, Root = "rows" });

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON streaming validation [mode-options]*'Root' option is only supported in Array mode*");
    }

    /// <summary>NDJSON must stay line-delimited, so an indented document is refused.</summary>
    [Fact]
    public void WriteJson_NdJsonWithWriteIndented_ShouldRefuseTheCombination()
    {
        var act = () => WriteJson(
            Context.From<S4JsRow>().Select(x => new { x.Id }),
            new JsonStreamOptions { Mode = JsonStreamMode.NdJson, WriteIndented = true });

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON streaming validation [mode-options]*'WriteIndented' option is only supported in Array mode*");
    }

    /// <summary>An undefined mode value fails closed with the supported set in the message.</summary>
    [Fact]
    public void WriteJson_UnknownMode_ShouldRefuseTheMode()
    {
        var act = () => WriteJson(
            Context.From<S4JsRow>().Select(x => new { x.Id }),
            new JsonStreamOptions { Mode = (JsonStreamMode)99 });

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON streaming validation [mode-options]*Unknown JsonStreamMode*Array and NdJson*");
    }

    private static string WriteJson<TResult>(QueryCommand<TResult> command, JsonStreamOptions options)
    {
        using var buffer = new MemoryStream();
        command.WriteJson(buffer, options);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Marker row of the JSON-stream option facts.</summary>
    [SqlTable("s4_js_row")]
    public sealed class S4JsRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }
}

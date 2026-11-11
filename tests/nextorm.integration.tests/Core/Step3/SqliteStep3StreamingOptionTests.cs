using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 2 — the CSV/JSON streaming terminals' option and shape surfaces that batch 1 did not
/// touch on a live SQLite database (<c>src/nextorm.core/Query/Csv/CsvStreamWriter.cs</c>:
/// <c>ValidateOptions</c>/<c>ValidateProjection</c>; <c>src/nextorm.core/Query/Json/JsonShapePlan.cs</c>:
/// <c>Classify</c>). The accepted fact writes real rows and asserts the exact UTF-8 bytes; every refusal
/// is raised before the row is written and is asserted by exception type plus the semantic identifier the
/// guard carries.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3StreamingOptionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_st_row (id integer primary key, name text null, note text null);
        delete from s3_st_row;
        create table if not exists s3_st_enum (id integer primary key, state text null);
        delete from s3_st_enum;
        insert into s3_st_row (id, name, note) values (1, 'a;b', null), (2, null, 'x');
        insert into s3_st_enum (id, state) values (1, 'Active'), (2, 'Closed');
        """;

    public SqliteStep3StreamingOptionTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    // ---------------------------------------------------------------------------------------------
    // Accepted CSV dialect
    // ---------------------------------------------------------------------------------------------

    /// <summary>A custom delimiter, a custom NULL marker and no header produce the exact RFC 4180 bytes.</summary>
    [Fact]
    public void Csv_CustomDialect_ShouldWriteExactBytes()
    {
        using var destination = new MemoryStream();

        Context.From<S3StRow>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Note })
            .WriteCsv(
                destination,
                new CsvStreamOptions { Delimiter = ';', NullMarker = "(null)", IncludeHeader = false },
                TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray())
            .Should().Be("1;\"a;b\";(null)\r\n2;(null);x\r\n");
    }

    // ---------------------------------------------------------------------------------------------
    // CSV option guards
    // ---------------------------------------------------------------------------------------------

    /// <summary>The delimiter cannot be a double quote, CR or LF; each reserved character is named.</summary>
    [Theory]
    [InlineData('"')]
    [InlineData('\r')]
    [InlineData('\n')]
    public void Csv_ReservedDelimiter_ShouldBeRefused(char delimiter)
    {
        var act = () => WriteCsv(new CsvStreamOptions { Delimiter = delimiter });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CSV delimiter cannot be a double quote, CR or LF*");
    }

    /// <summary>A lone surrogate delimiter cannot be encoded as UTF-8 and is refused.</summary>
    [Fact]
    public void Csv_LoneSurrogateDelimiter_ShouldBeRefused()
    {
        var act = () => WriteCsv(new CsvStreamOptions { Delimiter = '\uD800' });

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*CSV delimiter cannot be a lone surrogate*");
    }

    /// <summary>An empty NULL marker cannot be told apart from an empty field.</summary>
    [Fact]
    public void Csv_EmptyNullMarker_ShouldBeRefused()
    {
        var act = () => WriteCsv(new CsvStreamOptions { NullMarker = string.Empty });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CSV NULL marker cannot be null or empty*");
    }

    /// <summary>The NULL marker is written verbatim, so it must not contain the delimiter.</summary>
    [Fact]
    public void Csv_NullMarkerContainingDelimiter_ShouldBeRefused()
    {
        var act = () => WriteCsv(new CsvStreamOptions { Delimiter = ',', NullMarker = "a,b" });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CSV NULL marker cannot contain a double quote, CR, LF or the delimiter*");
    }

    /// <summary>The NULL marker must not contain a double quote either.</summary>
    [Fact]
    public void Csv_NullMarkerContainingQuote_ShouldBeRefused()
    {
        var act = () => WriteCsv(new CsvStreamOptions { NullMarker = "\"" });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CSV NULL marker cannot contain a double quote*");
    }

    /// <summary>A lone surrogate in the NULL marker has no valid UTF-8 form.</summary>
    [Fact]
    public void Csv_NullMarkerWithLoneSurrogate_ShouldBeRefused()
    {
        var act = () => WriteCsv(new CsvStreamOptions { NullMarker = "\uD800" });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CSV NULL marker cannot contain a lone surrogate*");
    }

    /// <summary>A projection with no columns has nothing to write as CSV.</summary>
    [Fact]
    public void Csv_ColumnlessProjection_ShouldBeRefusedBeforeAnyRow()
    {
        using var destination = new MemoryStream();

        var act = () => Context.From<S3StRow>()
            .Select(x => new S3StEmptyDto())
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no projected columns*nothing to write as CSV*");
    }

    /// <summary>A converted enum column has no box-free CSV formatter and is rejected by name.</summary>
    [Fact]
    public void Csv_ConvertedEnumColumn_ShouldBeRefusedByName()
    {
        using var destination = new MemoryStream();

        var act = () => Context.From<S3StEnumRow>()
            .Select(x => new { x.Id, x.State })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot format the column 'State'*S3StState*");
    }

    // ---------------------------------------------------------------------------------------------
    // JSON shape guards and the string-enum form
    // ---------------------------------------------------------------------------------------------

    /// <summary>A <see cref="TimeSpan"/> column is outside the JSON streaming whitelist and fails before output.</summary>
    [Fact]
    public void Json_TimeSpanProjection_ShouldBeRefusedBeforeOutput()
    {
        using var destination = new MemoryStream();

        var act = () => Context.From<S3StRow>()
            .Where(x => x.Id == 1)
            .Select(x => new { Id = x.Id, Span = TimeSpan.FromMinutes(1) })
            .WriteJson(destination);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Column 'Span' has type System.TimeSpan*not supported by JSON streaming*");
        destination.Length.Should().Be(0, "validation runs before the first byte is written");
    }

    /// <summary>
    /// A value-converted column cannot be JSON-streamed in phase 1; the guard names the column and the
    /// provider-representation workaround before any byte is written.
    /// </summary>
    [Fact]
    public void Json_ValueConvertedColumn_ShouldBeRefusedBeforeOutput()
    {
        using var destination = new MemoryStream();

        var act = () => Context.From<S3StEnumRow>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.State })
            .WriteJson(destination);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Column 'State' is value-converted*cannot be streamed as JSON*");
        destination.Length.Should().Be(0);
    }

    private void WriteCsv(CsvStreamOptions options)
    {
        using var destination = new MemoryStream();
        Context.From<S3StRow>()
            .Select(x => new { x.Id, x.Name })
            .WriteCsv(destination, options, TestContext.Current.CancellationToken);
    }

    /// <summary>Marker table of the plain CSV facts.</summary>
    [SqlTable("s3_st_row")]
    public sealed class S3StRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("note")]
        public string? Note { get; set; }
    }

    /// <summary>Marker table of the converted-enum facts (nextorm converter and STJ string enum).</summary>
    [SqlTable("s3_st_enum")]
    public sealed class S3StEnumRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("state")]
        [ValueConverter(typeof(EnumToStringConverter<S3StState>))]
        public S3StState State { get; set; }
    }

    /// <summary>
    /// Enum stored as its name by the nextorm value converter and declared with the stock STJ string
    /// converter on the type, so the JSON shape classifies it as <c>EnumString</c>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum S3StState
    {
        Active,
        Closed,
    }

    /// <summary>A projection type with no members at all.</summary>
    public sealed class S3StEmptyDto
    {
    }
}

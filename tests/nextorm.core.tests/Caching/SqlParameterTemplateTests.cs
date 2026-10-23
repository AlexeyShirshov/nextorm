using System.Data;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Model tests for the immutable parameter-occurrence template and its render-local recorder. They
/// exercise pure composition, fragment relocation and every construction/finalization guard without a
/// database, a container or any emitter/planner wiring.
/// </summary>
public class SqlParameterTemplateTests
{
    private const string Placeholder = "?0";

    [Fact]
    public void Template_Without_Slots_Reconstructs_Original_Text()
    {
        var recorder = new SqlParameterTemplateRecorder();
        recorder.AppendText("SELECT 1");

        var template = recorder.Finalize("SELECT 1");

        template.BindingCount.Should().Be(0);
        template.OccurrenceCount.Should().Be(0);
        template.Segments.Length.Should().Be(1);
        template.Segments[0].Kind.Should().Be(SqlParameterTemplateSegmentKind.Text);
        template.ReconstructOriginalText().Should().Be("SELECT 1");
        template.Compose(static (in SqlParameterOccurrence _) => null).Should().Be("SELECT 1");
    }

    [Fact]
    public void Template_With_No_Segments_Reconstructs_The_Empty_Statement()
    {
        var template = new SqlParameterTemplate(string.Empty, [], []);

        template.Segments.Length.Should().Be(0);
        template.ReconstructOriginalText().Should().BeEmpty();
    }

    [Theory]
    [InlineData("", " FROM t")]
    [InlineData("SELECT ", " FROM t")]
    [InlineData("SELECT a ", "")]
    public void Single_Slot_At_Start_Middle_Or_End_Reconstructs_Original_Text(string prefix, string suffix)
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText(prefix);
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        recorder.AppendText(suffix);
        var text = prefix + Placeholder + suffix;

        var template = recorder.Finalize(text);

        template.BindingCount.Should().Be(1);
        template.OccurrenceCount.Should().Be(1);
        template.Occurrences[0].Start.Should().Be(prefix.Length);
        template.Occurrences[0].Length.Should().Be(Placeholder.Length);
        template.Occurrences[0].End.Should().Be(prefix.Length + Placeholder.Length);
        template.StatementText.Should().Be(text);
        template.ReconstructOriginalText().Should().Be(text);
    }

    [Fact]
    public void Adjacent_Slots_Produce_No_Empty_Text_Segment()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        const string text = Placeholder + Placeholder;

        var template = recorder.Finalize(text);

        template.Occurrences.Length.Should().Be(2);
        template.Segments.Length.Should().Be(2);
        template.Segments[0].Kind.Should().Be(SqlParameterTemplateSegmentKind.Occurrence);
        template.Segments[1].Kind.Should().Be(SqlParameterTemplateSegmentKind.Occurrence);
        template.ReconstructOriginalText().Should().Be(text);
    }

    [Fact]
    public void Repeated_Occurrences_Reference_One_Binding()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("x = ");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        recorder.AppendText(" OR y = ");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        const string text = "x = ?0 OR y = ?0";

        var template = recorder.Finalize(text);

        template.BindingCount.Should().Be(1);
        template.OccurrenceCount.Should().Be(2);
        template.Occurrences[0].BindingId.Should().Be(bindingId);
        template.Occurrences[1].BindingId.Should().Be(bindingId);
        template.Occurrences[0].OccurrenceId.Should().NotBe(template.Occurrences[1].OccurrenceId);
        template.ReconstructOriginalText().Should().Be(text);
    }

    [Fact]
    public void Nested_Fragment_Occurrences_Are_Shifted_By_The_Parent_Offset()
    {
        var child = new SqlParameterTemplateRecorder();
        var childBindingId = child.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        child.AppendText("a");
        child.RecordPlaceholder(childBindingId, SqlStringLiteralRole.Value, Placeholder);
        child.AppendText("b");
        const string childText = "a?0b";

        var parent = new SqlParameterTemplateRecorder();
        parent.AppendText("SELECT ");
        var firstOffset = parent.Length;
        parent.AppendFragment(child);
        parent.AppendText(",");
        var secondOffset = parent.Length;
        parent.AppendFragment(child);
        parent.AppendText(" FROM t");
        var text = "SELECT " + childText + "," + childText + " FROM t";

        var template = parent.Finalize(text);

        template.BindingCount.Should().Be(1);
        template.OccurrenceCount.Should().Be(2);
        template.Occurrences[0].Start.Should().Be(firstOffset + 1);
        template.Occurrences[0].BindingId.Should().Be(0);
        template.Occurrences[1].Start.Should().Be(secondOffset + 1);
        template.Occurrences[1].BindingId.Should().Be(0);
        template.Occurrences[0].OccurrenceId.Should().Be(0);
        template.Occurrences[1].OccurrenceId.Should().Be(1);
        template.ReconstructOriginalText().Should().Be(text);
    }

    [Fact]
    public void Compose_Keeps_Unchanged_Placeholders_And_Replaces_Selected_Occurrence()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("SELECT ");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        recorder.AppendText(" WHERE x = ");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        recorder.AppendText(" AND y = ");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        const string text = "SELECT ?0 WHERE x = ?0 AND y = ?0";
        var template = recorder.Finalize(text);

        var replacements = new string?[template.OccurrenceCount];
        replacements[template.IndexOfOccurrence(1)] = "_utf8mb4 X'41'";

        var composed = template.Compose(replacements);

        composed.Should().Be("SELECT ?0 WHERE x = _utf8mb4 X'41' AND y = ?0");
        template.StatementText.Should().Be(text);
        template.ReconstructOriginalText().Should().Be(text);

        var composedByDelegate = template.Compose(
            static (in SqlParameterOccurrence occurrence) =>
                occurrence.OccurrenceId == 1 ? "_utf8mb4 X'41'" : null);

        composedByDelegate.Should().Be(composed);
    }

    [Fact]
    public void Compose_With_All_Placeholders_Kept_Is_Byte_Identical_To_The_Original_Text()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("SELECT /* keep */ ");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, "+0");
        recorder.AppendText(" -- tail");
        const string text = "SELECT /* keep */ +0 -- tail";

        var template = recorder.Finalize(text);

        template.Compose(new string?[template.OccurrenceCount]).Should().Be(text);
    }

    [Fact]
    public void Compose_Should_Reject_A_Replacement_Count_Mismatch()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        var template = recorder.Finalize(Placeholder);

        FluentActions.Invoking(() => template.Compose(new string?[2]))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Template_Should_Reject_Overlapping_Occurrence_Spans()
    {
        var bindings = new[] { new SqlParameterBinding(0, "p0", SqlParameterBindingOrigin.Captured) };
        var occurrences = new[]
        {
            new SqlParameterOccurrence(0, 0, SqlStringLiteralRole.Value, 0, 2),
            new SqlParameterOccurrence(1, 0, SqlStringLiteralRole.Value, 1, 2),
        };

        FluentActions.Invoking(() => new SqlParameterTemplate("?0?0", occurrences, bindings))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Template_Should_Reject_Duplicate_Binding_Names()
    {
        var bindings = new[]
        {
            new SqlParameterBinding(0, "p0", SqlParameterBindingOrigin.Captured),
            new SqlParameterBinding(1, "p0", SqlParameterBindingOrigin.Stable),
        };

        FluentActions.Invoking(() => new SqlParameterTemplate(string.Empty, [], bindings))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Recorder_Should_Reject_A_Duplicate_Binding_Registration()
    {
        var recorder = new SqlParameterTemplateRecorder();
        recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);

        FluentActions.Invoking(() => recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Stable))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Template_Should_Reject_A_Binding_Id_That_Is_Not_The_Canonical_Ordinal()
    {
        var bindings = new[] { new SqlParameterBinding(1, "p0", SqlParameterBindingOrigin.Captured) };

        FluentActions.Invoking(() => new SqlParameterTemplate(string.Empty, [], bindings))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Finalize_Should_Reject_An_Unresolvable_Binding_Id()
    {
        var recorder = new SqlParameterTemplateRecorder();
        recorder.RecordPlaceholder(7, SqlStringLiteralRole.Value, Placeholder);

        FluentActions.Invoking(() => recorder.Finalize(Placeholder))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Finalize_Should_Reject_A_Non_Contiguous_Recording()
    {
        var recorder = new SqlParameterTemplateRecorder();
        recorder.AppendText("abc");

        FluentActions.Invoking(() => recorder.Finalize("ab"))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => recorder.Finalize("abcd"))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Finalize_Should_Seal_The_Recorder()
    {
        var recorder = new SqlParameterTemplateRecorder();
        recorder.AppendText("abc");
        recorder.Finalize("abc");

        FluentActions.Invoking(() => recorder.AppendText("d"))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Template_Should_Reject_Duplicate_Occurrence_Ids()
    {
        var bindings = new[] { new SqlParameterBinding(0, "p0", SqlParameterBindingOrigin.Captured) };
        var occurrences = new[]
        {
            new SqlParameterOccurrence(0, 0, SqlStringLiteralRole.Value, 0, 1),
            new SqlParameterOccurrence(0, 0, SqlStringLiteralRole.Value, 1, 1),
        };

        FluentActions.Invoking(() => new SqlParameterTemplate("?0", occurrences, bindings))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fragment_Should_Reject_A_Conflicting_Binding()
    {
        var child = new SqlParameterTemplateRecorder();
        child.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        var parent = new SqlParameterTemplateRecorder();
        parent.RegisterBinding("p0", SqlParameterBindingOrigin.Stable);

        FluentActions.Invoking(() => parent.AppendFragment(child))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Runtime_Binding_Requires_A_Runtime_Argument_Ordinal()
    {
        FluentActions.Invoking(() => new SqlParameterBinding(0, "p0", SqlParameterBindingOrigin.Runtime))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new SqlParameterBinding(0, "p0", SqlParameterBindingOrigin.Captured, runtimeArgIndex: 0))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Hex_Payload_Size_Accounting_Uses_Checked_Arithmetic()
    {
        SqlParameterTemplate.AccumulateHexPayloadSize(0, 1).Should().Be(SqlParameterTemplate.HexLiteralSyntaxOverhead + 2);
        SqlParameterTemplate.AccumulateHexPayloadSize(10, 0).Should().Be(10 + SqlParameterTemplate.HexLiteralSyntaxOverhead);

        FluentActions.Invoking(() => SqlParameterTemplate.AccumulateHexPayloadSize(long.MaxValue, 1))
            .Should().Throw<OverflowException>();
        FluentActions.Invoking(() => SqlParameterTemplate.AccumulateHexPayloadSize(-1, 1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Binding_Should_Carry_The_Provider_Type_Metadata()
    {
        var binding = new SqlParameterBinding(
            0,
            "p0",
            SqlParameterBindingOrigin.Captured,
            hasConversion: true,
            dbType: DbType.Decimal,
            precision: 18,
            scale: 4);

        binding.BindingId.Should().Be(0);
        binding.Name.Should().Be("p0");
        binding.Origin.Should().Be(SqlParameterBindingOrigin.Captured);
        binding.RuntimeArgIndex.Should().BeNull();
        binding.HasConversion.Should().BeTrue();
        binding.DbType.Should().Be(DbType.Decimal);
        binding.Precision.Should().Be(18);
        binding.Scale.Should().Be(4);
    }
}

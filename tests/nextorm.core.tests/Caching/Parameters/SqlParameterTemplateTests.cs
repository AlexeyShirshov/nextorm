using System.Data;
using System.Text;
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

    // ------------------------------------------------------------------------------------------------
    // #327 DEF-04: rewind checkpoints. A renderer that emits a fragment into the shared builder and
    // then truncates it back hands the fragment to the recorder instead of leaving it ahead of the
    // builder; the wrapper that re-embeds the fragment relocates its occurrences to their real offsets.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void MarkAt_And_RewindTo_Should_Restore_The_Parent_And_Detach_The_Fragment()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("select ");

        var mark = recorder.MarkAt(recorder.Length);
        recorder.AppendText("(");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        recorder.AppendText(")");

        var fragment = recorder.RewindTo(mark);

        recorder.Length.Should().Be(mark.Length);
        recorder.OccurrenceCount.Should().Be(mark.OccurrenceCount);
        recorder.BindingCount.Should().Be(1, "a rewind detaches occurrences, never canonical bindings");
        fragment.Length.Should().Be(4);
        fragment.OccurrenceCount.Should().Be(1);

        var template = recorder.Finalize("select ");
        template.OccurrenceCount.Should().Be(0);
        template.ReconstructOriginalText().Should().Be("select ");
    }

    [Fact]
    public void Detached_Fragment_Should_Rebase_Its_Occurrence_Spans_To_Zero()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("select abs(");

        var mark = recorder.MarkAt(recorder.Length);
        recorder.AppendText("-");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        var fragment = recorder.RewindTo(mark);

        var bindings = new[] { new SqlParameterBinding(0, "p0", SqlParameterBindingOrigin.Captured) };
        var template = fragment.Finalize("-" + Placeholder, bindings);

        template.OccurrenceCount.Should().Be(1);
        template.Occurrences[0].Start.Should().Be(1, "the detached spans start where the truncated fragment started");
        template.Occurrences[0].Length.Should().Be(Placeholder.Length);
        template.Occurrences[0].BindingId.Should().Be(0);
        template.Occurrences[0].OccurrenceId.Should().Be(0);
        template.ReconstructOriginalText().Should().Be("-" + Placeholder);
    }

    [Fact]
    public void AppendComposed_Should_Relocate_A_Slot_Behind_The_Wrapper_Prefix()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        var builder = new StringBuilder();
        recorder.AppendText("select ");
        builder.Append("select ");

        var mark = recorder.MarkAt(recorder.Length);
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        var fragment = recorder.RewindTo(mark);

        var wrapper = "arrayFilter(x -> x > " + Placeholder + ", nums)";
        SqlParameterFragmentRelocation.AppendComposed(
            builder,
            recorder,
            wrapper,
            [new SqlParameterTrackedFragment(Placeholder, null, fragment)]);

        var text = "select " + wrapper;
        builder.ToString().Should().Be(text);

        var template = recorder.Finalize(text);
        template.OccurrenceCount.Should().Be(1);
        var occurrence = template.Occurrences[0];
        occurrence.Start.Should().Be(text.IndexOf(Placeholder, StringComparison.Ordinal));
        occurrence.Length.Should().Be(Placeholder.Length);
        occurrence.Role.Should().Be(SqlStringLiteralRole.Value);
        occurrence.BindingId.Should().Be(bindingId);
        template.Bindings[occurrence.BindingId].Name.Should().Be("p0");
        template.Compose([null]).Should().Be(text);
    }

    [Fact]
    public void AppendComposed_Should_Relocate_Two_Captured_Arguments_In_Order()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var first = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        var second = recorder.RegisterBinding("p1", SqlParameterBindingOrigin.Captured);
        var builder = new StringBuilder();
        recorder.AppendText("select ");
        builder.Append("select ");

        var fragments = new List<SqlParameterTrackedFragment>(2);
        foreach (var (bindingId, placeholder) in new[] { (first, "@p0"), (second, "@p1") })
        {
            var mark = recorder.MarkAt(recorder.Length);
            recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, placeholder);
            fragments.Add(new SqlParameterTrackedFragment(placeholder, null, recorder.RewindTo(mark)));
        }

        var wrapper = "arrayConcat(" + fragments[0].AppendedText + ", " + fragments[1].AppendedText + ")";
        SqlParameterFragmentRelocation.AppendComposed(builder, recorder, wrapper, fragments);

        var text = "select " + wrapper;
        builder.ToString().Should().Be(text);

        var template = recorder.Finalize(text);
        template.OccurrenceCount.Should().Be(2);
        template.Occurrences[0].Start.Should().Be(text.IndexOf("@p0", StringComparison.Ordinal));
        template.Occurrences[0].BindingId.Should().Be(first);
        template.Occurrences[1].Start.Should().Be(text.IndexOf("@p1", StringComparison.Ordinal));
        template.Occurrences[1].BindingId.Should().Be(second);
        template.Compose([null, null]).Should().Be(text);
    }

    [Fact]
    public void AppendComposed_Should_Relocate_Repeated_Identical_Fragment_Text_One_After_Another()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        var builder = new StringBuilder();
        recorder.AppendText("select ");
        builder.Append("select ");

        var fragments = new List<SqlParameterTrackedFragment>(2);
        for (var i = 0; i < 2; i++)
        {
            var mark = recorder.MarkAt(recorder.Length);
            recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
            fragments.Add(new SqlParameterTrackedFragment(Placeholder, null, recorder.RewindTo(mark)));
        }

        var wrapper = "least(" + Placeholder + ", " + Placeholder + ")";
        SqlParameterFragmentRelocation.AppendComposed(builder, recorder, wrapper, fragments);

        var text = "select " + wrapper;
        var template = recorder.Finalize(text);

        template.OccurrenceCount.Should().Be(2, "the second copy must not alias the first");
        template.Occurrences[0].Start.Should().Be(text.IndexOf(Placeholder, StringComparison.Ordinal));
        template.Occurrences[1].Start.Should().Be(text.LastIndexOf(Placeholder, StringComparison.Ordinal));
        template.Occurrences[0].BindingId.Should().Be(bindingId);
        template.Occurrences[1].BindingId.Should().Be(bindingId);
        template.Compose([null, null]).Should().Be(text);
    }

    [Fact]
    public void Nested_Rewind_Should_Keep_The_Inner_Occurrence_Offset_Through_Both_Wrappers()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        var builder = new StringBuilder();
        recorder.AppendText("select ");
        builder.Append("select ");

        var outerMark = recorder.MarkAt(recorder.Length);
        var innerMark = recorder.MarkAt(recorder.Length);
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        var innerFragment = recorder.RewindTo(innerMark);
        SqlParameterFragmentRelocation.AppendComposed(
            builder,
            recorder,
            "abs(" + Placeholder + ")",
            [new SqlParameterTrackedFragment(Placeholder, null, innerFragment)]);

        var innerText = builder.ToString("select ".Length, builder.Length - "select ".Length);
        builder.Length = "select ".Length;
        var outerFragment = recorder.RewindTo(outerMark);

        var wrapper = "toInt32(" + innerText + ")";
        SqlParameterFragmentRelocation.AppendComposed(
            builder,
            recorder,
            wrapper,
            [new SqlParameterTrackedFragment(innerText, null, outerFragment)]);

        var text = "select " + wrapper;
        builder.ToString().Should().Be(text);

        var template = recorder.Finalize(text);
        template.OccurrenceCount.Should().Be(1);
        template.Occurrences[0].Start.Should().Be(text.IndexOf(Placeholder, StringComparison.Ordinal));
        template.Occurrences[0].Length.Should().Be(Placeholder.Length);
        template.Occurrences[0].BindingId.Should().Be(bindingId);
        template.Compose([null]).Should().Be(text);
    }

    [Fact]
    public void MarkAt_Should_Reject_A_Position_Before_The_Recorded_Length()
    {
        var recorder = new SqlParameterTemplateRecorder();
        recorder.AppendText("select ");
        var shrink = recorder.Length - 1;

        FluentActions.Invoking(() => recorder.MarkAt(shrink))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RewindTo_Should_Reject_An_Occurrence_Straddling_The_Mark()
    {
        var recorder = new SqlParameterTemplateRecorder();
        recorder.AppendText("select ");
        var mark = recorder.MarkAt(recorder.Length);

        // A merged fragment may carry spans that start before the mark (relocation does not advance
        // the cursor); detaching such an occurrence would silently misplace it, so the rewind refuses.
        var child = new SqlParameterTemplateRecorder();
        child.RecordPlaceholder(0, SqlStringLiteralRole.Value, Placeholder);
        recorder.AppendOccurrences(child, 0);

        FluentActions.Invoking(() => recorder.RewindTo(mark))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TrimTo_Should_Drop_A_TextOnly_Tail()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("a");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);
        recorder.AppendText(", ");

        recorder.TrimTo(recorder.Length - 2);

        recorder.Length.Should().Be(3);
        recorder.OccurrenceCount.Should().Be(1);

        var template = recorder.Finalize("a" + Placeholder);
        template.OccurrenceCount.Should().Be(1);
        template.Occurrences[0].Start.Should().Be(1);
    }

    [Fact]
    public void TrimTo_Should_Reject_A_Trim_That_Cuts_Through_A_Placeholder()
    {
        var recorder = new SqlParameterTemplateRecorder();
        var bindingId = recorder.RegisterBinding("p0", SqlParameterBindingOrigin.Captured);
        recorder.AppendText("a");
        recorder.RecordPlaceholder(bindingId, SqlStringLiteralRole.Value, Placeholder);

        FluentActions.Invoking(() => recorder.TrimTo(2))
            .Should().Throw<InvalidOperationException>();
    }
}

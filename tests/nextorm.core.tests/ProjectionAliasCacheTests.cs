using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Focused unit coverage for the join-slot member parse at
/// <see cref="ProjectionAliasCache"/>: an engine projection member is <c>Item</c> followed by one or
/// more ASCII digits; a generated lexical alias that merely *ends* in a digit (for example
/// <c>Buyer2</c>) must never be misread as a positional slot before its
/// <see cref="JoinSlotAttribute"/> is consulted.
/// </summary>
public class ProjectionAliasCacheTests
{
    [Theory]
    [InlineData("Item1", true, 1)]
    [InlineData("Item2", true, 2)]
    [InlineData("Item8", true, 8)]
    [InlineData("Item9", true, 9)]
    [InlineData("Item10", true, 10)]
    [InlineData("Item0", true, 0)]
    public void TryParseItemPosition_accepts_Item_followed_by_digits(string name, bool expected, int expectedPosition)
    {
        ProjectionAliasCache.TryParseItemPosition(name, out var position).Should().Be(expected);
        position.Should().Be(expectedPosition);
    }

    [Theory]
    [InlineData("Item")]        // exactly the 4-char prefix, no digits
    [InlineData("Items")]       // digit position is a letter
    [InlineData("ItemX")]
    [InlineData("Item2b")]      // trailing non-digit
    [InlineData("Item 2")]      // embedded space
    [InlineData("Item-1")]
    [InlineData("item2")]       // case-sensitive prefix
    [InlineData("buyer2")]      // digit-ending alias
    [InlineData("Buyer2")]
    [InlineData("StoredBuyer2")]
    [InlineData("Approver10")]
    [InlineData("It3m2")]       // 'Item' interrupted by a digit
    [InlineData("I2")]
    [InlineData("")]
    public void TryParseItemPosition_rejects_anything_but_an_exact_ItemN_shape(string name)
    {
        ProjectionAliasCache.TryParseItemPosition(name, out var position).Should().BeFalse();
        position.Should().Be(0);
    }

    [Fact]
    public void GetMemberPosition_maps_a_digit_ending_alias_by_its_JoinSlotAttribute_not_by_the_trailing_digit()
    {
        // 'Buyer2' ends in a digit but is a generated lexical alias in slot 3, not 'Item2'.
        var buyer2 = typeof(DigitEndingAliases).GetProperty(nameof(DigitEndingAliases.Buyer2))!;
        ProjectionAliasCache.GetMemberPosition(buyer2).Should().Be(2);

        var approver10 = typeof(DigitEndingAliases).GetProperty(nameof(DigitEndingAliases.Approver10))!;
        ProjectionAliasCache.GetMemberPosition(approver10).Should().Be(4);

        // A member with neither an ItemN shape nor a JoinSlotAttribute carries no occurrence hint.
        var plain3 = typeof(DigitEndingAliases).GetProperty(nameof(DigitEndingAliases.Plain3))!;
        ProjectionAliasCache.GetMemberPosition(plain3).Should().Be(-1);
    }

    [Fact]
    public void GetMemberPosition_parses_ItemN_before_probing_JoinSlotAttribute()
    {
        // The cheap ItemN parse runs first: 'Item2' resolves to zero-based slot 1 even though the
        // (artificial) attribute on it claims position 5.
        var item2 = typeof(DigitEndingAliases).GetProperty(nameof(DigitEndingAliases.Item2))!;
        ProjectionAliasCache.GetMemberPosition(item2).Should().Be(1);
    }

    private sealed class DigitEndingAliases
    {
        [JoinSlot(3)]
        public int Buyer2 => 0;

        [JoinSlot(5)]
        public int Approver10 => 0;

        [JoinSlot(5)]
        public int Item2 => 0;

        public int Plain3 => 0;
    }
}

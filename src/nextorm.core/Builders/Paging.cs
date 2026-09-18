namespace nextorm.core;

/// <summary>
/// Limit/offset pair describing a single page request.
/// </summary>
/// <remarks>
/// Exposes public mutable fields rather than properties, so values cannot be validated and cannot be
/// evolved without a breaking change. Consider making the type <c>internal</c>, or replacing the
/// fields with validated properties. See <c>API-NAMING-REVIEW.md</c> finding P2-19.
/// </remarks>
public struct Paging
{
    public int Limit;
    public int Offset;
    public readonly bool IsEmpty => Offset <= 0 && Limit <= 0;
    public readonly bool IsTop => Offset <= 0 && Limit > 0;
}
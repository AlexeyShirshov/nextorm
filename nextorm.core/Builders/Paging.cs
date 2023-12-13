namespace nextorm.core;
public struct Paging
{
    public int Limit { get; set; }
    public int Offset { get; set; }
    public readonly bool IsEmpty => Offset <= 0 && Limit <= 0;
}
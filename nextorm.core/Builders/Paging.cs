namespace nextorm.core;
public struct Paging
{
    public int Limit;
    public int Offset;
    public readonly bool IsEmpty => Offset <= 0 && Limit <= 0;
}
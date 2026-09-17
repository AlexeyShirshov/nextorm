namespace nextorm.core;

/// <summary>
/// The kind of set operation joining two queries. <see cref="None = 0"/> and the original
/// <see cref="Distinct"/>/<see cref="All"/> values keep their numeric assignment so existing
/// serialized values and plan hashes stay stable; the INTERSECT/EXCEPT members are appended.
/// </summary>
public enum UnionType
{
    None = 0,
    Distinct = 1,
    All = 2,
    Intersect = 3,
    IntersectAll = 4,
    Except = 5,
    ExceptAll = 6
}

namespace NextORM.Core.Tests;

/// <summary>
/// T291 (#291) in-memory AVG fixture: the same column layout as the integration fixture, without any
/// database mapping attributes (the in-memory provider maps plain CLR objects).
/// </summary>
public class AvgFixture
{
    public int Id { get; set; }

    public byte B { get; set; }

    public short S { get; set; }

    public int I { get; set; }

    public long L { get; set; }

    public byte? NB { get; set; }

    public short? NS { get; set; }

    public int? NI { get; set; }

    public long? NL { get; set; }
}

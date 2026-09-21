namespace NextORM.Integration.Tests;

public class SimpleEntityDTO
{
    public SimpleEntityDTO(long id)
    {
        Id = id;
    }
    public long Id { get; set; }
}

internal record SimpleEntityRecord(long Id)
{
}

public class Cls
{
    public int Id { get; set; }
    public string? Str { get; set; }
}

public class cls1
{
    public cls1(int id)
    {
        Id = id;
    }

    public long Id { get; set; }
    public string OtherValue { get; set; } = "h";
}

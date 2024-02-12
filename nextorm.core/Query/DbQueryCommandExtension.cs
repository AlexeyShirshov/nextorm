namespace nextorm.core;

public class DbQueryCommandExtension
{
    public string? ManualSql;
    public Func<List<Param>>? MakeParams;
}
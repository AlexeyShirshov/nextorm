using nextorm.core;

namespace nextorm.sqlite.tests;

public class DataContext
{
    public CommandBuilder<ISimpleEntity> SimpleEntity {get;set;}=new(new SqliteClient(@"Data Source='C:\Users\alexey.CORP\sources\repos\nextorm\nextorm.sqlite.tests\bin\Debug\net7.0\data\test.db'"));
}
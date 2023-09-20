namespace nextorm.core;

public class ScalarSqlCommand : SqlCommand<ValueType>
{
    public ScalarSqlCommand(SqlClient sqlClient) : base(sqlClient)
    {
    }
}

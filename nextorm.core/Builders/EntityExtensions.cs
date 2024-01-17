namespace nextorm.core;

public static class EntityExtensions
{
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this Entity<TResult> entity, string sql) => entity.ToCommand().PrepareFromSql(sql);
}
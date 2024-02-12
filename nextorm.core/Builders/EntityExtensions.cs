namespace nextorm.core;

public static class EntityExtensions
{
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this Entity<TResult> entity, string sql) => entity.ToCommand().PrepareFromSql(sql);
    public static QueryCommand<TResult> WithSql<TResult>(this Entity<TResult> entity, string sql) => WithSql(entity, sql, null);
    public static QueryCommand<TResult> WithSql<TResult>(this Entity<TResult> entity, string sql, object? @params) => entity.ToCommand().WithSql(sql, @params);
}
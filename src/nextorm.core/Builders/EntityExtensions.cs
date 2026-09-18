namespace nextorm.core;

/// <summary>
/// Convenience extension methods over the query builders.
/// </summary>
public static class EntityExtensions
{
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this EntityBuilder<TResult> entity, string sql) => entity.ToCommand().PrepareFromSql(sql);
    public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql) => WithSql(entity, sql, null);
    public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql, object? @params) => entity.ToCommand().WithSql(sql, @params);
}
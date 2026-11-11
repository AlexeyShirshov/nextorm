namespace NextORM.Core;

/// <summary>
/// Convenience extension methods over the query builders.
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    /// Prepares the entity's query with the raw <paramref name="sql"/> and returns a reusable prepared
    /// command, so the statement is compiled once and can be executed repeatedly.
    /// </summary>
    /// <typeparam name="TResult">The entity type the query selects.</typeparam>
    /// <param name="entity">The builder whose query receives the raw SQL.</param>
    /// <param name="sql">The SQL statement to prepare.</param>
    /// <returns>A command that can be executed repeatedly.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this EntityBuilder<TResult> entity, string sql) => entity.ToCommand().PrepareFromSql(sql);
    /// <summary>
    /// Returns the entity's command with its generated SQL replaced by the raw <paramref name="sql"/>
    /// (no parameters are bound; use the overload that takes <c>params</c> for those).
    /// </summary>
    /// <typeparam name="TResult">The entity type the query selects.</typeparam>
    /// <param name="entity">The builder whose query receives the raw SQL.</param>
    /// <param name="sql">The SQL statement to run instead of the generated one.</param>
    /// <returns>A command that runs the raw SQL.</returns>
    public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql) => WithSql(entity, sql, null);
    /// <summary>
    /// Returns the entity's command with its generated SQL replaced by the raw <paramref name="sql"/>
    /// and the public properties of <paramref name="params"/> bound as named parameters.
    /// </summary>
    /// <typeparam name="TResult">The entity type the query selects.</typeparam>
    /// <param name="entity">The builder whose query receives the raw SQL.</param>
    /// <param name="sql">The SQL statement to run instead of the generated one.</param>
    /// <param name="params">An object whose public properties become the statement parameters; <c>null</c> binds none.</param>
    /// <returns>A command that runs the raw SQL.</returns>
    public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql, object? @params) => entity.ToCommand().WithSql(sql, @params);
}
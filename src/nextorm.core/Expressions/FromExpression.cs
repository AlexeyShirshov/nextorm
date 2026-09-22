namespace NextORM.Core;

public sealed class FromExpression
{
     public FromExpression(Type srcType) => SourceType = srcType;
     public FromExpression(string table, bool isAutoMapped = false, bool sourceIsInterface = false)
     {
          Table = table;
          IsAutoMapped = isAutoMapped;
          SourceIsInterface = sourceIsInterface;
     }
     public FromExpression(QueryCommand subQuery)
     {
          SubQuery = subQuery;
     }
     public FromExpression(TableFunctionExpression tableFunction)
     {
          TableFunction = tableFunction;
     }
     public FromExpression(PivotExpression pivot)
     {
          Pivot = pivot;
     }
     internal FromExpression(LinqSourceExpression linqSource)
     {
          LinqSource = linqSource;
     }
     internal FromExpression(RawSqlSourceExpression rawSqlSource)
     {
          RawSqlSource = rawSqlSource;
     }
     //public OneOf<string, QueryCommand> Table { get; }
     public readonly string? Table;
     /// <summary>
     /// Whether <see cref="Table"/> was derived from the entity type name (and is therefore subject
     /// to the active <see cref="INamingConvention"/>) rather than declared explicitly.
     /// </summary>
     public readonly bool IsAutoMapped;
     /// <summary>Whether the entity source type is an interface, so the convention can drop its <c>I</c> prefix.</summary>
     public readonly bool SourceIsInterface;
     public readonly QueryCommand? SubQuery;
     public readonly Type? SourceType;
     /// <summary>
     /// Set when the source is a table-valued function. Mutually exclusive with <see cref="Table"/>
     /// and <see cref="SubQuery"/>.
     /// </summary>
     public readonly TableFunctionExpression? TableFunction;
     /// <summary>
     /// Set when the source is a <c>PIVOT</c>/<c>UNPIVOT</c> over <see cref="PivotExpression.Inner"/>.
     /// Mutually exclusive with <see cref="Table"/>, <see cref="SubQuery"/>, <see cref="TableFunction"/>
     /// and <see cref="LinqSource"/>.
     /// </summary>
     public readonly PivotExpression? Pivot;
     /// <summary>
     /// Set when the source is produced by <c>SelectMany</c>/<c>GroupJoin</c>. In-memory only: the SQL
     /// providers reject it. Mutually exclusive with <see cref="Table"/>, <see cref="SubQuery"/> and
     /// <see cref="TableFunction"/>.
     /// </summary>
     internal readonly LinqSourceExpression? LinqSource;
     /// <summary>
     /// Set when the source is a raw SQL fragment rendered as a derived table. Mutually exclusive with
     /// <see cref="Table"/>, <see cref="SubQuery"/>, <see cref="TableFunction"/>, <see cref="Pivot"/> and
     /// <see cref="LinqSource"/>.
     /// </summary>
     internal readonly RawSqlSourceExpression? RawSqlSource;

     // public override int GetHashCode()
     // {
     //      unchecked
     //      {
     //           var hash = new XxHash32();

     //           hash.Add(TableAlias);

     //           hash.Add(Table.GetHashCode());

     //           return hash.ToHashCode();
     //      }
     // }
     // public override bool Equals(object? obj)
     // {
     //      return Equals(obj as FromExpression);
     // }
     // public bool Equals(FromExpression? obj)
     // {
     //      if (obj is null) return false;

     //      return TableAlias == obj.TableAlias && Table.Equals(obj.Table);
     // }
     internal FromExpression? CloneForCache()
     {
          // A table function's call expression is immutable and never mutated during preparation,
          // so it can be shared with the cached plan (like Table/SourceType) instead of cloned.
          if (Pivot is not null)
          {
               var pivot = Pivot.CloneForCache();
               return ReferenceEquals(pivot, Pivot) ? this : new FromExpression(pivot);
          }

          if (!string.IsNullOrEmpty(Table) || SourceType is not null || TableFunction is not null || LinqSource is not null || RawSqlSource is not null) return this;

          return new FromExpression(SubQuery!.CloneForCache());// { TableAlias = TableAlias };
     }
}
namespace NextORM.Core;

/// <summary>
/// Identifies a query source: a table name, an entity type, a subquery, a table-valued function, a
/// <c>PIVOT</c>/<c>UNPIVOT</c>, or one of the in-memory-only source kinds.
/// </summary>
public sealed class FromExpression
{
     /// <summary>Creates a source for a mapped CLR entity type.</summary>
     /// <param name="srcType">The entity type.</param>
     public FromExpression(Type srcType) => SourceType = srcType;
     /// <summary>Creates a source for a named table.</summary>
     /// <param name="table">The table name.</param>
     /// <param name="isAutoMapped">Whether <paramref name="table"/> was derived from the entity type name.</param>
     /// <param name="sourceIsInterface">Whether the entity source type is an interface.</param>
     public FromExpression(string table, bool isAutoMapped = false, bool sourceIsInterface = false)
     {
          Table = table;
          IsAutoMapped = isAutoMapped;
          SourceIsInterface = sourceIsInterface;
     }
     /// <summary>Creates a source from a derived-table subquery.</summary>
     /// <param name="subQuery">The subquery that produces the rows.</param>
     public FromExpression(QueryCommand subQuery)
     {
          SubQuery = subQuery;
     }
     /// <summary>
     /// Creates a source for a named table whose readable columns are described by
     /// <paramref name="columnShape"/> rather than by entity metadata. Used to read the rows a
     /// data-modifying CTE returns: the name is the CTE (<c>from ins as "t1"</c>) and the shape carries
     /// the <c>RETURNING</c> projection so member access resolves to the returned columns.
     /// </summary>
     /// <param name="table">The table/CTE name.</param>
     /// <param name="columnShape">A prepared command whose projection describes the readable columns.</param>
     internal FromExpression(string table, QueryCommand columnShape)
     {
          Table = table;
          ColumnShape = columnShape;
     }
     /// <summary>Creates a source from a lazy temporary-table materialisation.</summary>
     /// <param name="tempTable">The temporary table to materialise and read.</param>
     internal FromExpression(ITempTableSource tempTable)
     {
          TempTable = tempTable;
          Table = tempTable.Name;
     }
     /// <summary>Creates a source from a table-valued function.</summary>
     /// <param name="tableFunction">The table function call.</param>
     public FromExpression(TableFunctionExpression tableFunction)
     {
          TableFunction = tableFunction;
     }
     /// <summary>Creates a source from a <c>PIVOT</c>/<c>UNPIVOT</c>.</summary>
     /// <param name="pivot">The pivot definition.</param>
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
     internal FromExpression(XmlNodesExpression xmlNodes)
     {
          XmlNodes = xmlNodes;
     }
     //public OneOf<string, QueryCommand> Table { get; }
     /// <summary>The explicit table name, or <c>null</c> when the source is not a plain named table.</summary>
     public readonly string? Table;
     /// <summary>
     /// Whether <see cref="Table"/> was derived from the entity type name (and is therefore subject
     /// to the active <see cref="INamingConvention"/>) rather than declared explicitly.
     /// </summary>
     public readonly bool IsAutoMapped;
     /// <summary>Whether the entity source type is an interface, so the convention can drop its <c>I</c> prefix.</summary>
     public readonly bool SourceIsInterface;
     /// <summary>The derived-table subquery, or <c>null</c> when the source is not a subquery.</summary>
     public readonly QueryCommand? SubQuery;
     /// <summary>
     /// Set when the source is a lazy temporary table (see
     /// <see cref="TempTableExtensions.AsTempTable{TResult}(QueryCommand{TResult}, CreateTableOptions?)"/>).
     /// The physical <see cref="Table"/> name is the generated temporary-table name; the marker makes
     /// the context materialise the table in the same batch as the read. Mutually exclusive with the
     /// other source kinds.
     /// </summary>
     internal readonly ITempTableSource? TempTable;
     /// <summary>The entity type, used when the source is a mapped CLR type rather than a table name.</summary>
     public readonly Type? SourceType;
     /// <summary>
     /// Set when the source is a named table whose readable columns come from a projection rather than
     /// entity metadata (a data-modifying CTE read). The command supplies
     /// <see cref="QueryCommand.ResultType"/> and <see cref="QueryCommand.SelectList"/> to the column
     /// provider so member access resolves against the returned columns.
     /// </summary>
     internal readonly QueryCommand? ColumnShape;
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
     /// <summary>
     /// Set when the source is a SQL Server <c>xml.nodes()</c> rowset (a correlated
     /// <c>CROSS/OUTER APPLY</c> source). Mutually exclusive with <see cref="Table"/>,
     /// <see cref="SubQuery"/>, <see cref="TableFunction"/>, <see cref="Pivot"/>,
     /// <see cref="LinqSource"/> and <see cref="RawSqlSource"/>.
     /// </summary>
     internal readonly XmlNodesExpression? XmlNodes;

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

          if (!string.IsNullOrEmpty(Table) || SourceType is not null || TableFunction is not null || LinqSource is not null || RawSqlSource is not null || XmlNodes is not null) return this;

          return new FromExpression(SubQuery!.CloneForCache());// { TableAlias = TableAlias };
     }
}
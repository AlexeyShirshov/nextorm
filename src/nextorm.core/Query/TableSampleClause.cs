namespace NextORM.Core;

/// <summary>
/// The <c>TABLESAMPLE</c> table modifier attached to a query through <c>FromOptions.TableSample</c>:
/// the sampling algorithm, the percentage of the table to sample, and an optional repeatable seed.
/// </summary>
internal sealed record TableSampleClause(TableSampleMethod Method, double Percent, double? Seed);

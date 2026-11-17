# API XML-comment readability pass — 2026-11-16

## Status and scope

The first two passes recorded here reviewed selected existing XML comments in the nine DocFX projects.
The continuation results below record the remaining manual review. The edits improve existing
comments; they do not add API declarations, change behavior, or create public documentation pages.
The PublicAPI inventory and XML-fragment parser are coverage aids, not counts of manually reviewed
unique documentation blocks.

The current working-tree inventory is recorded below. The inventory is mechanical and does not mean
that every listed member or comment was manually reviewed.

All behavior statements were checked against the current working-tree source. This pass did not
use released NuGet packages, package installation, restore, or published versions as a validation
target.

## Review method

- Ran Roslyn `structure` over `nextorm.slnx`, then targeted `types` and `members` queries to locate
  the context builder, query context, query builders and terminals, provider configuration
  extensions, EF Core bridge, and extension-package entry points.
- Read the corresponding source comments after semantic discovery.
- Reviewed the exact source areas below. A Roslyn inventory or a read of one part of a file does not
  count as a review of every comment in that type or project.
- Re-scanned all nine DocFX source roots for C# files, existing `///` blocks, and PublicAPI entries.
  Parsed each comment block as XML and searched source comments for references to internal specs.
- The XML parser and text scans are inventory/validation evidence, not a substitute for manual
  readability review.

## Current source inventory

| Project | Tracked C# files | Files with `///` blocks | XML-doc blocks | PublicAPI entries |
|---|---:|---:|---:|---:|
| `nextorm.core` | 467 | 461 | 9,842 | 6,757 |
| `nextorm.sqlite` | 5 | 5 | 106 | 90 |
| `nextorm.sqlserver` | 8 | 7 | 278 | 199 |
| `nextorm.postgres` | 13 | 13 | 271 | 216 |
| `nextorm.mysql` | 3 | 3 | 143 | 119 |
| `nextorm.mariadb` | 3 | 3 | 42 | 32 |
| `nextorm.clickhouse` | 7 | 7 | 305 | 226 |
| `nextorm.entityframeworkcore` | 7 | 7 | 33 | 12 |
| `nextorm.extensions` | 4 | 4 | 66 | 36 |
| **Total** | **517** | **510** | **11,086** | **7,687** |

`XML-doc blocks` includes internal comments and `<inheritdoc/>` blocks; it is not a public-member
count. PublicAPI totals include shipped and unshipped entries from the current working tree.

## Source areas reviewed

| Project | Source area reviewed | Review boundary |
|---|---|---|
| `nextorm.core` | `DI/DataContextBuilder.cs`; `DataContext/IDataContext.cs`; named-table builder settings in `Builders/Entity/EntityBuilder.cs`; the type summary and `Pipeline` overloads in `Query/Commands/QueryCommand.TResult.cs`; lines 240–479 of `Builders/Entity/EntityBuilderExtensions.cs` | `DataContextBuilder.cs` and `IDataContext.cs` were read in full. The other files were reviewed only in the listed areas. |
| `nextorm.sqlite` | `DI/SqliteDataContextOptionsBuilderExtensions.cs` | The file was read in full; the routed-context overload was edited. |
| `nextorm.sqlserver` | `DI/SqlServerDataContextOptionsBuilderExtensions.cs` | The file was read in full; the routed-context overload was edited. |
| `nextorm.postgres` | `DI/PostgresDataContextOptionsBuilderExtensions.cs` | The file was read in full; the routed-context overload was edited. |
| `nextorm.mysql` | `DI/MySqlDataContextOptionsBuilderExtensions.cs` | The file was read in full; the routed-context overload was edited. |
| `nextorm.mariadb` | `DI/MariaDbDataContextOptionsBuilderExtensions.cs` | The file was read in full; the routed-context overload was edited. |
| `nextorm.clickhouse` | `DI/ClickHouseDataContextOptionsBuilderExtensions.cs` | The file was read in full; the routed-context overload was edited. |
| `nextorm.entityframeworkcore` | `EntityFrameworkCoreExtensions.cs` | The file was read in full; the class summary was edited. |
| `nextorm.extensions` | `ExtremeRowExtensions.cs` lines 90–182; `SelectGroupComponent.cs` lines 225–293 | Reviewed and edited the two extension classes and their public methods in those ranges. Other component comments were not reviewed. |

The Roslyn member queries also exposed a much larger core surface, including `QueryCommand` and
`EntityBuilderExtensions`. Those inventories are discovery evidence only; they do not mean all
member comments were read.

### Additional areas reviewed in this continuation

| Project | File | Exact comment area reviewed |
|---|---|---|
| `nextorm.core` | `Builders/Entity/EntityBuilderExtensions.cs` | Class summary and `BindEntity` documentation; output-stream parameter descriptions for the six `WriteJson` overloads. Together with the earlier lines 240–479 review, lines 1–479 were covered; later comments were not. |
| `nextorm.core` | `Query/SqlFunctions/SqlFunctions.ClickHouse.cs` | `ClickHouseFunctions` type summary. |
| `nextorm.core` | `Query/SqlFunctions/SqlFunctions.cs` | `SqlFunctions` remarks, `ClickHouse` property summary, and `CommonFunctions` remarks. |
| `nextorm.core` | `Utilities/ExpressionExtensions.cs` | Summaries and type-parameter descriptions for `TypeExpressionVisitor<T>` and `TypeExpressionVisitor<T1,T2>`; remarks for `ReplaceConstantsExpressionVisitor`. |
| `nextorm.core` | `Parameter.cs` | `Parameter` type remarks. |
| `nextorm.core` | `Builders/Paging.cs` | `Paging` type remarks. |
| `nextorm.core` | `Expressions/Ordering/Sorting.cs` | `Sorting` type remarks. |
| `nextorm.core` | `Builders/Joins/JoinedEntityBuilder.cs` | `JoinedEntityBuilder<T1,T2>` type remarks. |
| `nextorm.core` | `DataContext/Meta/Entities/IEntityMetadata.cs` | `IEntityMetadata` type remarks. |
| `nextorm.core` | `DataContext/Meta/Entities/IPropertyMetadata.cs` | `IPropertyMetadata` type remarks. |
| `nextorm.core` | `Query/Providers/IColumnsProvider.cs` | `IColumnsProvider` type remarks. |
| `nextorm.core` | `Query/Providers/IParameterProvider.cs` | `IParameterProvider` type remarks and member summary. |

The continuation also removed internal design-history/spec references from public XML comments in
the core API. A follow-up source-text scan found only references attached to internal implementation
comments, plus the excluded source-generator project; those were left unchanged.

## Changes made

| File | Comment changes |
|---|---|
| `src/nextorm.core/DI/DataContextBuilder.cs` | Replaced the class summary and removed an internal API-naming review note from generated API text. Clarified fluent return values, null behavior, boolean meanings, and the routing-resolver parameter. |
| `src/nextorm.core/DataContext/IDataContext.cs` | Reframed the interface summary around query/context use and clarified both boolean parameters to `GetPreparedQueryCommand`. |
| `src/nextorm.core/Builders/Entity/EntityBuilder.cs` | Clarified identifier/naming overrides, their null/boolean meanings, and that each returns a new builder without changing the current one. |
| `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs` | Reframed the result-command summary and replaced implementation-history text on `Pipeline` with its streaming behavior and return/parameter descriptions. |
| `src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs` | Clarified when the routed SQLite overload is used and where its connection comes from. |
| `src/nextorm.sqlserver/DI/SqlServerDataContextOptionsBuilderExtensions.cs` | Clarified when the routed SQL Server overload is used and where its connection comes from. |
| `src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs` | Clarified when the routed PostgreSQL overload is used and where its connection comes from. |
| `src/nextorm.mysql/DI/MySqlDataContextOptionsBuilderExtensions.cs` | Clarified when the routed MySQL overload is used and where its connection comes from. |
| `src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs` | Clarified when the routed MariaDB overload is used and where its connection comes from. |
| `src/nextorm.clickhouse/DI/ClickHouseDataContextOptionsBuilderExtensions.cs` | Clarified when the routed ClickHouse overload is used and where its connection comes from. |
| `src/nextorm.extensions/ExtremeRowExtensions.cs` | Reframed the extension-class summary around the selected rows and clarified the projected query-command return values. |
| `src/nextorm.extensions/SelectGroupComponent.cs` | Reframed the extension-class and method summaries around grouping and `HAVING` behavior. |
| `src/nextorm.entityframeworkcore/EntityFrameworkCoreExtensions.cs` | Reframed the class summary around the EF Core connection, transaction, and model used to create the nextorm context. |
| `src/nextorm.core/Builders/Entity/EntityBuilderExtensions.cs` | Reframed the extension-class summary, removed duplicated wording from `BindEntity`, and split the streaming JSON destination contract into shorter sentences. |
| `src/nextorm.core/Query/SqlFunctions/SqlFunctions.ClickHouse.cs` | Replaced an exhaustive function-name inventory in the type summary with a concise overview of function families and key constraints. |
| `src/nextorm.core/Query/SqlFunctions/SqlFunctions.cs` | Replaced naming-history remarks with usage guidance, shortened the ClickHouse property summary, and removed implementation-history remarks from `CommonFunctions`. |
| `src/nextorm.core/Utilities/ExpressionExtensions.cs` | Described the expression-node behavior accurately for the one- and two-type visitors; removed naming-history remarks. |
| `src/nextorm.core/Parameter.cs` | Removed a public naming-history remark. |
| `src/nextorm.core/Builders/Paging.cs` | Removed a public implementation-history remark. |
| `src/nextorm.core/Expressions/Ordering/Sorting.cs` | Removed a public implementation-history remark. |
| `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs` | Removed a public API-naming review note from the type remarks. |
| `src/nextorm.core/DataContext/Meta/Entities/IEntityMetadata.cs` | Removed a public naming-history remark. |
| `src/nextorm.core/DataContext/Meta/Entities/IPropertyMetadata.cs` | Removed a public naming-history remark. |
| `src/nextorm.core/Query/Providers/IColumnsProvider.cs` | Removed a public source-file naming note from the type remarks. |
| `src/nextorm.core/Query/Providers/IParameterProvider.cs` | Removed a public naming-history remark. |

## Validation after the first two passes

- XML-comment fragment parser: exit code 0; 11,086 fragments parsed across the nine projects; 0 XML parse errors.
- Source diff check: exit code 0; 0 changed non-XML-comment lines across the 25 source files changed by these passes.
- CRLF check: exit code 0; all 25 changed source files have CRLF line endings (one pre-existing no-final-newline file retains that state).
- `git diff --check`: exit code 0; no whitespace errors. Git emitted only line-ending warnings for already-dirty Markdown files outside this source-comment scope.
- No build, test, DocFX, package install, or restore was run.

## Scope remaining after the first two passes

At the end of the first two passes, the following existing public XML-comment areas remained for
manual readability review; the continuation results below document the later coverage:

- `nextorm.core`: the full `EntityBuilder<TEntity>` fluent surface; `EntityBuilderExtensions`
  comments after line 479; `QueryCommand`/`QueryCommand<TResult>` members beyond the reviewed
  result-command summary and `Pipeline` area; and the remaining public comments across core types.
- `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, and
  `nextorm.clickhouse`: public data-context, dialect, options, and provider-specific extension
  comments outside the six reviewed options-builder files.
- `nextorm.entityframeworkcore`: public types and members outside `EntityFrameworkCoreExtensions.cs`.
- `nextorm.extensions`: component types and public API outside the reviewed ranges in
  `ExtremeRowExtensions.cs` and `SelectGroupComponent.cs`.

The inventory contains 7,687 PublicAPI entries and 11,086 XML-doc blocks. These counts include
overloads and internal/inherited XML blocks and do not represent the number of unique public
documentation bodies manually reviewed. The first two passes stopped with this remaining scope due
to a session-resource limit; that historical stop did not waive the scope.

## Workspace preservation

The initial status check showed pre-existing edits to public documentation and provider README files;
this pass did not edit those files. Later status checks also showed changes to
`src/nextorm.core/PublicAPI.Unshipped.txt`, `src/nextorm.core/Query/VectorSearch/Vector.cs`, and
`tests/nextorm.core.tests/VectorSearch/`. They were outside this comment-only scope and were left
untouched.

## Continuation review results

### SQLite, SQL Server, and PostgreSQL

Remaining public comments were reviewed in `src/nextorm.sqlite/**`, `src/nextorm.sqlserver/**`, and
`src/nextorm.postgres/**`, except the three options-builder files already reviewed in full. Review
groups included SQLite dialect and data-context comments; SQL Server dialect capabilities/rendering,
engine-edition and vector syntax enums, data-context constructors/mapping/parameter/bulk hooks, and
typed/untyped `PIVOT`/`UNPIVOT` extensions; PostgreSQL dialect/data-context comments, `DistinctOn`,
and `PgVector`, `PgHalfVector`, `PgSparseVector`, and `PgBit` type/member families.

Changed only XML comments in `SqliteDialect.cs`, `SqlServerDataContext.cs`, and
`PostgresDataContext.cs`: clarified table-function support, the structured-parameter contract,
PostgreSQL raw-row support, and provider-specific column mapping. Other reviewed public comments were
retained when concise and source-accurate; inherited `<inheritdoc/>` families were left intact.
Internal-helper comments were outside the public-comment scope. The three changed files retain CRLF;
their scoped `git diff --check` passed (exit 0). No blocker was reported.

### Core data context

Reviewed existing public XML comments in `src/nextorm.core/DataContext/*.cs` and the assigned
`Bulk`, `Infrastructure`, `Lob`, `Mapping`, `Parameters`, `Procedures`, `Projection`, `Results`, and
`Roles` subtrees. `DataContext/IDataContext.cs` was retained as previously reviewed. Corrected the
`DataContext.PinHost` exception documentation in `DataContext/DataContext.cs` to distinguish invalid
arguments from an unregistered/default topology, picker mismatch, and conflicting routed transaction.
Other reviewed comments were retained where clear and accurate. CRLF was verified; no blocker was
reported.

### Expressions, utilities, attributes, enums, exceptions, and interceptors

Reviewed public comments in `Expressions/**`, `Utilities/**`, `Attributes/**`, `Enums/**`,
`Exceptions/**`, `Interceptors/**`, and `Parameter.cs`. The full file-family scope includes
`Expressions/{FromExpression,JoinExpression,UnionType,GroupingType,SelectExpression,XxHash32}.cs`,
`Expressions/Ordering/**`, `Expressions/Formats/**`, `Expressions/Comparers/**`,
`Expressions/Sources/**`, all five utility files, all eight attribute files, all four enum files,
both exception/interceptor files, and `Parameter.cs`.

Changed XML text in `ExpressionCache.cs`, `TypeExtensions.cs`, `ExpressionExtensions.cs`,
`SelectExpression.cs`, `XxHash32.cs`, and `RowMaterializerAttribute.cs` to clarify comparer behavior,
projection arity, visitor type/parameter contracts, nullable/accessor meaning, byte input, and the
attribute summary. The previously reviewed `TypeExpressionVisitor` summaries/type parameters,
`ReplaceConstantsExpressionVisitor` remarks, `Parameter` remarks, and `Sorting` remarks were retained.
Other reviewed comments were retained when clear and source-accurate. CRLF was verified; the
pre-existing no-final-newline state was preserved. No blocker was reported.

### Routing, sharding, DI, and root helpers

Reviewed public comments in `Routing/**`, `Sharding/**`, `DI/**`, and direct root source files except
`Parameter.cs`. The covered public files were `Routing/{RoutingRules,HostPin,RoundRobinTopology,
PhysicalTableName,DataRoutingBuilder,RoutingContracts,RoutedTransactions}.cs`,
`Sharding/{ShardKeyAttribute,ShardEntityMetadataProvider,ShardDataContextBuilderExtensions,
ShardPlacement,ConsistentHashRing,IShardKeyProvider,ShardKeyPredicateExtractor,IShardRouter,
IShardTopology}.cs`, `Sharding/FanOut/ShardQuery.cs`, `DI/ServiceCollectionExtensions.cs`, and
`Cte.cs`, `SqlDefault.cs`, `SqlKeywords.cs`. Internal-only comment files were excluded; `DI/DataContextBuilder.cs`
was retained as previously reviewed.

Review groups included keyed/unkeyed registration overloads, default-topology overloads, routed
transaction sync/async methods and forwarders, fan-out result methods, and inherited topology/router
members. Changed XML comments in 11 files to clarify precedence, behavior, result ordering, and remove
dense or historical wording. Routing contracts, table-name and transaction comments, and shard
key/placement/ring/router/topology contracts were retained as clear and source-consistent. CRLF and
scoped `git diff --check` were verified; no blocker was reported.

### EF Core bridge and extensions

Reviewed public comments in `src/nextorm.entityframeworkcore/**` and `src/nextorm.extensions/**`,
except the previously reviewed full `EntityFrameworkCoreExtensions.cs` file and the exact previously
reviewed ranges in `ExtremeRowExtensions.cs` and `SelectGroupComponent.cs`. The continuation covered
`NextOrmModelMapper.cs`, `NextOrmDbContextExtensions.cs`, `NextOrmQueryableExtensions.cs`,
`ExtremeRowExtensions.cs` lines 8–16 and 185–374, `SelectGroupComponent.cs` lines 6–185, and
`SelectGroupExpressionAnalyzer.cs` (`SelectGroupMode`, lines 35–49).

Changed XML comments only in `NextOrmModelMapper.cs`, `NextOrmQueryableExtensions.cs`,
`ExtremeRowExtensions.cs`, and `SelectGroupComponent.cs`, clarifying mapping-cache behavior, EF filter
semantics, query-source coverage, and component contracts. Other reviewed comments were retained
when clear and code-accurate. CRLF and scoped `git diff --check` passed; no blocker was reported.

### MySQL, MariaDB, and ClickHouse

Reviewed public comments in the assigned MySQL, MariaDB, and ClickHouse source roots. Review groups
included MySQL context constructors, parameter factories, dialect and `TimeOnly` comments; MariaDB
context constructor/version families and dialect capabilities; ClickHouse context constructors,
mapping and parameter comments, dialect capabilities/rendering, and the ClickHouse entity-builder
extension families for named tables, query sources, joined-builder arities, and array joins.

Changed only XML comments in `MySqlDataContext.cs`, `MariaDbDialect.cs`, `ClickHouseDataContext.cs`,
`ClickHouseDialect.cs`, and `ClickHouseEntityBuilderExtensions.cs`, clarifying reader-facing behavior,
JSON mapping, and repeated method summaries. Accurate constructor/ownership and capability comments,
as well as inherited documentation, were retained. The three options-builder DI files were not
rewritten because they were already reviewed in full. CRLF and scoped `git diff --check` passed; no
blocker was reported.

### Query commands, planning, mutations, clauses, locking, and windows

Reviewed the remaining public comments in `Query/*.cs`, `Query/Commands/**`, `Query/Clauses/**`,
`Query/Planning/**`, `Query/Mutations/**`, `Query/Locking/**`, and `Query/Windows/**`. The previously
reviewed `QueryCommand<TResult>` type summary and `Pipeline` comments were retained. Review groups
included query-command partials, set-operation selector overloads, streaming terminals, query-component
and preparation descriptors, query-plan equality/registry types, and window-function/frame overloads.

Changed XML comments in `QueryCommand.cs`, `QueryCommand.TResult.cs`, `QueryCommandExtensions.cs`,
`WindowFunctions.cs`, `QueryComponents.cs`, `QueryDefinition.cs`, `IQueryRegistry.cs`, and
`ExpressionPlanEqualityComparer.cs`. Changes clarify asynchronous preparation/streaming behavior,
caller-owned stream failure semantics, SQL Server JSON/XML terminals, set-operation overload
selection, query-shape descriptors, and window-frame overloads. `git diff --check -- src/nextorm.core/Query`
passed (exit 0); CRLF was preserved. No build/test/restore/DocFX run; no blocker was reported.

### Builders

Reviewed the existing public comments in all 71 tracked C# files under `Builders/*.cs` and the
`Bulk`, `CollectionSlot`, `Cte`, `Ddl`, `Dml`, `Entity`, `Joins`, `Merge`, `Returning`, `TempTables`,
and `Tvf` subtrees. Equivalent overload families were reviewed together. The previously reviewed
`EntityBuilder<TEntity>` named-table settings, `EntityBuilderExtensions` comments through line 479,
`JoinedEntityBuilder` type remarks, and `Paging` type remarks were retained.

Changed XML comments in `Bulk/{BulkInsertBuilder,BulkInsertOptions}.cs`,
`Dml/Insert/{InsertBuilder.Values,InsertBuilder,InsertReturningBuilder}.cs`,
`Entity/{EntityBuilder.VectorSearch,EntityBuilder,EntityBuilderEagerLoading,EntityBuilderExtensions}.cs`,
`Joins/{JoinIntoManyToManySpec,JoinIntoSpec,JoinedEntityBuilder,JunctionLinkSourceFactory}.cs`,
`Merge/{MergeBuilder,MergeReturningBuilder}.cs`, `Paging.cs`, `SqliteFts5CommandBuilder.cs`,
`TempTables/TempTableExtensions.cs`, and `Tvf/TvfJoinSourceExtensions.cs`. The changes clarify
bulk-builder reuse, composite junction carriers, TVF null exceptions, provider count behavior, and
FTS5 thresholds. Other reviewed groups were retained where comments matched behavior. Scoped
`git diff --check` passed (exit 0); no blocker was reported.

### Expression visitors — core

Reviewed public comments in the eight files under `Visitors/Core/`: `AliasFromProjectionVisitor.cs`,
`MemberExpressionVisitor.cs`, `BaseExpressionVisitor.cs`, `CorrelatedQueryExpressionVisitor.cs`,
`WhereExpressionVisitor.cs`, `SemanticExpressionEqualityComparer.cs`, `VisitorOptions.cs`, and
`ReplaceExpressionVisitor.cs`. Changed comments in the last three files to describe type-matched
replacements, same-type parameter comparison, and init-only option properties. Comments in Alias,
Member, Base, Correlated, and Where visitors were retained as clear and source-consistent. Internal
and private comments were outside this public-comment review. CRLF and scoped `git diff --check` were
verified; no blocker was reported.

### SQL dialect contracts and source-rendering options

Reviewed existing public XML comments in `DataContext/Dialect/{AggregateFilterStyle,
DialectCapabilities,ISqlDialect,NullsOrderingRenderer,SqlDialectBase}.cs` and
`DataContext/Sql/FromOptions.cs`. The review covered dialect capability/renderer pairs, provider
defaults and fallback behavior, and the public table-sampling, hint, and source-lock options. It
covered `AggregateFilterStyle`; public renderer contracts and shape records in `DialectCapabilities`;
all `ISqlDialect` members and `StringTrimKind`; `NullsOrderingRenderer.Validate`, `Render`, and
`RenderNative`; public and protected `SqlDialectBase` hooks; and all public `FromOptions` methods.
Equivalent capability flags and inherited `<inheritdoc/>` members were reviewed as groups.

This review clarified the `SupportsDatePart` contract and described `SupportsDelete` and
`SupportsUpdate` as renderability gates, not guarantees about affected-row counts. It also reframed
the `SqlDialectBase` type and savepoint summaries around the contract a dialect author needs. Earlier
working-tree edits in `DialectCapabilities.cs`, `SqlDialectBase.cs`, and `FromOptions.cs` were
preserved, including the concise `ISqliteFunctions` surface summary and the `FromOptions.WithIndex`
parameter description: empty lists are accepted and leave the options unchanged, while null or blank
entries are ignored.

Inspected the remaining `DataContext/Sql/*.cs` and `DataContext/Sql/Lowering/*.cs` files for public
XML-comment scope. Their containing renderer and lowering types are internal, so their implementation
comments were not edited. The detailed `ISqlDialect` capability comments were retained where they
explain provider support, default behavior, or the exact rendering contract needed by external dialect
implementers.

### SQLite FTS3/4 snippet overloads

Verified that `SqliteFunctionTranslator.TryTranslate` preserves the `FTS3Snippet` argument order and
that `SqliteFunctionRenderer.Render` forwards those arguments to `snippet(...)` in that order.
Clarified the overload summaries to map the CLR parameter names to SQLite's positional argument names
(`start_text`, `end_text`, `ellipsis`, `column_index`, and `num_tokens`). This resolves the earlier
ambiguity without changing translation behavior.

### SQL function surfaces

Reviewed the existing public XML comments across the six files in
`Query/SqlFunctions/`: the common surface and the ClickHouse, MySQL/MariaDB, PostgreSQL, SQL Server,
and SQLite function families. Equivalent provider and overload families were reviewed together.

Changed summaries and member comments to shorten exhaustive catalogs, distinguish MySQL from
MariaDB-only behavior, clarify JSON and percentile limitations, describe PostgreSQL session-setting
arguments, make SQL Server conversion and null-result contracts easier to scan, and correct the
ClickHouse `arrayPushFront` wording. The SQLite FTS3/4 argument mapping is recorded in the preceding
section. Other reviewed comments were retained when clear and source-accurate.

### Remaining core API comments

Reviewed and clarified existing public comments in `DataContext/Cache/Preparation/PrepareOptions.cs`,
`DataContext/Meta/IEntityVariantResolver.cs`,
`DataContext/Meta/Relationships/RelationshipKind.cs`, `Query/Json/JsonStreamOptions.cs`, and
`Query/Pivot/PivotExpression.cs`. Changes remove obsolete naming/history wording, use the mapping
variant terminology consistently, describe many-to-many declaration and JSON stream formats, and
correct the pivot-renderer cross-reference. Other comments in these files were retained when clear
and source-accurate.

### Public aggregate-classification helpers

Reviewed the public type and member comments in `Visitors/Aggregates/AggregateClassification.cs`.
Clarified the classification scope, method-call requirements, attribute fallback, window-name
mapping, projection shape, and convert-unwrapping behavior. Internal and private visitor
implementation comments are outside this public-comment review; edits to those comments were
restored.

## Review closure and final validation

The continuation results above cover the remaining public source areas listed in the earlier scope
statement across all nine DocFX projects. Equivalent overloads and inherited comment families were
reviewed as groups, not counted as separate manual-review units. Internal/private XML comments and
`<inheritdoc/>` expansions were not treated as independent public comment bodies.

Final validation results:

- XML-comment fragment parser: exit code 0; 11,086 blocks in 517 tracked C# files across the nine
  projects; 0 parse errors.
- Source diff check: exit code 0; 91 changed C# files had 0 non-XML-comment differences.
- CRLF check: exit code 0; 0 bare-LF or bare-CR files among the 91 changed C# files and this report.
  Five C# files retained their existing no-final-newline state.
- `git diff --check`: exit code 0 across the 91 changed C# files; report whitespace check found 0
  trailing-whitespace lines.
- Full validation log: `/tmp/opencode/readability-api-validation.log`.

No build, test, restore, NuGet, or DocFX command was run.

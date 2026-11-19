# Immutable planner criterion correction — task `issues-65-69`, cycle `N=5`, revision `r=2`

Source: corrected criterion text supplied in the authorized DO dispatch.

---8<--- PLAN BEGIN

## Corrected AC-65-1

*`MappingScope`, `IEntityScopeResolver` (`DefaultEntityScopeResolver`), `IEntityMetadataProvider` (`DefaultEntityMetadataProvider`), `PrefixedParameterProvider`, `DataContextBuilder.UseEntityMetadataProvider` / `UseScopeResolver`, `EntityBuilder.WithScope` / `MergeBuilder.WithScope`, and `IEntityMetadata.Schema` / `EntityMetadataBuilder.Schema` are implemented; default-scope queries produce identical SQL, parameters and plan-cache entries as before: no plan-cache hit is lost, no parameter or table is renamed under default scope, and non-default scopes never leak into default-scope or other-scope cache entries or metadata.*

## AC-65-2 required constructs (exhaustive)

`SELECT`/`JOIN`/subquery/CTE, `INSERT`/`UPDATE`/`DELETE`, `MERGE` over the three sources (table, subquery/query, TVP/collection), `TRUNCATE`, cross-scope plan-cache isolation, parameter-name collision avoidance. Nothing outside that list may be demanded.

## Unchanged criteria

`AC-65-2` criterion text, `AC-65-3` (`met`) and `AC-REPO-1` remain unchanged from `.pdca/status/issues-65-69-cycle-N2-plan.md` §3. `AC-65-3` is already `met`.

---8<--- PLAN END

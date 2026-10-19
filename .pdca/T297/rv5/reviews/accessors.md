# T297 r=5 — D:P03b accessor simplification review

Task: inspect the post-P03a accessor layer (after `4432fbcb`) for obsolete adapters or redundant
traversal and remove only what is genuinely mechanical; otherwise declare a no-op with evidence.
No preparation storage, frozen files, default/no-op semantics, nullable/sentinel semantics or
`GetFlag`/`SetFlag` logic may change.

## Candidate list and decision

| Candidate | Location | Decision | Why |
|---|---|---|---|
| Wrapper `WriteRelations()` | `src/nextorm.core/Query/QueryCommand.Description.cs:192-197` | must stay | It is not an adapter: it is the relation-write barrier that clears `QueryDescription.SharedRelations` after `WriteDescription()` (`:194-195`). 10 call sites need exactly that pair (`Description.cs:244,254,274,284,294,304`; `QueryCommand.cs:1327,1395,1424,1475`); inlining would duplicate the two-line barrier at 10 sites (more code, same behaviour), and using `WriteDescription()` there instead would append into a sibling's in-place list. |
| `WritePredicates` / `WriteClauses` / `WriteOptions` shims | — | already gone | `tools.roslyn action=symbols` for each returns `symbol not found`; no textual occurrence remains under `src/nextorm.core`. No shim left to remove. |
| `_description is { } d` patterns repeating a null check | `Description.cs:199` (`GetFlag`) | must stay | Exactly one such pattern exists in `src/nextorm.core` (only other `_description` uses are `?.`/`??`). `GetFlag` needs the non-null `description` to read the non-nullable `ulong Flags`; rewriting it as `(_description?.Flags & flag) != 0` would keep the same single check but obscure the frozen flag logic. No repeated null check to collapse. |
| `QueryDescription` properties that only forward | `QueryDescription` (`Description.cs:69-159`) | none exist | Roslyn `members` lists only internal fields plus `MarkShared()` (`:142`) and `CloneShellForWrite()` (`:152`). There is no property/accessor pair to delete. |
| Accessor still taking two hops: `FilterScope` → `_filterScope` | `QueryCommand.cs:507-513` → `Description.cs:227-236` | must stay | The outer member is the command-level setter with side effects (`SetFilterScope`, preparation reset, `ArgumentNullException`); the inner one is the raw COW slot with its own equality guard. `_filterScope` is used directly by the constructors/clone/preparer (`QueryCommand.cs:122,230,291,314,335-336,497,510,521,524`; `Clone.cs:25`; `QueryPreparer.cs:2146,2181,2292-2293,2369-2370,4003`), so the inner layer cannot be folded away; merging would move the reset semantics into every reader. |
| Accessor still taking two hops: `Joins` → `_joins` | `QueryCommand.cs:477` → `Description.cs:238-246` | must stay | The public view is read-only; the private accessor owns the write path (reference guard + `WriteRelations()`). `_joins` has 26 references across `QueryCommand.cs`, `QueryPreparer.cs`, `TempTables.cs` and `Clone.cs`, including constructor writes (`QueryCommand.cs:132,192,231`) where the `WriteRelations` barrier and memo invalidation must not apply. Same shape for `UnionQuery`/`OuterReferences`/`Ctes`/`Hints`/`Windows`, which are public/internal read views over the COW accessors, not obsolete shims. |

No semantic-preserving removal was found. **Decision: P03b is a no-op — no code change.**

## Verification evidence (unchanged tree)

| Check | Command | Result | Log |
|---|---|---|---|
| Gate | `python3 .pdca/scripts/validate_inner_loop.py brief .pdca/T297/rv5/test-scope.json` | rc 0 | `.pdca/T297/rv5/logs/test-scope-p03b.log` |
| Build | `MSBUILDDISABLENODEREUSE=1 dotnet build nextorm.slnx -c Debug` | rc 0, 0 warnings / 0 errors | `.pdca/T297/rv5/logs/p03b-build.log` |
| Focused core | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~CteDescriptionCowTests\|FullyQualifiedName~CachedPreparationRecipeTests"` | rc 0, 33 passed / 0 failed / 0 skipped | `.pdca/T297/rv5/logs/p03b-core-focused.log` |
| Full core | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | rc 0, 2658 passed / 0 failed / 0 skipped | `.pdca/T297/rv5/logs/p03b-core-full.log` |
| E19 | `dotnet test tests/nextorm.publicextensibility.tests -c Debug --no-build` | rc 0, 19 passed / 0 failed / 0 skipped | `.pdca/T297/rv5/logs/p03b-e19-focused.log` |

Evidence for the "already gone" row: `tools.roslyn({action:"symbols"})` for `WritePredicates`,
`WriteClauses`, `WriteOptions` returns `(symbol not found: ...)`; `rg` over `src/nextorm.core`
finds no occurrences. Evidence for the `_description is { }` row: a single occurrence at
`QueryCommand.Description.cs:199`; no `_description?.Relations.`/`.Clauses.`/`.Options.`/
`.Predicates.` two-hop forms remain.

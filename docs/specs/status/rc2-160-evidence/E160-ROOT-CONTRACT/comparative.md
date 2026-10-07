# P:160-ROOT-CONTRACT — derived-root comparative evidence (facts only)

- issue: #160
- task: D160, cycle 1, plan revision r=2, attempt n=1/3
- branch: 1.0.9-rc2
- status file: `docs/specs/status/rc2-160-join-alias-mixing-1.md`
- evidence contract: **rv=2** (rv=1 explicitly superseded by rv=2)
- date: 2026-10-08
- purpose: gather facts for the next planner decision (i)/(ii)/(iii). **No acceptance change, no
  pass/deferral declaration.** The three derived-root tests stay red as expected evidence.

## R160 acceptance criteria (verbatim from the status file, rv=2)

- R160-01: all mixing directions compile; JOIN/APPLY and SQL slots match chain order; `ItemK` and slot-K alias resolve to the same `tK`. Negative: reordering alias↔slot, repeated CLR type, `Buyer2` must not shift slots.
- R160-02: alias-only semantics preserved; positional-only preserves existing API/SQL/execution path with no alias overhead. Negative: pure positional must not enter alias refusal/seam; unsupported provider op not silently supported.
- R160-03: `.WithAlias` works on all listed root sources; `p.Order.X` → `t1.X`. Negative: repeated `.WithAlias`, applied to a join result, invalid/duplicate root alias → compile-time rejection, no wrong projection.
- R160-04: dim-1 planned correctly; later Extend yields slots 1,2,…; arity 2–8 correct. Negative: 9th slot rejected by existing diagnostic contract; documented `As<T>` overflow path remains usable.
- R160-05: alias members expression-only; positional members keep prior semantics. Negative: direct read of alias member throws, including root projection.
- R160-06: any alias (root or join) fail-closed in-memory with `NotSupportedException`; pure positional chains work. Negative: root alias without join and alias after positional prefix also rejected, no partial result.
- R160-07: repeated/alternating executions keep correct plans/params/cache; cached-path perf gate run. Negative: changing alias/slot/param does not reuse a wrong plan; shared command does not get sticky `Cache=false`.
- R160-08: build exit 0, 0 warnings/0 errors; coverage line ≥85%, branch ≥75%. Negative: green build without coverage/mandatory evidence is not full acceptance.
- R160-09: real execution on PostgreSQL, SQL Server, MySQL, MariaDB, SQLite, ClickHouse; expected SQL/result and positional/alias parity. Negative: skipped or missing provider is not passing evidence.
- R160-10: EN/RU docs reflect mixing, root alias, expression-only, in-memory refusal, arity; obsolete generated names/alias-only wording removed. Negative: no new public links into `docs/specs/**`, no broken links, no EN/RU divergence.
- R160-11: NORMGEN001–006 contracts preserved; root misuse gets stable diagnostics; incrementality and overload binding correct. Negative: same-name descriptors, invalid markers, wrong root usages do not produce malformed generated C# and do not bypass diagnostics.

## The four root derivations at issue

| root | source kind | `_query` after re-root? | alised outcome |
|---|---|---|---|
| `FromSql("…")` | raw source (`SourceFrom`) | no | **works** — derived source preserved |
| `From(builder)` | builder's command | yes | throws `NotSupportedException` |
| `From(QueryCommand<T>)` | derived query command | yes | throws `NotSupportedException` |
| `From<T>(Cte<T>)` / temp / function | not part of this probe | — | not gathered |

## Comparative results (baseline vs `.WithAlias(Alias.Root)`)

All baseline probes ran against SQLite via `AliasSqliteDatabase.CreateContext()`; the order has one row
with `BuyerId=10`. Probe source (temporary, removed after the run) and raw logs:
`unaliased-baseline.txt`, `unaliased-baseline-run.log` (this folder).

### 1. `FromSql("select Id, BuyerId from orders")`

- (a) **unaliased baseline** `FromSql(...).Join<Person>(people, (o,p) => o.GetInt64("BuyerId") == p.Id)`:
  **OK**, result `ids=[10]` (raw log line 1).
- (b) **aliased** `FromSql(...).WithAlias(Alias.Root).Join<Person>(people, (o,p) => o.Root.GetInt64("BuyerId") == p.Id, Alias.Buyer)`:
  **runtime works**, result asserts pass; rendered SQL (from `root-alias-tests.log`, test
  `Generated_root_alias_on_a_fromsql_source_keeps_the_derived_source`):
  `select t2.Id from (select Id, BuyerId from orders) as 't1' join person as 't2' on t1.BuyerId = cast(t2.Id as bigint)`.
  The test fails at `RootAliasTests.cs:209` only because it asserts the literal `orders as 't1'` while
  the derived source is correctly wrapped as `(select Id, BuyerId from orders) as 't1'`. The derived
  source is **preserved** (no physical `orders` unwrap, no extra nesting).
- Interpretation: not a runtime limitation; an over-strict SQL-substring expectation in the test.

### 2. `From(builder)` — `ctx.From(ctx.From<Order>(b => b.Table("orders")))`

- (a) **unaliased baseline** `From(builder).Join<Person>(people, (o,p) => o.BuyerId == p.Id)`:
  **OK**, result `ids=[10]` (raw log line 2).
- (b) **aliased** `From(builder).WithAlias(Alias.Root).Join<Person>(people, …, Alias.Buyer)`:
  **throws** `System.NotSupportedException` — "A derived query as the primary FROM source can be
  joined only once; add further joins to the derived query instead." — at
  `src/nextorm.core/Builders/EntityBuilder.cs:3465` (`ResolveJoinBase`), reached via
  `ResolveAliasJoinBase` (`EntityBuilder.cs:3487`) from `JoinAliasEntity` (`EntityBuilder.cs:3001`).
  Call site: `RootAliasTests.cs:250` (`root-alias-tests.log` failure 2).
- Guard condition: `EntityBuilder.cs:3464-3466`
  (`typeof(TEntity).TryGetProjectionDimension(out _) && _joins is not { Count: > 0 }`). After
  `.WithAlias` the root type is the alias projection, and `_query` is non-null, so a first alias join
  over it is rejected.

### 3. `From(QueryCommand<T>)` — `ctx.From(ctx.From<Order>(…).Where(…).ToCommand())`

- (a) **unaliased baseline** `From(source).Join<Person>(people, (o,p) => o.BuyerId == p.Id)`:
  **OK**, result `ids=[10]` (raw log line 3).
- (b) **aliased** `From(source).WithAlias(Alias.Root).Join<Person>(people, …, Alias.Buyer)`:
  **throws** the same `System.NotSupportedException` at `EntityBuilder.cs:3465`; call site
  `RootAliasTests.cs:228` (`root-alias-tests.log` failure 3).

## Boundary test run (this session)

- command: `dotnet test tests/nextorm.alias.tests -c Debug --filter "FullyQualifiedName~RootAliasTests"`
- exit: `2`; selected 13, passed 10, failed 3 (the three derived-root tests above), skipped 0.
- log: `docs/specs/status/rc2-160-evidence/E160-ROOT-CONTRACT/root-alias-tests.log`
- targeted green of the new mechanism:
  `dotnet test tests/nextorm.alias.tests -c Debug --no-build --filter "FullyQualifiedName~Generated_root_alias_supports_a_positional_join_after_it"`
  exit `0`; selected 1, passed 1, failed 0 — log `positional-after-root-alias.log`.

## Open facts for the planner (no option selected)

1. `FromSql` already works at runtime; only its SQL-substring expectation is over-strict.
2. `From(builder)` and `From(QueryCommand<T>)` hit `EntityBuilder.cs:3464-3466` because `.WithAlias`
   makes the root an alias projection while `_query` stays non-null.
3. `EntityBuilder.cs:3464-3466` is kept as the planner ordered; it was not changed in this session.

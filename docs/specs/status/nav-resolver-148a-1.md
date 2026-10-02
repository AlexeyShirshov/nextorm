# NavigationPathResolver foundation — #148-A (cycle 1, r2, attempt 1/3)

- status: DONE — #148-A slice A committed (`8393f822d4fe99d45972e9c15142f27b2a899583`); #148 stays OPEN for slice B
- cycle: 1, revision r2, attempt 1/3 (autonomous mode)
- issue: #148 (milestone 1.0.9-rc1); source design `docs/specs/design/implicit-navigation-queries.md` (uncommitted)
- goal: implement the internal, expression-only `NavigationPathResolver` foundation that resolves an exact
  navigation-member chain rooted in a visible query source into an immutable `ResolvedNavigationPath`,
  using the process-wide configured relationship metadata with no cache mutation and no visitor wiring.
- scope of this DO unit: **D1 only** (three internal types in `src/nextorm.core/Visitors/`).

## Acceptance criteria (r2, contract-derived A1–A8)

- A1 Types: `NavigationPathResolver`, `NavigationResolutionScope`, `ResolvedNavigationPath` are internal in
  `src/nextorm.core/Visitors/`; `Resolve(Expression, NavigationResolutionScope) -> ResolvedNavigationPath`;
  no public API addition, no new resolver interface/DI/generic abstraction.
- A2 Metadata authority: relationship metadata resolved through the process-wide CLR-type-keyed configured
  path using `DataContextExtensions.ResolveMetadata(null, entityType)` semantics exactly as
  `RelationshipResolver` (`RelationshipResolver.cs:85-86`), with `From<T>(cfg)` precedence
  (`DataContextExtensions.cs:225-256,996,1015`); the resolver never writes/seeds/overwrites/clears the
  metadata cache; no per-source mapping.
- A3 Source identity: scope supplies current scope identity + explicit visible root bindings (owning-scope
  identity + source identity + expression anchor + entity type); matching is by `ParameterExpression`/
  `FromExpression` reference identity and exact member sequence, never expression text or entity type;
  same CLR type with a different alias/parameter is a distinct source.
- A4 Result: `ResolvedNavigationPath` is an immutable snapshot (scope + root owning-source identity,
  ordered exact navigation members, per-hop declaring/related types, kind/cardinality, direction, resolved
  FK/PK `IPropertyMetadata`, and for M2M the junction entity/mapping identity + both legs); no public
  writable arrays, no retained mutable relationship objects; equality compares components including exact
  member identity (hashes are not a substitute).
- A5 Hops: a non-final collection hop is unsupported; the final hop may be a reference or a collection;
  reference chains are allowed.
- A6 Keys: exactly one resolved key member per traversed leg; multi-element key metadata fail-closes with
  `NotSupportedException`; composite keys are not added.
- A7 Conversions: identity conversions and safe reference upcasts are permitted only when the exact
  declared member binding is preserved; downcasts and user-defined conversions are rejected.
- A8 Rejections/evaluation: every rejection (`NotSupportedException`, never null/default marker) —
  undeclared member, missing/unresolvable key, collection as intermediate hop, unbound parameter,
  sibling/unregistered scope, captured/local/static/constant root, arbitrary method call inside the
  operand, downcast/custom conversion; no name-based guessing; the resolver never compiles/evaluates an
  expression, reads a navigation getter, or enumerates entity objects; no visitor wiring
  (`WhereExpressionVisitor`, `PredicateTranslator`, `CorrelatedQueryExpressionVisitor`, preparation,
  rendering, InMemory) and no `AsEntityBuilder` (#148-B).

## r2-corrected frozen contract (implemented exactly)

- Internal types in `Visitors/`: `NavigationPathResolver.Resolve(Expression navigationPath,
  NavigationResolutionScope scope) -> ResolvedNavigationPath`; `NavigationResolutionScope` supplies current
  scope identity + explicit visible root bindings; `ResolvedNavigationPath` is the immutable result.
- Metadata authority via the configured path (A2); no cache write.
- Source identity via parameter/`FromExpression` reference identity (A3).
- Result/equality (A4); hops (A5); keys (A6); conversions (A7); rejections/no-evaluation/no-wiring (A8).
- Use `PropertyInfo`/`IPropertyMetadata` from `RelationshipMetadata`/`RelationshipResolver`
  (`ResolveProperty:14`, `ResolvePrincipalProperty:30`, `ResolveKey:46`, `FindInverse:66`,
  `ValidateJunctionKeyType:98`) — metadata is not redesigned.

## DO units

- D1 (this unit): three internal foundation types; builds clean.
- D2: navigation expansion stage before prepare (canonicalize reference joins / direct terminals /
  adapter) — out of scope here; owned by its own DO stream.
- D3: `AsEntityBuilder<T>` expression-only API (#148-B) — out of scope here; no public API change.
- D4: tests + acceptance evidence for #148-A/B (reference absence/dangling/null/OR/bool projection,
  chains/self/dual/source scopes, four direct collection operations, cache/reject matrix) — out of scope
  here; owned by the CHECK/test stream.

## Enumeration reconciliation (coder-derived vs plan)

- Plan stream units: **D1** (resolver foundation) / **D2** (navigation-expansion stage before prepare) /
  **D3** (`AsEntityBuilder<T>` expression-only API) / **D4** (tests + acceptance evidence).
- Coder-derived acceptance set **A1–A8** is not a separate plan stream: A1–A7 are the r2 contract
  properties delivered by **D1**, and **A8** is the fail-closed / no-evaluation / no-wiring invariant that
  D1 satisfies and D4 must re-verify. No unit renamed, dropped or re-scoped.
- D1 delivered; **D2/D3/D4 were not delivered in this DO** and remain owned by their own streams
  (D2/D3 → #148-B; D4 → CHECK). Public surface unchanged.

## Progress log

2026-10-02T15:12:25Z | DO | revision r2 | iteration 1/3 | plan recorded (goal/A1-A8/r2 contract/D1-D4) | docs/specs/status/nav-resolver-148a-1.md

- durable state: cycle 1, plan revision r2, attempt 1/3 (no replan; no rejected candidate; counters unchanged).
- defect history: none observed in DO.

2026-10-02T15:14:20Z | DO | revision r2 | iteration 1/3 | D1: created `NavigationPathResolver`/`NavigationResolutionScope`/`ResolvedNavigationPath` (+`ResolvedNavigationHop`/`ResolvedNavigationLeg`/`NavigationDirection`/`NavigationSourceBinding`), all internal under `Visitors/`; metadata via `DataContextExtensions.ResolveMetadata(null, type)` exactly as `RelationshipResolver`, no cache write; root matched by parameter/reference identity; single-column key fail-closed; identity conversions + safe reference upcasts only; every rejection `NotSupportedException`; no evaluation and no visitor wiring; Debug exit 0 0W/0E; Release exit 0 0W/0E; CRLF preserved; no commit | /tmp/opencode/148a/build-debug.log, /tmp/opencode/148a/build-release.log

2026-10-02T15:16:21Z | DO | revision r2 | iteration 1/3 | D4 docs: #148-A/#148-B ownership + acceptance allocation + metadata-authority correction (`DataContextExtensions.ResolveMetadata(null, entityType)`, not per-source mappings) recorded in the design spec; #148 tracking rows in the roadmap; API-naming register entry (no public surface); limitations EN/RU "implicit navigation queries / `AsEntityBuilder<T>` not yet delivered"; DocFX exit 0 (2 pre-existing duplicate-source warnings) | docs/specs/status/nav-resolver-148a-1.md

- Docs: `docs/specs/design/implicit-navigation-queries.md`, `docs/specs/roadmap/todo_navigation_properties.md`, `docs/specs/design/API-NAMING-REVIEW.md`, `docs/advanced/limitations.md` + `docs/ru/advanced/limitations.md`, `docs/specs/status/nav-resolver-148a-1.md`; no public API change; no spec links on public pages; no commit.

2026-10-02T15:32:26Z | DO | revision r2 | iteration 1/3 | D5 finish: builds 0/0 (Debug + Release 0W/0E); focused 47/47; regression 470/470 (core suite); coverage line 86.8% / branch 78.2%; new-type branch counts NavigationPathResolver 57/71 (80.3%), ResolvedNavigationPath 14/18 (77.8%), ResolvedNavigationHop 21/26 (80.8%), ResolvedNavigationLeg 3/6 (50.0%), NavigationSourceBinding 6/6 (100%), NavigationResolutionScope 0/0 (100%); uncovered new-type conditions NavigationPathResolver:90(9/10),129(5/6),202(3/4),219(4/5),256(1/2),260(4/8),292(2/4),298(0/2),301(1/2); ResolvedNavigationPath:233(1/2),238(1/2),241(1/2),250(1/2); ResolvedNavigationHop:179(1/2),181(1/2),186(1/4); ResolvedNavigationLeg:50(1/2),58(2/4) [full uncovered list incl. QueryPreparer/RelationshipMetadata collaborator conditions: /tmp/opencode/148a/d5/new-type-branches.txt]; audit 0/0 (0 SuppressMessage / #pragma / #nullable disable / ExcludeFromCodeCoverage / empty catch / TODO-FIXME / disabled-Skip, 0 slop findings); mutation 8 killed + 1 proven-unreachable equivalent (e2 composite-key guards; proof /tmp/opencode/148a/d5/mutation/e2-equivalence-proof.txt); perf 7/0 ratio 1.95 wall 50s (exit 0); integration 3042/0 failed/187 pre-existing capability skips; per-provider passed/failed/skipped SQLite 559/0/41, PostgreSQL 575/0/25, SQL Server 559/0/41, MySQL 523/0/77, ClickHouse 103/0/0; nav/CTE/JoinInto 63 relevant tests executed (none skipped); evidence /tmp/opencode/148a/d5/per-provider.log, integration-run.log, focused.log, regression.log, coverage-collect.log, coverage-report/Summary.txt, new-type-branches.txt, suppression-audit.txt, mutation/SUMMARY.md, perf-acceptance.log, build-debug.log, build-release.log | docs/specs/status/nav-resolver-148a-1.md

- durable state after audit fix: cycle 1, plan revision r2, attempt 1/3 (no replan; no rejected candidate; counters unchanged).
- defect history (semantic audit, r2): C1 OneToOne direction inverted → fixed once (red→green: 2 tests failed on the inverted code, then green); T1 cache isolation → fixed once; C2 `SameIdentity` null guard → fixed once; S3 unused test helper → removed once; C3/C4 test gaps → tests added once; no loop-back, no escalation; evidence /tmp/opencode/148a/fix/c1-red.log, c1-green (test-navigation-focused.log).

2026-10-02T15:41:33Z | DO | revision r2 | iteration 1/3 | semantic-audit fix C1/T1/C2/S3/C3/C4 (no new revision): C1 fixed OneToOne direction `DependentToPrincipal`→`PrincipalToDependent` (resolver now separates `RelationshipKind.OneToOne` from `ManyToOne`; enum doc clarified) + red→green proof (inverted code: 2 failed /tmp/opencode/148a/fix/c1-red.log; fixed: 2 passed); T1 added `[Collection("Query cache controls")]` to `NavigationPathResolverTests`; C2 added `ArgumentNullException.ThrowIfNull(other)` to the three `SameIdentity` methods; S3 removed unused `Bind` helper; C3 added root-only/empty-path test and an `Equals` reference/null/foreign/count test; C4 composite-key guards remain documented unreachable (no composite support). No public API change, resolver still internal/unwired. Debug 0W/0E, Release 0W/0E; focused 50/50, regression 470/470; coverage line 86.8% / branch 78.2%; new-type branch counts NavigationPathResolver 57/71 (80.3%), ResolvedNavigationPath 18/18 (100%, up from 14/18), ResolvedNavigationHop 21/26 (80.8%), ResolvedNavigationLeg 3/6 (50%), NavigationSourceBinding 6/6, NavigationResolutionScope 0/0; evidence /tmp/opencode/148a/fix/ (build-debug.log, build-release.log, test-navigation-focused.log, test-regression.log, c1-red.log, coverage-collect.log, coverage-report.log) | docs/specs/status/nav-resolver-148a-1.md

- durable state after Fix2: cycle 1, plan revision r2, attempt 1/3 (no replan; no rejected candidate; counters unchanged).
- defect history: `148A-immutable-list-exposure` — P1, **first occurrence**, fixed once in r2 (published collections backed by `ReadOnlyCollection<T>` over a copied array; regression red→green); no loop-back, no escalation; evidence /tmp/opencode/148a/fix2/.
- red→green nuance: on this toolchain the pre-fix `[.. hops]`/`[.. visibleRoots]` collection expressions already materialized the compiler-synthesized read-only wrapper `<>z__ReadOnlyArray<T>` (not `List<T>`), so the guard test passed pre-fix as well; the red proof therefore used the defect pattern as reported (a mutable `new List<T>(...)` backing), which the test catches (1 failed, exit 2, /tmp/opencode/148a/fix2/red-proof.log), vs the `Array.AsReadOnly` fix green (exit 0, green-proof.log). The fix removes reliance on the compiler-synthesized type and pins the guarantee explicitly.

2026-10-02T15:47:00Z | DO | revision r2 | iteration 1/3 | Fix2: `148A-immutable-list-exposure` first occurrence (no new revision) — `ResolvedNavigationPath.Hops` and `NavigationResolutionScope.VisibleRoots` now back `IReadOnlyList<T>` with `Array.AsReadOnly<T>([..])` (`ReadOnlyCollection<T>` over a copied array) instead of a downcastable mutable `List<T>`; regression `Resolve_PublishedCollections_AreTrulyImmutableSnapshots` (downcast-to-`List<T>` null, `IsReadOnly` true, `Add`/indexer-set throw `NotSupportedException`, data unchanged); Debug 0W/0E, Release 0W/0E; focused 51/51, regression 470/470; coverage line 86.8% / branch 78.2% (21562/27556); new-type branches ResolvedNavigationPath 18/18 (100%), NavigationResolutionScope 0/0 (100%), NavigationPathResolver 57/71 (80.3%), ResolvedNavigationHop 21/26 (80.8%), ResolvedNavigationLeg 3/6 (50%), NavigationSourceBinding 6/6 (100%); no public API change, resolver internal/unwired; evidence /tmp/opencode/148a/fix2/ | docs/specs/status/nav-resolver-148a-1.md

## ACT — #148-A slice A DONE / committed — 2026-10-02T15:52Z

- status: **DONE (slice A)** — cycle 1, revision r2, attempt 1/3, no further attempt. **r2 CHECK PASS (3/3)**. **#148 stays OPEN for slice B.**
- ACT commit: `8393f822d4fe99d45972e9c15142f27b2a899583` — subject `#148 Add navigation path resolver foundation (slice A)`.
- **Verification (r2 CHECK):** whole-solution Debug+Release build exit 0, 0 Warning(s)/0 Error(s); focused navigation tests **51 passed / 0 failed**; core regression **470 passed / 0 failed**; coverage **line 86.8 / branch 78.2** (>= 85/75); mutation **8 killed + 1 proven equivalent** (0 unexplained survivors); perf acceptance exit 0, **7 cases / 0 failures**, cached-vs-prepared ratio **1.95** (< 2.244 investigate trigger); full integration exit 0, **3042 passed / 0 failed / 187 pre-existing capability skips** (SQLite 559/0/41, PostgreSQL 575/0/25, SQL Server 559/0/41, MySQL 523/0/77, ClickHouse 103/0/0). Evidence: `/tmp/opencode/148a/d5/` (`build-debug.log`, `build-release.log`, `focused.log`, `regression.log`, `coverage-reportgen.log`, `mutation/SUMMARY.md`, `perf-acceptance.log`, `integration-run.log`, `per-provider.log`).
- **Defects fixed red→green (r2, no new revision):**
  - `C1` OneToOne direction inverted — fixed once; red `/tmp/opencode/148a/fix/c1-red.log` (2 failed on the inverted `DependentToPrincipal`) → green `/tmp/opencode/148a/fix/test-navigation-focused.log` (2 passed).
  - `148A-immutable-list-exposure` — published `ResolvedNavigationPath.Hops`/`NavigationResolutionScope.VisibleRoots` now backed by `ReadOnlyCollection<T>` over a copied array (`Array.AsReadOnly<T>([..])`), never a downcastable `List<T>`; regression `Resolve_PublishedCollections_AreTrulyImmutableSnapshots` red `/tmp/opencode/148a/fix2/red-proof.log` (1 failed) → green `/tmp/opencode/148a/fix2/green-proof.log` (exit 0).
  - additional r2 fixes: T1 cache isolation (`[Collection("Query cache controls")]`), C2 `SameIdentity` null guard, S3 unused helper removal, C3/C4 test gaps.
- **Committed scope:** 3 production (`Visitors/NavigationPathResolver.cs`, `NavigationResolutionScope.cs`, `ResolvedNavigationPath.cs`) + 1 test (`NavigationPathResolverTests.cs`) + 7 docs (design/roadmap/API-naming/limitations EN+RU) + 2 status files — #148-A artifacts only; no public API change; resolver internal/unwired. **No push; no merge.**
- **Slice B not included:** `NavigationExpansion` + `Any`/`Count`/`LongCount`/`Count` + `AsEntityBuilder<T>` + SQL/InMemory paths + end-to-end rejection remain; #148 stays OPEN.
- 2026-10-02T15:52Z | ACT | r2 | 1/3 | #148-A slice A committed (`8393f822d4fe99d45972e9c15142f27b2a899583`); r2 CHECK PASS (3/3); defects C1 + 148A-immutable-list-exposure red→green; #148 OPEN for slice B | `/tmp/opencode/148a/d5/`, `/tmp/opencode/148a/fix2/`

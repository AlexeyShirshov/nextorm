# AGENTS

## Build & toolchain
- .NET 10 SDK; production and test projects target `net10.0` (the source generator targets `netstandard2.0`). `global.json` pins only the test runner (`Microsoft.Testing.Platform`), not an SDK version.
- Central Package Management: reference packages as `<PackageReference Include="X" />` (no `Version`); add/update the version in `Directory.Packages.props`.
- `TreatWarningsAsErrors=true` for every project and configuration — nullable and analyzer warnings fail the build. Fix them or suppress explicitly via `NoWarn`.
- `Directory.Build.props` redirects build output into per-host-OS dirs: `bin/<linux|windows>/`, `obj/<linux|windows>/`, so WSL and Windows builds coexist. Look there for artifacts; there is no shared `obj/`.
- Benchmarks: `benchmarks/nextorm.benchmark` is a BenchmarkDotNet console app using `BenchmarkSwitcher` — `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWhere*`.

## C# semantic analysis (Roslyn-only)
- **Roslyn-only.** Definitions, references, call sites, implementations, overloads and renames go through the `roslyn` tool; text search reads only non-symbol material (string literals, comments, docs, configs). The global rule in `~/.config/opencode/AGENTS.md` is the authority.
- V2: the tool is never top-level — call it inside `execute`: `const r = await tools.roslyn({ action: "refs", target: "NextORM.Core.SqlBuilder.MakeSelect" }); return r`. A missing catalog entry means the catalog is partial, not the tool gone: run `search({ query: "roslyn" })` first.
- Batch calls in ONE `execute` (`Promise.all([...])`): the `MSBuildWorkspace` cold start is slow on this solution and is paid once. Pass `solution: "/home/alex/sources/nextorm/nextorm.slnx"` only when auto-detection is wrong or the call is made from outside this repo. The tool wraps the global `roslynq` binary.
- Actions: `structure`, `types`, `symbols`, `members`, `refs`, `callers`, `implementations`, `rename`; pass symbols by full name (`NextORM.Core.SqlBuilder.MakeSelect`).
- Bulk renames: `rename <symbol> <newName>` (dry-run), then `apply=true` once the diff looks right — never file-by-file edits. Verify with `dotnet build`.

### Searching text (ignore-aware only)
- Plain text is searched with ignore-aware tools only: the built-in `grep` (ripgrep), `rg`, or — cheapest — `git grep` (tracked files only), scoped to sources: `rg -n --glob '*.cs' '<literal>' src tests` or `git grep -n '<literal>' -- 'src/**' 'tests/**'`.
- Shell `grep -r` / `grep -rn` / `grep -R` / `find` over the tree is the one thing to avoid: they ignore `.gitignore` and walk `bin/`, `obj/` and `TestResults/` — ~1 GB of artifacts under `src`/`tests` on a built worktree — so one call reads ~1 GB to find a few MB. Search hygiene, not style.

## Layout
- `src/nextorm.core` is the engine: query builder/plan cache, expression visitors (`Visitors/`), dialects, in-memory context. Providers reference it: `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.clickhouse`; `nextorm.mariadb` builds on `nextorm.mysql`.
- Core file map (common mis-guesses, `file:line` for a start): SQL/AST expressions are in `Expressions/` (`Expressions/SelectExpression.cs`), not `Query/`; the SQL builder, planner and row mapping live under `DataContext/` (`DataContext/SqlBuilder.cs`, `DataContext/QueryPlanner.cs`, `DataContext/RowMapperFactory.cs`, `DataContext/RowMaterializerBuilder.cs`; note `DataContext/DataContext.cs` is a folder member, not a root `DataContext.cs`); CTE hoisting is `Builders/CteHoister.cs`; JSON shape/streaming is `Query/Json/` (`Query/Json/JsonShapePlan.cs`); CSV options are `Query/CsvStreamOptions.cs`; fluent builders are `Builders/`.
- `src/nextorm.core.sourcegenerator` is an empty `IIncrementalGenerator` stub — in the solution but referenced by no project, so it generates nothing today.
- `tests/nextorm.<provider>.tests` are dialect/SQL-generation tests using placeholder connection strings; they need no database. Only `tests/nextorm.integration.tests` talks to real databases, through `ProviderTestSuite` + `CommonTestSuite.*.cs` (a test added there runs against every provider; provider-only behavior belongs in `*SpecificTests.cs`).
- `.pdca/` — fixed PDCA workspace root in this repo (`§Skill workspace` of the canonical `pdca` skill): `.pdca/scripts/` holds the executable helper copies, `.pdca/status/` the status files of new cycles, `.pdca/<task>/rv<k>/` their evidence. Only `.pdca/status/**` is versioned: the scripts are local copies of the canonical skill sources and the evidence is local, so CI and product tooling must never read from `.pdca/**` (product tooling lives under `tools/`). Legacy records `docs/specs/status/**` and `artifacts/pdca/**` stay read-only evidence of past cycles — never rewrite or move them.

## Shared query commands & plan cache
- `.Any()`/`.Count()`/`.All()` and similar reuse **one** `QueryCommand` per `DataContext` (`EntityBuilderExtensions.GetAnyCommand` → `QueryCache.AnyCommand`; `ReplaceCommand` only swaps the referenced subquery, it does not reset command state).
- **Sticky** is the trap: `queryCommand.Cache = false` is backed by the sticky `_dontCache` field, so never mutate the shared command on behalf of a single call. The flag persists across calls, leaks to every later query on the context and silently disables the plan cache for the whole context — green tests, production regression.
- To prepare without caching, pass `storeInCache: false` to `_planner.GetPreparedQueryCommand(...)` — local to the call, the command untouched. The lazy temp-table path (`DataContext.GetPreparedTemporaryTableCommand`, `DataContext.cs`) is the reference example.

## TVP metadata & decimal precision
- The TVP auto-resolution path keeps **its own** metadata cache inside `DataContextCache` (`TvpMetadataCacheTests`): it must never seed the process-wide configured-metadata cache, always defers to a configured `From<T>(cfg)` mapping, and is cleared by `DataContextCache.Clear()`. Seeding the configured cache silently ignores a later fluent registration.
- `[DecimalPrecision(p, s)]` / fluent `DecimalPrecision(p, s)` are validated against the **bound provider type** (`converter?.ProviderType ?? propertyType`) in `DecimalPrecisionRules.Validate`; a non-decimal bound type or a pair declaring only one of precision/scale throws. Provider defaults stay (SQL Server `decimal(38,18)`, ClickHouse `Decimal(38,10)`).

## Tests
- **Assertions: FluentAssertions only.** Every value/exception assertion in `tests/**` goes through FluentAssertions — `actual.Should().Be(...)`, `actual.Should().BeSameAs(...)` for reference identity (`PropertyInfoAssertions` has no `Be` in 7.x), `FluentActions.Invoking(...).Should().Throw<T>()`, `await act.Should().ThrowAsync<T>()`. `Xunit.Assert` is reserved for the skip primitives `Assert.Skip` / `Assert.SkipUnless` / `Assert.SkipWhen`, which have no FluentAssertions equivalent. `tests/nextorm.core.tests/Conventions/AssertionStyleTests.cs` parses every test source and fails on any other `Assert.*` and on a test project missing the FluentAssertions reference (version pinned centrally).
- **Integration tests use a real DBMS.** A test in `tests/nextorm.integration.tests` must exercise a live database: a container-backed provider through `ProviderTestSuite` / `ITestProvider`, a raw ADO.NET connection to a container, a file-based SQLite connection, or — for **core-level** tests only — a single provider (SQLite is the natural choice: in-process, no container). A test that needs no database is a unit test and belongs to the matching unit project: `tests/nextorm.core.tests` for core/DI/type contracts and the in-memory provider, `tests/nextorm.<provider>.tests` for SQL-generation and dialect assertions over a placeholder connection. The in-memory provider is never used in the integration project (`InMemoryDataContext` is unit-only); `tests/nextorm.core.tests/Conventions/IntegrationSuiteScopingTests.cs` enforces both halves by parsing every integration source.
- Run: `dotnet test tests/<project> -c Debug` works (via the Microsoft.Testing.Platform runner in `global.json`); focus one test with `--filter "FullyQualifiedName~InMemoryTests"`. xunit v3 native filters also work: `dotnet run --project tests/<project> -c Debug -- -class nextorm.core.tests.InMemoryTests` (`-method`, `-namespace`, `-trait`).
- **The container suite is not optional evidence.** `.opencode/skills/running-integration-tests/SKILL.md` owns the whole story — `DOCKER_HOST`, the Podman socket, the canonical run, the skip budget and troubleshooting. Load it before running. The suite is fail-closed: without `DOCKER_HOST` the container-backed providers **fail**, and the only opt-out is `NEXTORM_SKIP_PROVIDERS=postgres,sqlserver,mysql,clickhouse` (a run with opt-outs is no evidence for those providers). A missing socket is a step to fix, not a reason to stop: start the Podman machine, re-check and rerun. Never present a suite that skipped provider tests as green.
- External databases: `NEXTORM_POSTGRES_CONNECTION`, `NEXTORM_SQLSERVER_CONNECTION`, `NEXTORM_MYSQL_CONNECTION`, `NEXTORM_CLICKHOUSE_CONNECTION` (see `tests/nextorm.integration.tests/Providers/*Container.cs`).

## Coverage
- Whole-solution gate: `coverage.settings.xml` covers only `nextorm.{core,sqlite,postgres,sqlserver}`; `MIN_LINE_COVERAGE=85` and `MIN_BRANCH_COVERAGE=75`, hard-failing on `main` only. The thresholds and the `dotnet-coverage` → `reportgenerator` steps live in `.github/workflows/dotnet.yml`.
- The integration suite has its own gate: `coverage.integration.settings.xml` measures what `tests/nextorm.integration.tests` alone covers across every provider assembly it references (core, sqlite, postgres, sqlserver, mysql, mariadb, clickhouse, entityframeworkcore). Thresholds `MIN_INTEGRATION_LINE_COVERAGE=65` / `MIN_INTEGRATION_BRANCH_COVERAGE=74`, same main-only hard-fail. `NextORM.Core.InMemory*` is excluded from this gate's denominator (`reportgenerator -classfilters:"-NextORM.Core.InMemory*"`): the in-memory provider is unit-only by policy, so the integration suite can never execute those paths. The numbers are not comparable to 85/75 — unit-only paths are 0 % there by design. Reproduce:
  ```bash
  DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
    dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura \
      -o tests/coverage/integration.cobertura.xml \
      "dotnet test tests/nextorm.integration.tests --no-build --verbosity normal"
  dotnet tool run reportgenerator -reports:tests/coverage/integration.cobertura.xml \
    -targetdir:tests/coverage/integration-report -reporttypes:"Html;TextSummary" \
    -riskhotspotassemblyfilters:"+nextorm.*"
  ```
- The accepted measurement and its accounting (baselines, branch deltas, skip budget) live in the cycle's status record under `docs/specs/status/**`; keep the thresholds unchanged.

## Docs
- DocFX is a local tool: `dotnet docfx docs/docfx.json`. `docs/api/` and `docs/_site/` are generated and gitignored; article pages are hand-written.
- Renaming a public type or method requires updating both `docs/**` and `docs/ru/**` (prose, samples, source paths, curated API reference) in the same change. Search both trees for the old name first (`rg -n "<old-name>" docs docs/ru`, or `git grep`).
- Moving a guide page into a separate section (or deleting one) leaves a gap in the numbered `docs/guide/NN-*.md` series: renumber the remaining pages consecutively, and update every link and `toc.yml` entry in both `docs/**` and `docs/ru/**` in the same change — never leave a hole (e.g. `30 → 32`).
- Public docs (`docs/**`, `docs/ru/**`, root `readme.md`) do not link to `docs/specs/**`: specs are internal, excluded from the DocFX build and never published. No hyperlinks, no relative `.md` links, no GitHub `blob` links to specs from article pages or the readme — keep the reasoning inline or point to a public page.

## Git
- The user pushes; `git push` stays with them.
- No commits unless explicitly asked — including in separate git worktrees.
- No merges and no merge commits unless explicitly asked: `git merge` inherently requires commits, so patch is the default integration mechanism.
- Exception — a `pdca-collection` cycle started with explicit auto-commit permission may (a) commit on `collection/**` branches inside the cycle worktree and (b) `git merge --no-ff` a group branch into the current branch. Outside such a cycle both bans above stand.
- Worktrees integrate by **patch, not commits**: in each worktree produce a diff (`git diff <base> > /tmp/<name>.patch`, or `git diff` for uncommitted changes), apply it into the target tree (`git apply` / `patch`), and leave the result uncommitted for the user to review.

## Conventions
- Async naming: use the `Async` suffix only when a method has a synchronous twin. A lone async method keeps the plain name.
- Line endings: CRLF throughout (`core.autocrlf=true`). Preserve CRLF when writing/editing; never leave LF-only or mixed. Normalize after edits: `perl -pi -e 's/\r?\n/\r\n/g' <file>`.

## Brainstorming & GitHub tracking
- In this repo, load `.opencode/skills/nextorm-brainstorming/SKILL.md` together with the global `brainstorming` skill before starting the brainstorm.
- For any agreed feature/refactor/fix, create or update its GitHub issue and milestone, and record `#N` + verified URL in the spec/handoff, before asking for written-spec review or handing off to planning/implementation.
- The overlay only adds tracking; brainstorming's approval gates stay unchanged, and no git commit/push/merge is granted.
- PDCA work keeps its existing spine — use `nextorm-pdca`; the overlay does not turn PDCA into brainstorming.

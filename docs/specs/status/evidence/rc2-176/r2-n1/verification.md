# D176.6 — verification

## UTF-8 / CRLF

All hand-written touched files are UTF-8 and CRLF (checked byte-wise):

- `docs/guide/28-streaming-data.md`, `docs/ru/guide/28-streaming-data.md`,
  `docs/specs/roadmap/todo_json_streaming.md`
- `src/nextorm.core/nextorm.core.csproj`
- `benchmarks/nextorm.benchmark/SqliteBenchmarkJsonStreamPhase2.cs`
- evidence notes under `docs/specs/status/evidence/rc2-176/r2-n1/**` (hand-written `.md`)

Generated logs / patches under the evidence tree are byte logs (LF acceptable) and are not part of
the CRLF-normalized source set.

## `git diff --check` (scoped)

```
git diff --check -- docs/guide/28-streaming-data.md docs/ru/guide/28-streaming-data.md \
  docs/specs/roadmap/todo_json_streaming.md src/nextorm.core/nextorm.core.csproj \
  src/nextorm.core/Query/QueryCommand.QueryPreparer.cs src/nextorm.core/Query/Json/JsonRowWriterFactory.cs \
  src/nextorm.core/Query/Json/JsonShapePlan.cs src/nextorm.core/Query/QueryCommand.cs \
  src/nextorm.core/DataContext/DataContext.cs
```

**exit 0** (no whitespace errors).

## DocFX

`dotnet docfx docs/docfx.json` **exit 0** — 2 pre-existing warnings (duplicate source files in
`nextorm.core.sourcegenerator`), 0 errors; no warning references the touched pages (`docfx.txt`).

## Mutation restoration

All six mutations were restored from byte-exact backups (`cmp` clean against the pre-mutation copies);
`rg "Mutation:" src benchmarks tests` finds nothing, and the affected suites are green on the restored
final tree (40/40 core writer, 63/63 sqlite JSON; every mutant had produced a nonzero test exit).
Refreshed on the r=2/n=2 loop-back tree; coverage (86.8 % line / 79.3 % branch) and C07 were re-run on
the same final tree.

## Throwaway files

None in the repository. The only untracked additions are D176 artifacts (new JSON sources/tests, the
C08 benchmark and its BDN reports, the evidence tree). Mutation backups live under `/tmp/opencode/`
outside the workspace.

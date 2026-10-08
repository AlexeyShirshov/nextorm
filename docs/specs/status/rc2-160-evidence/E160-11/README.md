# E160-11 — docs / links (D160.3-6 verification, plan r=3, rv=4)

## Commands and results

```
git diff --check
dotnet docfx docs/docfx.json
```

- `git diff --check`: exit **0** (clean; only benign `LF will be replaced by CRLF` warnings for
  shell-redirected log artifacts and regenerated BenchmarkDotNet reports). Three pre-existing
  trailing-whitespace lines (regenerated BDN `SqliteBenchmarkWhere` reports + the acceptance log)
  were stripped to restore a clean check. Log: `git-diff-check.log`.
- `dotnet docfx docs/docfx.json`: exit **0**, **2 pre-existing warnings / 0 errors** (duplicate
  `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` source files in the generator
  project — unrelated to D160). Log: `docfx.log`. Wall 45 s.

## Documented-behaviour check (r=3 delta, F2/F4)

The r=3 units are fixes to **already-documented** behaviour, so the EN/RU article pages are
unchanged:

- F2 (stored non-root receivers) restores the documented free positional/alias mixing
  (`docs/guide/02-joins.md` +RU; `docs/advanced/limitations.md` +RU).
- F4 (correlated APPLY through the alias seam) uses the existing correlated-APPLY contract; the
  single-entity-receiver restriction and the no-lateral providers are already documented in
  `docs/guide/02-joins.md` §«Correlated APPLY / LATERAL» (+RU) and
  `docs/advanced/limitations.md` §«Коррелированный источник APPLY / LATERAL» (+RU).
- No public links into `docs/specs/**` were added.

## Register update (same unit)

`docs/specs/design/API-NAMING-REVIEW.md` §#160 was extended with a
**«Дополнение 08.10.2026 — D160.3 r=3 (F2/F3/F4)»** paragraph recording the additive generated
public-surface delta (8 `${suffix}_P{n+1}` pairs from F2, 10 correlated-APPLY alias types from F4,
`JoinSourceKind.Correlated` classification, the documented alias-after-correlated-APPLY bounded
limitation, and F3's `Item<digits>` → `NORMGEN002` with no new public surface / diagnostic id).
Manual public API unchanged; `PublicAPI*.txt` still absent (Step 5, issue #53).

# E177-12 — transport prototype provenance (D177-1), carried into the R177 reclaim

- Unit: `R177-D177-R6` (cycle N=1, plan revision r=2, evidence contract rv=2, iteration n=1)
- Product baseline: `2ac20818bc5bdb429406f99758b1db50a470561e` (`#177 D177 native JSON fast-path`)
- Predecessor status: `docs/specs/status/rc2-177-native-json-1.md` (D177, r=2/rv=2; DO unit **D177-1**
  "SQL Server transport/semantic prototype"); terminal outcome for that attempt was CHECK completeness FAIL,
  not a transport defect.
- Decision: prototype **still applies**; not repeated. No reproduced transport defect invalidates it.

## Actual source artifacts (read, hashed this revision)

The prototype/measurement harness is an evidence artifact, not product/test/doc source; it lives under the
gitignored `artifacts/` tree and is not part of `nextorm.slnx`.

| Artifact | Size | sha256 |
|---|---|---|
| `artifacts/D177/rv2/perf/harness/Program.cs` | 8936 B | `d6656f847d85f7eb224f51b482145e55b978867fb7ac49423f32a24c8074ed49` |
| `artifacts/D177/rv2/perf/harness/harness.csproj` | 765 B | `190fdbc2fa8d03ff652894e17d5e3fc181b1e09b5ecc2356fa1cd1fd1a266aaa` |
| `artifacts/D177/rv2/perf/harness-run.log` | 1939 B | `f3a12dddd52bf79c69fc69e7742266cf0cf9b6fdc8b0ba7e0689f72bd5420e72` |
| `artifacts/D177/rv2/perf/perf-summary.md` | — | `78d8ef587dcd6ebba8e7ba679ecc2b41a5d0531edbe5fdf121179235cebc06a6` |
| `artifacts/D177/rv2/ledger.md` §E177-12 (:133-135) | 22682 B | `3d2adadcedbb9442ee666e423b21e8c997cdf7a5e9945574f7179d3510f38ed4` |

- Harness mechanism: `Program.cs` opens a live SQL Server (env `NEXTORM_SQLSERVER_CONNECTION`,
  `Program.cs:19-20`), seeds sizes `{0,1,100,10000}`, and compares the native (`FOR JSON`) document to the
  managed document parsed with `JsonNode.DeepEquals` (`Program.cs:22/52-57`). Invocation (per
  `perf-summary.md`, gitignored evidence): `["dotnet","run","--project",
  "artifacts/D177/rv2/perf/harness/harness.csproj","-c","Release"]`, exit 0.
- Proved predicate (bytes from `harness-run.log`): one complete valid document per size
  (`logically_equal=true` at 0/1/100/10000), empty result = `[]` (`size 0 native_bytes 2`), Unicode/escaped
  payloads parse as a single document, and `RetainedBytes=0` at all sizes (fixed rented pump buffers,
  bounded app-owned memory). This is exactly the E177-12/A1,A3,A8 predicate.

## Why it still applies (no repeat)

- The transport under test is the **unchanged in-tree product** at `2ac20818`
  (`JsonNativeStream.WriteDocument` `src/nextorm.core/Query/Json/JsonNativeStream.cs:131`;
  `ValidateReader` :244). This reclaim unit is tests/evidence-only: `git diff --name-only` shows **no
  `src/**` change**, so the prototype's transport contract is byte-for-byte the same code it validated.
- The only product defect reproduced during this cycle is the projected `SqlFunctions.Parameter<T>`
  unaliased render under native `FOR JSON` (`findings/native-parameter-projection-defect.md`, issue #208).
  It is an alias/eligibility failure at SQL composition (`SqlBuilder`/`SqlSourceRenderer`), **not** a
  document-chunk transport defect, so it does not invalidate the chunk/empty/Unicode/bounded-memory
  prototype.
- Anchor continuity: current live native evidence (E177-01/02/04, `logs/sqlserver-nativejsonstream.log`,
  16/16; E177-10 `logs/native-measurement.log`) exercises the same `WriteDocument`/`WriteDocumentAsync`
  pump on a live SQL Server, so the predecessor prototype remains applicable without a fresh one-off run.

## Conclusion

E177-12 pre-DO reconnaissance obligation satisfied by provenance: actual artifacts identified and hashed,
predicate met, applicability reasoned. No repeated prototype run; no reproduced transport defect.

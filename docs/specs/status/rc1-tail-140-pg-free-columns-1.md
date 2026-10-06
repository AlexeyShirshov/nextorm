# PostgreSQL free/partial column list in projection — issue #140 (task D140)

- task: D140
- issue: #140 (https://github.com/AlexeyShirshov/nextorm/issues/140) — **closed on GitHub; NOT reopened**
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: **done**
- cycle: N=1
- plan revision: r=2
- attempt: n=1/3
- contract: rv=2 (supersedes rv=1)
- mode: autonomous (collection); auto-commit of task files only
- git: no push/merge; the ACT commit happens later

## Resume (supersedes the prior STOP)

The prior STOP (`escalate`: author clarification required, recorded in the previous revision of this
file) is **superseded** by the author's **option A**: #140 is a **documentation** task — document the
actual surface of the PostgreSQL record functions and point schema-less users at the existing
alternatives. #140 is already **closed on GitHub and is NOT reopened**; the scope is **docs-only**
(no `.cs`, no tests, no config).

## Goal

Make the EN + RU documentation unambiguous about PostgreSQL `jsonb_to_record`/`jsonb_to_recordset`:
their result schema is declared by the caller's row type and rendered as the alias
column-definition list (`AS x(a int, b text)`); PostgreSQL requires that column list, so a free or
partial column list **without** a caller-declared `TRow` is not supported for these two functions.
A reader who wants a schema-less result is routed to the three existing paths (a
`[DynamicColumns]` dictionary store, the raw reader, `jsonb_each`/`jsonb_each_text`/`jsonb_object_keys`).

## Acceptance criteria

- **R140-01** (canonical, EN+RU): `docs/guide/provider-specific/postgresql.md` `## Dynamic record
  schema` / `docs/ru/guide/provider-specific/postgresql.md` `## Динамическая схема записи` state
  that the two functions render the caller's row type as the alias column-definition list
  (`AS x(a int, b text)`), that PostgreSQL requires it, and that a free/partial column list without a
  caller-declared `TRow` is **not supported** for these two functions. *Negative case:* the text must
  not claim or imply that a free/partial column list works for them; a short illustrative SQL snippet
  is allowed but **no new public API** may be invented.
- **R140-02** (canonical paths, EN+RU): the same sections list the three schema-less paths with a
  one-line purpose each and a **public** link: `[DynamicColumns] Dictionary<string, object?>` →
  `../27-dynamic-columns.md`; the raw `ToDataReader`/`ResultSet` reader → `../12-raw-sql.md` /
  `../26-large-objects.md`; `jsonb_each`/`jsonb_each_text`/`jsonb_object_keys` →
  `../11-table-valued-functions.md#dynamic-result-schema`. *Negative case:* no link to `docs/specs/**`.
- **R140-03** (TVF guide, EN+RU): `docs/guide/11-table-valued-functions.md` `## Dynamic result
  schema` / RU `## Динамическая схема результата` explicitly list the three alternatives and link the
  canonical section; the existing `jsonb_each*` prose is reused, no long examples duplicated.
  *Negative case:* no new/duplicated full SQL example block is added there.
- **R140-04** (limitations, EN+RU): `docs/advanced/limitations.md:39-40` /
  `docs/ru/advanced/limitations.md:39-40` add a short caveat that the limitation applies to the record
  functions with a caller-declared schema, that schema-less scenarios have the existing alternatives,
  with a public link to the canonical section. *Negative case:* the caveat must not broaden the
  `json_populate_record(set)` limitation into "PostgreSQL JSON is unsupported".
- **R140-05** (consistency and scope): EN and RU are semantically identical; every touched link is
  public; CRLF is preserved on all six docs; `dotnet docfx docs/docfx.json` succeeds. **Allowed
  paths** are exactly the six agreed docs — `docs/guide/provider-specific/postgresql.md`,
  `docs/ru/guide/provider-specific/postgresql.md`, `docs/guide/11-table-valued-functions.md`,
  `docs/ru/guide/11-table-valued-functions.md`, `docs/advanced/limitations.md`,
  `docs/ru/advanced/limitations.md` — **plus** the mandatory cycle status file
  `docs/specs/status/rc1-tail-140-pg-free-columns-1.md` (bookkeeping only, excluded from DocFX).
  **Forbidden:** code, tests, config, TOC files, any other specs, `docs/guide/27-dynamic-columns.md`,
  `docs/providers/postgres.md` (+RU). *Negative case:* any forbidden file in `git status`, or a
  non-public link, fails the criterion.

## Task list

- **D140.1** — canonical provider-specific PostgreSQL guide, EN + RU (`## Dynamic record schema` /
  `## Динамическая схема записи`): requirement + three alternatives.
- **D140.2** — TVF guide, EN + RU (`## Dynamic result schema` / `## Динамическая схема результата`):
  three alternatives + link to the canonical section.
- **D140.3** — limitations, EN + RU (`docs/advanced/limitations.md:39-40`): caveat + public link.
- **D140.4** — verification (DocFX boundary build, EN↔RU parity, scope/CRLF checks).

## Variant matrix

| # | Variant | Decision |
|---|---|---|
| V1 | Extend the canonical `provider-specific/postgresql.md` section and cross-link the TVF guide + limitations | **Chosen** — one canonical home, minimal diff |
| V2 | Document only in the TVF guide `## Dynamic result schema` | Rejected — the canonical home required by the brief is the provider-specific guide |
| V3 | Edit `docs/providers/postgres.md` (+RU) instead | Rejected — explicitly forbidden by the brief |
| V4 | Reopen #140 / request another author clarification | Rejected — #140 is closed; author option A fixes the scope as docs-only |

## Test strategy

Docs-only change: no test project is affected, so there is no inner-loop test subset. The only
executable verification is the DocFX build and EN↔RU parity. Boundary command:
`dotnet docfx docs/docfx.json`. Inner-loop file/symbol tests: none.

## Docs plan

D140.1–D140.3 as above, EN first, then the RU mirror kept semantically identical. Links must resolve
inside the DocFX docset; no `xref` is invented, no public API is added.

## Perf-measurement decision

**Not applicable** — this is a documentation-only cycle and no runtime code path changes. Per the
[`nextorm-pdca`](../../../.opencode/skills/nextorm-pdca/SKILL.md) overlay: «Для несвязанных
documentation-only циклов приёмка не требуется».

## Reconnaissance

**None** — the brief already pins every file and section; no additional code exploration is needed.

## Unit mode

**Single-writer** — one `coder` edits all six docs and the status file; no parallel writers.

## Evidence contract

- **E140-01** — `git diff` of the six docs, with `file:line` anchors of the added content (EN + RU).
- **E140-02** — `dotnet docfx docs/docfx.json` exit code, warning/error counts, full log path.
- **E140-03** — EN↔RU parity check of the added statements.
- **E140-04** — CRLF verification of all six files.
- **E140-05** — `git status --porcelain` / `git diff --stat` scope check (six docs + this status file
  only; untracked `artifacts/`, `docs/_site/` acceptable and unstaged).
- **E140-06** — caveat attribution check in EN + RU: the record-function caveat is present in the
  PostgreSQL `json_populate_record(set)` row (`docs/advanced/limitations.md:40`,
  `docs/ru/advanced/limitations.md:40`) and absent from the ClickHouse
  `format`/`merge`/`input` row (`docs/advanced/limitations.md:39`,
  `docs/ru/advanced/limitations.md:39`).

## CHECK r=1 (n=1) — **fail**

Two findings (both actionable, no false positives):

- **`D140-record-caveat-wrong-row`** — the new record-function caveat (and its public link) was
  attributed to the **ClickHouse** `format`/`merge`/`input` row instead of the **PostgreSQL**
  `json_populate_record(set)` row. Provider attribution wrong in both EN and RU. Observed at r=1/n=1;
  fixed in r=2 (E140-06).
- **`D140-frozen-scope-extra-specs-file`** — the brief's R140-05 scope text listed `docs/specs/**` as
  forbidden while the cycle's own **mandatory status file** (`docs/specs/status/...`) must be edited
  as bookkeeping. Scope wording corrected in r=2 (R140-05: allowed = six docs + this status file;
  forbidden = code, tests, config, TOC, any other specs, `docs/guide/27-dynamic-columns.md`,
  `docs/providers/postgres.md`).

Candidate `stable-anchors` (line-number drift of the moved caveat) — **dismissed: no defect**; the
row/section anchors are semantic, not line-based, and both rows remain at lines 39/40.

## CHECK r=2 (n=1) — **PASS**

Criterion matrix, all checked clean:

- **R140-01** — canonical EN+RU sections state that `jsonb_to_record`/`jsonb_to_recordset` render the
  caller's row type as the alias column-definition list (`AS x(a int, b text)`), that PostgreSQL
  requires it, and that a free/partial column list without a caller-declared `TRow` is not supported.
  No new public API invented.
- **R140-02** — the same sections list the three schema-less paths with public links
  (`../27-dynamic-columns.md`, `../12-raw-sql.md` / `../26-large-objects.md`,
  `../11-table-valued-functions.md#dynamic-result-schema`); no `docs/specs/**` link.
- **R140-03** — TVF guide EN+RU lists the three alternatives and links the canonical section; no
  duplicated full SQL example.
- **R140-04** — limitations EN+RU carry a short caveat with a public link to the canonical section;
  the caveat does not broaden the `json_populate_record(set)` limitation.
- **R140-05** — EN↔RU semantically identical; every touched link public; CRLF preserved; DocFX
  succeeds. Exactly the **7 allowlisted files** were touched (six docs + this status file).
- **E140-06** — caveat attribution verified: present on the PostgreSQL `json_populate_record(set)` row
  (`docs/advanced/limitations.md:40`, `docs/ru/advanced/limitations.md:40`) and absent from the
  ClickHouse `format`/`merge`/`input` row (lines 39).

Verification: `dotnet docfx docs/docfx.json` — **exit 0, 0 errors, 2 pre-existing warnings**; full log
`/tmp/d140-docfx-r2.log`.

Both r=1 defects were resolved in r=2, no recurrence:

- `D140-record-caveat-wrong-row` — caveat now on the PostgreSQL row (E140-06 clean).
- `D140-frozen-scope-extra-specs-file` — scope wording corrected; only the 7 allowlisted files changed
  (R140-05).

## Progress log

- 2026-10-06T07:44Z | DO | r=1 | n=1/3 | DO started | this file
- 2026-10-06T07:53Z | CHECK | r=1 | n=1/3 | CHECK r=1 fail (D140-record-caveat-wrong-row, D140-frozen-scope-extra-specs-file); stable-anchors candidate dismissed (no defect) | CHECK r=1 section above
- 2026-10-06T07:53Z | ACT | r=2 | n=1/3 | Replanned: CHECK r=1 fail -> move caveat to PG row + explicit status-file scope | rv=2 supersedes rv=1
- 2026-10-06T07:53Z | DO | r=2 | n=1/3 | DO started r=2/n=1 | this file
- 2026-10-06T07:58Z | CHECK | r=2 | n=1/3 | CHECK r=2/n=1 PASS (R140-01..05, E140-06 clean; docfx exit 0, 0 errors, 2 pre-existing warnings; 7 allowlisted files; both r=1 defects resolved, no recurrence) | /tmp/d140-docfx-r2.log
- 2026-10-06T07:58Z | ACT | r=2 | n=1/3 | ACT done — D140 finalized **done** | this file

# PostgreSQL free/partial column list in projection — issue #140 (task D140)

- task: D140
- issue: #140 (https://github.com/AlexeyShirshov/nextorm/issues/140)
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: **incomplete**
- cycle: N=1
- plan revision: r=1
- attempt: n=1/3
- contract: rv=1
- mode: autonomous (collection); no auto-commit of code (no code produced)
- git: no push/merge; status bookkeeping only

## Goal (from issue #140)

PostgreSQL **free/partial column list in projection** — the issue requests a way to project a
caller-selected subset ("free" / "partial") of columns, without the current requirement to declare
the full row shape in advance. The issue is the open follow-up of milestone `1.0.9-rc1` and is
unchanged: it has **no confirmed capability meaning**.

## Acceptance stated by the issue

- **SQL-generation tests** covering the intended (free/partial projection) shape.
- **EN + RU docs** describing the resulting surface.

Both acceptance items are **unmet and not executable as written**: the issue does not identify the
target capability, the shape syntax, or the provider scope, so no SQL-generation test or doc statement
can be authored without guessing.

## Gathered facts (file:line)

- **Shipped record functions:** `jsonb_to_record<TRow>` / `jsonb_to_recordset<TRow>` are declared at
  `src/nextorm.core/Query/SqlFunctions.Postgres.cs:716-729`
  (`ResultSchema = TableFunctionSchema.AliasColumnList`).
- **Dialect support:** `src/nextorm.postgres/PostgresDialect.cs:288-294` (`SupportsTableFunction` lists
  `jsonb_to_record` / `jsonb_to_recordset`), `:297` (`SupportsResultSchema` → `AliasColumnList`),
  `:300-306` (`MakeTableFunctionAlias(username(...))`).
- **SQL-generation tests:** `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1768-1791`
  (`TableFunction_JsonbToRecord_ShouldRenderAliasColumnList`,
  `TableFunction_JsonbToRecordset_ShouldRenderAliasColumnList`).
- **Integration tests:** `tests/nextorm.integration.tests/PostgresSpecificTests.cs:236-261`
  (`JsonbToRecord_WithDeclaredSchema_ShouldReturnRow`, `JsonbToRecordset_WithDeclaredSchema_ShouldReturnRows`).
- **Docs EN:** `docs/providers/postgres.md:255-260` (record functions, caller-declared `TRow` schema
  rendered as the alias column-definition list).
- **Docs RU:** `docs/ru/providers/postgres.md:257-262` (same) and the table-function table `:350`.
- **Limitations:** `docs/advanced/limitations.md:39-40` and `docs/ru/advanced/limitations.md:39-40` —
  the free-form record path is already exposed via a caller-declared `TRow`; `json_populate_record(set)`
  is the only variant that *would* fill a free column list but needs a mapped base record type.
- **Roadmap gaps:** `docs/specs/roadmap/sql-function-coverage-gap.md:141-144` — `json_to_record(set)` and
  `jsonb_populate_record(set)` remain listed as gaps; `jsonb_to_record`/`jsonb_to_recordset` are shipped.
- **Prior disposition:** `docs/specs/status/collection-1.0.9-rc1.md:40` — **#140 blocked**:
  "free column list" has no confirmed meaning or identifiable capability.

> Note: the brief anchored `SqlFunctions.Postgres.cs` and `postgresql.md`; the concrete files resolve to
> `src/nextorm.core/Query/SqlFunctions.Postgres.cs` and `docs/providers/postgres.md` (RU `docs/ru/providers/postgres.md`).
> The line numbers in the brief match those files.

## Escalate decision (strong tier) — authoritative, recorded verbatim-summarized

- Disposition: **clarification required**.
- Options **A–E cannot be selected**: none of the candidate interpretations of "free/partial column
  list in projection" is identifiable from the issue text; each would require speculative scope.
- **No code** was written and no test/doc was changed.
- The **planner was not re-invoked** (no admissible revised plan; re-gathering would not resolve an
  author-intent question).
- **Milestone unchanged**: #140 stays in `1.0.9-rc1`; no re-placement was authorized.
- Routing by recording only — the decision is not re-decided.

## Q140 — author question (to post later; NOT posted)

> **Q140 (issue #140 — "PostgreSQL free/partial column list in projection"):** What concrete capability
> does "free/partial column list in projection" mean for NextORM? Specifically:
>
> 1. **Target shape** — is it (a) projecting a caller-selected subset of an entity's mapped columns in
>    `Select`, (b) projecting untyped/free columns from a raw source (`FromSql`/`From`) without a
>    `BindEntity<T>` declaration, (c) free-form JSON record output (`json_to_record`/`jsonb_to_record`
>    without a caller-declared `TRow`), or (d) something else?
> 2. **Input source** — which provider/source is in scope (a real table, a raw SQL fragment, a JSON
>    document, a composite/record value)?
> 3. **Column list origin** — where does the free/partial list come from (a lambda projection, an explicit
>    string list, the result metadata) and how are column names/types resolved at build time?
> 4. **Unsupported cases** — what should happen for unknown/missing/extra columns (throw, skip, null)?
> 5. **Provider scope** — PostgreSQL only, or every provider?
>
> An answer to (1)–(3) is sufficient to unblock a PLAN; without it the issue has no identifiable
> capability and cannot produce deterministic SQL-generation tests or EN/RU docs.

## Rationale

With no confirmed capability meaning, there is **no admissible revised plan**: any PLAN branch would
invent scope, acceptance and docs, violating the evidence-over-assertion and frozen-acceptance rules.
Therefore the correct outcome is an **autonomous STOP** routed by recording the `escalate` decision.
**No cleanup**: the already-made G1 commits (D161, D197) stay on `1.0.9-rc1`; no rollback, no re-plan,
no task re-dispatch.

## Result

- Status: **incomplete**.
- Issue #140: **remains open**, pending the author's answer to Q140.
- Patch: **none** — no code, test, docs, or config changes (status bookkeeping only).
- Collection lane G1: stopped after D197; remaining tasks D134, D194, D150, D193, D198, D195, D196
  marked **incomplete** ("группа остановлена"), not executed.

## Progress log

- 2026-10-06T08:32Z | STOP | r=1 | n=1/3 | STOP: escalate decision — author clarification required | this file; `docs/specs/status/collection-1.0.9-rc1-tail.md`

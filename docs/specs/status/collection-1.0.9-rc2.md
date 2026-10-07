# Collection 1.0.9-rc2

- collection-id: `1.0.9-rc2`
- input: open issues of GitHub milestone `1.0.9-rc2` (milestone #20): 151, 153, 154, 157, 159, 160, 162, 167, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 184, 185, 188, 189, 190, 191 (26 tasks)
- brief source: `/tmp/opencode/rc2/briefs.md`
- mode: autonomous; auto-commit authorized by explicit user request and the repo AGENTS `pdca-collection` exception; **push never**
- base: branch `1.0.9-rc2` at `18659e41` (merge of PR #205 from `1.0.9-rc1`)
- collection revision: r1; evidence revision: rv1; PLAN-revision vector: all admitted tasks r=1/rv=1
- collection status: **in-progress** — P done (24 own PLANs `ready`), ALL-barrier evaluated over 24 admitted tasks; DO **gated** on the D191 written-spec review (see Blockers)

## Admission / gaps (P)

- **Admitted (24, `plan_state=ready`):** D151, D153, D154, D157, D159, D160, D162, D167, D169, D170, D173, D174, D175, D176, D177, D178, D179, D180, D184, D185, D188, D189, D190, D191.
- **Excluded-gap (2):**
  - **D171** (#171) — `revisit on demand` placeholder: no consumer, no agreed API/semantics, no derivable acceptance criteria; existing guard already rejects (`EntityBuilder.cs:779-784`). Not done, not superseded; issue stays **OPEN** in milestone `1.0.9-rc2`. Trigger: concrete consumer + agreed API.
  - **D172** (#172) — awaiting-consumer: issue's own precondition unmet; docs `relationships.md:67` record the feature as *not provided*. Not done, not superseded; issue stays **OPEN** in milestone `1.0.9-rc2`. Trigger: consuming scenario + agreed API/semantics.
- Rationale: both are uninventable-scope placeholders; excluding them lets the ALL-barrier be evaluated over the 24 executable tasks without inventing scope. (Decided by `planner`, r=1.)

## Groups

Single connected component by shared core footprint (`EntityBuilder`, `SqlBuilder`, `DataContext`, mapping/JoinInto, JSON cluster, shared docs/tests). Disjoint-footprint groups are impossible. Per `pdca-collection`, a single group works in the **current worktree/branch**: no group/task worktrees, no group branches, no `git merge --no-ff`; authorized commits go to branch `1.0.9-rc2`.

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| G1 | 24 admitted tasks | D153 → D154 → D157 → D159 → D160 → D184 → D191 → D162 → D170 → D174 → D175 → D173 → D190 → D176 → D178 → D180 → D177 → D179 → D185 → D167 → D169 → D189 → D188 → D151 | current worktree | 1.0.9-rc2 | in-progress |

Intra-group order is a serialization dependency (footprint overlap) plus one functional edge `D189 → D188`; it is not a functional chain for the rest.

## Tasks

| task | issue | group | branch | status | plan_state | selected_variant | cycle_id | plan_revision | status file | reason + patch |
|---|---|---|---|---|---|---|---|---|---|---|
| D151 | #151 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-151-stryker-mutation-1.md | Stryker re-run native extreme-row |
| D153 | #153 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-153-public-extensibility-1.md | closed #153 (8401ca51) |
| D154 | #154 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-154-dto-public-ctors-1.md | closed #154 (5d72a79) |
| D157 | #157 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-157-register-finding-25-1.md | closed #157 (5978556f) |
| D159 | #159 | G1 | 1.0.9-rc2 | done | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-159-cte-direct-joins-1.md | closed #159 (078a7a2d) |
| D160 | #160 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-160-join-alias-mixing-1.md | Join alias mixing + root alias |
| D161 | - | - | - | - | - | - | - | - | - | (reference only: #130/#161 hint APIs already shipped) |
| D162 | #162 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-162-navigation-temp-tvp-fail-closed-1.md | V32 navigation temp/TVP fail-closed |
| D167 | #167 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-167-csv-chunked-read-1.md | CSV chunked read for byte[] (P0 validator prerequisite) |
| D169 | #169 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-169-dml-scope-hints-1.md | DML scope hints on join paths |
| D170 | #170 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-170-onetoone-fk-uniqueness-1.md | OneToOne FK uniqueness |
| D171 | #171 | - | 1.0.9-rc2 | excluded-gap | gap | pdca-dotnet | N=1 | - | docs/specs/status/rc2-171-joininto-as-derived-1.md | revisit-on-demand placeholder; issue OPEN in milestone |
| D172 | #172 | - | 1.0.9-rc2 | excluded-gap | gap | pdca-dotnet | N=1 | - | docs/specs/status/rc2-172-child-collection-projection-1.md | awaiting-consumer; issue OPEN in milestone |
| D173 | #173 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-173-projection-folding-member-1.md | projection folding ignores Member |
| D174 | #174 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-174-identity-selector-cache-1.md | IdentitySelectorCache clear |
| D175 | #175 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-175-joininto-test-quality-1.md | JoinInto test-quality debt |
| D176 | #176 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-176-json-nested-1.md | JSON streaming Phase 2 |
| D177 | #177 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-177-native-json-1.md | JSON streaming Phase 3 DB-side fast-path |
| D178 | #178 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-178-json-enum-1.md | JSON streaming enum |
| D179 | #179 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-179-json-error-policy-1.md | JSON streaming error policy |
| D180 | #180 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-180-json-stream-provider-conversion-1.md | JSON provider column conversion |
| D184 | #184 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-184-loadwith-eagerloadmode-1.md | LoadWith EagerLoadMode |
| D185 | #185 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-185-bindentity-registration-1.md | BindEntity registration |
| D188 | #188 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-188-comparison-benchmarks-tier1-1.md | Comparison benchmarks tier 1 (needs D189) |
| D189 | #189 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-189-sqlite-datareader-1.md | SQLite ToDataReader verification |
| D190 | #190 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-190-join-whole-entity-1.md | Whole-entity JOIN projection |
| D191 | #191 | G1 | 1.0.9-rc2 | pending | ready | pdca-dotnet | N=1 | r=1 | docs/specs/status/rc2-191-provider-extensions-1.md | Provider-specific extensions move (**blocked on user review of committed spec**) |

## Decisions

- **Gap exclusion:** D171/D172 excluded from the actionable set (not-actionable-now, awaiting external input); issues stay OPEN in milestone `1.0.9-rc2`; no scope invented (planner, r=1).
- **Single group G1:** the footprint graph over the 24 admitted tasks is one connected component through shared core files; disjoint groups impossible. Single-group mode ⇒ current worktree/branch, no worktrees/branches, no merge.
- **Order** is footprint serialization plus `D189 → D188`.
- **Blockers:**
  - **D191 — external user-approval gate.** `docs/superpowers/specs/2026-10-05-provider-specific-extensions-design.md` declares `written spec — AWAITING USER REVIEW` and `implementation — NOT AUTHORIZED`. Autonomous DO cannot satisfy this; requires explicit user review/approval before DO of D191 (and, since G1 is sequential, before any task ordered after D191).
  - **D167 — P0 prerequisite:** in-repo `scripts/validate_inner_loop.py` absent (host copy exists at `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py`); resolve inside the lane or provide the manual evidence gate; do not weaken acceptance.

## Evidence contract r1/rv1

| req | row | check | owner | applicability |
|---|---|---|---|---|
| truthful admission | C-E01 | Validate admission snapshot for milestone `1.0.9-rc2`: 26 open issues, 24 admitted `ready`, D171/D172 excluded-gap and still OPEN in the milestone. | collection CHECK | at admission; re-check exclusions at completion |
| exclusive ownership | C-E02 | Expand the 24 planned footprints (incl. conditional paths), normalize C# identities via Roslyn, verify a single non-overlapping G1 ownership manifest and no concurrent writers. | scout (facts) + CHECK (verdict) | before first DO and on footprint change |
| verified completion | C-E03 | For each admitted task verify its frozen pdca-dotnet evidence contract, final CHECK and ACT on the final tree; missing/stale evidence is not completion; skipped providers ≠ pass. | CHECK | per task and before integration |
| safe integration | C-E04 | Single group ⇒ no group branch/merge; row is **N/A** unless a group branch exists. Integration = the authorized commits on `1.0.9-rc2` (patch/commit route recorded before execution). | coder (op) + CHECK (verdict) | only if a group branch exists |

CHECK re-gather budget: ≤2 targeted evidence requests per collection CHECK invocation, owned by CHECK.

## Общая верификация и восстановление

- Verification state: `unverified` (P done; DO not started).
- Defect id and history: none.
- Next allowed step: continue G1 DO at first pending task D160 (same saved PLAN, continue from DO).

## Done / Verified / Incomplete

- Done: D153 (#153, commit 8401ca51); D154 (#154, commit 5d72a79; issue #154 close blocked by GitHub API HTTP 500, retry pending); D157 (#157, commit 5978556f); D159 (#159, commit 078a7a2d; issue closed).
- Verified: no.
- Incomplete: none. Excluded-gap: D171, D172 (issues OPEN, milestone unchanged).

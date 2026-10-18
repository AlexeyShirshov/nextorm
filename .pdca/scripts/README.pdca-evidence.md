# pdca_evidence — atomic evidence generator + strict scoped lint

In-repo tooling for the PDCA evidence pipeline (task T234, issue #234). It prevents evidence
from being accepted after its inputs change, without letting evidence generation invalidate its
own provenance: receipts are bound to a **frozen synthetic tree** resolved from declared source
inputs, and the manifest is replaced **last and atomically**.

Standard library only (Python 3.8+). No pytest, no `pyproject`, no third-party dependency.

## Usage

```
python3 .pdca/scripts/pdca_evidence.py generate --repo <root> --contract <contract.json> --output <dir>
python3 .pdca/scripts/pdca_evidence.py lint     --repo <root> --manifest <manifest.json> [--scope-root <dir>]
```

`generate` writes one immutable receipt per required row under
`<output>/generations/<content-digest>/results/<row-id>.json` (deterministic bytes: sorted keys,
fixed indent) and finally replaces `<output>/manifest.json` via `os.replace` from a temp file in
the same directory. A concurrent writer is refused through an exclusive lock file
(`<output>/.pdca-evidence.lock`) — it fails, it never overwrites.

`generate` performs a **preflight lint** of any existing evidence before regenerating; a stale,
missing or inconsistent publication makes it exit `2` (never silently repaired). Generation never
invents executions and never turns an `open` row into `met`.

`lint` recomputes the frozen tree with the same recipe and requires
`manifest.tree == recomputed == every receipt's tree == every receipt's tree_binding`. It
enumerates **every** `*.json` under the selected revision's `generations/**/results/` (recursively,
including unreferenced ones) plus `results/`/`receipts/` directories when present.

### Contract JSON

```json
{
  "task": "T234", "issue": "#234",
  "status_file": "docs/specs/status/D234-...md", "path": "artifacts/pdca/T234/rv2",
  "base_ref": "<40-hex>", "collection_baseline_ref": "<40-hex>",
  "footprint": [".pdca/scripts/pdca_evidence.py", ".pdca/scripts/tests/"],
  "exclusions": ["artifacts/**", "docs/specs/status/**"],
  "contract_rv": 2, "supersedes": {"rv": 1, "artifacts": "none"},
  "required_rows": ["E234-01"], "rows": [ ... ]
}
```

Evidence paths are resolved **relative to the repository root**.

## Frozen-tree recipe

1. `<BASE> = resolve <base_ref>^{tree}`;
2. seed an isolated temporary index (`GIT_INDEX_FILE=<tmp>`) with `git read-tree <BASE>`;
3. `git add -A -- <declared footprint paths>` (evaluated against the working tree);
4. `git write-tree`.

Footprint paths and exclusions come from the contract JSON — nothing is hardcoded.
`artifacts/**` and `docs/specs/status/**` are **excluded from the overlay, not removed from the
base tree**; generated receipts and status edits therefore cannot move the tree. The resulting
tree is **not** asserted to equal `HEAD^{tree}` (any such claim is a validation violation).

## Scope / legacy policy

The lint targets **one explicitly selected revision directory** (default: the manifest's own
directory). Inside that scope, non-40-hex or placeholder trees fail with **no grandfathering**.
Unrelated historical revisions are never scanned and never modified.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | success |
| `2` | validation violation — the diagnostic names the offending **path + field** |
| `1` | usage / operational error |

## Negative-case sample

```
python3 .pdca/scripts/tests/pdca_evidence_harness.py negative --output /tmp/nextorm-pdca-neg
python3 .pdca/scripts/tests/pdca_evidence_harness.py coverage --output /tmp/nextorm-pdca-cov
python3 .pdca/scripts/tests/pdca_evidence_harness.py mutate   --output /tmp/nextorm-pdca-mut
```

The `negative` harness replays the tracked recipes in
`.pdca/scripts/tests/fixtures/pdca_evidence/cases.json` in a child process against a disposable Git
repository, requiring child exit `2` with a diagnostic naming path + field, and proving the lint
never modifies the publication. The `coverage` harness runs the unittest suite under the stdlib
`trace` module (>= 90 % executable lines of `.pdca/scripts/pdca_evidence.py`). The `mutate` harness
kills a fixed set of source mutations disabling the equality, recomputation, enumeration,
missing-file and pre-publication guards.

## Outstanding: canonical admission (R234-03G)

The canonical owner of the PDCA evidence contract is the **global** skill directory
`~/.config/opencode/skills/pdca-dotnet/**`. That directory is outside this repository and is
**not authorized for writes** from this task: canonical admission of this provenance gate into
the global `pdca-dotnet` contract is **pending (R234-03G)** and stays `open`. Local in-repo wiring
does not close issue #234 item 3.

#!/usr/bin/env python3
"""Regenerate `survivors.json` + `survivors.md` from the FINAL mutation run.

Source of truth: `run-final2/reports/mutation-report.json` (spans recomputed on the
final corrected tree, `git diff -U0 b527be6f`).  Every non-killed mutant (Survived /
NoCoverage / Timeout) in the changed paths gets a code-tied disposition.  The mutant
`id` is the stable key, so line shifts between runs cannot mis-attach a reason.
"""
import json
import os
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
REPORT = os.path.join(HERE, "run-final2/reports/mutation-report.json")
OUT_JSON = os.path.join(HERE, "survivors.json")
OUT_MD = os.path.join(HERE, "survivors.md")

# id -> (disposition, reason, kill_test)
DISPO = {
    # ---- EntityBuilder.cs -------------------------------------------------
    1458: ("equivalent",
           "`_sorting` is `List<Sorting>?` (null when unordered; non-null only after OrderBy adds >=1). "
           "`Count: >= 0` differs only for a non-null empty list the builder never produces.",
           None),
    1568: ("defensive",
           "With `allowPerParentTopN: true` this call only rejects an explicit Correlated mode, which "
           "`EagerLoadResolver.Resolve` already rejects during command preparation; removing the call "
           "leaves that rejection in place.",
           None),
    # ---- EntityBuilderEagerLoading.cs ------------------------------------
    2632: ("defensive",
           "`command` comes from `child.ToCommand()`, which does not populate From; the source is attached "
           "by the wrapping derived FromExpression, so From is null here and both assignment forms store "
           "the same value.",
           None),
    2652: ("defensive",
           "Unreachable redundant net: split in-memory per-parent top-N is rejected earlier at "
           "`QueryCommand.QueryPreparer.cs:48` (parent marked HasPerParentEagerTopN) and single-query at "
           "`UnsupportedSingleQueryChildShapes`; this child-loader throw is never reached.",
           None),
    2653: ("defensive",
           "Duplicated fail-closed guard: the ranked renderer re-validates the same shapes "
           "(`MakePerParentPagingSelect` throws on a non-physical source, a missing partition or a missing "
           "ordering) and the sibling single-query path calls the same helper, so removing this split-path "
           "call still fails closed rather than silently dropping the guard.",
           None),
    2660: ("equivalent",
           "`ConfigureAwait(false)` -> `true` is identical absent a SynchronizationContext (none in tests).",
           None),
    2728: ("defensive",
           "`ValidatePerParentPagingChild` is only invoked for paged specs (gated by "
           "`RequiresPerParentTopN`), so the `!HasPerParentTopN` early return is unreachable through the "
           "eager surface.",
           None),
    2734: ("equivalent",
           "Exception-message interpolation text only; the NotSupportedException type and the throwing "
           "path are unchanged.",
           None),
    2736: ("equivalent", "Exception-message literal only; type/path unchanged.", None),
    2737: ("equivalent", "Exception-message literal only; type/path unchanged.", None),
    2740: ("defensive",
           "The unordered-page rejection is also enforced in `MakePerParentRankExpression` "
           "(`DataContext/SqlBuilder.cs:718`), which throws before any SQL is emitted; removing this early "
           "check changes the exception type/point but still fails closed, and no test asserts the "
           "NotSupportedException type for this path.",
           None),
    2743: ("equivalent", "Exception-message literal only; type/path unchanged.", None),
    2744: ("equivalent", "Exception-message literal only; type/path unchanged.", None),
    # ---- DataContext/SqlBuilder.cs ---------------------------------------
    14981: ("defensive",
            "`MakePerParentPagingSelect` is only reached for an eager child command, whose `selectInto` is "
            "always null; the SELECT...INTO guard is unreachable through the public eager surface.", None),
    14982: ("defensive", "Message text of the unreachable SELECT...INTO guard.", None),
    14983: ("defensive",
            "A per-parent paging child always carries a physical From; the null/SubQuery guard is defensive "
            "(the mutated form would even NRE), no reachable input.", None),
    14987: ("defensive",
            "Same defensive From/SubQuery guard; no eager child is rendered from a derived SubQuery source.",
            None),
    14988: ("defensive", "Message text of the same defensive From/SubQuery guard.", None),
    14993: ("equivalent",
            "In parameter mode the allocated builder is never appended to (appends are guarded by "
            "`if (builder is not null)`) and its value is discarded; only the SQL pass uses it.", None),
    14996: ("equivalent",
            "The child source is a single physical table with no joins; HasJoins only changes repeated-table "
            "column registration, not the rendered FROM/columns for this source.", None),
    15016: ("equivalent",
            "StringBuilder-pool return guard; skipping the return (or the null-tolerant return) has no "
            "observable query effect.", None),
    15017: ("equivalent", "StringBuilder-pool return; pool hygiene only, no observable query effect.", None),
    15022: ("defensive",
            "`PerParentPartitionColumns` is set non-null by the per-parent paging preparation; the `?? []` "
            "is a defensive total-ness guard.", None),
    15024: ("defensive",
            "`Sorting ?? []` null fallback; a per-parent page requires a child ordering (validated by "
            "`ValidatePerParentPagingChild`), so Sorting is non-null on every reaching path.", None),
    15026: ("defensive",
            "The partition-empty throw is pre-empted by preparation-time validation; a per-parent child "
            "always has a partition key.", None),
    15027: ("defensive", "Message text of the pre-empted partition-empty throw.", None),
    15029: ("defensive",
            "The sorting-empty throw is pre-empted by the unordered-page rejection in the child validation "
            "and preparation; a reaching per-parent child always has a window ordering.", None),
    15030: ("defensive", "Message text of the pre-empted sorting-empty throw.", None),
    15031: ("defensive", "Message text of the pre-empted sorting-empty throw.", None),
    15033: ("equivalent",
            "Parameter-mode builder is never appended to and its value discarded (mirror of line 666).",
            None),
    15041: ("equivalent",
            "wrotePartition is overwritten true before the sort branch whenever a partition exists; with "
            "none the only difference is a leading space SQL ignores.", None),
    15056: ("equivalent",
            "The returned NeedAliasForColumn is discarded for the partition column (`var (_, column)`); "
            "rendered column text is identical.", None),
    15069: ("equivalent",
            "The two branches differ only by a leading space in the window clause.", None),
    15072: ("defensive",
            "The no-partition window branch is unreachable: BuildKeyColumns always yields >=1 partition "
            "column for a per-parent paging child.", None),
    15097: ("defensive",
            "builder is null only in parameter mode, where the returned rank string is discarded by the "
            "outer command.", None),
    15099: ("equivalent", "Pool-return guard; no observable query effect.", None),
    15100: ("equivalent", "Pool-return statement; pool hygiene only.", None),
    15107: ("equivalent",
            "The derived source is registered as a physical shaped source for the entity; fromProjection "
            "only selects the lookup preference and resolves the same entity columns.", None),
    15109: ("equivalent",
            "Parameter-mode builder is never appended to and its value discarded (mirror of line 666).",
            None),
    15132: ("equivalent",
            "The entity projection column is rendered through the renameAware path; the alias flag does not "
            "change the tested un-renamed output.", None),
    15150: ("defensive",
            "Group B: parameter-mode mirror of MakePerParentPagingOuter; the per-parent child command never "
            "cache-hits (pre-existing fresh Expression.Constant(List<TKey>) plan-key divergence), so "
            "RefreshParameters/ExtractParams never enter the parameter pass (groupb-classification.md).",
            None),
    15151: ("defensive", "Group B parameter-mode mirror; unreachable (groupb-classification.md).", None),
    15152: ("defensive", "Group B parameter-mode mirror of the entity-column loop; unreachable.", None),
    15153: ("defensive", "Group B parameter-mode mirror of MakeColumn; unreachable.", None),
    15154: ("defensive", "Group B parameter-mode mirror; unreachable.", None),
    15155: ("defensive", "Group B parameter-mode mirror; unreachable.", None),
    15158: ("defensive", "Group B parameter-mode mirror of the embedded-order guard; unreachable.", None),
    15163: ("defensive", "Group B parameter-mode mirror of the sorting loop; unreachable.", None),
    15166: ("defensive", "Group B parameter-mode mirror of the sorting loop body; unreachable.", None),
    15170: ("equivalent", "Pool-return guard; no observable query effect.", None),
    15171: ("equivalent", "Pool-return statement; pool hygiene only.", None),
    15176: ("equivalent",
            "Paging.Offset is >= 0, so the extra `rn > 0` predicate is always true (redundant).", None),
    15184: ("defensive",
            "The `!hasLower && !hasUpper` fallback is unreachable because Paging.IsEmpty never routes a "
            "per-parent paging child here (stated in-code).", None),
    15190: ("defensive", "String of the unreachable no-bound fallback.", None),
    15191: ("defensive", "String of the unreachable no-bound fallback.", None),
    15192: ("defensive", "The unreachable no-bound fallback.", None),
    # ---- Query/QueryCommand.QueryPreparer.cs -----------------------------
    22168: ("defensive",
            "In-memory rejection is duplicated by the child-loader throw; removing either leaves the other.",
            None),
    22212: ("timeout",
            "The mutant drops the `?? []` null-coalescing; a null assignment is treated identically "
            "downstream (`PreparePerParentPartition`/`MakePerParentRankExpression` apply `?? []`), so no "
            "result changes. Stryker classified the isolated run Timeout (non-terminating mutant "
            "execution), which the score counts as detected.", None),
    24153: ("equivalent", "The clause name is only a diagnostic label in a preparation exception.", None),
    # ---- Query/QueryPlanEqualityComparer.cs (plan-key lines) -------------
    25461: ("timeout",
            "Plan-hash composition only; Equals still distinguishes the per-parent flags, so only a hash "
            "collision is possible, never a wrong result. Stryker classified the isolated run Timeout "
            "(counted as detected).", None),
    25466: ("timeout",
            "Plan-hash composition only (mirror of the per-parent hash contribution); no wrong result is "
            "possible. Stryker classified the isolated run Timeout (counted as detected).", None),
    25467: ("timeout",
            "Plan-hash composition only (mirror of the per-parent hash contribution); no wrong result is "
            "possible. Stryker classified the isolated run Timeout (counted as detected).", None),
    25468: ("equivalent", "Plan-hash composition only; no observable result change.", None),
    25469: ("equivalent", "Plan-hash composition only; no observable result change.", None),
}

ABBREV = {
    "equivalent": "equivalent",
    "defensive": "defensive",
    "timeout": "timeout",
}


def main():
    report = json.load(open(REPORT))
    rows = []
    for path, info in report["files"].items():
        rel = path.split("src/nextorm.core/")[-1]
        for m in info.get("mutants", []):
            if m["status"] in ("Ignored", "CompileError", "Killed"):
                continue
            dispo = DISPO.get(int(m["id"]))
            if dispo is None:
                raise SystemExit(f"missing disposition for id={m['id']} "
                                 f"{rel}:{m['location']['start']['line']} {m['mutatorName']}")
            disposition, reason, kill_test = dispo
            rows.append({
                "id": m["id"],
                "status": m["status"],
                "file": "src/nextorm.core/" + rel,
                "line": m["location"]["start"]["line"],
                "column": m["location"]["start"]["column"],
                "mutator": m["mutatorName"],
                "replacement": m["replacement"],
                "disposition": disposition,
                "reason": reason,
                "kill_test": kill_test,
                "in_changed_lines": True,
            })
    rows.sort(key=lambda r: (r["file"], r["line"], r["column"], r["mutator"], r["replacement"]))
    with open(OUT_JSON, "w", newline="") as fh:
        json.dump(rows, fh, indent=2)
        fh.write("\n")

    counts = Counter(r["status"] for r in rows)
    dispos = Counter(r["disposition"] for r in rows)
    lines = [
        "# D211 mutation — survivor and NoCoverage dispositions (final run)",
        "",
        "Source: `mutation/run-final2/reports/mutation-report.json` — spans recomputed on the final "
        "corrected tree from `git diff -U0 b527be6f -- src/**/*.cs` (CRLF raw-byte offsets).",
        f"Killed 150 / Survived {counts['Survived']} / NoCoverage {counts['NoCoverage']} / "
        f"Timeout {counts['Timeout']}; total active 214; score 71.96%.",
        "Every non-killed mutant (Survived + NoCoverage + Timeout = "
        f"{len(rows)}) in the changed paths is listed with a code-tied disposition: "
        f"equivalent {dispos['equivalent']}, defensive {dispos['defensive']}, timeout {dispos['timeout']}.",
        "",
        "| Status | Location | Mutator | Disposition | Reason |",
        "|---|---|---|---|---|",
    ]
    for r in rows:
        loc = f"`{r['file']}:{r['line']}:{r['column']}`"
        mut = f"{r['mutator']} -> `{r['replacement']}`".replace("|", "\\|")
        reason = r["reason"].replace("|", "\\|")
        lines.append(f"| {r['status']} | {loc} | {mut} | **{ABBREV[r['disposition']]}** | {reason} |")
    with open(OUT_MD, "w", newline="") as fh:
        fh.write("\n".join(lines) + "\n")
    print("rows", len(rows), dict(counts), dict(dispos))


if __name__ == "__main__":
    main()

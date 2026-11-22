# Milestone 1.0.11 PDCA transport defects

For PDCA dispatchers and maintainers: this record explains two verified handoff failures at collection intake and how to prevent their recurrence within existing permissions. It does not revise the archived verdict, product criteria, or schedule. The required G1 stop was compliant, not a defect. No product or ClickHouse finding is recorded.

## M1011-TRANSPORT-01

- **Symptom:** `report_ref.py opencode` returned exit code 1 (`Session not found`) for the original `pdca-scout` session. The evidence root had neither the original report nor a `{path, sha256}` reference.
- **Supported cause:** The report transport could not resolve that session. The exact underlying cause is unknown; the error does not establish why the session was unavailable.
- **Dispatch effect:** Intake PLAN stayed at revision 1, attempt 0, with its fact request open. A new authorized scout dispatch was needed. Pinning and verifying the new completed session succeeded; the read-only scout did not create `observation-pack.md`. No DO, CHECK, or ACT was opened by the recovery.
- **Recommended prevention:** Pin and verify the exact completed scout turn at the first authorized status write. If the source still cannot be retrieved, record the transport blocker and ask the driver to authorize recollection. Do not reconstruct the original or rely on an output file the read-only scout cannot write.
- **Evidence:** [Intake log, lines 87–89](../../../.pdca/status/open-milestone-tasks-1.md) and the [parent retrospective, section 2](../../../.pdca/collection-milestone-1-0-11/rv1/report-312d6d246747cad9b87f09ad9ca789507d1d30a2c150208b5f87eb7c0186a305.md).

## M1011-TRANSPORT-02

- **Symptom:** The replacement scout report was pinned, but `pdca-planner` could not read it. The accompanying summary omitted mandatory acceptance conditions.
- **Supported cause:** The report was outside the planner's allowed `plan_reference_path`. The handoff relied on an inaccessible reference and did not supply all required facts through the permitted brief.
- **Dispatch effect:** Intake PLAN stayed at revision 1, attempt 0 and needed another dispatch with the missing facts inline. The existing verified report was sufficient for that packaging; no repeat scouting or later phase was needed for the handoff repair.
- **Recommended prevention:** Before dispatch, use the permitted planner brief to supply the exact expected results, mandatory acceptance conditions, applicable performance comparison conditions, and material unknowns from the verified source. Keep `{path, sha256}` for provenance rather than relying on it as the planner's input. Do not widen the reader's permissions.
- **Evidence:** [Intake log, lines 89–90](../../../.pdca/status/open-milestone-tasks-1.md) and the [parent retrospective, sections 2–3](../../../.pdca/collection-milestone-1-0-11/rv1/report-312d6d246747cad9b87f09ad9ca789507d1d30a2c150208b5f87eb7c0186a305.md).

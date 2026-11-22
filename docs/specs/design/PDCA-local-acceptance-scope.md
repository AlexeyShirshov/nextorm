# PDCA local acceptance scope

## `process/pdca-overbroad-local-gates`

- **Symptom:** A narrow code fix can inherit full design recipes, sealing counts, coverage deltas,
  mutation checks, or complete-suite acceptance unrelated to its declared behavior.
- **Root cause:** Local regression checks and complete integration/release gates were not separated by
  scope; optional analysis was written as a universal gate.
- **Effect:** Work and rechecks can be spent on unrelated evidence before the target behavior and
  affected provider paths are independently accepted.
- **Evidence:** Tracking context `#403` and `#404`; project procedure in
  `.opencode/skills/nextorm-pdca/SKILL.md:92-116,162-211`.
- **Prevention:** Keep the local loop on the target regression and affected tests/providers. Apply
  full-suite, coverage, mutation, and design evidence only when the pre-existing declared scope
  requires it. The project skill is the procedure source of truth.

## `process/integration-receipt`

- **Symptom:** Run identity and resource ownership were spread across logs; the workflow also implied
  that manually provisioned `wslc` endpoints could use the capped runner.
- **Root cause:** The runner had no structured receipt and is Podman-bound: it sets `DOCKER_HOST`,
  checks the Podman socket, and acquires its non-reentrant lock only after launch.
- **Effect:** A receipt could not establish the executed binary, and manual provisioning could race or
  conflict with the runner lock.
- **Evidence:** `tools/integration/run-integration-capped.py:218,343,548-673,806-809`;
  `.opencode/skills/running-integration-tests/SKILL.md:20-25`; tracking context `#403` and `#404`.
- **Prevention:** Append a JSONL receipt, fingerprint the test assembly when uniquely identifiable,
  omit connection values, and never claim unverified cleanup. Keep the `wslc` route blocked until a
  lease-aware runner can safely own its lifecycle.

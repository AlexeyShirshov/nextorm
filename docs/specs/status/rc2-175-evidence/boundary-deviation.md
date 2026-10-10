# D175 r1 — boundary-sweep deviation (validator `report` exit 2)

## Observation

`python3 .../validate_inner_loop.py report /tmp/nextorm-D175/r1/evidence.json` prints:

```
FAIL: more than one comprehensive boundary test sweep: 2
REPORT_EXIT=2
```

(`validator-brief.txt` = EXIT=0; the `brief` gate passes. Only `report` exits 2.)

## Disposition: accepted tool limitation — NOT a missing sweep

The helper caps "comprehensive boundary test sweep" at **one**; D175's plan-mandated boundary is
**two** selected project sweeps (no single solution-wide test command applies to a test-only change
that touches two test projects but no product code):

| boundary sweep | command | result | exit |
| --- | --- | --- | --- |
| CORE-FULL | `dotnet test tests/nextorm.core.tests -c Debug` | 1767 total / 0 failed / 0 skipped | 0 |
| SQLITE-FULL | `dotnet test tests/nextorm.sqlite.tests -c Debug` | 1161 total / 0 failed / 1 skipped (env-gated `NEXTORM_LOB_SQLITE_PROBE`) | 0 |

Both sweeps are present and green; the sole remaining boundary execution is the solution build
(`dotnet build nextorm.slnx -c Debug`, exit 0). The FAIL is the helper's single-sweep cap counting
the second full project sweep, not an absent sweep and not a red run.

## Precedent

- **rc1-126** (`docs/specs/status/rc1-126-tuple-ctor-1.md:229-230`, `:305-306`): `report` exit 2
  with `more than one comprehensive boundary test sweep: 7` was recorded as an accepted deviation
  against a plan mandating seven full per-provider boundary sweeps; the extra sweeps were the
  plan's required evidence, and the cycle was accepted on that record.
- **rc1-141** (`docs/specs/status/rc1-141-validate-1.md:36`, `:129-131`): established the
  single-comprehensive-sweep contract the helper enforces, and shows the complementary case where
  extra unfiltered runs are the defect. Here the extra sweep is *mandated by the plan*, matching the
  rc1-126 pattern, not the rc1-141 defect.

Conclusion: exit 2 is the documented single-boundary-cap tool limitation, explicitly dispositioned
and accepted; the required boundary evidence (CORE-FULL + SQLITE-FULL + BUILD, all exit 0) is
complete.

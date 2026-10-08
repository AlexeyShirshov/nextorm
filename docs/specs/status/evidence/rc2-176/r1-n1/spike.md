# D176.1 reconnaissance spike — baseline, binding, and presence observations

Branch `1.0.9-rc2`, worktree `/home/alex/sources/nextorm` (base `18659e41`). Throwaway probe
only; no product behavior changed. Raw probe outputs are in `probe/`.

## 1. Baseline (C07, pre-change)

Command: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
Exit `0`, wall 74 s (BDN `Global total time: 00:00:49 (49.84 sec)`), exactly **7** executed
cases. Log: `baseline-acceptance.txt`.

| Case | Mean | Allocated | Gen0 |
|------|------|-----------|------|
| `Nextorm_Count` | 4.404 ms | 374.22 KB | 39.06 |
| `Nextorm_GroupByCount` | 107.3 ms | 50.14 MB | 6250.00 |
| `Nextorm_Cached` | 2.968 ms | 609.43 KB | 74.22 |
| `Prepared_ToList` | 1,252.2 us | 76.14 KB | 7.81 |
| `Cached_ToList` | 2,747.6 us | 583.2 KB | 70.31 |
| `Cached_PlanOnly_Param` | 869.4 us | 507.05 KB | 61.52 |
| `Nextorm_Cached_ToListAsync` | 2.901 ms | 607.69 KB | 74.22 |

Cached-vs-prepared ratio (same run): `Cached_ToList / Prepared_ToList` time **2.20**,
allocation **7.66**. Baseline doc table (`docs/specs/performance/acceptance-benchmarks.md`)
recorded 1.87 / 7.42; this run is within its stated noise band. No regression classification.

## 2. Build / test baseline (C01/C02, pre-change)

| Invocation | Exit | Result |
|---|---|---|
| `dotnet build nextorm.slnx -c Debug` | 0 | 0 warnings / 0 errors (`build-debug.txt`) |
| `dotnet test tests/nextorm.core.tests -c Debug --no-build` | 0 | total 1789 / succeeded 1789 / failed 0 / skipped 0 (`core-tests.txt`) |

## 3. Probe A — nested anonymous + named/member-init projection

Probe file (throwaway, now removed): `tests/nextorm.sqlite.tests/D176ProbeTests.cs`.
Method `ProbeA_NestedAnonymous` / `ProbeA_NestedMemberInit`; run filtered with
`dotnet test tests/nextorm.sqlite.tests -c Debug --no-build --filter FullyQualifiedName~D176Probe`
(exit 0, 4 selected / 4 passed). Raw: `probe/probe-a-nested-anonymous.txt`,
`probe/probe-a-nested-memberinit.txt`.

**Observation: nested construction is already opaque at preparation time.**
`Select(p => new { p.Id, Child = new { p.Name } })` prepares successfully and yields exactly
**two** `SelectExpression`s — the nested object is a *single* column of the anonymous CLR
type, not a group of scalar leaves:

```
selectList.Count = 2
  [0] ordinal=0 name=Id    type=Int32                     nullable=False projectionItem=null
  [1] ordinal=1 name=Child type=<>f__AnonymousType17`1    nullable=True  projectionItem=null
```

`WriteJson` therefore throws in `JsonShapePlan.Classify`:
`NotSupportedException: Column 'Child' has type <>f__AnonymousType17\`1[System.String] which
is not supported by JSON streaming in phase 1`.

The named/member-init form (`new ProbeOuter { Id = p.Id, Child = new ProbeInner { Name =
p.Name } }`) behaves identically (`Child` is one `ProbeInner` column, `WriteJson` rejects it).
The second terminal, `EntityBuilderExtensions.WriteJson` on the whole-entity query, still
works: `[{"Id":1,"Name":"p1","LeafA":11},{"Id":2,"Name":null,"LeafA":null}]`.

**Conclusion for Probe A:** existing prepared metadata does **not** supply per-leaf bindings
for a nested projection; the plan's premise (capture the recursive shape *before*
preparation makes nested construction opaque) is confirmed, not contradicted. The
already-selected fallback (JSON-only preparer entry that captures the descriptor and the
lowered scalar selection together) is required work in D176.2 — it is planned implementation,
not a new semantic alternative.

Anchors: `Query/QueryCommand.QueryPreparer.cs:539` (NewExpression branch), `:552-594`
(one `SelectExpression` per ctor argument; no recursion into nested constructions), `:572`
(`TryExpandEntityItem` call), `:655-698` (MemberInit branch, also non-recursive);
`Query/Json/JsonShapePlan.cs:136,172-198` (`Classify` whitelist rejection).

Side observation (Phase-1 naming): for a constructor projection the JSON member names come
from the constructor *parameter* names (`probe/probe-b-all-null-constructed.txt` emits
`{"id","name","leaf"}`) while the named/member-init path uses property names. This is
pre-existing Phase-1 behavior; R176.01 requires checking exact property names against the
actual result type. Flagged as an observation, not a D176.1 defect.

## 4. Probe B — matched/unmatched join slot and all-null constructed object

Raw: `probe/probe-b-join-slot.txt`, `probe/probe-b-all-null-constructed.txt`.

**Join slot ordinal map.** `ctx.From<ProbeParent>().LeftJoin(...).Select(p => new
ProbeBoth(p.Item1, p.Item2))` prepares with six columns carrying two `ProjectionEntityItem`
groups (`SelectExpression.cs:355-375`):

```
[0] ordinal=0 name=Id       int        projectionItem=Slot=0,Entity=ProbeParent,Member=null
[1] ordinal=1 name=Name     string?    projectionItem=Slot=0,Entity=ProbeParent,Member=null
[2] ordinal=2 name=LeafA    int?       projectionItem=Slot=0,Entity=ProbeParent,Member=null
[3] ordinal=3 name=Id       int        projectionItem=Slot=1,Entity=ProbeChild, Member=null
[4] ordinal=4 name=ParentId int        projectionItem=Slot=1,Entity=ProbeChild, Member=null
[5] ordinal=5 name=Name     string?    projectionItem=Slot=1,Entity=ProbeChild, Member=null
```

SQL renders validly:
`select t1.id, t1.name, t1.leaf_a as 'LeafA', t2.id, t2.parent_id as 'ParentId', t2.name
from probe_parent as 't1' left join probe_child as 't2' on t1.id = t2.parent_id`.

**Raw `DBNull` inputs observed** by executing the prepared command's `DbCommand` directly.
For the unmatched row (parent `id=2`, no child) ordinals `3,4,5` are all `IsDBNull=True`,
while the parent ordinals `0,1,2` are present:

```
row1: [0]id=False:Int64 [1]name=True:String [2]LeafA=True:Int64
      [3]id=True:Int64 [4]ParentId=True:Int64 [5]name=True:String
```

**Current JSON parity is not achievable.** `WriteJson` on the same shape throws
`NotSupportedException: Duplicate JSON property name 'Id' after naming-policy conversion`
from the global guard (`JsonShapePlan.cs:122,161-163`) because `Slot=0.Id` and `Slot=1.Id`
collide in one flat name set. Even with unique names, the JSON plan never consumes the
`ProjectionEntityItem` slot grouping, so an unmatched entity slot would be written as an
object with null properties instead of the absent-entity `null` the ordinary materializer
produces. Oracle contrast — ordinary rows: 2, `Right is null` = 1; STJ:
`[{"Left":{"Id":1,...},"Right":{"Id":10,...}},{"Left":{"Id":2,"Name":null,"LeafA":null},"Right":null}]`.

**Where the presence decision lives.** It is *not* in `JoinExpression.cs:81` (that line is
`public JoinType JoinType { get; }`). It is the all-columns-null predicate built by
`RowMaterializerBuilder.BuildEntityItem`: for every column of one `ProjectionEntityItem`
group it ORs `NOT isNull(column)` and returns
`Condition(anyNotNull, entity, null)` (`RowMaterializerBuilder.cs:287-301`). The `isNull`
input is the provider's raw `IsDBNull(column.Index)` lambda passed into
`RowMaterializerBuilder.Build` (`RowMapperFactory.cs:359`, `Build` call at `:348-359`);
`column.Index` is the original mapped ordinal — i.e. the predicate is evaluated over the
`DBNull` inputs *before* any `DefaultOnNull` substitution. The JSON path
(`DataContext.PrepareJsonStream:368-392`, `JsonShapePlan.Build` at `:390`) never routes
through this predicate. This is the presence rule to reuse/extract in D176.2, per plan §5.

**All-null constructed object.** A top-level explicit construction whose leaves are all null
already stays a JSON object: `Select(p => new ProbeAllNull(p.Id, p.Name, p.LeafA))` over the
row with `name IS NULL AND leaf_a IS NULL` writes `[{"id":2,"name":null,"leaf":null}]`
(ordinary materializer returns a non-null instance with null properties; STJ oracle
`[{"Id":2,"Name":null,"Leaf":null}]`). No dedicated presence metadata is needed for a
top-level `new`; the requirement bites only when a *nested* constructed object or an
outer-join entity slot must be distinguished from an all-null present object.

## 5. Fallback trigger recorded (for D176.2)

Internal plumbing required by the selected approach is **missing** and is recorded as the
planned fallback trigger, not implemented here:

1. A JSON-only preparer entry that returns the recursive descriptor and the lowered scalar
   selection together — existing `cmd.SelectList` collapses nested construction to one
   opaque column (Probe A) and keeps `ProjectionItem.Slot` grouping only for entity items
   (Probe B).
2. Explicit ordinal binding from that descriptor to the prepared reader ordinals, plus the
   scoped per-object name tables replacing the global `HashSet` at `JsonShapePlan.cs:122,161`.
3. Reuse/extract the presence predicate at `RowMaterializerBuilder.cs:287-301` +
   `RowMapperFactory.cs:359` for entity presence, and carry an explicit construction-presence
   flag; do not write an all-leaves-null heuristic writer.

No heuristic writer was written; no product file was modified.

## 6. Temporary files

`tests/nextorm.sqlite.tests/D176ProbeTests.cs` was created for the probe and **deleted** after
the run (`git status --short src tests` is clean). Probe outputs remain under
`probe/*.txt`; they are raw evidence, not product code.

## 7. Verdict

- Probe A: **pass as reconnaissance** — nested binding is absent and the premise is confirmed;
  fallback (JSON-only preparer binding entry) confirmed as D176.2 work.
- Probe B: **pass as reconnaissance** — slot/ordinal map and raw `DBNull` inputs captured;
  presence decision located; JSON parity currently unreachable due to the global duplicate guard
  and the missing slot-presence metadata.
- No STOP / DO→PLAN candidate: no premise contradicted; only a minor plan anchor correction
  (`JoinExpression.cs:81` → `RowMapperFactory.cs:359` + `RowMaterializerBuilder.cs:287-301`).

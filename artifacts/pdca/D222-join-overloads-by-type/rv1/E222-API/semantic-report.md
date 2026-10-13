# E222-API — T222 rv=1 semantic inventory (source-less join overloads)

Producer: `roslynq nextorm.slnx members <type>` (Roslyn), raw output in `E222-API/raw/`.
Authoritative `file:line` are recorded after implementation (see `members.json`).

## New public members (28)

| Owner | New members | Result arity | Count |
|---|---|---:|---:|
| `EntityBuilder<TEntity>` | `Join/LeftJoin/RightJoin/FullJoin<TJoinEntity>(Expression<Func<TEntity, TJoinEntity, bool>>, Action<JoinOptions>?)` | 2 | 4 |
| `JoinedEntityBuilder<T1,T2>` | same four, `new`, predicate over `Projection<T1,T2>` | 3 | 4 |
| `JoinedEntityBuilder<T1..T3>` | same four, `new` | 4 | 4 |
| `JoinedEntityBuilder<T1..T4>` | same four, `new` | 5 | 4 |
| `JoinedEntityBuilder<T1..T5>` | same four, `new` | 6 | 4 |
| `JoinedEntityBuilder<T1..T6>` | same four, `new` | 7 | 4 |
| `JoinedEntityBuilder<T1..T7>` | same four, `new` | 8 | 4 |
| **total** | | | **28** |

Roslyn per-owner counts: {"NextORM.Core.EntityBuilder`1": 4, "NextORM.Core.JoinedEntityBuilder`2": 4, "NextORM.Core.JoinedEntityBuilder`3": 4, "NextORM.Core.JoinedEntityBuilder`4": 4, "NextORM.Core.JoinedEntityBuilder`5": 4, "NextORM.Core.JoinedEntityBuilder`6": 4, "NextORM.Core.JoinedEntityBuilder`7": 4}.

## Exclusions verified

- No source-less `CrossJoin`/`CrossApply`/`OuterApply` overloads (all 28 are the four conditional
  operators; the inventory regex matched only `Join|LeftJoin|RightJoin|FullJoin`).
- No source-less raw-table `EntityBuilder` (non-generic) overloads.
- No new members on the terminal `JoinedEntityBuilder<T1..T8>`.
- Existing source-explicit, `Cte<T>` and `QueryCommand<T>` overloads are unchanged (they still appear
  with their original signatures in the inventory).

## Resolution / invariants

- Each new overload calls the existing context-local `_dataProvider.From<TJoinEntity>()` and then the
  corresponding source-explicit overload; the join core (`JoinCore`) and `JoinSourceResolver.Resolve`
  (`Builders/Joins/JoinSourceResolver.cs`) are **not** modified.
- Early `ArgumentNullException.ThrowIfNull(joinCondition)` precedes source resolution.
- Generic parameters carry no new constraints (value-type join entities remain allowed).

# E222-DOCS — arity-8 claim registry (final, post corrective round n=3/3)

Mentions: 27. Classification: inaccurate=12 (all 12 rewritten), accurate=9, generator-scoped=6.

No remaining inaccurate 'compile-time cap / does not compile' claims (re-scan `capped at eight tables|at compile time|ninth join|does not compile|no further join|compile-time cap|ограничена восемью таблицами на этапе|девятое соединение не компилируется|потолок времени компиляции` returns only the corrected 'not a compile-time rejection' wording).

| location | context | classification | final status | note |
|---|---|---|---|---|
| `docs/guide/02-joins.md:25-31` | Overview item 1 (join arity) | inaccurate | rewritten | flat arity-chain scope bound; no new join case declared at 8; inherited generic-base joins compile |
| `docs/guide/02-joins.md:246-248` | Alias section: arity cap | inaccurate | rewritten | flat typed chain the alias surface extends stops at 8 slots; scope bound, not compile-time rejection |
| `docs/guide/02-joins.md:282-284` | Generator diagnostics NORMGEN004 (arity over eight) | generator-scoped | verified | applies to the generated alias surface only |
| `docs/guide/02-joins.md:414-421` | Scope (source-less form) | accurate | verified | unchanged; correct arity-8 wording |
| `docs/guide/02-joins.md:674-676` | Chained joins and arity 2..8 | accurate | verified | describes the chain up to eight tables; no rejection claim |
| `docs/guide/02-joins.md:703-706` | Cte<T> continuation | inaccurate | rewritten | no declared join case at arity 8; Cte does not change the scope bound; inherited joins reachable |
| `docs/guide/02-joins.md:722-731` | As (intermediate projection) | inaccurate | rewritten | As resets the counter for readability, not an escape from a compile-time cap |
| `docs/guide/02-joins.md:835-836` | Provider resolution of JoinedEntityBuilder<T1..T8> | accurate | verified | query-build-time resolution; no cap claim |
| `docs/guide/08-cte.md:467-469` | CTE continuation projection | inaccurate | rewritten | flat typed projection stops at 8; inherited joins reachable |
| `docs/advanced/limitations.md:22` | Named join aliases (limitations) | inaccurate | rewritten | scope bound, not compile-time rejection; As for readability |
| `docs/advanced/api-reference.md:70` | JoinedEntityBuilder<T1..T8> | accurate | verified | declared builder arity 2..8 |
| `docs/advanced/api-reference.md:71` | Generated alias surface | generator-scoped | verified | generated alias arity <= 8 |
| `docs/advanced/api-reference.md:76` | IProjection/IExtendableProjection | accurate | verified | arity 8 is not extendable (declared-type fact) |
| `docs/guide/30-projections.md:358` | Accumulator types | accurate | verified | lists Projection<T1>..T1..T8> |
| `docs/advanced/eager-loading.md:207` | JoinInto projection-arity overflow | generator-scoped | verified | different mechanism (eager-load flattening), fail-closed |
| `docs/advanced/relationships.md:194` | many-to-many JoinInto counts double | generator-scoped | verified | JoinInto projection-slot accounting |
| `docs/advanced/relationships.md:291` | more than seven child collections | generator-scoped | verified | JoinInto eager-load arity cap |
| `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs:1726-1733` | Terminal-class XML caveat | accurate | verified | scope bound, not compile-time rejection; inherited members compile |
| `docs/ru/guide/02-joins.md:26-33` | Overview item 1 (арность) | inaccurate | rewritten | граница области плоской цепочки; не отказ на компиляции |
| `docs/ru/guide/02-joins.md:251-253` | Alias: арность | inaccurate | rewritten | граница области, а не отказ на компиляции |
| `docs/ru/guide/02-joins.md:288` | Generator diagnostics (арность больше восьми) | generator-scoped | verified | generated alias surface |
| `docs/ru/guide/02-joins.md:424-433` | Scope (без источника) | accurate | verified | unchanged |
| `docs/ru/guide/02-joins.md:686-688` | Цепочки 2..8 | accurate | verified | no rejection claim |
| `docs/ru/guide/02-joins.md:716-719` | Cte<T> continuation | inaccurate | rewritten | на арности 8 не объявлено случая; граница области |
| `docs/ru/guide/02-joins.md:738-743` | As | inaccurate | rewritten | сброс читаемости, не выход из потолка компиляции |
| `docs/ru/guide/08-cte.md:473-476` | CTE continuation | inaccurate | rewritten | плоская проекция останавливается на 8; унаследованные доступны |
| `docs/ru/advanced/limitations.md:22` | Named join aliases (RU) | inaccurate | rewritten | граница области, а не отказ на компиляции |

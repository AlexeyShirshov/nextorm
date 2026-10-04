# Task #183 — Iteration 15: reduce fresh-fluent cached-path overhead in stages (cycle 1)

- collection: `1.0.9-b-5`, group-1
- branch: `collection/1.0.9-b-5/group-1`
- base: `1.0.9-b`
- milestone: `1.0.9-b`
- tracking: issue #183 — https://github.com/AlexeyShirshov/nextorm/issues/183
- design: `docs/specs/performance/iteration-15-cached-path-design.md`
- cycle: 1
- plan revision: r=1
- execution attempts: n/a (DO not started)
- phase: closed
- outcome: **incomplete** — awaiting written spec review; implementation not authorized
- date: 2026-10-04

## Goal

Снизить per-call стоимость fresh-fluent попаданий в implicit plan cache (в первую очередь two-table
Join), сохранив public API, identity плана, привязку параметров и prepared/cold-поведение.

## Acceptance criteria (design doc + collection brief)

- Stage A: корректная отчётность без уничтожения raw data; §2-коррекции отдельной версионированной
  ревизией; гомогенная декомпозиция Join/Where (construction → prepare → lookup/equality →
  captured-param refresh → execution); plan-only benchmarks без БД; valid CPU/allocation profiles
  реального benchmark-процесса; versioned baseline + comparison manifest; regression-бюджеты,
  замороженные ДО правок core (Gate A).
- Stage B: B1 (temp equality-scope allocations) / B2 (immutable guarded parameter-refresh recipe)
  по одному, каждый с отдельными correctness/perf evidence и откатом, только подтверждённый Stage A.
- Stage C: только решение/handoff; отдельный written design M12 #3 до реализации.
- Perf acceptance (collection): ровно 7 acceptance cases, без падений; wall time/Mean/Allocated и
  cached/prepared ratios vs `docs/specs/performance/acceptance-benchmarks.md`; build 0 Warning/0 Error;
  coverage ≥85 line / ≥75 branch.
- Инварианты: никакого `queryCommand.Cache=false` (sticky shared Any) — только локальный
  `storeInCache:false`; полное структурное равенство; prepared path и warm CTE 0B сохранены.

## PLAN

- `planner` (medium): gate 1 НЕ пройден. Классификация: недостаток доказательств → адресный `scout`;
  при неподтверждённой авторизации — `escalate` (триггер 5). Причина: спека
  `iteration-15-cached-path-design.md:5-6,246,250,254` и тело issue #183 требуют письменного ревью
  пользователя до кода («разрешения реализации нет»).
- Адресный `scout`: письменного ревью/апрува #183 нет ни в репозитории, ни в issue. Issue #183 —
  Open, milestone `1.0.9-b`, labels/comments отсутствуют; отдельного task-status файла нет.
- `scripts/validate_inner_loop.py` в worktree отсутствует (ABSENT).

## Escalation (trigger 5)

- Вопрос: каков авторизованный ограниченный deliverable и pass/fail #183 при конфликте
  collection-мандата (полный автономный цикл + perf-приёмка) с письменным гейтом спеки и при
  неподтверждённой доступности профайлера, который требует Stage A?
- Решение (strong tier):
  1. Collection-мандат **не** является авторизацией писать код. #183 = `incomplete`
     (awaiting written spec review); `src/**`, `benchmarks/**`, `tests/**`, `eng/**` не трогать.
  2. Stage A тоже подпадает под гейт; reduced Stage A с «profile unavailable» проваливает Gate A.
     Если ревью придёт — правильная поставка только Stage A отдельным циклом; B — следующим.
  3. Профайлер не подтверждён (`dotnet-trace` нет в `.config/dotnet-tools.json`); не ставить tool и
     не подменять профиль `MemoryDiagnoser`'ом; открытый вопрос к ревьюеру.
  4. #166 — deferred/pending, «trigger not fired»; группу не останавливать.
  5. Предикаты evidence — ниже.
- Routing: replan в `planner` не требуется; status-only close через `coder`; STOP для кода #183.
  Решение `escalate` роутится, не перевыбирается.

## Versioned evidence contract rv=1

| row | applies | predicate | status |
|---|---|---|---|
| G1-PERF | #183 | `git diff 1.0.9-b -- src benchmarks eng` пуст по #183; ни одна из 7 acceptance-метрик не заявлена улучшенной/ухудшенной | not-applicable (not run) |
| G1-CACHE | #183 | пустой diff по `QueryCache`/`ExpressionPlanEqualityComparer`/`QueryPlanner`/`QueryCommand.Plan`; `queryCommand.Cache`/`_dontCache` не затронуты | pass (no diff) |
| G1-CTE | #166 trigger | пустой diff по `src/nextorm.core/Builders/CteHoister.cs` (в т.ч. `Hoist`); #166 = deferred (trigger not fired) | pass (no diff) |

`G1-PERF` НЕ засчитывается зелёным: не измерялось (not-applicable, не pass).

## DO / CHECK / ACT

- DO, CHECK — не выполнялись для #183 (нет кода и измерений).
- ACT — закрытие задачи как `incomplete`; обновление collection-статуса. Issue #183 НЕ закрывается.

## #166

- deferred, trigger not fired: `CteHoister.Hoist` не редактировался. Остаётся pending в текущем
  milestone `1.0.9-b`.

## Notices / открытые вопросы

- `Notice:` tool `todowrite` недоступен в этой среде — session-mirror не ведётся; источник истины —
  этот файл.
- `Notice:` `scripts/validate_inner_loop.py` в worktree отсутствует — inner-loop evidence формат нужно
  определить заново при будущем DO.
- `Notice:` доступность профайлера не подтверждена; `dotnet-trace`/`dotnet-counters`/`dotnet-dump`/
  `dotnet-gcdump`/`perf` не найдены в `.config/dotnet-tools.json`, глобальные tools не проверены.
- Вопрос к ревьюеру: (a) одобрена ли письменная спека #183; (b) добавить `dotnet-trace` в манифест или
  принять BDN `MemoryDiagnoser` как замену (это меняет критерий Gate A).

## Done / Verified / Incomplete

- Done: —
- Verified: —
- Incomplete: #183 — awaiting written spec review
  (`iteration-15-cached-path-design.md:5-6,246,250,254`).

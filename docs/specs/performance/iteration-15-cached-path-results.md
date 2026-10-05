# Итерация 15 — результаты и передача (cached path, #183)

## Итог итерации 15 (cached path)

Stage A — DONE @752943d. B1 — incomplete/unaccepted/uncommitted. B2 — incomplete/unaccepted/uncommitted. #183 closes `DONE (with limitation)` под директиву владельца и решение эскалации (a): владелец явно разрешил не останавливать группу по метрике времени; неатрибутируемое время — приемлемый негативный исход, а не провал задачи. Ни B1, ни B2 не поставлены (not shipped).

## Stage A (измерения)

Крупнейшая non-execution аллокация — construction (586 KB/invocation), далее lookup/equality/parameter refresh (316 KB/invocation); refresh-needed 6528.104 B/op против no-refresh контроля 5184.188 B/op → диагностическая дельта 1343.916 B/op (точно на 3 прогонах, остаток 832.14 B/op; время indicative only).

Источник: `docs/specs/status/iteration-15-cached-path-183-2.md`, `docs/specs/performance/iteration-15-stage-a-report.md`.

## B1 — аллокации equality-скоупов

Реализация: green по build/тестам/coverage/integration/аллокациям; детерминированно −43,202 B/op на 4 cached-hit арм (3 раунда).

Не подтверждено: атрибутируемое ускорение целевой стадии.

Исход: incomplete/unaccepted/uncommitted (fallback (c)).

Патч: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/b1-incomplete.patch`, sha256 `0be74f92aa4250c78a566849dc1574d97c1c74aa154e18f56650501e34a7dc50`.

Открытое требование: повторить замер на тихом хосте / с process control.

## B2 — immutable guarded parameter-refresh recipe

Реализация готова (build 0/0; core 1482/0; sqlite 993 pass +1 env skip/0 fail; coverage line 87.1 / branch 78.7).

**Интеграция.** Изолированные прогоны по провайдерам на тех же U2-бинарниках: PostgreSQL 776/0, SQL Server 698/0, MySQL 656/0, ClickHouse 173/0 (все executed, 0 failed). Единый full-suite прогон упал: Total 3136, Failed 663, Skipped 191, Errors 0; доминирующая ошибка — 653 SQL Server-теста с `Microsoft.Data.SqlClient.SqlException` (pre-login handshake timeout, `Win32Exception: Unknown error 258`), плюс 1 MySQL-тест и 9 ClickHouse-тестов (HttpClient/socket-ошибки). Причина падения артефактом не подтверждена: строк OOM / 2 GiB / `137` в `integration.log` нет, контейнеры к моменту проверки удалены — приписывать падение Podman-OOM нельзя. Падение представлено инфраструктурными ошибками соединения, а изолированные прогоны на тех же бинарниках зелёные, что не является свидетельством регрессии U2. Артефакт: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/integration.log`.

Аллокации: детерминированно −112,802 B/op на `Where_CachedHit_PlanOnly` и `Where_CachedHit_ToList` (3 раунда); `Join_CachedHit_*` ~0.

Не подтверждено: атрибутируемое ускорение (ни одна дельта не превышает combined 99.9% CI; надёжной регрессии нет).

Исход по гейту дизайна (B/op AND speedup): CHECK FAIL → U2 откатан, не закоммичен.

Патч: `benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/u2-incomplete.patch`, sha256 `dd876ae9ed5b68a4fe3804478194db64741fb39bde3bebae24edb21899d18b7f`.

Открытое требование: то же.

## Остаточная декомпозиция и решение по M12 #3

Подтверждённые Stage A доли: construction (586 KB/invocation) > lookup/equality+refresh (316 KB/invocation); `PrepareCommand` не является крупнейшей остаточной non-DB стоимостью выше неопределённости.

Поэтому отдельный письменный дизайн `m12-3-prepare-command-design.md` сейчас НЕ создаётся; зафиксировано измеренное основание.

Триггер пересмотра: если construction будет адресован и prepare станет доминирующей статьёй, либо если неопределённость измерения снижена на тихом хосте.

## Открытые пункты (в текущем milestone)

1. B1/B2 time-attribution на тихом хосте / с process control.
2. B2 test-adequacy gap: изоляционный тест не доказывает, что fast-path реально сработал (проверять `TryBind == true`); guard-rejection ветки покрыты не полностью.
3. M12 #3 — только при срабатывании триггера.

## #183 disposition

AC4 **met** (2026-10-05, interleaved 10-pair ABAB; 0/10 min и 1/10 p10 блоков ≥1.20, единственный p10-блок — load-spike outlier) → **общая приёмка #183 met**. B1+B2 применены в рабочем дереве и остаются **незакоммиченными сверх `322f9be`**; AC4-запись — `docs/specs/status/iteration-15-cached-path-183-4-evidence/ac4/`. Публичного API-изменения нет. Атрибутируемое ускорение на `Join_CachedHit_ToList` не заявляется (материализация доминирует над cached path) — это ожидаемый исход, а не регрессия; «speedup proven» на этом арме не заявляется.

## Cycle 4 (#183 continuation) — measured results (CHECK r=1 FAIL, DO loop-back n=2/3)

Статус: **measured; CHECK r=1 FAIL (n=1/3), DO loop-back n=2/3 — приёмка не объявлена**. B1+B2 применены в рабочем дереве (не закоммичены).

Аллокации (детерминированно, `[MemoryDiagnoser]`, B/op на попадание в кэш = raw / 100, `bop-summary`):
- `Where_CachedHit_PlanOnly` / `Where_CachedHit_ToList` — **Δ −1944.03 / −1944.04 B/op** (per-op, N-инвариантно; raw per-100-invocation −194,403 / −194,404).
- `Join_CachedHit_PlanOnly` / `Join_CachedHit_ToList` — **Δ −359.96 / −424.00 B/op** (per-op; raw per-100-invocation −35,996 / −42,400).
- Harness `StageAttributionBenchmark.CachedHit_PlanOnly_Param` per-hit (N=256/4096): 6528.13 → 4584.09 B/op (Δ −1944.04).

Гейт аллокаций `eng/perf/iteration14_gate.py` → **exit 0**, 56 row/jobs в бюджете, 0 проваливших.

Acceptance-категория → **7/7, exit 0, 39.46 с BDN** (внешне ~39–43 с); отношение `Cached_ToList`/`Prepared_ToList` — **0.90** (`acceptance-new.log`; `Cached_PlanOnly_Param` 0.30, `Prepared_ToList` baseline 1.00).

Stage-attribution slope (out-of-process, N ∈ {1,16,256,4096}): отношение new/old по раундам
{0.526, 0.745, 0.565, 0.683, 0.535}, медиана **0.565**, знак стабилен 5/5, R² ≥ 0.9996
(минимум 0.999684); in-process кросс-проверка 0.898 (тот же знак). loadavg хоста 2.04–5.34
(параллельные сессии) — зафиксировано.

Evidence (durable, in-repo): `docs/specs/status/iteration-15-cached-path-183-4-evidence/{bop-summary.json,gate-new.log,acceptance-new.log,slope/slope-summary.json}`.
Авторитетное сырьё (raw JSON/логи) — `docs/specs/status/iteration-15-cached-path-183-4-evidence/`.

Сохранённые патчи `b1-incomplete.patch` / `u2-incomplete.patch` **перекрыты** реализацией цикла 4:
B2 агрессивнее — пер-хитовые аллокации словаря/коллектора удалены, одиночный boxing,
иммутабельный recipe, mismatch-safe fallback.

Публичного API-изменения нет; поведение прежнее.

## Cycle 4 — escalation: robust re-analysis of the D5 rejoin data (2026-10-05)

Решение эскалации: **`accept-with-open-AC4`**. Уже собранные данные `docs/specs/status/iteration-15-cached-path-183-4-evidence/rejoin/` (5 раундов old/new; новые бенчмарки НЕ запускались) пересчитаны по load-robust статистике. Источник — per-iteration `Statistics.OriginalValues` из `*-report-full-compressed.json`: `min` и `p10` (нагрузка только добавляет время, поэтому min/p10 оценивают истинную стоимость; среднее в шумных раундах смещено вверх). Per-iteration CSV в `rejoin/` отсутствует.

Отношение **new/old** по раундам (min; ниже — p10):

| arm | r1 | r2 | r3 | r4 | r5 | median min | median p10 | new<old (min) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Where_CachedHit_PlanOnly | 0.768 | 0.718 | 0.753 | 0.728 | 0.753 | 0.753 | 0.749 | 5/5 |
| Where_CachedHit_ToList | 0.940 | 0.928 | 0.896 | 0.937 | 0.882 | 0.928 | 0.906 | 5/5 |
| Join_CachedHit_PlanOnly | 0.935 | 1.050 | 1.061 | 1.123 | 0.983 | 1.050 | 1.048 | 2/5 |
| Join_CachedHit_ToList | 0.215 | 1.000 | 0.887 | 1.009 | 0.977 | 0.977 | 0.959 | 4/5 |

p10-отношения по раундам: Where_PlanOnly 0.772/0.727/0.752/0.720/0.749; Where_ToList 0.955/0.865/0.906/0.965/0.875; Join_PlanOnly 0.930/1.048/1.078/1.179/0.984; Join_ToList 0.217/1.060/0.873/1.010/0.959.

`Join_CachedHit_ToList`: **within noise**. Средние r3 (+110.12%) и r4 (+181.65%) — артефакт нагрузки: распределение new-раундов бимодально (`min`/`p10` ≈ 3.05–3.11 мс, но 43/100 и 61/100 итераций > 5 мс; loadavg 8.86–10.21 и 6.32–6.37). По `min`/`p10` new не медленнее old: отношения r2–r5 ∈ [0.887, 1.009] (отклонение ≤1.3% или быстрее); r1 искажён сам old (23 итерации, все ≈ 15 мс). Новый медленнее old во всех раундах НЕ наблюдается.

AC4 не объявляется выполненной. Открытый пункт: **AC4 (no reliable E2E regression) not established for `Join_CachedHit_ToList`; re-measure in a quiet window or CI (ABAB, taskset, ≥10 pairs, min/p10, paired Wilcoxon)**. Формулировки acceptance не изменялись; «AC4 met»/«speedup proven» не заявляются.

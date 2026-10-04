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

`DONE (with limitation)`; B1/B2 не засчитаны как поставленная ценность; формулировки «accepted»/«speedup proven» не использовать.

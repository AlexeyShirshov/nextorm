# TODO: Warm-путь построения плана (CTE / recursive CTE / Join4 / IN-list)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.19.
> Диагностика — `docs/specs/performance/benchmark-report.md`, итерации 6–7.

## Пункт и цель

- Проблема: на быстром (tmpfs) полном прогоне **prepared**-путь nextorm выигрывает все классы у Dapper,
  EF Core и linq2db, но **warm (не-prepared) путь** всё ещё отстаёт от Dapper примерно в 1.2–1.7× на
  `CTE`, рекурсивном `CTE`, `Join4` и `IN`-list (итерация 7, «Вывод»).
- Диагноз итерации 6: проигрыши находятся **не в исполнении и не в маппинге**, а в **построении и
  ключевании плана**. Часть уже устранена (IN-list — `InValuesEvaluator`; `INTERSECT`/`EXCEPT`/
  recursive CTE — баг хеша под-команды; холодный построитель join), но разрыв не закрыт полностью.
- Цель: закрыть warm-путь до «на уровне/выше Dapper» на перечисленных фичах, не сломав prepared-путь.
- Критерий приёмки: в `SqliteBenchmarkFeaturesFairCached.*` nextorm на `CTE`/рекурсивном `CTE`/`Join4`/
  `IN`-list не медленнее лучшего конкурента (Dapper) либо разница в пределах шума прогона (≤ ±3 %);
  prepared-путь не регрессирует (итерации 3–7); SQL-генерация и интеграционные тесты зелёные; покрытие
  не ниже `MIN_LINE_COVERAGE`; отчёт по бенчмаркам обновлён.

## Данные (baseline)

- Итерация 3 (tmpfs, полный режим): prepared выигрывает все классы; `First (entity)` и
  `LargeIteration (AsyncStream)` закрыты.
- Итерация 4: cold plan build 293 → ~30 µs (SQL-ключевой `MapperCache`).
- Итерация 6: IN-list plan-build 74–122 µs → ~7 µs, warm 161–184 µs → 71–79 µs; `INTERSECT`/`EXCEPT`
  ~8×, recursive CTE ~7×; join cold build LEFT 9.3→6.7 µs, 4-table 21.6→13.1 µs.
- Итерация 7 (открытый разрыв): warm CTE / recursive CTE / Join4 / IN-list vs Dapper 1.2–1.7×.
- Внимание: верхняя таблица отчёта («Сводка по классам, полный режим») — прогон итераций 1–2 на `/mnt/c`
  и помечена в отчёте как некорректная; опираться только на итерации 3–7.

## Прогресс

- **Итерация 8 (сделано).** IN-list warm plan-only 8.80 → **6.53 µs** (−26 %), `Contains` 9.75 → 7.08 µs
  (−27 %); end-to-end IN inline 1.45–1.53× Dapper (было ~1.67–1.88×). Причина остатка: `QueryPlanner`
  выставлял `NeedsParamRefresh` для любого не-runtime параметра, поэтому инлайн-список
  (`new[]{1,3,10}`) переизвлекался на каждом cache-hit'е. Инлайн-значения помечены `Parameter.Stable`
  (`InValues.IsStableValueExpression`, `QueryPlanner.cs`), SQL не меняется, prepared-путь не регрессировал.
- **Открыто:** CTE ~1.31×, recursive CTE ~1.52×, Join4 ~1.15×, IN **captured** ~1.61–1.70× от Dapper.
  Локального безопасного рычага нет: не-prepared API каждый раз собирает и заново ключует дерево
  (промаха кэша нет) — нужен shape-keyed кэш подготовленных CTE/join-под-команд либо проталкивание
  prepared-API во fluent-путь. См. `docs/specs/performance/performance-findings.md` M12 (широкая
  перестройка, отдельная задача).

## Матрица «фича × путь»

| Фича | Prepared | Warm (открыт) | Где искать стоимость |
|---|---|---|---|
| `CTE` | выигрывает | vs Dapper ~1.2–1.7× | ключевание/рендер `CteDefinition`, `CtesPlanHash`, `SqlSourceRenderer` |
| `RecursiveCte` | выигрывает | vs Dapper ~1.2–1.7× | то же + `maxRecursion` |
| `Join4` | выигрывает | vs Dapper ~1.2–1.7× | холодная сборка join, alias resolution, план-хеш join |
| `IN`-list | выигрывает | на грани (после итерации 6) | `InValuesEvaluator`, `RefreshInValuesShape`, `WherePlanHash` |
| Остальные фичи (CaseWhen, SumOver, RowNumber, Distinct, Udf, ToUpper, joins ≤2) | выигрывает | в пределах шума/выигрывает | — |

## План работ

1. **Измерить и разложить.** Прогнать в изолированной копии (`/tmp`, tmpfs-БД):
   `--filter "*SqliteBenchmarkFeaturePlanBuild*"` (холодное построение SQL по фичам) и
   `*SqliteBenchmarkFeaturePlanCache*`/`*SqliteBenchmarkFeaturesFairCached*` (warm). Профилировать
   `dotnet-trace`/`dotnet-counters` на CTE/Join4, разложить warm-стоимость на компоненты: визиторы
   `PrepareCommand`, план-хеш (`QueryPlanEqualityComparer`), ключевание плана, компиляция маппера.
2. **CTE / recursive CTE.** Убрать повторный рендер/ключевание деклараций CTE между вызовами одной формы
   (кэш по форме, как SQL-ключевой `MapperCache`); проверить, что `CtesPlanHash` считается по под-команде
   и не пересчитывается лишний раз.
3. **Join4.** Снизить холодную сборку составного join (alias resolution + `JoinExpression`-хеши);
   переиспользовать уже посчитанные под-хеши вместо полного пересчёта.
4. **IN-list.** Добить остаточный разрыв: избежать аллокаций `RefreshInValuesShape`/`WherePlanHash` в warm
   при неизменной форме списка.
5. **Регресс-гейт.** После каждой правки прогонять итерации 3–7-классы, чтобы prepared-путь не просел.

## Матрица «провайдер × форма»

Фича не провайдерная (метаданные/план/визиторы ядра), но проверить, что SQL не меняется и не регрессирует:

| Провайдер | Влияние | Проверка |
|---|---|---|
| SQLite | замеряется (бенчмарк), fixed warm-путь | `SqliteBenchmarkFeaturesFair*`, `*PlanBuild*` |
| PostgreSQL / SQL Server / MySQL / MariaDB / ClickHouse | SQL-генерация не должна измениться | соответствующие `*SqlGenerationTests` |
| InMemory | не затронут (нет SQL-плана) | `nextorm.core.tests` |

## Ближайший C#-аналог и уровень реализации

- Аналог — уже существующий SQL-ключевой `MapperCache` (итерация 4) и `InValuesEvaluator` (итерация 6):
  кэш по форме, а не по дереву выражения. Новых публичных API не требуется; правки `internal`.
- Уровень — ядро (`NextORM.Core`): `QueryCommand`/`QueryPreparer`, `SqlBuilder`, `Visitors/`,
  `SqlSourceRenderer`, `EntityBuilder`/`JoinedEntityBuilder`.

## Публичный API

- Новых публичных членов не планируется. Если появится — соблюсти конвенции и добавить в
  `docs/advanced/api-reference.md` EN+RU.

## План тестов

- Бенчмарки: `benchmarks/nextorm.benchmark` (`SqliteBenchmarkFeaturesFair`,
  `SqliteBenchmarkFeaturesFairCached`, `SqliteBenchmarkFeaturePlanBuild`, `SqliteBenchmarkFeaturePlanCache`).
- SQL-генерация: `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/SqlGenerationTests.cs`
  (защита от изменения SQL).
- In-memory: `tests/nextorm.core.tests` (не затронут).
- Интеграция: `tests/nextorm.integration.tests` (по возможности, SQLite локально) — без изменения
  результатов.
- Покрытие: `coverage.settings.xml` включает `nextorm.{core,sqlite,postgres,sqlserver}`; `MIN_LINE_COVERAGE`
  не понижать.

## Файлы к изменению

- Код: `src/nextorm.core/Query/QueryCommand*.cs`, `src/nextorm.core/DataContext/SqlBuilder.cs`,
  `src/nextorm.core/DataContext/SqlSourceRenderer.cs`, `src/nextorm.core/Visitors/*`,
  `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs`,
  `src/nextorm.core/DataContext/QueryPlanner.cs`.
- Бенчмарки: `benchmarks/nextorm.benchmark/*` (только если нужны новые замеры/атрибуты).
- Доки: `docs/specs/performance/benchmark-report.md` (новая итерация с результатами); при шиппинге —
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.19 (→ shipped) и этот файл (удалить).
- Спеки: `docs/specs/design/code-smells-review.md` — если правки затронут горячие пути (аллокации/LINQ).

## Порядок и верификация

```bash
# бенчмарки (изолированная копия, tmpfs-БД)
cd benchmarks/nextorm.benchmark && dotnet build -c Release
dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturesFairCached*"
dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturePlanBuild*"
# регресс SQL-генерации
cd /home/alex/sources/nextorm
dotnet build nextorm.sln -c Release
dotnet test tests/nextorm.sqlite.tests -c Debug
```

## Открытые вопросы

1. Приоритет: CTE/recursive CTE или Join4 первыми? (CTE-путь дешевле проверить, Join4 — крупнее выигрыш
   у пользователя.)
2. Достаточно ли «≤ ±3 % от Dapper» как критерия, или нужна строгая победа во всех warm-классах?
3. Нужен ли отдельный cache-ключ для CTE-форм (по аналогии с `MapperCache`), или достаточно снять
   повторное ключевание?
4. Обновлять ли верхнюю (некорректную) сводку отчёта или пометить её как историческую.

## Хендофф

- Исполнитель: `nextorm-db-perf-analyst` (может применять правки и мерить), при необходимости —
  `nextorm-performance-analyst` для триажа.

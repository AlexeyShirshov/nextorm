# План: стоимость свежей команды на warm cached пути (Категория B)

> **Статус:** план, не начат.
> **Ревизия базы:** `5d9b8d8` + Итерации 12–13 (`docs/specs/performance/benchmark-report.md`).
> **Дата:** 2026-09-26.
> **Область:** `src/nextorm.core` — fluent-сборка команды, `PrepareCommand`, план-кэш (`QueryPlan`/`QueryPlanStore`/`QueryPlanEqualityComparer`).
> **Внутренний документ** (не собирается DocFX, не публикуется; ссылки на него из `docs/**` запрещены).

## 1. Контекст

«Категория B» в отчёте — тёплый implicit-кэш плана: `Nextorm_Cached` (fluent-запрос с константой/захватом) против
Dapper (raw SQL), обычных EF Core и linq2db. По последнему полному прогону (Итерация 10, `benchmark-report.md`):

| Класс (N) | Nextorm_Cached ÷ лучший | Лучший |
|---|--:|---|
| Any (100) | 1.17× | Dapper |
| First scalar / entity (10) | 1.10× / 1.06× | Dapper |
| Single (10) | 1.15× | Dapper |
| Join (10) | 1.47× | Dapper |
| Where for-loop (100) | 0.70× | nextorm |
| Where list (100) | 1.19× | Dapper |
| Iteration list (1) | 0.73× | nextorm |
| LargeIteration list (1) | 0.75× | nextorm |

Проигрыш стабилен с Итераций 7–9; в Итерации 9 принят как «санкционированный быстрый путь — `Prepare()`, а не
implicit-кэш». Итерации 12–13 этот разрыв не закрыли: они целят в cold/SQL-build (`SqlFunctionAttribute`, O(1)-индекс
свойств, единая резолюция метаданных) и в **переиспользуемую** команду (мемоизация ключа плана). Обе не помогают
паттерну «свежая команда на каждый вызов», которым и являются бенчмарки Категории B.

## 2. Проблема: warm hit экономит только рендеринг SQL

На каждый вызов `_ctx.SimpleEntity.Where(e => e.Id == i).AnyAsync()` core проходит:

| # | Стадия | Код |
|---|---|---|
| 1 | Клон билдера + `ToCommand()` → свежий `QueryCommand` | `Builders/EntityBuilder.cs:295`, `:229` |
| 2 | **Полный `PrepareCommand` свежей подкоманды на каждый вызов** | `Builders/EntityBuilderExtensions.cs:955,978` (Any); `DataContext/QueryPlanner.cs:381` (First/Single/Join/Where-list) |
| 3 | `ReplaceCommand` → сбрасывает мемо ключа плана | `Query/QueryCommand.cs:507-520` |
| 4 | Внешний `RefreshInValuesShape` | `Query/QueryCommand.QueryPreparer.cs:127` |
| 5 | `new QueryPlan` + хэш + lookup в словаре | `Query/QueryCommand.Plan.cs:30`, `DataContext/QueryPlanner.cs:386-395` |
| 6 | **Глубокое структурное `Equals`** (Where/Select/ReferencedQueries) | `DataContext/Cache/QueryPlan.cs:76` → `Query/QueryPlanEqualityComparer.cs:43` → `Query/ExpressionPlanEqualityComparer.cs:75` |
| 7 | Обновление значений рантайм-параметров + execute | `DataContext/QueryPlanner.cs:477-485` |

План-кэш экономит стадию рендеринга SQL (`MakeSelectInternal`), но **стадии 2 и 6 остаются на каждом вызове**.
Это подтверждается трейсом тёплого lookup'а (Итерация 12/13): `QueryPlanEqualityComparer.Equals` ≈ 39 %,
`ExpressionComparer.Compare*` ≈ 31 %, `SelectExpressionPlanEqualityComparer.Equals` ≈ 14 %.

Для `Any`/`Count`/`All` усугубляется тем, что общая команда на контекст
(`EntityBuilderExtensions.GetAnyCommand`, `:955`) **каждый раз готовит свежую подкоманду** (`:978`) и подменяет
ссылку через `ReplaceCommand` (`:979`), который обязан инвалидировать мемо (Итерация 13, регрессия `Any`).

## 3. Цель и метрики (Definition of Done)

1. **Основная метрика:** `Nextorm_Cached ÷ Dapper` на точечных классах (Any/First scalar/First entity/Single/Join/
   Where list) — с 1.06–1.47× до **≤ 1.0×** (или явно зафиксированный остаток с обоснованием).
2. **Вторичные:** warm ns на этих классах −X %; аллокации B/op не растут (цель — снижение).
3. **Без регрессий:** prepared-путь и cold-путь (промах кэша, `Cache=false`) — в пределах шума (≤ ±3 %).
4. **Корректность:** все юнит-сьюты + полная интеграционная матрица через Testcontainers (PG/SQL Server/MySQL/
   ClickHouse, не «skipped»); покрытие ≥ `MIN_LINE_COVERAGE`; инварианты `Debug.Assert` в `QueryPlan` сохранены.
5. **Документация:** новая Итерация в `benchmark-report.md` с числами и воспроизведением.
6. **API:** публичная поверхность не меняется.

## 4. Фаза 0 — базлайн и декомпозиция (обязательна до кода)

Цель — не гадать, где именно µs, а получить разбивку по стадиям 1–7 на текущей ревизии.

- Воспроизвести Категорию B на `5d9b8d8` + Итерации 12–13: interleaved A/B, tmpfs (`NEXTORM_BENCH_DB`), один CPU,
  несколько раундов, оценка по min (методика Итераций 12–13).
- Собрать изолированный harness-арм, повторяющий поток `Nextorm_Cached` (для Any — с общей `AnyCommand`), и
  инструментировать/разделить: build / prepare(+hash) / lookup(+Equals) / param-refresh / execute. Инструмент —
  `dotnet-trace --profile dotnet-sampled-thread-time` + локальные счётчики.
- **Deliverable:** таблица «стадия → ns/вызов, доля, B/op» для Any/First/Single/Join/Where-list; по ней выбирается
  рычаг Фаз 1–3 и режется объём.

## 5. Фаза 1 — targeted, низкий риск

### 1a. Сохранять мемо ключа через `ReplaceCommand` при неизменной форме подзапроса
- **Идея:** в `GetAnyCommand` свежая подкоманда уже подготовлена; её вклад в ключ — `ReferencedQueriesPlanHash`
  внешней команды (`QueryCommand.QueryPreparer.cs:98-117`), который считается из предвычисленных под-хэшей. Передать
  этот хэш в `ReplaceCommand`; если он равен зафиксированному в мемо — **не сбрасывать** `_planKey` (и хранимый
  `ReferencedQueriesPlanHash`), иначе — сбросить, как сейчас.
- **Эффект:** общая `Any/Count/All`-команда на повторяющейся форме попадает в кэш **по ссылке** → стадия 6
  пропускается; таргетит классы Any/Count.
- **Файлы:** `Query/QueryCommand.cs:507`, `Builders/EntityBuilderExtensions.cs:955-982`, при необходимости
  `Query/QueryCommand.Plan.cs`.
- **Риск:** среднее — тот же участок дал регрессию `Any` в Итерации 13. Гард: строгое сравнение хэша формы
  подзапроса + shape-хэши (`InValuesShapeHash`/`PreWhereShapeHash`), регрессионные тесты `Any/Count/All`
  (shared-команда) до правки.

### 1b. Аллокации в пути сравнения/поиска
- **Идея:** переиспользовать thread-static `ExpressionComparer` и его `Dictionary` области параметров вместо
  аллокации на каждый `Equals` (`Query/ExpressionPlanEqualityComparer.cs:98,218`); убрать per-call churn `new QueryPlan`.
- **Эффект:** B/op и часть ns; не меняет алгоритмическую сложность.
- **Файлы:** `Query/ExpressionPlanEqualityComparer.cs`, `Query/QueryCommand.Plan.cs`.

## 6. Фаза 2 — hash-only prepare (структурная, средний риск)

- **Идея:** на промахе/попадании вычислять только хэши плана, пробовать lookup, а дорогие подготовленные части
  (трансляция `PreparedCondition`, select-list, columns, from) строить **лишь при промахе**. Сейчас
  `PrepareCommand(false)` (`Query/QueryCommand.Prepare.cs:17`, `QueryCommand.QueryPreparer.cs:24`) делает всё сразу на
  каждом вызове.
- **Эффект:** убирает стадию 2 с hit-пути — главный рычаг для First/Single/Join/Where-list.
- **Файлы:** `Query/QueryCommand.QueryPreparer.cs`, `Query/QueryCommand.Prepare.cs`, точки вызова в `QueryPlanner.cs`.
- **Сложность:** высокая. Сейчас переключатель `dontCalculateHash` — **противоположный** (пропустить хэш); нужен
  явный режим «только хэш» с гарантией, что промах достроит всё. Обязательно сохранить порядок резолюции
  (collation/naming/keyword на `:28-30`) и семантику `ShapeScanned` (`:35`).
- **Гард:** тесты на холодный промах и на hit для каждого терминала; сверка подготовленных частей hit vs miss.

## 7. Фаза 3 — fingerprint fast path (высокий риск, только если Фазы 1–2 оставили разрыв)

- **Идея:** хранить у плана 64/128-битный структурный отпечаток (комбинация предвычисленных под-хэшей и флагов
  формы). При совпадении отпечатка пропускать глубокий обход дерева в `QueryPlanEqualityComparer.Equals`
  (`Query/QueryPlanEqualityComparer.cs:43,311`); глубокое `Equals` остаётся страховкой от коллизий.
- **Риск:** высокий — идентичность плана и повторное использование закэшированного плана. 32-битный хэш для такой
  оптимизации недостаточен; отпечаток должен быть криптостойко-достаточным (64/128 бит) и покрывать **все** поля
  ключа. Обязателен design-review до кода.
- **Файлы:** `Query/QueryPlanEqualityComparer.cs`, `Query/ExpressionPlanEqualityComparer.cs`, `DataContext/Cache/QueryPlan.cs`.

## 8. Guardrails и риски

- **Идентичность плана — зона регрессий** (см. Iteration 13: общая `AnyCommand` + `ReplaceCommand`). Любая правка
  Фаз 1–3 обязана иметь регрессионные тесты на: shared `Any/Count/All`, форму In-values (`InValuesShapeHash`),
  `PreWhereShapeHash`, outer references, document-mode терминалы.
- Публичный API не менять; `Debug.Assert`-инварианты хэш/равенство в `QueryPlan` сохранить.
- CRLF; `TreatWarningsAsErrors=true`.
- Не коммитить; интеграция через патч, оставляя результат незакоммиченным (правила `AGENTS.md`).

## 9. Порядок работ и объём

| Фаза | Содержание | Риск | Ожидаемый эффект |
|---|---|---|---|
| 0 | Базлайн + декомпозиция по стадиям | — | выбор рычага, ~0.5–1 день |
| 1a | Мемо через `ReplaceCommand` (Any/Count/All) | средний | снятие доли стадии 6 |
| 1b | Аллокации сравнения/поиска | низкий | B/op ↓ |
| 2 | Hash-only prepare | высокий | стадия 2 с hit-пути (крупно) |
| 3 | 64/128-бит fingerprint | высокий | остаток стадии 6 |

Фаза запускается только если предыдущая не выполнила метрику п. 3.1.

## 10. Верификация

```bash
# юнит-сьюты
dotnet test tests/nextorm.core.tests -c Debug
# интеграция (все провайдеры, не skipped):
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
# BDN Категория B (после сборки benchmark):
NEXTORM_BENCH_FULL=1 NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db \
  dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter "*SqliteBenchmarkAny*" --filter "*SqliteBenchmarkFirst*"
```

Артефакты — `benchmarks/BenchmarkDotNet.Artifacts/results/`; сводка — новая Итерация в `benchmark-report.md`.

## 11. Открытые вопросы

- Достаточно ли Фазы 1a, чтобы закрыть Any/Count, или общая команда всё равно платит `PrepareWhere`
  (тогда нужен Фаза 2 даже для Any)?
- Для First/Single/Join/Where-list, где общей команды нет, Фаза 2 — единственный структурный рычаг; есть ли
  безопасный способ не транслировать `PreparedCondition` при попадании, учитывая, что ключ плана использует
  именно его хэш?
- Нужен ли 64-битный отпечаток как отдельный слой (Фаза 3) или можно ограничиться Фазой 2.

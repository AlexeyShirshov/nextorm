# Спека: единый child-loading pipeline (eager loading builder-поверхности)

> Tracking: [#209](https://github.com/AlexeyShirshov/nextorm/issues/209) (E1 strategy),
> [#210](https://github.com/AlexeyShirshov/nextorm/issues/210) (E2 ordering — **свёрнут в E3**),
> [#211](https://github.com/AlexeyShirshov/nextorm/issues/211) (E3 per-parent top-N),
> [#212](https://github.com/AlexeyShirshov/nextorm/issues/212) (E4 nested),
> [#213](https://github.com/AlexeyShirshov/nextorm/issues/213) (E5 JoinInto-остаток).
> Milestone **1.0.9**. Статус: дизайн согласован в штурме (Q1–Q10), черновик спеки — **на ревью**;
> реализация не начата.

> **Граница (Q1).** Спека — **builder-only**. IR-путь #20 (`IQueryable → IR → QueryCommand/QueryDefinition`,
> `docs/superpowers/specs/2026-10-04-linq-provider-roadmap-design.md:71,83`) проектируется независимо;
> здесь — только [sync-заметка](#14-sync-заметка-к-20). Спека внутренняя и **не** линкуется из публичных доков.

## 1. Цель и не-цели

**Цель.** Единый канонический child-loading pipeline под builder-API: `LoadWith`, `JoinInto`,
implicit navigation (#148) — тонкие фронтенды над общим ядром (сборка/дедуп/порядок/лимит/вложенность).
Закрыть претензию сравнения «eager-load ordering/strategy» (`docs/specs/comparison/linq2db-comparison.md:229-234,276-279`).

**Не-цели.** Change tracking / identity map между запросами; FK-convention inference; EF `Include`/`ThenInclude`
(это #20 phase 7); замена существующих публичных API; IR-path #20.

**Зафиксированные решения штурма (Q1–Q10).**

| # | Решение |
|---|---|
| Q1 | Спека builder-only; IR — sync-заметка. |
| Q2 | Единый pipeline; `LoadWith`/`JoinInto`/implicit-nav — фронтенды. |
| Q3 | Именованные стратегии + авто-диспетчер; `JoinInto` — фиксированный single-query фронтенд. |
| Q4 | E2 свёрнут в E3; отдельно — single-query honour'ит child `OrderBy` (ремап/fail-closed). |
| Q5 | per-parent top-N: `take` и `skip+take`, order-key обязателен; гибрид (оконная по умолчанию, коррелированная опц.), иначе fail-closed. |
| Q6 | nested: рекурсия по child-builder, глубина без ограничения, режим единый на граф. |
| Q7 | Стратегии `{Split, Single, Correlated, Auto}`; fallback **fail-closed** (не тихий, в отличие от linq2db #5904). |
| Q8 | nested в split и Single, где выразимо (иначе fail-closed); ordering/limit независимо на каждом уровне. |
| Q9 | Полный набор фич на всех трёх фронтендах. |
| Q10 | (1b) не-stitching терминалы на loaded-билдере → fail-closed; (2a) strategy/order/limit/nesting в ключе план-кэша, без sticky-флагов; (3a) context-level default стратегии на `DataContextBuilder`. |

## 2. Архитектура

```
фронтенды:  LoadWith (селекторы) · JoinInto (метаданные) · implicit-nav (#148)
                     │  нормализуют в
                     ▼
   канонический load-graph  →  pipeline  →  QueryCommand(ы) + in-memory stitcher
```

- **Нормализованный load-node:** тип (коллекция/ссылка), источник связи (селекторы/метаданные), ключи,
  **стратегия**, **ordering**, **top-N**, вложенные узлы (рекурсия), фильтр-скоуп, участвующие фильтры (E-набор).
- **Ядро:** `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs` + `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs`
  сводятся к одному stitcher'у/декодepу; существующие `LoadWith`/`JoinInto` — адаптеры (`todo_navigation_properties.md` §3).
- Поведение level-1 по умолчанию (`LoadWith` split, `JoinInto` single) сохраняется бит-в-бит.

## 3. Стратегии (E1)

Публичные значения (имена финализируются на **API-naming review**, не 1:1 linq2db):

| Стратегия | Смысл |
|---|---|
| `Split` | Наш текущий split: `WHERE childKey IN (...)` батчами ≤1000 (`docs/advanced/eager-loading.md:61-79`). Экв. linq2db `KeyedQuery`. |
| `Single` | Один денормализованный `LEFT JOIN` (`eager-loading.md:83-121`). Аналога у linq2db нет. |
| `Correlated` | Коррелированная per-parent выборка (`CROSS APPLY`/`LATERAL`/окно), нужна для top-N. |
| `Auto` | Авто-диспетчер: выбирает стратегию по форме узла (см. ниже). |

- **Авто-диспетчер (`Auto`)**: top-N запрошен → `Correlated`; иначе дефолт `Split`; `Single` — только явно.
- **Context-level default (Q10.3a):** на `DataContextBuilder` (аналог linq2db `LinqOptions.DefaultEagerLoadingStrategy`);
  явный marker на узле/builder'е выигрывает у глобального default.
- **Fail-closed (Q7.i):** невыразимая/неподдержанная провайдером стратегия → `NotSupportedException`;
  **никакого тихого fallback** (осознанное расхождение с linq2db #5904).
- `JoinInto` — фиксированный `Single`-фронтенд (стратегия не настраивается); `LoadWith`/implicit-nav конфигурируемы.

## 4. Ordering + per-parent top-N (E2 свёрнут в E3)

**Семантика.** У узла может быть задан order-key и лимит на родителя: `take` (первые N) и `skip+take`
(N-й..M-й). **Order-key обязателен** при лимите (без него top-N неопределён). Без лимита порядок детей
задаётся `OrderBy` child-запроса (как сейчас).

```csharp
// гипотетический API (имена — на API-naming review)
ctx.From<Customer>()
   .LoadWith(c => c.Orders, q => q.From<Order>(), c => c.Id, o => o.CustomerId,
             orderBy: o => o.CreatedAt, desc: true, take: 5)   // E3
   .ToList();
```

**Реализация (Q5.c, гибрид):**
- Оконная форма по умолчанию — `ROW_NUMBER() OVER (PARTITION BY parentKey ORDER BY <order-key>)`,
  фильтр `rn BETWEEN skip+1 AND skip+take`, в split-запросе. Портируемо: SQL Server / PostgreSQL / MySQL 8 /
  SQLite 3.25+ / ClickHouse.
- Коррелированная форма (`CROSS APPLY`/`LATERAL`) — опционально/где диалект и план оправдывают.
- Провайдер без требуемой формы → **fail-closed**.

**Фикс single-query (Q4).** В single-query режиме child `OrderBy` сейчас отклоняется
(`eager-loading.md:113`). Разрешаем: порядок поднимается на внешний запрос/ремапится через проекцию;
невыразимо → fail-closed (не тихий дроп).

## 5. Nested level 2+ (E4)

- **Форма (Q6.a):** рекурсия по child-builder — child-запрос сам несёт `LoadWith(...)`, pipeline уходит глубже.
- **Глубина (Q6.i):** без ограничения.
- **Режим (Q6.1):** единый на весь граф; per-level override режима не входит в этот цикл.
- **Стратегия уровня (Q8.b):** nested в `Split` и `Single`, где выразимо; иначе fail-closed.
- **Ordering/limit (Q8.1):** применимы независимо на каждом уровне.
- Уровень-1 поведение (`eager-loading.md:123-125`) не меняется; сегодня nested не honour'ится — станет поддержан.

## 6. JoinInto-остаток (E5)

Снять `NotSupportedException` для форм, отложенных после слайсов A+B (`todo_navigation_properties.md:259-268,323`):
составной principal-key / составной FK; составной junction-селектор; M:N-`JoinInto` под single-query.
Связь по-прежнему **только из метаданных** (FK-convention нет).

## 7. Сквозной семантический контракт (по уровням и фронтендам)

- **Assignment rule** — как `eager-loading.md:127-147` (non-null → clear+refill; null+settable → новый list;
  null+read-only → fail-closed).
- **Dedup/identity:** родитель дедуплицируется по mapped-ключу (single); на вложенных уровнях identity
  составная (ключ уровня-1 + ключ уровня-2 …), сборка — теми же правилами, что level-1.
- **Filter-scope:** эффективный скоуп ребёнка = объединение его `IgnoreFilters` и родительского
  (`eager-loading.md:114`); распространяется на все уровни.
- **Null-keys:** null-ключ не матчится, не падает (`eager-loading.md:155`).
- **Порядок:** заданный пользователем порядок детерминирован; при равных ключах — по контракту провайдера.

## 8. Терминалы (Q10.1b)

Load honour'ят (как сейчас) 4 stitching-терминала: `ToList`/`ToListAsync`/`ToArray`/`ToArrayAsync`.
**Изменение:** на loaded-билдере прочие терминалы (`ToHashSet`, `ToDictionary`, `First*`, `Single*`,
`ToEnumerable`, `ToAsyncEnumerable`, `ToCommand`, `Any`/`Count`) → **fail-closed** (`NotSupportedException`),
чтобы загрузка не терялась тихо (сейчас silent-ignore, `eager-loading.md:167-184`).

## 9. Кэш и владельцы (Q10.2a)

- strategy / ordering / top-N / nested / filter-scope входят в **ключ план-кэша**; ключ не захватывает runtime-значения.
- Без sticky `Cache=false` (AGENTS); подготовка без кэша — только call-local `storeInCache:false`.
- Инвалидация identity-кэшей (`JoinIntoSpec.IdentitySelectorCache`, #174) сохраняется.

## 10. Диагностика / fail-closed

Единый стиль `NotSupportedException` с точным указанием стратегии/формы/провайдера и `ParamName`, где применимо;
никакой клиентской оценки за границей явных `AsEnumerable`/materialization.

## 11. Frontend-матрица

| Фича \ Фронтенд | `LoadWith` | `JoinInto` | implicit-nav (#148) |
|---|---|---|---|
| Стратегия (E1) | настраивается | fixed `Single` | по контракту фронтенда |
| Ordering + top-N (E2/E3) | да | да | да |
| Nested (E4) | да | да | да |
| JoinInto-остаток (E5) | — | да | — |

## 12. Приёмка

Матрица «функция × провайдер (PG/MSSQL/MySQL/MariaDB/ClickHouse/SQLite) × форма», in-memory паритет.
Тесты: core (`nextorm.core.tests` — нормализация/сборка/порядок/дедуп), провайдерные SQL-gen,
`tests/nextorm.integration.tests/CommonTestSuite.*` (без `skipped`). Coverage ≥ `MIN_LINE_COVERAGE`/`MIN_BRANCH_COVERAGE`.
Негатив на каждый fail-closed путь.

## 13. Документация

EN+RU: `docs/advanced/eager-loading.md`, `docs/advanced/relationships.md`, `docs/advanced/limitations.md`,
`docs/guide/29-implicit-navigation.md`; обновить `docs/specs/comparison/linq2db-comparison.md` и
`linq2db-backlog-gap-analysis.md` (снять «ordering/strategy» из out-of-scope).

## 14. Sync-заметка к #20

Спека builder-онли. #20 phase 6 («nested collections/DTO … ordering/batching/reassembly»,
`...linq-provider-roadmap-design.md:170`) реализует ту же семантику на IR-пути. Во избежание расхождения:
общий **семантический контракт** (раздел 7) — источник истины для обоих; при старте phase 6 сверить и
не допускать divergent behavior (child ordering, dedup identity, filter-scope).

## 15. Открытые вопросы (решаются при реализации)

- Точные имена публичного API стратегии/ordering/limit/nested (API-naming review).
- Размещение типов pipeline (namespace/package) и форма context-level default.
- Точная форма оконной vs коррелированной генерации по диалектам (нормализация/эмуляция).
- Единый stitcher: объём рефакторинга `EntityBuilderEagerLoading` + `JoinIntoStitcher` без регрессий.

## 16. GitHub tracking

- #209 E1 strategy · #211 E3 ordering+top-N (поглощает #210) · #212 E4 nested · #213 E5 JoinInto.
- #210 закрывается со ссылкой на #211. Публикация issue ≠ апрув спеки ≠ реализация.

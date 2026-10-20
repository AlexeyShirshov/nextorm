# Дизайн: публичный раздел «Движок» (Engine) и растворение Querying в Guide

Дата: 2026-10-12. Статус: **письменная спека АПРУВНУТА** пользователем (2026-10-12). План
реализации приложен (раздел 10); продуктовая реализация этим документом **не авторизована** —
способ исполнения выбирает пользователь явно.

Tracking: GitHub issue [#220](https://github.com/AlexeyShirshov/nextorm/issues/220), милстоун
`1.0.9`. Апрув спеки не авторизует реализацию.

**Реализовано (2026-10-12, ветка `1.0.9`, без коммитов).** Создана секция `engine/**` (EN+RU:
`index`, `01-query-pipeline`, `02-execution-and-rows`, `03-plan-cache-and-reuse` + `engine/toc.yml`);
`Querying` растворён в `Guide` (`guide/30-projections`, `31-query-sources`, `32-provider-specifics`;
`guide/01–29` не тронуты); `Infrastructure` перенумерован (`01–04`); обновлены `docs/toc.yml`,
`docs/ru/toc.yml`, `docs/guide/toc.yml`, `docs/infrastructure/toc.yml`, карточки `index.md` (EN+RU) и
все входящие ссылки. Проверка: `dotnet docfx docs/docfx.json` — **0 errors**, 2 предсуществующих
warning (дубли `AnalyzerReleases.*.md` в sourcegenerator).

## 1. Намерение

Дать читателю отдельный раздел о том, «как устроен движок» на **базовом** уровне (не внутренности,
не спеки), и убрать дублирование: сейчас движковый обзор лежит в `querying/index.md`, а поведение
движка (кэш планов + `Prepare`) — в `infrastructure/01-query-reuse-and-caching.md`. Отдельная
секция `Querying` при этом растворяется в `Guide`, потому что её страницы — про использование
(проекции, источники, провайдер-специфика), а не про движок.

## 2. Текущее состояние (факты)

- Верхнеуровневые секции (`docs/toc.yml:1-22`): Home, Getting started, Guide, **Infrastructure**,
  **Querying**, Scalar functions, Providers, Comparisons, Advanced, API reference, Русская документация.
  В RU (`docs/ru/toc.yml`) порядок тот же: `Инфраструктура` (`:89`) перед `Запросы` (`:101`).
- `docs/querying/index.md:1` — `# Querying and projections`, внутри — обзор + диаграмма пайплайна
  (`:23`, `../assets/diagrams/nextorm-command-execution.svg`). Фактически это обзор движка.
- `docs/querying/01-projections.md`, `02-query-sources.md`, `03-provider-specifics.md` — страницы
  использования.
- `docs/infrastructure/01-query-reuse-and-caching.md` — 20 КБ, кэш планов + `Prepare()` (с диаграммой
  `nextorm-command-execution-prepared.svg` на `:89`). Это поведение движка.
- В `docs/index.md:14` обзор `querying/index.md` включён в карточку **Guide**, отдельной карточки
  Querying нет; у Infrastructure карточка своя (`docs/index.md:45`). RU — та же несогласованность
  (`docs/ru/index.md:14`,`:45`).
- Нумерованная серия `docs/guide/01..29` (+ `guide/provider-specific/`) в EN и RU; перенумерация
  01–29 запрещена (её трогают внешние ссылки), новые страницы добавляются в конец.
- Отдельной спеки/плана по структуре документации нет; раздела «движок» нет.

## 3. Согласованный дизайн

### 3.1 Новая top-level секция Engine / «Движок»

Размещение: сразу после `Guide`, перед `Infrastructure`. Вводный уровень.

Страницы (EN `docs/engine/**`, RU `docs/ru/engine/**`, имена файлов одинаковы):

- `engine/index.md` — **«Как работает движок»**. Карта компонентов и ответственностей
  (`EntityBuilder → QueryCommand → QueryPreparer`/expression-visitors → `SqlBuilder`/dialects →
  `QueryExecutor → DbDataReader → RowMapperFactory`), место план-кэша, обе диаграммы
  (`nextorm-command-execution.svg`, `nextorm-command-execution-prepared.svg`).
- `engine/01-query-pipeline.md` — **«Построение запроса (fluent → SQL)»**: `QueryCommand<TResult>`,
  подготовка/`QueryPreparer`, expression-visitors, `SqlBuilder` + `ISqlDialect`, параметры, ключ плана.
- `engine/02-execution-and-rows.md` — **«Исполнение и строки»**: `QueryExecutor` (`DbCommand` →
  `DbDataReader`), материализация (`RowMapperFactory`/`RowMaterializer`), терминалы: buffered /
  streamed / JSON+CSV (без материализации) / сырой reader (`ToDataReader`).
- `engine/03-plan-cache-and-reuse.md` — **«Кэш планов и переиспользование»**: перенос содержимого
  `infrastructure/01-query-reuse-and-caching.md` (кэш, `Prepare()`, scope, purge, captured-коллекции,
  провайдер-различия).

Обе диаграммы живут на `engine/index.md`; `engine/03` их **не дублирует**, а ссылается на index
(диаграмма Prepare тематически про `Prepare`, но размещается в обзоре — как согласовано).

### 3.2 Querying растворяется в Guide

- `querying/01-projections.md` → `guide/02-projections.md`
- `querying/02-query-sources.md` → `guide/01-query-sources.md`
- `querying/03-provider-specifics.md` → `guide/32-provider-specific-features.md`
  (номера 30–32 — в конец, `guide/01–29` не трогаем).
- Обзор `querying/index.md` становится `engine/index.md`; каталоги `docs/querying/` и
  `docs/ru/querying/` удаляются.
- Внутренние ссылки перенесённых страниц переписываются под новое расположение (например,
  `../guide/04-joins.md` → `04-joins.md`); RU-зеркала — синхронно.

### 3.3 Перенумерация Infrastructure

После выноса `01-query-reuse-and-caching.md` закрываем дыру:

- `02-connections.md` → `01-connections.md`
- `03-interceptors.md` → `02-interceptors.md`
- `04-value-converters.md` → `03-value-converters.md`
- `05-logging.md` → `04-logging.md`

EN и RU синхронно; `docs/infrastructure/toc.yml` (EN), `docs/ru/toc.yml` (RU) и карточка
Infrastructure в `docs/index.md`/`docs/ru/index.md` обновляются.

## 4. Операции с файлами (EN и RU)

| Действие | EN | RU |
|---|---|---|
| move | `querying/01-projections.md` → `guide/02-projections.md` | `ru/querying/01-projections.md` → `ru/guide/02-projections.md` |
| move | `querying/02-query-sources.md` → `guide/01-query-sources.md` | `ru/querying/02-query-sources.md` → `ru/guide/01-query-sources.md` |
| move | `querying/03-provider-specifics.md` → `guide/32-provider-specific-features.md` | `ru/querying/03-provider-specifics.md` → `ru/guide/32-provider-specific-features.md` |
| move | `infrastructure/01-query-reuse-and-caching.md` → `engine/03-plan-cache-and-reuse.md` | аналог в `ru/` |
| new | `engine/index.md`, `engine/01-query-pipeline.md`, `engine/02-execution-and-rows.md`, `engine/toc.yml` | `ru/engine/index.md`, `ru/engine/01…`, `ru/engine/02…` |
| rename | `infrastructure/02..05` → `01..04` | аналог в `ru/` |
| delete | `querying/` (остаётся пустым) | `ru/querying/` |

## 5. TOC и карточки index (EN+RU)

- EN `docs/toc.yml`: добавить `Engine` → `engine/` между `Guide` и `Infrastructure`; удалить `Querying`.
- RU `docs/ru/toc.yml`: добавить `Движок` (items `engine/index,01,02,03`) между `Руководство` и
  `Инфраструктура`; удалить `Запросы`; добавить `guide/30–32` в `Руководство`; перенумеровать items
  `Инфраструктура`.
- `docs/guide/toc.yml`: добавить Projections / Query sources / Provider-specific behaviour (30–32).
- `docs/index.md` и `docs/ru/index.md`: добавить карточку **Engine**; убрать строку `querying/index.md`
  из карточки Guide; обновить карточку Infrastructure (4 страницы).
- Новый `docs/engine/toc.yml` (EN-стиль секций).

## 6. Обновление ссылок

- Все входящие ссылки на перемещённые/переименованные страницы (EN+RU) — найти и переписать:
  `rg -n 'querying/|infrastructure/0[1-5]-' docs docs/ru` (исключая `docs/specs/**`, `api/`, `_site/`).
  Известное: `docs/index.md:136` (## Query reuse → `infrastructure/01-query-reuse-and-caching.md`)
  → `engine/03-plan-cache-and-reuse.md`.
- xref-ссылки на API (`xref:NextORM.Core.*`) не затронуты.
- Из публичных страниц ссылок на `docs/specs/**` быть не должно.

## 7. Вне scope

- Изменения кода (`src/**`, `tests/**`).
- Перенумерация `guide/01–29`, `scalar-functions`, `providers`, `advanced`.
- Новые внутренние спеки/разборы реализации движка.

## 8. Критерии приёмки и проверка

- `dotnet docfx docs/docfx.json` — **0 errors** (2 предсуществующих warning допустимы).
- Оба `toc.yml` (EN+RU) синхронны; карточки `index.md` (EN+RU) соответствуют TOC.
- Все внутренние ссылки разрешаются; секции `Querying`/`Запросы` как top-level нет.
- Обе диаграммы доступны из `engine/index.md` (EN+RU).
- `Guide` содержит `30-projections`, `31-query-sources`, `32-provider-specifics` (EN+RU).

## 9. Решения по открытым вопросам (приняты при апруве)

1. RU-название секции: **«Движок»**; EN — **Engine**.
2. Имя страницы: `engine/02-execution-and-rows.md`.
3. Обе диаграммы размещаются на `engine/index.md`; `engine/03` их не дублирует.
4. Заголовок `engine/03` — **«Кэш планов и переиспользование»**.
5. `engine/04-in-memory.md` — вне scope текущей задачи.

## 10. План реализации (docs-only, EN+RU; код не меняется)

**Фаза 0 — подготовка.** Работать на ветке `1.0.9`, без коммитов/push (по правилам репозитория);
сохранять CRLF в `.md`; перенумерация `guide/01–29` запрещена.

**Фаза 1 — секция Engine.**
1. `docs/engine/toc.yml` — Overview / Query pipeline / Execution and rows / Plan cache and reuse.
2. `docs/engine/index.md` — «Как работает движок»: перенести обзор из `docs/querying/index.md`
   (включая диаграмму `nextorm-command-execution.svg`), добавить карту компонентов и вторую
   диаграмму `nextorm-command-execution-prepared.svg`.
3. `docs/engine/01-query-pipeline.md` — новый текст: `QueryCommand`, `QueryPreparer`, visitors,
   `SqlBuilder`/`ISqlDialect`, параметры, ключ плана.
4. `docs/engine/02-execution-and-rows.md` — новый текст: `QueryExecutor` (`DbCommand`/`DbDataReader`),
   материализация (`RowMapperFactory`/`RowMaterializer`), терминалы (buffered/streamed/JSON+CSV/raw).
5. Переместить `docs/infrastructure/01-query-reuse-and-caching.md` → `docs/engine/03-plan-cache-and-reuse.md`:
   сменить H1, убрать диаграмму (уехала в index) и заменить на ссылку, починить относительные ссылки,
   сохранить xref и футер `Source:` (при необходимости — только пути).
6. RU-зеркала всех пунктов в `docs/ru/engine/**`.

**Фаза 2 — растворение Querying в Guide.**
7. Переместить `docs/querying/01-projections.md` → `docs/guide/02-projections.md`,
   `02-query-sources.md` → `guide/01-query-sources.md`, `03-provider-specifics.md` →
   `guide/32-provider-specific-features.md`; переписать внутренние ссылки под новое расположение.
8. Удалить `docs/querying/` и `docs/ru/querying/`.
9. RU-зеркала.

**Фаза 3 — перенумерация Infrastructure.**
10. Переименовать `docs/infrastructure/02..05` → `01..04` (EN+RU).
11. Обновить `docs/infrastructure/toc.yml`, `docs/ru/toc.yml`, карточки Infrastructure в index.

**Фаза 4 — навигация и ссылки.**
12. `docs/toc.yml`: добавить `Engine` → `engine/` между Guide и Infrastructure; удалить `Querying`.
13. `docs/ru/toc.yml`: добавить `Движок` (items `engine/index,01,02,03`); удалить `Запросы`;
    добавить `guide/30–32` в Руководство; перенумеровать items Инфраструктуры.
14. `docs/guide/toc.yml`: добавить 30–32.
15. `docs/index.md` и `docs/ru/index.md`: добавить карточку Engine; убрать `querying/index.md` из
    карточки Guide; обновить карточку Infrastructure (4 страницы).
16. Пройти по входящим ссылкам: `rg -n 'querying/|infrastructure/0[1-5]-' docs docs/ru` (кроме
    `docs/specs/**`, `api/`, `_site/`) — переписать все (EN+RU). Известное: `docs/index.md:136`.

**Фаза 5 — проверка.**
17. `dotnet docfx docs/docfx.json` → **0 errors**.
18. Аудит: нет битых внутренних ссылок; `Querying`/`Запросы` отсутствуют как top-level; в Guide
    есть 30–32; обе диаграммы открываются из `engine/index.md`; из публичных страниц нет ссылок на
    `docs/specs/**`.

**Оценка объёма:** правки ~30 файлов .md (EN+RU) + 2 `toc.yml` + 3 секционных `toc.yml`.

**Способ исполнения** (выбирает пользователь): обычная реализация по этому плану либо
PDCA-цикл (`pdca-dotnet`).


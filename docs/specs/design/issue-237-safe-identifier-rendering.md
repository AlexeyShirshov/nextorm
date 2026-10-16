# #237 — Обязательное безопасное экранирование имён `Column<T>` и SQL Server PIVOT/UNPIVOT

- **Дата:** 2026-10-16
- **GitHub issue:** [#237](https://github.com/AlexeyShirshov/nextorm/issues/237) — *Identifier safety: SQL Server bracket escaping and raw UNPIVOT/Column<T> names* (OPEN)
- **Milestone (актуальный на момент записи):** `1.0.9-rc3` (GitHub milestone #4)
- **Статус:** направление и scope согласованы; письменная спецификация — **APPROVED** (2026-10-16, подтверждено пользователем); builder-check: важного не осталось; implementation plan ещё не создан и не approved; реализация **не authorized**. Scope не меняется.

## 1. Контекст и происхождение

- #232 (T232, SQL Server typed PIVOT/UNPIVOT) при r=4 security-потоком поднял два **High** identifier-safety findings на **существующем, замороженном** коде движка. Оба sink лежат вне change footprint T232 и входят в его frozen/STOP-список, поэтому в T232 они **не исправлены** и записаны там как **accepted risk, not a blocker**. Remedy не был согласован.
- #237 — **отдельный новый scope** поверх этих findings, а не рераунд T232: переиспользуем факты из #232, но remedy и его контракт формулируются здесь заново.
- #235 (approved design) закрыт на уровне approved design и **в этой работе не меняется**; наличие утверждённого дизайна #235 **не означает**, что его новые правила уже развёрнуты в коде.

Два исходных finding (сохраняются как есть, без переинтерпретации):

- **High #1 — SQL Server bracket escaping не удваивает встроенный `]`.** `SqlServerDialect.Escape` оборачивает имя в скобки, но не удваивает встроенный `]` (в отличие от корректного `QuoteIdentifier`). Объявленный PIVOT value string (runtime identifier из typed projection) доходит до этого sink в `SqlServerPivotRenderer.RenderPivot` и через typed `Pivot` alias formatting. Вход `1], [2` рендерится как `[1], [2]` — два идентификатора вместо одного escaped.
- **High #2 — сырые UNPIVOT role names / `Column<T>` имена рендерятся без кавычек.** Typed `Unpivot` извлекает два role-идентификатора через identity `formatValueName` (`static name => name`), поэтому runtime role-строки не экранируются; `BaseExpressionVisitor.AppendIdentifier` при `_options.QuoteIdentifiers == false` добавляет имя **raw**. Затронутые публичные поверхности: `UnpivotValues.Name<T>(string)`, `UnpivotValues.Value<T>(string)`, `SqlFunctions.Column<T>(object, string)`. Строка вида `0+(SELECT COUNT(*) FROM sys.tables)` попадает в проекцию как исполняемое выражение вместо идентификатора.

## 2. Intent

Убрать выход identifier-строк в SQL как raw fragments и обеспечить корректное имя колонки с одинаковой безопасностью при `QuoteIdentifiers` `false` и `true`. Дополнительно устранить двойное quoting в одном name pipeline: existing typed `Pivot` предварительно делает `Escape` имени, после чего quoted mode может `QuoteIdentifier` его ещё раз. Проблему двойного quoting снимаем **в этом же** name pipeline.

## 3. Публичный контракт (решения пользователя)

Строки этих APIs — **исходные неэкранированные имена** (а `PivotValue.Create` сохраняет семантику **data value**, т.е. исходного pivot data значения), а **не** raw SQL. Они подлежат **обязательному безопасному provider quoting независимо от общего `QuoteIdentifiers`**. Manual prequoting НЕ поддерживается как особый синтаксис: никаких heuristic «если похоже на `[x]` — значит уже quoted» и никакого idempotent `QuoteIdentifier`; literal имя с brackets остаётся возможным и отличается от имени без brackets. Rewrite тестов и docs — approved.

### 3.1 `Column<T>(entity, databaseColumnName)`

- Адресует **одну** колонку; source/alias задаётся аргументом `entity`.
- Строка с точкой — **literal имя с точкой**, НЕ qualified path; не split.
- Строка с SQL-like пунктуацией — тоже **identifier**, а не исполняемый SQL.
- Legacy `'[1]'` теперь буквальное имя из трёх символов; для колонки с именем `1` пишем `'1'`.
- Изменение SQL shape и прежнего prequoted usage — **намеренная breaking correction безопасности**.

### 3.2 `PivotValue.Create(string)` и typed `pivot.Value<T>(value)`

- Сохраняют существующее **значение данных** и выводят derived identifier безопасно.
- В частности `'1'` => `[1]`, `'a]b'` => `[a]]b]` (SQL Server).
- Typed `.Value('[1]')` **не должен** молча интерпретироваться как `.Value('1')`.
- Не менять data values, не trim/strip пунктуацию и не преобразовывать типы.

### 3.3 UNPIVOT

- `UnpivotColumn.Create` — исходные имена колонок.
- `UnpivotValues.Name`/`UnpivotValues.Value` — исходные имена ролей.
- Declaration и projection references экранируются одинаково и **ровно один раз**.

### 3.4 Общий флаг и рамки

- Общий флаг `QuoteIdentifiers` продолжает управлять остальной генерацией.
- Только перечисленные identifier APIs всегда safe-quoted; **нельзя** повсеместно переключить контекст в quoted и назвать это fix.
- Existing null/nonconstant/unbound-source/in-memory validation контракты сохраняются. Нового broad allowlist имён/валидатора grammar, автоматического parsing raw expressions и нового raw-expression API не вводить. Тестировать существующие validation boundaries; конкретные защиты invalid inputs попутно не менять.
- SQL-like **корректное** имя не reject из-за пунктуации: оно quoted. Длины/invalid provider-specific database names остаются прежним контрактом; fix escaping **не обещает**, что любой input name существует/допустим на сервере.

## 4. Scope и архитектурные решения (primary calls)

Это **primary calls автора спеки**, а не дополнительные пользовательские утверждения.

1. **SQL Server `Escape`.** `SqlServerDialect.Escape` правильно удваивает встроенный `]`, сохраняя корректный output для обычных имён. Сигнатуру `ISqlDialect`/`Escape` не меняем. Это исправляет существующих SQL Server renderer-consumers (aliases / mapped refs / PIVOT / UNPIVOT) — их regression footprint учесть. **НЕ** включаем исправление `Escape` всех остальных диалектов: известные аналогичные concerns вне #237, только отметить в handoff как отдельный risk, без утверждения, что они устранены.
2. **Core `Column<T>` и marker name emission.** Используют provider `QuoteIdentifier` независимо от общей опции; это общий API, поведение применимо ко всем SQL-провайдерам (SQL Server, SQLite, PostgreSQL, MySQL/MariaDB, ClickHouse). Не менять существующие delimiter policies / `QuoteIdentifier` других провайдеров ради одного SQL Server fix. **Always-quote относится к обоим sink:** явный `SqlFunctions.Column<T>` через `AppendColumnReference` **и** прямой `NormSqlTranslator.TryTranslateMarker` для `PivotValues.Value` / `UnpivotValues.Name` / `UnpivotValues.Value`, независимо от того, успел ли projection rewriter заменить маркер. Ни один marker-route не остаётся raw или option-gated; broad change unrelated `AppendIdentifier` callers — по-прежнему нет (§4.4). Точный internal helper/overload — implementation plan.
3. **Single quoting = ровно одна escaping-стадия на каждое emitted occurrence.** Исходное имя **не** prequote; каждый emitted identifier occurrence (declaration или reference) проходит ровно **одну** escaping-стадию — это НЕ требование «единственная строка кода» для всех declaration/reference. Декларации PIVOT/UNPIVOT разрешено оставить на SQL Server `Escape`, а ссылки — на `QuoteIdentifier`, **если** `Escape` исправлен (§4.1) и оба дают одинаковую безопасную bracket semantics. Shared implementation / делегирование `Escape` → `QuoteIdentifier` допустимо по выбору плана, но менять все renderer callsites ради единственного примитива не требуется. Тест обоих modes доказывает отсутствие двойного quoting. Никакого idempotent/unquote heuristic, никакого разбора user brackets как SQL syntax. Assertion: declaration и reference используют **один и тот же raw input** и дают **одну и ту же безопасную репрезентацию** на каждое имя. Детальная декомпозиция helper/overload — планировщику; C# symbol impact — **только Roslyn**.
4. **Не менять глобально всех `BaseExpressionVisitor.AppendIdentifier` callers.** Legacy `TableAlias.Get*` string accessors, `WithTableName`, CTE/alias general raw paths **не** получают новый контракт в #237. В affected PIVOT docs/tests, где нужен безопасный доступ к колонке, заменить `GetNullableDecimal('[1]')` на существующий `Column<decimal?>(t, '1')` вместо изменения всей legacy-семьи. Явно: security claims ограничены перечисленными APIs, **не** «в ORM больше нет raw identifiers».
5. **Ничего смежного не трогаем.** Не менять fluent API signatures/names, gate `Pivot` только SQL Server, metadata/config quoting options, plan caching semantics. Результаты SQL/специмены и typed/untyped варианты должны оставаться согласованными; проверка повторной подготовки/разных имён не должна reuse неверный cached identifier.
6. **Injected grouping `Column<T>`.** `Column<T>`, внедрённый `PivotProjectionRewriter` для обычных grouping/source members, тоже следует always-quoted контракту. Изменение SQL shape этих PIVOT/UNPIVOT grouping refs в unquoted mode — намеренное и допустимое; affected snapshots обновляем. «Ordinary snapshots unchanged» означает unrelated queries/paths **вне** safety fix, а не обязанность сохранить raw refs внутри изменённого typed pipeline. Отдельный unsafe helper для grouping только ради старых golden strings не нужен.

## 5. Совместимость и миграция

- `Column(t, '[1]')` → `Column(t, '1')` для ссылки на имя `1`; literal database-имя `[1]` передавать как `'[1]'`, ожидаемый SQL на SQL Server — `[[1]]]`.
- При выключенном quoted mode `Column(t, 'name')` теперь emitted как `t.[name]` на SQL Server / `t."name"` на PostgreSQL/SQLite / backticks на MySQL/ClickHouse (source alias syntax не переопределять). Ровно один раз quoting, **без** bracket stripping.
- Public docs EN+RU (guides provider-specific sqlserver, projections, XML docs и другие affected examples) обновляются в той же будущей реализации.
- Старые тесты **нельзя** «починить» включением global `QuoteIdentifiers` или заранее escaping inputs.

## 6. Acceptance / test matrix (для ПЛАНА; сейчас ничего не писать и не запускать)

- **SQL Server `Escape`:** embedded `]`, repeated `]`, ordinary names; ordinary **unrelated** SQL snapshots не меняются, а намеренно меняется безопасная `Column`/marker shape, включая injected grouping refs изменённого typed pipeline (§4.6).
- **Explicit `Column` на каждом SQL-провайдере в обоих quoting modes:** simple name, numeric name, whitespace/Unicode/reserved word, delimiter/backslash по провайдеру, dot как одно имя, SQL-looking пунктуация остаётся внутри одного quoted identifier. Синтаксис валидировать provider-correct oracle, **не** assert-ить лишь substrings или отсутствие `;`.
- **SQL Server typed/untyped PIVOT/UNPIVOT:** declaration и projection names со спецсимволами; оба modes и контекстный default + query override там, где supported. Нет double escaping. Обновить legacy prequoted тестовые входы. Assertion: declaration и reference каждого имени идут от одного raw input и дают одну и ту же безопасную репрезентацию.
- **Согласованность и регрессии:** покрыты оба marker-route — explicit `Column<T>` через `AppendColumnReference` и direct `NormSqlTranslator.TryTranslateMarker` при ещё не заменённом rewriter'ом маркере; injected grouping `Column<T>` (§4.6) covered в unquoted mode; normal SQL/query results согласованы; existing invalid source / nonconstant marker / in-memory rejection сохраняются; повторная preparation и разные имена не склеиваются неверно в cache.
- **Regression scope SQL Server `Escape`-consumers:** aliases / mapped refs / update-delete / source locks, включая actual Roslyn impact, а не только PIVOT golden string.
- **Документация EN/RU/XML** согласована с новым raw-name контрактом; **no public links to docs/specs**.
- **Unit SQL-generation tests** — без БД. **Integration evidence обязательно** для affected provider behavior: SQLite плюс контейнерные SQL Server/PostgreSQL/MySQL/ClickHouse с корректным `DOCKER_HOST` согласно project running-integration-tests instructions; skipped provider — **не** green. MariaDB покрытие — reuse MySQL dialect + собственные no-db tests, real separate DB не выдумывать. План определяет targeted integration cases и не запускает их после каждой правки; полная обязательная evidence — на DO→CHECK.

## 7. Constraints / non-goals

- No full identifier architecture refactor.
- No new raw SQL API.
- No auto unquote / qualified path parser.
- No unrelated provider `Escape` corrections.
- No auto-close old issues.
- No product implementation until written spec approval; затем — separate plan approval.
- В этой работе — **только spec + GitHub tracking** (дизайн и tracking, без реализации).

## 8. Handoff / next

- Письменный дизайн **APPROVED** (2026-10-16); builder-check не оставил важного. Далее — implementation plan отдельным шагом (ещё не создан и не approved), способ исполнения выбирает пользователь; реализация пока не authorized.
- Открытых пользовательских выборов **сейчас нет** (builder может найти новые).
- Известный смежный risk: аналогичные `Escape`-concerns других диалектов остаются открытыми и **не** утверждаются как устранённые (см. §4.1).

## 9. Source refs (указатели, не цитаты)

Пути индикативны; для любых C# symbol/overload/using вопросов — только Roslyn, не текстовый поиск. Где путь ненадёжен — оставлено имя символа.

- `src/nextorm.sqlserver/SqlServerDialect.cs:168`, `:171`, `:846`
- `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs:148`, `:199`
- `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:335-341`, `:464-475`
- `NormSqlTranslator.cs:83-118`, `:191-201` (корректный текущий путь при необходимости установить через Roslyn)
- `Query/PivotProjectionRewriter.cs:339-354` (символ `PivotProjectionRewriter.ExtractUnpivotRoles`)
- Guides: projections + provider-specific sqlserver, EN и RU

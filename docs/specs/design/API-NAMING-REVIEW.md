# Аудит публичного API nextorm: именования

**Дата:** 2026-09-17; **актуализация 18.09.2026 по HEAD `d21c473`** (Build Release `0/0`);
**P0-переименования выполнены 18.09.2026** (Build Debug `0/0`, тесты: core 151, sqlite 180, sqlserver 166,
postgres 150, mysql 31, mariadb 7, clickhouse 47, integration 833/0 failed)

**Предрелизный аудит v1.0.3-alpha (21.09.2026, HEAD `2a2dfa6`, рабочее дерево чистое = `origin/1.0.3-alpha`): открытых P0/P1 по публичному API нет.** QM1 (переименование `Tablesample`→`TableSample`) и AR2 (пробел в док-описании ClickHouse-массивов) фактически закрыты в коде/доках; XP2 закрыт ранее — статусы отмечены в этом проходе. Шаг 5 (заморозка `PublicAPI.Shipped/Unshipped.txt`) остаётся открытым **P2** (RD2 и производные, трекинг — issue #53) и alpha-релиз не блокирует.

**Предрелизный аудит v1.0.4-alpha (22.09.2026, HEAD `91379b1` + uncommitted working tree, ~193 файла).** **Открытых P0/P1 нет.** P0 по именам — нет; P1 по именам — нет. Три P1-**доковых** находки прошлых срезов закрыты в текущем дереве вместе с док-кампанией: **CHJ1** (проза «`SEMI`/`ANTI`/`PASTE` не поддерживаются» заменена описанием `SemiJoin`/`AntiJoin`/`PasteJoin` в `docs/providers/clickhouse.md` EN+RU, `docs/guide/03-joins.md` EN+RU §«SEMI / ANTI / PASTE joins», `advanced/api-reference` EN+RU; gap-analysis §4 п.8 → «shipped»; `todo_clickhouse_join_strictness.md` удалён), **CHCB1** (by-name-доступ `SqlFunctions.Column<T>` описан в `guide/01-querying-and-projections.md` EN+RU §«Columns by name»/«Колонки по имени», `providers/clickhouse` EN+RU, `advanced/api-reference` EN+RU; gap-analysis §4 п.10 → «shipped»; `todo_clickhouse_columns_by_name.md` удалён) и **CHARR2** (сняты оговорки «массив нельзя материализовать»/«только вложенно»: `advanced/limitations` EN+RU, `providers/clickhouse` EN+RU, `guide/provider-specific/clickhouse` EN+RU). Также закрыты P2-док/XML: **CHARR3**, **CHARP1**, **CHARP2**, **CHTUP2**, **CHJS2**, **CHJ3**, а вместе с ними все более ранние P2-находки класса «публичный член без XML-`<summary>`» (**N2**, **U5**, **Q4**, **A2**, **D2**, **S2**, **S3**, **LIM2**, **IF1**, **IF2**, **AJ3**, **RD1**, **XP4** и аналогичные) — кампания задокументировала все 1283 публичных члена, `CS1591` теперь ошибка сборки. **XML-документация публичного API — 100 %** (`CS1591` убран из `<NoWarn>` всех 7 библиотечных `.csproj`, задокументированы 1283 члена, Приложение A закрыто) — переименований и изменений поведения нет. Открытыми остаются только **P2**: трекинг `PublicAPI.*.txt` (**Шаг 5**, issue #53; продолжения RD2/AR1/CHJ2/CHCB2/CHARR1/HOAF1/CHQA4/CHARP3/CHTUP1/CHJS1/CHDH1 и др.), принятые с обоснованием **CHQA1** (source-break `IQuantileAggregateRenderer` — pre-1.0 alpha) и **CHJ4** (политика DIM vs abstract), и **CHCB3** (форма/устойчивость `object entity`; кодовая сторона — `code-smells-review.md`, Находка 73). Детали — в таблицах ниже; счётчики подавлений/слопа — `code-smells-review.md` (предрелизный аудит v1.0.4-alpha).

**Обновление 22.09.2026 (clickhouse-json-type, влито в дерево).** **J8 (P1) закрыт** и **J10 закрыт**: `json_all_paths_with_types` переведён на `Dictionary<string,string>` (+ ветка `GetValue` в `SelectExpression.GetDataRecordMethod`), материализация подтверждена контейнерным интеграционным тестом; доки синхронизированы (нативный `JSON`-аргумент, прямая проекция `string[]`/`Dictionary`). Открыт только J9 (трекинг `PublicAPI.Unshipped.txt` при заморозке, Шаг 5). Детали — в разделе «ClickHouse нативные JSON-функции …».

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse row reader `Array(T)`/`Tuple`).** Добавлены два публичных агрегата `ClickHouseFunctions.group_array<T>`/`group_uniq_array<T>` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:127-143`); row reader (`SelectExpression.GetDataRecordMethod`) и классификация проекции (`TypeFacts`) — `internal`/без новых подписей. Новых публичных типов нет → Приложение A (45) без изменений, покрытие методов +2 (215/1094 против baseline 213/1092). P0/P1 по **именам** нет; открыты CHARR1 (трекинг `PublicAPI`, Шаг 5), CHARR2 (P1-док: EN/RU утверждают «массив нельзя материализовать»/«только вложенно»), CHARR3 (док-пробел). Подробности — в разделе «ClickHouse row reader `Array(T)`/`Tuple` и агрегаты `group_array`/`group_uniq_array`».

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse higher-order/lambda array-функции).** Добавлены `ISqlDialect.SupportsHigherOrderArrayFunctions` (+база/override) и 9 публичных методов `ClickHouseFunctions.array_map`/`array_filter`/`array_exists`/`array_all`/`array_count`/`array_first`/`array_first_index`/`array_last`/`array_last_index` (`Query/SqlFunctions.ClickHouse.cs:485-545`); новых публичных типов нет → Приложение A (45) без изменений, покрытие методов +9 (арифметически, 224/1103). P0 по именам нет; **HOAF2 (P1 док) и HOAF3 (P2 док) закрыты**; открыт P2 **HOAF1** (трекинг, Шаг 5). Подробности — в разделе «Аудит 22.09.2026 — ClickHouse higher-order (lambda) array-функции».

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse параметризованные array-агрегаты `topK`/`topKWeighted`/`quantiles`).** Добавлены новый публичный тип `ITopKAggregateRenderer` (+ DIM `ISqlDialect.TopKAggregates`), абстрактный член `IQuantileAggregateRenderer.RenderLevels` (переименован из `RenderArray`) и 3 метода `ClickHouseFunctions.quantiles`/`top_k`/`top_k_weighted` (`Query/SqlFunctions.ClickHouse.cs:95-118`). Новый публичный тип задокументирован → Приложение A (45) без изменений, общее число публичных типов +1, покрытие методов +3 (арифметически, 227/1106). P0/P1 по **именам** нет; **CHQA2 применён** (`RenderArray`→`RenderLevels`), **CHQA3 закрыт** (доки EN+RU/gap-analysis); открыты P2 **CHQA1** (source-break внешних реализаторов `IQuantileAggregateRenderer`; принято — pre-1.0 alpha, единственный in-repo реализатор) и **CHQA4** (трекинг `PublicAPI`, Шаг 5). Подробности — в разделе «Аудит 22.09.2026 — ClickHouse параметризованные array-агрегаты `topK`/`topKWeighted`/`quantiles`».

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse скалярная поверхность над `Tuple` `tuple`/`tupleElement`, срез 5 `todo_clickhouse_arrays.md`).** Новый публичный член `ISqlDialect.SupportsTupleFunctions` (DIM `=> false`) + `SqlDialectBase`/`ClickHouseDialect` override; новых публичных типов и DSL-методов нет → Приложение A (45) без изменений. P0/P1 по **именам** нет; открыты P2 **CHTUP1** (трекинг `PublicAPI`, Шаг 5) и **CHTUP2** (доки EN+RU/gap-analysis). Подробности — в разделе «Аудит 22.09.2026 — ClickHouse скалярная поверхность над `Tuple` (`tuple`/`tupleElement`)».

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse array-возвращающие JSON-функции `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`, срез 6 `todo_clickhouse_arrays.md`).** Добавлены 6 публичных методов `ClickHouseFunctions.json_extract_keys`/`json_extract_array_raw`/`json_extract_keys_and_values<T>` (`Query/SqlFunctions.ClickHouse.cs:194-224`); новых публичных типов и членов `ISqlDialect` нет (переиспользуется `SupportsJsonExtract`/`MakeJsonExtract`) → Приложение A (45) без изменений, покрытие методов +6 (арифметически, **236/1115**). P0/P1 по **именам** нет; открыты P2 **CHJS1** (трекинг `PublicAPI`, Шаг 5), **CHJS2** (доки EN+RU/gap-analysis не обновлены) и **CHJS3** (nullable `T`-контракт; кодовая сторона — `code-smells-review.md`, Находка 66). Подробности — в разделе «Аудит 22.09.2026 — ClickHouse array-возвращающие JSON-функции …».

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse иерархические dictionary-функции `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`, срез 7 `todo_clickhouse_arrays.md`).** Добавлены 3 публичных метода `ClickHouseFunctions.dict_get_hierarchy<TKey>`/`dict_get_children<TKey>` (→ `ulong[]`) и `dict_is_in<TKey>` (→ `bool`) (`Query/SqlFunctions.ClickHouse.cs:316,323,331`); новых публичных типов и членов `ISqlDialect` нет (переиспользуется `SupportsDictionaries`/`MakeDictionaryFunction`) → Приложение A (45) без изменений, покрытие методов +3 (арифметически, **239/1118**). P0/P1 по **именам** нет; открыт P2 **CHDH1** (трекинг `PublicAPI`, Шаг 5); **CHDH2** (доки EN+RU/gap-analysis) и **CHDH3** (устаревшие `<summary>` гейта `SupportsDictionaries`/override) **закрыты 22.09.2026**. Подробности — в разделе «Аудит 22.09.2026 — ClickHouse иерархические dictionary-функции …».

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse `SEMI`/`ANTI`/`PASTE` joins).** Добавлены `JoinType.Semi`/`Anti`/`Paste` (`src/nextorm.core/Expressions/JoinExpression.cs:28,33,39`), DIM-гейты `ISqlDialect.SupportsSemiAntiJoin`/`SupportsPasteJoin` (`:71,78`) + `SqlDialectBase`/`ClickHouseDialect` override, рендер `left semi`/`left anti`/`paste join` (`ClickHouseDialect.MakeJoinKeyword:92`) и 30 публичных DSL-методов `SemiJoin`/`AntiJoin`/`PasteJoin` (`EntityBuilder`, non-generic `EntityBuilder`, `JoinedEntityBuilder<T1..T7>`); новых публичных **типов** нет → Приложение A (45) без изменений, покрытие методов +30 (арифметически, **269/1148**). P0/P1 по **именам** нет; открыт **P1 доковый CHJ1** (`docs/providers/clickhouse.md:108` + RU `:109`, guide/api-reference и gap-analysis утверждают «`SEMI`/`ANTI`/`PASTE` не поддерживаются»), P2 **CHJ2** (трекинг `PublicAPI`, Шаг 5), **CHJ3** (устаревшие XML-summary `MakeJoinKeyword` + 2 члена `SqlDialectBase` без XML) и **CHJ4** (DIM vs abstract политика). Подробности — в разделе «Аудит 22.09.2026 — ClickHouse `SEMI`/`ANTI`/`PASTE` joins».

**Обновление 22.09.2026 (uncommitted worktree — доступ к колонкам mapped-сущностей по имени `SqlFunctions.Column<T>`).** Добавлен один публичный член `SqlFunctions.Column<T>(object entity, string columnName) -> T` (`src/nextorm.core/Query/SqlFunctions.cs:75`), полностью задокументирован (summary/typeparam/param/remarks, без `<returns>`); новых публичных **типов** нет → Приложение A (45) без изменений, покрытие методов +1 (арифметически, **270/1149**). P0/P1 по **именам** нет (`Column<T>` = `Parameter<T>`-паттерн + `EF.Property<T>`); открыт **P1 доковый CHCB1** (gap-analysis §4 п.10 и roadmap утверждают отсутствие by-name-доступа; guide/providers/api-reference EN+RU не обновлены), P2 **CHCB2** (трекинг `PublicAPI`, Шаг 5) и **CHCB3** (форма `object entity` без валидации источника; кодовая сторона — `code-smells-review.md`, Находка 73). Подробности — в разделе «Аудит 22.09.2026 — доступ к колонкам mapped-сущностей по имени».

**Обновление 22.09.2026 (полная XML-документация публичного API).** `CS1591` убран из `<NoWarn>` всех 7 библиотечных `.csproj` — отсутствие XML-документации у любого публичного типа/члена (метод, конструктор, свойство, поле, событие) теперь является ошибкой сборки (`TreatWarningsAsErrors=true`). Задокументированы все **1283** ранее недокументированных публичных члена (`nextorm.core` — 1056, провайдеры — 227; крупнейшие: `SqlDialectBase` 116, `EntityBuilderExtensions` 89, `QueryCommand<TResult>` 69, `JoinedEntityBuilder` 65, `EntityBuilder` 60). Покрытие XML-документацией доведено до **100 %** (исторический baseline 18.09.2026 — таблица в разделе 2). Build Debug `0 warnings / 0 errors`; unit-тесты: core 198, sqlite 273, sqlserver 246, postgres 273, mysql 71, mariadb 31, clickhouse 247 — все зелёные. Правки — только `///`-комментарии и `NoWarn`; переименований и изменений поведения нет. Шаг 3 [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) закрыт.

**Обновление 23.09.2026 (uncommitted worktree — CTAS `CREATE [TEMPORARY] TABLE ... AS SELECT`, `ToTempTable`/`ToTable`).** Новые **4** публичных типа: `TempTableExtensions` (`src/nextorm.core/Builders/TempTableExtensions.cs:20`, **12** публичных extension-терминалов), `TempTableOptions` (`Builders/TempTableOptions.cs:22`, 4 init-свойства), `TempTableOnCommit` (`:7`, 3 значения), `CreateTableAsClause` (`Query/CreateTableAsClause.cs:15`, 6 позиционных параметров); **4** DIM `ISqlDialect` (`SupportsCreateTableAsSelect`/`…ColumnList`/`…OnCommit`/`…WithNoData`, `Dialect/ISqlDialect.cs:1395,1402,1408,1415`) + `MakeCreateTableAsSelect` (`:1428`), `SqlDialectBase`-двойники (`:867-901`) и override'ы PG (`PostgresDialect.cs:397-441`), SQLite (`SqliteDialect.cs:243`), MySQL/MariaDB (`MySqlDialect.cs:401,404`). P0/P1 по **именам** нет. Открыты P2: **CTAS1** (`TempTableOptions` принимает и постоянный `ToTable`), **CTAS2** (диагностические `ToTempTableSql`/`ToTableSql` расходятся с `ToSql()`-конвенцией), **CTAS3** (трекинг `PublicAPI.*.txt`, Шаг 5), **CTAS4** (`TempTableOnCommit` дублирует `OnCommit`). Публичные доки EN+RU (guide 22 + toc + api-reference + providers/overview + limitations) обновлены, XML-doc 100 %. Кодовая сторона — `code-smells-review.md`, Находки 126–128 (🔴 126 — невалидный порядок `WITH` для CTE-источника). **Применено (23.09.2026, в том же worktree):** 🔴 126 исправлена — `WITH` теперь внутри тела (`DataContext.BuildCreateTableAsSql` склеивает `withSql + sourceSql` до `MakeCreateTableAsSelect`); **CTAS1 исполнена** — `TempTableOptions` переименован в `CreateTableAsOptions` (файл `Builders/CreateTableAsOptions.cs`, публичные доки EN+RU обновлены); 127 решена — DRY-швы `MakeCreateTableAsHead`/`MakeCreateTableAsTail` в `SqlDialectBase`, PG переопределяет только их. CTAS2–CTAS4 остаются открытыми.

**Область:** `src/nextorm.core`, `src/nextorm.postgres`, `src/nextorm.sqlite`, `src/nextorm.sqlserver`, `src/nextorm.mysql`, `src/nextorm.mariadb`, `src/nextorm.clickhouse`, `src/nextorm.core.sourcegenerator`
**Методика:** скилл `api-design` (Framework Design Guidelines) + `dotnet-xml-docs` (XML-документация). Основание для вывода — XML-комментарий (`<summary>`/`<param>`, если есть) либо тело метода/свойства. Проект в стадии **alpha**: обратная совместимость не поддерживается, имена меняются напрямую.

> **Примечание (20.09.2026):** ссылки на удалённые рабочие отчёты в этом журнале — историческое свидетельство аудита; их выводы перенесены в документацию.

## 1. Как читать отчёт

Приоритеты:

| Уровень | Значение |
|---------|----------|
| **P0** | Имя вводит в заблуждение или конфликтует с BCL; ошибка проектирования |
| **P1** | Нарушение .NET-конвенций именования (тип/член) |
| **P2** | Несогласованность / косметика / загрязнение публичной поверхности |

**Alpha-политика:** обратная совместимость не сохраняется. Старое имя заменяется новым **на месте** — в коде, тестах, примерах и документации. Ничего не помечаем `[Obsolete]`, не заводим алиасов, дублирующих членов и type forwarders. Поверхность замораживается только к релизу 1.0.

## 2. Покрытие XML-документацией (исторический baseline 18.09.2026)

Замер отражением по собранным сборкам (`ExportedTypes` + `GetMembers`) и сгенерированным `.xml`-файлам, HEAD `d21c473`:

| Категория | Документировано | Всего | Доля |
|-----------|----------------:|------:|-----:|
| Публичные типы | 113 | 158 | 72 % |
| Публичные методы | 213 | 1092 | 20 % |
| Публичные конструкторы | 8 | 130 | 6 % |
| Публичные свойства | 97 | 347 | 28 % |
| Публичные поля | 18 | 84 | 21 % |
| Публичные события | 0 | 1 | 0 % |

Область — 7 библиотечных сборок (`nextorm.core` + 6 провайдеров). Единственный публичный тип `nextorm.core.sourcegenerator` (`AnonymousClassEqualityComparer`) задокументирован; сам проект `GenerateDocumentationFile` не включает. Прошлый baseline (33/142 типа, 56/742 метода, 34/208 свойств) устарел — с тех пор XML-доки массово добавлены. `GenerateDocumentationFile=true` включён в 7 проектах, но `NoWarn=CS1591` скрывает пропуски — NuGet-потребители не видят IntelliSense-доков. **Недокументированных публичных типов — 45** (полный список в приложении A).

> **Обновление 22.09.2026:** покрытие доведено до **100 %**. Задокументированы все 1283 публичных
> члена; `CS1591` убран из `<NoWarn>` во всех 7 библиотечных `.csproj` и теперь является ошибкой
> сборки. Таблица и Приложение A выше — исторический замер от 18.09.2026.

### Документация `greatest`/`least` после включения SQLite (актуализация 19.09.2026)

Включение `SupportsGreatestLeast` для SQLite (`src/nextorm.sqlite/SqliteDialect.cs:31`) — поведенческое
изменение без переименований; контракт `CommonFunctions.greatest`/`least` не менялся (добавлены лишь
`public override`-члены `MakeGreatest`/`MakeLeast`, которые при заморозке поверхности в Шаге 5 попадут
в `PublicAPI.Unshipped.txt`).

Пробелы документации, которые закрываются в том же изменении (AGENTS.md: `docs/**` и `docs/ru/**`):

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `src/nextorm.core/Query/SqlFunctions.cs:182–194` (XML) | NULL-семантика описана только для PostgreSQL и MySQL/MariaDB | добавить SQLite: скалярные `max`/`min` возвращают `NULL`, если любой аргумент `NULL` |
| `docs/advanced/limitations.md:31`, `docs/ru/advanced/limitations.md:31` | «SQLite … rejects them with `NotSupportedException`» | SQLite поддерживает; описать NULL-семантику |
| `docs/guide/11-scalar-functions.md:675`, `docs/ru/guide/11-scalar-functions.md:691` | матрица: SQLite — `NotSupportedException` | `max(a, b)` / `min(a, b)`, одноаргументная форма — `(a)` |
| `docs/guide/11-scalar-functions.md:476`, `docs/ru/guide/11-scalar-functions.md:485` | список провайдеров без SQLite | добавить SQLite |
| — | `[ ]` | `[x]` |

NULL-семантика — **не единственный** пробел: прозаическая документация прямо противоречит новому поведению.

### Текст-JSON MySQL/MariaDB (точечный аудит 19.09.2026)

Изменение включает текстовую JSON-поверхность (`json_value`/`json_query`/`json_modify`/`isjson`) для
MySQL/MariaDB: новый публичный член `ISqlDialect.MakeTextJsonFunction(string, IReadOnlyList<string>)`
(базовая реализация в `SqlDialectBase` делегирует старому `MakeTextJsonFunction(string)`),
`MySqlDialect.SupportsTextJson => true` + переопределения `MakeTextJsonFunction`/`MakeIsJson`;
`MariaDbDialect` наследует MySQL. Публичных переименований нет, `docs/**` и `docs/ru/**` обновлены.
Build Release — 0/0.

**Extend-only?** Для потребителя изменение аддитивно (добавлена перегрузка, старая сохранена). Для
реализаторов `ISqlDialect` добавление абстрактного члена — source-breaking, но в репозитории
`ISqlDialect` реализует только `SqlDialectBase` (все диалекты наследуют его), поэтому фактического
разрыва нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| A | P2 | `ISqlDialect.cs:286`, `SqlDialectBase.cs:317` | 1-аргументный `MakeTextJsonFunction(string name)` сохранён, но **ни один** диалект его не переопределяет; единственный вызов — базовая реализация новой перегрузки (`SqlDialectBase.cs:319-320`). Дублирующий публичный член против alpha-политики «без дублирующих членов» | Удалить 1-аргументный член; базовая перегрузка пишет `$"{name}({string.Join(", ", args)})"` |
| B | P2 | `ISqlDialect.cs:124-129`, `Visitors/TextJsonSqlTranslator.cs:9,43`, `Query/SqlFunctions.cs:32-39`, `Query/SqlFunctions.SqlServer.cs:7-9` | XML-доки устарели: «SQL Server opts in», «only provider … is SQL Server», «SQL Server-only surface»; в `SupportsTextJson` не упомянут `isjson` | Добавить MySQL/MariaDB, убрать «only», включить `isjson` |
| C | P2 | `ISqlDialect.cs:295` | Новый член публичного интерфейса. Шаг 5 (заморозка) открыт: `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён | При заморозке внести в `PublicAPI.Unshipped.txt`; альтернатива — default interface method |

Пробелы документации EN/RU, видимые по этому изменению:

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/11-scalar-functions.md:457-484`, `docs/ru/guide/11-scalar-functions.md:466-493` | Заголовок «JSON as text (SQL Server, MySQL/MariaDB)», но пример (`:476`/`:485`) и таблица (`:481-484`/`:490-493`) дают только SQL Server-рендер (`json_value(...)`) | Показать MySQL-рендер (`json_unquote(json_extract(...))`, `json_extract(...)`, `json_set(...)`, `json_valid(...)`) либо сослаться на `docs/guide/18-json.md:89-115` |
| `docs/advanced/limitations.md:30`, `docs/ru/advanced/limitations.md:30` | Перечислены `json_value`/`json_query`/`json_modify` без `isjson` | Добавить `isjson` (в строке 53 он уже есть) |

`docs/guide/18-json.md` (EN+RU) описывает MySQL-рендер корректно (`:89-115`/`:90-116`); регресс
только в `11-scalar-functions.md`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`find -name 'PublicAPI*.txt'` — пусто; `rg MakeTextJsonFunction src tests` — 1-аргументный член
вызывается только из базы.

### `DateTime.DayOfYear` (точечный аудит 19.09.2026)

Публичной поверхности изменение не касается: добавлены только `public override MakeDatePart` в
`MySqlDialect`/`ClickHouseDialect` (SQLite правит существующий `switch`, SQL Server не тронут); имён
типов/членов не менялось. Шаг 5 (заморозка) остаётся открытым — переопределения попадут в
`PublicAPI.Unshipped.txt` только при подключении `PublicApiAnalyzers`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| N1 | P2 | `MemberTranslator.cs:68` (`"doy"`) → `SqlServerDialect.cs:152` | Внутренний ключ-сокращение `"doy"` утекает в SQL Server как `datepart(doy, …)` — невалидный T-SQL (функциональный дефект, см. `code-smells-review.md`, Находка 8) | Исправить рендер SQL Server (обязательно); при желании канонизировать `"dayofyear"` + PostgreSQL-override `extract(doy …)` |
| N2 | P2 | `MySqlDialect.cs:159`, `ClickHouseDialect.cs:98` | Новые `public override` без XML-`<summary>` (как и `SqlServerDialect.cs:152`); `NoWarn=CS1591` во всех 7 библиотечных `.csproj` | Добавить `<summary>` при закрытии Шага 5 |

Пробелы документации EN/RU (AGENTS.md: `docs/**` и `docs/ru/**`):

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/11-scalar-functions.md:202-204`, `docs/ru/guide/11-scalar-functions.md:207-209` | Перечислены `.Year`/`.Month`/`.Day`/`.Hour`/`.Minute`/`.Second`, `.DayOfYear` отсутствует | Добавить `.DayOfYear` и `doy`-рендер (SQL Server `datepart(dayofyear, …)` после фикса) |
| `docs/providers/sqlite.md:19`, `docs/ru/providers/sqlite.md:19` | Список `strftime`-частей без `doy` | Добавить `doy` → `%j` (`cast(... as integer)`) |
| — | «`DateTime.DayOfYear`/`DayOfWeek` напрямую не маппятся» | `DayOfYear` закрыт (с исправленным T-SQL); оставить `DayOfWeek`/`DATEFIRST` |
| — | «`doy` — допустимое сокращение T-SQL» | Неверно: T-SQL принимает `dayofyear`/`dy`/`y` (Microsoft Learn DATEPART) |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0/0**; `find -name 'PublicAPI*.txt'` — пусто.

### Оконные `percent_rank`/`cume_dist` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет: добавлены `CommonFunctions.percent_rank()` /
`cume_dist()` → `WindowFunction<double>` (`SqlFunctions.cs:351,354`; XML-`<summary>` есть) и две
ветки `WindowSql.MapWindowFunctionName` (`WindowSql.cs:23-24`, через `nameof`, без строковых
литералов). Имена `snake_case` — сознательное SQL-зеркало DSL (реестр §3 «Отмечено, но менять не
рекомендуется»). Возврат `double` корректен: SQL-стандарт даёт `double precision` в `[0,1]`/`(0,1]`,
и все 5 диалектов (`double precision`/`float`/`double`/`real`/`Float64`) читаются в `System.Double`;
не-генерик тип согласован с остальными ранжирующими функциями (`row_number`/`rank`/`dense_rank`).
Дефолтная XML-документация у обоих членов присутствует. Build Release — 0/0.

Шаг 5 (заморозка) остаётся открытым: `PublicAPI.*.txt` нет. При подключении `PublicApiAnalyzers`
внести `NextORM.Core.SqlFunctions.CommonFunctions.percent_rank() -> NextORM.Core.WindowFunction<double>!`
и `cume_dist()`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| W1 | P2 | `docs/guide/10-window-functions.md:11-13`, `docs/ru/guide/10-window-functions.md:10-13` | Обзор перечисляет типы результата «`int` для ранжирующих, `T?` для значений/агрегатов» и не упоминает новое `double` у `percent_rank`/`cume_dist` | Добавить `double` для распределений |
| W2 | P2 | `docs/guide/10-window-functions.md:244-248`, `docs/ru/guide/10-window-functions.md:247-251` | `Source:`-футер устарел: `SqlFunctions.cs:23` — это свойство `Sql`, сам `WindowFunction<T>` — `WindowFunctions.cs:16`, `SqlFunctions.cs:221` — `date_diff`, оконные методы — `:314-372`; ссылки на интеграционные тесты `:72`/`:99` сдвинуты вставкой нового теста на `:54` | Пересчитать номера строк; убрать `WindowFunction<T>` из `SqlFunctions.cs` |
| W3 | P2 | `docs/guide/10-window-functions.md:10`, `docs/ru/guide/10-window-functions.md:10` | Пустой inline-код ``  `` — потеряна ссылка на `SqlFunctions.Sql` (пре-существующий, но файл затронут этой правкой) | Восстановить `` `SqlFunctions.Sql` `` |
| W4 | P2 | `docs/guide/10-window-functions.md:220-234`, `docs/ru/guide/10-window-functions.md:222-237` | «every SQL provider … supports all functions» — на ClickHouse `percent_rank`/`cume_dist` проверены только SQL-gen, рантайм-проверки нет (PG/SQL Server/MySQL/SQLite покрыты) | Добавить ClickHouse в интеграционный прогон либо оговорить проверенные провайдеры |
| W5 | P2 | — | Блокер `nth_value` — именно SQL Server (функции нет) | Внести `[ ] nth_value` в бэклог |

`nth_value` **не** следует добавлять только с флагом `SupportsNthValue`: одного флага мало — у
SQL Server функции нет вовсе (нужна эмуляция), а на остальных результат зависит от рамки
(дефолтная `range unbounded preceding and current row` даёт `NULL` только до набора `n` строк).
Решение отложить — верное; при возврате к фиче нужен либо принудительный полный фрейм, либо
эмуляция через `first_value`.

> **Актуализация 19.09.2026 (верификация).** Прогноз реализован: `nth_value` добавлен
> (`CommonFunctions.nth_value`, `SqlFunctions.cs:453`) с флагом `ISqlDialect.SupportsNthValue`
> (`:160`), SQL Server гейтит `NotSupportedException` (`WindowFunctionTranslator.cs:50-51`).
> W1/W3/W4/W5 закрыты; прежняя аргументация о переносе решения устарела. Детали и список
> заморозки — в разделе «Повторная верификация cross-provider `iif`/`choose`/`nth_value`».

### ClickHouse `uniq`/`uniqExact`/`uniqCombined`/`uniqHLL12` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены: четыре метода
`ClickHouseFunctions.uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` → `long`
(`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:46,49,52,55`; XML-`<summary>` у всех),
`ISqlDialect.SupportsUniqAggregates` (`ISqlDialect.cs:236`) и
`ISqlDialect.MakeUniqAggregate(string, string)` (`ISqlDialect.cs:245`; реализация по умолчанию —
`SqlDialectBase.cs:51`). Имена snake_case — сознательное SQL-зеркало (реестр §3 «Отмечено, но менять
не рекомендуется», как `percent_rank`/`count_if`), P1 нет. Build Release — **0/0**.

**`long` (+ `toInt64`) — верный контракт.** Нативный `uniq*` возвращает `UInt64`, который
материализатор и драйвер ClickHouse не принимают; `long` согласован с `count_big`
(`SqlFunctions.cs:244`), а `toInt64(...)` — единственное приведение, сохраняющее 64-битный диапазон
(в `Int32`, как у `count`/`count_distinct`, значение не влезает). Теоретический overflow при
distinct-count > `Int64.MaxValue` для `uniq`/`uniqHLL12` не определён, но практически недостижим.

**Хук `MakeUniqAggregate` оправдан.** Без него `toInt64` пришлось бы либо хардкодить в
`AdvancedAggregateTranslator` (утечка провайдера в ядро), либо переопределять `MakeAggregate`, который
возвращает только имя функции и используется ещё и в `group by`. Дефолт `MakeAggregate(name)(argument)`
сохраняет паритет для диалекта без каста; дублирующего публичного члена, как в случае
`MakeTextJsonFunction(string)`, здесь нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| U1 | P2 | `docs/guide/04-grouping-and-aggregates.md:387-402`, `docs/ru/guide/04-grouping-and-aggregates.md:390-404` | Новое семейство distinct-count отсутствует в таблице семейств и в абзаце про ClickHouse (`arg_min`/`arg_max` и `-If` перечислены, `uniq` — нет) | Добавить строку `uniq`… + `SupportsUniqAggregates` и упоминание в обоих деревьях |
| U2 | P2 | `docs/ru/advanced/api-reference.md:58` | Строка `SqlFunctions` перечисляет только `Sql`/`Parameter`; строки `ClickHouse` (EN `:62`) в RU нет вовсе | Синхронизировать RU с EN (AGENTS.md: `docs/**` и `docs/ru/**`) |
| U3 | P2 | `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:5-9` | Классовый `<summary>` описывает только `argMin`/`argMax` и `-If`; новый uniq-набор не упомянут | Дополнить перечень |
| U4 | P2 | `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:236,245` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактный `MakeUniqAggregate` — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | ~~Внести в `PublicAPI.Unshipped.txt` при заморозке (ср. C в аудите текст-JSON)~~ — ✅ снято Фазой 3: `MakeUniqAggregate` удалён, в поверхность не входит; вносить `IUniqAggregateRenderer`/`UniqAggregates` (трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53). |
| U5 | P2 | `src/nextorm.clickhouse/ClickHouseDialect.cs:45,48` | Новые `public override` без XML-`<summary>` — как и прочие override диалекта | Добавить при закрытии Шага 5 (ср. N2) |
| U6 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:232-235` | `Contain("uniqExact(somestring)")` проходит и без обёртки; слой `toInt64` закреплён только интеграционным тестом, `MakeUniqAggregate` напрямую не тестируется | Добавить `Contain("toInt64(uniqExact(")` и dialect-assert `MakeUniqAggregate("uniq_exact", "x") == "toInt64(uniqExact(x))"` |

Пробелы документации EN/RU, видимые по этому изменению:

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/04-grouping-and-aggregates.md:391,401`, `docs/ru/guide/04-grouping-and-aggregates.md:394,404` | Перечислены `arg_min`/`arg_max` и `-If`, семейства distinct-count нет | Добавить `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` и `SupportsUniqAggregates` |
| `docs/ru/advanced/api-reference.md:58` | Строка `SqlFunctions` — только `Sql`/`Parameter` | Синхронизировать с EN (`Postgres`/`SqlServer`/`ClickHouse` + отдельная строка `ClickHouse`) |
| — | `[~] uniq/uniqExact` в шортлисте | `[x]` (пункт `:92` уже закрыт) |
| — | «План» всё ещё обещает ветки `EmitSimple` и «новый диалект-хук не нужен» | Исторический журнал; итог в разделе «Результат» (`:79-83`) корректен, править не обязательно |
| `docs/providers/clickhouse.md:32`, `docs/ru/providers/clickhouse.md:32` | «`uniq`/… как `uniqExact`/`uniqCombined`/`uniqHLL12`» — при этом `uniq` рендерится как `uniq` | Уточнить, что `uniq` → `uniq`, остальные три — по именам |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name 'PublicAPI*.txt'`
— пусто; ClickHouse unit-тесты и интеграционный `UniqAggregates_ShouldCountDistinctValues` — зелёные
(по данным изменения); PostgreSQL-тест `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1324-1326`
фиксирует `NotSupportedException`.

### ClickHouse параметрические `quantile`/`median` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.quantile`/`quantile_exact`/`quantile_timing` (`double?`, аргументы `double level, T? value`)
и `median` (`double?`, один аргумент) — `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:63,66,69,72`
(у всех `<summary>`); `ISqlDialect.SupportsQuantileAggregates` (`ISqlDialect.cs:252`, база `false` —
`SqlDialectBase.cs:53`) и `ISqlDialect.MakeQuantile(string, string, string)` (`ISqlDialect.cs:261`; база
`$"{MakeAggregate(name)}({level})({value})"` — `SqlDialectBase.cs:55-56`); `ClickHouseDialect` переопределяет
флаг (`:46`), `MakeAggregate` (`quantile_exact` → `quantileExact`, `quantile_timing` → `quantileTiming`, `:127-128`)
и `MakeQuantile` (`toFloat64(...)`, `:53-54`); транслятор — `AdvancedAggregateTranslator.EmitQuantile` (`:186-203`).
Имена snake_case — сознательное SQL-зеркало (реестр §3, как `uniq`/`arg_min`/`percent_rank`), P1 нет.
Build Release — **0/0**.

**`double?` + `toFloat64`: контракт верный, но применён не везде.** По документации ClickHouse
`quantile(level)(x)` для числовых типов даёт `Float64`, но `Decimal`/`Date`/`DateTime`/`DateTime64`
сохраняют входной формат; `quantileExact` сохраняет числовой входной тип; `quantileTiming` возвращает
`Float32`. Приведение к CLR `double` поэтому обязательно, и `MakeQuantile` с `toFloat64(...)` его обеспечивает.
**Однако `median(x)` (псевдоним `quantile(0.5)(x)`) рендерится через `EmitSimple` без обёртки**
(`AdvancedAggregateTranslator.cs:132-134`): для `Decimal`/`Date`/`DateTime`-колонок он вернёт входной тип и
нарушит объявленный `double?` (`SqlFunctions.ClickHouse.cs:72`). Все тесты используют `x.Id` (`Int32` → `Float64`),
поэтому дефект не покрыт (Q2).

**ClickHouse-only размещение — верно.** Двухскобочная форма `quantile(level)(value)` есть только в ClickHouse;
PostgreSQL выражает квантили иначе (`percentile_cont`/`percentile_disc`/`mode` через `WITHIN GROUP`,
`SupportsOrderedAggregates`/`MakeWithinGroup`), у SQL Server/MySQL/SQLite аналога нет. Перенос на
`CommonFunctions` подразумевал бы поддержку всеми диалектами — неверно; `ClickHouseFunctions` согласован
с `arg_min`/`uniq`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| Q1 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-11` | Два `<summary>` на классе `ClickHouseFunctions`: большой (строки 5–10) и следующий за ним однострочный (строка 11). В сгенерированном `nextorm.core.xml` (`T:NextORM.Core.ClickHouseFunctions`) оба попадают в `<member>` — DocFX/IntelliSense получат дубль. Большой summary при этом не упоминает новое семейство `quantile`/`median` | Удалить устаревший однострочник (строка 11), а в основную `<summary>` добавить `quantile`/`quantileExact`/`quantileTiming`/`median` |
| Q2 | P2 | `Visitors/AdvancedAggregateTranslator.cs:132-134` | `median` идёт через `EmitSimple` и не оборачивается в `toFloat64`; `median(x)` = `quantile(0.5)(x)` сохраняет `Decimal`/`Date`/`DateTime`, а объявленный возврат — `double?` (`SqlFunctions.ClickHouse.cs:72`). Функциональный дефект для не-числовых `T` | Рендерить `median` через приведение (`toFloat64(median(value))`): отдельный унарный хук либо маршрут через `MakeQuantile("quantile", "0.5", value)`; добавить тест на `DateTime`/`decimal`-колонку |
| Q3 | P2 | `ISqlDialect.cs:163,252,261` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактные члены — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`). Обновлено 19.09.2026: добавлен `SupportsPercentileWindow` | Внести ~~`SupportsQuantileAggregates`/`MakeQuantile`/`MakeMedian`~~ удалены Фазой 3; остаётся `SupportsPercentileWindow` (вносить) + `IQuantileAggregateRenderer`/`QuantileAggregates` (трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53). |
| Q4 | P2 | `ClickHouseDialect.cs:46,53-54` | Новые `public override` (`SupportsQuantileAggregates`, `MakeQuantile`) без XML-`<summary>` — как и прочие override диалекта | Добавить при закрытии Шага 5 (ср. N2/U5) |
| Q5 | P2 | — | Пункт `[ ]`, «Методы `CommonFunctions.quantile`» (реализовано на `ClickHouseFunctions`); шортлист `[ ]` | `[x]`; `ClickHouseFunctions.quantile`; строку 197 разделить — квантили `[x]`, `groupArray`/`topK` оставить `[ ]` |

Пробелы документации EN/RU, видимые по этому изменению. Заявленные правки трёх прозаических файлов
в рабочем дереве **отсутствуют**: `rg -i "quantile|median"` по `docs/providers/clickhouse.md`,
`docs/guide/04-grouping-and-aggregates.md`, `docs/advanced/api-reference.md` и их RU-зеркалам пуст
(единственные совпадения — `percentile_cont` в примере guide/04).

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/providers/clickhouse.md:33`, `docs/ru/providers/clickhouse.md:34` | Список агрегатов заканчивается `uniq*` | Добавить пункт: `quantile(level)(value)`/`quantileExact`/`quantileTiming` → `toFloat64(...)`, `median(x)` |
| `docs/guide/04-grouping-and-aggregates.md:391-392,397-404`, `docs/ru/guide/04-grouping-and-aggregates.md:394-395,400-407` | Проза и таблица семейств перечисляют ClickHouse-агрегаты без квантилей (таблица заканчивается `Ordered-set`) | Добавить в прозу и строку таблицы: `quantile`/`quantile_exact`/`quantile_timing`/`median`, `SupportsQuantileAggregates`, ClickHouse |
| `docs/advanced/api-reference.md:62` (EN) | Строка `ClickHouse` без квантилей; в `docs/ru/advanced/api-reference.md` строки `ClickHouse` нет вовсе (пре-существующий U2) | Дополнить EN и завести RU-строку с `quantile`/`median` |
| `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,148,155` | Перечислены ClickHouse-агрегаты (`argMin`/`argMax`, `-If`, `groupBit*`, `corr`) без квантилей | Добавить `quantile`/`median` в перечень |
| — | «ids 1..10, медиана 5.5»; раздела «Результат» нет | Фактические ids 1..3, медиана 2 (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:46`); добавить итог |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`find -name 'PublicAPI*.txt'` — пусто; ClickHouse unit **59/59**, PostgreSQL **154/154** (прогнано заново),
интеграционный `QuantileAggregates_ShouldReturnQuantile` — зелёный (по данным изменения); `rg -i "quantile|median"`
по трём прозаическим файлам EN+RU — пусто.

### ClickHouse `any`/`anyLast` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.any_agg`/`any_last` → `T?` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:80,83`;
XML-`<summary>` у обоих, `any_agg` ссылается на `CommonFunctions.any`), `ISqlDialect.SupportsAnyAggregates`
(`ISqlDialect.cs:274`, база `false` — `SqlDialectBase.cs:59`), `ClickHouseDialect.SupportsAnyAggregates => true`
(`ClickHouseDialect.cs:52`, с `<summary>`) и маппинг `MakeAggregate`: `any_agg → any`, `any_last → anyLast`
(`ClickHouseDialect.cs:139-140`); транслятор — `AdvancedAggregateTranslator` (`:123-128`, переиспользован
`EmitSimple`). Build Release — **0/0**; ClickHouse unit **62/62**, PostgreSQL **155/155** (прогнано заново).

**`any_agg` вместо перегрузки `any` — верное решение.** `CommonFunctions.any<T>(QueryCommand<T>)`
(`SqlFunctions.cs:175`) — квантор подзапроса, а трансляция диспетчеризуется **по имени метода**:
`TypeFacts.IsPredicateCall` (`TypeFacts.cs:40`) помечает любой метод с именем `any` предикатом,
`NormSqlTranslator` (`:77-90`) и `ArraySqlTranslator` (`:31`) маппят `"any"`,
`CorrelatedQueryExpressionVisitor` (`:120`) ловит `nameof(CommonFunctions.any)`. Перегрузка `any<T>(T?)`
дала бы коллизию по имени в этих местах (особенно в `IsPredicateCall`, где нет проверки объявляющего
типа), поэтому `any_agg` — не косметика, а необходимость; зафиксировано ранее
Имя `any_agg`/`any_last` — snake_case SQL-зеркало (реестр §3 «Отмечено, но
менять не рекомендуется»), P1 нет; асимметрия суффикса (`any_agg` vs `any_last`) косметична.

**`T?` — верный контракт.** ClickHouse `any`/`anyLast` возвращают тип входа без приведения, поэтому
`EmitSimple` без каста корректен (в отличие от `uniq*`/`quantile*`, где понадобились
`toInt64`/`toFloat64`). `T?` согласован с `arg_min`/`arg_max` (`SqlFunctions.ClickHouse.cs:20,23`) и
`sum_if`/`min_if`/`max_if` (`:33,39,42`). Для не-null входов ClickHouse отдаёт не-null значение
(0/пустая строка на пустом входе), т.е. `T?` — безопасное расширение; материализация покрыта
интеграционным `AnyAggregates_ShouldReturnRowValue`. Функционального дефекта, аналогичного `median`
(Q2), здесь нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| A1 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,148-155` | Новое row-picking семейство отсутствует в перечне ClickHouse-агрегатов (там `argMin`/`argMax`, `uniq*`, `quantile*`/`median`, `-If`), хотя рабочий план заявлял этот файл в списке правок | Добавить `any`/`anyLast` в строку `:106` и в прозу `:149-155` |
| A2 | P2 | `SqlDialectBase.cs:59` | Новый `public override` без XML-`<summary>` (как и соседние override базы) | Добавить при закрытии Шага 5 (ср. N2/U5/Q4) |
| A3 | P2 | `ISqlDialect.cs:306,313` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактные — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`). Обновлено 19.09.2026: `any_agg` переехал `ClickHouseFunctions` → `CommonFunctions` (cross-provider) и гейтится новым `SupportsAnyValueAggregate`; `SupportsAnyAggregates` сужен до `any_last` | Внести `SupportsAnyAggregates`, `SupportsAnyValueAggregate` и `CommonFunctions.any_agg<T>` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/XP7) |

Пробелы документации EN/RU, видимые по этому изменению:

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,149-155` | ClickHouse-агрегаты перечислены без `any`/`anyLast` | Добавить row-picking `any`/`anyLast` |
| `docs/advanced/api-reference.md:62` (EN) | «…`uniq`/`uniq_exact`/… the parameterised…» — грамматический разрыв (строка затронута правкой) | Косметика: «…`uniq*`, the parameterised…» |
| — | Запланированное имя теста `AnyAggregates_ShouldThrowBecausePostgresHasNoAny` | Фактическое — `…PostgresHasNoAnyAggregate` (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1341`) |

RU-зеркала синхронизированы: `docs/ru/providers/clickhouse.md:36,100`,
`docs/ru/guide/04-grouping-and-aggregates.md:408`, `docs/ru/advanced/api-reference.md:60`.
EN-документация также обновлена: `docs/guide/04-grouping-and-aggregates.md:405`,
`docs/providers/clickhouse.md:36,100`, `docs/advanced/api-reference.md:62`.
Пункт закрыт `[x]`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name
'PublicAPI*.txt'` — пусто; `rg SupportsAnyAggregates src` — 5 вхождений (интерфейс/база/диалект/транслятор);
ClickHouse unit **62/62**, PostgreSQL **155/155**; `AnyAggregates_ShouldReturnRowValue` — зелёный на
реальном ClickHouse (по данным изменения).

### ClickHouse строковый JSON `JSONExtract*` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.json_extract_string`/`json_extract_raw`/`json_type` → `string?`,
`json_extract_int`/`json_length` → `long`, `json_extract_float` → `double`,
`json_extract_bool`/`json_has` → `bool` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:91-112`;
XML-`<summary>` у всех восьми); `ISqlDialect.SupportsJsonExtract` (`ISqlDialect.cs:282`, база `false` —
`SqlDialectBase.cs:62`) и `ISqlDialect.MakeJsonExtract(string, IReadOnlyList<string>)`
(`ISqlDialect.cs:289`; база `$"{name}({join})"` — `SqlDialectBase.cs:64-65`); `ClickHouseDialect`
переопределяет флаг (`:55`) и хук (`:61-78`) с маппингом snake→camel и `toInt64(JSONLength(...))`;
транслятор — `JsonExtractSqlTranslator` (`Visitors/JsonExtractSqlTranslator.cs`), подключён в
`NormSqlTranslator` после `TextJsonSqlTranslator` (`:345-346`). Имена snake_case — сознательное
SQL-зеркало (реестр §3, как `uniq`/`quantile`/`any_agg`), P1 нет. Build Release — **0/0**;
ClickHouse unit **64/64**, PostgreSQL **156/156** (прогнано заново).

**Возвратные типы верны.** ClickHouse: `JSONExtractInt`→`Int64` (`long`), `JSONExtractFloat`→`Float64`
(`double`), `JSONExtractString`/`JSONExtractRaw`/`JSONType`→`String` (`string?`),
`JSONLength`→`UInt64` (обёрнут в `toInt64`, как `uniq*`, — иначе row reader не материализует),
`JSONExtractBool`/`JSONHas`→`UInt8` (декларированы `bool`). Приведение `notEquals(..., 0)` из плана
не понадобилось: интеграционный `JsonExtract_ShouldReadStringJson` проходит на реальном ClickHouse с
`B == true`; отсутствие значения даёт `0`/`''`, поэтому `long`/`double`/`bool` корректно non-nullable,
а `string?` — безопасное расширение.

**Хук `MakeJsonExtract` — верная форма.** Совпадает с `MakeTextJsonFunction(string, IReadOnlyList<string>)`
(то же вынесение провайдерного знания в диалект), принимает уже отрендеренные аргументы и **N-арный**
список, поэтому будущие variadic-пути ClickHouse (`JSONExtractString(json, 'a', 'b')`) не потребуют
менять сигнатуру хука — только снять guard `Arguments.Count == 2` в трансляторе. Каст `json_length`
выполнен в диалекте, как `toInt64`/`toFloat64` у `uniq*`/`quantile*`; дублирующего публичного члена,
как `MakeTextJsonFunction(string)`, здесь нет. Отдельный от `MakeTextJsonFunction` хук оправдан:
ClickHouse не поддерживает `SupportsTextJson`-набор (`json_value`/`json_query`/`json_modify`/`isjson`),
а общий флаг дал бы ложный гейт. 2-аргументное `(json, path)` покрывает вложенность
точечным путём (`'a.b'`), который ClickHouse принимает; ограничение приемлемо, но должно быть
зафиксировано в прозе (J-таблица).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| J1 | P2 | `ISqlDialect.cs:282,289` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактный `MakeJsonExtract` — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/A3) |
| J2 | P2 | — | Пункт всё ещё `[ ]`; текст приписывает family к `CommonFunctions.json_extract_*`, тогда как реализовано на `ClickHouseFunctions.json_extract_*`; не отражён частичный объём (arrays/`JSON`-тип/`visitParam` отложены) | Отметить `[~]`: закрыть 8 скалярных 2-арг, перечислить отложенные; `CommonFunctions` → `ClickHouseFunctions` |
| J3 | P2 | `docs/providers/clickhouse.md:28-36`, `:101`, `docs/ru/providers/clickhouse.md:28-36`, `:101` | Новое семейство не добавлено в список функций; строка таблицы `Arrays / JSON / extended scalars → not supported (PostgreSQL-only)` прямо противоречит строковому JSON (нужно развести native JSON и строковый `JSONExtract`) | Дополнить список и уточнить строку таблицы в обеих ветках |
| J4 | P2 | `docs/guide/11-scalar-functions.md:28`, `docs/ru/guide/11-scalar-functions.md:30` | Обзор ClickHouse-поверхности: «`arg_min`/`arg_max` и `-If`» — нет `JSONExtract*`/`JSONHas` | Добавить строковый JSON (оба зеркала) |
| J5 | P2 | `docs/advanced/api-reference.md:62`, `docs/ru/advanced/api-reference.md:60` | Строка `ClickHouse` без `JSONExtract*`; RU-строка `:60` — пре-существующий пробел (A1/U2), но затронута этой правкой | Дополнить обе ветки |
| J6 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,121-123,156-164` | Перечень ClickHouse-функций без строкового JSON; рабочий план заявлял файл в списке правок | Добавить строку `JSONExtract*`/`JSONHas`, `SupportsJsonExtract` |
| J7 | P2 | — | Нет раздела «Результат» с фактическими тестами/итогом (конвенция соседних планов); путь `sql-capabilities-gap-analysis.md` указан без `docs/specs/roadmap/` | Добавить итог и фактический путь |

Пробелы документации EN/RU, видимые по этому изменению. Заявленные правки трёх прозаических файлов
в рабочем дереве **отсутствуют**: `rg -i "json_extract|JSONExtract|JSONHas|JSONLength|SupportsJsonExtract"`
по `docs/providers/clickhouse.md`, `docs/guide/11-scalar-functions.md`, `docs/advanced/api-reference.md`
и их RU-зеркалам **пуст** (совпадения есть только в планах/todo и в разделах про MySQL/SQL Server text-JSON).

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/providers/clickhouse.md:28-36`, `:101`, `docs/ru/providers/clickhouse.md:28-36`, `:101` | Список функций заканчивается `any_agg`/`any_last`; таблица: «Arrays / JSON / extended scalars — not supported (PostgreSQL-only)» | `json_extract_string`/`_int`/`_float`/`_bool`/`_raw`, `json_has`, `json_length` (`toInt64(JSONLength(...))`), `json_type` → `JSONExtract*`/`JSONHas`; развести native JSON и строковый JSON |
| `docs/guide/11-scalar-functions.md:24-29`, `docs/ru/guide/11-scalar-functions.md:24-30` | ClickHouse = «`arg_min`/`arg_max` и `-If`» | Добавить строковый JSON; при желании — секция и строка матрицы (`:692`/`:708`) |
| `docs/advanced/api-reference.md:62`, `docs/ru/advanced/api-reference.md:60` | Строка `ClickHouse` без JSON | Дополнить `JSONExtract*`/`JSONHas` |
| `docs/specs/roadmap/sql-capabilities-gap-analysis.md:106,121-123,156-164` | Строковый JSON ClickHouse не упомянут | Добавить строку/прозу |
| — | `[ ]`; `CommonFunctions.json_extract_*` | `[~]`; `ClickHouseFunctions.json_extract_*`; частичный объём |
| — | Нет итога; путь без `docs/specs/roadmap/` | Добавить «Результат» и исправить путь |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано);
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **64/64**,
`tests/nextorm.postgres.tests` — **156/156** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто;
XML-`<summary>` у всех восьми новых методов и обоих новых членов `ISqlDialect` присутствуют.

### ClickHouse `visitParamExtract*` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.visit_param_extract_string`/`visit_param_extract_raw` → `string?`,
`visit_param_extract_int` → `long`, `visit_param_extract_float` → `double`,
`visit_param_extract_bool` → `bool` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:119-131`;
XML-`<summary>` у всех пяти). Диалект — `ClickHouseDialect.MakeJsonExtract`
(`src/nextorm.clickhouse/ClickHouseDialect.cs:73-77`, snake→camel); транслятор — пять веток
`JsonExtractSqlTranslator` (`src/nextorm.core/Visitors/JsonExtractSqlTranslator.cs:46-60`). Имена
snake_case — сознательное SQL-зеркало (реестр §3, как `json_extract_*`/`uniq`/`quantile`/`any_agg`),
P1 нет; второй параметр назван `name`, а не `path`, и это верно: `visitParamExtract*` принимает
плоское имя поля, а не точечный путь. Build Release — **0/0**; ClickHouse unit **65/65**, PostgreSQL
**157/157**; интеграционный `VisitParamExtract_ShouldReadFlatJson` — зелёный на реальном ClickHouse.

**Переиспользование `SupportsJsonExtract`/`MakeJsonExtract`/`JsonExtractSqlTranslator` — верно,
отдельный флаг/хук не нужен.** Оба семейства — ClickHouse-only извлечение JSON **из текстовой
колонки**; `SupportsJsonExtract => true` выставляет только ClickHouse (база `false` —
`SqlDialectBase.cs:62`), поэтому новый флаг был бы истинен ровно там же, где существующий, т.е.
дублировал бы публичный член `ISqlDialect` (лишний source-breaking контракт к Шагу 5) без
функционального эффекта. Хук `MakeJsonExtract(string name, IReadOnlyList<string> args)` уже
ключёван по имени и N-арен, поэтому пять новых пар добавлены одним `switch` без смены сигнатуры;
отдельный хук, как и отдельный транслятор, не дал бы ничего, кроме копии. Это же решение
зафиксировано в плане («тот же строково-JSON family … новых хуков нет»).

**Возвратные типы верны.** ClickHouse: `visitParamExtractInt`→`Int64` (`long`),
`visitParamExtractFloat`→`Float64` (`double`), `visitParamExtractString`/`visitParamExtractRaw`→
`String` (`string?`), `visitParamExtractBool`→`UInt8` (декларирован `bool`, материализуется, как
`JSONExtractBool`). Каст не нужен: в отличие от `JSONLength`/`uniq*` (`UInt64`) и
`quantile*`/`median`, `visitParamExtract*` не отдаёт `UInt64`, поэтому `toInt64`/`toFloat64` не
добавлены — это соответствует поведению реального ClickHouse (интеграционный тест).

**Отличие от J-раздела: новых членов `ISqlDialect` нет.** `SupportsJsonExtract` и `MakeJsonExtract`
уже введены правкой `JSONExtract*` (J1). При заморозке (Шаг 5) в `PublicAPI.Unshipped.txt` войдут
только пять методов `ClickHouseFunctions.visit_param_extract_*`; `PublicAPI.*.txt` по-прежнему нет.
Этим изменением закрыты `J3` (provider/clickhouse.md список `:37-42` + таблица `:107-108`),
`J5` (api-reference EN `:62` / RU `:60`) и `J6` (gap-analysis таблица `:123`); `J2` — частично
(`:133-137` — `[x]`, пункт `:124-131` остаётся `[~]` по отложенным
`JSONExtractArrayRaw`/`JSON_VALUE`); `J4` — проза `guide/11` дополнена `JSONExtract*`, но см. V2.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| V1 | P2 | `ISqlDialect.cs:277-282,284-288`, `ClickHouseDialect.cs:54`, `JsonExtractSqlTranslator.cs:5-11`, `SqlFunctions.ClickHouse.cs:5-13` | XML-доки гейта/хука/транслятора и классовые `<summary>` описывают только `JSONExtract*`/`JSONHas`; новое семейство `visitParamExtract*` не упомянуто, хотя проходит через тот же флаг/хук (ср. U3/Q1/N2) | Дополнить все пять мест плоским JSON `visitParamExtract*` |
| V2 | P2 | `docs/guide/11-scalar-functions.md:28-29`, `docs/ru/guide/11-scalar-functions.md:31` | Обзор ClickHouse-поверхности: «…and the string-JSON `JSONExtract*` family» — быстрый разбор плоского JSON не упомянут (файл затронут J4-дополнением) | Добавить `visitParamExtract*` (либо сослаться на `docs/guide/18-json.md:22-25`) в оба зеркала |
| V3 | P2 | `docs/guide/18-json.md:42,297-298`, `docs/ru/guide/18-json.md:42,299-300` | Матрица: ClickHouse «JSON-as-text functions = No», а ниже «SQLite and ClickHouse … nextorm does not expose them yet» — прямо противоречит буллету `:22-25`/`:22-26` и `docs/providers/clickhouse.md:107` (пре-существующая нестыковка, видимая в правленом файле) | Поставить ClickHouse `Supported ([SupportsJsonExtract])` в матрице и переписать буллет `:297`/`:299` (исключить ClickHouse; остаётся SQLite/JSON1 native-JSON) |
| V4 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:154-166` | Проза ClickHouse-функций (`:154-158`) и абзац JSON (`:164-166`) перечисляют `JSONExtract*`/`JSONHas`/`JSONLength`/`JSONType` без `visitParamExtract*`, хотя таблица `:123` его содержит | Добавить `visitParamExtract*` в прозу (или явно отнести к строковому JSON-семейству) |

Пробелы документации EN/RU, видимые по этому изменению: прозаические файлы, заявленные в изменении
(`providers/clickhouse.md` EN+RU, `guide/18-json.md` EN+RU, `advanced/api-reference.md` EN+RU),
обновлены и содержат `visit_param_extract_*`; `:133-137` — `[x]`,
раздел «Результат» есть, `sql-capabilities-gap-analysis.md:123`
дополнен. Остаются перечисленные V2–V4.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано);
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **65/65**,
`tests/nextorm.postgres.tests` — **157/157** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто;
`rg "visit_param|visitParam" src tests` — методы/маппинг/ветки/тесты на месте; XML-`<summary>` у всех
пяти новых методов присутствуют.

### ClickHouse JSONPath-скаляры `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.json_value`/`json_query` → `string?`, `json_exists` → `bool`
(`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:113-124`; XML-`<summary>` у всех трёх);
`ISqlDialect.SupportsJsonPath` (`ISqlDialect.cs:354-360`; база `false` — `SqlDialectBase.cs:100-101`);
`ClickHouseDialect.SupportsJsonPath => true` (`src/nextorm.clickhouse/ClickHouseDialect.cs:72-73`);
три пары имён добавлены в существующий `ClickHouseDialect.MakeJsonExtract` (`:96-98`). Транслятор —
`JsonExtractSqlTranslator` (ветки `:48-56`, рендер `EmitJsonPathFunction` `:101-128`), подключён в
`NormSqlTranslator` после `TextJsonSqlTranslator` (`:376`). Коллизии имён с другими поверхностями
(`SqlServerFunctions.json_value`/`json_query`, `PostgresFunctions.json_exists`) устранены на уровне
трансляторов guard'ом по `DeclaringType` (`TextJsonSqlTranslator.cs:20-21`, `JsonSqlTranslator.cs:30-33`);
корректность возвратных типов подтверждена интеграционным `JsonPath_ShouldReadStringJson` на реальном
ClickHouse (`V="hi"`, `Q="[{\"x\":1}]"`, `E=true`). Имена snake_case — сознательное SQL-зеркало
(реестр §3), P1 нет. Build Release — **0/0**; unit: clickhouse **101/101**, postgres **174/174**,
sqlserver **177/177**, mysql **44/44**, sqlite **207/207**, core **157/157** (0 failed).

**Отдельный `SupportsJsonPath` избыточен (ответ на вопрос 2).** Флаг выставлен `true` ровно там же,
где `SupportsJsonExtract`: `rg SupportsJsonExtract src` даёт единственный оверрайд — `ClickHouseDialect.cs:70`,
`rg SupportsJsonPath src` — единственный оверрайд `ClickHouseDialect.cs:73`. Два флага не различают ни
одного провайдера, т.е. дублируют один capability. Это прямо противоречит уже принятому в реестре
решению по `visitParamExtract*` (раздел V: «отдельный флаг … дублировал бы публичный член `ISqlDialect`
… без функционального эффекта») и alpha-политике «без дублирующих членов» (§1). Более того,
JSONPath-семантика SQL Server/MySQL (те же `json_value(json, '$.path')`) уже гейтится
`SupportsTextJson`, т.е. по сути это третий флаг для того же концепта. Причина, по которой гейт вообще
нужен, — точное сообщение об ошибке; его можно сохранить без флага.

**Хук `MakeJsonExtract` переиспользован — по форме верно, по имени/докам устарел (см. JP2).**
Хук ключёван по имени и N-арен (обоснование разделов J/V), поэтому три пары маппинга добавлены одним
`switch` без смены сигнатуры; `MakeJsonExtract`/`SupportsJsonExtract` — то же строково-JSON семейство.
Но публичное имя `MakeJsonExtract` и его XML-док (`ISqlDialect.cs:347-353`) описывают только
`JSONExtract*`/`visitParamExtract*`, тогда как теперь хук рендерит и `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| JP1 | P2 | `ISqlDialect.cs:354-360`, `SqlDialectBase.cs:100-101`, `ClickHouseDialect.cs:73`, `JsonExtractSqlTranslator.cs:108` | `SupportsJsonPath` истинен ровно там же, где `SupportsJsonExtract` (оба — только ClickHouse), т.е. дублирующий член публичного интерфейса (source-breaking к Шагу 5) без функционального эффекта; нарушает прецедент V-раздела и alpha-политику «без дублирующих членов» | Удалить `SupportsJsonPath` (интерфейс/база/диалект) и гейтить `EmitJsonPathFunction` по `SupportsJsonExtract`, сохранив JSONPath-сообщение; синхронно поправить `ClickHouseDialectTests.cs:93`, `docs/guide/18-json.md` (+RU), `docs/providers/clickhouse.md` (+RU). Если флаг решено оставить — зафиксировать реальный провайдерный разрыв, иначе это дубль |
| JP2 | P2 | `ISqlDialect.cs:347-353`, `SqlDialectBase.cs:97`, `ClickHouseDialect.cs:79-104` | XML-док хука `MakeJsonExtract` и базовая `<summary>` заявляют только `JSONExtract*`/`visitParamExtract*`, но хук теперь маппит и `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`; публичное имя перестало описывать полную ответственность | Обновить доки до «строково-JSON/JSONPath name map»; при заморозке поверхности рассмотреть нейтральное `MakeJsonFunction`/`MakeJsonTextFunction` (alpha допускает) |
| JP3 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-14`, `Query/SqlFunctions.cs:41-51`, `Visitors/JsonExtractSqlTranslator.cs:5-13` | Классовые `<summary>` `ClickHouseFunctions`/`JsonExtractSqlTranslator` и `<summary>` свойства `SqlFunctions.ClickHouse` перечисляют `JSONExtract*`/`visitParamExtract*`, но не JSONPath (кумулятивно, продолжение V1/D3) | Дополнить все три JSONPath-скалярами либо заменить перечень обобщённой формулировкой со ссылкой на `ClickHouseFunctions` |
| JP4 | P2 | `docs/guide/11-scalar-functions.md:29`, `docs/ru/guide/11-scalar-functions.md:31`; `docs/guide/11-scalar-functions.md:746`, `docs/ru/guide/11-scalar-functions.md:762` | Обзор ClickHouse-поверхности не упоминает JSONPath-скаляры; строка матрицы `JSONPath (jsonb_path_*)` по-прежнему помечает ClickHouse как `NotSupportedException` (продолжение V2) | Добавить JSONPath в обзорный буллет; уточнить, что строка матрицы — про `jsonb_path_*` (PG-only), а `json_value`/`json_query`/`json_exists` есть у ClickHouse |
| JP5 | P2 | `PublicAPI.*.txt` отсутствуют | Новые члены (`SupportsJsonPath` — если оставлен, 3 метода, оверрайд) не трекаются; Шаг 5 открыт | При включении `PublicApiAnalyzers` внести в `PublicAPI.Unshipped.txt` (ср. J1/D1/GLI2) |

Пробелы документации EN/RU: `docs/guide/18-json.md` (EN `:25-27,44`, RU `:25-27,44`),
`docs/providers/clickhouse.md` (EN `:46-47,138`, RU `:45-46,137`) и `docs/advanced/api-reference.md`
(EN/RU `:62`) обновлены и синхронны по именам; план содержит раздел
«Результат»; JSONPath-скаляры — `[~]` с тестами. Остаются JP3/JP4.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project
tests/<p> -c Release --no-build` — clickhouse **101/101**, postgres **174/174**, sqlserver **177/177**,
mysql **44/44**, mariadb **14/14**, sqlite **207/207**, core **157/157**; `find -name 'PublicAPI*.txt'`
— пусто; XML-`<summary>` у трёх новых методов и `SupportsJsonPath` (+ оверрайд) присутствуют.

### ClickHouse нативные JSON-функции `json_all_paths`/`json_all_paths_with_types`/`to_json_string` (точечный аудит 22.09.2026, worktree `clickhouse-json-type`)

Публичная поверхность аддитивна, переименований нет. Новые члены (Шаг 5 — внести в `PublicAPI.Unshipped.txt`):
`ClickHouseFunctions.json_all_paths(string?)` → `string[]` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:174`),
`ClickHouseFunctions.json_all_paths_with_types(string?)` → `string[]` (`:183`),
`ClickHouseFunctions.to_json_string<T>(T?)` → `string?` (`:190`); XML-`<summary>` есть у всех трёх
(`:167-173`, `:176-182`, `:185-189`). `ISqlDialect` новых членов не получил — расширены только XML-доки
`SupportsJsonExtract` (`ISqlDialect.cs:399-410`) и `MakeJsonExtract` (`:412-420`), `SqlDialectBase.SupportsJsonExtract`
(`SqlDialectBase.cs:101`); `ClickHouseDialect.MakeJsonExtract` добавил три пары имён (`ClickHouseDialect.cs:189-191`).
Имена snake_case — сознательное SQL-зеркало (реестр §3, как `json_extract_*`/`visit_param_extract_*`/`uniq`),
P0/P1 по именам нет. Build Release — **0/0**; unit: clickhouse **188/188**, postgres **257/257** (0 failed, 0 skipped).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| J8 | P1 | `Query/SqlFunctions.ClickHouse.cs:176-183` | Возвратный тип `string[]` не соответствует нативному `Map(String,String)`: `ClickHouse.Driver` отдаёт `Dictionary<string,string>`, а nextorm для `string[]` делает `(string[])GetValue` (`Expressions/SelectExpression.cs:117-122`, `DataContext/RowMapperFactory.cs:24-53`) → `InvalidCastException` при прямой проекции; задокументированная вложенность `length<T>(T[])` для `Map` невалидна (ClickHouse `length` — String/Array/QBit, мост — `mapKeys`/`mapValues`). Разбор — `code-smells-review.md`, Находка 60 | Исправить контракт (гейт fail-fast либо `IReadOnlyDictionary<string,string>` + map-ридер + `mapKeys`/`mapValues`); не документировать `length` как пример. **Закрыто 22.09.2026 (вариант B):** контракт → `Dictionary<string,string>`, `GetValue`-ветка в `SelectExpression.GetDataRecordMethod`, интеграционный тест `JsonAllPathsWithTypes_ShouldProjectNativeJsonMap` зелёный |
| J9 | P2 | `PublicAPI.*.txt` отсутствуют | Три новых метода `ClickHouseFunctions` не трекаются; Шаг 5 открыт | При включении `PublicApiAnalyzers` внести в `PublicAPI.Unshipped.txt` (ср. J1/JP5/D1) |
| J10 | P2 | `Query/SqlFunctions.ClickHouse.cs:167-183`; `ISqlDialect.cs:399-410` | XML-доки: `json_all_paths` назван «usable only nested» (для `Array(String)` это избыточно — драйвер отдаёт `string[]`); `json_all_paths_with_types` обещает вложенность через `length`, что неверно для `Map`; у обоих не указано, что аргумент — нативный `JSON`, а тесты передают `String`-колонку | Синхронизировать доки с фактическим контрактом после фикса J8; явно указать требование нативного `JSON` (или `CAST`) и мост `mapKeys`/`mapValues` |

Пробелы документации EN/RU: доки ClickHouse-поверхности обновлены в обеих ветках и синхронны по именам
(`docs/guide/18-json.md`, `docs/providers/clickhouse.md`, `docs/guide/provider-specific/clickhouse.md`,
`docs/advanced/limitations.md` + RU); gap-analysis §4 п.7 помечен shipped; `todo_clickhouse_json_type.md` удалён.
J8 и J10 закрыты 22.09.2026; остаётся только J9 (трекинг `PublicAPI.Unshipped.txt` при заморозке, Шаг 5).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit clickhouse **188/188**,
postgres **257/257**; `find -name 'PublicAPI*.txt'` — пусто; XML-`<summary>` у трёх новых методов присутствуют.

### ClickHouse функции словарей `dictGet`/`dictGetOrDefault`/`dictHas` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`ClickHouseFunctions.dict_get<TValue,TKey>`/`dict_get_or_default<TValue,TKey>` → `TValue?`,
`dict_has<TKey>` → `bool` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:139,142,145`;
XML-`<summary>` у всех трёх); `ISqlDialect.SupportsDictionaries` (`ISqlDialect.cs:297`, база `false` —
`SqlDialectBase.cs:67`) и `ISqlDialect.MakeDictionaryFunction(string, IReadOnlyList<string>)`
(`ISqlDialect.cs:303`; база `$"{name}({join})"` — `SqlDialectBase.cs:69-70`); `ClickHouseDialect`
переопределяет флаг (`:86`, с `<summary>`) и хук (`:88-100`) с маппингом `dict_get`→`dictGet`,
`dict_get_or_default`→`dictGetOrDefault`, `dict_has`→`dictHas`; транслятор — `DictionarySqlTranslator`
(`:17-56`), подключён в `NormSqlTranslator` после `JsonExtractSqlTranslator` (`:348-349`).
Имена snake_case — сознательное SQL-зеркало (реестр §3 «Отмечено, но менять не рекомендуется», как
`uniq`/`quantile`/`any_agg`/`json_extract_*`), P1 нет. Build Release — **0/0**; ClickHouse unit **67/67**,
PostgreSQL **158/158** (прогнано заново).

**`TValue`/`TKey` — верный контракт.** Тип атрибута словаря задаётся в `CREATE DICTIONARY` и статически
неизвестен, поэтому значение параметризовано (`TValue`), а ключ — вторым параметром (`TKey`), по образцу
`arg_min<TValue, TBy>` (`SqlFunctions.ClickHouse.cs:21`). `TValue?` — безопасное расширение: отсутствие
id даёт дефолт типа атрибута (`0`/`''`, NULL у `Nullable`), а не ошибку; согласовано с `arg_min`/`any_agg`
(разделы U/A). `dict_get_or_default` тоже возвращает `TValue?`, т.к. `defaultValue` объявлен `TValue?`
(пользователь может передать `null`). `dict_has` → non-nullable `bool` корректен: нативный `UInt8`
материализуется как `bool` — тот же путь, что у `JSONExtractBool`/`JSONHas` (раздел J).
Плата за универсальность: `TValue` не выводится из аргументов, поэтому вызов всегда указывает оба
generic-параметра (`dict_get<string, long>(…)`); типизированные `dictGetString`/`dictGetInt*` отложены
(вне объёма) и сняли бы это неудобство.

**Отдельные `SupportsDictionaries`/`MakeDictionaryFunction`/`DictionarySqlTranslator` — оправданы.**
Словари — не JSON: переиспользование `SupportsJsonExtract` сделало бы гейт ложным (флаг заявлял бы
строковый JSON у диалекта со словарями и наоборот), а `MakeJsonExtract` маппит только `JSONExtract*`/
`visitParamExtract*`-имена. Отдельный транслятор согласован с уже существующим разбиением
`TextJsonSqlTranslator`/`JsonExtractSqlTranslator` и даёт `NotSupportedException` с точным текстом
(`DictionarySqlTranslator.cs:37-38`). Хук не создаёт дублирующего публичного члена: база `false` +
рендер по умолчанию, как у соседних семейств.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| D1 | P2 | `ISqlDialect.cs:297,303` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет). Абстрактные члены — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести `SupportsDictionaries` + `MakeDictionaryFunction` и три метода `ClickHouseFunctions` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3) |
| D2 | P2 | `SqlDialectBase.cs:69-70` | Базовая реализация `MakeDictionaryFunction` без XML-`<summary>` (как `MakeJsonExtract` `:64-65`); `SupportsDictionaries` `:66` уже с `<summary>` | Добавить `<summary>` при закрытии Шага 5 (ср. N2/U5/Q4/A2) |
| D3 | P2 | `Query/SqlFunctions.cs:40-45` | XML-`<summary>` свойства `SqlFunctions.ClickHouse` перечисляет только `argMin`/`argMax` и `-If`, не упоминая `uniq`/`quantile`/`any`/строковый JSON/словари (кумулятивно устарело) | Дополнить перечень либо заменить на обобщённую формулировку со ссылкой на `ClickHouseFunctions` |

Пробелы документации EN/RU, видимые по этому изменению. Заявленные правки прозаических файлов
(`docs/providers/clickhouse.md` EN+RU `:42-43,109`, `docs/advanced/api-reference.md:62` / RU `:60`,
`sql-capabilities-gap-analysis.md:124,166`) в рабочем дереве
**присутствуют**, RU-зеркала синхронизированы.

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/11-scalar-functions.md:28-29`, `docs/ru/guide/11-scalar-functions.md:30-31` | Обзор ClickHouse-поверхности: «`arg_min`/`arg_max`, the `-If` combinator, the string-JSON … fast path» — функции словарей отсутствуют | Добавить `dict_get`/`dict_get_or_default`/`dict_has` в оба зеркала |
| — | `5. [~] JSONExtract*/visitParamExtract* — …; dictGet* (уровень 2) остаётся` | Убрать `dictGet*`: пункт закрыт (`:138-144` — `[x]`) |
| — | План теста `DictGet_ShouldUseClickHouseNames` | Фактическое имя — `DictFunctions_ShouldUseClickHouseNames` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:327`) |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано);
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **67/67**,
`tests/nextorm.postgres.tests` — **158/158** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто;
XML-`<summary>` у трёх новых методов и двух новых членов `ISqlDialect` присутствуют (кроме базовой
`SqlDialectBase.MakeDictionaryFunction`, D2).

### PostgreSQL uuid-генераторы и session/info-функции (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены: восемь `public`-методов
`PostgresFunctions.gen_random_uuid()`/`uuidv7()` → `Guid?`, `pg_typeof(object?)`/`current_user()`/
`session_user()`/`current_schema()`/`current_database()`/`version()` → `string?`
(`src/nextorm.core/Query/SqlFunctions.Postgres.cs:403,406,409,412,415,418,421,424`; XML-`<summary>` у всех
восьми). Трансляция — `ExtendedScalarFunctionTranslator` (`KeywordFunctions`: `current_user`/`session_user`/
`current_schema`; `DirectFunctions`: `gen_random_uuid`/`uuidv7`/`current_database`/`version`;
`pg_typeof` — отдельная ветка `EmitPgTypeOf`, `:112-125`). Новый `public override`
`PostgresDialect.MakeTypeName(Type)` (`src/nextorm.postgres/PostgresDialect.cs:23`, с `<summary>`) маппит
`string` → `text`. Имена snake_case — сознательное SQL-зеркало (реестр §3 «Отмечено, но менять не
рекомендуется», как `uniq`/`quantile`/`dict_get`), P1 нет; BCL-конфликтов (`version`/`current_user` и т.п.)
нет. Build Release — **0/0**.

**`MakeTypeName(typeof(string))` → `text` — верное место.** Рендер имени типа — ответственность диалекта;
хардкод `"text"` в ядре (`EmitPgTypeOf`) протёк бы провайдерным знанием. База
(`SqlDialectBase.cs:118-128`, `_ => type.Name`) отдаёт для `string` CLR-имя `String` — невалидный
cast-таргет; в PostgreSQL правильный текстовый тип — `text`. Бласт-радиус переопределения узкий:
`MakeTypeName(typeof(string))` достижим только из `EmitPgTypeOf` — два других вызова
(`PredicateTranslator.cs:19-25`, `BaseExpressionVisitor.cs:87-95`) отфильтрованы
`TypeFacts.TryGetNumericConversion` (`TypeFacts.cs:49-58`, только numeric→numeric). Скрытый риск на
будущее: `MySQL`/`ClickHouse` тоже отдают `String` для `string` (их `MakeTypeName` не маппит `string`) —
сегодня не вызывается, но при появлении строкового каста потребуется такой же override.

**Переиспользование `SupportsExtendedScalarFunctions` — верно.** Флаг истинен только у
`PostgresDialect` (`:75`), база — `false` (`SqlDialectBase.cs:42`); все восемь функций PostgreSQL-only
(`gen_random_uuid` — PG13+, `uuidv7` — PG18+, session/info — PG). Отдельный флаг был бы дублирующим
публичным членом `ISqlDialect` (лишний source-breaking контракт к Шагу 5) без функционального эффекта;
SQLite-тест `SessionInfoFunctions_ShouldThrowBecauseSqliteLacksThem` подтверждает reject-путь.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| PG1 | P2 | `SqlFunctions.Postgres.cs:403,406,409,412,415,418,421,424` + `PostgresDialect.cs:23` | Восемь новых `public`-методов и `public override MakeTypeName` — новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет) | Внести все восемь + `PostgresDialect.MakeTypeName(System.Type)` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1) |
| PG2 | P2 | `docs/guide/11-scalar-functions.md:251`, `docs/ru/guide/11-scalar-functions.md:256` | Таблица «PostgreSQL extended date and time» перечисляет `current_date`/`localtime`, но не новое семейство; в `docs/guide/11` (EN+RU) `gen_random_uuid`/`uuidv7`/`pg_typeof`/`current_*`/`version` отсутствуют (`rg` пуст) | Добавить строки/блок: keyword-формы `current_user`/`session_user`/`current_schema`, функции `current_database()`/`version()`/`gen_random_uuid()`/`uuidv7()` (PG18+)/`pg_typeof()` в обе ветки |
| PG3 | P2 | `docs/providers/postgres.md:41-42`, `docs/ru/providers/postgres.md:41-42`, `docs/advanced/api-reference.md:60`, `docs/ru/advanced/api-reference.md:60` | `uuidv7` указан без оговорки «PostgreSQL 18+»; на PG13–17 вызов падает в рантайме. Оговорка есть только в XML (`SqlFunctions.Postgres.cs:405`) и ранее | Добавить «(PostgreSQL 18+)» в прозу EN+RU |
| PG4 | P2 | — | «`pg_typeof` = `bigint` для `id`» — фактически `integer`: `tests/nextorm.integration.tests/PostgresSpecificTests.cs:32` (`SimpleEntity.Id` — `int`) | Исправить на `integer` (или «тип `id`-колонки») |
| PG5 | P2 | `SqlFunctions.Postgres.cs:6-12` | Два `<summary>` подряд на публичном `PostgresFunctions` → дубль в `nextorm.core.xml`/DocFX (аналог Q1); пре-существующее | Удалить лишний `<summary>` (`:12`) |
| PG6 | P2 | `tests/nextorm.postgres.tests/PostgresDialectTests.cs` | `PostgresDialect.MakeTypeName(typeof(string)) == "text"` не закреплён напрямую — только косвенно через `cast(pg_typeof(id) as text)` (`SqlGenerationTests.cs:1415`) | Добавить `Dialect.MakeTypeName(typeof(string)).Should().Be("text")` (+ fallback, напр. `typeof(long)` → `bigint`) |
| PG7 | P2 | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:40,250` | Перечень PostgreSQL extended scalar не упоминает uuid-генераторы/`pg_typeof`/`version` (есть только `current_*`); план заявлял файл в списке правок | Дополнить перечень (uuid/`pg_typeof`/`version`) |

Пробелы документации EN/RU. `docs/providers/postgres.md` (EN `:41-42` / RU `:41-42`) и
`docs/advanced/api-reference.md` (EN `:60` / RU `:60`) обновлены и **синхронизированы**; раздел закрыт `[x]`. Не закрыты PG2/PG3 (гид и оговорка PG18+). Единственный `internal`-пробел: классовая
`<summary>` `ExtendedScalarFunctionTranslator.cs:5-16` не перечисляет новое семейство (на CS1591 не
влияет, тип `internal`).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`find -name 'PublicAPI*.txt'` — пусто; XML-`<summary>` у всех восьми методов и у `MakeTypeName`-override
присутствуют; интеграционный `InfoFunctions_ShouldReturnServerValues` — зелёный (по данным изменения).

> **Актуализация 19.09.2026 — промоушен session/info-функций.** Пять методов
> `current_user`/`session_user`/`current_schema`/`current_database`/`version` перенесены с
> `PostgresFunctions` на `CommonFunctions` и получили отдельный семейный контракт; строка PG1 и адреса
> `SqlFunctions.Postgres.cs:412,415,418,421,424` относятся к историческому снимку. На `PostgresFunctions`
> остались только PG-only `gen_random_uuid`/`uuidv7`/`pg_typeof` (`SqlFunctions.Postgres.cs:402,405,408`),
> по-прежнему под `SupportsExtendedScalarFunctions`. PG4/PG7 (план по info-функциям /
> `sql-capabilities-gap-analysis.md`) частично устарели: семейство теперь кросс-провайдерное, а
> план по info-функциям продолжен планом по session-функциям.

> **Актуализация 19.09.2026 — UUID-генераторы промоутнуты.** Шаг 1 показал, что
> `gen_random_uuid`/`uuidv7` — тоже кросс-провайдерное семейство (SQL Server `newid()`, MariaDB
> `UUID_v4()`/`UUID_v7()`, ClickHouse `generateUUIDv4()`/`generateUUIDv7()`). Методы перенесены на
> `CommonFunctions` (`SqlFunctions.cs:225` — `gen_random_uuid`, `:233` — `uuidv7`; XML-`<summary>` у
> обоих) и доступны как `SqlFunctions.Sql.*`; на `PostgresFunctions` остался только PG-only
> `pg_typeof` (`SqlFunctions.Postgres.cs:402`), поэтому адреса `:402,405` и строка PG1/PG5 относятся
> к историческому снимку. Контракт `ISqlDialect` — `SupportsUuidGenerators` (`ISqlDialect.cs:231`),
> `SupportsUuidGenerator(name)` (`:237`), `MakeUuidGenerator(name)` (`:243`); база —
> `SqlDialectBase.cs:63,65,70`. Транслятор — `Visitors/UuidFunctionTranslator.cs` (45 строк),
> подключён в `NormSqlTranslator.cs:336` после session/info; два имени удалены из
> `ExtendedScalarFunctionTranslator.DirectFunctions`. Диалекты: `PostgresDialect.cs:94-104`,
> `SqlServerDialect.cs:354-363` (только v4), `ClickHouseDialect.cs:108-118`, `MariaDbDialect.cs:20-30`;
> MySQL/SQLite наследуют `false` из базы. PG2–PG4/PG6/PG7 закрыты: `guide/11` (EN+RU),
> `providers/postgres.md` (EN+RU), `api-reference` (EN+RU),
> `sql-capabilities-gap-analysis.md` приведены к кросс-провайдерной форме, а
> `MakeTypeName(typeof(string))` закреплён (`tests/nextorm.postgres.tests/PostgresDialectTests.cs:19-21`).

**Находки по промоушену UUID (точечный аудит 19.09.2026).** Публичная поверхность аддитивна
(переезд `PostgresFunctions` → `CommonFunctions`, не переименование): для потребителя
`SqlFunctions.Postgres.gen_random_uuid()` продолжает компилироваться через наследование. Новые
члены: два метода `CommonFunctions` (`SqlFunctions.cs:225,233`), три члена `ISqlDialect`
(`ISqlDialect.cs:231,237,243`; база `SqlDialectBase.cs:63,65,70`), 12 `public override` в четырёх
диалектах. XML-`<summary>` есть у всех 20 новых публичных членов, включая обе ссылки гейта —
умбреллу и `SupportsUuidGenerator(string)` (в отличие от S4 у session/info, который уже закрыт).
Build Release — **0/0**.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| UG1 | P2 | `SqlFunctions.cs:225,233`; `docs/guide/11-scalar-functions.md:277-284` (EN+RU) | Асимметрия имён: v4 сохранил PostgreSQL-написание `gen_random_uuid`, v7 — версионное `uuidv7`. Теперь семейство кросс-провайдерное, и PG-имя утекает в вызовы SQL Server/MariaDB/ClickHouse (`SqlFunctions.Sql.gen_random_uuid()` → `newid()`/`uuid_v4()`/`generateUUIDv4()`). Имя унаследовано от `PostgresFunctions` (не регресс), а snake_case-зеркало уже принято в §3 | При заморозке Шага 5 рассмотреть симметричную пару `uuidv4`/`uuidv7` (alpha-политика — замена на месте: код, тесты, XML-доки, `docs/**` + `docs/ru/**`). Не блокирует |
| UG2 | P2 | `ISqlDialect.cs:231,237,243`; `SqlDialectBase.cs:63,65,70`; `SqlFunctions.cs:225,233`; `PostgresDialect.cs:94,97,100`, `SqlServerDialect.cs:354,357,360`, `ClickHouseDialect.cs:108,111,114`, `MariaDbDialect.cs:20,23,26` | Новые члены публичного интерфейса (абстрактные — source-breaking для внешних реализаторов; в репозитории реализует только `SqlDialectBase`), два метода переехали `PostgresFunctions` → `CommonFunctions`, 12 `public override` в четырёх диалектах. Шаг 5 открыт: `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён | Внести три члена `ISqlDialect`, два метода `CommonFunctions` и override в `PublicAPI.Unshipped.txt` при заморозке; переезд даст пару RS0017+RS0016 (ср. S1/PG1/U4/Q3/J1/A3/D1) |
| UG3 | P2 | `PostgresDialect.cs:97`, `MariaDbDialect.cs:23`; `docs/guide/11-scalar-functions.md:283-284` (EN+RU) | Флаг возможностей не зависит от версии сервера: `uuidv7` разрешён на любой PostgreSQL (функция с PG18) и любой MariaDB (11.7+), `gen_random_uuid` — на PG13+. На старом сервере клиент эмитит SQL, падающий на сервере, вместо `NotSupportedException` библиотеки. Версию диалект не знает; ограничение оговорено в XML-доках (`SqlFunctions.cs:222-223,230-231`) и `guide/11` | Оставить как документированное ограничение; при желании — интеграционный прогон на PG18+ / MariaDB 11.7+, чтобы закрепить |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`dotnet test` (Release, `--no-build`) — core **157/157**, postgres **168/168**, sqlserver **176/176**,
mysql **42/42**, mariadb **11/11**, clickhouse **89/89**, sqlite **205/205** (failed — 0); integration
**909 / 0 failed / 696 skipped** (без `DOCKER_HOST`: реальные PostgreSQL/SQL Server/MySQL/ClickHouse не
запускались, `uuidv7()` исполнялся только в unit-слое); `find -name 'PublicAPI*.txt'` — пусто;
новых публичных типов нет → Приложение A (45) не меняется.

### Session/info-функции промоутнуты в `CommonFunctions` (точечный аудит 19.09.2026)

Публичная поверхность **аддитивна** (пять методов переехали, не переименованы): `current_user()`,
`session_user()`, `current_schema()`, `current_database()`, `version()` → `string?` теперь объявлены на
`CommonFunctions` (`src/nextorm.core/Query/SqlFunctions.cs:177,183,189,195,201`; XML-`<summary>` у всех
пяти) и доступны как `SqlFunctions.Sql.*`; `SqlFunctions.Postgres.*` продолжает компилироваться через
`PostgresFunctions : CommonFunctions` (`SqlFunctions.Postgres.cs:12`). Новый контракт `ISqlDialect`
(`ISqlDialect.cs:203,210,216`; база — `SqlDialectBase.cs:45-48`): умбрелла `SupportsSessionInfoFunctions`
+ `SupportsSessionInfoFunction(string)` + `MakeSessionInfoFunction(string)`. Транслятор —
`Visitors/SessionInfoFunctionTranslator.cs` (47 строк), подключён в `NormSqlTranslator` (`:330`) после
`BuiltinFunctionTranslator`; пять имён удалены из `ExtendedScalarFunctionTranslator`
(`current_date`/`current_time`/`localtime`/`localtimestamp` и `gen_random_uuid`/`uuidv7`/`pg_typeof`
остались). Диалекты: `PostgresDialect.cs:79-89`, `SqlServerDialect.cs:333-345`, `MySqlDialect.cs:50-62`
(наследуется `MariaDbDialect`), `ClickHouseDialect.cs:89-99` (3 из 5), `SqliteDialect.cs:32-37` (только
`version` → `sqlite_version()`). Build Release — **0/0**.

**Три члена — звучат и не дублируют друг друга.** Форма «умбрелла + предикат по имени + рендер по имени»
повторяет уже принятую `SupportsDateTrunc` + `SupportsDateTruncField(string)` +
`MakeDateTrunc(string,string)`; `SupportsTableFunction(string)` — принципиально другая форма (там нет
умбреллы/`Make`-хука и нет частично поддерживаемых семейств), поэтому на неё ориентироваться не нужно.
Умбрелла не избыточна: во-первых, даёт отдельное сообщение уровня семейства
(`SessionInfoFunctionTranslator.cs:33-34`), во-вторых, без неё `SupportsSessionInfoFunction` пришлось бы
считать «умбреллой вообще для любого имени». Предикат по имени обязателен: ClickHouse умеет 3 из 5,
SQLite — 1 из 5, и общий флаг дал бы ложное разрешение. Дефолт `MakeSessionInfoFunction` — `throw`
(`SqlDialectBase.cs:47-48`) — достижим только при рассогласовании предиката и рендера, как у
`MakeRepeat`/`MakeStringPosition`.

**Именование — верное, P1 нет.** Пять имён — SQL/PG-зеркало (`current_user`, `session_user`,
`current_schema` — keyword-формы; `current_database()`, `version()` — функции), что уже закреплено в §3
«Отмечено, но менять не рекомендуется» (как `uniq`/`quantile`/`dict_get`/`json_extract_*`). BCL-конфликта
нет: `version` — метод, а не тип, и не пересекается с `System.Version`. Переезд — это перенос
поверхности, а не переименование; для потребителя вызовы `SqlFunctions.Postgres.*` не меняются, поэтому
это не P1-находка, а допустимое P2-изменение поверхности (S1/S5).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| S1 | P2 | `ISqlDialect.cs:203,210,216`; `SqlFunctions.cs:177-201` | Новые члены публичного интерфейса (абстрактные — source-breaking для внешних реализаторов, в репозитории реализует только `SqlDialectBase`) и пять методов, переехавших на `CommonFunctions`; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Переезд `PostgresFunctions` → `CommonFunctions` в PublicApiAnalyzers даст пару RS0017 (удаление из производного) + RS0016 (добавление в базовый) | Внести три члена `ISqlDialect` и пять методов `CommonFunctions` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/I8/PG1). Рантайм-совместимость не страдает: унаследованный метод резолвится через базовый тип |
| S2 | P2 | `SqlDialectBase.cs:45-48` | Три новых `public virtual`-члена без XML-`<summary>` (умбрелла — только `//`-комментарий; у двух методов — ни `//`, ни `///`), тогда как соседние `SupportsAnyAggregates` (`:65`), `SupportsJsonExtract` (`:67`), `MakeJsonExtract` (`:70`) документированы | Добавить `<summary>` при закрытии Шага 5 (ср. A2/D2/N2/U5/Q4) |
| S3 | P2 | `PostgresDialect.cs:79-89`, `MySqlDialect.cs:50-62`, `SqliteDialect.cs:32-37` | Переопределения `SupportsSessionInfoFunctions`/`SupportsSessionInfoFunction`/`MakeSessionInfoFunction` без XML-`<summary>` (у MySQL/SQLite — `//`-комментарии, у PostgreSQL — один комментарий на три члена). У SQL Server (`:330-345`) и ClickHouse (`:84-99`) `<summary>` есть — разнобой внутри одного семейства | Добавить `<summary>` в трёх диалектах при закрытии Шага 5 |
| S4 | P2 | `SqlFunctions.cs:173-201` | XML-доки всех пяти методов ссылаются только на умбреллу `ISqlDialect.SupportsSessionInfoFunctions`, но фактический гейт — `SupportsSessionInfoFunction(name)`. На SQLite и ClickHouse умбрелла `true`, а `session_user()`/`current_schema()` бросают — док вводит в заблуждение | Сослаться также на `SupportsSessionInfoFunction(string)` (или только на него) в пяти `<summary>` |
| S5 | P2 | `docs/guide/11-scalar-functions.md:252`, `docs/ru/guide/11-scalar-functions.md:257`; `docs/advanced/api-reference.md:60`, `docs/ru/advanced/api-reference.md:61`; `docs/providers/postgres.md:41-42` | Проза и API-reference по-прежнему подают пять методов как `SqlFunctions.Postgres.*` / PostgreSQL-only; в EN-строке `Sql` (`api-reference.md:58`) семейство не упомянуто, у SQL Server/MySQL/MariaDB/ClickHouse/SQLite в `docs/providers/*.md` его нет (`rg` пуст). AGENTS.md требует синхронно править `docs/**` и `docs/ru/**` | Перевести пять методов в строку `Sql` (`SqlFunctions.Sql.*`), завести кросс-провайдерную таблицу «функция × диалект» (PG/SQL Server/MySQL/MariaDB — все 5; ClickHouse — 3; SQLite — `version`) и продублировать в RU |

**Актуализация 19.09.2026 (промоушен UUID).** S2/S3/S4/S5 закрыты текущим кодом: XML-`<summary>` есть
у всех трёх членов базы (`SqlDialectBase.cs:46-58`) и у переопределений PostgreSQL/MySQL/SQLite
(`PostgresDialect.cs:77-91`, `MySqlDialect.cs:51-67`, `SqliteDialect.cs:31-39`); XML-доки всех пяти
методов ссылаются и на умбреллу, и на `SupportsSessionInfoFunction(string)` (`SqlFunctions.cs:184-217`);
проза EN+RU переведена на `SqlFunctions.Sql.*` и матрицу «функция × диалект»
(`docs/guide/11-scalar-functions.md:260-272` и RU, `docs/providers/*.md`,
`docs/advanced/api-reference.md:59` и RU). S1 (заморозка `PublicAPI.*.txt`) остаётся в силе.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test` (Release,
`--no-build`) — core **153/153**, postgres **163/163**, sqlserver **171/171**, mysql **38/38**,
mariadb **8/8**, clickhouse **73/73**, sqlite **202/202**, failed — 0; `find -name 'PublicAPI*.txt'` —
пусто; XML-`<summary>` у пяти методов `CommonFunctions` и трёх членов `ISqlDialect` присутствуют;
интеграционные `InfoFunctions_ShouldReturnServerValues` покрыты для PG/SQL Server/MySQL/ClickHouse и
`InfoFunctions_ShouldReturnSqliteVersion` — для SQLite.

### SQL Server условные `IIF`/`CHOOSE` (точечный аудит 19.09.2026)

Публичная поверхность аддитивна, переименований нет. Новые члены:
`SqlServerFunctions.iif<TResult>(bool, TResult?, TResult?)` / `choose<TResult>(int, params TResult?[])` →
`TResult?` (`src/nextorm.core/Query/SqlFunctions.SqlServer.cs:66,73`; XML-`<summary>` у обоих),
`ISqlDialect.SupportsIifChoose` (`ISqlDialect.cs:297`, база `false` — `SqlDialectBase.cs:68`,
SQL Server `true` — `SqlServerDialect.cs:327`). Рендер — `BuiltinFunctionTranslator`
(`:34-39` ветки, `EmitIifChoose` `:115-121`, `FlattenChoose` `:124-134`) через
`SqlOperandTranslator.EmitFunction` (`SqlOperandTranslator.cs:144`); новых `Make*`-хуков нет, т.к.
имена SQL совпадают с CLR. Имена `iif`/`choose` — нижний регистр как SQL-зеркало (реестр §3
«Отмечено, но менять не рекомендуется», как `nullif`/`greatest`), BCL-конфликтов нет, P1 нет.
Build Release — **0/0** (прогнано заново); SQL Server unit **169/169**, PostgreSQL **161/161**
(прогнано заново).

**`TResult?` — верный контракт, в русле DSL.** У неограниченного generic-параметра `T?` — это
nullable-*аннотация*, не `Nullable<T>`: для `int` метаданные дают `int`, для `string` — `string?`.
Поэтому `iif(cond, 1, 0)`/`choose(i, 1, 2)` остаются non-null `int`, а ссылочные ветви корректно
допускают NULL (`CHOOSE` вне диапазона, NULL-ветвь `IIF`) — то же решение, что у `greatest<T>` и
`dict_get<TValue,TKey>`. Оговорка по value-типам: XML-док `choose` обещает «NULL when out of range»
(`SqlFunctions.SqlServer.cs:69-72`), но для `choose(i, 1, 2, 3)` NULL не выражается (`TResult` = `int`),
и материализация DBNull в `int` может упасть; это не новый дефект (та же семантика у
`greatest`/`sum_if`), но документированный NULL-кейс не покрыт — см. I6.

**`iif` отдельным методом (а не маппингом `?:`) — верно.** C#-тернарник — `ConditionalExpression`,
который уже рендерится в переносимый `case when ... end` (`MakeCase`; `PredicateTranslator.cs`).
Подмена его на T-SQL `IIF` сломала бы переносимость и SQL-снапшоты остальных 5 диалектов. `IIF` —
SQL-Server-специфичная форма, поэтому явный opt-in метод на `SqlServerFunctions` с гейтом (аналогично
`json_value`/`string_split`) — правильный компромисс; у `CHOOSE` C#-аналога нет вовсе. Плата —
`IIF` семантически эквивалентен `CASE` (T-SQL документирует `IIF` как сахар над `CASE`), т.е. на
SQL Server метод факультативен; это осознанный паритет с T-SQL, не дефект.

**`FlattenChoose` — верная обработка `params`.** Компилятор C# заворачивает variadic-аргументы в один
`NewArrayExpression`; `FlattenChoose` (`:124-134`) разворачивает его в позиционные операнды, повторяя
`FlattenParams` у `greatest`/`least` (`:329-332`). Для inline-формы `choose(i, "a", "b", "c")` это
даёт ровно `choose(i, 'a', 'b', 'c')` (закреплено тестом). Пограничный случай — явный/захваченный
массив (`var v = new[]{"a","b"}; choose(i, v)`): второй аргумент не `NewArrayExpression`,
`FlattenChoose` возвращает его как массив-операнд, и `SqlOperandTranslator.AppendArrayOperand` молча
биндит его одним параметром → `choose(@i, @p)` невалиден для SQL Server. Это **не регресс** (та же
дыра у `greatest`/`least` `FlattenParams`), но у нового variadic-метода стоит либо бросать понятный
`NotSupportedException` на не-`NewArrayExpression`, либо оговорить в XML-доке (I6). Guard
`Arguments.Count == 2` у `choose` избыточен (`params` всегда даёт 2 аргумента), но безвреден.

**`SupportsIifChoose` — правильный гейт для введённой поверхности.** Единый флаг покрывает обе
функции; это безопасно (только отклоняет, ложного разрешения не даёт), потому что `iif`/`choose` живут
на `SqlServerFunctions` и `true` выставляет только SQL Server. Дробить на `SupportsIif`/`SupportsChoose`
сейчас незачем: второго потребителя нет, а лишний член `ISqlDialect` — лишний source-breaking контракт
к Шагу 5 (то же рассуждение, что в V-разделе о переиспользовании `SupportsJsonExtract`). SQLite
действительно умеет `iif` (3.32+) без `choose`, но в этом изменении `iif` под SQLite **не
предъявляется**; если он появится, флаг придётся разделить (и, вероятно, перенести `iif` в
`CommonFunctions`/`SqliteFunctions`). До тех пор флаг консервативен и верен.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| I1 | P2 | `Query/SqlFunctions.SqlServer.cs:6-11` | Два `<summary>` подряд на `SqlServerFunctions` (строки 6–10 и 11) → дубль в `nextorm.core.xml`/DocFX (аналог Q1/PG5). Основной summary устарел: «Text-JSON surface … and the SQL Server table functions», `iif`/`choose` не упомянуты | Удалить однострочник `:11`; в основную `<summary>` добавить условные `iif`/`choose` |
| I2 | P2 | `Visitors/BuiltinFunctionTranslator.cs:113` | Осиротевший `<summary>` про `date_trunc` остался над `EmitIifChoose` (две `<summary>` на методе); сам `EmitDateTrunc` (`:136`) теперь без доки — вставка метода разорвала пару «док ↔ метод» | Удалить `:113` (или вернуть его над `EmitDateTrunc`, `:136`) |
| I3 | P2 | `Query/SqlFunctions.cs:32-39` | XML-`<summary>` свойства `SqlFunctions.SqlServer` перечисляет только text-JSON и `string_split`/`openjson`; `iif`/`choose` не упомянуты (кумулятивно устарело, аналог D3/U3) | Дополнить перечень условными `iif`/`choose` |
| I4 | P2 | `docs/ru/advanced/api-reference.md` | RU-зеркало не содержит строки `SqlFunctions.SqlServer` вовсе (EN `:61` этой правкой дополнена `iif`/`choose`); `Postgres`/`ClickHouse` в RU есть (`:60`,`:61`) — пре-существующий пробел U2, но изменённая строка EN не отражена | Завести RU-строку `SqlServer` с `iif`/`choose` (AGENTS.md: `docs/**` и `docs/ru/**`) |
| I5 | P2 | `docs/guide/11-scalar-functions.md:496`, `docs/ru/guide/11-scalar-functions.md:505` | Раздел «Conditional helpers»/«Условные функции» (только `nullif`/`greatest`/`least`/`num_*`) не упоминает новое `iif`/`choose`; обновлены лишь `providers/sqlserver.md` и EN api-reference | Добавить `iif`/`choose` + `SupportsIifChoose` в оба зеркала |
| I6 | P2 | `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:46`; `tests/nextorm.integration.tests/SqlServerSpecificTests.cs:15-31` | `iif` закреплён только слабым `Contain("iif(")`; `choose` вне диапазона (документированный NULL) и захваченный массив не покрыты | Усилить до полного `iif(id > 0, 'yes', 'no')`; добавить `choose(99, …)` → `null` (string) и тест/guard на не-`NewArrayExpression` |
| I7 | P2 | — | Заявлен «SQL-gen PostgreSQL/SQLite: `IifChoose_ShouldThrow`», но SQLite-теста нет (`rg IifChoose tests` — только postgres/sqlserver/integration) | Исправить на PostgreSQL; SQLite добавить отдельно либо убрать из плана |
| I8 | P2 | `ISqlDialect.cs:297`, `SqlDialectBase.cs:68`, `SqlServerDialect.cs:327`, `Query/SqlFunctions.SqlServer.cs:66,73` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён) | Внести в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/PG1) |

**Пробелы документации EN/RU.** Обновлены и синхронизированы: `docs/providers/sqlserver.md`
(EN `:105-107`,`:182`; RU `:107-109`,`:185`), `docs/advanced/api-reference.md:61` (EN),
`docs/specs/roadmap/sql-capabilities-gap-analysis.md:125`. Не
отражено: `docs/ru/advanced/api-reference.md` (I4), `docs/guide/11` EN+RU (I5),
(I7).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`dotnet test tests/nextorm.sqlserver.tests -c Release --no-build` — **169/169**, `tests/nextorm.postgres.tests`
— **161/161** (прогнано заново); `find -name 'PublicAPI*.txt'` — пусто; `rg "SupportsIifChoose" src`
— 4 вхождения (интерфейс/база/диалект/транслятор).

> **Актуализация 19.09.2026 (верификация).** I1–I5 и I7 закрыты текущим кодом, I6 закрыт частично,
> I8 заменён: объединённый `SupportsIifChoose` удалён и разделён на `SupportsIif`/`SupportsChoose`
> (+ хук `MakeIif`), `iif` переехал на `CommonFunctions`. `SqlServerFunctions.iif` в коде больше не
> объявлен (работает по наследованию). Разбор и актуальный список заморозки — в разделе «Повторная
> верификация cross-provider `iif`/`choose`/`nth_value`».

### ClickHouse `GROUP BY ... WITH TOTALS` (точечный аудит 19.09.2026)

Область: `EntityBuilder<TEntity>.WithTotals()` (`EntityBuilder.cs:444-451`), `GroupByWithTotals` на
`QueryDefinition` (`:41`) и `QueryCommand` (`:159`), `ISqlDialect.SupportsGroupByWithTotals`/
`MakeGroupByTotals` (`ISqlDialect.cs:313-324`) + реализации (`SqlDialectBase.cs:73-77`,
`ClickHouseDialect.cs:140-151`). Плюмбинг: `Select`/`ToCommand`/`CopyTo` + join-инициализаторы,
`QueryPlanEqualityComparer`, `QueryPreparer`, `SqlBuilder`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| WT1 | P2 | `ISqlDialect.cs:313-324`, `SqlDialectBase.cs:73-77`, `ClickHouseDialect.cs:140-151`, `EntityBuilder.cs:444` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные `SupportsGroupByWithTotals`/`MakeGroupByTotals` — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести `WithTotals()`, `GroupByWithTotals` (`QueryDefinition`/`QueryCommand`), `SupportsGroupByWithTotals` + `MakeGroupByTotals` (интерфейс/база/ClickHouse) в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/PG1/I8) |
| WT2 | P2 | `docs/guide/04-grouping-and-aggregates.md:193-197`; `docs/ru/guide/04-grouping-and-aggregates.md:175-179` | Абзац о `WITH TOTALS` лежит внутри раздела `## GROUPING SETS`, хотя модификатор общий (сочетается с ROLLUP/CUBE) и помещён туда по остаточному принципу; в RU он идёт до объяснения grouping sets, в EN — после. Раздел «Provider differences» (`:504-510` EN / `:505-511` RU) перечисляет ROLLUP/CUBE/GROUPING SETS, но не WITH TOTALS | Вынести абзац в отдельный подраздел после ROLLUP/CUBE (EN+RU) и добавить в «Provider differences» строку про ClickHouse-only + ограничение `ClickHouse.Driver` |

**Именование — конвенции соблюдены, P0/P1 нет.** `WithTotals()` (ср. `WithTableHint`),
`GroupByWithTotals` (ср. `GroupingType`/`GroupingSets`), `SupportsGroupByWithTotals` (ср.
`SupportsRollup`/`SupportsCube`/`SupportsGroupingSets`), `MakeGroupByTotals` (ср. `MakeGrouping`/
`MakeGroupingSets`). Кириллических/BCL-конфликтных имён нет.

**Дизайн «флаг + `MakeGroupByTotals`» выбран верно** (вместо расширения `GroupingType`/`MakeGrouping`).
`WITH TOTALS` ортогонален `ROLLUP`/`CUBE`: грамматика ClickHouse прямо допускает несколько модификаторов
(`ParserSelectQuery.cpp`: «multiple modifiers allowed, e.g. WITH ROLLUP WITH CUBE WITH TOTALS»), а
`MakeGrouping(columns, groupingType)` по контракту одно-модификаторный. Расширение `GroupingType` дало бы
комбинаторные значения (`RollupWithTotals`, `CubeWithTotals`, …) и сломало бы этот контракт. Пара
`SupportsX` + `MakeX` точно повторяет существующий паттерн (`SupportsRollup`/`MakeGrouping`,
`SupportsGroupingSets`/`MakeGroupingSets`).

**Ключ плана — полон, но продублирован.** Флаг участвует в `Equals` (`QueryPlanEqualityComparer.cs:45`)
и `GetHashCode` (`:185`) — этого достаточно. Дополнительный `+1` в `GroupingPlanHash`
(`QueryCommand.QueryPreparer.cs:500-505`) избыточен, а его комментарий неверен. `CopyTo` (`QueryCommand.Clone.cs:45-46`)
флаг явно не копирует, но клон строится через `Definition`, который его несёт (`QueryCommand.cs:120`),
поэтому корректность держится. Подробности и рекомендации — в `code-smells-review.md` (аудит WITH TOTALS).

**Запрет `GROUPING SETS` — верен.** ClickHouse сам отклоняет комбинацию:
`NOT_IMPLEMENTED: "WITH TOTALS and GROUPING SETS are not supported together"`
(`src/Interpreters/InterpreterSelectQuery.cpp:1849`). nextorm бросает `NotSupportedException` до рендера
(`SqlBuilder.cs:130-131`). Проверка не привязана к флагу диалекта, но сегодня флаг
`SupportsGroupByWithTotals` выставлен только у ClickHouse, так что решение консервативное и безопасное;
если появится провайдер с поддержкой комбинации, проверку придётся сделать диалект-зависимой.

**XML-документация — все новые члены задокументированы.** `<summary>` есть у `WithTotals`,
`QueryDefinition.GroupByWithTotals`, `QueryCommand.GroupByWithTotals`, `SupportsGroupByWithTotals`/
`MakeGroupByTotals` (интерфейс, база, ClickHouse). Новых публичных типов нет → Приложение A (45,
исторический список на `d21c473`) не меняется. `CS1591` по-прежнему в `<NoWarn>` 7 библиотечных
`.csproj` (Шаг 5).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **73/73**,
`tests/nextorm.postgres.tests --filter FullyQualifiedName~GroupByWithTotals` — **1/1**; `find -name
'PublicAPI*.txt'` — пусто; `AnalysisLevel=latest-all` (rebuild) — **0** предупреждений на новых строках
(в т.ч. `ClickHouseDialect.cs:140-151`).

### ClickHouse `LIMIT n BY expr` (точечный аудит 19.09.2026)

Область: `EntityBuilder<TEntity>.LimitBy<TResult>` ×2 (`Builders/EntityBuilder.cs:204-226`),
`ISqlDialect.SupportsLimitBy`/`MakeLimitBy` (`DataContext/Dialect/ISqlDialect.cs:621-630`),
`SqlDialectBase` (`:96-102`), `ClickHouseDialect` (`:148-158`); `LimitByClause` — `internal`
(`Query/LimitByClause.cs:11`) → в публичную поверхность не входит. Build Release — **0/0**;
XML-`<summary>` есть у **7 из 8** новых публичных членов (единственный пропуск — `SqlDialectBase.SupportsLimitBy`).
Новых публичных типов нет → Приложение A (45) без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| LIM1 | P2 | `ISqlDialect.cs:625,630` | Новые члены публичного интерфейса; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные — source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | Внести ~~`SupportsLimitBy` + `MakeLimitBy`~~ удалены Фазой 3; вносить `EntityBuilder<TEntity>.LimitBy` ×2 и `ILimitByRenderer`/`LimitBy` (трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53). |
| LIM2 | P2 | `SqlDialectBase.cs:96` | Новый `public virtual bool SupportsLimitBy => false` без XML-`<summary>` (соседний `MakeLimitBy` `:98-101` документирован) — тот же разнобой, что D2/S2/U5/N2/Q4/A2 | Добавить `<summary>` при закрытии Шага 5 |
| LIM3 | P2 | `docs/providers/clickhouse.md` (+`docs/ru/providers/clickhouse.md`), `docs/guide/05-sorting-and-paging.md` (+RU) | Новый публичный `LimitBy`/`LIMIT BY` не упомянут в пользовательских доках: страница ClickHouse перечисляет `limit`/`offset` (EN `:47-48` / RU `:47-48`) и таблицу возможностей (`:113`), но не `limit by`; гайд по пейджингу знает только `Limit`/`Offset`/`Page` (EN `:15,111-159`). AGENTS.md требует правку `docs/**` **и** `docs/ru/**` в том же изменении | Добавить bullet + строку таблицы (`LIMIT n BY` / `SupportsLimitBy`) и абзац в гайд (EN+RU) |
| LIM4 | P2 | — | Пункт `[ ] LIMIT n BY expr` не отмечен выполненным, хотя код на месте | Поставить `[x]` (ср. I7) |

ℹ️ **Наблюдения (фикс не требуется):**
- `EntityBuilder<TEntity>.LimitBy<TResult>(int, Expression<Func<TEntity,TResult>>)` использует `TResult`
  для **селектора ключа**; по Framework Design Guidelines это `TKey`. Но ровно так же назван уже
  существующий `GroupBy<TResult>` (`EntityBuilder.cs:433,445,458,488`), поэтому переименование имеет
  смысл только парой (иначе рассогласование сильнее); не находка.
- `LimitByClause` внутренний — верно: `QueryDefinition.LimitBy`/`QueryCommand.LimitBy` помечены
  `internal`, `EntityBuilder.LimitByClause` — `internal`, так что тип не протекает в публичную
  поверхность и не требует документации Приложения A.
- XML-док `ISqlDialect` `<see cref="LimitByClause"/>` (`:622-623`) ссылается на `internal`-тип из
  публичного члена: внутри сборки это разрешается, но потребитель в IntelliSense/DocFX может увидеть
  неразрешимую ссылку — можно заменить на прозу «the LIMIT BY clause».

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **76/76**,
`tests/nextorm.postgres.tests` — **164/164**, `tests/nextorm.core.tests` — **155/155**;
`rg -i "limitby" docs --glob '!docs/specs/**'` — пусто (подтверждает LIM3; совпадения только в
`docs/specs/roadmap/`).

### ClickHouse-модификаторы `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS` (точечный аудит 19.09.2026)

Область: `EntityBuilder<TEntity>.Final`/`Sample`×2/`Settings`/`PreWhere` (`Builders/EntityBuilder.cs:195-264`), `QueryDefinition.Final`/`SampleRatio`/`SampleOffset`/`Settings`/`PreWhere` (`Query/QueryDefinition.cs:45-53`), `QueryCommand`-свойства + `PreparedPreWhere`/`PreWhereShapeHash` (`Query/QueryCommand.cs:181-194`), `ISqlDialect.SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` + `MakeFinal`/`MakeSample`/`MakeSettings` (`DataContext/Dialect/ISqlDialect.cs:651-673`), `SqlDialectBase` (`:105-131`), `ClickHouseDialect` (`:154-164`). Build Release — **0/0**; XML-`<summary>` есть у **всех** новых публичных членов; новых публичных типов нет → Приложение A (45) без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| FM1 | P2 | ✅ **Закрыто 19.09.2026.** `docs/providers/clickhouse.md:52-56,124` (+RU `:50-53,124`), `docs/guide/17-query-hints.md:84-110` (+RU `:85-111`) | Было: EN/RU не упоминали `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`. Стало: список модификаторов + `xref` на `Supports*` в таблице возможностей, пример SQL, оговорка про `Memory`-движок; оба зеркала синхронны (AGENTS.md) | — |
| FM2 | P2 | `EntityBuilder.cs:235-251`; `SqlDialectBase.cs:130-131` | Публичная `Settings(params (string Key, string Value)[])` использует `ValueTuple` (имена элементов не часть CLR-контракта) и не совпадает по типу с `IReadOnlyList<KeyValuePair<string,string>>` на `QueryCommand`/`QueryDefinition`; значения интерполируются **сырыми** (`key = value`), поэтому строковый параметр ClickHouse (`'…'`) передаётся только вручную — безопасной перегрузки нет | Привести к одному типу (`IEnumerable<KeyValuePair<string,string>>`/`IReadOnlyList<…>`) либо оставить tuple, но добавить типизированную/экранирующую перегрузку; XML-док уже предупреждает — закрепить в Шаге 5 |
| FM3 | P2 | `QueryCommand.cs:181-194` (ср. `:178`) | Новые `Final`/`SampleRatio`/`SampleOffset`/`Settings`/`PreWhere` — `public` с `internal set`, тогда как ClickHouse-сосед `LimitBy` — `internal`; ClickHouse-специфичное состояние выставлено наружу непоследовательно | Выбрать один уровень: `internal` как `LimitBy` либо сознательно `public` + запись в `PublicAPI.Unshipped.txt` (FM5) |
| FM4 | P2 | `SqlBuilder.cs:79-93` | `FINAL`/`SAMPLE` дописываются к **любому** `from`, включая производную таблицу (`DataContextExtensions.From(QueryCommand)`, `DataContextExtensions.cs:87-88`) и TVF (`FromTableFunction`, `:104`); ClickHouse допускает их только у физической таблицы (`FROM table [AS alias] FINAL …`) → `from (select …) as t final` — синтаксическая ошибка. Guard'а нет | Бросать понятный `NotSupportedException`, когда `from.SubQuery`/`from.TableFunction` не `null`, либо оговорить ограничение в XML-доке (ср. наблюдения LIM) |
| FM5 | P2 | `ISqlDialect.cs:651-673`, `SqlDialectBase.cs:105-131`, `ClickHouseDialect.cs:154-164`, `QueryDefinition.cs:45-53`, `QueryCommand.cs:181-194`, `EntityBuilder.cs:195-264` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные члены `ISqlDialect` — source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`) | Внести `Final`/`Sample`×2/`Settings`/`PreWhere`, свойства `QueryDefinition`/`QueryCommand` и 4 пары `SupportsX`/`MakeX` в `PublicAPI.Unshipped.txt` при заморозке (ср. LIM1/WT1/I8) |
| FM6 | ✅ закрыто 24.09.2026 (был P2) | `FromOptions.cs:56,59` (был `EntityBuilder.cs:219,222`) | `Sample(ratio[, offset])` пропускает `double.NaN`: `ratio is < 0 or > 1` для `NaN` ложно, поэтому `Sample(double.NaN)` проходит и рендерится как `sample NaN` (`SqlDialectBase.cs:117`) — ошибка ClickHouse в рантайме вместо `ArgumentOutOfRangeException`; `Infinity` ловится (`> 1`) | Добавить `double.IsFinite(ratio)`/`IsFinite(offset)` (или `ratio is not (>= 0 and <= 1)`). ✅ **Закрыто 24.09.2026:** guard перенесён в `FromOptions.Sample` (`FromOptions.cs:56,59`), `EntityBuilder.Sample` удалён. В HEAD guard уже присутствовал — запись была stale (см. FO5/FO6 ниже) |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именования — конвенции соблюдены, P0/P1 нет.** `Final()`/`Sample()`/`PreWhere()` — императивные fluent-методы (ср. `Where`/`LimitBy`); `Settings` — существительное (ср. `Distinct`/`LimitBy`); `SupportsFinal`/`MakeFinal` и т.д. точно повторяют паттерн `SupportsLimitBy`/`MakeLimitBy`/`SupportsRollup`/`MakeGrouping`. BCL-конфликтов нет.
- **`IsFinal` (builder) vs `Final` (command/definition)** — разнобой `Is`-префикса (`IsDistinct` везде `Is`); косметика, менять только парой с `IsDistinct`.
- **Порядок рендера верен.** FINAL/SAMPLE после алиаса, `PREWHERE` после `JOIN` и до `WHERE`, `SETTINGS` в конце — совпадает с грамматикой ClickHouse (`ParserTableExpression`/`ASTTableExpression`); включая случай `needAlias` (join/correlated).
- **`SqlDialectBase` даёт generic-тела `MakeFinal`/`MakeSample`/`MakeSettings` при флагах `false`** — достижимы только через диалект, выставивший флаг (как `MakeGrouping`); для контраста `MakeLimitBy` бросает `NotSupportedException` — разнобой стиля (ℹ️).
- **Расхождения с планом.** Рабочий план заявлял имена тестов, которых нет; фактические — `QueryModifiers_ShouldThrowBecausePostgresHasNone` (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1440`), `MakeQueryModifiers_ShouldRender` (`tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`) и один интеграционный `QueryModifiers_ShouldExecute` (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:141`, только SETTINGS). ✅ раздел «Результат» заполнен 19.09.2026; остаётся расхождение имён: план обещал `Final_ShouldThrowBecausePostgresHasNoFinal`/три интеграционных `*_ShouldExecute`, фактические — один `QueryModifiers_ShouldThrowBecausePostgresHasNone` и один `QueryModifiers_ShouldExecute`; `Sample`/`Settings` на in-memory тем же guard'ом покрыты, но тестом прямо не утверждены.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **83/83**, `tests/nextorm.core.tests` — **156/156**; `tests/nextorm.postgres.tests` — **165/165** (прежний unrelated-провал `OrderedSetAggregates_ShouldEmitWithinGroup` в текущем дереве зелёный); модификаторы в `docs/providers`/`docs/guide` EN+RU описаны (FM1 закрыт); `find -name 'PublicAPI*.txt'` — пусто (FM5).

### Cross-provider `any_agg` и оконные `percentile_cont`/`percentile_disc` (точечный аудит 19.09.2026)

Область: промоушен `any_agg` из `ClickHouseFunctions` в `CommonFunctions` (`Query/SqlFunctions.cs:318-324`), `ClickHouseFunctions.any_last` (`Query/SqlFunctions.ClickHouse.cs:82`); флаги `ISqlDialect.SupportsAnyValueAggregate`/`SupportsAnyAggregates`/`SupportsPercentileWindow` (`DataContext/Dialect/ISqlDialect.cs:163,306,313`) + `SqlDialectBase` (`:36-37,75-79`); `MySqlDialect` (`:49,215`), `MariaDbDialect` (`:17`), `ClickHouseDialect` (`:51-55,239`), `SqlServerDialect` (`:114-116`); оконные `CommonFunctions.percentile_cont`/`percentile_disc` (`SqlFunctions.cs:404-416`) против ordered-set `PostgresFunctions.percentile_cont`/`percentile_disc` (`SqlFunctions.Postgres.cs:452-459`); `WindowSql.cs:25-26`, `WindowFunctionTranslator.cs:50-52,78-85`, `NormSqlTranslator.cs:67-73`, `AdvancedAggregateTranslator.cs:84-89,123-128`. Тесты: `AnyValueAggregate_*` в mysql/clickhouse/sqlserver/sqlite/postgres + 6 dialect-assertions; `PercentileWindow_*` в sqlserver/mariadb/mysql; интеграционные `MySqlSpecificTests.AnyValueAggregate_ShouldReturnGroupValue`, `SqlServerSpecificTests.PercentileWindow_ShouldReturnValue`. Build Release — **0/0**; XML-`<summary>` есть у всех новых `CommonFunctions`/`ISqlDialect`-членов; новых публичных типов нет → Приложение A (45) без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| XP1 | P2 | `SqlFunctions.cs:318-321`, `ISqlDialect.cs:308-313`, `SqlDialectBase.cs:78` | XML-доки утверждают, что `any_agg` рендерится как `ANY_VALUE(x)` **SQL Server**'ом («Rendered as `ANY_VALUE(x)` by SQL Server and MySQL/MariaDB»; «SQL Server, MySQL/MariaDB and ClickHouse opt into…»), но `SqlServerDialect` `SupportsAnyValueAggregate` **не переопределяет** — остаётся `false` (`tests/nextorm.sqlserver.tests/SqlServerDialectTests.cs:242`, `SqlGenerationTests.cs:1477`). `ANY_VALUE` есть только в SQL Server 2025/Fabric, включение отложено осознанно | Убрать SQL Server из всех трёх доков: `ANY_VALUE` — MySQL/MariaDB, `any` — ClickHouse; оговорить, что SQL Server 2025+ намеренно не включён |
| XP2 | ✅ (был P1) | `SqlFunctions.cs:413` vs `SqlFunctions.Postgres.cs:456` (и `:416`/`:459`) | Одноимённые `percentile_cont`/`percentile_disc`: оконная (`CommonFunctions`, возврат `WindowFunction<T?>`) и ordered-set (`PostgresFunctions`, возврат `T?`). `PostgresFunctions : CommonFunctions`, поэтому оконная перегрузка **наследуется** на Postgres-поверхность, и `SqlFunctions.Postgres.percentile_cont(0.5, x.Id)` биндится к ней, падая «must be completed with Over(...)» (`NormSqlTranslator.cs:71-73`) вместо ordered-set. Имя-SQL-зеркало осознанно (реестр §3), поэтому не переименовываем | В XML-док `PostgresFunctions.percentile_cont/disc` явно указать: value-форма требует `Expression<Func<T>>` (лямбда), а унаследованная `(double, T?)` — оконная и на PostgreSQL недоступна; добавить регресс-тест на сообщение для value-формы. Диспетчеризацию транслятора укрепить — см. `code-smells-review.md`, Находка 13 |
| XP3 | P2 | `ISqlDialect.cs:306` | `SupportsAnyAggregates` (мн. ч.) после промоушена `any_agg` гейтит **только** `any_last` (док `:300-306` сужен) — имя вводит в заблуждение | Переименовать на месте в `SupportsAnyLastAggregate` (alpha; ср. A3 и реестр §3) либо оставить с явной оговоркой; при заморозке внести в `PublicAPI.Unshipped.txt` |
| XP4 | P2 | `SqlServerDialect.cs:114-116` | Новый `public override SupportsPercentileWindow` задокументирован `//`, а не `///`; `<summary>` отсутствует (у базы `SqlDialectBase.cs:36-37` и MariaDB `MariaDbDialect.cs:16-17` — есть) | Добавить `<summary>` при закрытии Шага 5 (ср. N2/U5/Q4/A2) |
| XP5 | P2 | `SqlFunctions.ClickHouse.cs:5-13`, `SqlFunctions.cs:41-49` | Доки `ClickHouseFunctions` и свойства `SqlFunctions.ClickHouse` по-прежнему перечисляют `any`/`anyLast` как ClickHouse-only; `any` (= `any_agg`) переехал в `CommonFunctions` и стал cross-provider (MySQL/MariaDB/ClickHouse), у ClickHouse остался только `any_last` | Убрать `any` из обоих списков, оставить `anyLast` со ссылкой на `CommonFunctions.any_agg` |
| XP6 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:299`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1346` | Вызовы `SqlFunctions.ClickHouse.any_agg` компилируются через наследование от `CommonFunctions`, но вводят в заблуждение: `AnyAggregates_ShouldUseClickHouseNames` теперь покрывает не-ClickHouse метод, а postgres-тест «нет any» идёт через ClickHouse-поверхность. В PublicApiAnalyzers это даст пару RS0017 (удалён из производного) + RS0016 (добавлен в базовый) | Заменить на `SqlFunctions.Sql.any_agg`; в ClickHouse-тесте оставить только `any_last` (дубль `any` уже покрыт `AnyValueAggregate_ShouldUseClickHouseAny:308`) |
| XP7 | P2 | `ISqlDialect.cs:163,313`; `SqlDialectBase.cs:37,79`; `MySqlDialect.cs:49`; `MariaDbDialect.cs:17`; `ClickHouseDialect.cs:55`; `SqlServerDialect.cs:116`; `SqlFunctions.cs:324,413,416` | Новые члены публичной поверхности; Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Абстрактные `SupportsAnyValueAggregate`/`SupportsPercentileWindow` — source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`); перенос `any_agg` из производного `ClickHouseFunctions` в базовый `CommonFunctions` — пара RS0017+RS0016 | Внести в `PublicAPI.Unshipped.txt` при заморозке: `SupportsAnyValueAggregate`, `SupportsPercentileWindow`, `CommonFunctions.any_agg<T>`, `percentile_cont<T>`, `percentile_disc<T>` + override'ы (ср. A3/Q3/C/U4) |

ℹ️ **Наблюдения (фикс не требуется):**
- **`any_agg` рядом с `any` — верное решение сохранено.** Перенос в `CommonFunctions` не создаёт коллизии с квантором `any` (`CommonFunctions.any<T>(QueryCommand<T>)`), т.к. `TypeFacts.IsPredicateCall`/`NormSqlTranslator`/`ArraySqlTranslator`/`CorrelatedQueryExpressionVisitor` диспетчеризуют по имени `"any"`, а `any_agg` его не даёт; обоснование из аудита A (выше) остаётся в силе.
- **Флаги согласованы с паттерном `SupportsX` (default `false` в базе).** `T?`-контракт `any_agg` корректен (в отличие от `uniq`/`quantile`, каст не нужен); SQL Server намеренно `false`.
- **Оконный рендер верен.** `percentile_cont(fraction) within group (order by value) over (...)` (`WindowFunctionTranslator.cs:78-85`) — стандартная форма SQL Server/MariaDB; PostgreSQL ordered-set (`MakeWithinGroup`, без `over`) отделён проверкой `DeclaringType` (`NormSqlTranslator.cs:71-73`).
- **`percentile_disc`-summary короче, чем у `percentile_cont`, но присутствует**; `CS1591` подавлен в 7 `.csproj` (Шаг 5).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/<p> -c Release --no-build` — core **156/156**, mysql **40/40**, mariadb **9/9**, clickhouse **83/83**, sqlserver **173/173**, postgres **165/165**, sqlite **203/203** (0 failed); `find -name 'PublicAPI*.txt'` — пусто; контейнерные интеграционные тесты не запускались (Podman-сокет отсутствует) — SQL-gen + все unit зелёные.

**Статус фиксов (19.09.2026, внесены в том же изменении):** XP1, XP2, XP4, XP5, XP6 и Находка 13 устранены — SQL Server убран из XML-доков `any_agg` (три места); `PostgresFunctions.percentile_cont`/`percentile_disc` получили пояснение про лямбда-форму и ссылку на оконную перегрузку; `SqlServerDialect.SupportsPercentileWindow` документирован `<summary>`; доки `ClickHouseFunctions`/`SqlFunctions.ClickHouse` оставляют только `any_last`; тесты переведены на `SqlFunctions.Sql.any_agg`; `AdvancedAggregateTranslator` гейтит percentile по `DeclaringType == typeof(PostgresFunctions)`. XP3 (имя `SupportsAnyAggregates`) оставлен с явной оговоркой в доке; XP7 (PublicAPI) — при заморозке Шага 5.

### ClickHouse табличные функции `numbers`/`numbers_mt` (точечный аудит 19.09.2026)

Область: `SqlFunctions.INumbersRow` (`Query/SqlFunctions.cs:99-108`), `ClickHouseFunctions.numbers` ×3 + `numbers_mt` (`Query/SqlFunctions.ClickHouse.cs:145-167`), `ISqlDialect.SupportsTableFunction`/`WrapTableFunction` (`DataContext/Dialect/ISqlDialect.cs:643,671`), `SqlDialectBase.WrapTableFunction` (`:122`), `ClickHouseDialect.SupportsTableFunction`/`WrapTableFunction` (`:165-176`), `SqlSourceRenderer.MakeTableFunction` (`:274-338`, обёртка `:312-317`). Build Release — **0/0**; unit: clickhouse **89/89**, core **157/157**, postgres **168/168** (0 failed); контейнерная интеграция `NumbersTableFunction_ShouldReturnThreeRows` — зелёная (реальный ClickHouse). XML-`<summary>` есть у всех новых публичных членов. **Новый публичный тип** `SqlFunctions.INumbersRow` задокументирован → Приложение A (45) без изменений; Шаг 5 по-прежнему открыт.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| TF1 | P2 | `DataContext/Dialect/ISqlDialect.cs:669-671` | Новый член публичного интерфейса (абстрактный — source-breaking для внешних реализаторов `ISqlDialect`; в репозитории реализует только `SqlDialectBase`). Шаг 5 открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён) | Внести `ISqlDialect.WrapTableFunction`, `SqlDialectBase.WrapTableFunction`, `ClickHouseDialect.WrapTableFunction`, `SqlFunctions.INumbersRow` (+`Value`) и 4 метода `ClickHouseFunctions.numbers*` в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/J1/A3/D1/PG1/I8/WT1/LIM1/FM5) |
| TF2 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-13`, `Query/SqlFunctions.cs:41-49` | Док `ClickHouseFunctions` и свойства `SqlFunctions.ClickHouse` перечисляют агрегаты/JSON/словари, но **не** `numbers`/`numbers_mt` (кумулятивно устарело, ср. D3/U3/I3/XP5) | Дополнить оба перечня табличными функциями |
| TF3 | P2 | `DataContext/Dialect/ISqlDialect.cs:637-643` | `<summary>` `SupportsTableFunction` перечисляет `generate_series`/`unnest`/`string_split`/`openjson`, но не `numbers`/`numbers_mt` | Добавить ClickHouse-пример |
| TF4 | P2 | `docs/advanced/api-reference.md:62`, `docs/ru/advanced/api-reference.md:62` | Строка `ClickHouse` не содержит `numbers`/`numbers_mt` (EN и RU синхронны между собой, но обе устарели) | Добавить табличные функции в обе ветки |
| TF5 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:462-486` | `TableFunction_Numbers_ShouldEmitCall`/`_NumbersMt_` проверяют только `Contain("from numbers(@count)")`, что верно и **без** обёртки; хук `WrapTableFunction` (суть изменения) и `numbers_mt`-обёртка юнит-тестом не закреплены — слой `(select toInt64(number) as number from …)` держит только интеграционный тест (аналог U6 для `uniq`) | Усилить до `Contain("(select toInt64(number) as number from numbers(@count))")` + добавить dialect-assert `WrapTableFunction("numbers", "numbers(@count)") == "(select toInt64(number) as number from numbers(@count))"` и `WrapTableFunction("numbers_mt", …)` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `numbers`/`numbers_mt` — сознательное SQL-зеркало (реестр §3, как `uniq`/`quantile`/`any_agg`); `INumbersRow` повторяет `IGenerateSeriesRow`/`IUnnestRow<T>`/`IStringSplitRow`/`IOpenJsonRow`. BCL-конфликтов нет.
- **`WrapTableFunction` vs `Make*`-семейство.** Имя выбивается из `MakeApply`/`MakeFinal`/`MakeGrouping` (глагол `Wrap`, не `Make`), но семантически точнее; косметика.
- **Пользовательские TVF.** `SqlSourceRenderer.cs:312` вызывает хук для **любой** `[SqlTableFunction]`, а ClickHouse-диалект ключуется по имени: пользовательская TVF с именем `numbers` будет ошибочно обёрнута. Край, не дефект текущего API.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **89/89**, `tests/nextorm.core.tests` — **157/157**, `tests/nextorm.postgres.tests` — **168/168**; `rg "toInt64\(number\)|WrapTableFunction" tests/` — пусто (подтверждает TF5); `find -name 'PublicAPI*.txt'` — пусто; `docs/guide/13-table-valued-functions.md` EN+RU `:240-261`, `docs/providers/clickhouse.md` EN+RU `:53,127`,  `sql-capabilities-gap-analysis.md:107` — синхронны.

### ClickHouse табличные функции `zeros`/`zeros_mt` (точечный аудит 20.09.2026)

Область: `SqlFunctions.IZerosRow` (`Query/SqlFunctions.cs:111-120`), `ClickHouseFunctions.zeros`/`zeros_mt` (`Query/SqlFunctions.ClickHouse.cs:170-185`), `ClickHouseDialect.SupportsTableFunction` (`src/nextorm.clickhouse/ClickHouseDialect.cs:183-185`), `SqlSourceRenderer.MakeTableFunction` (`DataContext/SqlSourceRenderer.cs:311-317`), док класса `ClickHouseFunctions` (`Query/SqlFunctions.ClickHouse.cs:5-12`), док свойства `SqlFunctions.ClickHouse` (`Query/SqlFunctions.cs:41-49`), док `ISqlDialect.SupportsTableFunction` (`DataContext/Dialect/ISqlDialect.cs:653-661`); тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:555-579`, `ClickHouseDialectTests.cs:98-105`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1458-1468`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:177-187`. Build Release — **0/0**; unit: clickhouse **96/96**, core **157/157**, postgres **171/171** (0 failed); контейнерная интеграция `ZerosTableFunction_ShouldReturnThreeRows` — зелёная на реальном ClickHouse (зафиксировано ранее; в этом аудите не перезапускалась — `docker`/`podman` CLI недоступны). XML-`<summary>` есть у типа `IZerosRow` и обоих методов; свойство `Value` — без отдельного `<summary>`, ровно как `INumbersRow.Value`/`IGenerateSeriesRow.Value` (тип документирован, посвойственного док-стиля в семействе row-интерфейсов нет, `CS1591` в `<NoWarn>`). **Новый публичный тип** `SqlFunctions.IZerosRow` задокументирован → Приложение A (45) без изменений; Шаг 5 по-прежнему открыт.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| Z1 | P2 | `Query/SqlFunctions.cs:116-120`; `Query/SqlFunctions.ClickHouse.cs:177-185` | Новые публичные члены (`IZerosRow` + `Value`, `zeros`, `zeros_mt`) не отслеживаются: `PublicAPI.Shipped/Unshipped.txt` отсутствуют, `PublicApiAnalyzers` не подключён. Шаг 5 открыт | Внести `SqlFunctions.IZerosRow` (+`Value.get/set`) и `ClickHouseFunctions.zeros`/`zeros_mt` в `PublicAPI.Unshipped.txt` при заморозке (ср. TF1/CNT1) |
| Z2 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-12`; `Query/SqlFunctions.cs:41-49`; `DataContext/Dialect/ISqlDialect.cs:653-661` | Кумулятивный док-пробел: `ClickHouseFunctions`, свойство `SqlFunctions.ClickHouse` и `<summary>` `SupportsTableFunction` перечисляют `numbers`/`numbers_mt`, но **не** `zeros`/`zeros_mt` (TF2/TF3 для `numbers` уже дописаны — здесь тот же пробел по новому семейству) | Дополнить `zeros`/`zeros_mt` во всех трёх перечнях |
| Z3 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:555-579` | `TableFunction_Zeros_ShouldEmitCall`/`_ZerosMt_` проверяют только `Contain("from zeros(@count)")`, что пройдёт и при появлении лишней обёртки; свойство «`zeros` не нуждается в `WrapTableFunction`» (суть решения) не закреплено негативно (аналог TF5; у `numbers_mt` ассерт тоже по-прежнему слабый) | Добавить dialect-assert `WrapTableFunction("zeros", call) == call` и `WrapTableFunction("zeros_mt", call) == call`; при желании усилить SQL-gen до полного `from zeros(@count) as t1` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `zeros`/`zeros_mt` — сознательное SQL-зеркало (реестр §3, как `numbers`/`uniq`/`quantile`); `IZerosRow` + `Value` точно повторяют `INumbersRow` + `Value`, а также `IGenerateSeriesRow`/`IUnnestRow<T>`/`IStringSplitRow`/`IOpenJsonRow`. BCL-конфликтов нет.
- **`byte Value` против `long Value` — не несогласованность.** `IZerosRow.Value` — `byte`, потому что колонка ClickHouse `zero` имеет тип `UInt8`; `INumbersRow.Value` — `long`, потому что `number` — `UInt64` и обёртка кастует к `Int64`. Оба факта задокументированы в XML и в `docs/guide/13-table-valued-functions.md` (EN+RU).
- **Материализация без обёртки подтверждена.** `WrapTableFunction` оборачивает только `numbers`/`numbers_mt` (`ClickHouseDialect.cs:191-194`); `MakeTypeName` отображает `typeof(byte) → "UInt8"` (`:364`), чтение идёт через `GetByte` (`SelectExpression.cs`), поэтому `zeros` материализуется напрямую — интеграционный тест на реальном ClickHouse зелёный.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **96/96**, `tests/nextorm.core.tests` — **157/157**, `tests/nextorm.postgres.tests` — **171/171**; `grep -rn "WrapTableFunction" tests/` — пусто (подтверждает Z3); `find -name 'PublicAPI*.txt'` — пусто (подтверждает Z1); EN+RU `docs/advanced/api-reference.md`, `docs/providers/clickhouse.md`, `docs/guide/13-table-valued-functions.md` и `docs/specs/roadmap/{sql-capabilities-gap-analysis.md}` содержат `zeros`/`zeros_mt` (синхронны).

### ClickHouse счётчики `count`/`count_big`/`count_distinct`/`count_big_distinct`/`count_if`/`count_over` (точечный аудит 19.09.2026)

Область: `DataContext/Dialect/ISqlDialect.cs` (`MakeCount` `:662-666`, `WrapsCountResult` `:667-672`, `WrapCount` `:673-680`), `SqlDialectBase.cs` (`:407-413`), `ClickHouseDialect.cs` (`:150-158`), `Visitors/{NormSqlTranslator,AdvancedAggregateTranslator,WindowFunctionTranslator}.cs`, тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:315-347`, `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs:74-80`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:18-42,338-344`. XML-`<summary>` есть у обоих новых членов `ISqlDialect` и базовых реализаций; новых публичных типов нет → Приложение A (45) без изменений; Шаг 5 по-прежнему открыт (`PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён). Build Release — **0/0**; unit: clickhouse **94/94**, core **157/157**; контейнерная интеграция ClickHouse — **3/3**.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| CNT1 | P2 | `ISqlDialect.cs:672,680`; `SqlDialectBase.cs:411,413`; `ClickHouseDialect.cs:155,157` | Новые члены публичного интерфейса (абстрактные — source-breaking для внешних реализаторов `ISqlDialect`; в репозитории реализует только `SqlDialectBase`). Шаг 5 открыт | Внести `ISqlDialect.WrapsCountResult`/`WrapCount` и их оверрайды в `PublicAPI.Unshipped.txt` при заморозке (ср. C/U4/Q3/A3/D1/TF1) |
| CNT2 | P2 | `docs/guide/04-grouping-and-aggregates.md:503`; `docs/ru/guide/04-grouping-and-aggregates.md:504` | Таблица «Provider differences» утверждает, что ClickHouse для `count_big` «emits `count(*)` (already 64-bit)». После изменения неверно: `count_big()` → `toInt64(count(*))`, `count()` → `toInt32(count(*))`. EN и RU синхронны между собой, но обе устарели | Обновить строку ClickHouse в обеих ветках; дополнить абзац `:273-275` (RU `:275-278`), который перечисляет SQL Server/SQLite/PostgreSQL без ClickHouse |
| CNT3 | P2 | `docs/providers/clickhouse.md:31-33`; `docs/ru/providers/clickhouse.md:31-34` | Перечень агрегатов описывает `toInt64(...)` для `uniq*`, но не упоминает приведение `count`/`count_big`/`count_distinct`/`count_big_distinct` (и `count_if`) к `Int32`/`Int64` | Добавить обёртку счётчиков в обе ветки |
| CNT4 | P2 | `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:315-332`; `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:24-42` | `count_big_distinct` — единственный из шести вариантов без unit/integration-ассерта (`rg count_big_distinct tests/` — 0), хотя назван в контракте изменения | Добавить `BigD = SqlFunctions.Sql.count_big_distinct(x.Int)` и `Contain("toInt64(count(distinct nullableint))")`; по желанию — строку в integration-тест |
| CNT5 | P2 | `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:26-27` | Комментарий говорит «the dialect casts them to Int64», но `count()`/`count_distinct()`/`count_if()` кастятся в `Int32` (`toInt64` — только у `*_big`) | Уточнить: Int32 для int-вариантов, Int64 для `count_big*` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `WrapCount(string, bool)` повторяет уже принятый в реестре `WrapTableFunction` (верб `Wrap` — сознательная девиация от `Make*`-семейства, см. TF-аудит `:846`). `WrapsCountResult` — семантический предикат («результат требует обёртки»), как `ISqlDialect.RequireSubqueryAlias`/`EnforcesScalarSubqueryCardinality`, а не `Supports*`-capability; флаг управляет ветвлением/аллокацией, а не гейтит поддержку фичи. Строгая альтернатива `SupportsCountWrapping` — менее точна. Переименование не требуется.
- **`MakeCount` + `WrapCount` против `MakeUniqAggregate`.** Для `uniq*` обёртка целиком живёт в `Make*`-хуке (`ClickHouseDialect.cs:151-152`); для count — пост-хук, потому что аргументы рендерятся между `MakeCount(...)` и `)`. Асимметрия осознанная и оправдана — иначе `MakeCount` не смог бы получить уже отрендеренные аргументы.
- **`WrapsCountResult` — не первый `bool`-член `ISqlDialect` без префикса `Supports`.** `RequireSubqueryAlias` и `EnforcesScalarSubqueryCardinality` — того же класса (поведение, не capability); расширение не ухудшает однородность.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; clickhouse unit **94/94**, core **157/157**; ClickHouse integration **3/3**; `find -name 'PublicAPI*.txt'` — пусто (подтверждает CNT1); в `docs/` (кроме этого реестра) имён `WrapsCountResult`/`WrapCount` нет, поэтому CNT2/CNT3 — единственные doc-пробелы изменения.

### Точечный аудит 2 — cross-provider conditional/any_agg/percent_rank (19.09.2026)

Публичная поверхность аддитивна плюс одна замена флага; переименований имён членов нет. Новые/изменённые
члены:

- `CommonFunctions.iif<TResult>(bool, TResult?, TResult?)` → `TResult?` — условная функция перенесена с
  `SqlServerFunctions` на кросс-провайдерный `SqlFunctions.Sql` (`Query/SqlFunctions.cs:254`; XML-`<summary>`
  есть).
- `CommonFunctions.nth_value<T>(T?, int)` → `WindowFunction<T?>` — оконная функция
  (`Query/SqlFunctions.cs:440`; XML-`<summary>` есть); поддержана PostgreSQL/MySQL/MariaDB/SQLite/ClickHouse,
  SQL Server гейтит.
- `ISqlDialect.SupportsIif` (`ISqlDialect.cs:373`, база `false` — `SqlDialectBase.cs:106`) +
  `MakeIif(string, string, string)` (`ISqlDialect.cs:610`, база — `SqlDialectBase.cs:331`);
  нативные формы `iif`/`if`/`case when`.
- `ISqlDialect.SupportsChoose` (`ISqlDialect.cs:379`, база `false` — `SqlDialectBase.cs:108`) — `choose`
  остаётся SQL Server-only.
- `ISqlDialect.SupportsNthValue` (`ISqlDialect.cs:160`, база `false` — `SqlDialectBase.cs:36`) — гейт
  `nth_value`.
- `MariaDbDialect.SupportsAnyValueAggregate => false` (`MariaDbDialect.cs:24`) — MariaDB больше не наследует
  MySQL-включение (`ANY_VALUE` нет до 13.2, MDEV-10426).
- `ClickHouseDialect.SupportsPercentRankCumeDist => true` (`ClickHouseDialect.cs:64`) — прежнее утверждение,
  что ClickHouse не умеет `percent_rank`/`cume_dist`, было неверным (ошибка возникает при вызове без `OVER`).
- `ClickHouseDialect.SupportsNthValue => true` (`ClickHouseDialect.cs:67`).

**`SupportsIifChoose` разделён.** Прежний объединённый флаг удалён и заменён парой
`SupportsIif`/`SupportsChoose`: `iif` стал кросс-провайдерным, `choose` остался SQL Server-only.
Build Release — **0/0**.

**Пробелы документации EN/RU.** Правки затронули `docs/providers/*.md`,
`docs/guide/11-scalar-functions.md`, `docs/guide/10-window-functions.md`,
`docs/guide/04-grouping-and-aggregates.md`, `docs/advanced/api-reference.md` и их RU-зеркала;
`sql-capabilities-gap-analysis.md`
синхронизированы. Новых публичных типов нет → Приложение A (45) без изменений.

**Шаг 5 (заморозка) открыт:** `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён. При заморозке
внести `CommonFunctions.iif<TResult>`, `CommonFunctions.nth_value<T>`, ~~`ISqlDialect.SupportsIif`~~ (удалён Фазой 3; не вносить),
`SupportsChoose`, `SupportsNthValue`, ~~`MakeIif`~~ (удалён Фазой 3), `MariaDbDialect.SupportsAnyValueAggregate`,
`ClickHouseDialect.SupportsPercentRankCumeDist` и `ClickHouseDialect.SupportsNthValue`
(ср. C/U4/Q3/J1/A3/D1/PG1/I8/WT1/LIM1/FM5/CNT1).

### Повторная верификация cross-provider `iif`/`choose`/`nth_value` (19.09.2026)

Независимый повторный проход по изменению, уже зарегистрированному выше как «Точечный аудит 2 —
cross-provider conditional/any_agg/percent_rank (19.09.2026)». Подтверждает выводы, **исправляет
устаревшие номера строк** того раздела (`CommonFunctions.iif` — `SqlFunctions.cs:267`, а не `:254`;
`nth_value` — `:453`, а не `:440`; `ISqlDialect.MakeIif` — `:614`, а не `:610`) и фиксирует
пробелы, которых тот проход не отметил.

Область: `Query/SqlFunctions.cs` (`CommonFunctions.iif` `:267`, `nth_value` `:453`),
`Query/SqlFunctions.SqlServer.cs` (`choose` `:67`; `iif` больше не объявлен),
`DataContext/Dialect/ISqlDialect.cs` (`SupportsPercentRankCumeDist` `:154`, `SupportsNthValue`
`:160`, `SupportsIif` `:377`, `SupportsChoose` `:383`, `MakeIif` `:614`) и `SqlDialectBase.cs`
(`:35`, `:36`, `:106`, `:108`, `:331`), `Visitors/BuiltinFunctionTranslator.cs` (`EmitIif`
`:114-132`, `EmitChoose` `:135-141`), `Visitors/WindowFunctionTranslator.cs` (`nth_value`-гейт
`:50-51`), `Visitors/WindowSql.cs` (`:23`), шесть диалектов. Build Release — **0/0** (прогнано
заново); `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён.

**Имена и аннотации — P0/P1 нет.** `iif`/`choose`/`nth_value` — нижний регистр как SQL-зеркало
(реестр §3, как `nullif`/`greatest`/`any_agg`), BCL-конфликтов нет. Nullable-аннотации в русле DSL:
у неограниченного `T` запись `T?` — аннотация, не `Nullable<T>` (для `int` метаданные дают `int`,
для `string` — `string?`), поэтому `iif(cond, 1, 0)` остаётся non-null `int`;
`nth_value<T>(T?) -> WindowFunction<T?>` согласован с `lag`/`lead`/`first_value`/`last_value`.
`SupportsIif`/`SupportsChoose`/`SupportsNthValue`/`MakeIif` — PascalCase по схеме `Supports*`/`Make*`.

**Разделение флага конвенциям не противоречит, но это замена, а не расширение.** `SupportsIifChoose`
удалён из `ISqlDialect`, вместо него `SupportsIif`/`SupportsChoose` + хук `MakeIif`; в терминах
extend-only это breaking (RS0017+RS0016), допустимый только alpha-политикой §1 (`PublicAPI.*.txt`
нет, поверхность не заморожена). Разделение оправдано: `iif` стал кросс-провайдерным (`SupportsIif`
истинно у всех 5 SQL-диалектов, `MakeIif` переопределён у каждого), `choose` остался SQL Server-only;
прежняя аргументация «дробить незачем» устарела вместе с переносом `iif`. Переезд `iif`
`SqlServerFunctions`→`CommonFunctions` source-совместим (наследование сохраняет
`SqlFunctions.SqlServer.iif`), binary-breaking для скомпилированных ссылок.

**Статус ранее зарегистрированных находок (раздел «SQL Server условные `IIF`/`CHOOSE`», W-раздел):**

| # | Статус | Комментарий |
|---|--------|-------------|
| I1 | ✅ закрыта | `SqlFunctions.SqlServer.cs:6-11` — один `<summary>` |
| I2 | ✅ закрыта | `BuiltinFunctionTranslator.cs` — отдельные `<summary>` у `EmitIif` (`:113`), `EmitChoose` (`:134`), `EmitDateTrunc` (`:156`) |
| I3 | ✅ закрыта | `SqlFunctions.cs:32-39` — `choose` и перенос `iif` на `CommonFunctions` упомянуты |
| I4 | ✅ закрыта | `docs/ru/advanced/api-reference.md:60` — RU-строка `SqlServer` с `choose` появилась |
| I5 | ✅ закрыта | `docs/guide/11-scalar-functions.md:556,561-563`; RU `:564,570-572` |
| I6 | 🟡 частично | Закрыто: `choose(99, …)` → `null` (`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:30-37`) и полный ассерт `choose(1, 'a', 'b', 'c')` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:48`). Осталось: `iif` проверяется разбитыми `Contain("iif(")`+`Contain("'yes', 'no')")` (`:46-47`), guard на захваченный массив не добавлен |
| I7 | ✅ закрыта | `Iif_ShouldUseNativeForm`; SQLite-тест есть (`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1423-1425`) |
| I8 | ♻️ заменена | `SupportsIifChoose` удалён; актуальный список заморозки — IF6 ниже |
| W1 | ✅ закрыта | `docs/guide/10-window-functions.md:13` (+RU) — `double` для `percent_rank`/`cume_dist` |
| W3 | ✅ закрыта | обзор снова ссылается на `` `SqlFunctions.Sql` `` |
| W4 | ✅ закрыта | `tests/nextorm.integration.tests/CommonTestSuite.Window.cs:54`; `ClickHouseDialect.SupportsPercentRankCumeDist => true` (`:64`) |
| W5 | ✅ закрыта | `[x]` |
| W2 | ⏸ не проверялась | `Source:`-футер `guide/10` — вне области этого прохода |

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IF1 | P2 | `MySqlDialect.cs:34`, `PostgresDialect.cs:61`, `SqliteDialect.cs:46`, `ClickHouseDialect.cs:30` | Четыре новых `public override string MakeIif(...)` без XML-`<summary>` (у `SqlServerDialect.cs:336` есть). `NoWarn=CS1591` во всех 7 библиотечных `.csproj` скрывает пропуск от сборки и NuGet-потребителя | Добавить `<summary>` по образцу `SupportsIif`; закрыть при Шаге 5 (ср. N2/U5/Q4) |
| IF2 | P2 | `MySqlDialect.cs:38,40`, `PostgresDialect.cs:65,67`, `SqliteDialect.cs:50,53` | Шесть затронутых `public override`-флагов (`SupportsPercentRankCumeDist` ×3, `SupportsNthValue` ×3) задокументированы строчными `//`-комментариями, а не XML-`<summary>` (`MySqlDialect.SupportsNthValue` — вообще без доки); CS1591 скрыт | Заменить/добавить `<summary>` при закрытии Шага 5 (ср. IF1/N2) |
| IF3 | P2 | `SqlDialectBase.cs:6-11` vs `:331-332` | Классовый док обещает «a dialect can never inherit a placeholder that throws at runtime», но новый `MakeIif` — throwing-заглушка (как прежние `MakeSessionInfoFunction` `:58`, `MakeUuidGenerator` `:71`, `MakeStringReverse` `:309`); инвариант неверен | Исправить док (оговорить хуки, гейтированные `Supports*`), либо перенести пояснение в `<remarks>` |
| IF4 | P2 | `Visitors/BuiltinFunctionTranslator.cs:5-17` | Классовый `<summary>` перечисляет только `nullif`, `greatest`/`least`, `date_trunc`, `string_agg`/`array_agg`; `iif`/`choose` (и `date_add`/`date_diff`/`end_of_month`/`date_from_parts`/`contains`/`freetext`) не упомянуты — кумулятивно устарело (ср. D3/U3/GLI1) | Дополнить перечень, в первую очередь `iif`/`choose` |
| IF5 | ✅ закрыто (20.09.2026; Фаза 3 21.09.2026 удалила сами члены) | `ISqlDialect.cs:625`; `SqlDialectBase.cs:352` | `MakeIif` абстрактен в `ISqlDialect`, но база даёт throwing-реализацию: внешний реализатор интерфейса обязан его реализовать (source-breaking), а наследник базы с `SupportsIif => true` без override упадёт в рантайме | **Реализовано (вариант A):** `ISqlDialect.MakeIif` стал default interface method с throwing-телом (source-breaking для внешних реализаторов снят); `SqlDialectBase.MakeIif` оставлен `virtual` — 6 диалектов по-прежнему `override`; добавлен контрактный тест `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs` (обобщён на `SupportsIif`/`SupportsSessionInfoFunctions`/`SupportsUuidGenerators`/`SupportsLimitBy` ⇒ соответствующий `Make*` переопределён) |
| IF6 | P2 | проект целиком | **Шаг 5 (заморозка) открыт:** `PublicAPI.Shipped/Unshipped.txt` нет, `PublicApiAnalyzers` не подключён, `ApiCompat`/API-approval-теста нет | Заморозка/трекинг — см. [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53)). Список ниже актуален только для не-capability членов; `SupportsIif`/`MakeIif` и остальные удалённые Фазой 3 пары (41) в `PublicAPI.*.txt` не вносить. |

**Список заморозки (IF6).** Новые члены: `CommonFunctions.iif<TResult>(bool, TResult?, TResult?) -> TResult?`,
`CommonFunctions.nth_value<T>(T?, int) -> WindowFunction<T?>`, ~~`ISqlDialect.SupportsIif.get -> bool`~~ (удалён Фазой 3),
`ISqlDialect.SupportsChoose.get -> bool`, `ISqlDialect.SupportsNthValue.get -> bool`,
`ISqlDialect.SupportsPercentRankCumeDist.get -> bool`,
~~`ISqlDialect.MakeIif(string, string, string) -> string`~~ (удалён Фазой 3), плюс `public override`-члены в `SqlDialectBase`
и шести диалектах (`SupportsChoose`, `SupportsNthValue`,
`SupportsPercentRankCumeDist`, `MariaDbDialect.SupportsAnyValueAggregate`) — по принятой в реестре
конвенции (ср. U5/Q4/N2). Удаления (RS0017): `ISqlDialect.SupportsIifChoose` и
`SqlServerFunctions.iif`; для `iif` PublicApiAnalyzers даст пару RS0017 (производный тип) + RS0016
(базовый тип) — как при промоушене session/UUID-функций (ср. S1/UG2).

**Фаза 3 (21.09.2026):** capability-пары `SupportsIif`/`MakeIif` и остальные 39 удалены — из списка выше они исключены; актуальный трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53).

**Статус фиксов P2 (20.09.2026, внесены в том же изменении):** IF1–IF4 устранены — XML-`<summary>`
добавлены к `MakeIif` во всех четырёх провайдерских диалектах и к шести флагам
`SupportsPercentRankCumeDist`/`SupportsNthValue`; классовый док `SqlDialectBase` переформулирован
(хуки, гейтированные `Supports*`, могут бросать в базе); `<summary>` `BuiltinFunctionTranslator`
дополнен `iif`/`choose`, date-хелперами и full-text. IF5 закрыт (DIM + контрактный тест). IF6
остаётся открытым (Шаг 5 — заморозка `PublicAPI.*.txt`).

**Общий вывод по «`Supports*` ⇒ не реализовано»:** это структурная проблема не только `iif`.
Диалект может объявить флаг `true` без рабочей реализации только там, где база **бросает**:
`MakeIif`, `MakeSessionInfoFunction`, `MakeUuidGenerator`, `MakeLimitBy` (плюс `protected`
`MakeStringPosition`/`MakeStringReverse`). Флаги с рабочим ANSI-дефолтом (`SupportsGreatestLeast`,
`SupportsDateTrunc`, `SupportsDateArithmetic`) безопасны. Контрактный тест покрывает бросающие пары;
стратегическое решение — «capability object» (`IifRenderer? Iif { get; }`, где наличие рендерера и
есть флаг); реализовано capability-объектами (Фазы 1–3), итог — в этом реестре и `docs/advanced/api-reference.md`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0/0** (прогнано заново);
`find -name 'PublicAPI*.txt'` — пусто; `rg "SupportsIifChoose|EmitIifChoose" src` — 0;
`rg "SqlServerFunctions.iif" src` — 0.

### ClickHouse distributed `GLOBAL IN` (точечный аудит 20.09.2026)

Область: `ClickHouseFunctions.global_in<T>` ×3 (`Query/SqlFunctions.ClickHouse.cs:146-158`),
`ISqlDialect.SupportsGlobalPredicates` (`DataContext/Dialect/ISqlDialect.cs:687-691`, база `false` —
`SqlDialectBase.cs:417-418`), `ClickHouseDialect.SupportsGlobalPredicates => true`
(`src/nextorm.clickhouse/ClickHouseDialect.cs:196-197`), трансляторы
`Visitors/{NormSqlTranslator.cs:160-222,InValuesTranslator.cs:36,86,CorrelatedQueryExpressionVisitor.cs:116-117,184-186}`;
тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:581-609`, `ClickHouseDialectTests.cs:239`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1470-1483`,
`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:189-211`. Build Release — **0/0**;
unit: clickhouse **99/99**, postgres **172/172**. XML-`<summary>` есть у всех трёх перегрузок
`global_in` и у `ISqlDialect.SupportsGlobalPredicates`; новых публичных типов нет → Приложение A (45)
без изменений.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| GLI1 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-14`; `Query/SqlFunctions.cs:41-51` | Кумулятивный док-пробел (ср. Z2): классовая `<summary>` `ClickHouseFunctions` и `<summary>` свойства `SqlFunctions.ClickHouse` перечисляют семейства, но **не** `global_in`, хотя `docs/advanced/api-reference.md` EN+RU (`:62`) его уже упоминают | Дополнить оба перечня `global_in` |
| GLI2 | P2 | `Query/SqlFunctions.ClickHouse.cs:152-158`; `DataContext/Dialect/ISqlDialect.cs:691` | Новые публичные члены (`global_in` ×3, `SupportsGlobalPredicates`) не отслеживаются: `PublicAPI.Shipped/Unshipped.txt` отсутствуют, `PublicApiAnalyzers` не подключён. `SupportsGlobalPredicates` — абстрактный член публичного интерфейса (source-breaking для внешних реализаторов). Шаг 5 открыт | Внести оба члена в `PublicAPI.Unshipped.txt` при заморозке (ср. TF1/CNT1) |
| GLI3 | P2 | `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1470-1483`; `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:597-609` | Негативный тест покрывает только подзапросную форму; value-list `global_in` на неподдерживающем провайдере не проверена (гейт `NormSqlTranslator.cs:217` не закреплён). Value-list тест использует захваченный `List<int>`, т.е. идёт по некэшируемому пути (код-смелл Находка 14) и не ассертит переиспользование плана | Добавить `GlobalIn_Values_UnsupportedByProvider_ShouldThrow`; после фикса Находки 14 — тест переиспользования плана по образцу `InListCacheTests` (`tests/nextorm.sqlite.tests`) |

ℹ️ **Наблюдения (фикс не требуется):**

- **Именование — конвенции соблюдены, P0/P1 нет.** `global_in` — сознательное SQL-зеркало
  (`global_in` ≈ `GLOBAL IN`, как `@in`, `count_big`, `any_agg`); это не новое нарушение, а
  зафиксированное решение `API-NAMING-REVIEW.md` §3 «Отмечено, но менять не рекомендуется».
  Тип возврата `bool`, параметры `column`/`cmd`/`values`, формы перегрузок полностью повторяют
  `CommonFunctions.@in` (`SqlFunctions.cs:248-252`) — вопрос 3 закрыт положительно. BCL-конфликтов нет.
- **`SupportsGlobalPredicates` — именование согласовано.** Префикс `Supports*` + плюрал `Predicates`
  (флаг задуман под `GLOBAL IN` и будущий `GLOBAL JOIN`) — шире текущей
  реализации, но не вводит в заблуждение. `SqlDialectBase` даёт безопасный дефолт `false`.
- **Размещение члена в `ISqlDialect`.** `SupportsGlobalPredicates` объявлен между
  `MakeSubqueryPredicate` и `MakePage`, а не в начале с остальными `Supports*`-флагами; косметика, на
  именование не влияет.
- **Уровень `P2`, не `P1`.** Новые имена не конфликтуют с BCL и не нарушают конвенций типов;
  GLI1–GLI3 — это док-пробел, отсутствие трекинга поверхности и пробел тестов, а не переименования.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; clickhouse unit
**99/99**, postgres **172/172**; `find -name 'PublicAPI*.txt'` — пусто (подтверждает GLI2);
`rg "global_in|SupportsGlobalPredicates" docs/` — `docs/providers/clickhouse.md` (+RU),
`docs/advanced/api-reference.md` (+RU) и roadmap-спеки синхронны; `grep -n "global_in" src/nextorm.core/Query/SqlFunctions.ClickHouse.cs` —
3 перегрузки, `<summary>` у каждой; пары EN/RU (`docs/providers/clickhouse.md:68-70`,
`docs/ru/providers/clickhouse.md:67-69`, `docs/advanced/api-reference.md:62`,
`docs/ru/advanced/api-reference.md:62`) совпадают по именам.

### ClickHouse join strictness `ANY`/`ALL`/`ASOF` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна, переименований нет; правка `docs/**`+`docs/ru/**` по именам не
требуется (прозы обновлены автором фичи). Новые члены:

- `JoinStrictness { Default, Any, All, Asof }` — `Expressions/JoinExpression.cs:30-40` (XML-`<summary>`
  у типа и каждого члена);
- `JoinExpression.Strictness` — `:51` (XML-`<summary>` есть; сеттер **`internal set`** с 20.09.2026 —
  сужен по Находке 16, публичная мутация план-ключа устранена; сам член по-прежнему войдёт в
  `PublicAPI.Unshipped.txt` как `.get`-свойство);
- `EntityBuilder<TEntity>.WithStrictness(JoinStrictness)` — `Builders/EntityBuilder.cs:280`
  (XML-`<summary>`+`<exception>`), плюс 7 ковариантных `new`-перегрузок
  `JoinedEntityBuilder<T1..T8>.WithStrictness` — `Builders/Joins/JoinedEntityBuilder.cs:60,111,157,203,249,295,329`
  (все с `<inheritdoc/>`);
- `ISqlDialect.SupportsJoinStrictness` — `DataContext/Dialect/ISqlDialect.cs:59` (база `false` —
  `SqlDialectBase.cs:26`) и `ISqlDialect.MakeJoinKeyword(JoinType, JoinStrictness)` — `:65`
  (база — ANSI-форма + `NotSupportedException` на non-default — `SqlDialectBase.cs:191-204`);
  оверрайды ClickHouse — `src/nextorm.clickhouse/ClickHouseDialect.cs:25,31-54` (оба с XML-`<summary>`).

**CS1591/XML-doc (вопрос 4).** XML-комментарий присутствует у всех новых публичных членов и их
оверрайдов, поэтому Приложение A (45) не меняется — новых недокументированных публичных типов нет.
Именование — конвенции соблюдены, P0/P1 нет: `JoinStrictness` повторяет официальный термин ClickHouse
(`join_strictness`), `Asof` — PascalCase (не `ASOF`), как `Http`/`Json`; конфликтов с BCL нет;
`WithStrictness` согласован с уже существующим `WithTotals` (`EntityBuilder.cs:595`), `MakeJoinKeyword` —
с `MakeApply`/`MakeFinal`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| JS1 | P2 | `JoinExpression.cs:51`; `ISqlDialect.cs:59,65`; `EntityBuilder.cs:280`; `JoinedEntityBuilder.cs:60,111,157,203,249,295,329`; `PublicAPI.*.txt` отсутствуют | Новые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). «`JoinExpression.Strictness` — публичный **сеттер**, пишущий в план-ключ» — **исправлено 20.09.2026**: `Strictness`/`IsGlobal` сужены до `internal set` (Находка 16 закрыта) | При заморозке внести члены в `PublicAPI.Unshipped.txt` (актуальная арность `MakeJoinKeyword` и новые члены — см. GG1/GG2); сужение сеттера — сделано |
| JS2 | P2 | `ISqlDialect.cs:59,65` vs `ISqlDialect.cs:626-632` (`MakeIif` DIM) | Оба новых члена интерфейса абстрактные — source-breaking для внешних реализаторов `ISqlDialect`, тогда как соседний `MakeIif` намеренно сделан default interface method «so that existing external `ISqlDialect` implementations … keep compiling»; в одном контракте разная политика совместимости | Alpha-политика допускает разрыв — зафиксировать решение явно (трекинг JS2); throwing-DIM отложил бы разрыв на runtime для `Default`-join'ов, поэтому предпочтительнее оставить abstract и записать обоснование |

ℹ️ **Наблюдения (фикс не требуется).**

- **Именование — P0/P1 нет.** `JoinStrictness`/`Asof`/`WithStrictness`/`SupportsJoinStrictness`/
  `MakeJoinKeyword` не конфликтуют с BCL и следуют суффиксным конвенциям (`*Strictness` — термин
  ClickHouse; `Supports*`, `Make*`, `With*` — как у соседних модификаторов). Вопрос 4 закрыт
  положительно.
- **Дублирования публичной поверхности нет.** Отдельный флаг `SupportsJoinStrictness` оправдан: это
  ClickHouse-only capability, и он не совпадает ни с одним существующим флагом (в отличие от снятого
  `SupportsJsonPath` — JP1). 7 `new`-перегрузок — вынужденная ковариантность, не дубли контракта
  (code-smells ℹ️, вопрос 2).
- **Двойная валидация не про нейминг (вопрос 3).** Гейт рендера + `throw` базового хука — то же
  разделение «флаг/эмиттер», что у `SupportsFinal`/`MakeFinal`; на именование не влияет.
- **`MakeIif`-DIM — единственное отклонение** в `ISqlDialect`; см. JS2. Остальные недавние члены
  (`SupportsDictionaries`/`MakeDictionaryFunction`, `SupportsGlobalPredicates`) также абстрактные, так
  что `WithStrictness`-фича следует доминирующей практике, а не ломает её.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name 'PublicAPI*.txt'`
— пусто (подтверждает JS1); XML-`<summary>` у `JoinStrictness`, `JoinExpression.Strictness`,
`ISqlDialect.SupportsJoinStrictness`, `ISqlDialect.MakeJoinKeyword` и обоих оверрайдов ClickHouse
присутствуют; Приложение A (45) без изменений; `grep -l "WithStrictness\|JoinStrictness" docs` —
`docs/providers/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
имена EN/RU синхронны.

### ClickHouse GLOBAL JOIN (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет, `docs/**`+`docs/ru/**` обновлены автором
(`docs/providers/clickhouse.md:80-82` + RU, `docs/advanced/api-reference.md` + RU). Новые/изменённые члены:

- `JoinExpression.IsGlobal` — `Expressions/JoinExpression.cs:52-58` (`public bool { get; internal set; }`,
  XML-`<summary>` есть; `CloneForCache` — `:72`, план-ключ — `JoinExpressionPlanEqualityComparer.cs:36,58`);
- `ISqlDialect.SupportsGlobalJoin` — `DataContext/Dialect/ISqlDialect.cs:60-64` (default `false` —
  `SqlDialectBase.cs:27`; override ClickHouse — `ClickHouseDialect.cs:27-28`);
- `ISqlDialect.MakeJoinKeyword` **расширен** до `(JoinType, JoinStrictness, bool isGlobal)` —
  `ISqlDialect.cs:65-71` (база — `SqlDialectBase.cs:190-210`; ClickHouse — `ClickHouseDialect.cs:30-57`,
  рендер `" global[ type][ any|all|asof] join "`);
- `EntityBuilder<TEntity>.Global()` — `Builders/EntityBuilder.cs:285-293` (XML-`<summary>`+`<exception>`),
  плюс 7 ковариантных `new Global()` на `JoinedEntityBuilder<T1..T8>` —
  `Builders/Joins/JoinedEntityBuilder.cs:60,113,159,205,251,297,331` (все с `<inheritdoc/>`).

**CS1591/XML-doc.** XML-`<summary>` присутствует у всех новых/изменённых публичных членов и их оверрайдов
(`IsGlobal`, `SupportsGlobalJoin` интерфейс+база+ClickHouse, базовая и ClickHouse-реализации
`MakeJoinKeyword`, базовая `Global()`); 7 ковариантных перегрузок — `<inheritdoc/>`. Новых публичных
типов нет → Приложение A (45) без изменений. Именование — конвенции соблюдены, P0/P1 нет: `Is*` для bool,
`Supports*` — как `SupportsGlobalPredicates`/`SupportsJoinStrictness`, `Global()` — терсный модификатор
рядом с `Distinct()`/`Final()`/`Sample()`. Различение `SupportsGlobalPredicates` (GLOBAL IN) и
`SupportsGlobalJoin` (GLOBAL JOIN) однозначно.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| GG1 | P2 | `JoinExpression.cs:58`; `ISqlDialect.cs:64,71`; `SqlDialectBase.cs:27,192`; `ClickHouseDialect.cs:28,34`; `EntityBuilder.cs:293`; `JoinedEntityBuilder.cs:60,113,159,205,251,297,331`; `PublicAPI.*.txt` отсутствуют | Новые/изменённые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsGlobalJoin` и `MakeJoinKeyword` — абстрактные члены публичного интерфейса: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`). `MakeJoinKeyword` меняет **арность** существующего члена — пара RS0017+RS0016 при заморозке | При заморозке внести: `JoinExpression.IsGlobal.get`, `ISqlDialect.SupportsGlobalJoin.get`, `ISqlDialect.MakeJoinKeyword(JoinType, JoinStrictness, bool) -> string`, `SqlDialectBase`/`ClickHouseDialect`-оверрайды и `EntityBuilder<TEntity>.Global()` + 7 `new`-перегрузок. Зафиксировать alpha-разрыв явно (продолжение политики JS2); альтернатива для flag-членов — default interface property `SupportsGlobalJoin => false` (сняла бы source-разрыв), но отклонена ради единообразия с доминирующей парой `abstract`+`SqlDialectBase.virtual` |
| GG2 | P2 | `ISqlDialect.cs:71` | Расширение сигнатуры `MakeJoinKeyword` (а не отдельный `MakeGlobalJoinKeyword`) — churn публичного интерфейса, добавленного предыдущей фичей | **Принято:** метод не выпущен (alpha), внешних реализаторов нет, оба модификатора рендерятся в одном keyword (`global left any join`), поэтому единый хук связнее; зафиксировать в списке заморозки GG1 (см. code-smells, вопрос 2) |

ℹ️ **Наблюдения (фикс не требуется).**

- **Композиция `Global()`/`WithStrictness(...)` в любом порядке — корректна** (`ReplaceLastJoin`
  переносит оба флага; см. code-smells, вопрос 1). Единственный пробел — тест обратного порядка
  (Находка 18), не нейминг.
- **`WithStrictness`/`Global` называют действие, а не «join»** — оба модификатора применяются к
  последнему join'у, имя сохраняет arity-агностичность; `Global()` не конфликтует с BCL.
- **Отдельный флаг `SupportsGlobalJoin` оправдан** — ClickHouse-only capability, не совпадает с
  `SupportsGlobalPredicates` (разные фичи) и не дублирует `SupportsJoinStrictness`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
`find -name 'PublicAPI*.txt'` — пусто (подтверждает GG1); XML-`<summary>` у `IsGlobal`,
`SupportsGlobalJoin`, `Global()` и `MakeJoinKeyword` (интерфейс/база/ClickHouse) присутствуют;
Приложение A (45) без изменений; `grep -l "SupportsGlobalJoin\|EntityBuilder.Global" docs` —
`docs/providers/clickhouse.md` (+RU), `docs/specs/roadmap/{sql-capabilities-gap-analysis.md}` — имена EN/RU синхронны. Тесты в этом проходе не перезапускались (только build); ссылки — по прогону автора фичи.

### ClickHouse массивы и `arrayJoin` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые/изменённые члены:

- `ISqlDialect.SupportsArrayFunctions` — `DataContext/Dialect/ISqlDialect.cs:136-143` (abstract; default
  `false` — `SqlDialectBase.cs:33`; override ClickHouse — `src/nextorm.clickhouse/ClickHouseDialect.cs:30-35`);
- `ISqlDialect.SupportsArrayJoin` — `ISqlDialect.cs:144-149` (default `false` — `SqlDialectBase.cs:34`;
  override ClickHouse — `ClickHouseDialect.cs:36-38`);
- `ISqlDialect.MakeArrayFunction(string name, string call)` — `ISqlDialect.cs:682-689` (**default interface
  method**, identity); `SqlDialectBase.cs:421-423` (identity); override ClickHouse — `ClickHouseDialect.cs:40-45`
  (`length`/`indexOf` в `toInt64(...)`);
- `ClickHouseFunctions.array_join<T>(T[]) -> T`, `length<T>(T[]) -> long`, `has<T>(T[], T) -> bool`,
  `index_of<T>(T[], T) -> long`, `has_any<T>(T[], T[]) -> bool`, `has_all<T>(T[], T[]) -> bool`,
  `array_string_concat<T>(T[], string? = null) -> string`, `split_by_char(string?, string?) -> string[]`,
  `array_sort<T>(T[]) -> T[]`, `array_reverse<T>(T[]) -> T[]`, `array_distinct<T>(T[]) -> T[]` —
  `Query/SqlFunctions.ClickHouse.cs:215-253`.

**CS1591/XML-doc.** XML-`<summary>` присутствует у всех 11 новых методов, обоих флагов
(интерфейс+база+ClickHouse) и у трёх реализаций `MakeArrayFunction`; новых публичных типов нет →
Приложение A (45) без изменений. Именование — конвенции соблюдены (snake_case DSL — SQL-зеркало,
`Supports*`/`Make*` — как у соседних семейств), P0/P1 по именам нет. Отдельный CH-класс оправдан — см. ℹ️.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| AR1 | P2 | `ISqlDialect.cs:143,149,689`; `SqlDialectBase.cs:33,34,423`; `ClickHouseDialect.cs:35,38,44`; `SqlFunctions.ClickHouse.cs:215-253`; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsArrayFunctions`/`SupportsArrayJoin` — абстрактные члены: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`); `MakeArrayFunction` — DIM, разрыва не создаёт | При заморозке внести: два флага, `ISqlDialect.MakeArrayFunction(string, string) -> string`, override'ы `SqlDialectBase`/`ClickHouseDialect` и 11 методов `ClickHouseFunctions` (ср. A3/JS2/GG1/XP7) |
| AR2 | ✅ (был **P1**) | `docs/providers/clickhouse.md:154`; `docs/ru/providers/clickhouse.md:154`; `docs/advanced/api-reference.md:62`; `docs/ru/advanced/api-reference.md:62`; `docs/guide/11-scalar-functions.md:399-400`; `docs/ru/guide/11-scalar-functions.md:406-407` | Документация противоречит реализованной поверхности и не обновлена в обеих языковых ветках (правило AGENTS.md «`docs/**` **и** `docs/ru/**`»): строка таблицы «Arrays / native JSON / extended scalars → not supported (PostgreSQL-only)» / «Массивы / нативный JSON / расширенные скаляры → не поддерживаются (только PostgreSQL)» прямо отрицает CH-массивы; описание `SqlFunctions.ClickHouse` в api-reference не перечисляет array-семейство; guide/11 содержит только `SqlFunctions.Postgres.array_*` | Добавить array-семейство (`array_join`/`length`/`has`/`index_of`/`has_any`/`has_all`/`array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`) в описание `ClickHouse` (api-reference EN+RU), строку таблицы и guide/11 (EN+RU) — одним изменением |
| AR3 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-14`; `Query/SqlFunctions.cs:41-51`; `Visitors/ArraySqlTranslator.cs:5-18` | Классовые `<summary>` не перечисляют новое array-семейство (публичные `ClickHouseFunctions`/`SqlFunctions.ClickHouse`) и всё ещё описывают `ArraySqlTranslator` как PG-only (`SupportsArrays`/`CommonFunctions`); у array-возвращающих методов (`array_sort`/`array_reverse`/`array_distinct`/`split_by_char`) док не оговаривает «только вложенно» | Дополнить оба публичных `<summary>` перечнем array-функций и обновить summary `ArraySqlTranslator` (CH + `SupportsArrayFunctions`/`SupportsArrayJoin`); вернуть оговорку о вложенном использовании (ср. guide/11) |
| AR4 | P2 | `ISqlDialect.cs:689` vs `SqlDialectBase.cs:33,34,423` | `MakeArrayFunction` — DIM с identity-телом, тогда как два соседних новых флага абстрактны, а родственный `MakeJsonExtract` — abstract+base.virtual; в контракте смешаны две политики совместимости | **Принято:** identity — безопасный default для не-CH реализаторов, а CH всё равно override'ит; зафиксировать решение в списке заморозки (продолжение JS2) либо унифицировать политику флагов через default interface properties `=> false` |

ℹ️ **Наблюдения (фикс не требуется).**

- **Промоушен в `CommonFunctions` не нужен.** `ClickHouseFunctions` не наследует `PostgresFunctions`;
  нативные имена/семантика различаются (`length` vs `cardinality`, `has` vs `@>`, `indexOf` vs
  `array_position`, `arrayStringConcat` vs `array_to_string`, `arrayJoin` без PG-аналога), а CH-поверхность
  работает по array-колонкам, PG — по array-параметрам. Правило «≥2 провайдера выражают одно и то же →
  `CommonFunctions`» применимо только к идентичной C#-поверхности (как `any_agg`/session-info/uuid/iif), так
  что дублирование здесь осознанное.
- **`MakeArrayFunction`/флаги не могут быть `internal`.** `ISqlDialect` реализуется сборками-провайдерами
  (`nextorm.clickhouse`), поэтому члены обязаны оставаться публичными — та же причина, по которой публичны
  `Paging`/`ISqlDialect.MakePage`. Ограничение «только PostgreSQL/ClickHouse» выражается флагом, а не видимостью.
- **`array_join<T>(T[]) -> T` — корректная модель row-expansion.** Возврат элемента (а не `T[]`) согласован с
  ClickHouse `arrayJoin` и исключает попытку материализовать массив; array-возвращающие `array_sort`/
  `array_reverse`/`array_distinct`/`split_by_char` предназначены только для вложенного использования (см. AR3).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
`find -name 'PublicAPI*.txt'` — пусто (подтверждает AR1); XML-`<summary>` у 11 array-методов, обоих флагов
и `MakeArrayFunction` (интерфейс/база/ClickHouse) присутствуют; Приложение A (45) без изменений. Тесты в
этом проходе: clickhouse **122/122**, postgres **177/177** (0 failed); ссылки — по разделу
`code-smells-review.md`, «ClickHouse массивы и `arrayJoin`».

### ClickHouse `ARRAY JOIN` (клауза уровня `FROM`) (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые члены:

- `EntityBuilder<TEntity>.ArrayJoin<TArray>(Expression<Func<TEntity,TArray>>) -> EntityBuilder<TEntity>`
  (`Builders/EntityBuilder.cs:291-292`) и `LeftArrayJoin<TArray>(...)` (`:299-300`) — XML `<summary>` есть;
- 14 ковариантных `new`-перегрузок на `JoinedEntityBuilder<T1..T8>`
  (`Builders/Joins/JoinedEntityBuilder.cs:65-68,122-125,172-175,222-225,272-275,322-325,360-363`) —
  **без XML-доков** (AJ3);
- `public enum ArrayJoinKind { Inner = 0, Left = 1 }` (`Expressions/ArrayJoinKind.cs:8-13`) — XML `<summary>`
  есть у типа и обоих членов; имя повторяет ClickHouse (`ASTArrayJoin::Kind::Inner/Left`);
- `QueryCommand.ArrayJoinExpressions` (`Query/QueryCommand.cs:201`) и `ArrayJoinKind` (`:203`) — public;
  `QueryDefinition.ArrayJoins`/`ArrayJoinKind` (`:58-60`), `QueryCommand._arrayJoins`/`_preparedArrayJoin`
  (`:26-27`) — internal;
- `ISqlDialect.SupportsArrayJoinClause` (`DataContext/Dialect/ISqlDialect.cs:777-781`, abstract) +
  `ISqlDialect.MakeArrayJoin(ArrayJoinKind, IReadOnlyList<string>)` (`:782-787`, abstract);
  `SqlDialectBase` (`:153-161`, `virtual`/throw) и `ClickHouseDialect`
  (`src/nextorm.clickhouse/ClickHouseDialect.cs:270-278`, override) — XML `<summary>` есть у всех.

Новых публичных **типов** один — задокументированный `ArrayJoinKind` → Приложение A (45) без изменений.
Именование — конвенции соблюдены: `Supports*`/`Make*` как у соседних семейств, `ArrayJoinKind` не конфликтует
с BCL и не путается со скалярным `SupportsArrayJoin` (это флаг функции `arrayJoin`, `Clause` — флаг клаузы).
P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| AJ1 | P2 | `ISqlDialect.cs:777-787`; `SqlDialectBase.cs:153-161`; `ClickHouseDialect.cs:270-278`; `EntityBuilder.cs:291-300`; `ArrayJoinKind.cs:8-13`; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsArrayJoinClause`/`MakeArrayJoin` — абстрактные члены: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`) | При заморозке внести: `ArrayJoinKind` (+`Inner`/`Left`), оба fluent-метода и 14 `new`-перегрузок, `ISqlDialect.SupportsArrayJoinClause`/`MakeArrayJoin(ArrayJoinKind, IReadOnlyList<string>)`, override'ы `SqlDialectBase`/`ClickHouseDialect`, `QueryCommand.ArrayJoinExpressions`/`ArrayJoinKind` (ср. AR1/GG1/JS1/FM5). **Фаза 3:** `SupportsArrayJoinClause`/`MakeArrayJoin` удалены — вносить `IArrayJoinRenderer`/`ArrayJoinClause` (трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53). |
| AJ2 | P2 | `Query/QueryCommand.cs:201` | `public IReadOnlyList<Expression>? ArrayJoinExpressions => _preparedArrayJoin;` раскрывает **подготовленную** форму (`Expression[]`) публично, тогда как аналогичное `PreparedPreWhere` (`:205`) — `internal`, а `PreWhere` (`:196`) публично отдаёт исходную лямбду. Конкретный тип — массив: `(Expression[])cmd.ArrayJoinExpressions` позволяет мутировать вход план-ключа (тот же класс, что Находка 16/5 в `code-smells-review.md`), плюс утечка внутренней поверхности. Потребители — только внутри `nextorm.core` (`SqlBuilder.cs:105`, `InMemoryQueryBuilder.cs:98`, `QueryPlanEqualityComparer.cs:71,313`) | Сузить до `internal` (или переименовать в `PreparedArrayJoin` и оставить `internal`); если нужен публичный доступ — отдавать `Array.AsReadOnly(...)`/исходные `ArrayJoins` |
| AJ3 | P2 | `Builders/Joins/JoinedEntityBuilder.cs:65-68,122-125,172-175,222-225,272-275,322-325,360-363` | 14 `new ArrayJoin`/`LeftArrayJoin` не имеют XML-доков, тогда как соседние `Global`/`WithStrictness` помечены `/// <inheritdoc/>`; `CS1591` в `<NoWarn>` (`nextorm.core.csproj:9`, `nextorm.clickhouse.csproj:9`, …), поэтому сборка 0/0 и пробел не виден | Добавить `/// <inheritdoc/>` к каждой перегрузке (док базового метода уже содержит оговорку «элемент не привязан к CLR-члену») |
| AJ4 | P2 | `Expressions/ArrayJoinKind.cs:8-13`; `Query/QueryCommand.cs:203` | У enum нет члена «нет клаузы»: `ArrayJoinKind` по умолчанию `Inner`, поэтому у команды без `ARRAY JOIN` свойство возвращает `Inner` и не отличимо от настоящей `Inner`-клаузы без чтения `ArrayJoinExpressions`. С выражениями всё корректно; вопрос — только о выразительности (alpha, поверхность не заморожена) | По желанию — `None = 0` (и `Inner = 1`, `Left = 2`) либо XML-оговорка «значим только при непустом `ArrayJoinExpressions`» |
| AJ5 | P2 | `docs/providers/clickhouse.md:154`; `docs/ru/providers/clickhouse.md:154`; `docs/guide/11-scalar-functions.md:423,438,447`; `docs/ru/guide/11-scalar-functions.md:431,446,456`; `docs/advanced/api-reference.md` | Публичная док-поверхность EN/RU описывает только скаляр `SqlFunctions.ClickHouse.array_join(...)`/`arrayJoin(array)`, но не новую `FROM`-клаузу `EntityBuilder.ArrayJoin`/`LeftArrayJoin` (правило AGENTS.md «`docs/**` **и** `docs/ru/**`»); в `api-reference` семейства нет вовсе | Добавить `ArrayJoin`/`LeftArrayJoin` (+ `ArrayJoinKind`) в provider-таблицу ClickHouse, guide/11 и `api-reference` EN+RU одним изменением (ср. AR2) |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
`dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **129/129**;
`tests/nextorm.core.tests` — **160/160** (0 failed); XML-`<summary>` у `ArrayJoinKind` (тип + 2 члена), обоих
fluent-методов, `SupportsArrayJoinClause`/`MakeArrayJoin` (интерфейс/база/ClickHouse) и `ArrayJoinExpressions`
присутствует; `grep -l "ArrayJoin" docs` подтверждает AJ5 (в EN/RU-доках — только скаляр, клаузы нет);
Приложение A (45) без изменений; `find -name 'PublicAPI*.txt'` — пусто (подтверждает AJ1, Шаг 5 открыт).

### ClickHouse `ARRAY JOIN` — привязка элемента (`ArrayJoinElement`, вариант A1) (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые члены:

- `public interface IArrayJoinProjection` (`Expressions/ArrayJoinProjection.cs:9`) — маркер;
- `public class ArrayJoinProjection<TEntity, TElement> : IArrayJoinProjection` (`:21`) с `Item1` (`:24`) и
  `Element` (`:27`) — XML `<summary>` есть у типа и обоих свойств;
- `EntityBuilder<TEntity>.ArrayJoinElement<TElement>(Expression<Func<TEntity,TElement[]>>) ->
  EntityBuilder<ArrayJoinProjection<TEntity,TElement>>` (`Builders/EntityBuilder.cs:343`) и
  `LeftArrayJoinElement<TElement>(...)` (`:354`) — XML `<summary>`/`<typeparam>` есть, `<exception>` — нет (AJ9);
- внутренние `ArrayJoinNames` (`ElementAlias = "__nextorm_aj_element"` `:38`, `ElementMember` `:41`),
  `EntityBuilder.SourceEntityType` (`:96-102`), `BindArrayJoinElement` (`:103-104`), `ToArrayJoinElement`
  (`:357-393`), `CopyProjectionIndependentStateTo` (`:530-556`); проброс в `QueryDefinition.BindArrayJoinElement`
  (`Query/QueryDefinition.cs:65`), `QueryCommand.BindArrayJoinElement` (`Query/QueryCommand.cs:211`),
  `QueryCommand.Clone` (`Query/QueryCommand.Clone.cs:57`), план-ключ
  (`Query/QueryPlanEqualityComparer.cs:71,315`), рендер (`DataContext/SqlBuilder.cs:119-121`), трансляцию
  (`Visitors/MemberTranslator.cs:94-109`).

Новых публичных **типов** два — оба задокументированы → Приложение A (45) без изменений. P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| AJ6 | P2 | `Builders/EntityBuilder.cs:343,354` | Сигнатура `Expression<Func<TEntity, TElement[]>>` принимает **только CLR-массив**, тогда как соседний `ArrayJoin<TArray>` (`:291,299`) принимает `Expression<Func<TEntity, TArray>>` и валидирует произвольный `IEnumerable` (`:320`). Колонка, отображённая как `List<T>`/последовательность, не сможет использовать привязку элемента | Принять `Expression<Func<TEntity, IEnumerable<TElement>>>` (или добавить перегрузку), выровнявшись с `ArrayJoin<TArray>`; иначе явно оговорить «только массивы» в `<summary>` |
| AJ7 | P2 | `Expressions/ArrayJoinProjection.cs:9,21` | Маркер `IArrayJoinProjection` не входит в семейство `IProjection`, но носит то же имя `*Projection`: код, диспетчеризуемый по `IProjection` (маппинг, InMemory, `AliasFromProjectionVisitor`), молча его не увидит. Наследовать **нельзя**: `MemberTranslator.cs:111-119` на `IProjection`-ветке отрендерит `Item1.` как член проекции | Переименовать, убрав `Projection` (`ArrayJoinElement<TEntity,TElement>`/`IArrayJoinElement`), либо оставить и явно закрепить в `<summary>` «вне семейства `IProjection`» (текущий текст уже это фиксирует — достаточно ссылки) |
| AJ8 | P2 | `Expressions/ArrayJoinProjection.cs`; `Builders/EntityBuilder.cs:343,354`; `PublicAPI.*.txt` отсутствуют | Новые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт) | При заморозке внести `IArrayJoinProjection`, `ArrayJoinProjection<,>` (+`Item1`/`Element`), `ArrayJoinElement<TElement>`/`LeftArrayJoinElement<TElement>` (ср. AJ1/AR1) |
| AJ9 | P2 | `Builders/EntityBuilder.cs:336-355` (throw `:361-374`) | У обоих публичных методов нет `<exception>`-доков, хотя они бросают `NotSupportedException` (in-memory, `:362`) и `InvalidOperationException` (уже привязано `:365`, `Where`/`Having` до вызова `:368`, joined-запрос `:371`, смешение вида `:374`); соседний `WithStrictness` (`:402`) исключение документирует | Добавить `<exception cref="NotSupportedException">`/`<exception cref="InvalidOperationException">` с перечнем условий |

Публичная док-поверхность EN/RU (`docs/**` и `docs/ru/**`) новых методов не описывает — это расширение
AJ5 (правило AGENTS.md), нового номера не заводим; добавить `ArrayJoinElement`/`LeftArrayJoinElement` и
`ArrayJoinProjection<,>` вместе с клаузой AJ5. Сгенерированный SQL-алиас `__nextorm_aj_element` остаётся
`internal`-константой и в поверхность не входит — публично-значимого магического идентификатора нет.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
unit-наборы — core **161/161**, clickhouse **135/135**, postgres **179/179** (0 failed, Release, `--no-build`);
XML-`<summary>` у `IArrayJoinProjection`, `ArrayJoinProjection<,>` (тип + `Item1`/`Element`), обоих fluent-методов
и внутренних `SourceEntityType`/`BindArrayJoinElement`/`CopyProjectionIndependentStateTo` присутствует;
cref'ы `ArrayJoinProjection{TEntity, TElement}.Element` (`ArrayJoinProjection.cs:40`, `QueryDefinition.cs:63`,
`QueryCommand.cs:209`) разрешаются — сборка 0/0; `find -name 'PublicAPI*.txt'` — пусто (подтверждает AJ8);
`grep -rn "ArrayJoinElement" docs` — пусто (подтверждает расширение AJ5). Контейнерные интеграционные тесты
в этом проходе не перезапускались (по отчёту автора — 3/3 зелёные).

### PostgreSQL/ANSI query modifiers — `DISTINCT ON`/`TABLESAMPLE`/`WITH TIES`/row locking (точечный аудит 20.09.2026)

Публичная поверхность аддитивна (два новых public-типа + fluent-методы и dialect-хуки); переименований
в этом изменении нет. Новые члены:

- `public enum TablesampleMethod { System, Bernoulli }` (`Query/TablesampleMethod.cs:4`) и
  `public enum LockMode { Update, Share }` (`Query/LockMode.cs:4`) — `<summary>` у типа и **обоих**
  членов каждого;
- `EntityBuilder<TEntity>.DistinctOn<TResult>(Expression<Func<TEntity,TResult>>)` (`Builders/EntityBuilder.cs:493`),
  `.Tablesample(double, TablesampleMethod, double?)` (`:510`), `.ForUpdate()` (`:525`), `.ForShare()` (`:532`),
  `.WithTies()` (`:570`) — у всех `<summary>`; `<param>` нет у первых двух (только `<paramref>`);
- `Paging.WithTies` (`Builders/Paging.cs:39`) — `<summary>` есть; см. QM2;
- `ISqlDialect.SupportsDistinctOn`/`MakeDistinctOn` (`DataContext/Dialect/ISqlDialect.cs:758,763`),
  `SupportsTablesample`/`SupportsTablesampleMethod`/`MakeTablesample` (`:786,792,797`),
  `SupportsWithTies` (`:840`), `SupportsLocking`/`MakeLock` (`:832,834`), изменённый
  `MakeTop(int,bool,out string?)` (`:826`) — `<summary>` есть у всех, кроме полноты `<param>`/`<returns>`
  у `MakeTop` (QM4). Внутренние `DistinctOnClause`/`TablesampleClause`/`LockClause`, `QueryCommand.*`,
  `QueryDefinition.*` в поверхность не входят.

Два новых публичных типа задокументированы → Приложение A (45 недокументированных) не растёт; общее
число публичных типов 158→160. Полный пересчёт отражением — при закрытии Шага 5 (таблица §2 выше —
снимок 18.09.2026).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| QM1 | ✅ (был P1) | `Query/TablesampleMethod.cs:4` (и файл), `Builders/EntityBuilder.cs:510`, `ISqlDialect.cs:786,792,797`, `SqlDialectBase.cs:160,166,173`, `PostgresDialect.cs:91,94,97`, `SqlServerDialect.cs:54,57,60`, `QueryCommand.cs:207`, `QueryDefinition.cs:47` | `Tablesample`/`TablesampleMethod` — составное слово «table sample» слито в одно; Capitalization Conventions .NET требуют `TableSample`/`TableSampleMethod` (`Table` + `Sample`, как `TableName`). `DistinctOn`/`WithTies`/`LockMode` — корректны | Переименовать на месте (alpha): `TableSampleMethod`, `TableSampleClause`, `EntityBuilder.TableSample`, `SupportsTableSample`/`SupportsTableSampleMethod`/`MakeTableSample`; обновить тесты и `docs/**`+`docs/ru/**` (AGENTS.md) |
| QM2 | P2 | `Builders/Paging.cs:39` | Публичное bool-свойство `WithTies` без префикса `Is`/`Has`, тогда как соседние `IsEmpty`/`IsTop` (`:41-42`) его имеют; `Paging` — публичный контракт (`ISqlDialect.MakePage(Paging,…)`/`MakeTop`), поэтому имя входит в заморозку | `HasWithTies`/`IsWithTies`; либо оставить как SQL-зеркало и зафиксировать в `<summary>` (тогда — строка в «Отмечено, но менять не рекомендуется», ср. DSL-исключение) |
| QM3 | P2 | `ISqlDialect.cs:826`, `SqlDialectBase.cs:511`, `SqlServerDialect.cs:331` | `bool MakeTop(int limit, bool withTies, out string? topStmt)` — bool-возвращающий метод без `Try`-префикса; `bool`+`out` дублирует идиому nullable-возврата `GetPagingOrderBy` (`:846`). Смена сигнатуры — source-breaking для внешних реализаторов `ISqlDialect` (alpha; в репозитории — только `SqlDialectBase`/`SqlServerDialect`) | `TryMakeTop(...)` (Try-паттерн) либо `string? MakeTop(int limit, bool withTies)`; при заморозке учесть смену подписи в `PublicAPI.Unshipped.txt` |
| QM4 | P2 | `ISqlDialect.cs:821-826`; `Builders/EntityBuilder.cs:486-492,505-509`; `SqlDialectBase.cs:511`; `SqlServerDialect.cs:331` | `MakeTop` использует `<paramref name="withTies"/>`, но `<param>`/`<returns>`/`<paramref name="topStmt"/>` отсутствуют; `DistinctOn`/`Tablesample` ссылаются на `exp`/`percent`/`method`/`seed` без `<param>`; override `SqlDialectBase.MakeTop`/`SqlServerDialect.MakeTop` без `<summary>` (ср. U5/S2/LIM2) | Дописать `<param>`/`<returns>` и `<summary>` override'ов при закрытии Шага 5 |
| QM5 | P2 | `docs/guide/provider-specific/postgresql.md:85-89` + `docs/ru/guide/provider-specific/postgresql.md:86-90`; `docs/guide/provider-specific/overview.md:30` + RU `:31`; `docs/advanced/api-reference.md` (+RU); `docs/guide/05-sorting-and-paging.md`, `docs/guide/08-distinct.md` (+RU) | Провайдер-страница PostgreSQL **прямо противоречит коду**: `DISTINCT ON`, `LIMIT … WITH TIES`, `TABLESAMPLE`, `FOR UPDATE`/`FOR SHARE` перечислены в «Not yet supported», хотя реализованы; матрица `overview.md` и `advanced/api-reference.md`, гайды сортировки/`DISTINCT` новые модификаторы не упоминают (`rg` пуст). AGENTS.md требует `docs/**` **и** `docs/ru/**` | Перенести четыре модификатора из «Not yet supported» в перечень поддержанного, дополнить матрицу, api-reference и гайды; синхронно EN+RU |
| QM6 | P2 | `PublicAPI.*.txt` отсутствуют; `ISqlDialect.cs:758,763,786,792,797,826,832,834,840`; `Builders/EntityBuilder.cs:493,510,525,532,570`; `Builders/Paging.cs:39` | Новые/изменённые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). Абстрактные члены + смена `MakeTop` — source-breaking для внешних реализаторов `ISqlDialect` | Внести в `PublicAPI.Unshipped.txt` при заморозке: `TablesampleMethod`(+2), `LockMode`(+2), пять fluent-методов, `Paging.WithTies`, девять dialect-членов и изменённый `MakeTop` (даст пару RS0017/RS0016) |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; XML-`<summary>` у
`TablesampleMethod`/`LockMode` (тип + члены), пяти fluent-методов, `Paging.WithTies` и девяти dialect-членов
присутствует; у `MakeTop` — только `<summary>`+`<paramref>` (QM4); `find -name 'PublicAPI*.txt'` — пусто
(подтверждает QM6); unit-наборы Release `--no-build` — core **165/165**, postgres **194/194**,
sqlserver **185/185**, mysql **49/49**, mariadb **14/14**, sqlite **209/209**, clickhouse **137/137**.

### PostgreSQL text-search и temporal tables (точечный аудит 20.09.2026)

Публичная поверхность аддитивна (два новых public-типа + fluent-метод и dialect-хуки); переименований
нет. Новые члены:

- `PostgresFunctions.to_tsvector`/`to_tsquery`/`plainto_tsquery`/`phraseto_tsquery`/`websearch_to_tsquery`
  (`string`→`string?`), `ts_match(string?, string?)`→`bool`, `ts_rank(string?, string?)`→`double?`,
  `ts_headline(string, string?)`→`string?` (`Query/SqlFunctions.Postgres.cs:415-439`) — `<summary>` у всех,
  `<param>`/`<returns>` нет (стиль всего файла);
- `public enum TemporalKind { AsOf, Between, FromTo, ContainedIn, All }` (`Query/TemporalKind.cs:4`) и
  `public sealed class TemporalClause` (`Query/TemporalClause.cs:8`) — `<summary>` у типа и **всех** членов
  (5 фабрик + `Kind`/`From`/`To`), у фабрик — `<paramref>` без `<param>`;
- `EntityBuilder<TEntity>.ForSystemTime(TemporalClause)` (`Builders/EntityBuilder.cs:535`) — `<summary>`+`<param>`;
- `ISqlDialect.SupportsTextSearchFunctions` (`:251`), `SupportsTemporalTable` (`:812`),
  `SupportsTemporalKind(TemporalKind)` (`:818`), `MakeTemporalTable(TemporalClause)` (`:823`) и базовые
  `SqlDialectBase` (`:54,179,185,191`) плюс override'ы `SqlServerDialect` (`:69,72`), `MariaDbDialect`
  (`:42,45`), `PostgresDialect` (`:111`) — `<summary>` у всех; `MakeTemporalTable`/`SupportsTemporalKind`
  без `<param>`; override `MakeTemporalTable` без `<inheritdoc/>`.

Наименования `TemporalKind`/`TemporalClause`/`ForSystemTime` — корректны (PascalCase, глагольная фраза);
`ts_*` — SQL-зеркало, остаётся под DSL-исключением («Отмечено, но менять не рекомендуется»). Два новых
типа задокументированы → Приложение A (45) не растёт, поверхность §2 160→162. Документация обновлена
синхронно EN+RU (`docs/guide/provider-specific/postgresql.md` (+RU), `docs/guide/provider-specific/sqlserver.md`
(+RU), `docs/advanced/api-reference.md` (+RU), `docs/providers/overview.md` (+RU)) — нарушения класса QM5 нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| QM7 | P2 | `Query/SqlFunctions.Postgres.cs:415-439`; `Query/TemporalClause.cs:27-51`; `ISqlDialect.cs:818,823`; `SqlDialectBase.cs:185,191` | У новых публичных членов только `<summary>`: `ts_*`-методы ссылаются на `document`/`query`/`tsvector`/`tsquery` без `<param>`; `MakeTemporalTable`/`SupportsTemporalKind` — без `<param>`; override `SqlDialectBase.MakeTemporalTable` — без `<inheritdoc/>` (ср. QM4) | Дописать `<param>`/`<returns>` и `<inheritdoc/>` overrid'ов при закрытии Шага 5 |
| QM8 | P2 | `PublicAPI.*.txt` отсутствуют; `Query/TemporalKind.cs:4`; `Query/TemporalClause.cs:8`; `Builders/EntityBuilder.cs:535`; `ISqlDialect.cs:251,812,818,823`; `Query/SqlFunctions.Postgres.cs:415-439` | Новые публичные члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт) | Внести в `PublicAPI.Unshipped.txt` при заморозке: `TemporalKind`(+5), `TemporalClause`(тип + `Kind`/`From`/`To` + 5 фабрик), `ForSystemTime`, четыре dialect-члена и восемь `PostgresFunctions`-методов |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `<summary>` у `TemporalKind`
(тип + члены), `TemporalClause` (тип + члены), `ForSystemTime`, восьми `ts_*`-методов и четырёх dialect-членов
присутствует; у `MakeTemporalTable`/`SupportsTemporalKind`/`ts_*` — только `<summary>` (QM7); `rg --files
-g 'PublicAPI*.txt'` — пусто (QM8); unit (Release, `--no-build`, этот проход): core **166/166**,
postgres **198/198**, sqlserver **191/191**, mariadb **16/16**, sqlite **211/211**, clickhouse **139/139**.

### PostgreSQL `round`/`extract`/`date_part`/`setseed`/`digest`+`sha256`/`array_shuffle`+`array_sample` (точечный аудит 20.09.2026)

Публичная поверхность **аддитивна**: переименований нет, новых public-типов нет (Приложение A — 45 без
изменений; счётчик типов §2 не растёт). Новые члены:

- `CommonFunctions.extract(string part, DateTime? value) -> int?` (`Query/SqlFunctions.cs:333`),
  `CommonFunctions.date_part(string part, DateTime? value) -> double?` (`:342`) — `<summary>` есть,
  `<param>`/`<returns>` нет (стиль файла);
- `PostgresFunctions.array_shuffle<T>(T[] array) -> T[]` (`Query/SqlFunctions.Postgres.cs:91`),
  `array_sample<T>(T[] array, int n) -> T[]` (`:94`), `setseed(double? seed) -> double?` (`:290`),
  `digest(string? data, string? type) -> byte[]?` (`:359`), `digest(byte[]? data, string? type) -> byte[]?`
  (`:367`), `sha256(byte[]? data) -> byte[]?` (`:374`) — `<summary>` у всех;
- `ISqlDialect.SupportsRandomSeed` (`DataContext/Dialect/ISqlDialect.cs:249`), `SupportsCryptoFunctions`
  (`:257`), `SupportsDatePart(string)` (`:662`) — **абстрактные** (source-breaking для внешних
  реализаторов); `MakeMathFunction(string, IReadOnlyList<string>, IReadOnlyList<Type>)` (`:678`) — **DIM**;
- `SqlDialectBase.SupportsRandomSeed` (`:53`), `SupportsCryptoFunctions` (`:55`), `SupportsDatePart`
  (`:424`), `MakeMathFunction` 3-арг. (`:433`);
- override'ы: `PostgresDialect.SupportsDatePart` (`:75`), `MakeDatePart` (`:79`, новый override),
  `SupportsRandomSeed` (`:119`), `SupportsCryptoFunctions` (`:122`), `MakeMathFunction` 3-арг. (`:180`);
  `MySqlDialect` (`:194,206`), `SqliteDialect` (`:149,168`), `SqlServerDialect` (`:181,192`),
  `ClickHouseDialect` (`:336,348`) — `SupportsDatePart` у всех пяти, `MakeDatePart` изменён у четырёх.

**Контракты.** Корректны: `extract`→`int?` (целые части), `date_part`→`double?` (только `epoch`),
`digest`/`sha256`→`byte[]?` (PG `bytea`), `array_shuffle`/`array_sample`→`T[]`. Отмечено:
`setseed`→`double?` при PG `void` — контракт «всегда `null`» описан в `<summary>`, но `<returns>` нет;
`digest(null, "sha256")` неоднозначен между `string?`/`byte[]?` на стороне вызова (транслятор
диспетчеризует по имени и рендерит одинаково — рантайм-эффекта нет). `date_part` намеренно принимает
только `epoch` (числовой аналог integer-`extract`); 3-арг. `MakeMathFunction` — **не** мёртвый дубль
`MakeTextJsonFunction(string)`: DIM снимает source-breaking, `virtual` в базе нужен для override
`PostgresDialect`. FQN списка заморозки сверены с XML-документацией собранной сборки
(`M:NextORM.Core.CommonFunctions.extract`, `M:NextORM.Core.PostgresFunctions.array_shuffle`).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| RD1 | P2 | `PostgresDialect.cs:180-183`, `SqlDialectBase.cs:424` | Новые публичные члены без XML-`<summary>`: override `PostgresDialect.MakeMathFunction(string, IReadOnlyList<string>, IReadOnlyList<Type>)` задокументирован `//`, `SqlDialectBase.SupportsDatePart(string)` — без доки; четыре изменённых `MakeDatePart`-override'а (mysql/sqlite/sqlserver/clickhouse) тоже `//`. `NoWarn=CS1591` во всех 7 библиотечных `.csproj` скрывает пропуск (ср. N2/U5/Q4/A2) | Добавить `<summary>` при закрытии Шага 5 |
| RD2 | P2 | `ISqlDialect.cs:249,257,662`; `SqlDialectBase.cs:53,55,424,433`; `PostgresDialect.cs:75,79,119,122,180`; `MySqlDialect.cs:194,206`; `SqliteDialect.cs:149,168`; `SqlServerDialect.cs:181,192`; `ClickHouseDialect.cs:336,348` | Новые/изменённые члены не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). `SupportsRandomSeed`/`SupportsCryptoFunctions`/`SupportsDatePart` — абстрактные: source-breaking для внешних реализаторов `ISqlDialect` (в репозитории реализует только `SqlDialectBase`); `MakeMathFunction` 3-арг. — DIM, разрыва не создаёт | Внести в `PublicAPI.Unshipped.txt` при заморозке (полный список ниже; ср. C/U4/Q3/A3/J1/D1/UG2/S1) |
| RD3 | P2 | `Query/SqlFunctions.Postgres.cs:285-290`; `Query/SqlFunctions.cs:333,342`; `Query/SqlFunctions.Postgres.cs:359,367` | `setseed` возвращает `double?` при PG-`void` — `<summary>` описывает «always null», но `<returns>`/`<remarks>` нет; `digest(null, "sha256")` неоднозначен между перегрузками | Дописать `<returns>` у `setseed`; неоднозначность перегрузок оставить (SQL одинаков) |
| RD4 | P2 | `docs/advanced/api-reference.md:59` + `docs/ru/advanced/api-reference.md:59` | Строка `CommonFunctions` перечисляет `date_trunc`/`date_add`/`date_diff`/`date_from_parts`/`end_of_month`, но не новые `extract`/`date_part` (EN и RU синхронно). Гайд `docs/guide/11-scalar-functions.md` (+RU) и `docs/providers/postgres.md` (+RU) обновлены — расхождения класса QM5 нет | Дополнить перечисление `extract`/`date_part` в обеих ветках |
| RD5 | P2 | `Query/SqlFunctions.cs:342`; `BuiltinFunctionTranslator.cs:292-293` | `date_part` отклоняет все части, кроме `epoch`, тогда как в PostgreSQL `date_part('year', x)` — валидный double-аналог `extract`. Сужение описано в XML, но расходится с PG-ожиданием | Оставить нормализованную форму и явнее сослаться на `extract` в XML/прозе либо расширить `date_part` до числовых частей |

**Обновление (todo-pg, аудит применён).** RD1 закрыта: `<summary>` добавлены `SqlDialectBase.SupportsDatePart`,
`PostgresDialect.MakeMathFunction(...3-арг.)` и четырём `MakeDatePart`-override'ам
(mysql/sqlite/sqlserver/clickhouse). RD3 закрыта: у `setseed` появился `<returns>`. RD4 закрыта: в
`docs/advanced/api-reference.md` (+RU) добавлены `extract`/`date_part`, `array_shuffle`/`array_sample`,
`digest`/`sha256`, `setseed`. RD5 закрыта: у `date_part` добавлен `<remarks>` со ссылкой на `extract`.
RD2 остаётся открытой — `PublicAPI.Unshipped.txt` не заведён (Шаг 5 вне рамок этого пункта), подписи
для заморозки ниже сохранены. Слияние `todo-ch` добавляет к RD2 ClickHouse-часть поверхности
(24 `to_*`, `generate_random` ×2, `IGenerateRandomRow`, 4 члена `ISqlDialect`/`SqlDialectBase` и 4
переопределения в `ClickHouseDialect`); её подписи дописаны в тот же блок ниже (`# --- продолжение RD2`),
отдельная находка не заводится — см. раздел «Слияние `todo-ch`…» после OJW1.

**Шаг 5 — точные подписи для `PublicAPI.Unshipped.txt` (RD2)** (формат Roslyn PublicAPI, `#nullable enable`):

```text
NextORM.Core.ISqlDialect.SupportsRandomSeed.get -> bool
NextORM.Core.ISqlDialect.SupportsCryptoFunctions.get -> bool
NextORM.Core.ISqlDialect.SupportsDatePart(string! part) -> bool
NextORM.Core.ISqlDialect.MakeMathFunction(string! name, System.Collections.Generic.IReadOnlyList<string!>! args, System.Collections.Generic.IReadOnlyList<System.Type!>! argTypes) -> string!
NextORM.Core.SqlDialectBase.SupportsRandomSeed.get -> bool
NextORM.Core.SqlDialectBase.SupportsCryptoFunctions.get -> bool
NextORM.Core.SqlDialectBase.SupportsDatePart(string! part) -> bool
NextORM.Core.SqlDialectBase.MakeMathFunction(string! name, System.Collections.Generic.IReadOnlyList<string!>! args, System.Collections.Generic.IReadOnlyList<System.Type!>! argTypes) -> string!
NextORM.Postgres.PostgresDialect.SupportsRandomSeed.get -> bool
NextORM.Postgres.PostgresDialect.SupportsCryptoFunctions.get -> bool
NextORM.Postgres.PostgresDialect.SupportsDatePart(string! part) -> bool
NextORM.Postgres.PostgresDialect.MakeDatePart(string! part, string! value) -> string!
NextORM.Postgres.PostgresDialect.MakeMathFunction(string! name, System.Collections.Generic.IReadOnlyList<string!>! args, System.Collections.Generic.IReadOnlyList<System.Type!>! argTypes) -> string!
NextORM.MySql.MySqlDialect.SupportsDatePart(string! part) -> bool
NextORM.Sqlite.SqliteDialect.SupportsDatePart(string! part) -> bool
NextORM.SqlServer.SqlServerDialect.SupportsDatePart(string! part) -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsDatePart(string! part) -> bool
NextORM.Core.CommonFunctions.extract(string! part, System.DateTime? value) -> int?
NextORM.Core.CommonFunctions.date_part(string! part, System.DateTime? value) -> double?
NextORM.Core.PostgresFunctions.array_shuffle<T>(T[]! array) -> T[]!
NextORM.Core.PostgresFunctions.array_sample<T>(T[]! array, int n) -> T[]!
NextORM.Core.PostgresFunctions.setseed(double? seed) -> double?
NextORM.Core.PostgresFunctions.digest(string? data, string? type) -> byte[]?
NextORM.Core.PostgresFunctions.digest(byte[]? data, string? type) -> byte[]?
NextORM.Core.PostgresFunctions.sha256(byte[]? data) -> byte[]?
# --- продолжение RD2: ClickHouse-поверхность слияния todo-ch (20.09.2026) ---
NextORM.Core.ISqlDialect.SupportsDateConversionFunctions.get -> bool
NextORM.Core.ISqlDialect.SupportsStringSplit.get -> bool
NextORM.Core.ISqlDialect.MakeDateConversion(string! name, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.Core.ISqlDialect.MakeStringSplit(string! separator, string! value) -> string!
NextORM.Core.SqlDialectBase.SupportsDateConversionFunctions.get -> bool
NextORM.Core.SqlDialectBase.SupportsStringSplit.get -> bool
NextORM.Core.SqlDialectBase.MakeDateConversion(string! name, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.Core.SqlDialectBase.MakeStringSplit(string! separator, string! value) -> string!
NextORM.ClickHouse.ClickHouseDialect.SupportsDateConversionFunctions.get -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsStringSplit.get -> bool
NextORM.ClickHouse.ClickHouseDialect.MakeDateConversion(string! name, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.ClickHouse.ClickHouseDialect.MakeStringSplit(string! separator, string! value) -> string!
NextORM.Core.ClickHouseFunctions.to_date<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_date_time<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_date32<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_year<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_quarter<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_month<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_day_of_month<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_day_of_week<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_day_of_year<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_hour<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_minute<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_second<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_start_of_year<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_quarter<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_month<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_week<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_day<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_hour<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_minute<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_start_of_second<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_monday<T>(T? value) -> System.DateTime?
NextORM.Core.ClickHouseFunctions.to_yyyymm<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_yyyymmdd<T>(T? value) -> int?
NextORM.Core.ClickHouseFunctions.to_unix_timestamp<T>(T? value) -> long?
NextORM.Core.ClickHouseFunctions.generate_random() -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IGenerateRandomRow!>!
NextORM.Core.ClickHouseFunctions.generate_random(long seed) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IGenerateRandomRow!>!
NextORM.Core.SqlFunctions.IGenerateRandomRow
NextORM.Core.SqlFunctions.IGenerateRandomRow.Id.get -> long
NextORM.Core.SqlFunctions.IGenerateRandomRow.Id.set -> void
NextORM.Core.SqlFunctions.IGenerateRandomRow.Value.get -> double
NextORM.Core.SqlFunctions.IGenerateRandomRow.Value.set -> void
NextORM.Core.SqlFunctions.IGenerateRandomRow.Name.get -> string?
NextORM.Core.SqlFunctions.IGenerateRandomRow.Name.set -> void
# --- продолжение RD2: PostgreSQL наборные функции (20.09.2026) ---
NextORM.Core.SqlFunctions.IRegexpMatchesRow
NextORM.Core.SqlFunctions.IRegexpMatchesRow.Matches.get -> string![]!
NextORM.Core.SqlFunctions.IRegexpMatchesRow.Matches.set -> void
NextORM.Core.SqlFunctions.IRegexpSplitToTableRow
NextORM.Core.SqlFunctions.IRegexpSplitToTableRow.Value.get -> string?
NextORM.Core.SqlFunctions.IRegexpSplitToTableRow.Value.set -> void
NextORM.Core.SqlFunctions.IJsonArrayElementsRow
NextORM.Core.SqlFunctions.IJsonArrayElementsRow.Value.get -> string?
NextORM.Core.SqlFunctions.IJsonArrayElementsRow.Value.set -> void
NextORM.Core.SqlFunctions.IJsonbEachRow
NextORM.Core.SqlFunctions.IJsonbEachRow.Key.get -> string?
NextORM.Core.SqlFunctions.IJsonbEachRow.Key.set -> void
NextORM.Core.SqlFunctions.IJsonbEachRow.Value.get -> string?
NextORM.Core.SqlFunctions.IJsonbEachRow.Value.set -> void
NextORM.Core.SqlFunctions.IJsonObjectKeysRow
NextORM.Core.SqlFunctions.IJsonObjectKeysRow.Key.get -> string?
NextORM.Core.SqlFunctions.IJsonObjectKeysRow.Key.set -> void
NextORM.Core.SqlFunctions.IJsonPathQueryRow
NextORM.Core.SqlFunctions.IJsonPathQueryRow.Value.get -> string?
NextORM.Core.SqlFunctions.IJsonPathQueryRow.Value.set -> void
NextORM.Core.SqlFunctions.ITsStatRow
NextORM.Core.SqlFunctions.ITsStatRow.Word.get -> string?
NextORM.Core.SqlFunctions.ITsStatRow.Word.set -> void
NextORM.Core.SqlFunctions.ITsStatRow.Ndoc.get -> int?
NextORM.Core.SqlFunctions.ITsStatRow.Ndoc.set -> void
NextORM.Core.SqlFunctions.ITsStatRow.Nentry.get -> int?
NextORM.Core.SqlFunctions.ITsStatRow.Nentry.set -> void
NextORM.Core.PostgresFunctions.regexp_matches(string? source, string? pattern) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpMatchesRow!>!
NextORM.Core.PostgresFunctions.regexp_matches(string? source, string? pattern, string? flags) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpMatchesRow!>!
NextORM.Core.PostgresFunctions.regexp_split_to_table(string? source, string? pattern) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpSplitToTableRow!>!
NextORM.Core.PostgresFunctions.regexp_split_to_table(string? source, string? pattern, string? flags) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IRegexpSplitToTableRow!>!
NextORM.Core.PostgresFunctions.jsonb_array_elements(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonArrayElementsRow!>!
NextORM.Core.PostgresFunctions.jsonb_array_elements_text(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonArrayElementsRow!>!
NextORM.Core.PostgresFunctions.jsonb_each(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonbEachRow!>!
NextORM.Core.PostgresFunctions.jsonb_each_text(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonbEachRow!>!
NextORM.Core.PostgresFunctions.jsonb_object_keys(object? json) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonObjectKeysRow!>!
NextORM.Core.PostgresFunctions.jsonb_path_query(object? json, string? path) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.IJsonPathQueryRow!>!
NextORM.Core.PostgresFunctions.ts_stat(string? query) -> System.Linq.IQueryable<NextORM.Core.SqlFunctions.ITsStatRow!>!
NextORM.Core.PostgresFunctions.jsonpath(string? path) -> string?
NextORM.Postgres.PostgresDialect.WrapTableFunction(string! name, string! call) -> string!
# --- продолжение RD2: ClickHouse sequence/array батча todo-ch2 (20.09.2026) ---
NextORM.Core.ISqlDialect.SupportsSequenceAggregates.get -> bool
NextORM.Core.ISqlDialect.MakeSequenceAggregate(string! name, string? parameters, string! arguments) -> string!
NextORM.Core.SqlDialectBase.SupportsSequenceAggregates.get -> bool
NextORM.Core.SqlDialectBase.MakeSequenceAggregate(string! name, string? parameters, string! arguments) -> string!
NextORM.ClickHouse.ClickHouseDialect.SupportsSequenceAggregates.get -> bool
NextORM.ClickHouse.ClickHouseDialect.MakeSequenceAggregate(string! name, string? parameters, string! arguments) -> string!
NextORM.Core.ClickHouseFunctions.window_funnel<TTime>(long window, TTime? timestamp, params bool[]! conditions) -> int
NextORM.Core.ClickHouseFunctions.sequence_match<TTime>(string? pattern, TTime? timestamp, params bool[]! conditions) -> int
NextORM.Core.ClickHouseFunctions.retention(params bool[]! conditions) -> int[]!
NextORM.Core.ClickHouseFunctions.range(long end) -> long[]!
NextORM.Core.ClickHouseFunctions.range(long start, long end) -> long[]!
NextORM.Core.ClickHouseFunctions.range(long start, long end, long step) -> long[]!
NextORM.Core.ClickHouseFunctions.array_enumerate<T>(T[]! array) -> long[]!
NextORM.Core.ClickHouseFunctions.array_cum_sum<T>(T[]! array) -> T[]!
NextORM.Core.ClickHouseFunctions.array_slice<T>(T[]! array, long offset) -> T[]!
NextORM.Core.ClickHouseFunctions.array_slice<T>(T[]! array, long offset, long length) -> T[]!
NextORM.Core.ClickHouseFunctions.array_push_back<T>(T[]! array, T element) -> T[]!
# --- продолжение RD2: Batch 3 — именованные окна / XML / ClickHouse multi_if (20.09.2026) ---
NextORM.Core.ISqlDialect.SupportsXmlFunctions.get -> bool
NextORM.Core.ISqlDialect.SupportsXmlFunction(string! name) -> bool
NextORM.Core.ISqlDialect.MakeXmlFunction(string! name, string! operand, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.Core.SqlDialectBase.SupportsXmlFunctions.get -> bool
NextORM.Core.SqlDialectBase.SupportsXmlFunction(string! name) -> bool
NextORM.Core.SqlDialectBase.MakeXmlFunction(string! name, string! operand, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.SqlServer.SqlServerDialect.SupportsXmlFunctions.get -> bool
NextORM.SqlServer.SqlServerDialect.SupportsXmlFunction(string! name) -> bool
NextORM.SqlServer.SqlServerDialect.MakeXmlFunction(string! name, string! operand, System.Collections.Generic.IReadOnlyList<string!>! args) -> string!
NextORM.Core.SqlServerFunctions.xml_value<T>(string? xml, string? xpath, string? sqlType) -> T?
NextORM.Core.SqlServerFunctions.xml_query(string? xml, string? xpath) -> string?
NextORM.Core.SqlServerFunctions.xml_exist(string? xml, string? xpath) -> bool
NextORM.Core.ISqlDialect.SupportsNamedWindows.get -> bool
NextORM.Core.ISqlDialect.SupportsWindowFrameGroups.get -> bool
NextORM.Core.ISqlDialect.SupportsWindowFrameExclusion.get -> bool
NextORM.Core.ISqlDialect.SupportsInFrameWindowFunctions.get -> bool
NextORM.Core.ISqlDialect.SupportsMultiIf.get -> bool
NextORM.Core.ISqlDialect.MakeMultiIf(System.Collections.Generic.IReadOnlyList<string!>! arguments, System.Type! resultType) -> string!
NextORM.Core.SqlDialectBase.SupportsNamedWindows.get -> bool
NextORM.Core.SqlDialectBase.SupportsWindowFrameGroups.get -> bool
NextORM.Core.SqlDialectBase.SupportsWindowFrameExclusion.get -> bool
NextORM.Core.SqlDialectBase.SupportsInFrameWindowFunctions.get -> bool
NextORM.Core.SqlDialectBase.SupportsMultiIf.get -> bool
NextORM.Core.SqlDialectBase.MakeMultiIf(System.Collections.Generic.IReadOnlyList<string!>! arguments, System.Type! resultType) -> string!
NextORM.Postgres.PostgresDialect.SupportsNamedWindows.get -> bool
NextORM.Postgres.PostgresDialect.SupportsWindowFrameGroups.get -> bool
NextORM.Postgres.PostgresDialect.SupportsWindowFrameExclusion.get -> bool
NextORM.Sqlite.SqliteDialect.SupportsNamedWindows.get -> bool
NextORM.Sqlite.SqliteDialect.SupportsWindowFrameGroups.get -> bool
NextORM.Sqlite.SqliteDialect.SupportsWindowFrameExclusion.get -> bool
NextORM.MySql.MySqlDialect.SupportsNamedWindows.get -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsNamedWindows.get -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsWindowFrameGroups.get -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsInFrameWindowFunctions.get -> bool
NextORM.ClickHouse.ClickHouseDialect.SupportsMultiIf.get -> bool
NextORM.ClickHouse.ClickHouseDialect.MakeMultiIf(System.Collections.Generic.IReadOnlyList<string!>! arguments, System.Type! resultType) -> string!
NextORM.Core.NamedWindowOrderKey
NextORM.Core.NamedWindowOrderKey.Expression.get -> System.Linq.Expressions.LambdaExpression!
NextORM.Core.NamedWindowOrderKey.Direction.get -> NextORM.Core.OrderDirection
NextORM.Core.WindowDefinition
NextORM.Core.WindowDefinition.Name.get -> string!
NextORM.Core.WindowDefinition.PartitionBy.get -> System.Collections.Generic.IReadOnlyList<System.Linq.Expressions.LambdaExpression!>!
NextORM.Core.WindowDefinition.OrderBy.get -> System.Collections.Generic.IReadOnlyList<NextORM.Core.NamedWindowOrderKey!>!
NextORM.Core.WindowDefinition.Frame.get -> NextORM.Core.WindowFrame?
NextORM.Core.WindowFrameExclusion
NextORM.Core.WindowFrameExclusion.NoOthers = 0 -> NextORM.Core.WindowFrameExclusion
NextORM.Core.WindowFrameExclusion.CurrentRow = 1 -> NextORM.Core.WindowFrameExclusion
NextORM.Core.WindowFrameExclusion.Group = 2 -> NextORM.Core.WindowFrameExclusion
NextORM.Core.WindowFrameExclusion.Ties = 3 -> NextORM.Core.WindowFrameExclusion
NextORM.Core.WindowFrameType.Groups = 2 -> NextORM.Core.WindowFrameType
NextORM.Core.WindowFrame.Exclusion.get -> NextORM.Core.WindowFrameExclusion?
NextORM.Core.WindowFrame.Groups(NextORM.Core.WindowFrameBound! start, NextORM.Core.WindowFrameBound! end) -> NextORM.Core.WindowFrame!
NextORM.Core.WindowFrame.Groups(int preceding, int following) -> NextORM.Core.WindowFrame!
NextORM.Core.WindowFrame.WithExclusion(NextORM.Core.WindowFrameExclusion exclusion) -> NextORM.Core.WindowFrame!
NextORM.Core.WindowFunction<T>.Over(string! windowName) -> T
NextORM.Core.QueryDefinition.Windows.get -> System.Collections.Generic.IReadOnlyList<NextORM.Core.WindowDefinition!>?
NextORM.Core.QueryDefinition.Windows.init -> void
NextORM.Core.QueryCommand.Windows.get -> System.Collections.Generic.IReadOnlyList<NextORM.Core.WindowDefinition!>?
NextORM.Core.EntityBuilder<TEntity>.Window(string! name, System.Linq.Expressions.Expression<System.Func<TEntity, object?>>![]? partitionBy = null, NextORM.Core.NamedWindowOrderKey![]? orderBy = null, NextORM.Core.WindowFrame? frame = null) -> NextORM.Core.EntityBuilder<TEntity>!
NextORM.Core.EntityBuilder<TEntity>.Asc(System.Linq.Expressions.Expression<System.Func<TEntity, object?>>! expression) -> NextORM.Core.NamedWindowOrderKey!
NextORM.Core.EntityBuilder<TEntity>.Desc(System.Linq.Expressions.Expression<System.Func<TEntity, object?>>! expression) -> NextORM.Core.NamedWindowOrderKey!
NextORM.Core.ClickHouseFunctions.MultiIfBranch<T>
NextORM.Core.ClickHouseFunctions.when<T>(bool condition, T? value) -> NextORM.Core.ClickHouseFunctions.MultiIfBranch<T>!
NextORM.Core.ClickHouseFunctions.otherwise<T>(T? value) -> NextORM.Core.ClickHouseFunctions.MultiIfBranch<T>!
NextORM.Core.ClickHouseFunctions.multi_if<TResult>(params NextORM.Core.ClickHouseFunctions.MultiIfBranch<TResult>![]! branches) -> TResult?
NextORM.Core.ClickHouseFunctions.lag_in_frame<T>(T? value) -> NextORM.Core.WindowFunction<T?>!
NextORM.Core.ClickHouseFunctions.lag_in_frame<T>(T? value, int offset) -> NextORM.Core.WindowFunction<T?>!
NextORM.Core.ClickHouseFunctions.lag_in_frame<T>(T? value, int offset, T? defaultValue) -> NextORM.Core.WindowFunction<T?>!
NextORM.Core.ClickHouseFunctions.lead_in_frame<T>(T? value) -> NextORM.Core.WindowFunction<T?>!
NextORM.Core.ClickHouseFunctions.lead_in_frame<T>(T? value, int offset) -> NextORM.Core.WindowFunction<T?>!
NextORM.Core.ClickHouseFunctions.lead_in_frame<T>(T? value, int offset, T? defaultValue) -> NextORM.Core.WindowFunction<T?>!
```

`MakeDatePart` у mysql/sqlite/sqlserver/clickhouse уже существовал (сигнатура не менялась) — в заморозку
не входит, как и удалений (RS0017) в этом изменении нет. Блок `ClickHouseFunctions.range`/`array_*` и
`window_funnel`/`sequence_match`/`retention` плюс пара `SupportsSequenceAggregates`/`MakeSequenceAggregate`
(`ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect`) — батч `todo-ch2`; токены `params`/nullable в строках
сверены с сигнатурами исходников, точную нормализацию выполнит генератор `PublicApiAnalyzers` при Шаге 5.
Блок `# --- продолжение RD2: Batch 3` — именованные окна, XML-методы и `multi_if`/`lagInFrame`; он
собран из тех же исходников (номер строки/арность сверены 20.09.2026), но точные doc-ID
(`TEntity`-аннотации, `params`-массивы, `init`-аксессоры, значения enum) также нормализует генератор
при заморозке; сводка находки — в разделе «Сводный аудит Batch 3…».

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name
'PublicAPI*.txt'` — пусто (подтверждает RD2); `<summary>` присутствует у всех новых членов, кроме
перечисленных в RD1; unit (Release, `--no-build`, этот проход): core **166/166**, postgres **206/206**,
sqlserver **195/195**, mysql **52/52**, mariadb **19/19**, sqlite **220/220**, clickhouse **143/143**;
интеграционные (Podman socket) — **11/11 passed, 0 skipped**.
### SQL Server `OPENJSON ... WITH` через `SqlTableFunctionAttribute.WithClause` (точечный аудит 20.09.2026)

Область: `SqlTableFunctionAttribute.WithClause` (`src/nextorm.core/SqlTableFunctionAttribute.cs:34`), `TableFunctionExpression.WithClause` и перегрузка конструктора (`Expressions/TableFunctionExpression.cs:20-46`), `SqlSourceRenderer.MakeTableFunction` (`DataContext/SqlSourceRenderer.cs:320-330`), док `SqlServerFunctions.openjson` (`Query/SqlFunctions.SqlServer.cs:50-60`). Тесты: `SqlGenerationTests.OpenJsonWith_ShouldAppendWithClause`, `SqlServerSpecificTests.OpenJson_WithTypedSchema_ShouldReturnTypedColumns`. Build Release — **0/0**; sqlserver unit **194/194**; контейнерная интеграция (реальный SQL Server через Testcontainers) — **1/1**. XML-`<summary>` есть у `WithClause` (свойство атрибута и свойство выражения) и обоих конструкторов; новых публичных **типов** нет → Приложение A (45) без изменений; Шаг 5 открыт.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| OJW1 | P2 | `SqlTableFunctionAttribute.cs:34`; `Expressions/TableFunctionExpression.cs:20,46` | Новые публичные члены (`SqlTableFunctionAttribute.WithClause`, `TableFunctionExpression.WithClause` + перегрузка ctor) не отслеживаются: `PublicAPI.*.txt` нет, `PublicApiAnalyzers` не подключён. Шаг 5 открыт | Внести их в `PublicAPI.Unshipped.txt` при заморозке (ср. TF1/Z1/CNT1) |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** `WithClause` — PascalCase-существительное, зеркалит SQL `WITH (...)`; BCL-конфликтов нет.
- **`WithClause` — декларация формы, не инъекция SQL.** Значение эмитится дословно, как provider-нативный `[SqlFunction]`; это то же DSL-исключение для SQL-зеркал, что уже принято в реестре.
- **Обратная совместимость.** Старый публичный конструктор `TableFunctionExpression(string, string?, MethodCallExpression)` сохранён и делегирует новому; изменения существующей сигнатуры нет.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test tests/nextorm.sqlserver.tests -c Debug` — **194/194**; `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -method nextorm.integration.tests.SqlServerSpecificTests.OpenJson_WithTypedSchema_ShouldReturnTypedColumns` — **1/1**; `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает OJW1); EN+RU `docs/guide/13-table-valued-functions.md`, `docs/providers/sqlserver.md` синхронны.

### Слияние `todo-ch`: ClickHouse date conversion + `string.Split` (точечный аудит 20.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (24 `to_*`-метода, `generate_random()`/`generate_random(long)`,
`SqlFunctions.IGenerateRandomRow`), `src/nextorm.clickhouse/ClickHouseDialect.cs`
(`MakeDatePart`/`SupportsDatePart`/`MakeDateConversion`/`SupportsDateConversionFunctions`/
`SupportsStringSplit`/`MakeStringSplit`/`WrapTableFunction`), `ISqlDialect`/`SqlDialectBase`
(те же новые члены), `Visitors/DateConversionSqlTranslator.cs` (internal, внешней поверхности не даёт).
Build Release — **0 warnings / 0 errors** (этот проход); `rg --files -g 'PublicAPI*.txt'` — пусто.

**Итог: новых P0/P1 нет; единственная P2 — уже открытая RD2 (ClickHouse-подписи дописаны в её блок
Шага 5), OJW1 — без изменений.**

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| RD2 (CH-часть) | P2 | `ISqlDialect.cs:149-150,228,738,765`; `SqlDialectBase.cs:35,47,494,527`; `ClickHouseDialect.cs:41,47,106,490`; `Query/SqlFunctions.ClickHouse.cs:230-239,286-358`; `Query/SqlFunctions.cs:134` | CH-часть поверхности не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2, не новая находка.** `SupportsStringSplit`/`SupportsDateConversionFunctions` — абстрактные члены `ISqlDialect` ⇒ source-breaking для внешних реализаторов (как `SupportsRandomSeed`/`SupportsCryptoFunctions`); `MakeDateConversion`/`MakeStringSplit` — DIM в интерфейсе и `virtual` в базе, разрыва не создают | Подписи внесены в блок Шага 5 (RD2); при заморозке трекать в `PublicAPI.Unshipped.txt` |

ℹ️ **Наблюдения (фикс не требуется):**
- **`MakeDatePart` — слияние PG+CH когерентно (проверено).** `dow` = `toInt32(toDayOfWeek(x) % 7)` даёт `0`=Sunday..`6`=Saturday, как `extract(dow)` у остальных; `isodow` = `toInt32(toDayOfWeek(x))` = ISO 1..7; `week` = `toISOWeek` (ISO 8601; `week` уже в базовом `DatePartFields`, `SqlDialectBase.cs:421-425`); остальные части — `toXxx` с `toInt32`; `epoch` — `toFloat64(toUnixTimestamp(...))`. `SupportsDatePart` CH добавляет `dow`/`isodow`/`epoch` поверх базы — наборы провайдеров согласованы.
- **`to_day_of_week` и `extract('dow')` расходятся намеренно.** `MakeDateConversion("to_day_of_week")` = `toInt32(toDayOfWeek(x))` (CH-нативные 1..7, Mon..Sun), что совпадает с XML-доком метода (`SqlFunctions.ClickHouse.cs:306`); нормализованный `0..6` — только у `extract`/`date_part`. Оба поведения задокументированы, поэтому это не P2-несогласованность.
- **XML-doc coverage полная.** `<summary>` есть у всех 24 `to_*`, обоих `generate_random`, `IGenerateRandomRow`, `SupportsStringSplit`/`SupportsDateConversionFunctions`/`MakeDateConversion`/`MakeStringSplit` в `ISqlDialect` и `SqlDialectBase`, у четырёх CH-переопределений и `SqlTableFunctionAttribute.WithClause`. Свойства `IGenerateRandomRow.Id/Value/Name` — без индивидуальных `<summary>`, как у соседних row-интерфейсов (`IZerosRow`, `INumbersRow`); Приложение A (45) без изменений. `CS1591` по-прежнему в `<NoWarn>` 7 `.csproj`.
- **`IGenerateRandomRow`/`generate_random` — нейминг конвенциям соответствует.** `I…Row` — как `IZerosRow`/`INumbersRow`/`IGenerateSeriesRow`; snake_case `to_*`/`generate_random` — принятое DSL-исключение для SQL-зеркал. BCL-конфликтов нет.
- **`MakeDateConversion`/`MakeStringSplit` — capability-hook, не понижение видимости.** Имена зеркалят уже принятые `MakeDatePart`/`MakeArrayFunction`; гейт `Supports*` стоит в трансляторе до рендера (`DateConversionSqlTranslator.cs:100-105`, `StringFunctionTranslator.cs:342`).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход); в диффе `src`+`tests` новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает RD2/OJW1). Тесты в этом проходе не перезапускались (только build).

### PostgreSQL наборные функции через `[SqlTableFunction]` (точечный аудит 20.09.2026)

Область: `Query/SqlFunctions.cs` (7 row-интерфейсов: `IRegexpMatchesRow`, `IRegexpSplitToTableRow`,
`IJsonArrayElementsRow`, `IJsonbEachRow`, `IJsonObjectKeysRow`, `IJsonPathQueryRow`, `ITsStatRow`),
`Query/SqlFunctions.Postgres.cs` (11 методов-`[SqlTableFunction]` + скалярный `jsonpath`),
`src/nextorm.postgres/PostgresDialect.cs` (`SupportsTableFunction` — новые имена,
`WrapTableFunction` — новый override), `Expressions/SelectExpression.cs` (ветка `string[]`),
`Visitors/JsonSqlTranslator.cs` (ветка `jsonpath`, internal). `SqlDialectBase.SupportsTableFunction`
не менялся (base уже `false`); новых `Supports*`/`Make*`-флагов нет — используется строковый гейт по
имени. Build Release — **0/0**; `rg --files -g 'PublicAPI*.txt'` — пусто.

**Итог: новых P0/P1 нет; единственная P2 — уже открытая RD2 (PG-подписи дописаны в её блок
Шага 5), OJW1/RD2-CH — без изменений.**

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| RD2 (PG-setof) | P2 | `Query/SqlFunctions.cs`; `Query/SqlFunctions.Postgres.cs`; `src/nextorm.postgres/PostgresDialect.cs` | Поверхность не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2, не новая находка.** Новых абстрактных членов `ISqlDialect` нет → source-разрыва для внешних реализаторов нет; `WrapTableFunction` — `virtual` override, разрыва не создаёт | Подписи внесены в блок Шага 5 (RD2); при заморозке трекать в `PublicAPI.Unshipped.txt` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** Методы зеркалят SQL-токены (`regexp_matches`,
  `jsonb_array_elements_text`, `ts_stat`) — принятое DSL-исключение (как `generate_series`/`to_*`);
  `jsonpath` назван по типу `jsonpath`. `I…Row` — как `IGenerateSeriesRow`/`IStringSplitRow`.
  BCL-конфликтов нет.
- **`WrapTableFunction` — исправление существующего бага, не новое поведение.** PostgreSQL заменяет
  единственную колонку скалярной наборной функции на псевдоним в `FROM`, а nextorm всегда алиасит
  производный источник (`RequireSubqueryAlias`), поэтому `generate_series`/`unnest` уже были
  неработоспособны на реальном PostgreSQL (интеграционного теста не было). Обёртка
  `(select <name> from <call>)` восстанавливает имя колонки; функции с явной колонкой не оборачиваются.
  Подтверждено реальными интеграционными тестами.
- **`string[]` в row reader — точечно, не общая материализация массивов.** Ветка в
  `SelectExpression.GetDataRecordMethod` (`GetValueMI`) добавлена по образцу `byte[]`; она нужна
  только для `regexp_matches` (`text[]`) и не включает заблокированный пункт триажа «row reader
  `Array(T)`/`Tuple`» (общая типизированная материализация массивов по-прежнему не реализована).
- **`jsonpath` — не инъекция SQL.** Рендерится `cast(expr as jsonpath)` через `SqlOperandTranslator`,
  как уже делает `EmitJsonPathFunction` для `jsonb_path_query_first`; аргумент параметризуется
  штатно.
- **XML-doc coverage полная.** `<summary>` есть у 7 row-интерфейсов, 11 методов-`[SqlTableFunction]`,
  `jsonpath`, override `WrapTableFunction`; свойства row-интерфейсов — без индивидуальных `<summary>`,
  как у соседних (`IGenerateSeriesRow`, `INumbersRow`). Приложение A (45) без изменений; `CS1591`
  по-прежнему в `<NoWarn>` 7 `.csproj`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (Debug): core
**166/166**, postgres **218/218**, sqlite **221/221**, sqlserver **198/198**, mysql **53/53**,
mariadb **20/20**, clickhouse **155/155**; контейнерная интеграция (Podman socket) —
`PostgresSpecificTests` **18/18**, `PostgresIntegrationTests` (общий набор) **211/211, 6 skipped**
(TVF-набор пропускается для PostgreSQL). Rejection — `PostgresTableFunctions_ShouldThrowBecauseOnlyPostgresHasThem`
в sqlite/sqlserver/mysql/mariadb/clickhouse. EN+RU `docs/guide/13-table-valued-functions.md`,
`docs/guide/provider-specific/postgresql.md`, `docs/providers/postgres.md` синхронны.

### Коррелированный скаляр в проекции: ссылка на член join-проекции (точечный аудит 20.09.2026)

Область: `Visitors/MemberTranslator.VisitMember` + новый `private static`
`MemberTranslator.TryTranslateProjectionOuterReference` (`src/nextorm.core/Visitors/MemberTranslator.cs`).
Изменение **внутреннее** (`MemberTranslator` — `internal`), публичный API не затронут: новые члены —
`private`, новых публичных типов/методов/свойств нет. Build Release — **0 warnings / 0 errors**
(этот проход); `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 открыт).

**Итог: публичных P0/P1/P2 нет.**

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| — | — | — | Новых публичных членов нет; внутренний метод `TryTranslateProjectionOuterReference` не требует трекинга `PublicAPI.*` | — |

ℹ️ **Наблюдения (фикс не требуется):**
- **Публичная поверхность не расширялась.** Корреляция по-прежнему выражается существующими
  терминалами `QueryCommand<T>` и `SqlFunctions.Sql.exists/@in/any/all`; отдельный `OuterRef*`-API
  сознательно не вводился (`plan-correlated-subqueries.md` §2 «Не-цели»). Дублирование поверхности
  запрещено границами скилла, поэтому «публичный API поверх `OuterRefMarker`» не добавлялся.
- **Именование — конвенции соблюдены.** `TryTranslateProjectionOuterReference` — private,
  `Try…`-префикс как у соседних `TryTranslate`; XML-`<summary>` присутствует.
- **Гейт провайдеров не нужен.** Коррелированный скалярный подзапрос — ANSI; ограничение относится к
  in-memory, который уже бросает `NotSupportedException` (`InMemoryQueryBuilder`). Новый
  `Supports*`-флаг не вводился намеренно (нет невыразимого провайдера).
- **`AliasFromProjectionVisitor` — переиспользован, не продублирован.** Алиас элемента `ItemN`
  берётся тем же visitor'ом, что и в существующей ветке `OuterRefMarker`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; SQL-gen: core
**166/166**, sqlite **222/222**, sqlserver **198/198**, postgres **209/209**, mysql **52/52**,
mariadb **19/19**, clickhouse **154/154**; интеграция (Podman socket): SQL Server
`-class …SqlServerIntegrationTests -method "*Correlated*"` — **14/14**, Postgres/MySQL по **2/2**,
SQLite `-method "*OnJoinProjection*"` — **2/2**; в диффе `src`+`tests` новых
`#pragma`/`SuppressMessage`/`NoWarn` — **0**; `rg --files -g 'PublicAPI*.txt'` — пусто.

### ClickHouse скалярные array-функции `range`/`arrayEnumerate`/`arrayCumSum`/`arraySlice`/`arrayPushBack` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые члены:

- `ClickHouseFunctions.range(long) -> long[]` (`Query/SqlFunctions.ClickHouse.cs:318`),
  `range(long, long) -> long[]` (`:321`), `range(long, long, long) -> long[]` (`:324`),
  `array_enumerate<T>(T[]) -> long[]` (`:330`), `array_cum_sum<T>(T[]) -> T[]` (`:336`),
  `array_slice<T>(T[], long) -> T[]` (`:343`), `array_slice<T>(T[], long, long) -> T[]` (`:346`),
  `array_push_back<T>(T[], T) -> T[]` (`:352`).
- Транслятор — `Visitors/ArraySqlTranslator.cs:230-245` (internal, внешней поверхности не даёт);
  новых флагов/хуков нет — переиспользуется `ISqlDialect.SupportsArrayFunctions`/`MakeArrayFunction`.

**CS1591/XML-doc.** XML-`<summary>` присутствует у всех 8 новых методов (с оговоркой «возвращает
массив ⇒ только вложенно»); новых публичных типов нет → Приложение A (45) без изменений. Именование —
конвенции соблюдены (snake_case DSL — SQL-зеркало, `T[]`-аргументы как у соседних array-методов),
P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| ASF1 | P2 | `Query/SqlFunctions.ClickHouse.cs:318-352`; `PublicAPI.*.txt` отсутствуют | 8 новых членов публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1, не новая находка** (сводно — раздел «Сводный аудит слияний `todo-pg2`/`todo-mssql2`/`todo-ch2`»). Новых abstract-членов `ISqlDialect` нет, разрыва для внешних реализаторов не создаётся | При заморозке внести восемь методов `ClickHouseFunctions.range`/`array_enumerate`/`array_cum_sum`/`array_slice`/`array_push_back` в `PublicAPI.Unshipped.txt` (точные подписи — в блоке Шага 5, RD2; ср. AR1/AJ1/AJ8) |

ℹ️ **Наблюдения (фикс не требуется):**
- **Гейт переиспользован осознанно.** `range`/`arrayEnumerate`/`arrayCumSum`/`arraySlice`/`arrayPushBack` —
  то же семейство «array-функции над нативным `Array(T)`», что и уже реализованные `length`/`has`/…,
  поэтому новый `Supports*`-флаг не вводится: диалект без `SupportsArrayFunctions` бросает
  `NotSupportedException` (`ArraySqlTranslator.RequireArrayFunctions`). Отдельный флаг под каждый член
  был бы зонтиком без доказательства невыразимости.
- **`MakeArrayFunction` не расширяется.** Новые функции возвращают массив, и их результирующий тип
  определяется внешней функцией (`length`/`arrayStringConcat`), поэтому оборачивать их в `toInt64`
  нельзя; CH-каст остаётся только у `length`/`indexOf`.
- **`range` не промоутится в `PostgresFunctions`.** У PostgreSQL нет array-возвращающего `range`
  (`generate_series` — set-returning и уже покрыт TVF), `array_append` — отдельная PG-функция с другим
  именем; см. матрицу.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход);
`dotnet test tests/nextorm.clickhouse.tests -c Debug` — **159/159**;
`Postgres…ClickHouseArrayScalarFunctions_UnsupportedByProvider_ShouldThrow` — **1/1**;
`ClickHouseIntegrationTests` (Testcontainers) — **45/45**, включая
`ArrayScalarFunctions_ShouldReturnValues`; `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает ASF1);
Приложение A (45) без изменений.

### ClickHouse агрегаты последовательностей `windowFunnel`/`retention`/`sequenceMatch` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые/изменённые члены:

- `ISqlDialect.SupportsSequenceAggregates` (`DataContext/Dialect/ISqlDialect.cs:416`, abstract; default
  `false` — `SqlDialectBase.cs:110`; override ClickHouse — `src/nextorm.clickhouse/ClickHouseDialect.cs:245`);
- `ISqlDialect.MakeSequenceAggregate(string, string?, string) -> string` (`ISqlDialect.cs:425`, **default
  interface method**, бросает `NotSupportedException`); `SqlDialectBase.cs:116` (`virtual`, бросает);
  override ClickHouse — `ClickHouseDialect.cs:254` (`windowFunnel`/`sequenceMatch`/`retention`, `toInt32`);
- `ClickHouseFunctions.window_funnel<TTime>(long, TTime?, params bool[]) -> int`
  (`Query/SqlFunctions.ClickHouse.cs:102`), `sequence_match<TTime>(string?, TTime?, params bool[]) -> int`
  (`:111`), `retention(params bool[]) -> int[]` (`:121`);
- транслятор — `Visitors/AdvancedAggregateTranslator.cs` (internal, внешней поверхности не даёт).

**CS1591/XML-doc.** XML-`<summary>` присутствует у трёх методов, флага и хука
(интерфейс+база+ClickHouse); новых публичных **типов** нет → Приложение A (45) без изменений. Именование
— snake_case DSL зеркалит SQL (`window_funnel`/`sequence_match`/`retention`), `Supports*`/`Make*` — как
у соседних семейств; P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| SQ1 | P2 | `ISqlDialect.cs:416,425`; `SqlDialectBase.cs:110,116`; `ClickHouseDialect.cs:245,254`; `Query/SqlFunctions.ClickHouse.cs:102,111,121`; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1, не новая находка** (сводно — раздел «Сводный аудит слияний `todo-pg2`/`todo-mssql2`/`todo-ch2`», находка SQ-DIM). `SupportsSequenceAggregates` — абстрактный член `ISqlDialect` ⇒ source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`); `MakeSequenceAggregate` — DIM в интерфейсе и `virtual` в базе, разрыва не создаёт | При заморозке внести: `SupportsSequenceAggregates`, `MakeSequenceAggregate(string, string?, string)`, override'ы `SqlDialectBase`/`ClickHouseDialect` и три метода `ClickHouseFunctions` (точные подписи — в блоке Шага 5, RD2; ср. AR1/AJ1/ASF1). Для флага рассмотреть DIM `=> false` (см. SQ-DIM) |

ℹ️ **Наблюдения (фикс не требуется):**
- **Двойные скобки — параметрическая форма, как `quantile`.** `windowFunnel(window)(timestamp, conds...)`
  и `sequenceMatch(pattern)(timestamp, conds...)` рендерятся через `MakeSequenceAggregate` с
  `parameters != null`; `retention` — одинарные скобки (`parameters == null`). Отдельный флаг под каждый
  агрегат не нужен: три члена — одно семейство, невыразимое ни у одного другого провайдера.
- **`params bool[]` и вложенность условий.** Условия передаются встроенными выражениями; захваченный
  массив отвергается (`NotSupportedException`), т.к. его элементы невыразимы как SQL-предикаты.
  `retention` возвращает `Array(UInt8)`, поэтому материализуется только вложенно
  (`length`/`arrayStringConcat`), что согласовано с отсутствующим row reader массивов.
- **`IEventEntity` — тестовая сущность интеграционных тестов** (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`),
  публичной поверхности пакета не касается; Приложение A не затрагивает.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход);
`dotnet test tests/nextorm.clickhouse.tests -c Debug` — **163/163**;
`dotnet test tests/nextorm.postgres.tests -c Debug` — **211/211**;
`ClickHouseIntegrationTests` (Testcontainers) — **48/48**, включая
`WindowFunnel_ShouldCountConsecutiveConditions`/`SequenceMatch_ShouldMatchPattern`/
`Retention_ShouldReturnConditionMask`; `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает SQ1);
Приложение A (45) без изменений.

### Сводный аудит слияний `todo-pg2`/`todo-mssql2`/`todo-ch2` (20.09.2026)

Область (дельта трёх merge'ей, база `970769a`, HEAD `8670bba`): PostgreSQL наборные TVF
(`Query/SqlFunctions{,.Postgres}.cs`, `PostgresDialect.SupportsTableFunction`/`WrapTableFunction`,
`Expressions/SelectExpression.cs` ветка `string[]`, `Visitors/JsonSqlTranslator.cs` ветка `jsonpath`),
MSSQL коррелированный скаляр в проекции (`Visitors/MemberTranslator.cs`, `internal`) и ClickHouse
(`ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect` — `SupportsSequenceAggregates`/`MakeSequenceAggregate`,
`Query/SqlFunctions.ClickHouse.cs` — 8 array-методов + `window_funnel`/`sequence_match`/`retention`,
`Visitors/{ArraySqlTranslator,AdvancedAggregateTranslator}.cs`). Build Release — **0 warnings / 0 errors**.

Три точечных раздела выше (`RD2 (PG-setof)`, `ASF1`, `SQ1`) — **одна и та же открытая P2-находка RD2**
(поверхность не трекается до Шага 5); ниже — сводная точка входа, ID подразделов сохранены для
трассировки, повторные обоснования не дублируются. Публичных P0/P1 нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| RD2 (batch 2) | P2 | `ISqlDialect.cs:416,425`; `SqlDialectBase.cs:110,116`; `ClickHouseDialect.cs:245,254`; `Query/SqlFunctions.ClickHouse.cs:102,111,121,318-352`; `Query/SqlFunctions.cs:86-160`; `Query/SqlFunctions.Postgres.cs:236,574-633`; `PostgresDialect.cs:47,63` | Поверхность не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1, не новая находка.** `SupportsSequenceAggregates` — **абстрактный** член `ISqlDialect` ⇒ source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`), тогда как парный `MakeSequenceAggregate` — DIM; PG `WrapTableFunction` — `virtual` override, разрыва не создаёт | Подписи batch 2 внесены в блок Шага 5 (RD2); при заморозке внести в `PublicAPI.Unshipped.txt`. Для `SupportsSequenceAggregates` см. SQ-DIM ниже |
| SQ-DIM | P2 | `ISqlDialect.cs:416,425` | **Асимметрия source-совместимости внутри одной пары:** гейт `SupportsSequenceAggregates` абстрактный (разрыв для внешних реализаторов), а рендер `MakeSequenceAggregate` — DIM с дефолтным `throw` (разрыва не создаёт). Тот же класс — уже принятые абстрактные `SupportsRandomSeed`/`SupportsCryptoFunctions`/`SupportsDatePart` (RD2) и `SupportsArrayFunctions`/`SupportsArrayJoin` (AR1) | Определиться на Шаге 5: либо DIM `=> false` для новых `Supports*`-флагов (как у `MakeSequenceAggregate`), либо зафиксировать абстрактность как принятую политику и отразить это в заморозке |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** 7 row-интерфейсов `I…Row` (как `IGenerateSeriesRow`),
  snake_case `regexp_matches`/`regexp_split_to_table`/`jsonb_*`/`ts_stat`/`window_funnel`/`sequence_match`/
  `retention`/`range`/`array_*`/`jsonpath` — принятое DSL-исключение для SQL-зеркал; BCL-конфликтов нет
  (`ClickHouseFunctions.range` не затеняет `System.Linq.Enumerable.Range` — другой тип и сигнатура).
  `IRegexpMatchesRow` — non-nullable `string[] Matches` (`setof text[]` всегда даёт массив при наличии строки).
- **XML-doc coverage полная (summary-level).** `<summary>` у всех новых публичных методов, интерфейсов,
  флага/хука и переопределений (`PostgresDialect.WrapTableFunction`, `ClickHouseDialect.*`). Свойства
  row-интерфейсов — без индивидуальных `<summary>`, как у соседних `IGenerateSeriesRow`/`INumbersRow`.
  Новых публичных типов **без** документации нет → Приложение A (45) без изменений; `CS1591` остаётся
  в `<NoWarn>` 7 библиотечных `.csproj` (принято до Шага 5).
- **MSSQL `MemberTranslator.TryTranslateProjectionOuterReference` (`:433`) публичной поверхности не даёт**
  (`internal static` + `private static`) — отдельная P-находка не заводится (см. раздел «Коррелированный
  скаляр…» выше). Ветка `VisitMember` (`:243`) добавлена после существующих, старые ветки не тронуты.
- **Два строковых списка имён в `PostgresDialect`.** `SupportsTableFunction` (`:47`, 11 имён) и
  `WrapTableFunction` (`:63`, 6 имён) должны поддерживаться в синхроне: сегодня расхождение осознанное
  (обёртка только у одно-колоночных функций, где колонка названа по функции), но при добавлении имени
  только в один список появится дрейф — см. ℹ️ в `code-smells-review.md` (batch 2).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход);
`rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает RD2); в диффе `src`+`tests` новых
`#pragma`/`SuppressMessage`/`NoWarn` — **0**. Живые прогоны юнит/интеграционных наборов — в
`code-smells-review.md` («Сводный аудит … batch 2») и в точечных разделах выше.

### PostgreSQL/ANSI именованные окна + `GROUPS`/`EXCLUDE` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые/изменённые члены:

- `WindowFunction<T>.Over(string) -> T` (`src/nextorm.core/Query/WindowFunctions.cs:42`) — ссылка на
  именованное окно (`OVER w`); существующие перегрузки не тронуты;
- `enum WindowFrameType` + член `Groups` (`WindowFunctions.cs:80`); `enum WindowFrameExclusion`
  (`:95`); `WindowFrame.Exclusion` (`:172`), `WindowFrame.Groups(WindowFrameBound, WindowFrameBound)`/
  `Groups(int, int)` (`:181`,`:184`), `WindowFrame.WithExclusion(WindowFrameExclusion)` (`:191`);
- `NamedWindowOrderKey` (`Query/WindowDefinition.cs:9`; `Expression`/`Direction` — `:18`,`:21`) и
  `WindowDefinition` (`:29`; `Name`/`PartitionBy`/`OrderBy`/`Frame` — `:40`,`:43`,`:46`,`:49`);
- `EntityBuilder<TEntity>.Window(string, Expression<Func<TEntity, object?>>[]?, NamedWindowOrderKey[]?, WindowFrame?)`
  (`Builders/EntityBuilder.cs:530`), `Asc`/`Desc` (`:558`,`:565`);
- `QueryDefinition.Windows` (`Query/QueryDefinition.cs:67`) и `QueryCommand.Windows`
  (`Query/QueryCommand.cs:243`) + внутренний `WindowsPlanHash` (`QueryCommand.cs:51`);
- `ISqlDialect.SupportsNamedWindows` (`DataContext/Dialect/ISqlDialect.cs:223`),
  `SupportsWindowFrameGroups` (`:229`), `SupportsWindowFrameExclusion` (`:236`) — abstract, default
  `false` (`SqlDialectBase.cs:56,58,60`); override'ы PostgreSQL (`PostgresDialect.cs:91,94,97`),
  SQLite (`SqliteDialect.cs:57,60,63`), ClickHouse (`ClickHouseDialect.cs:166,169`),
  MySQL (`MySqlDialect.cs:45`, наследуется MariaDB);
- план-ключ и рендер — `QueryPlanEqualityComparer.WindowsEqual` (`Query/QueryPlanEqualityComparer.cs:163`),
  `SqlBuilder.MakeNamedWindow` (`DataContext/SqlBuilder.cs:529`), `WindowSql.IsValidWindowName`
  (`Visitors/WindowSql.cs:41`) (internal).

**CS1591/XML-doc.** XML-`<summary>` присутствует у всех новых публичных типов/членов и у трёх флагов
(интерфейс+база+провайдеры); два новых публичных **типа** (`NamedWindowOrderKey`, `WindowDefinition`)
документированы, поэтому Приложение A (45, «без документации») не меняется.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| NW1 | P2 | `ISqlDialect.cs:223,229,236`; `SqlDialectBase.cs:56,58,60`; провайдерные override'ы; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение AR1/RD2/SQ1, не новая находка** (сводно — раздел «Сводный аудит Batch 3…» ниже). Три `Supports*` — абстрактные члены `ISqlDialect` ⇒ source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`) | При заморозке внести: три флага + override'ы + `WindowFunction<T>.Over(string)`, `WindowFrame.Groups`/`WithExclusion`, `NamedWindowOrderKey`, `WindowDefinition`, `EntityBuilder.Window`/`Asc`/`Desc`, `QueryDefinition`/`QueryCommand.Windows` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Имя окна валидируется.** `EntityBuilder.Window` и `WindowFunctionTranslator` пропускают только
  ASCII-идентификатор (`WindowSql.IsValidWindowName`), т.к. имя встраивается в SQL verbatim; иначе —
  `ArgumentException`. Ссылка на незадекларированное, но валидное имя остаётся ошибкой сервера (как и в
  самом SQL) — публичная валидация вхождения не делается, чтобы не дублировать парсер.
- **`WindowFrame.Exclusion` — nullable.** `null` (значение по умолчанию существующих фабрик) не
  печатает `EXCLUDE`, поэтому `WindowFrame.Rows(...)` не меняет уже существующий SQL;
  `WithExclusion(NoOthers)` рендерит явный `exclude no others`.
- **`Window` объявляется после join'ов.** Спецификация параметризована типом проекции; перенос через
  `Join`/`ArrayJoinElement` запрещён (`InvalidOperationException`), как и дубль имени. План-ключ
  (`WindowsPlanHash`/`WindowsEqual`) включает имена, ключи и рамки, включая `Exclusion`.
- **`GROUPS` не требует ORDER BY на уровне API.** Как и в SQL, сервер потребует `ORDER BY` в самом окне;
  библиотека не добавляет искусственную проверку.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход);
`dotnet test tests/nextorm.postgres.tests -c Debug` — **229/229**;
`tests/nextorm.core.tests` — **174/174**; `tests/nextorm.sqlite.tests` — **225/225**;
`tests/nextorm.clickhouse.tests` — **167/167**; `tests/nextorm.mysql.tests` — **56/56**;
`tests/nextorm.mariadb.tests` — **23/23**; `tests/nextorm.sqlserver.tests` — **202/202**;
Testcontainers PostgreSQL `PostgresSpecificTests` — **21/21** (включая
`NamedWindow_ShouldDeclareOnceAndReference`/`GroupsFrame_ShouldIncludeWholePeerGroups`/
`FrameExclusion_ShouldRemoveCurrentRow`), `PostgresIntegrationTests` — **213/213** (6 skip);
coverage `dotnet-coverage`+`reportgenerator` — **77.1 % line / 67.3 % branch** (порог 75; новые окна —
`WindowDefinition`/`NamedWindowOrderKey`/`WindowFrame` 100 %, `WindowSql` 94.7 %, `WindowFunctionTranslator`
86.5 %); `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает NW1); Приложение A (45) без изменений.

### SQL Server скалярные методы типа XML `value`/`query`/`exist` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые члены:

- `SqlServerFunctions.xml_value<T>(string?, string?, string?) -> T?` (`Query/SqlFunctions.SqlServer.cs`),
  `xml_query(string?, string?) -> string?`, `xml_exist(string?, string?) -> bool` (XML-`<summary>` у всех);
- `ISqlDialect.SupportsXmlFunctions` (`DataContext/Dialect/ISqlDialect.cs`, abstract; base `false` —
  `SqlDialectBase.cs`; override SQL Server), `ISqlDialect.SupportsXmlFunction(string)` (abstract; base
  `false`; SQL Server `true` для `value`/`query`/`exist`, `false` для `nodes`),
  `ISqlDialect.MakeXmlFunction(string, string, IReadOnlyList<string>)` (abstract; base бросает
  `NotSupportedException`; SQL Server рендерит `{operand}.{name}({args})`);
- транслятор — `Visitors/XmlSqlTranslator.cs` (internal, внешней поверхности не даёт).

**CS1591/XML-doc.** `<summary>` присутствует у трёх методов и всех трёх новых членов `ISqlDialect`
(интерфейс + база + SQL Server); новых публичных **типов** нет → Приложение A (45) без изменений.
Именование — snake_case DSL зеркалит SQL (`json_value`/`json_query`), `Supports*`/`Make*` — как у
соседних семейств, generic `T?` — как `choose<TResult>`; P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| X1 | P2 | `ISqlDialect.cs:178,626,634`; `SqlDialectBase.cs:39,41,46`; `SqlServerDialect.cs:48,51,54`; `Query/SqlFunctions.SqlServer.cs:48,56,65`; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2, не новая находка** (сводно — раздел «Сводный аудит Batch 3…» ниже). `SupportsXmlFunctions`/`SupportsXmlFunction`/`MakeXmlFunction` — абстрактные члены `ISqlDialect` ⇒ source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`) | ~~При заморозке внести три члена `ISqlDialect`, override'ы базы/SQL Server и три метода `SqlServerFunctions` в `PublicAPI.Unshipped.txt`~~ **Фаза 3:** `SupportsXmlFunctions`/`SupportsXmlFunction`/`MakeXmlFunction` удалены — вносить `IXmlFunctions`/`XmlFunctions` и `SqlServerFunctions` (трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53) |

ℹ️ **Наблюдения (фикс не требуется):**
- **Гейт по имени, не зонтиком.** `SupportsXmlFunction("nodes")` на SQL Server возвращает `false`:
  rowset-метод требует внешней ссылки на обёрнутую строку в `FROM`/`CROSS APPLY` — того же
  отсутствующего механизма, что «сырой SQL как композируемый источник» и коррелированный `APPLY`
  (см. `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.3 / `docs/specs/roadmap/todo_xml_nodes.md`). Поэтому `nodes` не добавлен, а невыразимость обоснована
  по функции, как требует скилл.
- **`SqlServerFunctions`, а не `CommonFunctions`.** Постфиксная форма `xmlcol.value('xpath','type')`
  есть только в SQL Server; PostgreSQL (`xpath`/`xpath_exists`) и MySQL/MariaDB (`ExtractValue`/
  `UpdateXML`) используют инвертированный порядок аргументов и другую семантику (набор vs скаляр),
  поэтому единый кросс-провайдерный API не вводится — по образцу `choose`.
- **Обязательный литерал.** T-SQL принимает XQuery и SQL-тип только строковыми литералами, поэтому
  транслятор валидирует `SqlLiteral.TryGetConstantString` и сам квотирует (`ToSqlStringLiteral`);
  не-константа даёт `NotSupportedException` (тест `XmlMethods_ShouldThrowWhenArgumentsAreNotConstants`).
- **`xml_exist` не помечен предикатом намеренно.** Он **не** добавлен в `TypeFacts.IsPredicateCall`,
  поэтому в условии проходит через существующий `MakeBooleanValuePredicate` → `(xmlcol.exist('…')) = 1`
  (bit не является предикатом в T-SQL), а в проекции остаётся bit — как `isjson`.
- **`T?` у `xml_value` — верный контракт.** `value()` возвращает запрошенный SQL-тип, поэтому CLR-тип
  задаёт вызывающий (`xml_value<int>`/`<string>`); форма совпадает с `choose<TResult>`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (Debug): core
**166/166**, sqlite **224/224**, postgres **221/221**, sqlserver **204/204**, mysql **54/54**,
mariadb **21/21**, clickhouse **165/165**; контейнерная интеграция (Podman socket, реальный SQL Server)
`SqlServerSpecificTests.XmlMethods_ShouldReturnValues` — **1/1**; `DialectCapabilityContractTests` —
**7/7**; `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает X1); EN+RU `docs/guide/11-scalar-functions.md`,
`docs/guide/provider-specific/sqlserver.md`, `docs/providers/sqlserver.md`, `docs/advanced/api-reference.md`
синхронны.

### ClickHouse `multiIf` и frame-respecting `lagInFrame`/`leadInFrame` (точечный аудит 20.09.2026)

Публичная поверхность аддитивна; переименований нет. Новые/изменённые члены:

- `ISqlDialect.SupportsMultiIf` (`DataContext/Dialect/ISqlDialect.cs:519`, abstract; default `false` —
  `SqlDialectBase.cs:152`; override ClickHouse — `src/nextorm.clickhouse/ClickHouseDialect.cs:97`);
- `ISqlDialect.MakeMultiIf(IReadOnlyList<string>, Type) -> string` (`ISqlDialect.cs:529`, **default
  interface method**, бросает `NotSupportedException`); `SqlDialectBase.cs:158` (`virtual`, бросает);
  override ClickHouse — `ClickHouseDialect.cs:105` (`multiIf(a, b, …)`);
- `ISqlDialect.SupportsInFrameWindowFunctions` (`ISqlDialect.cs:245`, abstract; default `false` —
  `SqlDialectBase.cs:62`; override ClickHouse — `ClickHouseDialect.cs:163`);
- `ClickHouseFunctions.MultiIfBranch<T>` (`Query/SqlFunctions.ClickHouse.cs:441`, вложенный public
  sealed, приватный ctor — только для деревьев выражений), `when<T>(bool, T?) -> MultiIfBranch<T>`
  (`:453`), `otherwise<T>(T?) -> MultiIfBranch<T>` (`:459`), `multi_if<TResult>(params
  MultiIfBranch<TResult>[]) -> TResult?` (`:469`), `lag_in_frame<T>(T?) -> WindowFunction<T?>`
  (`:477`), `lag_in_frame<T>(T?, int)` (`:480`), `lag_in_frame<T>(T?, int, T?)` (`:483`),
  `lead_in_frame<T>(T?)` (`:491`), `lead_in_frame<T>(T?, int)` (`:494`), `lead_in_frame<T>(T?, int, T?)`
  (`:497`);
- трансляторы — `Visitors/BuiltinFunctionTranslator.cs` (`multi_if` ветка `:45`, `EmitMultiIf`
  `:178`, `FlattenMultiIfBranches` `:217`; внутренняя поверхность), `Visitors/WindowSql.cs:21-22`
  (маппинг имён), `Visitors/WindowFunctionTranslator.cs:37,48` (принимает `ClickHouseFunctions` и
  гейтит `lagInFrame`/`leadInFrame`), `Visitors/NormSqlTranslator.cs:72` (guard «нужен `Over(...)`»).

**CS1591/XML-doc.** XML-`<summary>` присутствует у флагов, хука (интерфейс+база+ClickHouse), у шести
оконных методов и у `when`/`otherwise`/`multi_if`/`MultiIfBranch<T>`; добавлен один публичный **тип**
(`MultiIfBranch<T>`), и он задокументирован → Приложение A (45) не меняется. Именование — snake_case DSL
зеркалит SQL (`multi_if`, `lag_in_frame`), `when`/`otherwise` — C#-хелперы builder'а (не SQL-токены),
`Supports*`/`Make*` — как у соседних семейств; P0/P1 по именам нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| MF1 | P2 | `ISqlDialect.cs:245,519,529`; `SqlDialectBase.cs:62,152,158`; `ClickHouseDialect.cs:97,105,163`; `Query/SqlFunctions.ClickHouse.cs:441-497`; `PublicAPI.*.txt` отсутствуют | Новые члены публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение AR1/SQ1, не новая находка** (сводно — раздел «Сводный аудит Batch 3…» ниже). `SupportsMultiIf`/`SupportsInFrameWindowFunctions` — абстрактные члены `ISqlDialect` ⇒ source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`); `MakeMultiIf` — DIM и `virtual`, разрыва не создаёт | При заморозке внести флаги, `MakeMultiIf`, override'ы и 10 методов/`MultiIfBranch<T>` (ср. AR1/SQ1) |

ℹ️ **Наблюдения (фикс не требуется):**
- **`multi_if` остаётся ClickHouse-поверхностью, хотя `CASE WHEN` умеют все.** Точной функции `multiIf`
  (variadic пары условие/значение + `else`) нет ни у одного провайдера кроме ClickHouse; `CASE WHEN`
  уже отдаётся переносимо через `iif`/C# тернарник, поэтому кросс-провайдерная `multi_if` дублировала бы
  существующую форму и добавила бы слабо типизированную variadic-поверхность. Флаг пер-функция, не
  зонтик; обоснование — в матрице провайдер×форма.
- **Builder `when`/`otherwise` вместо `params object?[]`.** Сохраняет типы условия (`bool`) и значения
  (`TResult`), выводит `TResult` без явного generic-аргумента и не боксирует выражения; `params
  object?[]` потерял бы обе гарантии (ср. I6 о слабом `params`-контракте). `MultiIfBranch<T>` —
  непрозрачный носитель, приватный ctor, самостоятельного смысла не имеет (голый `when`/`otherwise`
  отвергается в `BuiltinFunctionTranslator` с понятным сообщением).
- **Тип результата `multiIf` — общий супертип ClickHouse, числовой результат приводится.** ClickHouse
  выводит общий супертип ветвей (например `UInt8` для маленьких целых литералов), который row reader не
  читает как объявленный CLR-тип, поэтому `MakeMultiIf` получает `resultType` и для числовых CLR-типов
  оборачивает вызов в `cast(multiIf(...) as Int64/Float64/...)`; строковые/date-результаты не кастуются,
  чтобы не терять точность (`DateTime64`). См. интеграционный `MultiIf_ShouldReturnMatchedBranch`
  (проверяет и строковые, и числовые бакеты).
- **`lagInFrame`/`leadInFrame` — единственный frame-respecting вариант.** У PostgreSQL/SQL Server/
  MySQL/MariaDB/SQLite `lag`/`lead` фрейм игнорируют (или, как ClickHouse, отвергают явный фрейм), так
  что промоушен невозможен; отдельный `Make*`-хук не вводится — нативное написание фиксировано, а
  гейта достаточно (как у `nth_value`). Контракты `MakeDatePart`/`to_day_of_week` не тронуты.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Debug` — **171/171**;
`dotnet test tests/nextorm.postgres.tests -c Debug` — **222/222**; новые интеграционные тесты
`ClickHouseIntegrationTests.MultiIf_ShouldReturnMatchedBranch` и
`InFrameWindowFunctions_ShouldRespectFrame` (Testcontainers) — **1/1** каждый при отдельном прогоне
(полный прогон класса под конкуренцией ворктри нестабилен из-за запуска контейнера: один проход дал
transient socket-флейк в несвязанном `LeftArrayJoinClause_ShouldKeepEmptyArrays`, другой — отказ
старта контейнера/скипы); `rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает MF1); Приложение A
(45) без изменений (`MultiIfBranch<T>` задокументирован).

### Сводный аудит Batch 3 — именованные окна / XML-методы / ClickHouse `multi_if` (20.09.2026)

Область (три патча Batch 3, наложенные на `8670bba` без коммита): PostgreSQL/ANSI именованные окна и
`GROUPS`/`EXCLUDE` (`Query/WindowDefinition.cs`, `Query/WindowFunctions.cs`, `Builders/EntityBuilder.cs`,
`Query/{QueryCommand,QueryCommand.Clone,QueryCommand.QueryPreparer,QueryDefinition,QueryPlanEqualityComparer}.cs`,
`DataContext/{SqlBuilder,Dialect/ISqlDialect,Dialect/SqlDialectBase}.cs`,
`Visitors/{WindowSql,WindowFunctionTranslator,NormSqlTranslator}.cs`, override'ы PG/SQLite/CH/MySQL),
SQL Server XML `value`/`query`/`exist` (`Query/SqlFunctions.SqlServer.cs`, `Visitors/XmlSqlTranslator.cs`,
`ISqlDialect`+`SqlServerDialect`) и ClickHouse `multiIf` + `lagInFrame`/`leadInFrame`
(`Query/SqlFunctions.ClickHouse.cs`, `Visitors/{BuiltinFunctionTranslator,WindowSql,WindowFunctionTranslator,NormSqlTranslator}.cs`,
`ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect`). Build Release — **0 warnings / 0 errors** (этот проход).

Три точечных раздела выше (`NW1`, `X1`, `MF1`) — **одна и та же открытая P2-находка RD2** (поверхность
не трекается до Шага 5); ID подразделов сохранены для трассировки, повторные обоснования не дублируются.
Ниже — сводная точка входа (аналогично разделу «Сводный аудит слияний `todo-pg2`/`todo-mssql2`/`todo-ch2`»).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| RD2 (batch 3) | P2 | `ISqlDialect.cs:178,223,229,236,245,519,529,626,634`; `SqlDialectBase.cs:39,41,46,56,58,60,62,152,158`; `PostgresDialect.cs:91,94,97`; `SqliteDialect.cs:57,60,63`; `ClickHouseDialect.cs:97,105,163,166,169`; `SqlServerDialect.cs:48,51,54`; `MySqlDialect.cs:45`; `Query/WindowDefinition.cs:9,29`; `Query/WindowFunctions.cs:42,80,95,172,181,184,191`; `Builders/EntityBuilder.cs:530,558,565`; `Query/QueryDefinition.cs:67`; `Query/QueryCommand.cs:243`; `Query/SqlFunctions.SqlServer.cs:48,56,65`; `Query/SqlFunctions.ClickHouse.cs:441-498`; `PublicAPI.*.txt` отсутствуют | Поверхность Batch 3 не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1/SQ1, не новая находка.** Новые абстрактные члены `ISqlDialect` (7 `Supports*`-флагов `SupportsXmlFunctions`/`SupportsXmlFunction`/`SupportsNamedWindows`/`SupportsWindowFrameGroups`/`SupportsWindowFrameExclusion`/`SupportsInFrameWindowFunctions`/`SupportsMultiIf` + хук `MakeXmlFunction`) ⇒ source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`); `MakeMultiIf` — DIM и `virtual`, разрыва не создаёт (та же асимметрия, что SQ-DIM/AR4) | Внести подписи в `PublicAPI.Unshipped.txt` при заморозке (продолжение блока RD2; ср. AR1/SQ1/ASF1). Заодно определиться с политикой abstract-флагов (SQ-DIM): DIM `=> false` либо зафиксировать abstract как принятую норму |
| NW2 | **Закрыто (20.09.2026)** | `Query/WindowDefinition.cs:9` vs `Query/WindowFunctions.cs:64` (ср. `Builders/EntityBuilder.cs:558,565`) | **Было (P2):** два публичных типа одного назначения — ключ `ORDER BY` окна — названы почти одинаково: `WindowOrderKey` (именованные окна: `Direction` + `LambdaExpression`, `EntityBuilder.Asc`/`Desc`) и `WindowOrder` (inline `Over`: `Direction` + `Expression<Func<object?>>`, `CommonFunctions.asc`/`desc`); имена не подсказывали разницу (named vs inline) | **Исправлено:** named-ключ переименован в `NamedWindowOrderKey` (alpha, замена на месте); `WindowOrder` остаётся inline-ключом. Обновлены код (`WindowDefinition.cs`, `EntityBuilder.cs`), XML-доки, `docs/guide/10-window-functions.md` + `docs/ru/guide/10-window-functions.md`, `docs/advanced/api-reference.md` + `docs/ru/advanced/api-reference.md`, спеки. Публичного разрыва в рамках alpha нет. Тесты не менялись (используют `e.Asc`/`e.Desc`, тип не именуют) |

ℹ️ **Наблюдения (фикс не требуется):**
- **XML-doc покрытие полное; новых публичных типов без доки нет.** `<summary>` есть у `NamedWindowOrderKey`,
  `WindowDefinition`, `WindowFrameExclusion`, `MultiIfBranch<T>`, у новых членов
  `WindowFunction<T>`/`WindowFrame`/`EntityBuilder`/`QueryDefinition`/`QueryCommand`, у методов
  `SqlServerFunctions`/`ClickHouseFunctions` и у флагов/хука `ISqlDialect` (интерфейс+база+провайдеры).
  Приложение A (45) без изменений; `CS1591` остаётся в `<NoWarn>` 7 библиотечных `.csproj` (Шаг 5).
- **Имена — конвенции соблюдены, P0/P1 нет.** snake_case `xml_value`/`xml_query`/`xml_exist`/`multi_if`/
  `lag_in_frame`/`lead_in_frame` — принятое SQL-зеркало; `when`/`otherwise` — C#-хелперы builder'а;
  `Supports*`/`Make*`, `WindowFrame.Groups`/`WithExclusion`, `WindowFrameExclusion` — как соседние
  семейства. BCL-конфликтов нет.
- **План-ключ и рендер согласованы.** `MakeNamedWindow` в param-режиме проходит все ключи без вывода SQL
  (`SqlBuilder.cs:529-607`), поэтому захваченные константы попадают в параметры в том же порядке, что и
  SQL-проход; `WindowsPlanHash` (`QueryCommand.cs:51`, расчёт `QueryCommand.QueryPreparer.cs:555-596`)
  сворачивает имя/партицию/порядок/рамку+`Exclusion`, `WindowsEqual`
  (`QueryPlanEqualityComparer.cs:163-199`) — зеркально, `CopyTo` переносит `_windows`/`WindowsPlanHash`
  (`QueryCommand.Clone.cs:62,30`). Мёртвого или расходящегося ключа нет.
- **`nodes` и кросс-провайдерный `multi_if` не вводятся обоснованно пер-функция.**
  `SupportsXmlFunction("nodes")` = `false` (нужен внешний reference в `CROSS/OUTER APPLY`,
  ср. `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4); `multi_if` — точная функция только ClickHouse (у остальных
  `CASE WHEN` уже отдаётся через `iif`/тернарник); `lagInFrame`/`leadInFrame` — единственный
  frame-respecting вариант (у остальных `lag`/`lead` фрейм игнорируют). Отдельные `Make*`-хуки не
  вводятся осознанно.
- **`WINDOW`/`GROUPS`/`EXCLUDE` гейтятся точечно.** default `false` в базе; транслятор бросает
  `NotSupportedException` до рендера (`WindowFunctionTranslator.cs:87-101`), а `WindowSql.IsValidWindowName`
  (`WindowSql.cs:43`) пропускает только ASCII-идентификатор, т.к. имя встраивается в SQL verbatim.

**Проверка (сводный прогон Batch 3, 20.09.2026):** `dotnet build nextorm.sln -c Release` —
**0 warnings / 0 errors**; unit (Debug, 0 failed): core **174**, postgres **232**, sqlserver **207**,
sqlite **226**, mysql **57**, mariadb **24**, clickhouse **175**; контейнерная интеграция (Podman):
`PostgresSpecificTests` **21/0**, общий PostgreSQL **213/0** (6 skip), ClickHouse **50/0**,
`SqlServerSpecificTests` **16/0**, `DialectCapabilityContractTests` **8/0**, MySQL **213/0** (7 skip);
`rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает RD2/NW1/X1/MF1); Приложение A (45) без изменений.
Точечные блоки `NW1`/`X1`/`MF1` выше содержат промежуточные счётчики периода разработки фич — актуальны
числа этого сводного прогона.

### SQL Server нативный `PIVOT`/`UNPIVOT` (точечный аудит 20.09.2026)

Область (uncommitted поверх `8670bba`; план фичи, пункты выполненного бэклога Phase 2):
новый публичный источник `PivotAggregate`/`PivotValue`/`UnpivotColumn`/`PivotExpression`
(`Query/PivotExpression.cs`, новый файл), `FromExpression.Pivot`, `EntityBuilder<TEntity>.Pivot`/`.Unpivot`,
члены `ISqlDialect`/`SqlDialectBase` (`SupportsPivot`/`MakePivot`/`SupportsUnpivot`/`MakeUnpivot`) и
override'ы `SqlServerDialect`. Build Release — **0 warnings / 0 errors** (этот проход).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| PIV-DOC1 | ✅ (был P2) | `Builders/EntityBuilder.cs:616,641-642` | XML-doc обещает несуществующий алиас: `<param name="values">` — «each optionally renaming its result column», `<param name="columns">` — «each optionally aliasing its name-column value», `nameColumnName` — «source column name/alias». Ни `PivotValue`, ни `UnpivotColumn` не имеют члена-алиаса: T-SQL не допускает `AS` внутри `IN (...)`, имя колонки = значение, переименование — только в проекции (ср. `docs/guide/provider-specific/sqlserver.md:106-118`). Дока вводит в заблуждение | Привести доки к факту: значение `IN` и есть имя результирующей колонки; алиаса нет. Убрать «optionally renaming»/«aliasing» и «name/alias». ✅ Применено 21.09.2026: дока приведена к факту (`EntityBuilder.cs:616-619,643-645`) |
| PIV-NAME1 | ✅ (был P2) | `Query/PivotExpression.cs:40,62` | Фабрики `PivotValue.Create(string)`/`UnpivotColumn.Create(string)` — в репозитории нет прецедента `Of`; соседи используют `Create` (`TableFunctionExpression.Create`, `Expressions/TableFunctionExpression.cs:50`) или смысловые имена (`TemporalClause.AsOf`/`Between`/`All`). `Of` — java-изм, а не конвенция .NET (`Create`/`From` по skill:api-design) | Переименовать в `PivotValue.Create`/`UnpivotColumn.Create` (или `From`). Alpha — замена на месте; правок EN+RU требуют `docs/guide/provider-specific/sqlserver.md` + RU и `docs/advanced/api-reference.md` + RU (грепнуть оба дерева). ✅ Применено 21.09.2026: `Create` (`Query/PivotExpression.cs:40,62`); EN+RU-доки и примеры обновлены |
| PIV-RD2 | P2 | `PublicAPI.*.txt` отсутствуют | Поверхность `PIVOT`/`UNPIVOT` (4 новых public-типа + 10 геттеров `PivotExpression` + 2 метода `EntityBuilder` + 4 члена `ISqlDialect`) не трекается. **Продолжение RD2/AR1/SQ1 (не новая находка):** `PublicApiAnalyzers` не подключён, Шаг 5 открыт. `ISqlDialect.SupportsPivot`/`MakePivot`/`SupportsUnpivot`/`MakeUnpivot` — абстрактные члены интерфейса ⇒ source-breaking для внешних реализаторов (в репо реализует только `SqlDialectBase`) | Внести подписи в `PublicAPI.Unshipped.txt` при заморозке; определиться с политикой abstract-флагов (SQ-DIM) |

ℹ️ **Наблюдения (фикс не требуется):**
- **XML-doc покрытие полное; новых публичных типов/членов без доки нет.** `<summary>` есть у `PivotAggregate`
  (и всех 5 членов), `PivotValue`/`Value`/`Of`, `UnpivotColumn`/`Column`/`Of`, `PivotExpression` и всех 10
  геттеров, `FromExpression.Pivot`, `EntityBuilder.Pivot`/`Unpivot`, флагов/хуков `ISqlDialect` +
  `SqlDialectBase` + `SqlServerDialect`. Приложение A (45) без изменений; `CS1591` остаётся в `<NoWarn>`
  7 библиотечных `.csproj` (Шаг 5). Новых `.csproj` нет — CPM не затронут.
- **Имена конвенциям соответствуют; P0/P1 нет.** `PivotAggregate` — enum с PascalCase-членами;
  `PivotExpression` — носитель выражения; `Supports*`/`Make*`, `PivotValue`/`UnpivotColumn` — как соседние
  семейства (`TableSampleClause`, `TemporalClause`). BCL-конфликтов нет (`Pivot*`/`Unpivot*` в BCL
  отсутствуют). `Pivot` — метод-глагол на builder'е, читается как `ArrayJoin`/`TableSample`.
- **`PivotExpression` — flags + nullable-поля вместо явной дискриминации.** `IsUnpivot` + взаимно-исключающие
  `AggregateColumn`/`ForColumn`/`Values` (PIVOT) vs `UnpivotValueColumn`/`UnpivotNameColumn`/`Columns`
  (UNPIVOT); некорректное состояние снаружи не создать (ctor'ы `internal`, фабрики `ForPivot`/`ForUnpivot`).
  Приемлемо для alpha; менять не обязательно (при заморозке можно рассмотреть два подтипа).
- **Обновление доков EN+RU выполнено** (`docs/guide/provider-specific/sqlserver.md` + `docs/ru/...`,
  `docs/advanced/api-reference.md` + RU, `docs/providers/sqlserver.md` + RU). Публичных переименований не
  было.
- **PIV-DOC1/PIV-NAME1 исправлены 21.09.2026.** `PivotValue.Create`/`UnpivotColumn.Create`
  (`Query/PivotExpression.cs:40,62`); XML-`<param>` больше не обещают несуществующий алиас
  (`Builders/EntityBuilder.cs:616-619,643-645`). PIV-RD2 остаётся открытым (Шаг 5 открыт: `PublicAPI.*.txt`
  нет). Док-разрыв `todo_mssql_pivot_derived.md` закрыт (файл удалён, ограничение снято).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (по данным задачи,
0 failed): core **175**, postgres **235**, sqlserver **214**, sqlite **230**, mysql **58**, mariadb **25**,
clickhouse **176**; интеграция SQL Server — `SqlServerSpecificTests` **18/0**, `DialectCapabilityContractTests`
**10/0**; покрытие line **84.8%** / branch **73.8%** (база 84.7% / 73.6%);
`rg --files -g 'PublicAPI*.txt'` — пусто (подтверждает PIV-RD2 = продолжение RD2); Приложение A (45) без
изменений.

### Capability-объекты диалекта (Фаза 1) — точечный аудит 20.09.2026

**Область:** `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs` (новый публичный файл),
`ISqlDialect.cs`, `SqlDialectBase.cs`, шесть `*Dialect.cs`; сопутствующее `PreparedHaving` в
`QueryCommand.*`/`SqlBuilder.cs` — см. `code-smells-review.md`, Находка 32. Build Release **0/0**.

Реализован переходный «capability object» из IF5/IF6: 4 новых публичных интерфейса + 4 публичных
DIM-свойства; флаги `Supports*` и рендеры `Make*` стали **вычисляемыми делегатами** к объектам.
Extend-only соблюдён — новые члены интерфейса объявлены DIM с дефолтом `null`, поэтому внешние
реализации `ISqlDialect` продолжают компилироваться без правок.

| ID | Ур. | Место | Суть | Фикс |
|---|---|---|---|---|
| DC1 | P2 | `DialectCapabilities.cs:11,27,42,56` | Смешение конвенций внутри одного нового семейства: `IIifRenderer`/`ILimitByRenderer` названы `*Renderer`, а `ISessionInfoFunctions`/`IUuidGenerators` — существительными во множественном числе, хотя все четыре — capability-провайдеры (у двух последних `Supports(name)`+`Render`, у первых — только `Render`). | Либо `ISessionInfoFunctionRenderer`/`IUuidGeneratorRenderer` (единый суффикс `*Renderer`), либо зафиксировать различие в `<summary>` (поимённый предикат). Alpha — переименовать сейчас дешевле всего. |
| DC2 | P2 | `DialectCapabilities.cs:62` | `ILimitByRenderer.Render(int,int,IReadOnlyList<string>,StringBuilder)` — новый публичный контракт принимает мутабельный `System.Text.StringBuilder` и возвращает `void`; остальные три — `string Render(...)`. Потребителю/тесту приходится самому создавать builder. | Предпочтительно `string Render(int limit, int offset, IReadOnlyList<string> columns)` (как `IIifRenderer`); если append к builder осознан (перф), задокументировать контракт в `<param>`/`<remarks>`. |
| DC3 | P2 | `ISqlDialect.cs:326,354,526,938`; `DialectCapabilities.cs:11,27,42,56` | **Шаг 5 (IF6) открыт:** `PublicAPI.Shipped/Unshipped.txt` нет, `PublicApiAnalyzers` не подключён, ApiCompat/approval-теста нет — 8 новых членов не трекаются (RS0016/RS0025 не срабатывают). | Перенесено в [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53)); вносить типы `NextORM.Core.IIifRenderer`/`ISessionInfoFunctions`/`IUuidGenerators`/`ILimitByRenderer` и их `Render`/`Supports`, плюс свойства `ISqlDialect.Iif.get`/`SessionInfoFunctions.get`/`UuidGenerators.get`/`LimitBy.get` (nullable-аннотации `?` обязательны; файл — с `#nullable enable`). |
| DC4 | P2 | `docs/advanced/api-reference.md`, `docs/ru/advanced/api-reference.md` | Новые публичные типы/свойства не упомянуты в curated API-reference; AGENTS.md требует править `docs/**` **и** `docs/ru/**` в том же изменении. | Добавить краткий раздел про capability-объекты в оба дерева (EN+RU) при заморозке. |

**XML-доки:** все 4 интерфейса и 8 членов задокументированы (`<summary>`/`<param>`) — Приложение A (45)
не меняется. `CS1591` остаётся в `<NoWarn>` всех 7 библиотечных `.csproj` (IF6), поэтому будущий пропуск
сборкой не гейтится.

**Отмечено, но менять не обязательно (ℹ️):**
- `SqlDialectBase.Supports*` остаются `virtual`, а `ISqlDialect.Supports*` — DIM: наследник может
  независимо переопределить флаг `true` без объекта, и тогда `Make*` бросит — класс A для внешнего
  диалекта не закрыт **структурно**. Фаза 3 удалила старые пары (21.09.2026);
  контрактный тест `DialectCapabilityContractTests` перебирает только in-repo диалекты.
- Имена `SessionInfoFunctions`/`UuidGenerators`/`LimitBy`/`Iif` BCL-конфликтов не создают (`Iif` —
  PascalCase-аббревиатура). P0/P1 по именам нет.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`rg --files -g 'PublicAPI*.txt'` — пусто; новые сущности: `IIifRenderer`, `ISessionInfoFunctions`,
`IUuidGenerators`, `ILimitByRenderer` (4 типа) и DIM-свойства `Iif`/`SessionInfoFunctions`/
`UuidGenerators`/`LimitBy` (4).

### Capability-объекты диалекта (Фаза 2 + 2a) — точечный аудит 21.09.2026

**Область:** `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs` (12 новых публичных интерфейсов),
`ISqlDialect.cs` (12 новых nullable DIM-свойств + 12 вычисляемых `Supports*`, `Make*` из abstract/throwing в
вычисляемые делегаты), `SqlDialectBase.cs` (12 `virtual` object-свойств `=> null`, вычисляемые делегаты,
удаление throwing-заглушек), шесть `*Dialect.cs` (override объекта вместо `Make*`),
`tests/nextorm.integration.tests/DialectCapabilityContractTests.cs`. Build Release **0 warnings / 0 errors**
(этот проход). Продолжение аудита Фазы 1 (DC1–DC4 выше).

Изменение **аддитивное/relaxing**: 12 новых типов и 12 DIM-свойств; abstract-члены `ISqlDialect` стали DIM,
поэтому внешние реализации продолжают компилироваться. Фаза 2+2a была аддитивной; публичных переименований в
ней не было, поэтому синхронизация EN/RU (AGENTS.md) формально не требовалась. Фиксы DC5 переименовали 4 типа и
синхронизировали `docs/**`+`docs/ru/**`; 12 типов внесены в curated API-reference (DC4 закрыт, EN/RU `:69`). P0/P1 по именам — **нет** (BCL/`CA1716`/`CA1724`-конфликтов нет:
`Xml`/`Uniq`/`MultiIf`/`Pivot`/`Lock`/`TableSample`).

| ID | Ур. | Место | Суть | Фикс |
|---|---|---|---|---|
| DC5 | ✅ (был P2) | `DialectCapabilities.cs:95,109,119,156` vs `:132,146,169,182,193,203,214` | **DC1 расширен.** Правило Фазы 1 («предикат+рендер ⇒ существительное во мн. числе; чистый рендер ⇒ `*Renderer`») нарушено в обе стороны: `ITableSampleRenderer:156` несёт `Supports(TableSampleMethod):159`, но назван `*Renderer`; `ISequenceAggregates:95`/`IUniqAggregates:109`/`IQuantileAggregates:119` предиката не несут, но названы существительными во мн. числе, тогда как `IMultiIfRenderer:132`/`IDistinctOnRenderer:146`/`IPivotRenderer:169`/`IArrayJoinRenderer:182`/`IDateConversionRenderer:193`/`IStringSplitRenderer:203`/`ILockRenderer:214` — `*Renderer`. | Выбрать один признак и применить без исключений (alpha — переименовать сейчас). Вариант A (правило предиката): `ITableSampleRenderer` → `ITableSampleMethods`, `ISequenceAggregateRenderer`/`IUniqAggregateRenderer`/`IQuantileAggregateRenderer`. Вариант B: зафиксировать «агрегатные семейства — мн. число, скалярные рендеры — `*Renderer`» и переписать обоснование DC1 (тогда `ITableSampleRenderer` с предикатом остаётся исключением). |
| DC6 | ✅ (был P2) | `ISqlDialect.cs:162` vs `:1116,1122` | Омонимия, внесённая Фазой 2: абстрактный `bool SupportsArrayJoin { get; }` (`:162`) гейтит скалярную функцию `arrayJoin(array)`, а новый объект `IArrayJoinRenderer? ArrayJoin` (`:1122`) с флагом `SupportsArrayJoinClause => ArrayJoin is not null` (`:1116`) — клаузу уровня `FROM`. Свойство `ArrayJoin` читается как пара к `SupportsArrayJoin`, но по факту пара к `SupportsArrayJoinClause`. | Переименовать свойство/интерфейс к клаузе (`ArrayJoinClause`/`IArrayJoinClauseRenderer`) либо скалярный флаг (`SupportsArrayJoinFunction`); смешение терминов в публичной поверхности дороже переименования. |
| DC7 | ℹ️ (принято) | `ISqlDialect.cs:1077,1098`; `SqlDialectBase.cs:262,270` | `SupportsPivot` и `SupportsUnpivot` оба вычисляются как `Pivot is not null`: один объект `IPivotRenderer` **принудительно** уравнивает поддержку PIVOT и UNPIVOT, хотя это два разных SQL-конструкта. Провайдер «только PIVOT» моделью не выражается. | Сегодня расхождения нет (SQL Server умеет оба) → не блокер. Если понадобится: разнести на два объекта (`Pivot`/`Unpivot`) либо вынести политику в объект; как минимум зафиксировать co-support в `<summary>` `IPivotRenderer`. |
| DC8 | P2 | `DialectCapabilities.cs` (12 типов); `ISqlDialect.cs` (`:156,271,277,426,432,448,455,494,500,572,578,1036,1041,1077,1083,1098,1116,1122,1153,1158`); `SqlDialectBase.cs:37,43,69,111,118,135,161,184,218,260,281,660` | **Продолжение DC3 (Шаг 5):** 12 новых публичных интерфейсов и 12 новых DIM-свойств `ISqlDialect` (плюс 12 `virtual`-свойств `SqlDialectBase`) не трекаются: `PublicAPI.Shipped/Unshipped.txt` нет, `PublicApiAnalyzers`/ApiCompat/approval-теста нет, `CS1591` — в `<NoWarn>` всех 7 библиотечных `.csproj`. | **Перенесено в [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53)).** Вносить при заморозке: типы `NextORM.Core.IXmlFunctions`/`ISequenceAggregateRenderer`/`IUniqAggregateRenderer`/`IQuantileAggregateRenderer`/`IMultiIfRenderer`/`IDistinctOnRenderer`/`ITableSampleMethods`/`IPivotRenderer`/`IArrayJoinRenderer`/`IDateConversionRenderer`/`IStringSplitRenderer`/`ILockRenderer` и их члены; свойства `ISqlDialect.<Prop>.get` с nullable-аннотацией `?` (`XmlFunctions`, `SequenceAggregates`, `UniqAggregates`, `QuantileAggregates`, `MultiIf`, `DistinctOn`, `TableSample`, `Pivot`, `ArrayJoinClause`, `DateConversion`, `StringSplit`, `Lock`) и одноимённые `SqlDialectBase.<Prop>.get`; файл — с `#nullable enable`. Удалённые Фазой 3 дублирующие `Supports*`/`Make*`-пары **не** вносить. |

**Стало (21.09.2026, фиксы применены).**
- **DC5 — закрыт.** Типы переименованы по правилу «предикат+рендер ⇒ существительное во мн. числе;
  чистый рендер ⇒ `*Renderer`»: `ITableSampleMethods` (был `ITableSampleRenderer`; несёт `Supports`),
  `ISequenceAggregateRenderer`/`IUniqAggregateRenderer`/`IQuantileAggregateRenderer` (были
  `ISequenceAggregates`/`IUniqAggregates`/`IQuantileAggregates`; предиката нет). Импл-классы переименованы
  синхронно: `SqlServerTableSampleMethods`/`PostgresTableSampleMethods`,
  `ClickHouseSequenceAggregateRenderer`/`ClickHouseUniqAggregateRenderer`/`ClickHouseQuantileAggregateRenderer`.
  Свойства `ISqlDialect.TableSample`/`SequenceAggregates`/`UniqAggregates`/`QuantileAggregates` не менялись.
  Проверка: `rg '\bITableSampleRenderer\b|\bISequenceAggregates\b|\bIUniqAggregates\b|\bIQuantileAggregates\b' src tests` — пусто.
- **DC6 — закрыт.** Свойство `ArrayJoin` → `ArrayJoinClause` в `ISqlDialect` (`:1122`), `SqlDialectBase`
  (`:281`) и `ClickHouseDialect` (`:287`) + контрактный тест (`:84,201,245`). Скалярный `SupportsArrayJoin`
  не тронут: клауза теперь читается однозначно (`SupportsArrayJoinClause` ↔ `ArrayJoinClause`).
- **DC7 — принято осознанно (ℹ️).** Один `IPivotRenderer` на пару PIVOT/UNPIVOT — намеренно; расхождения
  поддержки сегодня нет, объект описан как «PIVOT/UNPIVOT pair» (`DialectCapabilities.cs:165-168`,
  `ISqlDialect.cs:1077,1098`). Разнесение на два объекта отложено до реальной необходимости.
- **DC8 — перенесён в [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53))** (Шаг 5, по решению автора).

**Отложенные ANSI-гейты (класс C, остались `bool`) — оценка.** Обоснование RFC принято: у перечисленных
флагов нет provider-specific `Make*`-хука, их орфография идёт через общий рабочий ANSI-эмиттер
(`MakeAggregate`/`MakeWithinGroup`) либо прямо из переводчика, поэтому расхождение «объявлено, но рендер
бросает» структурно невозможно. Проверено по коду: `SupportsInFrameWindowFunctions` — нативные
`lagInFrame`/`leadInFrame` рендерятся `WindowSql`-маппингом при общем эмиттере; `SupportsAnyValueAggregate`/
`SupportsArgMinMax`/`SupportsBoolean|Bit|Statistical|RegressionAggregates` —
через `MakeAggregate`; `SupportsIfAggregates`/`SupportsFilter` **удалены (24.09.2026)**: заменены одним
публичным `AggregateFilterStyle` (`AnsiFilter`/`IfCombinator`/`None`), который выбирает ANSI `filter (where ...)`
или ClickHouse `-If` для общего API `SqlFunctions.Sql.count/sum/avg/min/max` (дочерние `ClickHouse.count_if`/… удалены); `SupportsChoose` — `BuiltinFunctionTranslator.EmitChoose` эмитит `choose(...)` при
гейте. Objectify'ить сейчас **не нужно**; рекомендуется зафиксировать сам критерий (нет отдельного
`Make*`-хука) зафиксирован в этом реестре; трекинг поверхности — `todo_public_api_freeze.md`. Единственный кандидат на будущее — `SupportsChoose`
(единственный гейт с SQL Server-only именем и без `Make*`), но он тоже безопасен: имя пишется эмиттером,
а не диалектом.

**Статус DC1–DC8 (актуализация 21.09.2026, после фиксов).**
- **DC1 — ✅ закрыт** вместе с DC5: правило предиката применено ко всем 16 типам (12 рендер-онли — `*Renderer`,
  4 предикатных — существительное во мн. числе).
- **DC2 — ✅ закрыт:** `ILimitByRenderer.Render` стал `string Render(int limit, int offset, IReadOnlyList<string> columns)`
  (`DialectCapabilities.cs:70`); `StringBuilder`/`void` в публичном контракте больше нет, `ISqlDialect.MakeLimitBy`
  аппендит результат сам (`ISqlDialect.cs:992`).
- **DC3/DC8 — перенесены в [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53))** (Фаза 3 удалила дублирующие пары; заморозку/трекинг ведёт этот todo-файл).
- **DC4 — ✅ закрыт:** 4 объекта Фазы 1 — `docs/advanced/api-reference.md:68` (+RU); 12 типов Фазы 2 с
  новыми именами DC5 — `:69` в обоих деревьях (`rg` по `docs/` без `specs/design` — старых имён нет);
  матрица [`docs/specs/comparison/capability-matrix.md`](../comparison/capability-matrix.md) синхронна.

**XML-доки:** все 16 публичных интерфейсов (12 новых) и их члены имеют `<summary>`; у
`IPivotRenderer.RenderPivot`/`RenderUnpivot` нет `<param>` (CS1591 не гейтится). Приложение A (45) не
меняется. **Проверка:** `dotnet build nextorm.sln -c Release` — **0/0**; новых сущностей —
`DialectCapabilities.cs` 16 интерфейсов (12 в этой фазе), 17 вычисляемых `Supports*` на 16 объектов;
`rg --files -g 'PublicAPI*.txt'` — пусто.

**Проверка (после фиксов DC5/DC6, 21.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings /
0 errors** (перепроверено в этом проходе); `DialectCapabilityContractTests` — **3/3 passed**
(перепроверено). `rg '\bITableSampleRenderer\b|\bISequenceAggregates\b|\bIUniqAggregates\b|\bIQuantileAggregates\b|\bRenderHint\b' src tests`
— пусто; `ArrayJoinClause` присутствует в `ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect`/тесте.
`DialectCapabilities.cs` — 16 интерфейсов (имена без старого смешения). По отчёту исполнителя: unit core 177 /
sqlite 241 / sqlserver 234 / postgres 241 / mysql 62 / mariadb 27 / clickhouse 181, 0 failed; full integration
1005 / 0 failed / 30 skipped; покрытие 85.0 % line / 73.2 % branch (дип от 85.1/73.8 — структурно
недостижимые DIM-фолбэки `SqlDialectBase`; носители покрыты: `SqlServerPivotRenderer` 50/50,
`SqlServerXmlFunctions` 6/6, `SqlServerTableSampleMethods` 16/16, `SqlServerLockRenderer` 6/6, Postgres-носители 100 %).
Приложение A (45) без изменений; `rg --files -g 'PublicAPI*.txt'` — пусто (DC3/DC8 — трекинг в [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53).

### Фаза 3 — удаление дублирующих `Supports*`/`Make*` — точечный аудит 21.09.2026

**Область:** `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs`,
`DialectCapabilities.cs`, шесть `*Dialect.cs`; трансляторы (`StringFunctionTranslator`, `XmlSqlTranslator`,
`DateConversionSqlTranslator`, `SessionInfoFunctionTranslator`, `UuidFunctionTranslator`,
`AdvancedAggregateTranslator`, `BuiltinFunctionTranslator`), `DataContext/SqlBuilder.cs`,
`DataContext/SqlSourceRenderer.cs`; 6 dialect-тестов и `DialectCapabilityContractTests`. Build Release
**0/0**, контрактный тест **2/2** (перепроверено этим аудитом). Фаза 3 фичи capability-объектов выполнена (21.09.2026); рабочий RFC-файл удалён после миграции содержимого (закрытое → этот реестр и `docs/**`, открытое → `todo_public_api_freeze.md`);
`ClickHouseDialect.MakeDateConversion` удалён (логика — в
`ClickHouseDateConversionRenderer`).

**Удалён 41 уникальный член** (пары `Supports*`/`Make*`, ставшие вычисляемыми делегатами; в
`ISqlDialect` + `SqlDialectBase` — 60 деклараций): `SupportsStringSplit`/`MakeStringSplit`; `SupportsXmlFunctions`/`SupportsXmlFunction`/
`MakeXmlFunction`; `SupportsDateConversionFunctions`/`MakeDateConversion`; `SupportsSessionInfoFunctions`/
`SupportsSessionInfoFunction`/`MakeSessionInfoFunction`; `SupportsUuidGenerators`/`SupportsUuidGenerator`/
`MakeUuidGenerator`; `SupportsUniqAggregates`/`MakeUniqAggregate`; `SupportsQuantileAggregates`/
`MakeQuantile`/`MakeMedian`; `SupportsSequenceAggregates`/`MakeSequenceAggregate`; `SupportsIif`/`MakeIif`;
`SupportsMultiIf`/`MakeMultiIf`; `SupportsLimitBy`/`MakeLimitBy`; `SupportsDistinctOn`/`MakeDistinctOn`;
`SupportsTableSample`/`SupportsTableSampleMethod`/`MakeTableSample`; `SupportsPivot`/`MakePivot`/
`SupportsUnpivot`/`MakeUnpivot`; `SupportsArrayJoinClause`/`MakeArrayJoin`; `SupportsLocking`/
`LockingUsesTableHints`/`MakeLock`/`MakeLockHint`. Единственный источник поддержки/рендеринга — объект
(`dialect.X?.Render(...)` / `dialect.X is not null`). **Остаточных ссылок в `src`/`tests`/`docs` нет**,
кроме исторических упоминаний в этом реестре и в самом RFC.

**Актуальный список заморозки (заменяет построчные `SupportsX`/`MakeX`-записи).** Трекинг — issue
[`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) — issue [#53 «IF6: Заморозка и трекинг публичного API»](https://github.com/AlexeyShirshov/nextorm/issues/53).
В `PublicAPI.*.txt` вносить:
- 16 интерфейсов: `IIifRenderer`, `ISessionInfoFunctions`, `IUuidGenerators`, `ILimitByRenderer`,
  `IXmlFunctions`, `ISequenceAggregateRenderer`, `IUniqAggregateRenderer`, `IQuantileAggregateRenderer`,
  `IMultiIfRenderer`, `IDistinctOnRenderer`, `ITableSampleMethods`, `IPivotRenderer`, `IArrayJoinRenderer`,
  `IDateConversionRenderer`, `IStringSplitRenderer`, `ILockRenderer` (+ их члены);
- 16 nullable DIM-свойств `ISqlDialect`: `Iif`, `SessionInfoFunctions`, `UuidGenerators`, `LimitBy`,
  `XmlFunctions`, `SequenceAggregates`, `UniqAggregates`, `QuantileAggregates`, `MultiIf`, `DistinctOn`,
  `TableSample`, `Pivot`, `ArrayJoinClause`, `DateConversion`, `StringSplit`, `Lock`, и их `virtual`
  реализации в `SqlDialectBase`;
- остаточная **не-объектная** поверхность класса B/C (64 `Supports*`-члена `ISqlDialect` на момент
  аудита + их хуки): `SupportsFinal`/`MakeFinal`, `SupportsSample`/`MakeSample`, `SupportsSettings`/
  `MakeSettings`, `SupportsPreWhere`, `SupportsApply`/`MakeApply`, `SupportsNamedWindows`,
  `SupportsCommandBehaviorSingleRow`, `MakeRepeat`, `MakeStringPosition`/`MakeStringReverse`, …;
- **не вносить** удалённые пары — они в поверхность не входят.

**Что считать устаревшим (superseded):** freeze-части записей `IF6` (список `SupportsIif`/`MakeIif`),
`U4` (`MakeUniqAggregate`), `Q3` (`SupportsQuantileAggregates`/`MakeQuantile`/`MakeMedian`), `LIM1`
(`SupportsLimitBy`/`MakeLimitBy`), `AJ1` (`SupportsArrayJoinClause`/`MakeArrayJoin`), `X1`
(`SupportsXmlFunctions`/`SupportsXmlFunction`/`MakeXmlFunction`), `DC3`/`DC8` (перечни 4/12 capability-членов) —
а также `RD2 (CH-часть)` (date conversion + `string.Split`), `RD2 (batch 2)`/`RD2 (batch 3)`/`SQ1`/`SQ-DIM`/`MF1`/`PIV-RD2` (их удалённые подмножества) — в части удалённых имён **недействительны**; вносить соответствующие capability-объекты; актуальный перечень — выше, в [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) и issue #53.

**Совместимость (alpha):** удаление публичных членов — source+binary-breaking для внешних реализаторов
`ISqlDialect`/`SqlDialectBase` (их `override` перестаёт компилироваться). Политика alpha допускает;
фиксация поверхности — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue #53). P0/P1 по именам новых членов нет; XML-доки сохранены у capability-объектов.

**Единственный незакрытый пункт Фазы 3:** заморозка/трекинг публичного API — ведётся в
[`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53)).
Удаление дублирующих пар (собственно Фаза 3 RFC) закрыто 21.09.2026; открыт только трекинг поверхности.

### Аудит 21.09.2026 — квотирование идентификаторов: XML-доки и публичная поверхность

Фича «provider-aware quoted identifiers» (uncommitted). Новые публичные члены:

| Член | Файл:строка | XML-док |
|------|-------------|:-------:|
| `ISqlDialect.QuoteIdentifier(string)` (DIM, ANSI `"name"`) | `DataContext/Dialect/ISqlDialect.cs:576` | ✅ |
| `SqlDialectBase.QuoteIdentifier` (`virtual`, = `Escape(name)`) | `DataContext/Dialect/SqlDialectBase.cs:269` | ✅ |
| `SqliteDialect.QuoteIdentifier` (override, ANSI) | `nextorm.sqlite/SqliteDialect.cs:147` | ✅ |
| `DataContextBuilder.QuoteIdentifiers` (`get`) | `DI/DataContextBuilder.cs:26` | ✅ |
| `DataContextBuilder.UseQuotedIdentifiers(bool = true)` | `DI/DataContextBuilder.cs:60` | ✅ |
| `IContextEnvironment.QuoteIdentifiers` (DIM `=> false`) | `DataContext/Roles/IContextEnvironment.cs:20` | ✅ |
| `DataContext.QuoteIdentifiers` (`get`) | `DataContext/DataContext.cs:86` | ❌ **нет `<summary>`** |
| `EntityBuilder<TEntity>.WithQuotedIdentifiers(bool = true)` | `Builders/EntityBuilder.cs:1243` | ✅ |
| `EntityBuilder.WithQuotedIdentifiers(bool = true)` | `Builders/EntityBuilder.cs:1385` | ✅ |
| `QueryCommand.QuoteIdentifiers` (`bool?`, `get; internal set;`) | `Query/QueryCommand.cs:314` | ✅ |
| `QueryCommand<TResult>.WithQuotedIdentifiers(bool = true)` | `Query/QueryCommand.TResult.cs:360` | ✅ |
| `VisitorOptions.QuoteIdentifiers` (13-й позиционный параметр) | `Visitors/VisitorOptions.cs:29` | — (у record-параметров доков и раньше не было) |

Задокументировано **10 из 11** новых публичных членов; пробел — `DataContext.QuoteIdentifiers`
(соседние члены `DataContext` тоже без доков, но этот — новый; `CS1591` скрыт в `<NoWarn>`).
`ContextEnvironment.QuoteIdentifiers` (`DataContext/ContextEnvironment.cs:44`) — `internal`, под
`CS1591` не попадает. Новых публичных **типов** нет → **Приложение A (45) не меняется**.
Подробности и P-уровни — в §3, «Аудит 21.09.2026 — квотирование идентификаторов».

### Аудит 21.09.2026 — соглашения об именовании (snake_case): XML-доки и публичная поверхность

Фича «provider-independent naming conventions (snake_case)» (uncommitted). Новые публичные члены:

| Член | Файл:строка | XML-док |
|------|-------------|:-------:|
| `INamingConvention` (новый публичный тип) | `DataContext/Meta/INamingConvention.cs:13` | ✅ |
| `INamingConvention.TableName(string, bool)` / `.ColumnName(string)` | `:18,21` | ✅ (`<param>`) |
| `SnakeCaseNamingConvention` (новый публичный тип, `sealed`) | `DataContext/Meta/SnakeCaseNamingConvention.cs:14` | ✅ |
| `SnakeCaseNamingConvention.Instance` | `:17` | ✅ |
| `SnakeCaseNamingConvention.TableName` / `.ColumnName` | `:20,26` | ✅ (`<inheritdoc>`) |
| `DataContextBuilder.NamingConvention` (`get`) | `DI/DataContextBuilder.cs:33` | ✅ |
| `DataContextBuilder.UseNamingConvention(INamingConvention?)` | `:81` | ✅ |
| `IContextEnvironment.NamingConvention` (DIM `=> null`) | `DataContext/Roles/IContextEnvironment.cs:28` | ✅ |
| `DataContext.NamingConvention` (`get`) | `DataContext/DataContext.cs:98` | ✅ |
| `IEntityMetadata.IsTableNameAuto` | `DataContext/Meta/IEntityMetadata.cs:20` | ✅ |
| `IPropertyMetadata.IsColumnNameAuto` | `DataContext/Meta/IPropertyMetadata.cs:22` | ✅ |
| `EntityBuilder<TEntity>.WithNamingConvention(INamingConvention?)` | `Builders/EntityBuilder.cs:1263` | ⚠️ self-`cref` |
| `EntityBuilder.WithNamingConvention(INamingConvention?)` | `:1424` | ✅ |
| `QueryCommand<TResult>.WithNamingConvention(INamingConvention?)` | `Query/QueryCommand.TResult.cs:374` | ✅ |
| `QueryCommand.NamingConvention` (`get; internal set;`) | `Query/QueryCommand.cs:326` | ✅ |
| `VisitorOptions.NamingConvention` (`init`-свойство в теле record) | `Visitors/VisitorOptions.cs:44` | ✅ |

Задокументировано **15 из 15** новых публичных членов (100 %); `ContextEnvironment`/
`ResolvedNamingConvention`/`FromExpression.IsAutoMapped`/`SourceIsInterface` — `internal`/поля с `<summary>`.
`CS1591` по-прежнему в `<NoWarn>` всех 7 библиотечных `.csproj`. `IPropertyMetadata.IsColumnNameAuto` (как и
`IsTableNameAuto`) добавлены **абстрактно** — см. §3, P2-1. Новые публичные **типы** (`INamingConvention`,
`SnakeCaseNamingConvention`) **задокументированы** → счётчик недокументированных в Приложении A (45) не
меняется. Пользовательская проза EN+RU обновлена (`docs/getting-started/03-entities-and-metadata.md:152-203`,
`docs/ru/getting-started/03-entities-and-metadata.md:141-191`); **но** curated-индекс
`docs/advanced/api-reference.md` (+RU) не дополнен — §3, P2-3.

## 3. Находки

### Статус находок (актуализация 18.09.2026)

P0 (#1–#9) и **все P1/P2 выполнены** 18.09.2026 (alpha; см. §5.1–§5.4). Актуальные `file:line` (после переименований):

| # | Ур. | Текущее место | Статус |
|---|---|---|---|
| 1 | P0 | `Expressions/XxHash32.cs:19` (`XxHash32`, больше не `HashCode`) | ✅ исправлено |
| 2 | P0 | `Query/IQueryRegistry.cs:15` | ✅ исправлено |
| 3 | P0 | `DataContext/DataContextExtensions.cs` (`With`/`WithRecursive` слиты сюда) | ✅ исправлено |
| 4 | P0 | `Builders/Joins/JoinedEntityBuilder.cs` | ✅ исправлено |
| 5 | P0 | `DataContext/QueryPreparationException.cs:14` (наследует `DataContextException`) | ✅ исправлено |
| 6 | P0 | `Query/RawSqlOverride.cs` (`internal`, свойства) | ✅ исправлено |
| 7 | P0 | `Builders/Projection.cs` (`Item1`…`Item8`) | ✅ исправлено |
| 8 | P0 | `BasicHelpers` удалён (мёртвый код, 0 вхождений) | ✅ исправлено |
| 9 | P0 | `Builders/TableAlias.cs` (`GetInt32`/`GetInt64`/…, параметр `columnName`) | ✅ исправлено |
| 10 | P1 | `Query/SqlFunctions.cs` (`SqlFunctions` + `CommonFunctions`; `*MI`/`SQLExpression` — `internal`) | ✅ исправлено |
| 11 | P1 | `DataContext/Meta/IEntityMetadata.cs:11`, `IPropertyMetadata.cs:13` | ✅ исправлено |
| 12 | P1 | `Query/WindowFunctions.cs` (Window* вынесены из DSL) | ✅ исправлено |
| 13 | P1 | `Parameter.cs:10`, `Query/IParameterProvider.cs:12,19` | ✅ исправлено |
| 14 | P1 | `ExpressionExtensions.cs`, `Visitors/ReplaceExpressionVisitor.cs` | ✅ исправлено |
| 15 | P1 | `Visitors/ParameterVisitors.cs:12` | ✅ исправлено |
| 16 | P1 | `DataContext/ValueList.cs:15,25,92,100` | ✅ исправлено |
| 17 | P1 | `Builders/Paging.cs:17`, `Expressions/Sorting.cs:20` (свойства, не поля) | ✅ исправлено |
| 18 | P1 | `DataContext/SqlBuilder.cs:16` | ✅ исправлено |
| 19 | P2 | `Builders/Paging.cs` (`Limit`/`Offset` — свойства с валидацией), `Expressions/Sorting.cs` | ✅ исправлено |
| 20 | P2 | `SqlTableAttribute.cs`, `Query/IColumnsProvider.cs` (файл = тип) | ✅ исправлено |
| 21 | P2 | `Payload/IPayload.cs` удалён (пустой маркер без использования) | ✅ исправлено |
| 22 | P2 | `DataContext/DataContext.cs`, `DI/DataContextBuilder.cs`, `*DataContext`, `InMemoryDataContext` | ✅ исправлено |
| 23 | P2 | `Query/RawSqlOverride.cs` (internal) | ✅ закрыто вместе с P0-6 |
| 24 | P2 | все `Default*Provider` в `Query/`; `DefaultParameterProvider.cs` выделен | ✅ исправлено |
| 25 | P2 | `nextorm.core.sourcegenerator/AnonymousClassGenerator.cs` (`internal`, переименован) | ✅ исправлено |
| 26 | P2 | all `src` — `namespace NextORM.Core` / `NextORM.<Provider>` | ✅ исправлено |
| 27 | P1 | `SqlFunctions.Postgres`/`.SqlServer`/`.ClickHouse` (`PostgresFunctions`/`SqlServerFunctions`/`ClickHouseFunctions`) | ✅ исправлено |
| 28 | P2 | `{Provider}DataContextOptionsBuilderExtensions` (уникальные имена, файлы переименованы) | ✅ исправлено |

### P0 — вводят в заблуждение / конфликтуют с BCL

Все пункты P0 **выполнены** (alpha: переименование на месте, без `[Obsolete]`/алиасов).

| # | Было | Стало | Комментарий |
|---|------|-------|-------------|
| 1 | `HashCode` (public struct) | `XxHash32` (public) | Затенение `System.HashCode` устранено; реализация xxHash32. Файл `Expressions/HashCode.cs` → `XxHash32.cs`; тест `HashCodeTests` → `XxHash32Tests`. `internal` не сделан, т.к. тип покрыт тестами из отдельной сборки |
| 2 | `IQueryProvider` | `IQueryRegistry` | Конфликт с `System.Linq.IQueryProvider` устранён. Файл/интерфейс переименованы |
| 3 | `IDataContextExtensions`, `DataContextExtensions` | `DataContextExtensions` | `With`/`WithRecursive` перенесены в `DataContextExtensions.cs`; файл-дубль удалён (было два `*Extensions` для одного `IDataContext`) |
| 4 | `EntityP2`…`EntityP8` | `JoinedEntityBuilder<T1,T2>`…`<T1..T8>` | Файл `JoinCommandBuilder.cs` → `JoinedEntityBuilder.cs` |
| 5 | `PrepareException : Exception` | `QueryPreparationException : DataContextException` | Заодно `BuildSqlCommandException : DataContextException` — единая библиотечная база |
| 6 | `DbQueryCommandExtension` (public поля) | `RawSqlOverride` (`internal sealed`, свойства) | Удалён из публичной поверхности; `ManualSql`/`MakeParams` — свойства |
| 7 | `Projection.t1`…`t8` | `Projection.Item1`…`Item8` | Рекомендация `T1`…`T8` **нереализуема**: член не может совпадать с именем generic-параметра (`CS0102`). Использованы идиоматичные tuple-имена `ItemN` (как в `System.Tuple`). Обновлены `MemberTranslator` (позиция по числовому суффиксу), `AliasFromProjectionVisitor` (`ItemN` → SQL-алиас `tN`), `InMemoryDataContext`/`QueryPlanner` (reflection по имени `ItemN`). Повторно рассмотрены и **отклонены** (21.09.2026) `TableN`/`EntityN`/`ProjectionN`/`SourceN`: позиция — «N-й источник join'а» и не всегда таблица/сущность/проекция (derived `QueryCommand`, CTE, raw `TableAlias`, `ArrayJoin`-элемент), а `ItemN` сохраняет tuple-идиому |
| 8 | `BasicHelpers` + файл `BasicHelpders.cs` | удалён | Мёртвый код: 0 вхождений в `src`/`tests`/`benchmarks`; класс был свалкой неиспользуемых extension-методов |
| 9 | `TableAlias.Int/Long/Short/String/Float/...` | `GetInt32`/`GetInt64`/`GetInt16`/`GetString`/`GetSingle`/… | Параметр `string _` → `string columnName`. Обновлён `BaseExpressionVisitor` (`nameof(TableAlias.*)`) и примеры в доках |

### P1 — нарушения конвенций именования типов

| # | Имя | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| 10 | `NORM_SQL`, `NORM` | `src/nextorm.core/Query/NORM.cs:7,162` | `NORM_SQL` — snake_case + вложенный тип; `NORM` — аббревиатура CAPS; весь fluent-surface использует snake_case (`count_big`, `avg_distinct`, `row_number`) и C#-keywords (`var`, `all`, `any`, `@in`). Домен — SQL-подобный DSL, поэтому частично оправдано, но публично это нарушение | Переименовать в `SqlFunctions` (или `NORM.Sql`) с PascalCase-методами и таблицей маппинга; публичные поля `*MI`/`SQLExpression` (MethodInfo/ConstantExpression) сделать `internal` |
| 11 | `IEntityMeta`, `IPropertyMeta` | `src/nextorm.core/DataContext/Meta/IEntityMeta.cs:3`, `IPropertyMeta.cs:5` | `Meta` — сокращение; рядом уже `EntityMetadataBuilder` — рассогласование | `IEntityMetadata`, `IPropertyMetadata` |
| 12 | `NORM.NORM_SQL` вложенность `WindowFunction<T>`, `WindowOrder`, `WindowFrame`, `WindowFrameBound`, `WindowFrameType`, `WindowFrameBoundKind` | `NORM.cs:23,63,79,86,96,125` | Публичные вложенные типы в DSL-классе; FQN вида `NORM.WindowFrameType` неочевидны | Вынести в `nextorm.core` как самостоятельные типы (или в `SqlFunctions.*`) |
| 13 | `Param`, `ParamList`, `IParamProvider`, `DefaultParamProvider`, `ParamExpressionVisitor2`, `NORM.Param<T>` | `ParamList.cs:3,8`, `Query/IParamProvider.cs` и др. | `Param` — сокращение `Parameter`; `ParamExpressionVisitor2` — хвостовая цифра-версия | `Parameter`, `ParameterList`; `ParamExpressionVisitor2` → осмысленное имя или `internal` |
| 14 | `TypeExpressionVisitor<T>`, `TwoTypeExpressionVisitor<T1,T2>`, `ReplaceConstantsExpressionVisitor`, `ReplaceConstantVisitor` | `ExpressionExtensions.cs:31,51,81`, `Visitors/ReplaceExpressionVisitor.cs:21` | Несогласованный суффикс (`*Visitor` vs `*ExpressionVisitor`), singular/plural (`Constant` vs `Constants`), «Two» vs цифра | Единый суффикс `ExpressionVisitor`; `ReplaceConstantsExpressionVisitor` |
| 15 | `TestSpecialMethodCallVisitor` | `src/nextorm.core/Visitors/ParamExpressionVisitor.cs:5` | Префикс `Test` в production-типе; публичный | `internal` либо `SpecialMethodCallVisitor` |
| 16 | `Buffer3`, `Buffer10`, `ValueList`, `ValueList3` | `src/nextorm.core/DataContext/ValueList.cs:7,11,74,78` | Магические числа в имени; `ValueList3` не согласован с `ValueList`; реализации внутреннего буфера | `internal` или единая схема `ValueList<T>`/`ValueList3<T>` с документированной арифметикой |
| 17 | `Paging`, `Sorting` (structs) | `Paging.cs:2`, `Sorting.cs:3` | Публичные структуры-детали реализации | `internal` либо оформить как публичный контракт (см. #19) |
| 18 | `SqlBuilder` (public struct) | `src/nextorm.core/DataContext/SqlBuilder.cs:9` | Деталь реализации (диалект, провайдеры); конструктор с 9 параметрами | `internal` |
| 27 | `NORM.PG`, `NORM.MS`, `NORM.CLK` (public nested) | `src/nextorm.core/Query/NORM.PG.cs:15`, `NORM.MS.cs:14`, `NORM.CLK.cs:13` | Провайдер-специфичные DSL-классы названы аббревиатурами из 2–3 букв; углубляют вложенность `NORM.*` | `NORM.Postgres`/`NORM.SqlServer`/`NORM.ClickHouse` (или вынести из `NORM`) |

#### P1, безопасная партия — выполнено 18.09.2026

Переименования публичной поверхности и перевод деталей реализации в `internal` (alpha, без алиасов):

| # | Было | Стало | Комментарий |
|---|------|-------|-------------|
| 11 | `IEntityMeta`, `IPropertyMeta` | `IEntityMetadata`, `IPropertyMetadata` | Файлы и `internal`-реализации `EntityMeta`/`PropertyMeta` → `EntityMetadata`/`PropertyMetadata` |
| 13 | `Param`, `ParamList`, `IParamProvider`, `DefaultParamProvider`, `ParamExpressionVisitor2`, `NORM.Param<T>` | `Parameter`, ~~`ParamList`~~, `IParameterProvider`, `DefaultParameterProvider`, `ParameterBinderVisitor` (`internal`), `NORM.Parameter<T>` | `ParamList` был мёртвым (0 вхождений) — удалён; `ParamExpressionVisitor2` сделан `internal` и переименован. Файл `Query/IParamProvider.cs` → `IParameterProvider.cs` |
| 14 | `TwoTypeExpressionVisitor<T1,T2>`, `ReplaceParameterVisitor`, `ReplaceConstantVisitor` | `TypeExpressionVisitor<T1,T2>`, `ReplaceParameterExpressionVisitor`, `ReplaceConstantExpressionVisitor` | Единый суффикс `ExpressionVisitor` + арность `T1, T2`; `ReplaceConstantsExpressionVisitor` (plural — полная параметризация) сохранён как намеренно отдельный тип |
| 15 | `TestSpecialMethodCallVisitor` (public) | `TestSpecialMethodCallVisitor` (`internal`) | Префикс `Test` больше не протекает в публичную поверхность |
| 16 | `Buffer3`, `Buffer10`, `ValueList`, `ValueList3` (public) | те же имена, `internal` | Магические числа не значимы для потребителя |
| 18 | `SqlBuilder` (public struct) | `SqlBuilder` (`internal`) | Деталь реализации с 9-параметрическим конструктором |

#### P1, DSL и контракт `Paging`/`Sorting` — выполнено 18.09.2026

| # | Было | Стало | Комментарий |
|---|------|-------|-------------|
| 10 | `NORM`, `NORM_SQL` | `SqlFunctions` (FQN `NextORM.Core.SqlFunctions`), `CommonFunctions` (FQN `NextORM.Core.CommonFunctions`) | Типы переименованы; `*MI`/`SQLExpression` — `internal`. Методы DSL **оставлены** snake_case/PascalCase-by-keyword (`count_big`, `@in`, `any`/`all`/`var`) как SQL-зеркало — см. «Отмечено, но менять не рекомендуется» |
| 12 | `NORM.WindowFunction<T>`, `NORM.WindowOrder`, `NORM.WindowFrame`, `NORM.WindowFrameBound`, `NORM.WindowFrameType`, `NORM.WindowFrameBoundKind` | `WindowFunction<T>`, `WindowOrder`, `WindowFrame`, `WindowFrameBound`, `WindowFrameType`, `WindowFrameBoundKind` (top-level) | Вынесены в `Query/WindowFunctions.cs` |
| 27 | `NORM.PG`/`MS`/`CLK` + `NORM.PG_SQL`/`MS_SQL`/`CLK_SQL` | `PostgresFunctions`/`SqlServerFunctions`/`ClickHouseFunctions` + `SqlFunctions.Postgres`/`.SqlServer`/`.ClickHouse` | Пользовательский путь `SqlFunctions.Postgres.any(...)`; `Sql`/`SQL` |
| 17 | `Paging`/`Sorting` (public mutable fields) | `Paging`/`Sorting` (валидирующие свойства) | `internal` невозможен: `ISqlDialect.MakePage(Paging, …)` публичен и реализуется 6 сборками-провайдерами. `Paging.Limit/Offset` валидируют неотрицательность; `Sorting.Direction/PreparedExpression` — свойства (см. #19) |

### P2 — несогласованность публичной поверхности

| # | Имя | Файл:строка | Проблема |
|---|-----|-------------|----------|
| 19 | `Paging.Limit/Offset`, `Sorting.Direction/PreparedExpression` | `Paging.cs:4-5`, `Sorting.cs:6-7` | Публичные **изменяемые поля** вместо свойств; нет валидации, сигнатуру нельзя менять без правки всех вызовов |
| 20 | `SqlTableAttribute` в файле `TableAttribute.cs`; `IColumnsProvider` в файле `ISourceProvider.cs` | `TableAttribute.cs:3`, `Query/ISourceProvider.cs:7` | Несоответствие «файл ↔ тип»; мешает навигации |
| 21 | `IPayload` | `src/nextorm.core/Payload/IPayload.cs:3` | Пустой маркерный интерфейс без документации и без членов |
| 22 | `DbContext`, `IDataContext`, `InMemoryContext`, `DbContextBuilder`, `DataContextOptionsBuilderExtensions` | `DataContext/DbContext.cs`, `DI/DataContextOptionsBuilder.cs:5`, providers `DI/…` | Смешение «DbContext» и «DataContext»/«Context»; builder называется `DbContextBuilder`, а его extensions — `DataContextOptionsBuilderExtensions` |
| 23 | ~~`DbQueryCommandExtension` (sing.) vs `*Extensions`~~ | ✅ закрыто вместе с P0-6: тип стал `internal` и выведен из публичной поверхности |
| 24 | `DefaultColumnsProvider` в `Query/`, `DefaultAliasProvider` в `DataContext/`, `DefaultParamProvider` в `Query/IParamProvider.cs` | | Непоследовательное размещение `Default*`-реализаций |
| 25 | `AnonymousClassEqualityComparer` (public) | `src/nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs:6` | Публичный тип генератора; должен быть `internal` |
| 26 | namespace `nextorm.core` (lowercase) | все файлы `src` | Пространства имён .NET должны быть PascalCase (`NextORM.Core`) |
| 28 | `DataContextOptionsBuilderExtensions` | `src/nextorm.{postgres,sqlite,sqlserver,mysql,mariadb,clickhouse}/DI/DataContextOptionsBuilderExtensions.cs` | Одноимённый публичный класс-расширение в 6 провайдерах; при нескольких `using` IntelliSense/`cref` неоднозначны, документация дублируется. Разнести по namespace или дать уникальные имена (`PostgresDataContextOptionsBuilderExtensions` …) |

#### P2 — выполнено 18.09.2026

- #19: `Paging.Limit`/`Offset` — свойства с проверкой неотрицательности; `Sorting.Direction`/`PreparedExpression` — свойства (backing-поля приватные). Object-initializer'ы и `sorting[i].Direction = …` продолжат работать.
- #20: `TableAttribute.cs` → `SqlTableAttribute.cs`; `Query/ISourceProvider.cs` → `Query/IColumnsProvider.cs`.
- #21: `IPayload` удалён (пустой маркер, 0 потребителей); `: IPayload` снят с `CreateEnumeratorPayload`/`CreateMainEnumeratorPayload`.
- #22: унифицировано на `DataContext`: `DbContext` → `DataContext`, `DbContextBuilder` → `DataContextBuilder` (`CreateDbContext` → `CreateDataContext`), провайдерные `*DbContext` → `*DataContext`, `InMemoryContext` → `InMemoryDataContext`. EF-типы (`Microsoft.EntityFrameworkCore.DbContext`, `EFDataContext`, `EFInMemoryDataContext`) не затронуты.
- #24: `DefaultAliasProvider.cs` перенесён в `Query/`; `DefaultParameterProvider` выделен в `Query/DefaultParameterProvider.cs` (рядом с `DefaultColumnsProvider`).
- #25: `AnonymousClassEqualityComparer` → `internal AnonymousClassGenerator` (`AnonymousClassGenerator.cs`).
- #26: `namespace nextorm.core` → `NextORM.Core`, `nextorm.<provider>` → `NextORM.<Provider>`, `nextorm.core.sourcegenerator` → `NextORM.Core.SourceGenerator`; обновлены `using`/квалифицированные ссылки и reflection-строка `NextORM.Core.Projection` в `InMemoryProjectionFactory`.
- #28: классы и файлы переименованы в `{Provider}DataContextOptionsBuilderExtensions` (Postgres/Sqlite/SqlServer/MySql/MariaDb/ClickHouse).

### Отмечено, но менять не рекомендуется

- `Sql*` (`SqlTableAttribute`, `ISqlDialect`, `SqlDialectBase`) — распространённая аббревиатура (как `System.Data.SqlClient`), допустима.
- Методы DSL (`@in`, `var`, `all`, `any`, `count_big`, …) — **оставлены** как есть: это SQL-зеркало, а C#-keywords вынуждены из-за деревьев выражений; перевод в PascalCase требует отдельной таблицы «метод → SQL-токен» и правки всего translator-слоя, который диспетчеризует по `nameof`. Решение осознанное: переименованы только типы (#10).
- `I*` интерфейсы, `*Exception`, `*Attribute`, `*Builder`, `*Options` — суффиксы соблюдены.

### Аудит 21.09.2026 — агрегатные терминалы внутри коррелированного подзапроса (P0/P1/P2 — нет)

Изменение `Visitors/CorrelatedQueryExpressionVisitor.cs` (rewrite `Count`/`Min`/`Max`/`Avg`/`Sum`/
`Stdev`/`Stdevp`/`Var`/`Varp` в `Select(<агрегат>).First()`) публичной поверхности **не касается**:
все новые члены (`CountMI`, `ReplaceAggregateTerminal`, `BuildAggregateSelect`, `UnwrapLambda`,
`AggregateMethodFor`, `SelectMethodFor`) — `private`/`private static`; XML-`<summary>` обновлён только
у приватного `IsAggregateTerminal`. Публичных переименований/добавлений нет, поэтому обязательной
синхронизации `docs/**` + `docs/ru/**` (AGENTS.md) не требуется.

Приложение A (45 публичных типов без XML-документации) и статус Шага 5 не меняются: `PublicAPI.*.txt`
по-прежнему отсутствуют, `CS1591` — в `<NoWarn>` всех 7 библиотечных `.csproj`. Косметика: четыре
приватных хелпера без `<summary>` — на CS1591/Приложение A не влияет (см. `code-smells-review.md`,
точечный аудит 21.09.2026).

### Аудит 21.09.2026 — глубина корреляции ≥ 2 и SQLite-гард `Single` (P0/P1/P2 — нет)

Изменение публичной поверхности не касается. Все новые члены — `internal`/`private`:
`QueryCommand.OuterRegistry` (`QueryCommand.cs:342`), `QueryCommand.RootRegistry` (`:362`),
`QueryCommand.IsCardinalityGuardable` (`:350`), `QueryCommand.PreparedHaving` (`:52`);
`WrapSingleScalarCardinalityGuard`/`CountAllMI`/`AbsMI` (`QueryCommand.QueryPreparer.cs:221-241`) и
`ReplaceParametersByValueVisitor` (`CorrelatedQueryExpressionVisitor.cs:394`, вложенный) — `private`.
`MemberTranslator.VisitIndex` и `ExpressionPlanEqualityComparer.VisitIndex` получили ветвления, подписи
не менялись.

Изменено **поведение** уже существующих публичных `IQueryRegistry.AddCommand`/`AddOuterReference`
(`QueryCommand.cs:406,444`): при выставленном `OuterRegistry` вызов форвардится корневому реестру.
Сигнатуры не менялись, переименований нет; синхронизация `docs/**` + `docs/ru/**` (AGENTS.md) не
требуется. Контракт интерфейса формально не расширен.

Косметика (не P-уровень): `ReplaceParametersByValueVisitor` выпадает из принятого суффикса
`*ExpressionVisitor` (`ReplaceConstantExpressionVisitor`, `ReplaceParametersExpressionVisitor` в
`Visitors/ReplaceExpressionVisitor.cs`); тип `private`, на CS1591/Приложение A не влияет.

Шаг 5 не меняется: `PublicAPI.Shipped/Unshipped.txt` отсутствуют, `PublicApiAnalyzers`/ApiCompat/
approval-теста нет, `CS1591` — в `<NoWarn>` всех 7 библиотечных `.csproj`; Приложение A (45) без
изменений. Находки по именованию P0/P1/P2 — **нет**. Содержательные находки 36–38 (корректность и
дизайн реестра/гарда) — в `docs/specs/design/code-smells-review.md`; Находка 36 исправлена 21.09.2026,
открыты 37 (ложный `Single`-гард при `DISTINCT`/`GROUP BY`/`UNION`) и 38 (робастность `OuterRegistry`).

### Аудит 21.09.2026 — коррелированный CROSS/OUTER APPLY (P1 закрыт 21.09.2026)

**Область:** `Builders/EntityBuilder.cs` (четыре новые перегрузки `CrossApply`/`OuterApply`),
`Expressions/JoinExpression.cs` (`From` field-backed, `ApplySource`/`SetFrom` — `internal`),
`DataContext/Dialect/ISqlDialect.cs` + `SqlDialectBase.cs` + `SqlServerDialect.cs`/`PostgresDialect.cs`/
`MySqlDialect.cs` (`SupportsApply`/`MakeApply`). Сопутствующие запахи — `code-smells-review.md`,
Находки 39–42. Build Release **0/0**.

Именование семейства корректно: `CrossApply`/`OuterApply` повторяют уже существующие value-перегрузки
(`EntityBuilder.cs:941,947,986,992`), `SupportsApply`/`MakeApply` следуют конвенции `Supports*`/`Make*`
(как `SupportsJoinStrictness`/`MakeJoinKeyword`), `ApplySource`/`SetFrom`/`BuildQueryCommand` — `internal`.
BCL-конфликтов (`CA1716`/`CA1724`) и именований P0 нет.

| ID | Ур. | Место | Суть | Фикс |
|---|---|---|---|---|
| AP1 | ✅ (был P1) | `EntityBuilder.cs:999,1006,1013,1020`; `Builders/Joins/JoinedEntityBuilder.cs:13,20-22,31-34` | Новые correlated-перегрузки наследуются `JoinedEntityBuilder<T1..Tn>` (`: EntityBuilder<Projection<...>>`) с `TEntity = Projection<T1,T2>` и возвращают **вложенную** арность `JoinedEntityBuilder<Projection<T1,T2>, T>`; наследуемый путь теряет исходные join'ы (`_joins = [applyJoin]`) и падает `BuildSqlCommandException: Table name is not registered for type Projection`2[...]` (воспроизведено). Value-перегрузки `new` (`JoinedEntityBuilder.cs:31-34`) работают и сохраняют плоскую арность. Детали — `code-smells-review.md`, Находка 39 | Объявить `new`-перегрузки correlated-формы на каждом `JoinedEntityBuilder<T1..T8>` (плоская арность, сохранение `_joins`) либо скрыть наследуемые; регресс-тест `joined.CrossApply(p => …)` → точный SQL |
| AP2 | P2 | `EntityBuilder.cs:941,947,986,992,999,1006,1013,1020` | Рассинхрон имён параметров внутри одного семейства: `_` (value `EntityBuilder<TJoinEntity>`), `query` (`QueryCommand<TJoinEntity>`), `source` (новые `Expression<Func<…>>`). `_` — discard-имя в публичном методе, неудобно для именованных аргументов; новые перегрузки добавили третий вариант | Выровнять: `source` для correlated-формы, `query` для `QueryCommand`, `source` (или `right`) для value-формы; `_` заменить в alpha |
| AP3 | P2 | `ISqlDialect.cs:46,53`; `SqlDialectBase.cs:25,314-319`; `SqlServerDialect.cs:32,197-202`; `PostgresDialect.cs:34`; `MySqlDialect.cs:23` | `SupportsApply` и `MakeApply` — новые **абстрактные** члены `ISqlDialect` ⇒ source-breaking для внешних реализаторов (в репозитории реализует только `SqlDialectBase`); Шаг 5 открыт, `PublicAPI.*.txt` нет. **Продолжение NW1/RD2/DC3, не новая находка.** Асимметрия «флаг абстрактный, хук абстрактный» — как в SQ-DIM | Внести в `PublicAPI.Unshipped.txt` при заморозке; для `SupportsApply` рассмотреть DIM `=> false` (политика SQ-DIM), чтобы не ломать внешние реализации. `SupportsApply`/`MakeApply` строкой в блоке заморозки RD2 |
| AP4 | ✅ (был P2) | `EntityBuilder.cs:997-1023`; `JoinExpression.cs:69-74` | У четырёх новых методов есть `<summary>`/`<param>`, но нет `<typeparam name="TJoinEntity">`/`<returns>`; `<summary>` дословно дублируется между `QueryCommand`- и `EntityBuilder`-вариантами; у `internal SetFrom` есть `<summary>`, но нет `<param>`. `CS1591` — в `<NoWarn>` всех 7 библиотек, сборкой не гейтится | Дописать `<typeparam>`/`<returns>`; свести дублирующиеся `<summary>` через `<inheritdoc/>`; `<param name="from">` у `SetFrom` — при закрытии Шага 5 |
| AP5 | ✅ (был P2) | `docs/guide/03-joins.md:181-184`; `docs/ru/guide/03-joins.md:183-187` | Гайд **противоречит** фиче: блок «**Correlation is not expressible yet**» / «**Корреляция пока не выражается**» утверждает, что применяемый источник не может ссылаться на левую строку, и описывает только value-перегрузки (`.CrossApply(dataContext.From<…>())`); новая correlated-форма (`s => …`, `p.Item1`) не документирована. AGENTS.md требует `docs/**` **и** `docs/ru/**` в том же изменении | Заменить врезку на описание correlated-формы (EN+RU), привести пример `CrossApply(s => ctx.From<…>().Where(x => x.Id == s.Id)…)`, синхронизировать таблицу провайдеров |
| AP6 | ✅ (был ℹ️) | `docs/advanced/api-reference.md:84`; `docs/ru/advanced/api-reference.md:84` | Строка таблицы `JoinType` ссылается на `[CrossApply](xref:NextORM.Core.EntityBuilder`1)`/`[OuterApply](…)` вместо `NextORM.Core.JoinType.CrossApply`/`OuterApply` — xref ведёт в `EntityBuilder`, не в член enum'а. Не связано с новой фичей (строка не менялась), но соседствует с ней | Исправить xref на `NextORM.Core.JoinType.CrossApply`/`.OuterApply` в обеих ветках |

**Стало (21.09.2026).**
- **AP1 — закрыт.** `EntityBuilder.JoinApply` отклоняет correlated-форму над join-проекцией:
  `typeof(TEntity).TryGetProjectionDimension(out _)` → `NotSupportedException` «…cannot reference a join
  projection…» (`EntityBuilder.cs:1028-1030`). Вызов `joined.CrossApply(p => …)` теперь падает внятно на
  построении (probe подтвердил), а не `BuildSqlCommandException` из рендера. Тест
  `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:610`
  `CrossApply_OnJoinedProjection_ShouldThrowClearNotSupported`; sqlserver **221/0**. Наследуемая
  перегрузка остаётся в поверхности (арность `JoinedEntityBuilder<Projection<T1,T2>, T>` не flattened),
  но корректно гейтится; при заморозке можно добавить `new`-перегрузки на `JoinedEntityBuilder<T1..T8>`
  для плоской арности (не обязательно для alpha).
- **AP4 — закрыт.** `<typeparam name="TJoinEntity">` добавлен всем четырём перегрузкам
  (`EntityBuilder.cs:997-1023`). Остаточная косметика (нет `<returns>`, дословные дубли `<summary>`,
  `<param name="from">` у `SetFrom`) — на Шаг 5.
- **AP5 — закрыт.** Врезка «Correlation is not expressible yet» удалена; добавлен раздел «Correlated
  APPLY / LATERAL» в `docs/guide/03-joins.md:181` и `docs/ru/guide/03-joins.md:183`; синхронно
  обновлены `docs/advanced/limitations.md` (+RU), `sql-capabilities-gap-analysis.md`,
  `todo_correlated_apply_lateral.md`, `plan-correlated-subqueries.md`.
- **AP6 — закрыт.** Xref'ы в `docs/advanced/api-reference.md:84` и RU заменены на
  `NextORM.Core.JoinType.CrossApply`/`.OuterApply`.
- **AP2 (имена параметров `_`/`query`/`source`) и AP3 (abstract `SupportsApply`/`MakeApply` + заморозка)
  — открыты**, см. таблицу и блок заморозки ниже.

**Заморозка (Шаг 5, продолжение блока RD2/DC3).** Внести при подключении `PublicApiAnalyzers`:
`EntityBuilder<TEntity>.CrossApply/OuterApply<TJoinEntity>(Expression<Func<TEntity, QueryCommand<TJoinEntity>>>)`
и `(Expression<Func<TEntity, EntityBuilder<TJoinEntity>>>)` (4 подписи, `source`);
`ISqlDialect.SupportsApply.get`/`MakeApply(JoinType, string)`;
`SqlDialectBase.SupportsApply.get`/`MakeApply`, override'ы `SqlServerDialect`/`PostgresDialect`/`MySqlDialect`.
`JoinExpression.ApplySource`/`SetFrom` и `BuildQueryCommand` — `internal`, в `PublicAPI.*.txt` не идут;
`JoinExpression.From` сигнатуру не менял (`get`/`init`, `required`), RS0016/RS0017 не ожидается.

### Аудит 21.09.2026 — производный источник как первичный `FROM` + `Join` (публичный API без изменений)

**Область:** `Builders/EntityBuilder.cs` — новые private `ResolveJoinBase` (`:1079-1093`),
`HasNoNonWhereModifiers` (`:1099-1104`), `ApplyWhereToJoined<TJoinEntity>` (`:1112-1121`),
`ReplaceTargetParameterVisitor` (`:1126-1130`); изменения `JoinCore`/`JoinApply`/`CreateJoined`.
Публичных типов/членов **не добавлено**, `ISqlDialect`/диалекты не менялись, рендер — существующий
`MakeFrom` (`FromExpression.SubQuery`). Build Release **0/0**.

Публичная поверхность: изменений нет → P0/P1/P2 по публичному API **нет**; BCL-конфликтов
(`CA1716`/`CA1724`) нет. Приложение A (45) без изменений. Шаг 5 (заморозка) открыт:
`PublicAPI.Shipped/Unshipped.txt` нет, `PublicApiAnalyzers`/ApiCompat/approval-теста нет, `CS1591` — в
`<NoWarn>` всех 7 библиотечных `.csproj`.

Приватные хелперы (в `PublicAPI.*.txt` не попадают; переименования применены 21.09.2026):

| ID | Ур. | Место | Суть | Фикс |
|---|---|---|---|---|
| DF1 | ✅ (был P2) | `EntityBuilder.cs:1099-1104` | `HasOnlyWhereModifier()` читалось как «есть только `Where`», а возвращало `true` и при полном отсутствии модификаторов; вдобавок пропускало `IsDistinct` (корректность — `code-smells-review.md`, Находка 43) | Применено: `HasNoNonWhereModifiers()` (+ `!IsDistinct && !IsFinal && SampleRatio is null && SampleOffset == 0`) |
| DF2 | ✅ (был P2) | `EntityBuilder.cs:1079-1093` | `BuildJoinBase()` в ветке «только `Where`» не строил, а возвращал `_query`; «Build» вводил в заблуждение | Применено: `ResolveJoinBase()` (больше не откатывается молча в `ToCommand()` для derived — бросает `NotSupportedException`) |
| DF3 | ✅ (был P2) | `EntityBuilder.cs:1112-1121` | `CarryWhereCondition()` — нестандартный глагол `carry` | Применено: `ApplyWhereToJoined()` (+ private `ReplaceTargetParameterVisitor` по `ReferenceEquals`) |

XML-`<summary>` есть у `ResolveJoinBase`, `HasNoNonWhereModifiers`, `ApplyWhereToJoined`,
`ReplaceTargetParameterVisitor`, `CreateJoined`, `GetJoinSource`; `<typeparam>`/`<returns>` не требуются
(private, `CS1591` подавлен в 7 `.csproj`). Корректностные находки 43 (`IsDistinct`) и 44 (type-based
rebind) **закрыты 21.09.2026** — `docs/specs/design/code-smells-review.md`; гейт in-memory — дополнение к
Находке 41 (тот же файл).

**Заморозка:** новых публичных подписей нет → по этой фиче в `PublicAPI.Unshipped.txt` вносить нечего
(в отличие от AP2/AP3 correlated-APPLY). `todo_mssql_derived_from_join.md` удалён. **Дополнение 21.09.2026:**
`docs/specs/roadmap/todo_mssql_pivot_derived.md` тоже удалён (выводы свёрнуты в доки), ограничение снято —
`PIVOT`/`UNPIVOT` принимают производный источник, публичной поверхности по-прежнему не добавлено (см.
«Аудит 21.09.2026 — PIVOT/UNPIVOT по производному источнику» ниже).

### Аудит 21.09.2026 — PIVOT/UNPIVOT по производному источнику (публичный API без изменений)

**Область:** `Builders/EntityBuilder.cs:668-690` (`ResolvePivotInner` принимает `_query`), XML-`<summary>`
`Pivot`/`Unpivot`/`ResolvePivotInner`. Тесты: `Pivot_ShouldAcceptDerivedSource`,
`Pivot_ShouldAcceptDerivedComputedSource`, `Unpivot_ShouldAcceptDerivedSource`,
`Pivot_ShouldThrowWhenDerivedSourceHasModifiers`
(`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2325-2395`); интеграционные
`Pivot_ShouldReshapeDerivedSource`/`Unpivot_ShouldStackDerivedColumns`
(`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:288,304`). Публичных типов/членов **не
добавлено**; `ISqlDialect`/диалекты/рендер (`SqlSourceRenderer.MakePivot`, `QueryPreparer.PrepareFrom`) не
менялись. Build Release **0/0**.

Публичная поверхность: изменений нет → P0/P1/P2 по публичному API **нет**; BCL-конфликтов
(`CA1716`/`CA1724`) нет. XML-doc изменённых `<summary>` полное; Приложение A без изменений. Шаг 5 открыт:
`PublicAPI.Shipped/Unshipped.txt` нет (PIV-RD2 остаётся), `PublicApiAnalyzers`/ApiCompat/approval-теста нет,
`CS1591` — в `<NoWarn>` всех 7 библиотечных `.csproj` (ср. skill:dotnet-api-surface-validation). Корректностные
находки 45/46 (там же) **исправлены 21.09.2026**; новых публичных подписей они не добавили
(`PivotExpression.CloneForCache` — `internal`, XML-доки приватного/внутреннего члена в `PublicAPI.*.txt` не
идут). Остаточный P2 — только **PIV-RD2** (заморозка поверхности, Шаг 5).

### Аудит 21.09.2026 — закрытие source-скоупа в `IColumnsProvider` (публичный API без изменений)

**Область (uncommitted):** `Query/IColumnsProvider.cs` — расширен XML-`<summary>` `PopSourceScope` (`:40-44`, добавлено «entries remain addressable by index … but stop satisfying any enclosing command's lookups»); реализация `Query/DefaultColumnsProvider.cs` — приватное поле `_list` сменило форму записи `(Type, QueryCommand?, bool)` → `(Type, QueryCommand?, bool, bool)`, `PopSourceScope`/`FindAlias`/`FindQueryCommand` помечают/пропускают закрытые записи. Публичных типов/членов **не добавлено, не удалено и не переименовано**; подпись `void PopSourceScope()` не менялась, `_list` — private. Build Release **0/0**; тесты — `DerivedSourceWithJoinThenJoin_*` в шести `SqlGenerationTests.cs` + интеграционный (`CommonTestSuite.Join.cs:226`).

Публичная поверхность: изменений нет → P0/P1/P2 по публичному API **нет**; BCL-конфликтов (`CA1716`/`CA1724`) нет. XML-doc изменённых членов: `PopSourceScope` документирован полностью (`<summary>` + `<see cref="FindAlias(ParameterExpression, bool)"/>`), `PushSourceScope` был документирован ранее — оба изменённых члена покрыты. Остальные члены этого же публичного интерфейса (`HasAliases`, `Add`×2, `FindAlias`×3, `FindQueryCommand`, `PushScope`, `PopScope`) XML-доков не имеют — **пре-существующий** member-level пробел (тип-уровневый `<summary>` есть, поэтому `IColumnsProvider` не входит в Приложение A из 45). Кандидат к Шагу 5, не к этому фиксу; в `code-smells-review.md` — наблюдение D.

Приложение A (45) без изменений. Шаг 5 (**заморозка**) открыт: `PublicAPI.Shipped/Unshipped.txt` нет, `PublicApiAnalyzers`/ApiCompat/approval-теста нет, `CS1591` — в `<NoWarn>` всех 7 библиотечных `.csproj`. Корректностная находка (закрытый source-скоуп) закрыта в `code-smells-review.md`, «закрытие source-скоупа в `DefaultColumnsProvider`»; новых публичных подписей она не добавила → в `PublicAPI.Unshipped.txt` вносить нечего. Трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53).

### Аудит 21.09.2026 — скалярный ключ `GroupBy` (`GroupBy(e => e.Int)`): публичный API без изменений

**Область (uncommitted):** `Query/QueryCommand.QueryPreparer.cs` — `PrepareGrouping` (`:690-734`)
делегирует разбор ключа приватному `BuildKeyColumns` (`:646-688`), который ранее поддерживал
`NewExpression` и одиночное выражение для `DISTINCT ON`/`LIMIT BY`. Публичная поверхность:
`EntityBuilder<TEntity>.GroupBy<TResult>` (`EntityBuilder.cs:1145`) уже была generic — новых типов,
членов и подписей **не добавлено, не удалено и не переименовано**; `BuildKeyColumns`/`PrepareGrouping`
— `private static`. BCL-конфликтов (`CA1716`/`CA1724`) и новых имён нет → **P0/P1/P2 по публичному API
нет**.

XML-доки изменённых членов: оба приватные, документировать нечего; публичные члены не менялись.
Приложение A (45) без изменений. Шаг 5 (**заморозка**) открыт без изменений: `PublicAPI.Shipped/`
`Unshipped.txt` нет, `PublicApiAnalyzers`/ApiCompat/API-approval-теста нет, `CS1591` — в `<NoWarn>` всех
7 библиотечных `.csproj`. Новых подписей для `PublicAPI.Unshipped.txt` этот фикс не создаёт.

Документация (AGENTS.md: `docs/**` и `docs/ru/**` обновляются в одном изменении). Поведение стало
поддерживаться, но руководство этого не описывает:

| Файл:строка | Сейчас | Должно быть |
|---|---|---|
| `docs/guide/04-grouping-and-aggregates.md:44`, `docs/ru/guide/04-grouping-and-aggregates.md:46` | Раздел `## GroupBy` показывает только `new { e.Int }`; скалярная форма `GroupBy(e => e.Int)` не упомянута (рабочий план `todo_scalar_group_by.md` требует оговорки) | Добавить, что для одной колонки анонимный тип не обязателен, с примером скалярной формы (EN+RU) |
| `docs/guide/provider-specific/clickhouse.md:58-62`, `docs/ru/guide/provider-specific/clickhouse.md:59-63` | Пример `WithTotals` использует `.GroupBy(x => x.Int)` — **пре-существует** и раньше бросал `InvalidOperationException`; при этом проекция `.Select(x => new { x.Key, … })` ссылается на несуществующий член `x.Key` (`IComplexEntity` его не имеет; SQL/остальные примеры используют `x.Int`) | Заменить `x.Key` → `x.Int`; после этого пример становится валидным благодаря данному фиксу |

**P2 (docs)** — ✅ оба пункта закрыты (правки только в документации): скалярная форма описана в
разделе `## GroupBy` файлов `docs/guide/04-grouping-and-aggregates.md` (+RU), опечатка `x.Key` →
`x.Int` исправлена в `docs/guide/provider-specific/clickhouse.md` (+RU). `x.Key` — пре-существующая
опечатка, не внесённая этим фиксом.

### Аудит 21.09.2026 — квотирование идентификаторов (P0/P1 по именам — нет; P2 — 4)

**Область (uncommitted):** `ISqlDialect.QuoteIdentifier` (DIM) + `SqlDialectBase`/`SqliteDialect`
overrides; флаг `bool QuoteIdentifiers` в `VisitorOptions`/`SqlBuildContext`;
`BaseExpressionVisitor.AppendIdentifier`; точки вывода `MemberTranslator`/`SqlSourceRenderer`;
`DataContextBuilder`/`IContextEnvironment`/`DataContext`; `EntityBuilder<TEntity>`/`EntityBuilder`/
`QueryCommand`/`QueryCommand<TResult>`; план-ключ `QueryPlanEqualityComparer` (+`ResolvedQuoteIdentifiers`,
`CopyTo`). Build Release **0/0**; новые SQL-gen тесты — sqlite **9/9**, sqlserver **2/2**.

**P0 — нет.** `QuoteIdentifier`/`QuoteIdentifiers`/`UseQuotedIdentifiers`/`WithQuotedIdentifiers` не
конфликтуют с BCL (`CA1716`/`CA1724`) и не вводят в заблуждение.

**P1 — нет.** Ближайший риск — неразличение `Escape`/`QuoteIdentifier` (ниже) — косметика уровня P2.

**P2-1 — `Escape` vs `QuoteIdentifier` (`ISqlDialect.cs:568,576`).** `Escape` задокументирован как
«Quotes an identifier (alias, keyword)», но фактически это **alias/keyword**-делимитер (`'name'` в
`SqlDialectBase`/SQLite, `[name]`/`` `name` ``/`"name"` у остальных), а `QuoteIdentifier` — физический
идентификатор. `SqlDialectBase.QuoteIdentifier => Escape(name)` делает их синонимами на 5 из 6
провайдеров, поэтому назначение не читается из имени. **Рекомендация (alpha, переименование на месте):**
уточнить XML-док `Escape` («delimits an alias/keyword») либо переименовать в `QuoteAlias`/`EscapeAlias`
(затронет 6 провайдеров + тесты). Не блокер.

**P2-2 — `VisitorOptions` — 13-й позиционный параметр (`VisitorOptions.cs:29`).** Trailing
`bool QuoteIdentifiers = false` **источник-совместим** (прежние позиционные вызовы компилируются) и
**`with`-friendly** (record генерирует `init`-свойство), но **бинарно несовместим** (сигнатура
primary-ctor изменилась) и растит и без того длинный список (13 > порог 5). Политика alpha бинарную
совместимость не гарантирует, поэтому смена **допустима, менять не обязательно**. Строго лучше —
объявить свойство в теле record (`public bool QuoteIdentifiers { get; init; }`), не трогая primary-ctor:
`with { QuoteIdentifiers = true }` и object-initializer работают, а сигнатура ctor не меняется.
Публичный `VisitorOptions` — деталь построения плана, внешних call-site в репо нет (4 конструирования
в `SqlBuildContext`/`SqlSourceRenderer`).

**P2-3 — документация EN+RU (`docs/**`, `docs/ru/**`).** Новый флаг не описан в пользовательской
документации: в `docs/**`/`docs/ru/**` нет ни `UseQuotedIdentifiers`, ни `WithQuotedIdentifiers` —
единственные вхождения в рабочем дереве — RFC `docs/specs/roadmap/todo_identifier_quoting.md` (вне
контента сайта). Правки `docs/advanced/limitations.md`/`docs/guide/04-grouping-and-aggregates.md`
(+RU) в этом же uncommitted-дереве относятся к другим фичам (хинты/агрегаты). RFC
(`todo_identifier_quoting.md:79`) сам требует «Доки EN+RU», AGENTS.md — синхронные обе ветки.
**Рекомендация:** раздел о квотировании в `docs/guide/**` + `docs/ru/guide/**`, а по итогам
`code-smells-review.md` Находок 52–53 — оговорка о schema-qualified и экранировании разделителя.

**P2-4 — Шаг 5 (трекинг).** `PublicAPI.Shipped/Unshipped.txt` по-прежнему нет; новые подписи (перечень
в §4, Шаг 5) должны попасть в `PublicAPI.Unshipped.txt` при заморозке. DIM-члены
(`ISqlDialect.QuoteIdentifier`, `IContextEnvironment.QuoteIdentifiers`) аддитивны и
источник-совместимы для внешних реализаций — риск ниже, чем у обычных новых членов интерфейса.

Приложение A (45) без изменений. Содержательные находки (schema-qualified, экранирование, план-ключ
вложенных команд) — в `code-smells-review.md` (Находки 52–53 + наблюдения A–E).

### Аудит 21.09.2026 — соглашения об именовании (P0 — нет; P1 — нет; P2 — 5)

**Область (uncommitted):** `INamingConvention`/`SnakeCaseNamingConvention`;
`UseNamingConvention`/`WithNamingConvention` на `DataContextBuilder`/`EntityBuilder<TEntity>`/
`EntityBuilder`/`QueryCommand<TResult>`; `NamingConvention` на `IContextEnvironment` (DIM) /
`ContextEnvironment` / `DataContext` / `QueryCommand`; `IsTableNameAuto`/`IsColumnNameAuto` в публичных
метаданных; `VisitorOptions.NamingConvention` (init-свойство, не primary-ctor); план-ключ
`QueryPlanEqualityComparer` (+`ResolvedNamingConvention`). Build Release **0/0**.

**P0 — нет.** `INamingConvention`/`SnakeCaseNamingConvention`/`UseNamingConvention`/`WithNamingConvention`/
`IsTableNameAuto`/`IsColumnNameAuto` не конфликтуют с BCL (`CA1716`/`CA1724`) и не вводят в заблуждение;
`I`-префикс, `Is*`-булев префикс, builder-методы возвращают свой тип — конвенции соблюдены.

**P1 — нет.** Все имена — PascalCase, без аббревиатур; синхронных близнецов нет, суффикс `Async` не нужен.

**P2-1 — `IEntityMetadata.IsTableNameAuto` / `IPropertyMetadata.IsColumnNameAuto` — абстрактные члены
публичных интерфейсов (`IEntityMetadata.cs:20`, `IPropertyMetadata.cs:22`).** В отличие от
`IContextEnvironment.NamingConvention` (DIM `=> null`, `Roles/IContextEnvironment.cs:28`), здесь новые
члены **без реализации по умолчанию** → source- и binary-breaking для внешних реализаторов. В репозитории
интерфейсы реализуют только `internal` `EntityMetadata`/`PropertyMetadata`, фактического разрыва нет;
политика alpha бинарную совместимость не гарантирует. **Рекомендация:** для консистентности с
`IContextEnvironment` объявить DIM (например, `bool IsTableNameAuto => true;` / `IsColumnNameAuto => true;`)
либо внести в список Шага 5 как осознанный разрыв.

**P2-2 — изменены подписи публичного расширения и публичного ctor (`MemberInfoExtensions.cs:18`,
`FromExpression.cs:6`).** `GetPropertyColumnName(this MemberInfo)` → `(this MemberInfo, INamingConvention? = null)`;
`FromExpression(string)` → `(string, bool = false, bool = false)`. Добавление optional-параметра
source-совместимо (прежние вызовы компилируются), но **бинарно несовместимо** (изменилась сигнатура).
`VisitorOptions` тот же риск обошёл init-свойством (правильно, ср. P2-2 аудита квотирования).
**Рекомендация:** внести новые подписи в `PublicAPI.Unshipped.txt` при заморозке (Шаг 5).

**P2-3 — curated API-индекс не обновлён (`docs/advanced/api-reference.md` +
`docs/ru/advanced/api-reference.md`).** `INamingConvention`/`SnakeCaseNamingConvention` отсутствуют в обоих
(и файлы не менялись). Индекс позиционируется как «curated index of nextorm's public types», а
`INamingConvention` — новый публичный extension point, который потребитель реализует и передаёт в
`UseNamingConvention`. **Рекомендация:** добавить строку (например, в «Context and roles» или новую
«Naming conventions») для `INamingConvention`/`SnakeCaseNamingConvention`; заодно завести/проверить
`IEntityMetadata`/`IPropertyMetadata` (их в индексе тоже нет).

**P2-4 — self-`<see cref>` в доке `EntityBuilder<TEntity>.WithNamingConvention`
(`Builders/EntityBuilder.cs:1261`).** `<summary>` generic-метода ссылается на самого себя
(`EntityBuilder{TEntity}.WithNamingConvention`); негенерик-версия (`:1422`) корректно ссылается на generic.
Косметика XML-доков, покрытие не страдает. **Рекомендация:** заменить на описание сути (как у
`QueryCommand<TResult>.WithNamingConvention`).

**P2-5 — Шаг 5 (трекинг).** `PublicAPI.Shipped/Unshipped.txt` по-прежнему нет; заморозка — трекинг #53.
Новые подписи к внесению: `INamingConvention` (тип) + `TableName(string, bool) -> string` +
`ColumnName(string) -> string`; `SnakeCaseNamingConvention` (тип, `sealed`) +
`Instance.get -> SnakeCaseNamingConvention` + два метода; `DataContextBuilder.NamingConvention.get ->
INamingConvention?` + `UseNamingConvention(INamingConvention?) -> DataContextBuilder`;
`IContextEnvironment.NamingConvention.get -> INamingConvention?` (DIM); `DataContext.NamingConvention.get ->
INamingConvention?`; `IEntityMetadata.IsTableNameAuto.get -> bool`; `IPropertyMetadata.IsColumnNameAuto.get ->
bool`; `EntityBuilder<TEntity>.WithNamingConvention(INamingConvention?) -> EntityBuilder<TEntity>`;
`EntityBuilder.WithNamingConvention(INamingConvention?) -> EntityBuilder`; `QueryCommand.NamingConvention.get ->
INamingConvention?`; `QueryCommand<TResult>.WithNamingConvention(INamingConvention?) -> QueryCommand<TResult>`;
`VisitorOptions.NamingConvention.get -> INamingConvention?`; изменённые
`MemberInfoExtensions.GetPropertyColumnName(MemberInfo, INamingConvention?) -> string` и
`FromExpression.FromExpression(string, bool, bool) -> void`.

Приложение A (45) без изменений (новые типы документированы). Содержательные находки по кэшу — в
`code-smells-review.md` (Находки 54–55 и наблюдения A–E).

### Аудит 22.09.2026 — слияние `todo_fulltext_ranking` + `todo_builtin_tvf_expansion` (P0 — нет; P1 — нет; P2 — 2)

**Область (uncommitted, worktree `tvf-expansion`):** `Query/SqlFunctions.cs` (`IKeyRankRow<TKey>`),
`Query/SqlFunctions.SqlServer.cs` (`containstable<TKey>`/`freetexttable<TKey>`),
`Query/SqlFunctions.Postgres.cs` (`ts_rank_cd`), `Visitors/ExtendedScalarFunctionTranslator.cs`
(`ts_rank_cd` в `TextSearchFunctions`), `SqlTableFunctionAttribute.cs` (`CallClause`/`VerbatimArguments`),
`Expressions/TableFunctionExpression.cs` (`CallClause`/`VerbatimArguments` + перегрузка ctor),
`DataContext/SqlSourceRenderer.cs` (`MakeTableFunction`/`IsVerbatimArgument`/`GetVerbatimArgument`),
`src/nextorm.sqlserver/SqlServerDialect.cs` (`SupportsTableFunction`); тесты — sqlserver/postgres/mysql.
Build Release — **0/0**; тесты (Release, `--no-build`) core **192/192**, sqlserver **240/240**,
postgres **249/249**, mysql **66/66** (0 failed, 0 skipped).

**P0 — нет.** `SqlFunctions.IKeyRankRow<TKey>`, `containstable<TKey>`, `freetexttable<TKey>`, `ts_rank_cd`,
`CallClause`, `VerbatimArguments` не конфликтуют с BCL (`CA1716`/`CA1724`) и не вводят в заблуждение:
`I…Row` — принятая схема row-интерфейсов (`IOpenJsonRow`, `IUnnestRow<T>`); `containstable`/`freetexttable`/
`ts_rank_cd` — SQL-зеркала (DSL-исключение, как `openjson`/`string_split`/`ts_rank`/`generate_series`);
`CallClause`/`VerbatimArguments` — PascalCase-существительные.

**P1 — нет.** Новых типов с аббревиатурами/суффиксными отклонениями и синхронных близнецов-методов нет;
суффикс `Async` не требуется.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| TFV1 | P2 | `SqlTableFunctionAttribute.cs:47-55`; `Expressions/TableFunctionExpression.cs:48-52` | XML-док `CallClause` говорит лишь «emitted verbatim», тогда как родственный `VerbatimArguments` (`SqlTableFunctionAttribute.cs:57-63`) прямо предупреждает «only pass trusted values». Обе настройки эмитят сырой SQL, предупреждение должно быть у обеих | Дописать в `<summary>` `CallClause` (атрибут и свойство выражения): «only developer-authored SQL; never user input» |
| TFV2 | P2 | `Expressions/TableFunctionExpression.cs:26` | Новый **публичный** 6-параметрический ctor (`string, string?, string?, string?, IReadOnlyList<int>?, MethodCallExpression`) превышает порог «>5»; call-site в решении — только фабрика `Create` (`:82`) и два делегирующих ctor (`:15,21`), внешних нет | Сделать широчайший ctor `internal` (фабрика `Create` — единственный потребитель) либо ввести параметр-объект; 3-/4-арг. публичные ctor'ы не трогать |

**✅ Исправлено (22.09.2026):** TFV1 — в `<summary>` `CallClause` добавлено «This is developer-authored SQL only — never build it from user input»; TFV2 — 6-параметрический ctor `TableFunctionExpression` понижен до `internal` (публичны только прежние 3-/4-арг. ctor'ы и фабрика `Create`).

**ℹ️ Наблюдения (фикс не требуется):**
- **`GetVerbatimArgument` — ограничение «только константная строка».** `SqlSourceRenderer.cs:386-389`
  принимает исключительно `ConstantExpression { Value: string }`, иначе бросает `NotSupportedException`
  с понятным текстом. Документировано на `SqlTableFunctionAttribute.VerbatimArguments` («must be a constant
  string»), но не на встроенных `containstable`/`freetexttable` (там только «only pass trusted values»);
  рекомендация — дописать «literal» и в доки методов (в связке с TFV1).
- **Param-mode согласован.** В первом проходе (`SqlSourceRenderer.cs:301-308`) verbatim-аргументы
  пропускаются, во втором (`:318-332`) — выводятся сырым текстом; порядок параметров совпадает. Тесты
  подтверждают: `containstable`/`freetexttable` → params `["search"]`, `JSON_TABLE` → `["doc"]`.
- **SQL-инъекция — принятый escape-hatch, не новый разрыв.** `VerbatimArguments` обходит параметризацию
  осознанно и задокументирован как trusted; у встроенных `containstable`/`freetexttable` жёстко зашиты
  индексы `{0, 1}` (table/column), а пользовательский ввод обязан идти в `search` (индекс 2, параметризуется
  штатно). `CallClause` задаётся только разработчиком через атрибут. Это то же DSL-исключение, что уже
  принято у `[SqlFunction]`/`WithClause`; единственный зазор — док-предупреждение (TFV1).
- **Индексы `VerbatimArguments` не валидируются.** Отрицательный/выходящий за `Arguments.Count`/дублирующий
  индекс молча игнорируется (совпадения не будет). Риск низкий (метаданные автора кода); при желании —
  валидация в `TableFunctionExpression.Create`.
- **XML-доки.** `<summary>` есть у `IKeyRankRow<TKey>`, `containstable<TKey>`, `freetexttable<TKey>`,
  `ts_rank_cd`, обоих `CallClause`/`VerbatimArguments` и нового ctor. `IKeyRankRow<TKey>.Key`/`.Rank` — без
  индивидуальных `<summary>`, как у всех соседних row-интерфейсов (`IOpenJsonRow`, `IUnnestRow<T>`);
  `<typeparam name="TKey">` отсутствует так же, как у `IUnnestRow<T>`. Приложение A (45) без изменений
  (новый тип документирован); `CS1591` по-прежнему в `<NoWarn>` 7 библиотечных `.csproj`.
- **Квотирование `[key]`/`[rank]` консистентно.** `IKeyRankRow<TKey>` повторяет приём `IOpenJsonRow`
  (`[Column("[key]")]` для зарезервированного `KEY`); `[rank]` заквотирован тем же стилем. Гейт
  `SupportsTableFunction("containstable"/"freetexttable")` (`SqlServerDialect.cs:76-77`) не даёт другим
  провайдерам отрендерить SQL Server-специфичное имя (тест `BuiltInTableFunction_Containstable_ShouldThrowOnPostgres`).
- **Extend-only соблюдён.** Старые ctor'ы `TableFunctionExpression` (3-/4-арг.) сохранены и делегируют новому;
  `SqlTableFunctionAttribute` получил только новые свойства; удалённых/изменённых публичных членов нет.
  Изменение `SupportsTableFunction` — поведенческое аддитивное, не сигнатурное.
- **Шаг 5 (трекинг).** `PublicAPI.Shipped/Unshipped.txt` по-прежнему нет, `PublicApiAnalyzers` не подключён.
  При заморозке внести: `SqlFunctions.IKeyRankRow<TKey>` (+`Key.get`/`Key.set`/`Rank.get`/`Rank.set`),
  `SqlServerFunctions.containstable<TKey>(string,string,string)`,
  `SqlServerFunctions.freetexttable<TKey>(string,string,string)`,
  `PostgresFunctions.ts_rank_cd(string?,string?)`, `SqlTableFunctionAttribute.CallClause.get/set`,
  `SqlTableFunctionAttribute.VerbatimArguments.get/set`, `TableFunctionExpression.CallClause.get`,
  `TableFunctionExpression.VerbatimArguments.get`, ctor
  `(string,string?,string?,string?,IReadOnlyList<int>?,MethodCallExpression)`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.{core,sqlserver,postgres,mysql}.tests -c Release --no-build` — core **192/192**,
sqlserver **240/240**, postgres **249/249**, mysql **66/66** (0 failed, 0 skipped); `find -name 'PublicAPI*.txt'`
— пусто; в диффе `src`+`tests` новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**. EN+RU
`docs/guide/13-table-valued-functions.md`, `docs/guide/provider-specific/{postgresql,sqlserver}.md`,
`docs/providers/{overview,postgres,sqlserver}.md`, `docs/advanced/{api-reference,limitations}.md` синхронны.

### Аудит 22.09.2026 — композируемый сырой SQL: новые публичные `FromSql` и `SupportsRawSqlSource` (P0 — нет; P1 — нет; P2 — 4)

**Область (uncommitted, worktree `composable-raw-sql`):** новый публичный
`DataContextExtensions.FromSql(this IDataContext, string, object? = null) -> EntityBuilder<TableAlias>`
(`DataContextExtensions.cs:101`); новый публичный член `ISqlDialect.SupportsRawSqlSource`
(`ISqlDialect.cs:785`, default `false` в `SqlDialectBase.cs:500`, `true` в 6 SQL-диалектах); `internal`
`RawSqlSourceExpression` + `FromExpression.RawSqlSource`; рендер `SqlSourceRenderer.MakeRawSqlSource`.
Build Release — **0/0**; тесты — core 192, postgres 249, sqlserver 240, mysql 67, mariadb 29, sqlite 270,
clickhouse 185 = **1232/1232, 0 failed, 0 skipped**. Новых публичных **типов** нет.

**P0 — нет.** `FromSql` совпадает с именем EF Core (`RelationalQueryableExtensions.FromSql`) в другом
namespace/типе и с семейством `From*` nextorm (`From` / `FromTableFunction`); `SupportsRawSqlSource` —
существующая `Supports*`-конвенция capability-гейта. BCL-конфликтов (`CA1716`/`CA1724`) и вводящих в
заблуждение имён нет.

**P1 — нет.** Оба имени — PascalCase, без аббревиатур; синхронного близнеца у `FromSql` нет → суффикс
`Async` не нужен; `Supports*` — булев префикс.

**P2-1 — `ISqlDialect.SupportsRawSqlSource` объявлен абстрактно, без DIM (`ISqlDialect.cs:785`).**
Член без реализации по умолчанию → source- и binary-breaking для внешних реализаторов `ISqlDialect`
(в репозитории интерфейс реализуют только `SqlDialectBase`-производные, фактического разрыва нет;
политика alpha это допускает). При этом XML-док говорит «The safe default is `false`; a provider that
leaves it `false` rejects such a source…», что подразумевает именно DIM — док и сигнатура расходятся.
**Рекомендация:** для консистентности с `IContextEnvironment.NamingConvention`/`QuoteIdentifier`
объявить `bool SupportsRawSqlSource => false;` в интерфейсе, либо внести в список Шага 5 как осознанный
разрыв. Ср. P2-1 аудита «соглашения об именовании» (`IEntityMetadata.IsTableNameAuto`).

**P2-2 — in-memory поведение не описано.** `FromSql` на `InMemoryDataContext` не отклоняется явно:
композиция даёт фантомную строку либо `NullReferenceException` (см. `code-smells-review.md`,
Находка 56). `docs/guide/14-raw-sql.md` (+RU) говорит «a provider opts in through
`SupportsRawSqlSource`; every SQL provider does», но не фиксирует, что in-memory (не `ISqlDialect`)
фичу не поддерживает, а `docs/advanced/limitations.md` (+RU) удалил единственную строку про raw SQL
как источник. **Рекомендация:** вернуть оговорку в limitations и/или добавить явный отказ (Находка 56).

**P2-3 — XML-доки `FromSql` не упоминают отказ для диалекта без поддержки.** Документированы
назначение, конвенция параметров и `TableAlias`-аксессоры (`DataContextExtensions.cs:91-100`), но нет
`<exception cref="NotSupportedException">` для случая `SupportsRawSqlSource == false` (бросается позже,
при подготовке команды, `SqlSourceRenderer.cs:294`). Дополнительно `WithSql` в доке — `<c>`, не
`<see cref>`. Не блокер (ср. AJ9).

**P2-4 — Шаг 5 (трекинг).** `PublicAPI.Shipped/Unshipped.txt` по-прежнему нет; при заморозке внести
`NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext, string, object?) -> EntityBuilder<TableAlias>`
и `NextORM.Core.ISqlDialect.SupportsRawSqlSource.get -> bool`. Приложение A (45) без изменений (новых
публичных типов нет, оба новых члена документированы). Трекинг — `todo_public_api_freeze.md`, issue #53.

Capability-объекты (Фаза 3) не применимы: `SupportsRawSqlSource` не дублирует объект-рендерер — рендер
`(<sql>) AS alias` лежит в ядре, dialect-specific остаётся только существующий `RequireSubqueryAlias`.
Соотношение подавлений проекта не изменилось (11/11 оправданных, 0 неоправданных); новых
`Skip=`/`#pragma`/`SuppressMessage`/`NoWarn`/`Task.Delay`/пустых `catch` нет.

**✅ Исправлено (22.09.2026):** P2-2/P2-3 закрыты — in-memory оговорён в `docs/guide/14-raw-sql.md` EN+RU (и в тесте `InMemoryTests.FromSql_ShouldThrowClearNotSupported`), у `FromSql` добавлен `<exception cref="NotSupportedException">`. P2-1 принят как проектная конвенция: флаги `ISqlDialect` объявлены абстрактно, а safe-default живёт в `SqlDialectBase` (так же, как `SupportsTableHints`), поэтому расхождения с докой нет. P2-4 остаётся трекингом заморозки (`todo_public_api_freeze.md`).

### Аудит 22.09.2026 — SQL Server `xml.nodes()` rowset как источник `CROSS/OUTER APPLY` (P0 — нет; P1 — нет; P2 — 3)

**Область (uncommitted).** Новые публичные члены:
`NextORM.Core.SqlFunctions.IXmlNodesRow` (`Query/SqlFunctions.cs:175-179`; свойство `Value` c `[Column("value")]`)
и `NextORM.Core.SqlServerFunctions.xml_nodes(string? xml, string? xpath) -> QueryCommand<SqlFunctions.IXmlNodesRow>`
(`Query/SqlFunctions.SqlServer.cs:67-79`). Изменён `internal SqlServerXmlFunctions.Supports` (`src/nextorm.sqlserver/SqlServerDialect.cs:453`:
`"nodes"` → `true`) — внешней поверхности не даёт. Build Release — **0/0**; `nextorm.sqlserver.tests`
**245/245**, `nextorm.core.tests` **194/194** (0 failed/0 skipped); `rg --files -g 'PublicAPI*.txt'` — пусто
(Шаг 5 открыт). Новый публичный **тип** один — `IXmlNodesRow` — и он документирован вместе с новым методом,
поэтому Приложение A (45 недокументированных) не меняется.

**P0 — нет.** `xml_nodes` — snake_case-зеркало SQL-токена, как `xml_value`/`xml_query`/`xml_exist`; дублирующие
имена/BCL-конфликты (`CA1716`/`CA1724`) отсутствуют. `IXmlNodesRow` следует семейству
`IStringSplitRow`/`IOpenJsonRow`/`INumbersRow` (`I`-префикс, `Row`-суффикс, `[Column]` на `Value`). Прямой вызов
`xml_nodes` бросает `NotSupportedException` — ровно как `string_split`/`openjson` (`SqlFunctions.SqlServer.cs:89,102`).

**P1 — нет.** Все имена PascalCase/`snake_case`-DSL; параметры `xml`/`xpath` и их порядок совпадают с
соседними XML-методами; синхронного близнеца нет → суффикс `Async` не нужен.

**P2-1 — два разных идиома для «SQL-функция, дающая rowset».** `string_split`/`openjson`/
`generate_series`/`unnest`/`numbers` — `IQueryable<Row>` + `[SqlTableFunction]`, подключаются через
`FromTableFunction`; `xml_nodes` — `QueryCommand<Row>`, валиден **только** как
`CrossApply`/`OuterApply`-источник. Расхождение осознанное и обосновано (`CROSS APPLY` c корреляцией
невыразим через `FromTableFunction`; см. XML-док метода и `todo_xml_nodes.md`), поэтому P2, а не P1.
**Рекомендация:** перенести это обоснование в `<remarks>` метода/`docs/guide/provider-specific/sqlserver.md`,
чтобы две идиомы читались как намеренные.

**P2-2 — классовый `<summary>` `SqlServerFunctions` не упоминает `xml_nodes`.** `Query/SqlFunctions.SqlServer.cs:6-12`
перечисляет «postfix XML data-type methods (`xml_value`/`xml_query`/`xml_exist`)», хотя теперь их четыре.
Сам `xml_nodes` задокументирован полностью (`<summary>` + `<see cref>` на `CrossApply`/`IXmlNodesRow.Value`/
`xml_value`/`xml_query`/`xml_exist`); все cref'ы резолвятся — build **0/0** при `CS1591` в `<NoWarn>`
(`CS1574` не подавлен, значит ссылки валидны). **Рекомендация:** добавить `nodes` в классовый `<summary>`
(и при желании — в `<summary>` свойства `SqlFunctions.SqlServer`).

**P2-3 — пользовательские доки EN+RU не обновлены.** `docs/guide/11-scalar-functions.md`,
`docs/guide/provider-specific/sqlserver.md`, `docs/providers/sqlserver.md`, `docs/advanced/api-reference.md`,
`docs/advanced/limitations.md` и их `docs/ru/**`-зеркала описывают три XML-скаляра, но `xml_nodes`/
`IXmlNodesRow` в них нет (`rg` по `docs/**` вне `specs/` — 0 совпадений). `AGENTS.md` требует синхронного
обновления `docs/**` и `docs/ru/**`, а рабочий план фичи (`todo_xml_nodes.md` §«Документация») прямо
перечисляет эти файлы и снятие пункта из `sql-capabilities-gap-analysis.md` §4. **Рекомендация:** отдать
`nextorm-design-engineer` (EN+RU в одном изменении).

**P2-4 (трекинг Шага 5).** При заморозке внести в `PublicAPI.Unshipped.txt` (файла нет):
`NextORM.Core.SqlFunctions.IXmlNodesRow`, `.Value.get -> string?`, `.Value.set -> void`,
`NextORM.Core.SqlServerFunctions.xml_nodes(string?, string?) -> NextORM.Core.QueryCommand<NextORM.Core.SqlFunctions.IXmlNodesRow>`,
а также `SqlServerDialect.Supports("nodes")` не является публичной сигнатурой (класс `internal`) — не вносить.
Трекинг — `todo_public_api_freeze.md`, issue #53.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.sqlserver.tests -c Debug` — **245/245**; `dotnet test tests/nextorm.core.tests
-c Debug` — **194/194**; `rg --files -g 'PublicAPI*.txt'` — пусто; `grep -r xml_nodes docs` вне `specs/` —
пусто (подтверждает P2-3); подавления проекта 11/11 (0 неоправданных).

### Аудит 22.09.2026 — ClickHouse `UInt64` row reader (`DbDataReader.GetFieldValue<ulong>`) (публичный API без изменений; P0 — нет; P1 — нет; P2 — нет)

**Область (uncommitted worktree `clickhouse-uint64-row-reader`).** `src/nextorm.core/Expressions/SelectExpression.cs:60,113-116`
(новое `private readonly static MethodInfo GetFieldValueMI` + ветка `ulong` в **уже существующем публичном**
`GetDataRecordMethod()`; сигнатура метода не менялась); `src/nextorm.core/DataContext/RowMapperFactory.cs:26-30`
(`RowMapperFactory` — `internal`); `src/nextorm.clickhouse/ClickHouseDialect.cs:57-59,163-166,251-256,562-566`
(только комментарии); тесты `SelectExpressionTests.cs:18-34`, `ClickHouseDialectTests.cs:143-148`,
`SqlGenerationTests.cs:1793-1801`, `ClickHouseIntegrationTests.cs:789-836` + `ClickHouseTestProvider.cs:137-152`;
доки EN+RU.

**Публичной поверхности не добавлено — подтверждено.** `git diff -- src/` не содержит добавленных `public`/
`protected`/`internal` объявлений: единственный новый член с модификатором доступа — `private readonly static
MethodInfo GetFieldValueMI`. `RowMapperFactory.MapColumn` — `public`-член `internal`-типа (внешней поверхности не
даёт), его контракт не менялся; `SelectExpression.GetDataRecordMethod()` добавлен только `else if`-веткой.
`IUInt64Entity` (`ClickHouseIntegrationTests.cs:833`) объявлен в **тестовой** сборке — в публичную поверхность 7
библиотек не входит. Проверено чтением диффа и `roslyn refs` по явному `solution=nextorm.sln`:
`RowMapperFactory.MapColumn` — 1 ссылка, `RowMapperFactory.GetOrBuild` — 1, `SelectExpression.GetDataRecordMethod`
— 5 (1 прод-вызов + 4 тестовых).

**XML-док покрытие не изменилось.** Новых публичных типов/членов нет; `GetDataRecordMethod()` остаётся без
XML-дока, как и до изменения (`CS1591` в `<NoWarn>` всех 7 библиотечных `.csproj`). Приложение A
(**45** недокументированных публичных типов) не меняется.

**Шаг 5 (заморозка) — статус без изменений.** `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` отсутствуют;
`Microsoft.CodeAnalysis.PublicApiAnalyzers`, `EnablePackageValidation`/ApiCompat и API-approval тест не подключены.
В будущий `PublicAPI.Unshipped.txt` из этого изменения вносить **нечего** (изменённые члены — `private`/`internal`).
Трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53.

**Доки.** `docs/advanced/limitations.md`, `docs/providers/clickhouse.md`, `docs/guide/provider-specific/clickhouse.md`
и их `docs/ru/**`-зеркала обновлены синхронно; `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.4 обновлён,
`todo_clickhouse_uint64_row_reader.md` удалён. Публичного переименования нет, но правило `AGENTS.md` о синхронности
EN/RU соблюдено. Непроверенное утверждение доков про MySQL/MariaDB `BIGINT UNSIGNED` (нет интеграционного теста на
этот диалект) вынесено как наблюдение 3 в `code-smells-review.md` — на именование публичного API не влияет.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release --no-incremental` — **0 warnings / 0 errors**;
`nextorm.core.tests` — **196/196**, `nextorm.clickhouse.tests` — **188/188** (0 failed/0 skipped);
`find -name 'PublicAPI*.txt'` — пусто; `grep "public"` по добавленным строкам `src/`-диффа — только `private`-поле;
подавления проекта 11/11 (0 неоправданных).
### Аудит 22.09.2026 — серверные/кластерные табличные функции ClickHouse (P0 — нет; P1 — нет; P2 — 2)

Область: `ClickHouseFunctions.url<T>`/`s3<T>`/`file<T>`/`remote<T>`/`remote_secure<T>`/`cluster<T>`/
`cluster_all_replicas<T>` (`Query/SqlFunctions.ClickHouse.cs`), `ClickHouseDialect.SupportsTableFunction`
(`src/nextorm.clickhouse/ClickHouseDialect.cs`), док класса `ClickHouseFunctions`
(`Query/SqlFunctions.ClickHouse.cs:5-17`) и свойства `SqlFunctions.ClickHouse` (`Query/SqlFunctions.cs:41-58`);
тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs` (7 SQL-gen + `IServerTableRow`),
`ClickHouseDialectTests.cs` (`SupportsTableFunction` ×16, `:168-185`), `tests/nextorm.postgres.tests/SqlGenerationTests.cs`
(`BuiltInTableFunction_ClickHouseServerTableFunctions_ShouldThrowOnPostgres`). Build Release — **0/0**;
`dotnet test tests/nextorm.clickhouse.tests -c Debug` — **193/193**; `dotnet test tests/nextorm.postgres.tests
-c Debug --filter FullyQualifiedName~BuiltInTableFunction` — **6/6**. XML-`<summary>` есть у всех 7 новых
публичных методов; **новых публичных типов нет** (схема строки объявляется generic-параметром `TRow`
вызывающего) → Приложение A (45) без изменений; Шаг 5 по-прежнему открыт.

| # | Ур. | Файл:строка | Проблема | Рекомендация |
|---|-----|-------------|----------|--------------|
| SCTF1 | P2 | `Query/SqlFunctions.ClickHouse.cs` (`url`…`cluster_all_replicas`) | Новые публичные члены не отслеживаются: `PublicAPI.Shipped/Unshipped.txt` отсутствуют, `PublicApiAnalyzers` не подключён. Шаг 5 открыт | Внести 7 методов `ClickHouseFunctions.*<TRow>` в `PublicAPI.Unshipped.txt` при заморозке (ср. TF1/Z1) |
| SCTF2 | P2 | `Query/SqlFunctions.ClickHouse.cs`, `docs/guide/13-table-valued-functions.md` (+RU), `docs/providers/clickhouse.md` (+RU) | Отложенные `format`/`merge`/`input` задокументированы в limitation-таблице и guide, но не являются элементом кода; при изменении решения вернуться к `SupportsTableFunction` | Оставить как осознанный пропуск до `todo_dynamic_result_schema.md` |

ℹ️ **Наблюдения (фикс не требуется):**
- **Именование — конвенции соблюдены, P0/P1 нет.** SQL-имена `remoteSecure`/`clusterAllReplicas` против
  CLR `remote_secure`/`cluster_all_replicas` точно повторяют пару `generate_random`/`generateRandom`;
  алиасы `url`/`s3`/`file`/`remote`/`cluster` — SQL-зеркало (реестр §3).
- **Generic `TRow` вместо именованного row-интерфейса — обосновано.** У `url`/`s3`/`file` схема задаётся
  строкой `structure`, у `remote`/`remoteSecure`/`cluster`/`clusterAllReplicas` — целевой таблицей,
  поэтому единого `I…Row` нет; `IUnnestRow<T>` уже показывает, что generic row-интерфейс — принятый
  приём. `format`/`merge`/`input` оставлены на динамическую схему (`todo_dynamic_result_schema.md`).
- **Секреты не проходят аргументами.** `remote`/`remoteSecure`/`s3` не принимают `user`/`password`/ключи;
  аутентификация — серверная (`<remote_servers>`/named collections), поэтому секреты не попадают в
  план/лог **по умолчанию**; значения параметров логируются лишь при opt-in `LoggingOptions.LogSensitiveData`
  (`QueryExecutor.cs:43-49`, `ResultSetEnumerator.cs:193-225`), а URL-аргумент биндится параметром и в
  план-ключ не входит (значения closure в ключ не входят). Caller-declared `url` может содержать
  `user:pass@`/presigned-параметры, поэтому доки/guide рекомендуют named collections. Формулировка
  уточнена 22.09.2026.
- **`<typeparam name="TRow">` отсутствует** у 7 методов при наличии `<typeparamref name="TRow"/>` (как у
  `IUnnestRow<T>`); на `CS1591` (в `<NoWarn>` 7 библиотечных `.csproj`) не влияет — для полноты DocFX
  тег можно добавить. ℹ️.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Debug` — **193/193**; `grep -n 'format\|merge\|input'
docs/advanced/limitations.md` — строки отложенных функций присутствуют; `rg --files -g 'PublicAPI*.txt'` — пусто.

### Аудит 22.09.2026 — ClickHouse row reader `Array(T)`/`Tuple` и агрегаты `group_array`/`group_uniq_array` (P0 — нет; P1 — 1 доковый; P2 — 2)

Публичная поверхность аддитивна; переименований нет. Новые члены:

- `ClickHouseFunctions.group_array<T>(T? value) -> T[]` (`Query/SqlFunctions.ClickHouse.cs:127-134`) → `groupArray(value)`;
- `ClickHouseFunctions.group_uniq_array<T>(T? value) -> T[]` (`:136-143`) → `groupUniqArray(value)`;
- маппинг — `ClickHouseDialect.MakeAggregate` (`src/nextorm.clickhouse/ClickHouseDialect.cs:398-399`); трансляция — `AdvancedAggregateTranslator.EmitSimple` (`src/nextorm.core/Visitors/AdvancedAggregateTranslator.cs:142-147`) под флагом `ISqlDialect.SupportsArrayFunctions`.
- Row reader (публичная сигнатура не меняется): `SelectExpression.GetDataRecordMethod()` (`Expressions/SelectExpression.cs:117-130`) — `GetValue`-ветки для `T[]`/`System.Tuple`; `TypeFacts.IsSingleColumnProjection`/`IsTupleType` (`Visitors/TypeFacts.cs:45-74`) — `internal`, внешней поверхности не дают.

**CS1591/XML-doc.** XML-`<summary>` есть у обоих новых методов; доки обновлены и у array-возвращающих соседей (`retention`, `split_by_char`, `array_sort`/`array_reverse`/`array_distinct`/`range`/`array_enumerate`/`array_cum_sum`/`array_slice`/`array_push_back`) — с «можно проецировать напрямую». Новых публичных **типов** нет → Приложение A (45) без изменений; покрытие публичных методов **+2** (215/1094 против baseline 213/1092, актуализация 18.09.2026) — дельта пересчитана арифметически по диффу. Именование — конвенции соблюдены: snake_case DSL зеркалит SQL (`groupArray`/`groupUniqArray` по образцу `uniq_exact`→`uniqExact`), гейт — как у соседних array-функций. P0/P1 по **именам** нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHARR1 | P2 | `Query/SqlFunctions.ClickHouse.cs:134,143`; `PublicAPI.*.txt` отсутствуют | 2 новых публичных члена не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1/ASF1/SQ1/SCTF1, не новая находка**; новых abstract-членов `ISqlDialect` нет, разрыва для внешних реализаторов не создаётся | При заморозке внести `ClickHouseFunctions.group_array<T>(T? value) -> T[]` и `group_uniq_array<T>(T? value) -> T[]` в `PublicAPI.Unshipped.txt` (точный текст — из анализатора при заморозке) |
| CHARR2 ✅ закрыта 22.09.2026 | P1 (док) | `docs/advanced/limitations.md:36` (+`docs/ru/advanced/limitations.md:36`); `docs/providers/clickhouse.md:56` (+RU); `docs/guide/provider-specific/clickhouse.md:152` (+RU) | EN/RU-доки прямо противоречат реализованному поведению (тот же класс, что AR2): массив-колонку больше не «нельзя материализовать» (`array_agg` теперь проецируется напрямую — `tests/nextorm.postgres.tests/SqlGenerationTests.cs` изменён), а `retention`/`split_by_char`/`array_*` больше не «usable only nested». Правило AGENTS.md `docs/**` **и** `docs/ru/**` не выполнено | Снять оговорки «row reader не умеет»/«только вложенно» и описать прямую проекцию `T[]`/`Tuple` в обеих языковых ветках |
| CHARR3 ✅ закрыта 22.09.2026 | P2 | `Query/SqlFunctions.ClickHouse.cs:5-25` (классовый `<summary>`); `docs/providers/clickhouse.md:40-56` (+RU); `docs/guide/provider-specific/clickhouse.md:148-153` (+RU); `docs/advanced/api-reference.md` (+RU) | Новые `group_array`/`group_uniq_array` не добавлены в прозаическую докуку (precedent AR3/AJ5/RD2); классовый `<summary>` `ClickHouseFunctions` перечисляет array-функции, но не эти агрегаты | Дополнить `<summary>` класса и `SqlFunctions.ClickHouse`, а также provider/guide/api-reference-списки EN+RU одним изменением |

ℹ️ **Наблюдения (фикс не требуется):**

- **`T? value` — осознанная nullability.** Для unconstrained `T` это не `Nullable<T>`; соседний `array_push_back<T>(T[], T)` использует `T` — косметическая неоднородность, не P-нарушение.
- **`<typeparam name="T">` отсутствует** у обоих методов (как у соседних generic-членов) — на `CS1591` (в `<NoWarn>` 7 библиотечных `.csproj`) не влияет; для полноты DocFX можно добавить.
- **`ValueTuple` и арность >7 не покрыты.** `TypeFacts.IsTupleType` (`Visitors/TypeFacts.cs:66-74`) распознаёт только `System.Tuple` арности 1..7 (драйвер ClickHouse возвращает `System.Tuple`; интеграционный тест — `Tuple<int,string>`); TODO-план (`docs/specs/roadmap/todo_clickhouse_arrays.md:70`) обещает `ValueTuple<>` — расхождение формулировки; арность 8+ не проверена. Кандидат — юнит/интеграционный тест на арности 1 и 3.
- **Row reader без новых публичных типов/флагов.** Тип ветки выбирается по `SelectExpression.PropertyType`, диалектного флага нет: это инфраструктура материализации, а не SQL-функция; SQL Server/MySQL/MariaDB/SQLite физически не отдают array/Tuple-колонку, InMemory row reader не использует. Обоснование — в `code-smells-review.md`, точечный аудит 22.09.2026.
- **Шаг 5 не двигается:** `PublicAPI.*.txt` — **0** файлов, `PublicApiAnalyzers`/ApiCompat/API-approval не настроены; новые подписи — в CHARR1.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **201/201**, `tests/nextorm.postgres.tests` — **265/265** (прогнано в этом проходе); `find . -name 'PublicAPI*.txt'` — пусто (подтверждает CHARR1); XML-`<summary>` у обоих новых методов и `<see cref="ISqlDialect.SupportsArrayFunctions"/>` разрешается; новых публичных типов нет — Приложение A (45) без изменений. Контейнерная интеграция ClickHouse 25.8 в этом проходе не перезапускалась; по отчёту автора изменения — ClickHouse **63/63**, PostgreSQL **223** (6 capability-skips).

### Аудит 22.09.2026 — ClickHouse higher-order (lambda) array-функции (P0 — нет; P1 — 1 доковый; P2 — 2)

**Область (uncommitted worktree).** Публичная поверхность аддитивна; переименований нет. Новые члены:

- `ISqlDialect.SupportsHigherOrderArrayFunctions` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:145-152`, **abstract**; default `false` — `SqlDialectBase.cs:36`; override — `src/nextorm.clickhouse/ClickHouseDialect.cs:57`);
- 9 методов `ClickHouseFunctions` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:485-545`):
  `array_map<TIn,TOut>(Expression<Func<TIn,TOut>> function, TIn[] array) -> TOut[]` (`:485`);
  `array_filter<T>(Expression<Func<T,bool>> predicate, T[] array) -> T[]` (`:492`);
  `array_exists<T>(...)-> bool` (`:499`); `array_all<T>(...)-> bool` (`:506`);
  `array_count<T>(...)-> long` (`:513`);
  `array_first<T>(...)-> T?` (`:521`); `array_first_index<T>(...)-> long` (`:529`);
  `array_last<T>(...)-> T?` (`:537`); `array_last_index<T>(...)-> long` (`:545`);
- `ClickHouseDialect.MakeArrayFunction` расширен `arrayCount`/`arrayFirstIndex`/`arrayLastIndex` → `toInt64(...)` (`ClickHouseDialect.cs:65-71`); `ArraySqlTranslator.TryTranslateHigherOrderArray`/`EmitHigherOrderArray`/`ExtractLambda` и `HigherOrderLambdaVisitor` (`Visitors/ArraySqlTranslator.cs:261-341,408-455`) — `internal`, внешней поверхности не дают;
- новых публичных **типов** нет → **Приложение A (45) без изменений**.

**CS1591/XML-doc.** XML-`<summary>` есть у всех 9 новых методов и у нового флага (интерфейс + база + ClickHouse); `<typeparam name>` отсутствует (как у соседних generic-членов) — на `CS1591` в `<NoWarn>` 7 библиотечных `.csproj` не влияет. Покрытие публичных методов — **+9** к последнему зафиксированному **215/1094** → **224/1103** (арифметически по диффу; переизмерение рефлексией в этом проходе не выполнялось). **Имя флага — P0/P1 нет:** snake_case DSL зеркалит SQL (`arrayMap`/…/`arrayLastIndex`), лямбда-аргумент стоит **первым** (SQL `arrayMap(func, arr)`); прецедент `Expression<Func<…>>` — `count(Expression<Func<bool>>)` (`Query/SqlFunctions.cs:531`), `sum<T>(T?, Expression<Func<bool>>)` (`:546`), `string_agg<T>(T?, string, Expression<Func<bool>>)` (`:480`), но там лямбда — **трейлинг-фильтр без параметра**, здесь — обязательный поэлементный трансформер (соответствие SQL, а не фильтру). `T?` у `array_first`/`array_last` — см. ℹ️.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| HOAF1 | P2 | `Query/SqlFunctions.ClickHouse.cs:485-545`; `DataContext/Dialect/ISqlDialect.cs:152`; `SqlDialectBase.cs:36`; `ClickHouseDialect.cs:57,70`; `PublicAPI.*.txt` отсутствуют | 9 новых методов и новый **абстрактный** член `ISqlDialect` не трекаются (Шаг 5 открыт). `SupportsHigherOrderArrayFunctions` abstract — source-breaking для внешних реализаторов `ISqlDialect` (продолжение AR1/AJ1/CHARR1, не новая проблема) | При заморозке внести флаг (`ISqlDialect` + оба override'а) и 9 подписей `ClickHouseFunctions.*` в `PublicAPI.Unshipped.txt` (точный текст — из анализатора) |
| HOAF2 ✅ закрыта 22.09.2026 | **P1 (док)** | `docs/guide/provider-specific/clickhouse.md:197-201` (+`docs/ru/guide/provider-specific/clickhouse.md:199-204`) | EN/RU прямо противоречили реализации («higher-order array functions … are out of scope today») | Снято: пункт убран из «Not yet supported»/«Пока не поддерживается»; флаг + 9 методов описаны в `guide/11-scalar-functions.md` (EN+RU) |
| HOAF3 ✅ закрыта 22.09.2026 | P2 | `docs/guide/11-scalar-functions.md` (+RU); `docs/advanced/api-reference.md` (+RU); `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.5 | Новые 9 методов и флаг не были добавлены в доки EN+RU; §4 п.5 гласил «has not been started» | Таблица guide/11, список api-reference и §4 п.5 (EN+RU) дополнены; comparison-спеки обновлены |

ℹ️ **Наблюдения (фикс не требуется):**

- **`T?` у `array_first`/`array_last` — «может быть default», а не NULL.** ClickHouse `arrayFirst`/`arrayLast` при отсутствии совпадения возвращают default элемента (0/пустая строка), не SQL NULL; для value-`T` `T?` = `Nullable<T>` остаётся не-null (`0`), для reference-`T` `T?` не меняет рантайм-тип. Форма согласована с соседями `group_array<T>(T?)`/`array_push_back`, но XML-док «or the default value of `T`» был бы точнее слова `null`. Путь «нет совпадения» интеграционным тестом не покрыт (проверены только совпадающие элементы) — кандидат в тест.
- **Отдельный флаг не конфликтует с `SupportsArrayFunctions`/`SupportsArrayJoin`.** Размещён в том же array-кластере (`ISqlDialect.cs:135/143/152/165`; `SqlDialectBase.cs:34-37`), тот же `virtual => false` и CH-override, XML-док с обоснованием. Дифференциатор «есть array-функции, нет lambda» пока недостижим (только CH реализует массивы), но это семейный флаг (не по члену) — «зонтик без доказательства невыразимости» (ср. ASF1) не создаётся.
- **Лямбда-параметр эмитится голым идентификатором** (`ArraySqlTranslator.cs:430`), а не через `AppendIdentifier`: это связанная переменная SQL-лямбды, не физический столбец/таблица, поэтому кавычки диалекта (`QuoteIdentifiers`) неприменимы; physical-идентификаторы внутри тела по-прежнему идут через `MemberTranslator`. Утечки лямбда-параметра в резолвер колонок нет: `VisitMember` (`:439-446`) на корне-параметре бросает `NotSupportedException`.
- **Аргументы-массивы рендерятся существующим `SqlOperandTranslator.AppendArrayOrColumn`** (`ArraySqlTranslator.cs:310,325`): колонка — в SQL, захваченный/inline-массив — одним параметром; SQL не зависит от числа элементов, план остаётся кэшируемым.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test tests/nextorm.clickhouse.tests -c Debug` — **208/208**; `dotnet test tests/nextorm.postgres.tests -c Debug` — **266/266**; контейнерная интеграция `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~ClickHouseIntegrationTests.Array"` — **15/15** (0 failed / 0 skipped; включая 5 новых). XML-`<summary>` у 9 методов и флага; новых публичных типов нет — Приложение A (45) без изменений; `find -name 'PublicAPI*.txt'` — пусто (подтверждает HOAF1). Содержательная кодовая сторона — `code-smells-review.md`, Находки 62–63.

### Аудит 22.09.2026 — ClickHouse параметризованные array-агрегаты `topK`/`topKWeighted`/`quantiles` (P0 — нет; P1 — нет; P2 — 4)

**Область (uncommitted worktree).** Публичная поверхность аддитивна; переименований нет. Новые члены:

- `IQuantileAggregateRenderer.RenderArray(string name, string levels, string value) -> string` (`src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:125-126`) — **новый абстрактный** член публичного интерфейса;
- `ITopKAggregateRenderer` (`DialectCapabilities.cs:132-143`) — новый публичный тип + `Render`/`RenderWeighted`;
- `ISqlDialect.TopKAggregates` DIM `=> null` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:386-392`), реализация `SqlDialectBase.TopKAggregates` (`:93-94`) и `ClickHouseDialect.TopKAggregates` (`src/nextorm.clickhouse/ClickHouseDialect.cs:145`);
- 3 метода `ClickHouseFunctions` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`):
  - `quantiles<T>(double[] levels, T? value) -> double[]` (`:95-102`) → `quantiles(level...)(value)`;
  - `top_k<T>(long k, T? value) -> T[]` (`:104-111`) → `topK(k)(value)`;
  - `top_k_weighted<T, TWeight>(long k, T? value, TWeight? weight) -> T[]` (`:113-118`) → `topKWeighted(k)(value, weight)`.

**CS1591/XML-doc.** XML-`<summary>` есть у всех новых типов/членов (новый интерфейс + 2 члена, DIM, база/override, 3 метода) и у обновлённых классовых summary `ClickHouseFunctions`/`AdvancedAggregateTranslator`; `<typeparam name>`/`<param>` отсутствуют у части generic-членов (как у соседей) — на `CS1591` в `<NoWarn>` 7 библиотечных `.csproj` не влияет. Новый публичный **тип** `ITopKAggregateRenderer` задокументирован, поэтому Приложение A (45 недокументированных) **не меняется**, но общее число публичных типов **+1** (переизмерение рефлексией в этом проходе не выполнялось). Покрытие публичных методов **+3** к зафиксированному 224/1103 → **227/1106** (арифметически по диффу).

**Именование — P0/P1 по именам нет.** `top_k`/`top_k_weighted` — snake_case-зеркало SQL `topK`/`topKWeighted` по образцу `uniq_exact`→`uniqExact`/`quantile_exact`→`quantileExact`; `quantiles` — множественное от `quantile`; `ITopKAggregateRenderer` — по образцу `IUniqAggregateRenderer`/`IQuantileAggregateRenderer`; `k` — нативный параметр ClickHouse. Выделение отдельного capability-объекта вместо `SupportsArrayFunctions` обосновано (per-function, не family-umbrella) и повторяет rationale `IUniqAggregateRenderer`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHQA1 | P2 (API-compat) | `DataContext/Dialect/DialectCapabilities.cs:125-126` | `RenderArray` добавлен **абстрактным** членом в публичный интерфейс `IQuantileAggregateRenderer` — source-break для внешних реализаторов. Контраст с тем же изменением: `ISqlDialect.TopKAggregates` намеренно оформлен DIM «so that existing external implementations keep compiling» (`ISqlDialect.cs:389`), а topK вынесен отдельным объектом. In-repo реализатор один (`ClickHouseQuantileAggregateRenderer`), pre-1.0/alpha — риск низкий; continuation AR1/AJ1/CHARR1/HOAF1 | Согласовать политику: либо зафиксировать, что renderer-интерфейсы не рассчитаны на внешнюю реализацию, либо вынести multi-level форму в отдельный объект `IMultiQuantileAggregateRenderer` (по образцу `ITopKAggregateRenderer`) вместо расширения существующего. Alpha-политика (`API-NAMING-REVIEW.md` §1) второй вариант не обязывает |
| CHQA2 ✅ применено 22.09.2026 | P2 (naming) | `DataContext/Dialect/DialectCapabilities.cs:125-126` | `RenderArray` называет форму по CLR-типу (массив) и читается как «рендерит массив», хотя у `quantiles` массив — это **параметр** (`levels`), а sibling'ы названы по агрегату (`Render`, `RenderMedian`) | Переименовано в `RenderLevels(name, levels, value)` (roslyn rename, 5 документов): параллельно `RenderMedian`, имя совпадает с параметром |
| CHQA3 ✅ закрыто 22.09.2026 | P2 (доки) | `docs/advanced/api-reference.md:63,70` (+`docs/ru/advanced/api-reference.md:63,70`); `docs/guide/04-grouping-and-aggregates.md:420-439` (+RU); `docs/guide/provider-specific/clickhouse.md:141-153` (+RU); `docs/specs/roadmap/sql-capabilities-gap-analysis.md:170` | `quantiles`/`top_k`/`top_k_weighted`, `ITopKAggregateRenderer` и `IQuantileAggregateRenderer.RenderArray` не добавлены в доки EN+RU; gap-analysis §4 п.4 по-прежнему гласил «Still open on this item: `topK`/`topKWeighted`/`quantiles`» | Дополнены api-reference/guide-04/provider-specific EN+RU (семейства, гейты `QuantileAggregates`/`TopKAggregates`, double-parentheses форма, `Array(T)`/`Array(Float64)`); `topK`/`quantiles` сняты из «Still open» в gap-analysis §4 п.4 |
| CHQA4 | P2 (трекинг) | `Query/SqlFunctions.ClickHouse.cs:95-118`; `DataContext/Dialect/DialectCapabilities.cs:125,132-143`; `ISqlDialect.cs:386-392`; `PublicAPI.*.txt` отсутствуют | Новый публичный тип `ITopKAggregateRenderer` + DIM `TopKAggregates` + абстрактный `RenderArray` + 3 метода не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). Continuation RD2/AR1/AJ1/CHARR1/HOAF1 — не новая проблема | При заморозке внести в `PublicAPI.Unshipped.txt` новый тип и все члены (base/override/DIM + 3 метода); точный текст — из анализатора |

ℹ️ **Наблюдения (фикс не требуется):**

- **`ISqlDialect`-сторона не ломает внешних реализаторов.** `TopKAggregates` — DIM `=> null` (`ISqlDialect.cs:386-392`), как у остальных capability-объектов; `SqlDialectBase` даёт `virtual` `=> null`, ClickHouse переопределяет. Пара «вычисляемый/абстрактный член vs объект» — существующий паттерн Фаз 2/3.
- **`RenderArray` в контрактном тесте вызывается не вакуумно** (`tests/nextorm.integration.tests/DialectCapabilityContractTests.cs:123`): при непустом `QuantileAggregates` проверяется `NotBeNullOrEmpty`, а `Dialects.Should().Contain(d => d.TopKAggregates != null)` (`:182`) не даёт объекту «потеряться». Находка 48 не повторяется.
- **Имена тестов согласованы.** `ClickHouseDialectTests.MakeTopK_ShouldUseDoubleParentheses` (`:266`) следует прежнему `MakeQuantile_ShouldUseDoubleParenthesesAndCastToFloat64` (`:256`) — стиль `Make*` для юнит-тестов рендереров уже принят, P-нарушения нет.
- **Ограничения среза осознанны** (`load_factor`/`'counts'`; `quantilesExact`/`Timing`/`GK`) — отражены в `todo_clickhouse_arrays.md`, новых публичных членов под них нет.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; XML-`<summary>` у всех новых типов/членов; новых публичных типов ровно **1** (`ITopKAggregateRenderer`) → Приложение A (45) без изменений; `find -name 'PublicAPI*.txt'` — пусто (подтверждает CHQA4). Полный прогон с покрытием — **2315 passed / 30 skipped / 0 failed**, **line 85.4% / branch 74.4%** (ClickHouse **220/220**, postgres **268/268**, ClickHouse+capability-contract **73/73**). Содержательная кодовая сторона — `code-smells-review.md`, Находка 64.

### Аудит 22.09.2026 — ClickHouse предикаты над массивами `startsWith`/`endsWith`/`hasSubstr` (uncommitted worktree, срез 4 `todo_clickhouse_arrays.md`; P0 — нет; P1 — нет; P2 — 3)

**Область (uncommitted worktree, branch `1.0.4-alpha`, HEAD `ba9c29b`).** Публичная поверхность аддитивна; переименований нет. Новые члены:

- `ClickHouseFunctions.starts_with<T>(T[] array, T[] prefix) -> bool` (`Query/SqlFunctions.ClickHouse.cs:451`) → `startsWith(array, prefix)`;
- `ClickHouseFunctions.ends_with<T>(T[] array, T[] suffix) -> bool` (`:454`) → `endsWith(array, suffix)`;
- `ClickHouseFunctions.has_substr<T>(T[] array, T[] other) -> bool` (`:462`) → `hasSubstr(array, other)`;
- трансляция — `Visitors/ArraySqlTranslator.TryTranslateClickHouseArray` (`Visitors/ArraySqlTranslator.cs:212-220`): три `case` → `EmitArrayFunction` через существующий `RequireArrayFunctions` (`:392`), гейт `ISqlDialect.SupportsArrayFunctions` (только `ClickHouseDialect.cs:52 => true`); `internal`, внешней поверхности не даёт;
- обновлена классовая `<summary>` `ClickHouseFunctions` (`Query/SqlFunctions.ClickHouse.cs:24-25`); новых публичных **типов**/флагов/`Make*`-хуков нет.

**Матрица подтверждена.** `startsWith`/`endsWith`/`hasSubstr` над `Array(T)` есть только у ClickHouse (PostgreSQL — лишь частная эмуляция префикса срезом, у остальных нет array-типа); проверено на реальном ClickHouse 25.8: `startsWith([3,1,2],[3,1])=1`, `endsWith([3,1,2],[1,2])=1`, `hasSubstr([3,1,2],[1,2])=1`, `hasSubstr([3,1,2],[3,2])=0` (непрерывная подпоследовательность).

**CS1591/XML-doc.** XML-`<summary>` есть у всех 3 новых методов (`:446-462`); новых публичных типов нет → **Приложение A (45) без изменений**. Покрытие публичных методов — **+3** к зафиксированному **227/1106** → **230/1109** (арифметически по диффу; переизмерение рефлексией в этом проходе не выполнялось).

**Именование — P0/P1 по именам нет.** `starts_with`/`ends_with`/`has_substr` — snake_case-зеркало ClickHouse-токенов `startsWith`/`endsWith`/`hasSubstr`, ровно как `has_any`→`hasAny`/`has_all`→`hasAll`/`index_of`→`indexOf`; форма `bool <verb>_<noun><T>(T[] array, T[] other) -> bool` повторяет соседей, первый параметр — массив (порядок SQL), имена `prefix`/`suffix`/`other` описательны. BCL-конфликтов нет (`startsWith` не затеняет `System.String.StartsWith` — другой тип и сигнатура).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHARP1 ✅ закрыта 22.09.2026 | P2 (доки) | `docs/guide/11-scalar-functions.md:487-490` (+`docs/ru/guide/11-scalar-functions.md:494-497`); `docs/guide/provider-specific/clickhouse.md:176` (+RU `:178`); `docs/providers/clickhouse.md:181` (+RU); `docs/advanced/api-reference.md:63` (+`docs/ru/advanced/api-reference.md:63`) | Три новых метода не добавлены в доки EN+RU, хотя сам план среза (`todo_clickhouse_arrays.md`, тест-план) перечисляет эти файлы; правило AGENTS.md «`docs/**` **и** `docs/ru/**`» не выполнено. Противоречия с реализацией нет — только пропуск (класс AR3/CHARR3/HOAF3) | Дополнить списки array-функций `startsWith`/`endsWith`/`hasSubstr` (CLR `starts_with`/`ends_with`/`has_substr`) в guide/11, provider-specific/clickhouse, providers/clickhouse и api-reference, EN+RU одним изменением |
| CHARP2 ✅ закрыта 22.09.2026 | P2 | `Query/SqlFunctions.cs:41-57`; `DataContext/Dialect/ISqlDialect.cs:136-143` | Кумулятивный док-пробел (ср. Z2/GLI1/AR3/CHARR3): классовая `<summary>` `ClickHouseFunctions` пополнена `startsWith`/`endsWith`/`hasSubstr`, но `<summary>` свойства `SqlFunctions.ClickHouse` и гейта `SupportsArrayFunctions` перечисляют array-функции без них | Дополнить оба перечня при закрытии Шага 5 (либо тем же изменением, что CHARP1) |
| CHARP3 | P2 (трекинг) | `Query/SqlFunctions.ClickHouse.cs:451,454,462`; `PublicAPI.*.txt` отсутствуют | 3 новых члена публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1/ASF1/SQ1/CHARR1/HOAF1/CHQA4, не новая находка**; новых abstract-членов `ISqlDialect` нет, разрыва для внешних реализаторов не создаётся | При заморозке внести три подписи `ClickHouseFunctions.starts_with<T>`/`ends_with<T>`/`has_substr<T>` в `PublicAPI.Unshipped.txt` (точный текст — из анализатора) |

ℹ️ **Наблюдения (фикс не требуется):**

- **Переиспользование `SupportsArrayFunctions` вместо per-function гейта — обосновано (DC-критерий).** Три метода — то же семейство «array-функции над нативным `Array(T)`», что `length`/`has`/`has_any`/`has_all`/`arraySort`; отдельный `Supports*` на каждый член был бы «зонтиком без доказательства невыразимости» (ASF1), тем более что имена совпадают с ClickHouse-токенами и `Make*`-хук не нужен (результат `UInt8`→`bool`, каст не добавляется; `MakeArrayFunction` не расширяется). Аналогия DC7 (один объект принудительно уравнивает co-support) здесь безвредна: array-тип есть только у ClickHouse, и он поддерживает все три предиката.
- **Версионная оговорка.** Перегрузки этих функций над `Array(T)` появились в относительно свежих ClickHouse; модель гейтов диалектная, не версионная, поэтому поддержка старых серверов — внешняя по отношению к реестру (для проверенного 25.8 не блокер).
- **Post-check (`rg` по `*Dialect.cs`) пуст — и это ожидаемо.** `rg "startsWith|endsWith|hasSubstr" src/nextorm.*/*Dialect.cs` — **0 совпадений**; единственный диалектный маркер — `ClickHouseDialect.SupportsArrayFunctions => true` (`ClickHouseDialect.cs:52`). Маппинг имён живёт в общем `ArraySqlTranslator` (CH-only ветка), как у `hasAny`/`hasAll`/`arraySort`; «одиночность» диалекта выражена флагом, а не токеном. Матрице не противоречит.
- **`ends_with` без оговорки о гейте.** `<summary>` `starts_with`/`has_substr` несут «Requires a provider that supports array functions (see `SupportsArrayFunctions`)», у `ends_with` — нет; стилевая неоднородность внутри одного семейства, не P-нарушение.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход); `rg "startsWith|endsWith|hasSubstr" src/nextorm.*/*Dialect.cs` — **0** (объяснено выше); `find -name 'PublicAPI*.txt'` — **0** (подтверждает CHARP3); XML-`<summary>` у всех 3 методов; новых публичных типов нет — Приложение A (45) без изменений. Тесты (по отчёту автора изменения): clickhouse unit **221/221**, postgres **269/269**, контейнерная интеграция `ClickHouseIntegrationTests` **72/72**, полный прогон **2318 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.4%** (базис не изменился). Содержательная кодовая сторона — `code-smells-review.md`, точечный аудит 22.09.2026.

### Аудит 22.09.2026 — ClickHouse скалярная поверхность над `Tuple` (`tuple`/`tupleElement`), срез 5 `todo_clickhouse_arrays.md` (uncommitted worktree; P0 — нет; P1 — нет; P2 — 2)

**Область (uncommitted worktree, branch `1.0.4-alpha`, HEAD `265a83d`).** Публичная поверхность аддитивна; переименований нет. Новые члены:

- `ISqlDialect.SupportsTupleFunctions` — **DIM** `=> false` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:155-162`);
- `SqlDialectBase.SupportsTupleFunctions` — `public virtual` (`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:36`);
- `ClickHouseDialect.SupportsTupleFunctions` — `public override` + XML-`<summary>` (`src/nextorm.clickhouse/ClickHouseDialect.cs:61-66`);
- `internal static TupleSqlTranslator` (`src/nextorm.core/Visitors/TupleSqlTranslator.cs`) и его точки вызова (`BaseExpressionVisitor.VisitMethodCall:118`, `MemberTranslator.TryTranslate:86`) — `internal`, внешней поверхности не дают; новых публичных **DSL-методов** и **типов** нет → **Приложение A (45) без изменений**.

**Уточнение tier-а.** «Tier (a) — без нового публичного API» верно только для DSL/типов: новый публичный член поверхности всё же один — capability-флаг (DIM + `virtual` + `override`). Он аддитивен и за счёт DIM **source- и binary-совместим** для внешних реализаторов `ISqlDialect` (в отличие от abstract-соседей).

**CS1591/XML-doc.** XML-`<summary>` есть у DIM (`:155-161`) и у ClickHouse-override (`:61-65`); `SqlDialectBase`-override без доки — ровно как соседние `SupportsArrays`/`SupportsArrayFunctions` (`SqlDialectBase.cs:34-38`), на `CS1591` (в `<NoWarn>` 7 библиотечных `.csproj`) не влияет. Новых публичных типов нет → Приложение A (45) без изменений. Покрытие публичных **свойств** — арифметически **+3** члена (интерфейс + база + override), из них с докой **+2**; публичных методов **+0** (переизмерение рефлексией в этом проходе не выполнялось).

**Именование — P0/P1 по именам нет.** `SupportsTupleFunctions` следует семейству `SupportsArrays`/`SupportsArrayFunctions`/`SupportsHigherOrderArrayFunctions` (`bool` + `<summary>`), BCL-конфликтов нет; `Tuple.Create`/`System.Tuple<>.ItemN` — существующие BCL-имена, которые **транслируются**, а не добавляются в поверхность.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHTUP1 | P2 (трекинг) | `ISqlDialect.cs:155-162`; `SqlDialectBase.cs:36`; `ClickHouseDialect.cs:61-66`; `PublicAPI.*.txt` отсутствуют | Новый публичный член (DIM + `virtual` + `override`) не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1/ASF1/SQ1/CHARR1/HOAF1/CHQA4/CHARP3, не новая проблема**; DIM-форма разрыва для внешних реализаторов не создаёт | При заморозке внести `ISqlDialect.SupportsTupleFunctions` (DIM), `SqlDialectBase.SupportsTupleFunctions`, `ClickHouseDialect.SupportsTupleFunctions` в `PublicAPI.Unshipped.txt` (точный текст — из анализатора) |
| CHTUP2 ✅ закрыта 22.09.2026 | P2 (док) | `docs/guide/11-scalar-functions.md:521-522` (+`docs/ru/guide/11-scalar-functions.md:528-529`); `docs/guide/provider-specific/clickhouse.md` (+RU); `docs/providers/clickhouse.md` (+RU); `docs/advanced/api-reference.md` (+RU); `docs/advanced/limitations.md` (+RU); `docs/specs/roadmap/sql-capabilities-gap-analysis.md:173-175` | Новая скалярная поверхность (`Tuple.Create`→`tuple(...)`, `.ItemN`→`tupleElement(t, n)`) не описана в EN+RU, хотя тест-план среза (`todo_clickhouse_arrays.md`) перечисляет эти файлы (правило AGENTS.md «`docs/**` **и** `docs/ru/**`»). В guide/11 (EN+RU) есть только материализация `Tuple(...)` row reader-ом, про трансляцию конструктора/элемента — нет; `untuple` не записан как ограничение; gap-analysis §4 п.4 держит `tuple`/`tupleElement` в «Still open». Противоречия с реализацией нет — только пропуск (класс AR3/CHARR3/HOAF3/CHARP1) | Дополнить guide/11, provider-specific/clickhouse, providers/clickhouse, api-reference (скалярная поверхность + гейт `SupportsTupleFunctions`), limitations (`untuple`, меняет набор колонок) и gap-analysis §4 п.4 EN+RU одним изменением |

ℹ️ **Наблюдения (фикс не требуется):**

- **Гейт `SupportsTupleFunctions` вместо `SupportsArrayFunctions` — обоснован (DC-критерий).** `Array(T)` и `Tuple(...)` — разные семейства типов: ClickHouse поддерживает оба, PostgreSQL — массивы ([`SupportsArrays`](xref:NextORM.Core.ISqlDialect.SupportsArrays) `=> true`), но скалярной tuple-поверхности не имеет; переиспользование array-флага принудительно связало бы co-support и сломало будущий «array без tuple». Отдельный семейный флаг (конструктор + доступ к элементу) не является «зонтиком без доказательства невыразимости» (ASF1): обе формы — нативные функции `tuple`/`tupleElement` одного типа.
- **DIM vs abstract.** `SupportsTupleFunctions` оформлен DIM, тогда как ближайшие array-соседи (`SupportsArrays`, `SupportsArrayFunctions`, `SupportsHigherOrderArrayFunctions`, `SupportsArrayJoin` — `ISqlDialect.cs:135/144/153/175`) **abstract**. В файле и раньше сосуществовали обе формы (`StringSplit`, `TopKAggregates` — DIM), а DIM безопаснее для внешних реализаторов; расхождение с соседями — вопрос единой политики Шага 5, не P-нарушение. Полезно зафиксировать выбор при заморозке.
- **`new Tuple<...>(a, b)` как проекция не тронут** (`NewExpression`, многоколоночная проекция по `TypeFacts.IsSingleColumnProjection`); но `.ItemN` на **вложенном** `new Tuple<...>` даёт битый SQL — это кодовая сторона, `code-smells-review.md`, Находка 65.
- **Арность.** `TypeFacts.IsTupleType` по-прежнему распознаёт `System.Tuple` 1..7 (`Visitors/TypeFacts.cs:67-75`); `Tuple.Create` арности 8 отрендерит `tuple(...)`, но не материализуется — пре-существующее ограничение среза 1, не новое.
- **`untuple` вне объёма** (возвращает несколько колонок, а не скаляр) — решение среза; требует только записи в `limitations` (CHTUP2).

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход); `dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **226/226**, `tests/nextorm.postgres.tests` — **271/271** (прогнано в этом проходе); `rg "tupleElement|SupportsTupleFunctions" src/nextorm.*/*Dialect.cs` — только `ClickHouseDialect.cs:64,66` (объяснено в `code-smells-review.md`); `find -name 'PublicAPI*.txt'` — **0** (подтверждает CHTUP1); XML-`<summary>` у DIM и override; новых публичных типов нет — Приложение A (45) без изменений. Тесты (по отчёту автора изменения): контейнерная интеграция `ClickHouseIntegrationTests` **74/74**, полный прогон **2327 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%** (baseline 85.4%/74.4%). Содержательная кодовая сторона — `code-smells-review.md`, точечный аудит 22.09.2026.

### Аудит 22.09.2026 — ClickHouse array-возвращающие JSON-функции `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`, срез 6 `todo_clickhouse_arrays.md` (uncommitted worktree; P0 — нет; P1 — нет; P2 — 3)

**Область (uncommitted worktree, branch `1.0.4-alpha`).** Публичная поверхность аддитивна; переименований нет. Новые члены — 6 методов `ClickHouseFunctions` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:194-224`):

- `string[] json_extract_keys(string? json)` (`:200`) → `JSONExtractKeys(json)`;
- `string[] json_extract_keys(string? json, string? path)` (`:203`) → `JSONExtractKeys(json, path)`;
- `string[] json_extract_array_raw(string? json)` (`:210`) → `JSONExtractArrayRaw(json)`;
- `string[] json_extract_array_raw(string? json, string? path)` (`:213`) → `JSONExtractArrayRaw(json, path)`;
- `Tuple<string, T>[] json_extract_keys_and_values<T>(string? json)` (`:221`) → `JSONExtractKeysAndValues(json, value_type)`;
- `Tuple<string, T>[] json_extract_keys_and_values<T>(string? json, string? path)` (`:224`) → `JSONExtractKeysAndValues(json, path, value_type)`.

Трансляция и маппинг — `internal` (`Visitors/JsonExtractSqlTranslator.cs:44-52`, `src/nextorm.clickhouse/ClickHouseDialect.cs:198-200`), внешней поверхности не дают; новых публичных **типов**, полей и членов `ISqlDialect` нет (переиспользуется `SupportsJsonExtract`/`MakeJsonExtract`) → **Приложение A (45) без изменений**.

**CS1591/XML-doc.** XML-`<summary>` есть у всех 6 новых методов (у 2-аргументных — `<see cref>` на 1-аргументный sibling); `<typeparam name="T">` у generic-пары отсутствует — как у соседних generic-членов, на `CS1591` (в `<NoWarn>` 7 библиотечных `.csproj`) не влияет. Покрытие публичных методов — **+6** к зафиксированному **230/1109** → **236/1115** (арифметически по диффу; переизмерение рефлексией в этом проходе не выполнялось).

**Именование — P0/P1 по именам нет.** `json_extract_keys`/`json_extract_array_raw`/`json_extract_keys_and_values` — snake_case-зеркало ClickHouse-токенов `JSONExtractKeys`/`JSONExtractArrayRaw`/`JSONExtractKeysAndValues`, ровно как `json_extract_string`→`JSONExtractString`/`json_extract_raw`→`JSONExtractRaw` (J-раздел) и `has_any`→`hasAny`; 1-/2-аргументные перегрузки отражают необязательный `path` ClickHouse (у остальных `json_extract_*` `path` обязателен — там нативно нет 1-аргументной формы). BCL-конфликтов нет. Возвратные типы — `string[]`/`Tuple<string,T>[]` — согласованы с `json_all_paths -> string[]`, `group_array<T> -> T[]` и с формой `System.Tuple`, которую отдаёт драйвер и распознаёт `TypeFacts.IsTupleType`. `T` обязан задаваться явно (нет параметра типа `T`) — это осознанно и отражено в XML. Единственный содержательный вопрос — nullable `T` (CHJS3, кодовая сторона — `code-smells-review.md`, Находка 66).

**Переиспользование `SupportsJsonExtract` вместо нового флага — обосновано.** Три функции — то же семейство JSON-as-text из текстовой колонки, что `JSONExtract*`/`visitParamExtract*`: `SupportsJsonExtract => true` выставляет только ClickHouse, поэтому отдельный флаг был бы истинен ровно там же и добавил бы лишний публичный контракт к Шагу 5 (source-breaking для внешних реализаторов) без функционального эффекта — тот же вывод, что для `visitParamExtract*` и `startsWith`/`hasSubstr`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHJS1 | P2 (трекинг) | `Query/SqlFunctions.ClickHouse.cs:200,203,210,213,221,224`; `PublicAPI.*.txt` отсутствуют | 6 новых членов публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение RD2/AR1/ASF1/SQ1/CHARR1/HOAF1/CHQA4/CHARP3/CHTUP1, не новая проблема**; новых abstract-членов `ISqlDialect` нет | При заморозке внести шесть подписей `ClickHouseFunctions.json_extract_keys`/`json_extract_array_raw`/`json_extract_keys_and_values<T>` в `PublicAPI.Unshipped.txt` (точный текст — из анализатора) |
| CHJS2 ✅ закрыта 22.09.2026 | P2 (док) | `docs/guide/18-json.md:22-25,47,301` (+`docs/ru/guide/18-json.md:22-25,47,304`); `docs/guide/provider-specific/clickhouse.md:168-169` (+RU); `docs/providers/clickhouse.md:57-59,173` (+RU `:58-60,176`); `docs/advanced/api-reference.md:63` (+RU `:63`); `docs/specs/roadmap/sql-capabilities-gap-analysis.md:175-177` | Три новых метода не добавлены в доки EN+RU, хотя тест-план среза (`todo_clickhouse_arrays.md`) перечисляет эти файлы (правило AGENTS.md «`docs/**` **и** `docs/ru/**`»). Список ClickHouse-функций `guide/18-json.md:22-25` заканчивается `JSONType`; gap-analysis §4 п.4 по-прежнему держит `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw` в «Still open on this item». Противоречия с реализацией нет — только пропуск (класс AR3/CHARR3/HOAF3/CHARP1/CHTUP2) | Дополнить списки JSON-функций (`JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`; CLR `json_extract_keys`/`json_extract_array_raw`/`json_extract_keys_and_values<T>`; результаты `string[]`/`Tuple<string,T>[]`; гейт `SupportsJsonExtract`) в guide/18, provider-specific/clickhouse, providers/clickhouse и api-reference EN+RU и снять три функции из «Still open» в gap-analysis одним изменением |
| CHJS3 | P2 (контракт) | `Query/SqlFunctions.ClickHouse.cs:221,224`; `Visitors/JsonExtractSqlTranslator.cs:130` | `T` не ограничен, поэтому `json_extract_keys_and_values<int?>` допустим, но `value_type` разворачивается до `'Int32'`, а объявленный элемент остаётся `Tuple<string,int?>` → потенциальный `InvalidCastException` на материализации (подробно — `code-smells-review.md`, Находка 66). XML-док не оговаривает допустимость nullable `T` | Зафиксировать контракт: либо отклонять nullable `T` с понятным исключением, либо рендерить nullable ClickHouse-тип; в XML-`<typeparam name="T">` описать ожидаемый non-nullable ClickHouse-тип и добавить тест на `int?` |

ℹ️ **Наблюдения (фикс не требуется):**

- **`Tuple<string,T>[]` — верная форма.** `Array(Tuple(String, value_type))` материализуется `System.Tuple<,>`-массивом (драйвер ClickHouse.Client; `TypeFacts.IsTupleType` — `Visitors/TypeFacts.cs:67-75`), ветка `SelectExpression.GetDataRecordMethod` для `T[]` — `GetValue` (`:117-123`), как у `group_array`/`json_all_paths`. `ValueTuple` намеренно не используется.
- **Литерал `value_type` рендерится в core через `Dialect.MakeTypeName`.** Провайдерное знание (имя типа) остаётся в диалекте; жёсткое `'` — общий строковый литерал SQL, риск экранирования отсутствует (имя типа из `System.Type`). Если у второго диалекта появится иная форма `value_type`, литерал следует увести в `MakeJsonExtract`/новый хук — сейчас преждевременно (см. `code-smells-review.md`, ℹ️).
- **`MakeTypeName` для `value_type` наследует пре-существующие пробелы диалекта.** `Guid`→`"Guid"` (у ClickHouse `UUID`), `sbyte`→`"SByte"`, `char`→`"Char"` — та же функция используется и в кастах (`ClickHouseDialect.cs:595`), т.е. это не новое; `bool`→`"Boolean"` требует проверки алиаса. Кандидат в тест при расширении набора `T`.
- **Тестовое покрытие среза.** CH SQL-gen покрывает все 6 форм (в т.ч. `Int32`/`Int64` для `value_type`), postgres rejection — 3 формы, контейнерная интеграция — 6 форм на реальном ClickHouse 25.8; nullable `T` и нечисловые `T` (`string`/`bool`) не покрыты — см. CHJS3.
- **Шаг 5 не двигается:** `PublicAPI.*.txt` — **0** файлов, `PublicApiAnalyzers`/ApiCompat/API-approval не настроены; новые подписи — в CHJS1.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход); `rg "JSONExtractKeys|JSONExtractArrayRaw|json_extract_keys" src/nextorm.*/*Dialect.cs` — только `ClickHouseDialect.cs:198-200` (объяснено в `code-smells-review.md`, post-check); `find -name 'PublicAPI*.txt'` — **0** (подтверждает CHJS1); XML-`<summary>` у всех 6 методов; новых публичных типов нет — Приложение A (45) без изменений. Тесты (по отчёту автора изменения): clickhouse unit **228/228**, postgres **271/271**, контейнерная интеграция `ClickHouseIntegrationTests` **75/75**, полный прогон **2330 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%** (baseline 85.4%/74.5%). Содержательная кодовая сторона — `code-smells-review.md`, Находка 66.

### Аудит 22.09.2026 — ClickHouse иерархические dictionary-функции `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`, срез 7 `todo_clickhouse_arrays.md` (uncommitted worktree; P0 — нет; P1 — нет; P2 — 3)

**Область (uncommitted worktree).** Публичная поверхность аддитивна; переименований нет. Новые члены — 3 метода `ClickHouseFunctions` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`):

- `ulong[] dict_get_hierarchy<TKey>(string? dict, TKey? id)` (`:316`) → `dictGetHierarchy('dict', key)`;
- `ulong[] dict_get_children<TKey>(string? dict, TKey? id)` (`:323`) → `dictGetChildren('dict', key)`;
- `bool dict_is_in<TKey>(string? dict, TKey? childId, TKey? ancestorId)` (`:331`) → `dictIsIn('dict', child, ancestor)`.

Трансляция — 3 ветки `internal` `DictionarySqlTranslator.TryTranslate` (`Visitors/DictionarySqlTranslator.cs:31-39`, `EmitFunction` — `:45-66`); маппинг имён — `ClickHouseDialect.MakeDictionaryFunction` +3 (`src/nextorm.clickhouse/ClickHouseDialect.cs:242-244`). Новых публичных **типов** и членов `ISqlDialect` нет — гейт прежний `SupportsDictionaries` (D-раздел), нового флага не вводится → **Приложение A (45) без изменений**.

**CS1591/XML-doc.** XML-`<summary>` есть у всех 3 новых методов (с `<see cref="ISqlDialect.SupportsDictionaries"/>`); `<typeparam name="TKey">` отсутствует — как у соседних `dict_has<TKey>`/`dict_get<TValue,TKey>`, на `CS1591` (в `<NoWarn>` 7 библиотечных `.csproj`) не влияет. Покрытие публичных методов — **+3** к зафиксированному **236/1115** → **239/1118** (арифметически по диффу; переизмерение рефлексией в этом проходе не выполнялось).

**Именование — P0/P1 по именам нет.** `dict_get_hierarchy`/`dict_get_children`/`dict_is_in` — snake_case-зеркало ClickHouse-токенов `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`, ровно как `dict_get`→`dictGet`, `dict_get_or_default`→`dictGetOrDefault`, `dict_has`→`dictHas` и `json_extract_keys`→`JSONExtractKeys`; BCL-конфликтов нет. Возвратные типы `ulong[]`/`bool` соответствуют нативным `Array(UInt64)`/`UInt8` (проверено автором на 25.8) и существующей поверхности: `ulong` — уже первоклассный тип проекции (row reader `SelectExpression.cs:113-115`; интеграционные `UInt64Columns_ShouldMaterializeAsUlong`/`UInt64Projection_ShouldMaterializeValueAboveInt64Max`), `Array(T)` → `T[]` (`json_all_paths`/`group_array`), `UInt8`→`bool` (`dict_has`/`JSONHas`). Арность (`==2`/`==3`) и порядок аргументов `child, ancestor` совпадают с ClickHouse.

**Переиспользование `SupportsDictionaries` вместо нового флага — обосновано (DC-критерий).** Три функции — то же семейство hierarchical dictionaries, что `dictGet*`/`dictHas`: `SupportsDictionaries => true` выставляет только ClickHouse, отдельный флаг был бы истинен ровно там же и добавил бы лишний публичный контракт к Шагу 5; гейт и `MakeDictionaryFunction`-хук уже существуют, транслятор — один и тот же.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHDH1 | P2 (трекинг) | `Query/SqlFunctions.ClickHouse.cs:316,323,331`; `PublicAPI.*.txt` отсутствуют | 3 новых члена публичной поверхности не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение D1/RD2/…/CHJS1/CHTUP1, не новая проблема** | При заморозке внести три подписи `ClickHouseFunctions.dict_get_hierarchy<TKey>`/`dict_get_children<TKey>`/`dict_is_in<TKey>` в `PublicAPI.Unshipped.txt` (точный текст — из анализатора) |
| CHDH2 | P2 (док) | `docs/guide/11-scalar-functions.md:30-31` (+`docs/ru/guide/11-scalar-functions.md:33`); `docs/guide/provider-specific/clickhouse.md:178` (+RU `:180`); `docs/providers/clickhouse.md:69-70,178` (+RU `:69-70,180`); `docs/advanced/api-reference.md:63` (+RU `:63`); `docs/specs/roadmap/sql-capabilities-gap-analysis.md:179` | Три новых метода не добавлены в доки EN+RU, хотя тест-план среза (`todo_clickhouse_arrays.md`, §«Срез 7») перечисляет эти файлы (правило AGENTS.md «`docs/**` **и** `docs/ru/**`»). Списки dictionary-функций в guide/11, provider-specific/clickhouse, providers/clickhouse и api-reference заканчиваются `dict_get`/`dict_get_or_default`/`dict_has`; gap-analysis §4 п.4 всё ещё держит `dictGetHierarchy`/`dictGetChildren`/`dictIsIn` в «Still open on this item». Противоречия с реализацией нет — только пропуск (класс AR3/CHARR3/HOAF3/CHARP1/CHTUP2/CHJS2) | **ИСПРАВЛЕНА 22.09.2026:** списки дополнены (`dict_get_hierarchy`/`dict_get_children` → `ulong[]`, `dict_is_in` → `bool`) в guide/11, provider-specific/clickhouse, providers/clickhouse, api-reference и `capability-matrix.md` EN+RU; три функции сняты из «Still open» в gap-analysis; `docfx` — 0 errors |
| CHDH3 | P2 (док, кумулятивный) | `DataContext/Dialect/ISqlDialect.cs:486-491`; `src/nextorm.clickhouse/ClickHouseDialect.cs:231` | XML-`<summary>` гейта `SupportsDictionaries` и его ClickHouse-override перечисляют только `dictGet`/`dictGetOrDefault`/`dictHas` — после этого среза семейство 6 функций. Пре-существующее накопление (ср. D3/CHARP2), не регресс среза, на `CS1591`/сборку не влияет | **ИСПРАВЛЕНА 22.09.2026:** оба `<summary>` перечисляют `dictGet`/`dictGetOrDefault`/`dictHas`/`dictGetHierarchy`/`dictGetChildren`/`dictIsIn`; build `0/0` |

ℹ️ **Наблюдения (фикс не требуется):**

- **`ulong[]` для `Array(UInt64)` — верная форма, но без интеграционной материализации.** `dictGetHierarchy`/`dictGetChildren` всегда возвращают `Array(UInt64)` независимо от типа ключа (проверено автором `clickhouse-local` 25.8); `ClickHouse.Driver` 1.4.0 отдаёт `Array(UInt64)` как `ulong[]` (элементный framework-тип — `ulong`, тот же, что у скалярного `UInt64`), row reader идёт по общей array-ветке `GetValue`+cast (`SelectExpression.cs:117-123`). Интеграционный тест отсутствует намеренно: нужен `CREATE DICTIONARY ... HIERARCHICAL` (у остальных `dictGet*` интеграционных тестов тоже нет) — пре-существующее ограничение D-раздела, не регресс среза. При расширении на `dict_get`-подобный integration harness — добавить `Array(UInt64)`-проекцию.
- **`bool` для `dictIsIn` — тот же путь, что `dictHas`.** Нативный `UInt8` → `bool` (`SelectExpression.GetBooleanMI`), подтверждено существующим `dict_has`/`JSONHas`; отдельного риска нет.
- **Тестовое покрытие среза.** CH SQL-gen `HierarchicalDictFunctions_ShouldUseClickHouseNames` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1173`) — 3 формы; маппинг имён — `ClickHouseDialectTests.MakeDictionaryFunction_ShouldMapNames` (`:249`, +3 ассерта); rejection — postgres `DictFunctions_ShouldThrowBecausePostgresHasNoDictionaries` (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1865`, +3 записи). Кодовая сторона о втором SQL-gen тесте — `code-smells-review.md`, Находка 67.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход); `find -name 'PublicAPI*.txt'` — **0** (подтверждает CHDH1); XML-`<summary>` у всех 3 методов; новых публичных типов нет — Приложение A (45) без изменений; изменённые `.cs` — CRLF. Тесты (по отчёту автора изменения): полный прогон **2333 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%**. Содержательная кодовая сторона — `code-smells-review.md`, точечный аудит 22.09.2026.

### Аудит 22.09.2026 — ClickHouse `SEMI`/`ANTI`/`PASTE` joins (uncommitted worktree; P0 — нет; P1 — 1 доковый; P2 — 3)

**Область (uncommitted worktree).** Публичная поверхность аддитивна; переименований нет. Новые члены:

- `JoinType.Semi = 8`/`Anti = 9`/`Paste = 10` — `src/nextorm.core/Expressions/JoinExpression.cs:28,33,39` (XML-`<summary>` у каждого, включая `<see cref="JoinStrictness"/>` в `Semi`/`Anti`);
- `ISqlDialect.SupportsSemiAntiJoin`/`SupportsPasteJoin` — `DataContext/Dialect/ISqlDialect.cs:71,78` (**default interface members** `=> false`, XML-`<summary>`+`<see cref="JoinType.Semi"/>`/`Paste` есть);
- `SqlDialectBase.SupportsSemiAntiJoin`/`SupportsPasteJoin` — `DataContext/Dialect/SqlDialectBase.cs:30-31` (`public virtual => false`; **XML-`<summary>` отсутствует**);
- override ClickHouse — `src/nextorm.clickhouse/ClickHouseDialect.cs:48,51` (XML-`<summary>` есть); `ClickHouseDialect.MakeJoinKeyword` (`:92`) **расширен** рендером `left semi join`/`left anti join`/`paste join` (существующий член, арность не менялась);
- DSL `EntityBuilder<TEntity>.SemiJoin`/`AntiJoin`/`PasteJoin` + 3 `QueryCommand`-перегрузки — `Builders/EntityBuilder.cs:981,989,996,999,1002,1005` (XML-`<summary>`/`<inheritdoc cref>` есть); non-generic `EntityBuilder` — 6 методов `:1506,1509,1515,1548,1551,1554`;
- 18 `new`-методов на `JoinedEntityBuilder<T1..T7>` (по 3 на арность; терминал `T8` не расширяется) — `Builders/Joins/JoinedEntityBuilder.cs:36,39,42,122,125,128,188,191,194,254,257,260,320,323,326,386,389,392` (у всех XML-`<summary>`).

**CS1591/XML-doc.** XML-комментарий есть у 3 enum-членов, 2 членов `ISqlDialect` (DIM), 2 override'ов ClickHouse и всех 30 DSL-методов (30-й — `<inheritdoc cref>`); **без XML остаются только 2 новых `public virtual`-члена `SqlDialectBase`** (`:30-31`) — `CS1591` скрыт `<NoWarn>` в 7 библиотечных `.csproj` (Шаг 5), сборка 0/0 дефект не покажет. Новых публичных **типов** нет → **Приложение A (45) без изменений**. Общее число публичных типов не менялось; покрытие: методы **+30** к **239/1118** → **269/1148**; свойства **+6** (4 документированы → **101/353**); члены enum **+3** документированы (если считать их полями — **21/87**) — арифметически по диффу, переизмерение рефлексией в этом проходе не выполнялось.

**Именование — P0/P1 по именам нет.** `SemiJoin`/`AntiJoin`/`PasteJoin` повторяют существующий паттерн `LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`/`CrossApply`/`OuterApply` (`EntityBuilder.cs:1487-1500`); `SupportsSemiAntiJoin`/`SupportsPasteJoin` — `Supports*`-пару `SupportsGlobalJoin`/`SupportsJoinStrictness`; `JoinType.Semi`/`Anti`/`Paste` — официальные токены ClickHouse (`LEFT SEMI/ANTI JOIN`, `PASTE JOIN`), PascalCase, без BCL-конфликтов (нет `System.*.Semi`/`Paste`). Возвратные формы согласованы с семантикой: SEMI/ANTI не добавляют правых колонок (возврат `EntityBuilder<TEntity>`/`JoinedEntityBuilder<T1..Tn>` с той же арностью, как и левая сторона), PASTE добавляет одну (`JoinedEntityBuilder<TEntity,TJoinEntity>`) — ошибка проектирования вида «SEMI отдал правые колонки» невозможна на уровне типов.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHJ1 ✅ закрыта 22.09.2026 | **P1 (док)** | `docs/providers/clickhouse.md:108`; `docs/ru/providers/clickhouse.md:109`; `docs/guide/03-joins.md:430` (+RU `:436`); `docs/advanced/api-reference.md:86` (+RU `:86`); `docs/specs/roadmap/sql-capabilities-gap-analysis.md:209-210`; `docs/specs/roadmap/todo_clickhouse_join_strictness.md:5` | **Прозаическая документация прямо противоречит реализации.** EN: «`ASOF` needs one equi-join column plus a final inequality. **`SEMI`/`ANTI`/`PASTE` are not supported.**» и RU-зеркало «`SEMI`/`ANTI`/`PASTE` **не поддерживаются**» — после фичи ClickHouse их поддерживает через `SemiJoin`/`AntiJoin`/`PasteJoin`. Также: `JoinType` в guide/03-joins и в api-reference перечислен только до `Cross`/`OuterApply` (нет `Semi`/`Anti`/`Paste` и `FullCross`, если он уже есть в enum); gap-analysis §4 п.8 всё ещё «not implemented»; `todo_clickhouse_join_strictness.md` в статусе «заблокировано» при том, что срез закрыт. AGENTS.md требует править `docs/**` **и** `docs/ru/**`; класс — как CHARR2/AR3 (док опровергает код) | **Обязательно к исправлению в том же изменении:** в `providers/clickhouse.md` (+RU) заменить «not supported» на описание `SemiJoin`/`AntiJoin`/`PasteJoin` (левые колонки, SEMI не размножает строки, PASTE — по позиции/без `ON`); дополнить списки `JoinType` в guide/03-joins EN+RU и api-reference EN+RU (`Semi`/`Anti`/`Paste`); закрыть п.8 в gap-analysis; перевести `todo_clickhouse_join_strictness.md` в «Done». Маршрут — `nextorm-design-engineer` |
| CHJ2 | P2 (трекинг) | `ISqlDialect.cs:71,78`; `SqlDialectBase.cs:30-31`; `ClickHouseDialect.cs:48,51`; `JoinExpression.cs:28,33,39`; `EntityBuilder.cs:981…1005,1506…1554`; `JoinedEntityBuilder.cs:36…392`; `PublicAPI.*.txt` отсутствуют | Новые 30 методов / 6 свойств / 3 члена enum не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение JS1/GG1/CHDH1, не новая проблема.** DIM-флаги не создают source-разрыва, но всё равно попадут в `PublicAPI.Unshipped.txt`; `MakeJoinKeyword` арность не менял | При заморозке внести флаги (`SupportsSemiAntiJoin.get`/`SupportsPasteJoin.get`), override'ы базы/ClickHouse, 3 члена `JoinType` и 30 DSL-методов (точный текст — из анализатора) |
| CHJ3 ✅ закрыта 22.09.2026 | P2 (док XML) | `ISqlDialect.cs:79-85`; `src/nextorm.clickhouse/ClickHouseDialect.cs:87-91`; `SqlDialectBase.cs:30-31` | XML-`<summary>` `MakeJoinKeyword` (интерфейс) утверждает «Renders the join keyword for `joinType`…», но не упоминает, что `joinType` теперь может быть `Semi`/`Anti`/`Paste` и что для них есть отдельные гейты; summary ClickHouse-оверрайда перечисляет только `[global] [inner|left|right|full|cross] [any|all|asof] join` (без `left semi`/`left anti`/`paste`). У 2 новых `virtual`-членов `SqlDialectBase` XML нет вовсе. На `CS1591` (в `<NoWarn>`) и сборку не влияет | Дополнить `<summary>` `MakeJoinKeyword` (интерфейс+ClickHouse) новыми видами и гейтами `SupportsSemiAntiJoin`/`SupportsPasteJoin`; добавить `<summary>` двум `SqlDialectBase`-членам при закрытии Шага 5 (кумулятивно, ср. CHDH3/CHARR2) |
| CHJ4 | P2 (политика совместимости) | `ISqlDialect.cs:71,78` vs `:59,64`; `SqlDialectBase.cs:30-31` | Новые флаги — **default interface members** (`=> false`), тогда как соседние `SupportsJoinStrictness` (`:59`) и `SupportsGlobalJoin` (`:64`) — абстрактные; в одном контракте две политики совместимости (тот же класс, что JS2/AR4/SQ-DIM). Дополнительно `SqlDialectBase` повторяет тот же `=> false`, что DIM, — для in-repo диалектов DIM избыточен (перекрыт `virtual`-override'ом), но нужен внешним реализаторам `ISqlDialect` без базы | **Принято с обоснованием:** DIM снимает source-разрыв для внешних реализаторов `ISqlDialect` — направление, рекомендованное AR4/SQ-DIM. Зафиксировать решение на Шаге 5: либо унифицировать все новые `Supports*`-флаги как DIM `=> false`, либо явно оставить `SupportsJoinStrictness`/`SupportsGlobalJoin` абстрактными (alpha-разрыв уже принят JS2/GG1). Не блокер релиза |

ℹ️ **Наблюдения (фикс не требуется).**

- **Отдельный флаг `SupportsSemiAntiJoin` на два вида (SEMI+ANTI) — оправдан (DC-критерий).** Оба вида всегда реализуются вместе (ClickHouse) и нигде больше; отдельные `SupportsSemiJoin`/`SupportsAntiJoin` были бы истинны ровно в одном диалекте и добавили бы лишний публичный контракт к Шагу 5. `SupportsPasteJoin` отделён, т.к. `PASTE` не имеет `ON` и образует самостоятельную конструкцию (совпадает с `todo_clickhouse_join_strictness.md` §«Что нужно решить»).
- **`new`-перегрузки 18 (6 арностей × 3) — вынужденная ковариантность, не дубли контракта** (см. code-smells ℹ️ для `WithStrictness`): плоская цепочка `.Join().SemiJoin()/PasteJoin()` должна сохранять конкретный arity; общий базовый метод вернул бы `EntityBuilder<Projection<…>>` и сломал chaining. Дублирование тел — уже Находка 68 в code-smells, не именование.
- **Отсутствие отдельного публичного `MakeSemiJoinKeyword`/`MakePasteJoinKeyword` — правильно.** Все виды рендерятся одним хуком `MakeJoinKeyword`, как `GLOBAL`/strictness (GG2): единый keyword `[global ]left semi join` связнее трёх хуков.
- **Приложение A (45) без изменений** — новых публичных типов нет; все новые члены либо задокументированы, либо (2 `SqlDialectBase`-члена) относятся к существующему типу.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name 'PublicAPI*.txt'` — **0** (подтверждает CHJ2); XML-`<summary>` у `JoinType.Semi/Anti/Paste`, `ISqlDialect.SupportsSemiAntiJoin/SupportsPasteJoin`, `ClickHouseDialect`-override'ов и 30 DSL-методов; новые публичные типы отсутствуют — Приложение A (45) без изменений; `rg "SEMI|ANTI|PASTE" docs/providers/clickhouse.md docs/ru/providers/clickhouse.md` — противоречие CHJ1 подтверждено обеими ветками. Содержательная кодовая сторона — `code-smells-review.md`, точечный аудит 22.09.2026 (Находки 68–71).

### Аудит 22.09.2026 — доступ к колонкам mapped-сущностей по имени (`SqlFunctions.Column<T>`), uncommitted worktree (P0 — нет; P1 — 1 доковый; P2 — 2)

**Область (uncommitted worktree).** Публичная поверхность аддитивна; переименований нет. Новый член:

- `SqlFunctions.Column<T>(object entity, string columnName) -> T` — `src/nextorm.core/Query/SqlFunctions.cs:75` (маркер, тело — `throw new NotSupportedException`); новые публичные **типы** отсутствуют.

**CS1591/XML-doc.** У члена есть `<summary>` + `<typeparam>` + `<param>`×2 + `<remarks>` (`:63-74`) → `CS1591` закрыт; отсутствует только `<returns>` (ℹ️ ниже). Новых публичных типов нет → **Приложение A (45) без изменений**; покрытие методов +1 (арифметически, **270/1149**), из них документирован — 1. Сборка `0/0`; `CS1591` скрыт `<NoWarn>` в 7 библиотечных `.csproj` (Шаг 5).

**Именование — P0/P1 по именам нет.** `Column<T>` следует паттерну соседнего маркера `Parameter<T>` (`SqlFunctions.cs:61`) и `EF.Property<T>(object entity, string propertyName)`; существительное-имя согласовано с SQL-зеркальной поверхностью `SqlFunctions`, BCL-конфликтов нет (`System.*.Column` отсутствует). Порядок параметров «сущность (target) → имя (required)», возврат `T`, generic-параметр `T` — по конвенции. `object entity` — цена эргономики: `Column<TEntity, T>(TEntity entity, …)` был бы типобезопаснее, но многословнее и не давал бы вывести `T`; EF Core принял то же решение (см. CHCB3).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CHCB1 ✅ закрыта 22.09.2026 | **P1 (док)** | `docs/specs/roadmap/sql-capabilities-gap-analysis.md:223-225`; `docs/specs/roadmap/todo_clickhouse_columns_by_name.md:52-64`; `docs/guide/01-querying-and-projections.md` (+RU `docs/ru/...`), `docs/providers/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU) | **Прозаическая документация не обновлена и противоречит реализации.** gap-analysis §4 п.10 по-прежнему «**ClickHouse columns have no by-name access without an entity property** … Todo: todo_clickhouse_columns_by_name.md»; roadmap-файл (статус «в работе») в §«Документация» заявляет, что обновлены `guide/01-querying-and-projections` (+RU), `providers/clickhouse` (+RU), `advanced/api-reference` (+RU) и gap-analysis → «shipped», но в рабочем дереве этих правок нет — `SqlFunctions.Column` встречается только в самом roadmap-файле. Дополнительно §«Тесты» обещает интеграцию на `hits_v1`/`IHit` со сверкой `WithSql`, а реализованы тесты на `wide_entity`/`IWideEntity` без `WithSql`. Класс CHJ1/CHARR2 (док опровергает код); AGENTS.md требует EN **и** RU | **Обязательно к исправлению в том же изменении (маршрут — `nextorm-design-engineer`):** добавить подраздел «колонки по имени» в `guide/01-querying-and-projections` (EN+RU; untyped `TableAlias` + entity `Column<T>`), строку в `providers/clickhouse` (EN+RU) и `advanced/api-reference` (EN+RU); перевести gap-analysis §4 п.10 в «shipped», roadmap-файл → «готово»; привести §«Тесты» в соответствие с фактическими тестами |
| CHCB2 | P2 (трекинг) | `SqlFunctions.cs:75`; `PublicAPI.*.txt` отсутствуют | Новый публичный член не трекается (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение JS1/CHJ2/CHDH1, не новая проблема** | При заморозке внести `NextORM.Core.SqlFunctions.Column<T>(object? entity, string? columnName) -> T` (точный текст — из анализатора) |
| CHCB3 | P2 (форма/устойчивость) | `SqlFunctions.cs:75`; `NormSqlTranslator.cs:75-85`; `BaseExpressionVisitor.cs:206-246`; ср. `MemberTranslator.cs:417` | `object entity` + отсутствие проверки источника: не-параметрическое/производное выражение молча даёт голый или чужой `alias.column` вместо ошибки. Кодовая сторона — `code-smells-review.md`, **Находка 73**. Публичная сигнатура повторяет `EF.Property<T>(object, string)`, поэтому проблема — в отсутствии валидации, а не в `object` как таковом | Требовать разрешения аргумента в параметр источника (иначе `BuildSqlCommandException`); при желании — ограничить тип дженерик-параметром сущности. Добавить негативный тест |

ℹ️ **Наблюдения (фикс не требуется).**

- **`Column<T>` в `SqlFunctions` (core), а не в провайдерном `ClickHouseFunctions` — правильно.** Фича — builder-level ссылка на идентификатор `alias.column`, работающая в любом SQL-диалекте через `QuoteIdentifier`/`AppendIdentifier`; todo-матрица подтверждает поддержку PG/SQL Server/MySQL/MariaDB/ClickHouse/SQLite. Отдельного `Supports*`-гейта не требуется (не SQL-функция).
- **Единственный публичный член, покрытие `CS1591` не страдает.** Отсутствие `<returns>` — косметика: стоило бы дописать «The column value read as `T`» (ср. соседние члены без `<returns>` тоже есть — `Parameter<T>` вообще без XML, пре-существующая дыра).
- **Тестовая матрица только 3 диалекта** (CH/PG/SQLite); SQL Server/MySQL/MariaDB используют общий `AppendIdentifier`/`QuoteIdentifier` (ℹ️).
- **Приложение A без изменений** — новых публичных типов нет; поверхность заморозки растёт на одну подпись (CHCB2).

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `find -name 'PublicAPI*.txt'` — **0** (подтверждает CHCB2); XML-`<summary>`/`<typeparam>`/`<param>`/`<remarks>` у `Column<T>` подтверждены (`SqlFunctions.cs:63-76`); `grep "SqlFunctions.Column" docs/` — только `docs/specs/roadmap/todo_clickhouse_columns_by_name.md` (подтверждает CHCB1); `grep "no by-name access" docs/specs/roadmap/sql-capabilities-gap-analysis.md` — §4 п.10 не обновлён. Тесты — см. содержательную сторону, `code-smells-review.md` (Находки 72–73).

### Аудит 22.09.2026 — DML `INSERT` (фаза 1), uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 4)

**Область (uncommitted worktree).** Публичная поверхность **аддитивна**, переименований нет. Новые подписи (к заморозке Шага 5):

- **Тип** `NextORM.Core.InsertBuilder<TEntity>` (`sealed`; ctor `internal`) + члены: `Value<TValue>(Expression<Func<TEntity,TValue>>, TValue) -> InsertBuilder<TEntity>`; `Value<TValue>(Expression<Func<TEntity,TValue>>, Expression<Func<TEntity,TValue>>) -> InsertBuilder<TEntity>`; `Values(TEntity) -> InsertBuilder<TEntity>`; `Values(IEnumerable<TEntity>) -> InsertBuilder<TEntity>`; `Insert() -> int`; `InsertAsync(CancellationToken = default) -> Task<int>`; `InsertWithIdentity<TKey>(Expression<Func<TEntity,TKey>>) -> TKey`; `InsertWithIdentityAsync<TKey>(Expression<Func<TEntity,TKey>>, CancellationToken = default) -> Task<TKey>`; `ToSql() -> string`.
- `DataContextExtensions.InsertInto<TEntity>(IDataContext, Action<EntityMetadataBuilder<TEntity>>? = null) -> InsertBuilder<TEntity>`.
- `IPropertyMetadata.IsKey.get -> bool` (DIM), `IsIdentity.get -> bool` (DIM), `IsComputed.get -> bool` (DIM).
- `ISqlDialect.SupportsReturning.get -> bool` (DIM), `SupportsOutput.get -> bool` (DIM), `SupportsLastInsertId.get -> bool` (DIM); `MakeReturning(IReadOnlyList<string>) -> string` (DIM), `MakeOutput(IReadOnlyList<string>) -> string` (DIM), `MakeLastInsertId() -> string` (DIM).
- `SqlDialectBase` — те же 6 как `virtual`; provider-переопределения: `PostgresDialect.SupportsReturning`, `SqliteDialect.SupportsReturning`/`SupportsLastInsertId`, `SqlServerDialect.SupportsOutput`, `MySqlDialect.SupportsLastInsertId`/`MakeLastInsertId` (MariaDB наследует MySQL).
- `EntityPropertyBuilder<T>.Key() -> EntityPropertyBuilder<T>`, `.Identity() -> …`, `.Computed() -> …`.

**Вне публичной поверхности (сознательно):** `SqlStatementType`, `MutationCommand`, `InsertCommand`, `InsertColumn`, `InsertValue`, `SqlMutationBuilder`, `IMutationExecutor` — все `internal`; `QueryExecutor` — `internal` (новые `public`-методы внутри `internal`-класса, `IQueryExecutor` не расширялся).

**CS1591/XML-doc.** Весь новый публичный API документирован (`<summary>`/`<typeparam>`/`<param>`/`<returns>`), `CS1591` не подавлен. **Уточнение к Шагу 5 выше:** в текущем дереве `NoWarn` отсутствует во всех 7 библиотечных `.csproj` (`rg -c NoWarn` → **0**), `CS1591` снят — под `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`) + `TreatWarningsAsErrors=true` сборка `0/0` доказывает 100% покрытие нового (и всего) публичного API. Новых публичных типов — **1** (`InsertBuilder<TEntity>`), он документирован → Приложение A без изменений по количеству.

**Именование — P0 нет.** `InsertBuilder`/`InsertInto`/`Value`/`Values`/`Insert`/`InsertWithIdentity`/`ToSql` следуют RFC и linq2db/ADO.NET (`InsertWithIdentity`); `Async`-суффикс — только у пар с sync-твином (AGENTS.md соблюдён); BCL-конфликтов нет (`System.*.InsertBuilder` отсутствует). `Supports*`/`Make*`/`Is*` — под существующие паттерны `ISqlDialect`/`IPropertyMetadata`. Единственный P1 — отсутствие пользовательской документации (IDML1).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML1 | **P1 (док)** | `docs/guide/` (+`docs/ru/guide/`), `docs/providers/*` (+RU), `docs/advanced/api-reference.md` (+RU), `docs/advanced/limitations.md` (+RU), `docs/specs/comparison/linq2db-comparison.md` (+RU), `docs/specs/roadmap/sql-capabilities-gap-analysis.md` | **Новый публичный DML-API не задокументирован.** `rg 'InsertInto\|InsertBuilder\|InsertWithIdentity' docs/ -g '!specs/**'` находит их **только** в `docs/specs/roadmap/todo_insert.md`; файла `docs/guide/19-data-modification.md` (и RU-зеркала) нет. RFC §«Файлы к изменению» (`todo_insert.md:329-332`) требует обновить guide/providers/limitations/api-reference/comparison/gap-analysis EN+RU; AGENTS.md — EN **и** RU | **Обязательно в том же изменении (маршрут — `nextorm-design-engineer`):** добавить `docs/guide/19-data-modification.md` + `docs/ru/...` (значения/сущность/батч/`InsertWithIdentity`, ограничения ClickHouse/in-memory, флаги `.Key()/.Identity()/.Computed()` и `[DatabaseGenerated]`), строки в `providers/*` (EN+RU) и `advanced/api-reference` (EN+RU); закрыть пробел DML в `linq2db-comparison`/gap-analysis. Ссылки на `docs/specs/**` из публичных доков запрещены |
| IDML2 | P2 (трекинг) | `PublicAPI.*.txt` отсутствуют; `Microsoft.CodeAnalysis.PublicApiAnalyzers`/ApiCompat/API-approval не подключены | Новые подписи не трекаются (Шаг 5 открыт). **Продолжение JS1/CHCB2/CHDH1** | При заморозке внести подписи `InsertBuilder<TEntity>` и `DataContextExtensions.InsertInto<TEntity>` (точный текст — из анализатора) |
| IDML3 | P2 (форма/надёжность) | `InsertBuilder.cs:164-186`; `DataContext.cs:240-274` | `InsertWithIdentity<TKey>` принимает любое mapped-свойство, `IsIdentity` не проверяется; на MySQL/MariaDB селектор игнорируется, возвращается `LAST_INSERT_ID()`. Кодовая сторона — `code-smells-review.md` **Находка 74** | Требовать `property.IsIdentity` (иначе исключение); добавить негативный тест. При желании — отклонять на диалекте без RETURNING/OUTPUT/LAST_INSERT_ID до исполнения |
| IDML4 | P2 (форма) | `InsertBuilder.cs:193` (`ToSql`) | `ToSql()` — «рендер без исполнения», но требует `IMutationExecutor` (`RequireExecutor`), а на in-memory бросает `NotSupportedException` с текстом «does not support data modification», хотя запись не выполняется; метод не заявлен в RFC. Semantics и текст исключения не совпадают | Либо вынести рендер из роли (nullable-диалект), либо переформулировать исключение («SQL rendering requires a database-backed context»); пометить в `<remarks>`, что параметры не возвращаются — диагностика только |
| IDML5 | P2 (форма) | `InsertBuilder.cs:41,60,95,108` | `null`-литерал делает перегрузки неоднозначными: `.Value(x => x.Name, null)` и `.Values(null)` не компилируются (CS0121) — `null` одинаково применим к `TValue` и к `Expression<Func<TEntity,TValue>>` (и к `TEntity` vs `IEnumerable<TEntity>`) | Документировать требование каста `(string?)null`/`(TEntity?)null` в `<summary>`; либо сузить expression-перегрузку (напр., отдельное имя `ValueFromColumn`) |

ℹ️ **Наблюдения (фикс не требуется).**

- **Отклонения от RFC — сознательные.** (1) Ось команд и роль `internal`: `MutationCommand`/`IMutationExecutor` не попали в публичную поверхность (RFC рисовал `public interface IMutationExecutor`) — плюс для заморозки; внешний контекст, не наследующий `DataContext`, не может реализовать исполнение мутаций, но публичная точка расширения остаётся `ISqlDialect`. (2) `Prepare()` и `Returning<T>()` не реализованы (фаза 1; вопрос 6 / issue #15) — план-кэша у мутаций нет. (3) `Value(column, Expression)` поддерживает только ссылку на mapped-член или parameter-free выражение (иначе `NotSupportedException`), а не произвольное выражение.
- **`IPropertyMetadata` — DIM, а не breaking change.** RFC §1 называл добавление членов breaking; реализация дала `IsKey`/`IsIdentity`/`IsComputed` как default interface members (как ранее `IsColumnNameAuto`) → источник- и бинарно-совместимо для внешних реализаций. Улучшение против RFC.
- **RFC-обещание `EntityMetadata.Keys`/`Identity` не выполнено** (`todo_insert.md:79`): ни в `IEntityMetadata`, ни в `internal EntityMetadata` таких convenience-свойств нет; DML-код обходится `Properties`+флагами. Для update/delete фазы 2, вероятно, понадобится.
- **Именование `.Key()/.Identity()/.Computed()`** — существительные-настройщики, не согласованные с глагольным `.HasColumnName`; но так задано RFC (`todo_insert.md:76`) и принято во fluent mapping DSL, поэтому ℹ️.
- **Новые `Supports*`/`Make*` — против направления «Фазы 3».** Оставлены на `ISqlDialect`/`SqlDialectBase`, тогда как Фаза 3 уводила диалектные возможности в capability-объекты; соответствует RFC. При заморозке учесть в перечне не-объектной поверхности (класс B/C).

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.InsertBuilder'1` — 9 публичных членов (совпадает со списком выше), `NextORM.Core.EntityPropertyBuilder'1` — публичный ctor + `Build`/`HasColumnName`/`Key`/`Identity`/`Computed`; `rg 'public .* (class|interface|enum) (InsertBuilder|IMutationExecutor|MutationCommand|InsertCommand|SqlMutationBuilder|SqlStatementType)' src/` — только `InsertBuilder` (`internal`-ось подтверждена); `rg -c NoWarn` по 7 `.csproj` — **0**; `rg 'InsertInto|InsertBuilder|InsertWithIdentity' docs/ -g '!specs/**'` — пусто (IDML1); `find -name 'PublicAPI*.txt'` — **0** (IDML2). Тесты — `code-smells-review.md` (Находки 74–75, наблюдения A–G).

### Аудит 22.09.2026 — DML `INSERT` (фаза 2): `Returning()`/`InsertReturning*`, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 2)

**Область (uncommitted worktree).** Публичная поверхность **аддитивна**, переименований нет. Новые подписи (к заморозке Шага 5):

- **Тип** `NextORM.Core.InsertReturningBuilder<TEntity,TResult>` (`sealed`; ctor `internal`) + члены: `Single() -> TResult`; `SingleAsync(CancellationToken = default) -> Task<TResult>`; `ToList() -> IReadOnlyList<TResult>`; `ToListAsync(CancellationToken = default) -> Task<IReadOnlyList<TResult>>`; `ToSql() -> string`.
- `InsertBuilder<TEntity>.Returning() -> InsertReturningBuilder<TEntity, TEntity>`; `InsertBuilder<TEntity>.Returning<TResult>(Expression<Func<TEntity,TResult>>) -> InsertReturningBuilder<TEntity, TResult>`.

**Вне публичной поверхности (сознательно):** `InsertReturningBuilder<TEntity,TResult>.ParseProjection` — `internal static`; `IMutationExecutor.ExecuteReturning<TResult>` (+async) — в `internal`-роли; `RowMapperFactory.GetOrBuild<TResult>(string, Type, SelectExpression[], bool, Func<…>)` — внутри `internal static`; `InsertCommand.ReturningColumns` — `internal`-команда. `roslyn members NextORM.Core.InsertReturningBuilder'2` — **5** публичных методов + `internal` ctor.

**CS1591/XML-doc.** Весь новый публичный API документирован (`<summary>`/`<typeparam>`/`<param>`/`<returns>`/`<exception>`), `CS1591` не подавлен (`rg -c NoWarn` по 7 библиотечным `.csproj` — **0**; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build **0/0** доказывает 100% покрытие нового (и всего) публичного API. Новых публичных типов — **1** (`InsertReturningBuilder<TEntity,TResult>`), он документирован → Приложение A без изменений по количеству.

**Именование — P0/P1 по именам нет.** `…Builder` — суффикс типа-билдера; терминалы, `Async`-суффикс только у пар с sync-твином (AGENTS.md); `ToSql` согласован с `InsertBuilder.ToSql`; `cancellationToken` — последним (CA1068); BCL-конфликтов нет. `Returning` — герундий по имени SQL-клаузы, принят RFC и `InsertBuilder.Returning()`.

**Переименование терминалов (22.09.2026, по ревью владельца).** Исходные `Returning().InsertReturning()`/`InsertReturningMany()` давали «масло масляное» — три `Returning` в одной цепочке. Терминалы builder'а переименованы: `InsertReturning`/`InsertReturningAsync` → `Single`/`SingleAsync`, `InsertReturningMany`/`InsertReturningManyAsync` → `ToList`/`ToListAsync` (тип `InsertReturningBuilder` и переключатель `Returning()`/`Returning(projection)` сохранены; `ToSql()` сохранён). Поверхность не заморожена (uncommitted), переименование не breaking; `Single`/`ToList` согласованы с терминалами `EntityBuilderExtensions` (`Any`/`ToList`/`First`/`Single`/`Last`/`Count`/`To*`).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML6 | **P1 (док)** | `docs/guide/19-insert-statement.md` (+`docs/ru/guide/`), `docs/advanced/api-reference.md` (+RU), `docs/providers/overview.md` (+RU), `docs/advanced/limitations.md` (+RU) | **Публичный API фазы 2 не задокументирован.** `rg 'Returning\|InsertReturning' docs/ -g '!specs/**'` находит только SQL-синтаксис `RETURNING id` в таблице `InsertWithIdentity`, а не методы: guide 19 не содержит `Returning()`/`InsertReturning*`; `api-reference` перечисляет `Value/Values/Insert/InsertWithIdentity/ToSql` без `Returning`; `providers/overview` не имеет строки про row-returning-материализацию. AGENTS.md требует EN **и** RU | **Обязательно в том же изменении (маршрут — `nextorm-design-engineer`):** раздел «Returning inserted rows» в guide 19 (EN+RU): `Returning()`/`Returning(projection)`/`InsertReturning`/`InsertReturningMany`, гейт `SupportsReturning`/`SupportsOutput`, негатив на MySQL/ClickHouse/in-memory, оговорка о произвольном порядке строк батча на SQLite и об interface-entity (IDML7); строки в `providers/overview` (EN+RU) и `api-reference` (EN+RU); ограничение SQL Server `OUTPUT`+триггеры в `limitations` (EN+RU). Ссылки на `docs/specs/**` из публичных доков запрещены. **Закрыто (22.09.2026):** в guide 19 EN+RU добавлен раздел «Returning the inserted rows» (`Returning()`/`Returning(projection)`/`InsertReturning*`, таблица провайдеров, оговорки о порядке батча и interface-сущности); строки в `api-reference` (EN+RU), `providers/overview` (EN+RU), `limitations` (EN+RU), `linq2db-comparison` (EN+RU) |
| IDML7 | P2 (форма/надёжность) | `InsertBuilder.cs:200-206`; `InsertReturningBuilder.cs:39-44`; `RowMaterializerBuilder.cs:28-31` | `Returning()` (entity-форма) на interface-`TEntity` падает `QueryPreparationException: Cannot get ctor from …` на исполнении (интерфейс — легитимный mapped-тип, ср. `InsertInto<IInsertEntity>()` в интеграционных тестах). Кодовая сторона — `code-smells-review.md` **Находка 76** | Отклонять уже в `Returning()`/`ParseProjection` понятным `NotSupportedException`, либо материализовать в конкретную реализацию; негативный тест + док-строка. **Закрыто (22.09.2026):** отклонение сделано не в парсере, а на исполнении (`DataContext.EnsureReturningMaterializable<TResult>`), чтобы не ломать `ToSql()` на интерфейсной сущности; тесты `Insert_ReturningWholeEntity_OnInterface_ShouldThrow` + `Returning_WholeEntity_OnInterface_ShouldParseForSqlGeneration` |
| IDML8 | P2 (док/форма) | `InsertBuilder.cs:208-217`; `InsertReturningBuilder.cs:145-154,163-178` | XML-summary `Returning<TResult>` обещает «member-init», но `new Dto { Name = x.Id }` (цель ≠ источник) даёт `ArgumentNullException`, т.к. `ParseProjection` игнорирует `binding.Member`. Кодовая сторона — `code-smells-review.md` **Находка 77** | Либо поддержать цель binding'а, либо сузить `<summary>` и отклонять переименованные binding'и `NotSupportedException`. **Закрыто (22.09.2026):** цель binding'а поддержана (`BuildSelectList` берёт `target.Name`/`target` как `PropertyName`/`PropertyInfo`); тест `ReturningMemberInit_ShouldReturnSourceColumn` (postgres) |

ℹ️ **Наблюдения (фикс не требуется).**

- **`InsertReturningBuilder` без публичного ctor** — единая точка входа `InsertBuilder.Returning()`; публичный тип с `internal` ctor корректен, к заморозке вносить тип + 5 методов.
- **`ToSql()` фазы 2** гейтится `EnsureReturningSupportedIfNeeded` → на MySQL/ClickHouse бросает `NotSupportedException` («cannot return inserted rows»), на in-memory — `NotSupportedException` про database-backed context; **продолжение IDML4** (семантика «render without execute»).
- **Новых DIM/интерфейсных членов нет** — `ISqlDialect.SupportsReturning`/`SupportsOutput`/`MakeReturning`/`MakeOutput` уже добавлены фазой 1; фаза 2 переиспользует их без правок. Единственный `public`-тип фазы 2 — `InsertReturningBuilder<TEntity,TResult>`.
- **Поверхность не заморожена** — `PublicAPI.*.txt` по-прежнему отсутствуют (`find` — 0), `Microsoft.CodeAnalysis.PublicApiAnalyzers`/ApiCompat/API-approval не подключены; **IDML2 открыт** (Шаг 5).

**Закрытие ранее открытых.** IDML1 — ✅ закрыта: создан `docs/guide/19-insert-statement.md` (EN+RU), обновлены `api-reference`/`limitations`/`providers/overview` (+RU) и toc (EN+RU). IDML3 — ✅ закрыта вместе с Находкой 74 (введена проверка `IsIdentity`). IDML2 (Шаг 5) остаётся открытой.

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0**; `roslyn members NextORM.Core.InsertReturningBuilder'2` — 5 публичных методов (`Single`/`SingleAsync`/`ToList`/`ToListAsync`/`ToSql`), `ParseProjection` — `internal`; `rg 'Returning|InsertReturning' docs/ -g '!specs/**'` — только SQL-синтаксис (IDML6); `rg -c NoWarn` по 7 `.csproj` — **0**; `find -name 'PublicAPI*.txt'` — **0** (IDML2/Шаг 5). Тесты — `code-smells-review.md` (Находки 76–77, наблюдения A–I).

### Аудит 22.09.2026 — коррелированные подзапросы в in-memory провайдере (P0 — нет; P1 — 1 доковый; P2 — 1 трекинг)

**Область (uncommitted worktree).** Новые `DataContext/InMemoryCorrelatedPlan.cs` (`InMemoryCorrelatedPlan`, `InMemoryCorrelatedEvaluator`, `OuterReferenceToParameterVisitor`), `DataContext/InMemoryCorrelatedSubqueryRewriter.cs`; правки `InMemoryDataContext` (`_correlatedPlans` + `GetCorrelatedPlan`), `InMemoryRowMaterializer` (rewriter для one-/multi-column + `EnsureType`), `InMemoryConditionFactory` (context-параметр + rewrite `PreparedCondition`), `InMemoryOrdering` (context-параметр + rewrite сортировки; снят прямой каст `Expression<Func<TEntity,object>>`), `InMemoryQueryBuilder` (снят blanket `OuterReferences`-guard, добавлен grouped-guard), `Query/QueryCommand.Clone.cs` (`CloneForCorrelatedEvaluation`); тесты `CorrelatedQueryInMemoryTests` (9 позитивных вместо `NotSupportedException`).

**Публичная поверхность — аддитивна и равна нулю.** Все новые типы (`InMemoryCorrelatedPlan`, `InMemoryCorrelatedEvaluator`, `OuterReferenceToParameterVisitor`, `InMemoryCorrelatedSubqueryRewriter`, вложенные detector'ы/`CorrelatedTerminal`/`ReferencedQueryIndexFinder`) — `internal sealed`; новые члены контекста (`GetCorrelatedPlan`, `_correlatedPlans`) и команд (`CloneForCorrelatedEvaluation`) — `internal`/`private`. `roslyn members NextORM.Core.InMemoryDataContext`/`NextORM.Core.QueryCommand` не показывают новых `public`/`protected` членов от фичи; в новых файлах нет `public`/`protected` типов. Переименований/удалений публичного API нет.

**CS1591/XML-doc.** Новых публичных членов нет → Приложение A без изменений по количеству. Внутренние типы документированы (`<summary>`/`<remarks>`) и `CS1591` к ним не применяется; `NoWarn` в 7 библиотечных `.csproj` — **0** (`CS1591` снят), `TreatWarningsAsErrors=true`, сборка **0/0**. Внутренние имена следуют паттерну `InMemory<Axis>` (ср. `InMemoryOrdering`/`InMemoryConditionFactory`); `CorrelatedTerminal` — `private`-enum; `Single`/`DefaultOnEmpty` в `InMemoryCorrelatedPlan` совпадают с терминами `QueryCommand`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IMC1 | **P1 (док)** | `docs/advanced/limitations.md:20,51` (+`docs/ru/advanced/limitations.md:20,51`) | **Публичный документ ограничений описывает старое поведение.** Обе строки утверждают «The in-memory provider has no per-row outer-row binding and throws `NotSupportedException`» / «у провайдера in-memory нет построчной привязки внешней строки, и он бросает `NotSupportedException`». Фича это меняет: in-memory теперь вычисляет коррелированные скаляр/`EXISTS`/`IN` по каждой внешней строке (`CorrelatedQueryInMemoryTests` 9/9), а `NotSupportedException` остаётся только для коррелированных `GROUP BY`/`HAVING`, ссылки на внешнюю строку в проекции/`ORDER BY`, глубины >1, async-источника и смеси с явными `SqlFunctions.Parameter`. Формулировка строки 51 («...на любой глубине вложенности у каждого SQL-провайдера; у in-memory нет привязки») также устарела | **Обязательно в том же изменении (маршрут — `nextorm-design-engineer`):** переформулировать обе строки (EN+RU): «in-memory поддерживает коррелированный scalar/`EXISTS`/`IN` на один уровень, повторяя построчную семантику; не поддерживает — коррелированные `GROUP BY`/`HAVING`, внешнюю ссылку в проекции/`ORDER BY`, глубину >1, async-источник». При желании — строка в `guide/06-subqueries.md` (EN+RU). Ссылки на `docs/specs/**` из публичных доков запрещены |
| IMC2 | P2 (трекинг) | `PublicAPI.*.txt` отсутствуют; `Microsoft.CodeAnalysis.PublicApiAnalyzers`/ApiCompat/API-approval не подключены | Фича публичной поверхности не добавляет, но Шаг 5 (заморозка) всё ещё открыт — **продолжение IDML2** | Специальных действий по этой фиче нет; заморозка общего списка остаётся в рамках Шага 5 |

ℹ️ **Наблюдения (фикс не требуется).**

- **DIM/интерфейсные члены не добавлялись** — `IQueryRegistry`, `IDataContext` без изменений; `CloneForCorrelatedEvaluation` — `internal` метод.
- **Внутренние имена консистентны** — `OuterReferenceToParameterVisitor` (visitor), `InMemoryCorrelatedEvaluator` (static builder), `GetCorrelatedPlan` (Get-verb) соответствуют существующим `OuterReference`-визитёрам и `GetPreparedQueryCommand`/`GetEnumerable`.
- **Кодовая сторона** — `code-smells-review.md` (Находки 78–81, наблюдения A–E).

**Проверка (22.09.2026).** Build Release — **0/0**; `roslyn members` по `InMemoryDataContext`/`QueryCommand` — новых `public`/`protected` членов нет; в двух новых файлах нет `public`/`protected` типов; `rg -c NoWarn` по 7 `.csproj` — **0**; `find PublicAPI*.txt` — **0** (Шаг 5); `docs/advanced/limitations.md:20,51` (+RU) — устаревшая формулировка (IMC1).

**Пост-фикс (22.09.2026): ✅ IMC1 устранено.** Обновлены EN+RU: табличная строка `docs/advanced/limitations.md` / `docs/ru/advanced/limitations.md` («...глубже одного уровня» с перечнем оставшихся гейтов), список «Not limitations» и `docs/guide/06-subqueries.md` / `docs/ru/guide/06-subqueries.md` (абзац, таблица провайдеров, раздел Limitations). Публичные доки по-прежнему не ссылаются на `docs/specs/**`.

### Аудит 22.09.2026 — DML `INSERT` (фаза 3): перенос `InsertWithIdentity` → `ReturningIdentity`/`ReturningKey`, uncommitted working tree (P0 — нет; P1 по именам — нет; P2 — 2)

**Область (uncommitted worktree).** Публичная поверхность **аддитивна**; публичных переименований нет. Изменения фазы 3:

- **Удалено** (никогда не коммитилось/не релизилось): `InsertBuilder<TEntity>.InsertWithIdentity<TKey>(Expression<Func<TEntity,TKey>>) -> TKey` и `InsertWithIdentityAsync<TKey>(Expression<Func<TEntity,TKey>>, CancellationToken = default) -> Task<TKey>`.
- **Добавлено** на `InsertBuilder<TEntity>` (**12** публичных членов всего): `Returning() -> InsertReturningBuilder<TEntity,TEntity>`; `Returning<TResult>(Expression<Func<TEntity,TResult>>) -> InsertReturningBuilder<TEntity,TResult>`; `ReturningIdentity<TKey>(Expression<Func<TEntity,TKey>>) -> InsertReturningBuilder<TEntity,TKey>`; `ReturningIdentity<TKey>() -> InsertReturningBuilder<TEntity,TKey>`; `ReturningKey<TKey>() -> InsertReturningBuilder<TEntity,TKey>`.
- **Тип** `NextORM.Core.InsertReturningBuilder<TEntity,TResult>` (`sealed`; ctor `internal`) — подпись не менялась (5 терминалов `Single`/`SingleAsync`/`ToList`/`ToListAsync`/`ToSql`).
- **Новые DIM** `ISqlDialect.SupportsIdentityFunction => SupportsLastInsertId` и `MakeIdentityFunction() => MakeLastInsertId()` (`ISqlDialect.cs:1016,1038`); `SqlDialectBase` — те же два как `virtual`; переопределения: `SqlServerDialect` (`select scope_identity()`), `PostgresDialect` (`select lastval()`); MySQL (`last_insert_id()`), SQLite (`last_insert_rowid()`), MariaDB — наследуют.

**Вне публичной поверхности:** `IMutationExecutor.SupportsGeneratedColumns`/`RenderIdentityFunction`/`ExecuteIdentityFunction` (+async) — `internal`-роль; `EnsureIdentityColumn`/`EnsureIdentityFunctionSupported` — `private` в `DataContext`.

**Removal — не breaking.** `InsertBuilder.cs`/`InsertReturningBuilder.cs` — untracked (`??`), в `HEAD` отсутствуют; `PublicAPI.*.txt` нет (**0**), поверхность не заморожена, ветка alpha → удаление пре-релизного API допустимо (AGENTS/SemVer; «Removal only after deprecation» для released-поверхности не наступает). **IDML3 закрыт именно удалением** — проверка `IsIdentity` осталась в `ResolveIdentityProperty` (`InsertBuilder.cs:272-282`) и вызывается из `ReturningIdentity(selector)`.

**CS1591/XML-doc.** Весь новый публичный API документирован, `CS1591` не подавлен (`rg -c NoWarn` по 7 библиотечным `.csproj` — **0**; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build **0/0** доказывает 100% покрытие нового (и всего) публичного API. Новых публичных **типов** — **0** (только методы + 2 DIM) → Приложение A без изменений.

**Именование — P0/P1 нет.** `ReturningIdentity`/`ReturningKey` — существительное+существительное, согласованы с `Returning` и с SQL-семантикой (identity-колонка vs ключ из метаданных); пара `ReturningIdentity<TKey>(selector)`/`ReturningIdentity<TKey>()` — стандартная «optional selector»-форма; `SupportsIdentityFunction`/`MakeIdentityFunction` — под существующие `Supports*`/`Make*` (`SupportsLastInsertId`/`MakeLastInsertId`); `Async`-суффикс — только у пар с sync-твином (AGENTS.md); `cancellationToken` — последним (CA1068); BCL-конфликтов нет. **God-object по неймингу нет**: имя типа одно и корректное, два внутренних режима наружу не протекают (внутренняя двухрежимность — `code-smells-review.md`, **Находка 83**).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML9 | **P2 (форма/док)** | `InsertBuilder.cs:179-189` (`ReturningIdentity<TKey>()`), `:191-206` (`ReturningKey<TKey>()`) | XML-`<exception cref="NotSupportedException">` висит на builder-методах, которые **никогда не бросают** (throw — в терминалах/`ToSql`; для `ReturningIdentity<TKey>()` назван только ClickHouse, хотя подходит любой диалект с `SupportsIdentityFunction=false`); `<summary>` `ReturningKey<TKey>()` обещает «identity-function fallback», но для не-identity ключа на MySQL/MariaDB исполнение **отклоняет** `NotSupportedException` (`docs/guide/19` это уже описывает); `EnsureKeyType` разрешает nullable-`TKey`, а materializer на это падает (кодовая сторона — `code-smells-review.md` **Находка 82**) | Перенести/уточнить `<exception>` на `Single*`/`ToList*`/`ToSql`; в `<remarks>` `ReturningKey<TKey>()` указать ограничение «non-identity key требует RETURNING/OUTPUT»; задокументировать/ограничить nullable-`TKey` до исправления Находки 82 |
| IDML10 | P2 (трекинг) | `PublicAPI.*.txt` отсутствуют; PublicApiAnalyzers/ApiCompat/API-approval не подключены | Новые подписи не трекаются (Шаг 5 открыт). **Продолжение IDML2/JS1/CHCB2** | При заморозке внести `InsertBuilder<TEntity>` (12 членов, включая 5 новых returning-методов), `ISqlDialect.SupportsIdentityFunction`/`MakeIdentityFunction` + провайдерные override'ы; зафиксировать, что `InsertWithIdentity` в shipped-файл не попадал (пре-релиз) |

ℹ️ **Наблюдения (фикс не требуется).**

- **`ReturningKey` vs `ReturningIdentity`** — два близких имени; различие («ключ из метаданных» vs «сгенерированная identity») неочевидно из названий, но раскрыто в `<summary>` и таблице guide 19 (EN+RU). Оставить.
- **`ReturningIdentity<TKey>()` требует явного type-argument** (нет параметра для вывода) — неизбежно для no-selector формы, задокументировано и покрыто тестами.
- **Публичные доки синхронизированы:** `rg 'InsertWithIdentity' docs/ -g '!**/specs/**'` — **0**; guide 19 (EN+RU), `advanced/api-reference` (EN+RU), `providers/overview` (EN+RU), `linq2db-comparison` (EN+RU) используют `Returning`/`ReturningIdentity`/`ReturningKey`; API-Reference строки `InsertBuilder`/`InsertReturningBuilder` обновлены.
- **Поверхность не заморожена** — `find -name 'PublicAPI*.txt'` — **0**; Шаг 5 открыт (IDML10).

**Закрытие ранее открытых.** IDML3 — ✅ закрыт удалением `InsertWithIdentity` и переносом проверки `IsIdentity` в `ResolveIdentityProperty`. IDML1/IDML6/IDML7/IDML8 — остаются закрытыми. **IDML2 (Шаг 5) — открыт**, теперь отслеживается IDML10. **К исправлению (маршрут — `nextorm-design-engineer`):** IDML9 (P2, док/форма; связана с Находкой 82 — сначала код-фикс, затем формулировки).

**Проверка (22.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0**; перечисление `public`-членов `InsertBuilder.cs` (чтение файла) — **12** (Value×2, Values×2, Insert, InsertAsync, Returning×2, ReturningIdentity×2, ReturningKey, ToSql); `roslyn types InsertBuilder` — `NextORM.Core.InsertBuilder<TEntity>`; `roslyn members NextORM.Core.ISqlDialect` — `SupportsIdentityFunction` (`ISqlDialect.cs:1016`), `MakeIdentityFunction()` (`:1038`); `rg 'InsertWithIdentity' src/ tests/ docs/ -g '!**/specs/**'` — **0**; `rg -c NoWarn` по 7 `.csproj` — **0**; `find -name 'PublicAPI*.txt'` — **0** (IDML10/Шаг 5). Тесты — `code-smells-review.md` (Находки 82–84, наблюдения A–I).

## 4. План работ

Проект в стадии **alpha** — обратная совместимость не сохраняется. Все пункты выполняются **прямыми переименованиями на месте**, с одновременным обновлением кода, тестов, примеров и документации в одном изменении.

### Шаг 1 — инвентаризация использований
- Для каждого пункта #1–#26 собрать вхождения старого имени по всему репозиторию:
  `rg -n "\bOldName\b" src test benchmarks docs`.
- Составить таблицу «старое имя → новое имя → где обновить» (код, тесты, примеры, XML-доки, `docs/**` EN, `docs/ru/**`).
- **Критерий:** для каждого старого имени известны все места правки; ничего не «забыто заранее».

### Шаг 2 — переименование кода и тестов (P0 ✅, безопасная партия P1 ✅; DSL/`Paging`/P2 открыты)
- P0 (#1–#9) переименован на месте — см. §3 и §5.1.
- P1 (#10–#18, #27) — см. §3, §5.3 и §5.4.
- P2 (#19–#28) — см. §3 и §5.4.
- **Критерий (выполнен):** build Debug 0/0; core **153**, sqlite **189**, sqlserver **167**, postgres **151**, mysql **31**, mariadb **7**, clickhouse **47**, integration **853 / Failed 0 / Skipped 23**; старых имён в коде (кроме исторических `<remarks>`) не осталось.

### Шаг 3 — namespace (пункт #26) — выполнено
- `nextorm.core` → `NextORM.Core`, `nextorm.<provider>` → `NextORM.<Provider>`, `nextorm.core.sourcegenerator` → `NextORM.Core.SourceGenerator` — прямая замена `namespace`/`using`/квалифицированных ссылок и reflection-строки `NextORM.Core.Projection` (`InMemoryProjectionFactory`).
- **Критерий (выполнен):** build 0/0; все тесты (core + 6 провайдеров + integration) зелёные.

### Шаг 4 — перегенерация документации (обязательно, в том же изменении)
Правило `AGENTS.md`: переименование публичного типа/метода требует обновления доков.
- Обновить XML-`<summary>`/`<remarks>`: текст, `<see cref>`, `<paramref>`, `<typeparamref>`, ссылки на имена типов.
- Обновить `docs/**` (EN) и `docs/ru/**` (RU): код-примеры, проза, пути к файлам, `advanced/api-reference.md` в обеих ветках.
- Проверить оба дерева: `rg -n "\bOldName\b" docs` — пусто для каждого старого имени.
- Перегенерировать сайт DocFX с чистой сборкой:
  `rm -rf docs/api docs/_site docs/xrefmap.yml && dotnet docfx docs/docfx.json`
  — 0 warnings / 0 errors; страницы API пересозданы под новыми именами (`EntityBuilder.yml`, `JoinedEntityBuilder-1.yml`, …), старые yml удалены.
- Нормализовать все `.md` и `.cs` в CRLF (`perl -pi -e 's/\r?\n/\r\n/g' <file>`).
- **Критерий:** `dotnet docfx` 0/0; grep старых имён по `docs/` пуст; CRLF сохранён.

### Шаг 5 — закрепление (трекинг — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue #53)

Актуализация 18.09.2026: ничего из шага 5 не сделано.
- **Актуализация 21.09.2026 (Фаза 3):** поверхность **сформирована** — `ISqlDialect`/`SqlDialectBase` лишились 41 дублирующего `Supports*`/`Make*`-члена, единственный источник поддержки/рендеринга — capability-объект. Заморозка/трекинг вынесены в [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md) (issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53)); в `PublicAPI.*.txt` вносить capability-интерфейсы/свойства и остаточную не-объектную поверхность (класс B/C), но **не** удалённые пары. Актуальный перечень — в блоке «Фаза 3» выше.
- `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` отсутствуют; `Microsoft.CodeAnalysis.PublicApiAnalyzers` не подключён; `EnablePackageValidation`/ApiCompat и API-approval тест не настроены.
- **Актуализация 20.09.2026:** подписи Batch 3 (именованные окна/XML-методы ClickHouse `multi_if`) дописаны в блок RD2 (метка `# --- продолжение RD2: Batch 3`), статус заморозки не меняется — `PublicAPI.*.txt` по-прежнему нет.
- `CS1591` по-прежнему в `<NoWarn>` **всех 7** библиотечных `.csproj` (`nextorm.core.csproj:9`, `nextorm.postgres.csproj:9`, `nextorm.sqlite.csproj:8`, `nextorm.sqlserver.csproj:17`, `nextorm.mysql.csproj:9`, `nextorm.mariadb.csproj:9`, `nextorm.clickhouse.csproj:9`).
- `CA1716`/`CA1724`/`CA1002`/`CA1051` в `Directory.Build.props` не включены (там только `TreatWarningsAsErrors=true`). Прогон `nextorm.core` при `AnalysisLevel=latest-all` даёт по ним: `CA1716` — 46, `CA1002` — 14, `CA1051` — 46, `CA1724` — 0 (уникальных мест). Правила входят в набор `latest-all` и сборкой по умолчанию не гейтятся.
- Единственное закрепление на сегодня — XML-`<remarks>` со ссылками на этот отчёт в местах находок (P0-1, P0-5, P0-6, P0-9, P1-16, P2-19, P2-25 и др.).
- **Актуализация 21.09.2026 (квотирование идентификаторов).** Новые подписи к заморозке: `ISqlDialect.QuoteIdentifier(string) -> string` (DIM), `SqlDialectBase.QuoteIdentifier` (`virtual`) + `SqliteDialect.QuoteIdentifier` (`override`), `DataContextBuilder.QuoteIdentifiers.get -> bool` и `DataContextBuilder.UseQuotedIdentifiers(bool) -> DataContextBuilder`, `IContextEnvironment.QuoteIdentifiers.get -> bool` (DIM), `DataContext.QuoteIdentifiers.get -> bool`, `EntityBuilder<TEntity>.WithQuotedIdentifiers(bool) -> EntityBuilder<TEntity>`, `EntityBuilder.WithQuotedIdentifiers(bool) -> EntityBuilder`, `QueryCommand.QuoteIdentifiers.get -> bool?`, `QueryCommand<TResult>.WithQuotedIdentifiers(bool) -> QueryCommand<TResult>`, изменённая сигнатура primary-ctor `VisitorOptions` (+`VisitorOptions.QuoteIdentifiers.get -> bool`). DIM-члены аддитивны/источник-совместимы. Findings — §3, «Аудит 21.09.2026 — квотирование идентификаторов»; содержательные — `code-smells-review.md` Находки 52–53 и наблюдения A–E.
- **Актуализация 21.09.2026 (соглашения об именовании, snake_case).** Новые подписи к заморозке: `INamingConvention` (тип) + `TableName(string, bool) -> string` + `ColumnName(string) -> string`; `SnakeCaseNamingConvention` (тип, `sealed`) + `Instance.get` + два метода; `DataContextBuilder.NamingConvention.get -> INamingConvention?` и `UseNamingConvention(INamingConvention?) -> DataContextBuilder`; `IContextEnvironment.NamingConvention.get -> INamingConvention?` (DIM); `DataContext.NamingConvention.get -> INamingConvention?`; `IEntityMetadata.IsTableNameAuto.get -> bool` (абстрактный — см. P2-1); `IPropertyMetadata.IsColumnNameAuto.get -> bool` (абстрактный); `EntityBuilder<TEntity>.WithNamingConvention(INamingConvention?) -> EntityBuilder<TEntity>`; `EntityBuilder.WithNamingConvention(INamingConvention?) -> EntityBuilder`; `QueryCommand.NamingConvention.get -> INamingConvention?`; `QueryCommand<TResult>.WithNamingConvention(INamingConvention?) -> QueryCommand<TResult>`; `VisitorOptions.NamingConvention.get -> INamingConvention?` (init-свойство); изменённые `MemberInfoExtensions.GetPropertyColumnName(MemberInfo, INamingConvention?) -> string` и `FromExpression.FromExpression(string, bool, bool) -> void` (source-совместимы, бинарно нет). Findings — §3, «Аудит 21.09.2026 — соглашения об именовании»; содержательные — `code-smells-review.md` Находки 54–55 и наблюдения A–E.
- **Критерий (не выполнен):** build 0/0; новые нарушения именования валят сборку.

## 5. Что сделано

### 5.1. P0-переименования (выполнено 18.09.2026)

- Инвентаризация: для каждого P0-имени собраны вхождения в `src`/`tests`/`benchmarks`/`docs`, составлена таблица «старое → новое».
- Выполнены переименования #1–#9 (см. §3): `HashCode`→`XxHash32`, `IQueryProvider`→`IQueryRegistry`, слияние `IDataContextExtensions` в `DataContextExtensions`, `EntityP2..P8`→`JoinedEntityBuilder<...>`, `PrepareException`→`QueryPreparationException` (+ общая база `DataContextException` для `BuildSqlCommandException`), `DbQueryCommandExtension`→`RawSqlOverride` (`internal`, свойства), `Projection.tN`→`ItemN`, удаление `BasicHelpers`, `TableAlias`→глагольные `Get*`.
- Reflection-точки, зависевшие от имён, обновлены синхронно: `MemberTranslator` (позиция по числовому суффиксу `ItemN`), `AliasFromProjectionVisitor` (`ItemN` → SQL-алиас `tN` через `DefaultAliasProvider.GetAliasName`), `InMemoryDataContext`/`QueryPlanner` (`GetProperty("Item" + dim)` / `"Item1"`).
- Удалены мёртвые тест-файлы `*DELETE*.cs` (заодно закрывают Находку 7 code-smells-review): они ссылались на старые имена и не исполнялись.
- Обновлены `docs/**` (EN) и `docs/ru/**` (RU): примеры join (`p.ItemN`), аксессоры `TableAlias`, `advanced/api-reference.md` (обе ветки), roadmap- и performance-спеки. `rg` по старым именам в `docs/` (кроме этого реестра) — пусто.
- Проверка: build Debug **0/0**; core **151**, sqlite **180**, sqlserver **166**, postgres **150**, mysql **31**, mariadb **7**, clickhouse **47**, integration **833 / Failed 0 / Skipped 23**.

### 5.2. Исходный аудит

- Проведён аудит, отчёт сохранён в `docs/specs/design/API-NAMING-REVIEW.md`.
- На момент аудита переименования **не выполнялись** — только план и отчёт.
- Добавлены XML-`<summary>`/`<remarks>` (и `<param>`/`<typeparam>` где нужно) для публичных типов и членов, отмеченных в находках P0/P1 и части P2:

  | Файл | Задокументировано |
  |------|-------------------|
  | `Expressions/XxHash32.cs` | `XxHash32` |
  | `Query/IQueryRegistry.cs` | `IQueryRegistry` |
  | `Query/ISourceProvider.cs` | `IColumnsProvider` |
  | `Query/IParamProvider.cs` | `IParamProvider`, `DefaultParamProvider` |
  | `Query/NORM.cs` | `NORM`, `NORM.NORM_SQL` |
  | `Query/RawSqlOverride.cs` | тип + свойства (стал `internal`) |
  | `ParamList.cs` | `ParamList`, `Param` |
  | `BasicHelpders.cs` | `BasicHelpers`, `BasicHelpers.Var` |
  | `Builders/EntityBuilder.cs` | `EntityBuilder<TEntity>`, `EntityBuilder` |
  | `Builders/EntityExtensions.cs` | `EntityExtensions` |
  | `Builders/Joins/JoinedEntityBuilder.cs` | `JoinedEntityBuilder<T1..T8>` (стало) |
  | `Builders/Projection.cs` | `Projection<T1..T8>` |
  | `Builders/TableAlias.cs` | `TableAlias`, `TableColumn` |
  | `Builders/Paging.cs` | `Paging` |
  | `Expressions/Sorting.cs` | `Sorting` |
  | `DataContext/SqlBuilder.cs` | `SqlBuilder` |
  | `DataContext/ValueList.cs` | `Buffer10`, `ValueList`, `Buffer3`, `ValueList3`, `ListExtensions` |
  | `DataContext/{DataContextException,BuildSqlCommandException,QueryPreparationException}.cs` | иерархия исключений |
  | `DataContext/Meta/{IEntityMeta,IPropertyMeta,EntityMetadataBuilder,EntityPropertyBuilder}.cs` | metadata-контракты |
  | `DataContext/Cache/*.cs` | `IPreparedQueryCommand`, `PreparedQueryCommand`, `DbPreparedQueryCommand`, `InMemoryCompiledQuery`, `QueryPlan` |
  | `DI/DataContextOptionsBuilder.cs` | `DbContextBuilder` |
  | `ExpressionExtensions.cs` | `ExpressionExtensions`, `TypeExpressionVisitor`, `TwoTypeExpressionVisitor`, `ReplaceConstantsExpressionVisitor`, `PredicateExpressionVisitor` |
  | `Visitors/*.cs` | `BaseExpressionVisitor`, `AliasFromProjectionVisitor`, `CorrelatedQueryExpressionVisitor`, `MemberExpressionVisitor`, `WhereExpressionVisitor`, `TestSpecialMethodCallVisitor`, `ParamExpressionVisitor2`, `ReplaceParameterVisitor`, `ReplaceConstantVisitor` |
  | `nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs` | `AnonymousClassEqualityComparer` (найденная MISNOMER: генератор назван «EqualityComparer») |

- Проверка: `dotnet build nextorm.sln` — 0 warnings / 0 errors; `dotnet vstest` — 111 passed; `dotnet docfx docs/docfx.json` — 0 warnings / 0 errors.
- **Актуализация 18.09.2026:** задокументировано 113/158 публичных типов (72 %), 213/1092 методов (20 %), 97/347 свойств (28 %); недокументированных типов — 45 (приложение A). **P0, P1 и P2 выполнены** (см. §5.1, §5.3 и §5.4).

### 5.3. P1, безопасная партия (выполнено 18.09.2026)

- Инвентаризация P1-имён по `src`/`tests`/`benchmarks` с разделением на чисто публичные переименования и перевод деталей реализации в `internal`.
- Выполнены #11, #13, #14, #15, #16, #18 (см. таблицу «Было → Стало» в §3):
  - #11: `IEntityMeta`/`IPropertyMeta` → `IEntityMetadata`/`IPropertyMetadata`; файлы и `internal`-реализации `EntityMeta`/`PropertyMeta` → `EntityMetadata`/`PropertyMetadata`.
  - #13: `Param` → `Parameter` (+ новый файл `Parameter.cs`), `ParamList` удалён как мёртвый (0 вхождений), `IParamProvider`/`DefaultParamProvider` → `IParameterProvider`/`DefaultParameterProvider` (файл `Query/IParamProvider.cs` → `IParameterProvider.cs`), `NORM.Param<T>` → `NORM.Parameter<T>`, `ParamExpressionVisitor2` → `internal ParameterBinderVisitor` (файл `Visitors/ParamExpressionVisitor.cs` → `ParameterVisitors.cs`).
  - #14: `TwoTypeExpressionVisitor<T1,T2>` → `TypeExpressionVisitor<T1,T2>` (арность вместо «Two»), `ReplaceParameterVisitor`/`ReplaceConstantVisitor` → `ReplaceParameterExpressionVisitor`/`ReplaceConstantExpressionVisitor`; `ReplaceConstantsExpressionVisitor` оставлен (намеренно отдельный проход полной параметризации).
  - #15/#16/#18: `TestSpecialMethodCallVisitor`, `Buffer3`/`Buffer10`/`ValueList`/`ValueList3`, `SqlBuilder` переведены в `internal`.
- Reflection/`nameof`-точки, зависевшие от имён: `nameof(NORM.Param)` → `nameof(NORM.Parameter)` в `TypedParamVisitor`/`NormSqlTranslator`/`ParameterVisitors`; `node.Method.Name` сравнивается с новым именем.
- Обновлены `docs/**` (EN) и `docs/ru/**` (RU): `NORM.Param` → `NORM.Parameter` (16 файлов), `IEntityMeta` → `IEntityMetadata`, а также профильные спеки (`plan-*`, `design/*`, `performance/*`).
- Проверка: build Debug **0/0**; core **151**, sqlite **180**, sqlserver **166**, postgres **150**, mysql **31**, mariadb **7**, clickhouse **47**, integration **833 / Failed 0 / Skipped 23**.

### 5.4. P1 DSL/контракт и весь P2 (выполнено 18.09.2026)

- #10/#12/#27: `NORM` → `SqlFunctions`, `NORM_SQL` → `CommonFunctions`; провайдерные `PG`/`MS`/`CLK` → `PostgresFunctions`/`SqlServerFunctions`/`ClickHouseFunctions`; аксессоры `SqlFunctions.Sql`/`.Postgres`/`.SqlServer`/`.ClickHouse`; `WindowFunction<T>`/`WindowOrder`/`WindowFrame`/`WindowFrameBound`/`WindowFrameType`/`WindowFrameBoundKind` вынесены в `Query/WindowFunctions.cs`; `*MI`/`SQLExpression` → `internal`. Методы DSL оставлены (SQL-зеркало; см. §3 «Отмечено»).
- #17/#19: `Paging.Limit`/`Offset` — свойства с проверкой неотрицательности; `Sorting.Direction`/`PreparedExpression` — свойства.
- #20: `TableAttribute.cs` → `SqlTableAttribute.cs`; `Query/ISourceProvider.cs` → `Query/IColumnsProvider.cs`.
- #21: `IPayload` удалён.
- #22: унификация на `DataContext`: база `DataContext`, провайдерные `*DataContext`, `DataContextBuilder` (+`CreateDataContext`), `InMemoryDataContext`. EF-типы (`Microsoft.EntityFrameworkCore.DbContext`, `EFDataContext`, `EFInMemoryDataContext`) не тронуты.
- #24/#25/#28: `DefaultAliasProvider`/`DefaultParameterProvider`/`DefaultColumnsProvider` в `Query/`; `AnonymousClassEqualityComparer` → `internal AnonymousClassGenerator`; `{Provider}DataContextOptionsBuilderExtensions`.
- #26: namespaces → `NextORM.Core` / `NextORM.<Provider>` / `NextORM.Core.SourceGenerator` (включая reflection-строку `NextORM.Core.Projection`).
- Обновлены `docs/**` (EN) и `docs/ru/**` (RU), `advanced/api-reference.md` в обеих ветках, этот реестр; CRLF сохранён.
- Дополнительно (god-class work, 18.09.2026): новые `internal`-типы `SqlBuildContext`, `SqlSourceRenderer`, `StringFunctionTranslator`, `MathFunctionTranslator`, `DateTimeFunctionTranslator`, `TableAliasAccessors`; новый **публичный** `NextORM.Core.EntityBuilderExtensions` — туда переехали терминальные операторы `EntityBuilder<TEntity>` (`Any`/`ToList`/`First`/`Single`/`Last`/`Count`/агрегаты/`To*`/`Prepare`/`AnyCommand`/`*Or*Command`). Вызовы не изменились (extension-методы), но потребителю нужен `using NextORM.Core;` (он и так необходим для `From<T>()`/`SqlFunctions`). Добавлены регресс-тесты `BackendMixingTests` (F3).
- Дополнительно (параметры, 18.09.2026): длинные списки параметров ≥6 разобраны параметр-объектами. Новые **публичные** типы: `QueryDefinition` (ctor-ы `QueryCommand`/`QueryCommand<TResult>` и `DataContextExtensions.CreateCommand<T>` теперь принимают его), `PrepareFromSqlMode` (флаги вместо пары `nonStreamUsing`/`storeInCache` в `PrepareFromSql`), `PreparedCommandOptions` (ctor `DbPreparedQueryCommand<TResult>`). Удалены длинные публичные overload'ы: 12-параметровый ctor `BaseExpressionVisitor`, 10-параметровый primary ctor `WhereExpressionVisitor`, публичные ctors `QueryCommand`/`QueryCommand<TResult>` (кроме `(IDataContext?, QueryDefinition)`), 4 перегрузки `CreateCommand`, а также мёртвый `InMemoryDataContext.PrepareFromSql` (in-memory raw SQL теперь бросает `NotSupportedException`). Внутренние новые типы: `SqlBuildContext`, `FromRenderOptions`, `AggregateTypeInfo`, `LoggingOptions`, `ProviderHooks`, `ConnectionHooks`.
- Проверка: build Debug **0/0**; core **153**, sqlite **195**, sqlserver **167**, postgres **151**, mysql **31**, mariadb **7**, clickhouse **47**; integration — провайдерные тесты зелёные, 22 падения только в `Correlated*`/`SubQuerySelectOrderSingle` (feature-работа над scalar-subquery cardinality, не связана с рефакторингом параметров); DocFX 0 errors.
- Бенчмарки (A/B против `d21c473`, `SqliteBenchmarkJoin`/`Where`): `Prepared`-пути по аллокациям идентичны (13.01/92.42/107.64 KB); в `Cached`-путях +~555 B/запрос (Join 94.05→99.6 KB, Where 535.8→591.3 KB) — это код параллельно влившегося рефакторинга (correlated-subquery `OuterReferences` в `QueryPlanEqualityComparer`/`QueryCommand.Clone`, выкачка `InMemory*`), а не переименований P0/P1/P2: откат `Paging`/`Sorting` к полям аллокации не изменил. `Paging`/`Sorting`, DSL- и namespace-переименования аллопрофиль не меняют.


## Приложение A — публичные типы без XML-документации (исторический список 18.09.2026; закрыто 22.09.2026)

**Закрыто 22.09.2026:** все перечисленные ниже типы задокументированы (см. обновление в разделе 2); список сохранён как исторический.

Полный машинный список (FQN, включая вложенные публичные типы), получен отражением по 7 собранным библиотечным сборкам (HEAD `d21c473`). Использовать как бэклог Шага 4. Generic-арность записана в метаданных (`` `1 `` = один generic-параметр). Прошлый список из 109 позиций устарел — после массового добавления `<summary>`/`<remarks>` он сократился до 45.

```
nextorm.clickhouse.ClickHouseDbContext
nextorm.clickhouse.DataContextOptionsBuilderExtensions
nextorm.core.DbContext
nextorm.core.DefaultAliasProvider
nextorm.core.DefaultColumnsProvider
nextorm.core.ExpressionCache`1
nextorm.core.ExpressionKey
nextorm.core.ExpressionPlanEqualityComparer
nextorm.core.FromExpression
nextorm.core.FromExpressionPlanEqualityComparer
nextorm.core.IAliasProvider
nextorm.core.IEqualityComparerExtensions
nextorm.core.InMemoryCommandBuilderExtensions
nextorm.core.InMemoryContext
nextorm.core.InMemoryEnumerator`2
nextorm.core.InMemoryEnumeratorAdapter`2
nextorm.core.InMemoryPreparedQueryCommand`1
nextorm.core.IPayload
nextorm.core.IValueEqualityComparer`1
nextorm.core.JoinExpression
nextorm.core.JoinExpressionPlanEqualityComparer
nextorm.core.JoinType
nextorm.core.MemberInfoExtensions
nextorm.core.OrderDirection
nextorm.core.OuterRefMarker`1
nextorm.core.QueryCommand
nextorm.core.QueryCommand`1
nextorm.core.QueryCommandExtensions
nextorm.core.QueryPlanEqualityComparer
nextorm.core.ResultSetEnumerator`1
nextorm.core.SelectExpression
nextorm.core.SelectExpressionPlanEqualityComparer
nextorm.core.SortingExpressionPlanEqualityComparer
nextorm.core.SqlTableAttribute
nextorm.core.TypeExtensions
nextorm.mariadb.DataContextOptionsBuilderExtensions
nextorm.mariadb.MariaDbContext
nextorm.mysql.DataContextOptionsBuilderExtensions
nextorm.mysql.MySqlDbContext
nextorm.postgres.DataContextOptionsBuilderExtensions
nextorm.postgres.PostgresDbContext
nextorm.sqlite.DataContextOptionsBuilderExtensions
nextorm.sqlite.SqliteDbContext
nextorm.sqlserver.DataContextOptionsBuilderExtensions
nextorm.sqlserver.SqlServerDbContext
```
nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs:6   AnonymousClassEqualityComparer
nextorm.core/BuildSqlCommandException.cs:6                       BuildSqlCommandException
nextorm.core/Builders/EntityBuilder.cs:16                        EntityBuilder<TEntity>
nextorm.core/Builders/EntityBuilder.cs:531                       EntityBuilder
nextorm.core/Builders/EntityExtensions.cs:3                      EntityExtensions
nextorm.core/Builders/InMemoryCommandBuilder.cs:31               InMemoryCommandBuilderExtensions
nextorm.core/Builders/Joins/JoinedEntityBuilder.cs:13,...         JoinedEntityBuilder<T1..T8>
nextorm.core/Builders/Paging.cs:2                                Paging
nextorm.core/Builders/Projection.cs:28..162                      Projection<T1..T8>
nextorm.core/Builders/TableAlias.cs:4,37                         TableAlias, TableColumn
nextorm.core/DI/DataContextOptionsBuilder.cs:5                   DbContextBuilder
nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:7       DbPreparedQueryCommand<T>
nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs:5        IPreparedQueryCommand<T>
nextorm.core/DataContext/Cache/InMemoryCompiledQuery.cs:4        InMemoryCompiledQuery<,>
nextorm.core/DataContext/Cache/PreparedQueryCommand.cs:2         PreparedQueryCommand<,>
nextorm.core/DataContext/Cache/QueryPlan.cs:5                    QueryPlan
nextorm.core/DataContext/DataContextException.cs:6               DataContextException
nextorm.core/DataContext/DbContext.cs:13                         DbContext
nextorm.core/DataContext/DefaultAliasProvider.cs:7               DefaultAliasProvider
nextorm.core/DataContext/InMemoryDataContext.cs:9                InMemoryContext
nextorm.core/DataContext/InMemoryEnumerator.cs:7                 InMemoryEnumerator<,>
nextorm.core/DataContext/InMemoryEnumeratorAdapter.cs:4          InMemoryEnumeratorAdapter<,>
nextorm.core/DataContext/InMemoryPreparedQueryCommand.cs:16      InMemoryPreparedQueryCommand<>
nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:7         EntityMetadataBuilder<T>
nextorm.core/DataContext/Meta/EntityPropertyBuilder.cs:6         EntityPropertyBuilder<T>
nextorm.core/DataContext/Meta/IEntityMeta.cs:3                   IEntityMeta
nextorm.core/DataContext/Meta/IPropertyMeta.cs:5                 IPropertyMeta
nextorm.core/DataContext/QueryPreparationException.cs:6                   QueryPreparationException
nextorm.core/DataContext/ResultSetEnumerator.cs:10               ResultSetEnumerator<>
nextorm.core/DataContext/SqlBuilder.cs:9                         SqlBuilder
nextorm.core/DataContext/ValueList.cs:7,11,74,78,141             Buffer10, ValueList, Buffer3, ValueList3, ListExtensions
nextorm.core/ExpressionCache.cs:5,10                             ExpressionCache<T>, ExpressionKey
nextorm.core/ExpressionExtensions.cs:7,31,51,81,148              ExpressionExtensions, TypeExpressionVisitor, TwoTypeExpressionVisitor, ReplaceConstantsExpressionVisitor, PredicateExpressionVisitor
nextorm.core/Expressions/FromExpression.cs:3                     FromExpression
nextorm.core/Expressions/FromExpressionPlanEqualityComparer.cs:4 FromExpressionPlanEqualityComparer
nextorm.core/Expressions/XxHash32.cs:12                          XxHash32
nextorm.core/Expressions/IEqualityComparerExtensions.cs:5        IEqualityComparerExtensions
nextorm.core/Expressions/JoinExpression.cs:5,15                  JoinType, JoinExpression
nextorm.core/Expressions/JoinExpressionPlanEqualityComparer.cs:5 JoinExpressionPlanEqualityComparer
nextorm.core/Expressions/OrderDirection.cs:3                     OrderDirection
nextorm.core/Expressions/SelectExpression.cs:8                   SelectExpression
nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs:4 SelectExpressionPlanEqualityComparer
nextorm.core/Expressions/Sorting.cs:3                            Sorting
nextorm.core/Expressions/SortingExpressionPlanEqualityComparer.cs:3,59 SortingExpressionPlanEqualityComparer, IValueEqualityComparer<T>
nextorm.core/MemberInfoExtensions.cs:5                           MemberInfoExtensions
nextorm.core/OuterRefMarker.cs:5                                 OuterRefMarker<T>
nextorm.core/ParamList.cs:3,8                                    ParamList, Param
nextorm.core/Payload/IPayload.cs:3                               IPayload
nextorm.core/Query/DefaultColumnsProvider.cs:7                   DefaultColumnsProvider
nextorm.core/Query/ExpressionPlanEqualityComparer.cs:11          ExpressionPlanEqualityComparer
nextorm.core/Query/IAliasProvider.cs:5                           IAliasProvider
nextorm.core/Query/IParamProvider.cs:5,9                         IParamProvider, DefaultParamProvider
nextorm.core/Query/IQueryRegistry.cs:5                           IQueryRegistry
nextorm.core/Query/ISourceProvider.cs:7                          IColumnsProvider
nextorm.core/Query/NORM.cs:7,162                                 NORM, NORM_SQL (+ nested Window*)
nextorm.core/Query/QueryCommand.*.cs                             QueryCommand (partials), QueryCommand<TResult>
nextorm.core/Query/QueryCommandExtensions.cs:5                   QueryCommandExtensions
nextorm.core/Query/QueryPlanEqualityComparer.cs:6                QueryPlanEqualityComparer
nextorm.core/SqlFunctionAttribute.cs:14                          SqlFunctionAttribute
nextorm.core/SqlTableFunctionAttribute.cs:17                     SqlTableFunctionAttribute
nextorm.core/TableAttribute.cs:3                                 SqlTableAttribute
nextorm.core/TypeExtensions.cs:6                                 TypeExtensions
nextorm.core/Visitors/*.cs                                       AliasFromProjectionVisitor, BaseExpressionVisitor, CorrelatedQueryExpressionVisitor, MemberExpressionVisitor, TestSpecialMethodCallVisitor, ParamExpressionVisitor2, ReplaceParameterVisitor, ReplaceConstantVisitor, WhereExpressionVisitor
nextorm.postgres/DI/DataContextOptionsBuilderExtensions.cs:6     DataContextOptionsBuilderExtensions
nextorm.postgres/PostgresDbContext.cs:7                          PostgresDbContext
nextorm.postgres/PostgresDialect.cs                              PostgresDialect
nextorm.sqlite/...                                               SqliteDbContext, SqliteDialect, DataContextOptionsBuilderExtensions
nextorm.sqlserver/...                                            SqlServerDbContext, SqlServerDialect, DataContextOptionsBuilderExtensions
```

### Аудит 22.09.2026 — регистр ключевых слов (`KeywordCase`) и index-хинты, uncommitted working tree (P0 — нет; P1 — 0; P2 — 5: 2 трекинга + 3 форма/охват)

Новые публичные члены (внесены осознанно, XML-doc на каждом):

- `NextORM.Core.KeywordCase` (enum), `NextORM.Core.IndexHintKind` (enum).
- `NextORM.Core.SqlKeywords` (public static; `Of(KeywordCase, string)`) — хелпер для провайдерных
  capability-рендереров, которые живут в отдельной сборке.
- `NextORM.Core.IIndexHintRenderer` (`MergesWithTableHints`, `RenderIndexHint`).
- `ISqlDialect.IndexHints` (nullable DIM, default `null`); `SqlDialectBase.IndexHints` (virtual, `null`).
- `ISqlDialect.Make{Page,Top,With,JoinKeyword,TableAlias,ColumnAlias}(... KeywordCase keywordCase = Lower)`
  и одноимённые `SqlDialectBase`/провайдерные override — **source-breaking** для внешних реализаторов
  `ISqlDialect` (alpha допускает, ср. [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md)).
- `DataContextBuilder.KeywordCase` / `UseKeywordCase` / `UseUppercaseKeywords`; `IContextEnvironment.KeywordCase`
  (DIM `Lower`); `DataContext.KeywordCase`.
- `EntityBuilder<TEntity>.WithKeywordCase` / `WithUppercaseKeywords` / `WithIndex(params)` /
  `WithIndex(IndexHintKind, params)` / `WithoutIndex`.
- `QueryCommand.KeywordCase` / `IndexHints` / `IndexHintKind`; `QueryCommand<TResult>.WithKeywordCase`.

**Findings (P2 — трекинг/форма; не блокируют alpha, решаются до заморозки).** Остальные диалектные ключевые слова (`for json`/`for xml`, `pivot`/`unpivot`, `tablesample`, `limit by`, `array join`, `for update`/`for share`, `distinct on`, группировочные модификаторы, temporal/apply/settings/final/sample, `returning`/`output`) — **задокументированное отложенное состояние (фаза 3)** в `docs/advanced/limitations.md:24` (EN+RU) и `docs/advanced/limitations.md:177-179`; в этом реестре повторно не открываются (содержательная сторона — `code-smells-review.md`, Находка 85, где отмечены только два места вне этого перечня).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| KC1 | P2 (трекинг/source-break) | `ISqlDialect.cs:88,618,654,656,888,982`; `SqlDialectBase.cs:269,303,326,348,351,677` | Шесть `Make*`-методов получили необязательный `KeywordCase keywordCase = Lower`. Для внешних реализаторов `ISqlDialect` это **source-breaking** (override обязан повторить параметр, хотя вызовы совместимы). Переименований нет; класс pre-1.0 alpha, ср. CHQA1 | Зафиксировать перечень в `PublicAPI.Shipped.txt` при подключении `PublicApiAnalyzers` (Шаг 5); отметить изменённую сигнатуру интерфейса в release-notes |
| KC2 | P2 (форма/заморозка) | `SqlKeywords.cs:12-18`; `ISqlDialect.cs:562` | `SqlKeywords` (`public static`, `Of(KeywordCase,string)` + приватный `ConcurrentDictionary`) — низкоуровневый хелпер с контрактом «передай ровно нижнерегистровый keyword-токен; идентификаторы/литералы/функции — никогда», не выразимым типами. Публичный вызов с произвольной строкой даёт неограниченный рост кэша и неверный регистр идентификатора; RFC (`docs/advanced/limitations.md:152-155,237`) оставлял выбор открытым. `NoWarn`/`IVT` в репозитории нет (`BaseExpressionVisitor.cs:76`) | Предпочтительно `internal static SqlKeywords` + `[InternalsVisibleTo]` на 6 провайдеров; альтернатива — оставить `public`, пометить `<remarks>` «low-level, dialect renderers only» и ограничить вход. Окончательно решить при заморозке |
| KC3 | P2 (форма/эргономика) | `DataContextBuilder.cs:126`; `EntityBuilder.cs:1529,1781`; `QueryCommand.TResult.cs:616` | У трёх методов `KeywordCase keywordCase = KeywordCase.Upper`: **дефолт параметра противоречит и enum-дефолту (`Lower`), и контекстному дефолту**. `UseKeywordCase()`/`WithKeywordCase()` без аргумента неожиданно **включает** верхний регистр (замаскированный `…UppercaseKeywords()`); у `QueryCommand<TResult>` парного `WithUppercaseKeywords` нет. RFC (`docs/advanced/limitations.md:124-127`) это предписывал, но форма расходится с правилом «дефолт параметра = дефолт настройки» | Сделать `KeywordCase` обязательным (`UseKeywordCase(KeywordCase)`), `UseUppercaseKeywords()`/`WithUppercaseKeywords()` — явный шорткат; или переименовать методы. **Поведение не менять без решения владельца** |
| KC4 | P2 (охват/API) | `SqlBuilder.cs:153`; `SqlServerDialect.cs:108`; `DialectCapabilities.cs:282-293` | Два места вне задокументированной фазы 3 остаются нижнерегистровыми в `Upper` (`code-smells-review.md`, Находка 85). Закрытие фазы 3 потребует **таких же source-breaking** `KeywordCase`-параметров у `IArrayJoinRenderer`, `ILimitByRenderer`, `ILockRenderer`, `ITableSampleMethods`, `IPivotRenderer`, `ISqlDialect.MakeTableHints`/`MakeForJson`/`MakeForXml` и (для `ROW`/`tuple`) у `ITupleRenderer` | Включить весь перечень renderer-интерфейсов в `docs/advanced/limitations.md` (фаза 3) и в план `PublicAPI.Shipped.txt`, чтобы заморозка не прошла дважды; `ITupleRenderer` — решить, входит ли `ROW` в политику (TUP2) |
| KC5 | P2 (форма/корректность) | `QueryCommand.TResult.cs:616-622` | Новый `WithKeywordCase` — clone+reset, теряющий `_from` для CTE/derived источника (тот же баг, что у `Hint` до Находки 56; кодовая сторона — `code-smells-review.md`, Находка 87). Для реестра значимо как контракт нового публичного члена | Синхронизировать с фиксом Находки 87 (сохранять `_from` до `ResetPreparation`, как в `Hint` `:573-580`) |

### Аудит 22.09.2026 — row-значения (`ITupleRenderer`) на PostgreSQL, uncommitted working tree (P0 — нет; P1 — 0; P2 — 3: 1 трекинг + 2 форма/мёртвый член)

- `NextORM.Core.ITupleRenderer` (`RenderConstructor`, `RenderElement`); `ISqlDialect.Tuple`
  (nullable DIM, default `null`) и `SqlDialectBase.Tuple` (virtual, `null`).
- `ISqlDialect.SupportsTupleFunctions` переведён на `Tuple is not null` (DIM); override
  `ClickHouseDialect.SupportsTupleFunctions` удалён, вместо него `ClickHouseDialect.Tuple`.
- `TypeFacts.IsValueTupleType` / `IsTupleLike` — internal, используются только транслятором/`VisitNew`.
- Поведенческое изменение: `.ItemN` inline-конструктора (`Tuple.Create(a,b)`,
  `new Tuple<...>(...)`, `new ValueTuple<...>(...)`) теперь сворачивается в сам аргумент на всех
  диалектах, включая ClickHouse (раньше `tupleElement(tuple(...), n)`).

P2-трекинг: `ITupleRenderer` / `ISqlDialect.Tuple` внести в `PublicAPI.Shipped.txt` при подключении
`PublicApiAnalyzers` (см. `todo_public_api_freeze.md`).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| TUP1 | P2 (мёртвый член) | `ISqlDialect.cs:179`; `SqlDialectBase.cs:60`; `TupleSqlTranslator.cs:75,105` | `SupportsTupleFunctions` переведён на `Tuple is not null`, но **никем не читается**: транслятор гейтит по `visitor.Dialect.Tuple` напрямую (`rg` по `src/`/`tests/` — только определения + доки). Публичный член с нулевыми потребителями (кодовая сторона — `code-smells-review.md`, Находка 88). Ранее (CHTUP1) это был осмысленный гейт | Либо удалить до заморозки (alpha; обновить xref в `docs/providers/clickhouse.md` +RU, `capability-matrix.md`, `advanced/api-reference.md` +RU), либо сделать `TupleSqlTranslator` использовать флаг как единственный гейт и убрать прямые `Dialect.Tuple`-проверки |
| TUP2 | P2 (форма/консистентность) | `DialectCapabilities.cs:282-293`; `PostgresDialect.cs:397-401`; `ClickHouseDialect.cs:763-767` | `ITupleRenderer` не принимает `KeywordCase`: `PostgresTupleRenderer` эмитит `ROW(...)` (верхний) даже в `Lower`, `ClickHouseTupleRenderer` — `tuple(...)` (нижний) даже в `Upper`. Контракт `KeywordCase` исключает имена функций/типы, но `ROW` — ключевое слово; новая поверхность сразу даёт несогласованность (в `Lower` PG-вывод не «весь нижний»). SQL-gen-тесты фиксируют `ROW(id, somestring)` и `tuple(...)` | Либо добавить `KeywordCase` в `ITupleRenderer` (`RenderConstructor`/`RenderElement`), либо задокументировать в `KeywordCase`/guide, что конструкторы `ROW`/`tuple` — вне политики (тогда снять `ROW` из «ключевых слов» в формулировках) |

**Пост-фикс 23.09.2026 (фаза 3 закрыта).** KC5 закрыт (сохранение `_from` в `WithKeywordCase`/`WithQuotedIdentifiers`/`WithNamingConvention`); TUP1 закрыт (гейт `TupleSqlTranslator` по `SupportsTupleFunctions`); KC4 закрыт полностью — фаза 3 реализована: `KeywordCase`-параметр добавлен в `IArrayJoinRenderer`, `ILimitByRenderer`, `ILockRenderer`, `ITableSampleMethods`, `IPivotRenderer`, `ISqlDialect.MakeForJson`/`MakeForXml`/`MakeTemporalTable`/`MakeFinal`/`MakeSample`/`MakeSettings`/`MakeGrouping`/`MakeGroupingSets`/`MakeGroupByTotals`/`MakeWithinGroup`/`MakeLikeEscape`/`MakeCase`/`MakeApply`/`MakeReturning`/`MakeOutput`/`MakeLastInsertId`/`MakeIdentityFunction`/`MakeMaxRecursion`/`RenderQueryHints`, а ARRAY JOIN `as` в `SqlBuilder` переведён на `Kw`; TUP2 решён вариантом «конструкторы `ROW`/`tuple` — вне политики `KeywordCase`» (эмитятся в каноничном для провайдера регистре, как имена функций) — зафиксировано в `docs/advanced/limitations.md` (EN+RU). KC1 (source-break `KeywordCase`-параметров `Make*`/`Render`-интерфейсов — теперь весь перечень), KC2 (`SqlKeywords` public) и KC3 (дефолт `= Upper`) остаются открытыми P2-трекингами к заморозке.

### Верификация 23.09.2026 — фаза 3 `KeywordCase`: публичная поверхность (P0 — нет; P1 — 0; P2 — 2: KC1 расширен + KC6 новый)

**Типы/члены.** Фаза 3 **не добавляет** публичных типов и членов — только необязательный параметр `KeywordCase keywordCase = KeywordCase.Lower` к **существующим** членам; переименований нет. Приложение A (кол-во публичных типов) без изменений. Новые renderer-реализации (`MySqlIndexHintRenderer`, `SqliteIndexHintRenderer`, `SqlServerIndexHintRenderer`, `PostgresTupleRenderer`, `ClickHouseTupleRenderer`) — `internal`. `SqlDialectBase.Kw` (`SqlDialectBase.cs:272`) — `protected static` (уже часть наследуемой поверхности типа), `BaseExpressionVisitor.Kw` (`BaseExpressionVisitor.cs:96`) и `SqlBuilder.Kw` (`SqlBuilder.cs:534`) — `internal`/`private`, вне публичной поверхности.

**Полный перечень `KeywordCase`-членов (к заморозке Шага 5).** `ISqlDialect` — **26** методов: `MakeApply`, `MakeCase`, `MakeColumnAlias`, `MakeFinal`, `MakeForJson`, `MakeForXml`, `MakeGroupByTotals`, `MakeGrouping`, `MakeGroupingSets`, `MakeIdentityFunction`, `MakeJoinKeyword`, `MakeLastInsertId`, `MakeLikeEscape`, `MakeMaxRecursion`, `MakeOutput`, `MakePage`, `MakeReturning`, `MakeSample`, `MakeSettings`, `MakeTableAlias`, `MakeTableHints`, `MakeTemporalTable`, `MakeTop`, `MakeWithinGroup`, `MakeWith`, `RenderQueryHints`; плюс **7** методов capability-рендереров: `IArrayJoinRenderer.Render`, `ILimitByRenderer.Render`, `ILockRenderer.Render`, `ITableSampleMethods.Render`, `IPivotRenderer.RenderPivot`/`RenderUnpivot`, `IIndexHintRenderer.RenderIndexHint`. Из них фаза 1/2 дала 6 (`MakePage`/`MakeTop`/`MakeWith`/`MakeJoinKeyword`/`MakeTableAlias`/`MakeColumnAlias`), фаза 3 — остальные 20 + renderer-набор. `ITupleRenderer` (`RenderConstructor`/`RenderElement`) `KeywordCase` **не получает** — TUP2 решён как «вне политики».

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| KC1 | **P2 (трекинг/source-break) — РАСШИРЕН** | `ISqlDialect.cs` (26 методов, включая `:53` `MakeApply`, `:118/123` `MakeGrouping`/`Sets`, `:488` `MakeGroupByTotals`, `:547` `RenderQueryHints`, `:554` `MakeTableHints`, `:568/574` `MakeForJson`/`Xml`, `:624` `MakeMaxRecursion`, `:678` `MakeCase`, `:737` `MakeLikeEscape`, `:832` `MakeWithinGroup`, `:914/920/944/972` `MakeFinal`/`Sample`/`TemporalTable`/`Settings`, `:1038-1054` `MakeReturning`/`Output`/`LastInsertId`/`IdentityFunction`); `DialectCapabilities.cs:71,168,182,197,204,215,255,279` | Необязательные `KeywordCase`-параметры на 26 интерфейсных методах + 7 renderer-методах. Для внешних реализаторов `ISqlDialect`/renderer-интерфейсов это **source-breaking** (override обязан повторить параметр, хотя вызовы совместимы). Переименований нет; pre-1.0 alpha, ср. CHQA1/JS2 | Внести **полный** перечень в `PublicAPI.Shipped.txt` при подключении `PublicApiAnalyzers` (Шаг 5) и отметить изменённые сигнатуры интерфейсов в release-notes. Оценка объёма — см. таблицу выше (26 + 7) |
| KC4 | P2 (охват/API) — **ЗАКРЫТ** | `SqlBuilder.cs:153`; `ISqlDialect.cs:554`; `SqlServerDialect.cs:108`; `DialectCapabilities.cs:282-293` | Оба места вне фазы 3 закрыты: ARRAY JOIN `as` → `Kw`; `MakeTableHints`/`MakeForJson`/`MakeForXml` получили параметр; capability-клозы (`array join`, `limit by`, `tablesample`, `pivot`/`unpivot`, `lock`, `distinct on`) — тоже. `ITupleRenderer` решён как вне политики (TUP2) | Закрыто; при заморозке внести сигнатуры из KC1 (содержательная проверка — `code-smells-review.md`, верификация 23.09.2026) |
| KC5 | P2 (форма/корректность) — **ЗАКРЫТ** | `QueryCommand.TResult.cs:620-626` | `WithKeywordCase` (и заодно `WithQuotedIdentifiers`/`WithNamingConvention`) теперь сохраняют `_from` до `ResetPreparation` (`:592-594,608-610,622-625`), как `Hint`. Тест `KeywordCase_CommandOverride_ShouldPreserveCteSource` (sqlite) проверяет `FROM recent` | Закрыто |
| TUP1 | P2 (мёртвый член) — **ЗАКРЫТ** | `ISqlDialect.cs:179`; `SqlDialectBase.cs:60`; `TupleSqlTranslator.cs:75,105` | `SupportsTupleFunctions` (`Tuple is not null`) теперь читается транслятором в паре с `Dialect.Tuple` — член перестал быть декоративным | Закрыто |
| TUP2 | P2 (форма/консистентность) — **РЕШЁН (док)** | `DialectCapabilities.cs:282-293`; `PostgresDialect.cs:397`; `ClickHouseDialect.cs:763`; `docs/advanced/limitations.md` (+RU) | `ITupleRenderer` без `KeywordCase`; конструкторы `ROW(...)`/`tuple(...)` эмитятся в каноничном для провайдера регистре. Вариант «вне политики `KeywordCase`» задокументирован (EN+RU), тесты `TupleCreate_ShouldRenderRowConstructor`/`TupleElementAccess_OnInlineConstructor_ShouldFoldToArgument` фиксируют поведение | Закрыто как задокументированное исключение; при заморозке внести `ITupleRenderer` без параметра `KeywordCase` |
| KC6 | **P2 (форма/док) — НОВЫЙ** | `ISqlDialect.cs:980` (единственный `<param name="keywordCase">` — у `MakeTop`); `DialectCapabilities.cs:70,167,181,196,203,214,254` (только `<paramref>`-фраза; `:279` `IIndexHintRenderer` — ни того, ни другого); `SqlDialectBase.cs:204-707` | Новые `KeywordCase`-параметры не описаны на уровне `<param>` у 25 из 26 `ISqlDialect`-методов (и у `SqlDialectBase`-двойников). Build/DocFX чисты (CS1591/CS1573 не срабатывают: у этих членов и раньше не было `<param>`-тегов), поэтому это только doc-полнота, а не гейт | Добавить единообразную строку `<param name="keywordCase">` (или `<paramref>`-фразу, как у renderer-интерфейсов) в рамках заморозки KC1. Кодовая сторона — `code-smells-review.md`, верификация 23.09.2026, ℹ️ D |

**Закрытие/статус.** KC4/KC5/TUP1 закрыты, TUP2 решён докой, **KC1 расширен** до полного перечня (26+7), **KC6** открыт; **KC2** (`SqlKeywords` public + неограниченный кэш) и **KC3** (дефолт `= Upper`) остаются открытыми P2 к заморозке без изменений. Новых публичных членов к регистрации нет — при заморозке добавляются только изменённые сигнатуры из KC1. Шаг 5 по-прежнему открыт (`find PublicAPI*.txt` — **0**).

**Публичные доки.** `docs/advanced/limitations.md` (EN+RU) — строка про `KeywordCase` переписана на полное покрытие с оговоркой про `ROW`/`tuple`; `docs/guide/17-query-hints.md` (+RU) — xref `MakeTableHints`/`RenderQueryHints` обновлены на новую арность (`…,NextORM.Core.KeywordCase`); `docs/providers/overview.md` (+RU) — xref `MakePage` обновлён; `docs/specs/roadmap/todo_sql_keyword_case.md` удалён (не ссылается из публичных доков). `dotnet docfx docs/docfx.json` — 0/0. P1-доковых находок фазы 3 нет.

**Проверка (23.09.2026).** `roslyn members NextORM.Core.ISqlDialect`/`SqlDialectBase` — перечень арностей `KeywordCase`-методов совпадает с KC1; `roslyn`/чтение — новых публичных типов нет; `rg '<param name="keywordCase">'` — только `ISqlDialect.cs:980`; `rg -c NoWarn` по 7 `.csproj` — 0; `find -name 'PublicAPI*.txt'` — 0 (Шаг 5); build Release 0/0; `docfx` 0/0.

### Аудит 23.09.2026 — DML `INSERT` (фаза 4): batch values surface, uncommitted working tree (P0 — нет; P1 — 2 доковых/контрактных; P2 — 2)

**Область (uncommitted worktree).** Публичная поверхность **аддитивна**, переименований нет. Новые подписи (к заморозке Шага 5):

- `InsertBuilder<TEntity>.Value<TScalar>(TScalar) -> InsertBuilder<TEntity>`;
- `InsertBuilder<TEntity>.Values<TScalar>(IEnumerable<TScalar>) -> InsertBuilder<TEntity>`;
- `InsertBuilder<TEntity>.Values<TSource,TResult>(IEnumerable<TSource>, Expression<Func<TSource,TResult>>) -> InsertBuilder<TEntity>`;
- `InsertBuilder<TEntity>.Values<TValue>(Expression<Func<TEntity,TValue>>, IEnumerable<TValue>) -> InsertBuilder<TEntity>`.

На `Values(TEntity)`/`Values(IEnumerable<TEntity>)` добавлен `[OverloadResolutionPriority(1)]` (`InsertBuilder.cs:112,126`). Класс `InsertBuilder<TEntity>` уже в списке заморозки (продолжение IDML2/IDML10); новых публичных **типов** нет — Приложение A без изменений.

**Именование — P0/P1 по именам нет.** `Value`/`Values` продолжают RFC/linq2db-линию; BCL-конфликтов нет; `Async`-суффиксы не затронуты. Вопросы — перегрузочная ширина `Value`/`Values` (IDML12) и два доковых/контрактных пробела (IDML13/IDML14).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML11 | P2 (трекинг) | `src/nextorm.core/Builders/InsertBuilder.cs:169,201,220,249`; `PublicAPI.*.txt` отсутствуют | 4 новые подписи не трекаются (`PublicApiAnalyzers` не подключён, Шаг 5 открыт). **Продолжение IDML2/IDML10, не новая проблема** | При заморозке внести 4 подписи в `PublicAPI.Unshipped.txt` (точный текст — из анализатора), в т.ч. `[OverloadResolutionPriority(1)]` на `Values(TEntity)`/`Values(IEnumerable<TEntity>)` |
| IDML12 | P2 (форма/удобство) | `InsertBuilder.cs:112,126,201` | Набор `Value`/`Values` — 8 перегрузок; `Values(IEnumerable<TEntity>)` vs `Values<TScalar>(IEnumerable<TScalar>)` разводятся только `[OverloadResolutionPriority(1)]` (.NET 9+, не виден в IntelliSense), а однопараметрический `Value(x => x.Col)` молча биндится к `Value<TScalar>` и кладёт делегат константой (кодовая сторона — ℹ️ E Находки 89/90) | Описать приоритет/семантику в XML-`<remarks>` и guide 19; добавить гард/тест на `Expression`-аргумент в `Value<TScalar>`; зафиксировать атрибут при заморозке |
| IDML13 | **P1 (док/контракт)** | `InsertBuilder.cs:58,77,249`; `docs/guide/19-insert-statement.md:97-111` (+RU); `docs/advanced/api-reference.md` (+RU) | Column-oriented и single-`Value` перегрузки **не отклоняют computed-колонку-цель**, тогда как mapping (`:451`) и entity (`:138`) — отклоняют/исключают; `<exception>` у `Value(column, value)`/`Values(column, values)` не упоминает unmapped/computed. Кодовая сторона — `code-smells-review.md` **Находка 89** | Сначала код-фикс (Находка 89), затем задокументировать контракт «writable column» (identity разрешён, computed запрещён) на всех формах + негативные тесты |
| IDML14 | **P1 (док)** | `docs/guide/19-insert-statement.md` (+`docs/ru/guide/`), `docs/advanced/api-reference.md` (+RU), `docs/providers/overview.md` (+RU) | Три новые batch-формы (`Values(source, mapping)`, `Value(scalar)`/`Values(scalar[])`, `Values(column, values)`) есть только в `docs/specs/roadmap/todo_insert.md:275-281`, но **отсутствуют** в публичном guide 19 EN+RU (там только `Value(column,value)`, `Values(entity)`, `Values(IEnumerable<TEntity>)`, `:83-111`) и в `api-reference` (ссылки только на `Value`/`Values(TEntity)`). AGENTS.md требует EN **и** RU | Добавить в guide 19 EN+RU раздел «Batch from a source / projection / scalar / per-column» с примерами и ограничениями (`Values(s, s => new { … })`, `Values(new[]{…})`, `Values(x => x.Col, seq)`), строки в `api-reference`/`providers/overview` (EN+RU). Ссылки на `docs/specs/**` запрещены |

ℹ️ **Наблюдения.** Фаза 4 нового abstract-члена `ISqlDialect` не добавила (только `InsertBuilder`); `[OverloadResolutionPriority]` — compile-time метаданные, не бинарный/источниковый разрыв для вызывающих. Surface-lock: `find PublicAPI*.txt` — **0**, Шаг 5 открыт (продолжение IDML11).

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.InsertBuilder'1` — **16** публичных методов (фаза 3 — 12; +4 выше), новых публичных типов — 0; `find -name 'PublicAPI*.txt'` — **0** (IDML11/Шаг 5); `rg -c NoWarn` по 7 `.csproj` — 0; `rg 'Values(.*=>|Values\(x =>' docs/ -g '!specs/**'` — в guide 19 не находит mapping/scalar/column-sequence (IDML14); `docfx` 0/0 (прогон заявлен). Кодовая сторона — `code-smells-review.md` (Находки 89–90).

### Аудит 23.09.2026 — DML `INSERT` (фаза 5): строка «только дефолты» и per-column `DEFAULT` (P0 — нет; P1 — нет; P2 — 3)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- публичный тип `NextORM.Core.SqlDefault` (readonly struct) + `SqlDefault.Value` (`src/nextorm.core/SqlDefault.cs:13,16`);
- overload `InsertBuilder<TEntity>.Value<TValue>(Expression<Func<TEntity,TValue>>, SqlDefault)` (`Builders/InsertBuilder.Values.cs:39`);
- 3 DIM `ISqlDialect.SupportsDefaultValues`/`UsesEmptyColumnListForDefaults`/`SupportsColumnDefault` (`DataContext/Dialect/ISqlDialect.cs:1041,1048,1056`) + virtuals `SqlDialectBase.cs:710,712,714` + провайдерные override'ы (PG/SQL Server/SQLite `SupportsDefaultValues`; MySQL/MariaDB `+UsesEmptyColumnListForDefaults`,`SupportsColumnDefault`).

Публичных методов `InsertBuilder<TEntity>` стало **17** (было 16).

**Именование — P0/P1 по именам нет.** `SqlDefault` описателен, BCL-конфликтов нет (в BCL нет `SqlDefault`; `System.Data.SqlTypes.*` не пересекается); `Value` для singleton-маркера — идиома BCL (`DBNull.Value`); `Supports*`/`Uses*` — под существующие паттерны `ISqlDialect`. `Async`-суффиксы не затронуты.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML15 | P2 (форма/расширяемость) | `SqlMutationBuilder.cs:53-56,100`; `ISqlDialect.cs:1041-1056`; `SqlDialectBase.cs:710-714` | Флаги `Supports*`/`Uses*` — на диалекте, но SQL обеих форм (`DEFAULT VALUES` / `() VALUES ()` / `DEFAULT`) захардкожен в ядре; третий диалект не переопределит рендер. Остальные DML-клаузы идут через `ISqlDialect.Make*` (`MakeReturning`/`MakeOutput`/`MakeLastInsertId`/`MakeIdentityFunction`). Кодовая сторона — `code-smells-review.md` **Находка 92** | Вынести `MakeDefaultValues(KeywordCase)`/`MakeColumnDefault(KeywordCase)` на `ISqlDialect`/`SqlDialectBase` (дефолты = текущие строки) и вызывать из `MakeInsert`; либо в `<remarks>` флагов зафиксировать встроенность обеих форм |
| IDML16 | P2 (трекинг) | `SqlDefault.cs:13,16`; `InsertBuilder.Values.cs:39`; `ISqlDialect.cs:1041,1048,1056`; `PublicAPI.*.txt` отсутствуют | 5 новых подписей (тип + член + overload + 3 DIM) не трекаются (PublicApiAnalyzers/ApiCompat не подключены, Шаг 5 открыт). **Продолжение IDML2/IDML10/IDML11** | При заморозке внести `SqlDefault`, `SqlDefault.Value.get`, `Value<TValue>(Expression<…>, SqlDefault)`, 3 DIM + провайдерные override'ы (точный текст — из анализатора) |
| IDML17 | P2 (форма/контракт) | `InsertBuilder.Values.cs:39-46`; `SqlDefault.cs`; `InsertBuilder.cs:224` | Dedicated-overload принимает `SqlDefault`, но не читает параметр; `Value(x => x.Col, default)` может быть неоднозначен; `ParseMapping` детектит маркер по точному типу — boxed/converted в `object`-колонку не распознаётся (кодовая сторона — **Находка 91**); `<summary>` `SqlDefault` этого ограничения не упоминает | Уточнить `<remarks>` (маркер-параметр не читается; проекция через anonymous type; object/base-колонка — после фикса Находки 91); сначала код-фикс, затем формулировки |

ℹ️ **Наблюдения.** DIM'ы `ISqlDialect` source/binary-совместимы для внешних реализаторов (дефолтные тела, ср. KC1); `SqlDialectBase` — `virtual` с дефолтами, внешние наследники не затронуты. `SqlDefault` — readonly struct без членов: `default(SqlDefault)` и `SqlDefault.Value` взаимозаменяемы («синглтон по совместительству»), разрывом не является. Surface-lock: `find PublicAPI*.txt` — **0**, Шаг 5 открыт (IDML16).

**Закрытие ранее открытых.** IDML13 — ✅ закрыта: `ResolveWritableColumn` (`InsertBuilder.cs:283-291`) отклоняет computed-цель во всех column-формах; IDML14 — ✅ закрыта: guide 19 EN+RU (`:113-146`) документирует mapping/scalar/column-sequence формы, api-reference EN+RU — `SqlDefault.Value` и batch-перегрузки. **IDML12 остаётся открытой** (перегрузочная ширина); IDML15 — не её дубль, а контракт рендера. IDML2/IDML10/IDML11/IDML16 (Шаг 5) — открыты.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.InsertBuilder` — **17** публичных методов (фаза 4 — 16; +`Value<TValue>(col, SqlDefault)`); `roslyn members NextORM.Core.SqlDefault` — `Value`; `roslyn members NextORM.Core.ISqlDialect` — `SupportsDefaultValues`/`UsesEmptyColumnListForDefaults`/`SupportsColumnDefault`; `rg 'SqlDefault' docs/ -g '!specs/**'` — guide 19 EN+RU + api-reference EN+RU (IDML14 закрыта); `find -name 'PublicAPI*.txt'` — **0** (IDML16/Шаг 5); `rg -c NoWarn` по `.csproj` — 0; `docfx` 0/0. Кодовая сторона — `code-smells-review.md` (Находки 91–92).

**Закрытие по итогам правок (23.09.2026).** IDML15 — ✅ закрыта: введены публичные DIM'ы `ISqlDialect.MakeDefaultValues(KeywordCase)`/`MakeColumnDefault(KeywordCase)` (source/binary-совместимы, дефолтные тела = прежний текст ядра), `UsesEmptyColumnListForDefaults` удалён до релиза; `SqlMutationBuilder` вызывает `Make*`. IDML17 — ✅ частично: `ParseMapping` разворачивает `Convert` (кодовая Находка 91), добавлен regression-тест на object-типизированный член; формулировка `<summary>` `SqlDefault` про boxing/`object`-колонку **осталась открытой** (мелочь). Наблюдение D (`ResolveSingleWritableProperty`) и IDML12/IDML16 (Шаг 5) — открыты.

### Аудит 23.09.2026 — DML `INSERT` (фаза 6): `INSERT ... SELECT`, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 2)

**Область.** Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- `InsertBuilder<TEntity>.Values<TSource,TResult>(EntityBuilder<TSource>, Expression<Func<TSource,TResult>>) -> InsertBuilder<TEntity>` (`src/nextorm.core/Builders/InsertBuilder.Values.cs:189`).
- Новых публичных **типов** нет — Приложение A без изменений; `EntityBuilder<TEntity>` уже в списке заморозки (продолжение IDML2/IDML10/IDML11/IDML16).

Публичных методов `InsertBuilder<TEntity>` стало **18** (было 17).

**Именование и перегрузка — P0/P1 по именам нет.** Новый overload назван и позиционирован ровно как sibling `Values<TSource,TResult>(IEnumerable<TSource>, Expression<Func<TSource,TResult>>)`: параметры `source`/`mapping` совпадают, XML-doc полный (summary/typeparam/param/returns/exception). **Неоднозначности разрешения перегрузок нет:** `EntityBuilder<TEntity>` реализует только `ICloneable` (`src/nextorm.core/Builders/EntityBuilder.cs:21`) и имеет неявные операторы **только** в `QueryCommand<TEntity>`/`QueryCommand` (`:940,947`) — не в `IEnumerable`, поэтому он не конкурирует ни с `Values<TSource,TResult>(IEnumerable<TSource>, …)`, ни с `Values<TScalar>(IEnumerable<TScalar>)`, ни с `Values(IEnumerable<TEntity>)`/`Values(TEntity)`; компиляция тестов подтверждает единственный применимый кандидат. `Async`-суффикса нет (sync-пары у формы нет); BCL-конфликтов имён нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML18 | P2 (трекинг) | `InsertBuilder.Values.cs:189`; `PublicAPI.*.txt` отсутствуют | Новая подпись не трекается (PublicApiAnalyzers/ApiCompat не подключены, Шаг 5 открыт). **Продолжение IDML2/IDML10/IDML11/IDML16, не новая проблема** | При заморозке внести `Values<TSource,TResult>(EntityBuilder<TSource>, Expression<Func<TSource,TResult>>)` (точный текст — из анализатора) |
| IDML19 | **P1 (док)** | `docs/advanced/limitations.md:15` (+`docs/ru/advanced/limitations.md:15`); `docs/guide/19-insert-statement.md` (EN+RU, разделы `:7-313`); `docs/advanced/api-reference.md:38` (+RU) | **Публичные доки противоречат фиче.** `limitations.md:15` по-прежнему относит `INSERT ... SELECT` к «not implemented» («`UPDATE`/`DELETE`/`MERGE` and `INSERT ... SELECT`/upsert are not» / RU «...и `INSERT ... SELECT`/upsert — нет»), хотя форма реализована и покрыта тестами. В `guide/19` нет раздела «Insert from a query» (RFC `todo_insert_select.md` его прямо планировал). `api-reference:38` описывает тип как «Fluent `INSERT ... VALUES` builder» и не упоминает новый overload. AGENTS.md требует EN **и** RU | Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) убрать `INSERT ... SELECT` из «not implemented» в `limitations.md` (EN+RU) и описать остаточные гейты — CTE-источник и `SqlFunctions.Parameter` отклоняются; (2) добавить в guide 19 (EN+RU) раздел «Insert from a query» с примером `Values(ctx.From<TSource>().Where(...), s => new { … })` и ограничениями; (3) в `api-reference:38` (EN+RU) добавить xref на overload и слово о серверном источнике. Ссылки на `docs/specs/**` из публичных доков запрещены |
| IDML20 | P2 (форма/контракт) | `InsertBuilder.Values.cs:203` (`_rowCount = 1`); `InsertReturningBuilder.cs:51,64,162-166` | `<exception cref="InvalidOperationException">The insert writes more than one row; use ToList.</exception>` `Single()`/`SingleAsync()` **недостижим** для `INSERT ... SELECT`: source-форма всегда даёт `RowCount = 1`, поэтому `.Single()` молча возвращает первую строку многорядной вставки. Кодовая сторона — `code-smells-review.md` **Находка 93** | Сначала код-фикс (Находка 93): отклонять `.Single()` при `_source != null`; затем формулировка `<exception>`/guide. Тест — источник 2+ строк + `.Single()` |

ℹ️ **Наблюдения.** Оба параметра формы (`source`/`mapping`) симметричны sibling-overload'у; имя `Values` (а не `InsertFrom`/`FromQuery`) — продолжение выбранной формы (`todo_insert_select.md`), не переименовывать. Публичный overload не расширяет `ISqlDialect` — новых abstract-членов нет (в отличие от DIM-фаз). Surface-lock: `find PublicAPI*.txt` — **0**, Шаг 5 открыт (IDML18). `todo_insert_select.md` пока не удалён — мелочь specs-дерева (не публичный док).

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.InsertBuilder'1` — **18** публичных методов (фаза 5 — 17; + overload), новых публичных типов — **0**; `rg -n "IEnumerable" src/nextorm.core/Builders/EntityBuilder.cs` — только `ArrayJoin`/`SelectMany`-селекторы, тип не реализует `IEnumerable`; неявные операторы — только `QueryCommand` (`:940,947`); `rg "insert.*select" docs/guide/19-insert-statement.md` — 0 (IDML19); `docs/advanced/limitations.md:15` (+RU) — форма всё ещё в «not implemented» (IDML19); `find -name 'PublicAPI*.txt'` — **0** (IDML18/Шаг 5); `rg -c NoWarn` по 7 `.csproj` — 0. Тесты — `code-smells-review.md`, верификация фазы 6.

**Закрытие по итогам правок (23.09.2026).** IDML20 — ✅ закрыта: `FirstOrThrow` бросает при `rows.Count > 1`, поэтому `.Single()` на многорядном `INSERT ... SELECT` падает (кодовая Находка 93); `<exception cref="InvalidOperationException">` у `Single()`/`SingleAsync()` теперь снова достижим — формулировка «writes more than one row» остаётся корректной. IDML19 — ✅ закрыта в docs-шаге: `limitations.md` (EN+RU) больше не относит `INSERT ... SELECT` к «not implemented» (описаны остаточные гейты — CTE-источник и `SqlFunctions.Parameter`), guide 19 (EN+RU) получил раздел «Insert from a query», api-reference (EN+RU) — xref на overload. IDML18 остаётся открытой (трекинг до Шага 5/PublicAPI-lock), как IDML2/10/11/16.

### Аудит 23.09.2026 — DML `INSERT` (фаза 7): data-modifying CTE, uncommitted working tree (P0 — нет; P1 — нет; P2 — 4)

**Область.** Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- Новый публичный тип `MutationCteQuery<TResult>` (`src/nextorm.core/Builders/MutationCteQuery.cs:18`, `sealed`, `internal` ctor) — **5** публичных членов: `Ctes` (`:32`), `With(string, QueryCommand)` (`:38`), `WithRecursive(string, QueryCommand, int?)` (`:46`), `From(string)` (`:56`), `FromTable(string)` (`:70`).
- Новый extension `DataContextExtensions.With<TEntity,TResult>(this IDataContext, string, InsertReturningBuilder<TEntity,TResult>) -> MutationCteQuery<TResult>` (`DataContext/DataContextExtensions.cs:294`) — overload существующего `With(this IDataContext, string, QueryCommand) -> CteQuery` (`:271`).
- Новый публичный член `CteDefinition.IsDataModifying` (`Builders/CteQuery.cs:62`).
- Новый DIM `ISqlDialect.SupportsDataModifyingCtes => false` (`DataContext/Dialect/ISqlDialect.cs:1040`) + `SqlDialectBase` virtual (`:710`) + PostgreSQL override `true` (`src/nextorm.postgres/PostgresDialect.cs:25`).

Публичного конструктора у `MutationCteQuery<TResult>` нет (`internal`), поэтому тип создаётся только через `With`. Приложение A — исторический список (закрыт 22.09.2026), не расширяется; новый тип идёт в список заморозки Шага 5.

**Именование — P0/P1 по именам нет.** `MutationCteQuery` продолжает линию sibling'а `CteQuery` (регистр акронима `Cte`, не `CTE`; суффикса `Builder` нет, как и у `CteQuery`); BCL-конфликтов нет (`System.*.MutationCteQuery` отсутствует). `FromTable` зеркалит `IDataContext.From(string)`, возвращающий тот же `EntityBuilder<TableAlias>`, и отделяет read-CTE от mutation-CTE. `IsDataModifying` — `Is*` для bool; `SupportsDataModifyingCtes` — паттерн `Supports*` у `ISqlDialect`. Перегрузка `With` **не неоднозначна**: `InsertReturningBuilder<TEntity,TResult>` не является `QueryCommand` и не имеет implicit-операторов (в отличие от `EntityBuilder<TEntity>`, `Builders/EntityBuilder.cs:940,947`). XML-doc у всех новых публичных членов полный (summary/typeparam/param/returns/exception), CS1591 не подавлен.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML21 | P2 (трекинг) | `Builders/MutationCteQuery.cs:18,32,38,46,56,70`; `DataContext/DataContextExtensions.cs:294`; `Builders/CteQuery.cs:62`; `DataContext/Dialect/ISqlDialect.cs:1040`; `src/nextorm.postgres/PostgresDialect.cs:25`; `PublicAPI.*.txt` отсутствуют | Новый публичный тип + 5 членов + overload + `CteDefinition.IsDataModifying` + DIM `SupportsDataModifyingCtes` (и 2 override) не трекаются: `Microsoft.CodeAnalysis.PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` нет (Шаг 5 открыт). **Продолжение IDML2/IDML10/IDML11/IDML16/IDML18, не новая проблема** | При заморозке внести подписи (точный текст — из анализатора): `MutationCteQuery<TResult>`, `.Ctes.get`, `.With(string, QueryCommand)`, `.WithRecursive(string, QueryCommand, int?)`, `.From(string)`, `.FromTable(string)`, `DataContextExtensions.With<,>(…, InsertReturningBuilder<,>)`, `CteDefinition.IsDataModifying.get`, `ISqlDialect.SupportsDataModifyingCtes.get` + `SqlDialectBase`/`PostgresDialect` override'ы |
| IDML22 | P2 (форма/док) | `DataContext/DataContextExtensions.cs:271,294`; `docs/guide/09-cte.md` (+`docs/ru/guide/09-cte.md`) | Перегрузки `With` возвращают **разные типы** (`CteQuery` vs `MutationCteQuery<TResult>`). Неоднозначности разрешения нет (см. выше) и это осознанно: имя продолжает выбранную CTE-форму. Однако XML-`<summary>` существующего `With(name, query)` (`:270`) не упоминает data-modifying overload, поэтому пара не видна из IntelliSense первой перегрузки | **Не переименовывать**; при заморозке зарегистрировать обе подписи (IDML21). Опционально: добавить в `<remarks>`/`<seealso>` `With(this IDataContext, string, QueryCommand)` ссылку на `With<TEntity,TResult>(…)`, а в guide 09 — абзац «обе перегрузки `With`» (EN+RU). Любое переименование — одновременно `docs/**` **и** `docs/ru/**` (AGENTS.md) |
| IDML23 | P2 (форма/именование) | `Builders/MutationCteQuery.cs:70` (`FromTable`); `Builders/CteQuery.cs:62` (`IsDataModifying`); `DataContext/Dialect/ISqlDialect.cs:1040` (`SupportsDataModifyingCtes`) | Названия проверены и конвенциональны: `Cte`-регистр согласован с `CteQuery`/`CteDefinition`; `Is*`/`Supports*` — существующие паттерны; `FromTable(cteName)` читает read-CTE через `TableAlias`-путь, зеркаля `IDataContext.From(string)`. Единственная асимметрия — `From` закреплён за mutation-CTE, read-CTE уходит в `FromTable` (при полностью свободной поверхности читалось бы `FromReadCte`); BCL-конфликтов нет | **Не переименовывать** (уже задокументировано в guide 09 EN+RU); зафиксировать решение и пару `From`/`FromTable` при заморозке. Δ не требует доков, пока имя не меняется |
| IDML24 | P2 (док/охват) | `docs/guide/09-cte.md:256` (+`docs/ru/guide/09-cte.md:258`); `docs/advanced/limitations.md:15,62` (+`docs/ru/advanced/limitations.md:15,62`); `docs/advanced/api-reference.md:48` (+`docs/ru/advanced/api-reference.md:48`); `docs/specs/roadmap/sql-capabilities-gap-analysis.md:169-172`; строки в `docs/providers/overview.md` (EN+RU) и `docs/guide/provider-specific/postgresql.md` (EN+RU) отсутствуют | **Доковый план RFC выполнен в том же изменении EN+RU** (это закрытие, а не пробел): guide 09 получил раздел «Data-modifying CTE (PostgreSQL)»/«Модифицирующий CTE (PostgreSQL)» с примерами и оговоркой о snapshot; limitations снял «data-modifying CTE не поддерживаются»; api-reference добавил `MutationCteQuery<TResult>`; gap-analysis §4 п.18 обновлён. Остаточный **мелкий охват**: capability-таблица `providers/overview.md` (EN+RU) содержит строки `INSERT ... VALUES`/`Returning`, но не отдельную строку про data-modifying CTE; `guide/provider-specific/postgresql.md` тоже молчит. RFC (план закрыт и удалён) этих файлов не требовал | Опционально (не блокирует, P2-охват): добавить в `providers/overview.md` (EN+RU) строку «Data-modifying CTE — supported (PostgreSQL) / throws `NotSupportedException` (остальные)» и абзац в `guide/provider-specific/postgresql.md` (EN+RU). Ссылки на `docs/specs/**` из публичных доков запрещены |

ℹ️ **Наблюдения.** DIM `ISqlDialect.SupportsDataModifyingCtes` и его `SqlDialectBase`-двойник source/binary-совместимы для внешних реализаторов (дефолтные тела); переименований нет. `MutationCteQuery` — `sealed` с `internal` ctor и `internal BuildShape`: снаружи конструируется только через `With`, а инвариант «`From`-имя = mutation-имя» проверяется в рантайме (`ArgumentException`). Публичный `Ctes` (`IReadOnlyList<CteDefinition>`) зеркалит `CteQuery.Ctes`. Surface-lock: `find PublicAPI*.txt` — **0**, Шаг 5 открыт (IDML21). Приложение A не расширяется (исторический, закрыт). Содержательная кодовая сторона — `code-smells-review.md`, Находки 96–99 и наблюдения A–H.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.MutationCteQuery<TResult>` — **5** публичных членов (`Ctes`, `With`, `WithRecursive`, `From`, `FromTable`), ctor `internal`; `roslyn members NextORM.Core.CteDefinition` — `IsDataModifying.get` публичный, ctor `(string, QueryCommand, InsertCommand)` `internal`; `roslyn members NextORM.Core.DataContextExtensions` — обе перегрузки `With` (`:271`/`:294`), implicit-операторов у `InsertReturningBuilder` нет (`rg 'implicit operator'` — только `Builders/EntityBuilder.cs:940,947`); `ISqlDialect.SupportsDataModifyingCtes` — DIM (`:1040`), `SqlDialectBase` virtual (`:710`), PG override `true` (`PostgresDialect.cs:25`); `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`), `NoWarn` по 7 `.csproj` — **0**, CS1591 не подавлен ⇒ XML-doc-покрытие полное; `rg '#pragma warning disable|SuppressMessage' src/` — **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma` (все с `restore`) = **11/11**, новых в файлах фичи — **0**; `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 инертных `S*` без `SonarAnalyzer` + `CA2254`); `find -name 'PublicAPI*.txt'` — **0** (IDML21/Шаг 5); `rg 'MutationCteQuery' docs/ -g '!specs/**'` — guide 09 EN+RU, limitations EN+RU, api-reference EN+RU (IDML24); `rg -i 'data-modifying' docs/providers/overview.md docs/ru/providers/overview.md` — пусто (IDML24). Сфокусированные тесты (Release, `--no-build`): postgres `DataModifyingCte_*` — **5/5**; гейты SQLite/SQL Server/MySQL/ClickHouse/in-memory — по 1; интеграционный PG `DataModifyingCte_ShouldInsertAndReturnRows`. Тесты — `code-smells-review.md` (Находки 96–99, наблюдения A–H).

### Аудит 23.09.2026 — DML `INSERT` (фаза 7-бис): `INSERT ... SELECT` как тело CTE и главный `INSERT ... SELECT` (P0 — нет; P1 — нет; P2 — 1)

**Область (дополнение к фазе 7, поверхность аддитивна).** Закрыты два остатка RFC:

- Новый публичный член `CteQuery.With<TEntity,TResult>(string, InsertReturningBuilder<TEntity,TResult>) -> MutationCteQuery<TResult>` (`src/nextorm.core/Builders/CteQuery.cs`), объявляющий модифицирующий CTE **после** уже собранных read-CTE, чтобы его `INSERT ... SELECT`-тело могло на них ссылаться. Продолжение IDML21; сигнатура симметрична `DataContextExtensions.With<TEntity,TResult>(IDataContext, string, InsertReturningBuilder<,>)`.
- `InsertBuilder<TEntity>.Values<TSource,TResult>(EntityBuilder<TSource>, Expression<Func<TSource,TResult>>)` больше **не** отклоняет CTE-источник: `WITH` поднимается перед `INSERT` (`QueryPlanner.RenderSource` теперь возвращает `WithSql` отдельно; `DataContext.BuildInsertSql` ставит его перед `insert into`). Это снятие ранее задокументированного гейта, а не новый публичный член.
- Тело модифицирующего CTE теперь принимает `INSERT ... SELECT` (`SqlSourceRenderer.RenderMutation` рендерит источник и отбрасывает его CTE — они уже объявлены внешним `WITH`).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| IDML25 | P2 (трекинг) | `src/nextorm.core/Builders/CteQuery.cs` (`CteQuery.With<TEntity,TResult>`); `PublicAPI.*.txt` отсутствуют | Новая публичная generic-перегрузка не трекается (продолжение IDML2/10/11/16/18/21). Перегрузка `CteQuery.With(string, QueryCommand)` не конфликтует: `InsertReturningBuilder<,>` не `QueryCommand` | При заморозке (Шаг 5) внести `CteQuery.With<TEntity,TResult>(string, InsertReturningBuilder<TEntity,TResult>)` вместе с остальными |

**Закрытие (23.09.2026).** IDML25 — новый член учтён; IDML22 (обе перегрузки `With`) распространён и на пару `CteQuery.With`. Публичные доки EN+RU обновлены в том же изменении: guide 09 (тело `INSERT ... SELECT`, ссылка на ранее объявленный read-CTE, главный `INSERT ... SELECT`), guide 19 (снят «источник с CTE не поддерживается» → «CTE поднимается перед `INSERT`»), limitations EN+RU, gap-analysis §4 п.18 (RFC-план закрыт и удалён). `docfx` — **0/0**. Гейт остаётся только для не-PG провайдеров (`SupportsDataModifyingCtes = false`).

### Аудит 23.09.2026 — DML `MERGE` (key upsert): `MergeInto`/`Using`/`OnKeys`/`WhenMatchedUpdate`/`WhenNotMatchedInsert`, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 4)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- новый публичный тип `NextORM.Core.MergeBuilder<TEntity>` (`Builders/MergeBuilder.cs:18`, `sealed`, `internal` ctor) — **8** публичных методов: `Using(TEntity)` (`:38`, `[OverloadResolutionPriority(1)]`), `Using(IEnumerable<TEntity>)` (`:51`), `OnKeys()` (`:87`), `WhenMatchedUpdate()` (`:112`), `WhenNotMatchedInsert()` (`:120`), `ToSql()` (`:131`), `Merge()` (`:141`), `MergeAsync(CancellationToken = default)` (`:147`);
- новый extension `DataContextExtensions.MergeInto<TEntity>(this IDataContext, Action<EntityMetadataBuilder<TEntity>>? = null)` (`DataContext/DataContextExtensions.cs:51`) — sibling `InsertInto<TEntity>` (`:32`), общий private `ResolveMetadata<TEntity>` (`:58`);
- **7** новых членов `ISqlDialect` (DIM, source/binary-совместимы): `SupportsOnConflict` (`:1099`), `MakeOnConflict(IReadOnlyList<string>, KeywordCase)` (`:1106`), `SupportsOnDuplicateKey` (`:1114`), `MakeOnDuplicateKey(KeywordCase)` (`:1120`), `MakeUpsertValueReference(string, KeywordCase)` (`:1129`), `SupportsMerge` (`:1137`), `MakeMerge(string, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>, string, KeywordCase)` (`:1150`); `SqlDialectBase`-двойники (`:710-722`); провайдерные override'ы: PG `SupportsOnConflict` (`PostgresDialect.cs:28`), SQLite `SupportsOnConflict` (`SqliteDialect.cs:38`), MySQL `SupportsOnDuplicateKey` (`MySqlDialect.cs:42`) + `MakeUpsertValueReference` (`:44`), SQL Server `SupportsMerge` (`SqlServerDialect.cs:43`) + `MakeMerge` (`:46`); MariaDB наследует MySQL;
- существующие типы (`InsertBuilder<TEntity>` и др.) публичных членов не поменяли.

Публичного конструктора у `MergeBuilder<TEntity>` нет (`internal`), поэтому тип создаётся только через `MergeInto`. Приложение A — исторический список (закрыт 22.09.2026), не расширяется; тип идёт в список заморозки Шага 5.

**Именование — P0 нет.** Все имена совпадают с планом RFC `todo_merge.md` и linq2db-линией: `MergeInto` (entry), `Using`, `OnKeys`, `WhenMatchedUpdate`, `WhenNotMatchedInsert`, `Merge`/`MergeAsync`, `ToSql`. BCL-конфликтов нет (`MergeBuilder` в BCL отсутствует). `Async`-суффикс корректен — есть sync-пара `Merge` (AGENTS соблюдён). `Supports*`/`Make*` — существующий паттерн `ISqlDialect`; XML-doc у всех новых публичных членов полный (summary/typeparam/param/returns/exception), CS1591 не подавлен.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| MRG1 | **P1 (док/противоречие)** | `docs/advanced/limitations.md:3,15` (+`docs/ru/advanced/limitations.md:3,15`); `docs/guide/19-insert-statement.md:443` (+`docs/ru/guide/19-insert-statement.md:446`); `docs/advanced/api-reference.md` (+RU); `docs/providers/overview.md` (+RU) | **Публичные доки утверждают обратное фиче.** `limitations.md:15` (EN+RU) — «`UPDATE`/`DELETE`/`MERGE` and upsert are not»; `:3` — «leaves … `UPDATE`/`DELETE`/`MERGE` … to the caller»; guide 19 — «**No upsert** — `ON CONFLICT`/`ON DUPLICATE KEY`/upsert is tracked with `MERGE`». `MergeInto` нигде не упомянут (`rg -i 'MergeInto' docs/ -g '!specs/**'` — 0). RFC `todo_merge.md` («Файлы к изменению → Документация») требует guide 19, providers, limitations, api-reference EN+RU в том же изменении. Ср. IDML19 (закрыта для `INSERT ... SELECT` тем же приёмом). AGENTS.md требует EN **и** RU | Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) `limitations.md` (EN+RU) — снять `MERGE`/upsert из «not implemented», описать гейты (ClickHouse/in-memory → `NotSupportedException`; MySQL/MariaDB не используют переданные `OnKeys`); (2) guide 19 (EN+RU) — раздел «Key upsert (`MergeInto`)» с примером `ctx.MergeInto<T>().Using(e).OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().Merge()` и таблицей провайдеров; (3) `api-reference` (EN+RU) — `MergeBuilder<TEntity>`/`MergeInto`; (4) `providers/overview.md` (EN+RU) — строка capability. Ссылки на `docs/specs/**` из публичных доков запрещены |
| MRG2 | P2 (трекинг) | `Builders/MergeBuilder.cs:18-147`; `DataContextExtensions.cs:51`; `ISqlDialect.cs:1099-1150`; `SqlDialectBase.cs:710-722`; `PostgresDialect.cs:28`; `SqliteDialect.cs:38`; `MySqlDialect.cs:42,44`; `SqlServerDialect.cs:43,46`; `PublicAPI.*.txt` отсутствуют | Публичный тип + 8 членов + extension + 7 DIM + `SqlDialectBase`/провайдерные override'ы не трекаются: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` нет (Шаг 5 открыт). **Продолжение IDML2/10/11/16/18/21/25, не новая проблема** | При заморозке внести подписи (точный текст — из анализатора): `MergeBuilder<TEntity>`, `.Using(TEntity)`, `.Using(IEnumerable<TEntity>)`, `.OnKeys()`, `.WhenMatchedUpdate()`, `.WhenNotMatchedInsert()`, `.ToSql()`, `.Merge()`, `.MergeAsync(CancellationToken)`, `DataContextExtensions.MergeInto<,>(…)`, 7 членов `ISqlDialect` + override'ы. Зафиксировать `[OverloadResolutionPriority(1)]` |
| MRG3 | P2 (контракт/док) | `Builders/MergeBuilder.cs:87-108,110-124,154-157`; `SqlMutationBuilder.cs:214-225`; `MySqlDialect.cs:42-44` | `OnKeys()` обязателен на всех провайдерах, но на MySQL/MariaDB `ON DUPLICATE KEY UPDATE` не имеет conflict-target, поэтому переданные ключи **игнорируются** (в SQL ключа нет — тесты mysql/mariadb). `WhenMatchedUpdate()`/`WhenNotMatchedInsert()` не принимают ни условий, ни колонок — их флаги служат только гейтом «обе вызваны». Кодовая сторона — `code-smells-review.md`, **Находка 102** | Не переименовывать/не менять поведение; зафиксировать в XML-`<remarks>` и guide 19 (EN+RU): «`OnKeys()` обязателен для единообразия; на MySQL/MariaDB определяет unique-индекс; ветки пока без параметров». Синхронизировать с MRG1 |
| MRG4 | P2 (форма/консистентность) | `ISqlDialect.cs:1129`; `SqlDialectBase.cs:718` | Дефолт `MakeUpsertValueReference` игнорирует `keywordCase` (`"excluded." + column`), поэтому в режиме `Upper` `excluded` остаётся нижним, тогда как вся остальная клауза казуется (`ON CONFLICT (id) DO UPDATE SET`). `excluded` — имя псевдо-таблицы (идентификатор), т.е. решение TUP2 («конструкторы/имена вне политики») применимо, но для upsert'а не задокументировано | Либо явно оставить как «идентификатор вне политики `KeywordCase`» и одной строкой задокументировать в `limitations.md`/guide (EN+RU), либо (если решено казовать) применить `Kw`. Кодовая сторона — `code-smells-review.md`, Наблюдение E. Не блокирует заморозку |
| MRG5 | P2 (форма/именование) | `MergeBuilder.cs:87` (`OnKeys`), `:112,120` (`WhenMatchedUpdate`/`WhenNotMatchedInsert`), `:37,51` (`Using`) | Имена выбраны RFC-планом и SQL-ориентированы, но расходятся с linq2db: у linq2db conflict-target задаётся `OnTargetKey()` (с селектором/по ключу), а ветки — `WhenMatched(...)`/`WhenNotMatched(...)` + `ThenUpdate`/`ThenInsert`. Локально `OnKeys` безаргументный (ключ из метаданных), ветки тоже безаргументные. Несоответствие **осознанное** (RFC: «Имена SQL-ориентированы … совпадают с linq2db (`Merge`, `MergeWithOutput`)»), но зафиксировано только в spec | **Не переименовывать**; при заморозке зарегистрировать как есть и зафиксировать решение в release-notes/`linq2db-comparison.md` (EN+RU/спеки). Дополнительно: `[OverloadResolutionPriority(1)]` стоит только на `Using(TEntity)`, а у `InsertBuilder` — на **обе** entity-перегрузки (`InsertBuilder.Values.cs:93,107`) — привести к одному виду (или убрать атрибут как ненужный без конкурирующей generic-перегрузки) |

ℹ️ **Наблюдения.** (1) `MergeBuilder` `sealed` + `internal` ctor — снаружи только через `MergeInto` (как `MutationCteQuery`). (2) **Affected-rows различаются по провайдерам**: MySQL/MariaDB по умолчанию отдают 1 за update и 2 за insert на строку, SQL Server `MERGE` — число затронутых, PG/SQLite — число строк upsert'а; `<returns>` `Merge()`/`MergeAsync()` говорит нейтрально («as reported by the provider») — при MRG1 стоит добавить оговорку. (3) `MergeInto` — sibling `InsertInto`: общий `ResolveMetadata` переиспользован (метаданные кэшируются per-process), новых публичных хелперов нет. (4) Surface-lock: `find PublicAPI*.txt` — **0**, Шаг 5 открыт (MRG2). (5) Переименование любой публичной подписи потребует `docs/**` **и** `docs/ru/**` (AGENTS.md) — пока переименований нет.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.MergeBuilder<TEntity>` — **8** публичных методов, ctor `internal`; `roslyn members NextORM.Core.ISqlDialect` — `SupportsOnConflict`/`MakeOnConflict`/`SupportsOnDuplicateKey`/`MakeOnDuplicateKey`/`MakeUpsertValueReference`/`SupportsMerge`/`MakeMerge`; `rg -i 'MergeInto|upsert' docs/ -g '!specs/**'` — только «no upsert» (MRG1); `rg -c NoWarn` по 7 `.csproj` — 0; `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`), build `0/0` ⇒ XML-doc у всех новых публичных членов полный; `find -name 'PublicAPI*.txt'` — **0** (MRG2/Шаг 5); новых публичных типов — **1** (`MergeBuilder<TEntity>`), Приложение A не расширяется; интеграционных тестов upsert нет (`rg -i 'merge|upsert' tests/nextorm.integration.tests` — 0; кодовая сторона — `code-smells-review.md`, Наблюдение D). Сфокусированные тесты (Release): sqlite/postgres/sqlserver/mysql/mariadb/clickhouse `MergeSqlGenerationTests` + core `MergeBuilderTests`. Кодовая сторона — `code-smells-review.md`, Находки 100–102.

**Закрытие (23.09.2026).** **MRG1 — закрыто**: обновлены публичные доки EN+RU — `docs/advanced/limitations.md` (+RU, `MERGE`/upsert снят из «not implemented»), `docs/guide/19-insert-statement.md` (+RU, новый раздел «Upsert (key merge)» с якорем `#upsert-key-merge`), `docs/advanced/api-reference.md` (+RU, строка `MergeBuilder<TEntity>`), `docs/providers/overview.md` (+RU, строка «Key upsert»). **MRG3/MRG4/MRG5 — закрыто частично**: контракт `OnKeys()`/веток (в т. ч. no-op ключей на MySQL/MariaDB) описан в `<remarks>` `MergeBuilder<TEntity>` и в guide 19; некейсованность `excluded` задокументирована в XML `MakeUpsertValueReference`; `[OverloadResolutionPriority(1)]` выставлен на **обеих** `Using`-перегрузках. **MRG2** (surface-lock `PublicAPI.*.txt`) остаётся до Шага 5 (заморозка).

### Аудит 23.09.2026 — DML `DELETE` (фаза 1): `DeleteFrom`/`All`/`Delete`/`DeleteAsync`, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 3)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- новый публичный тип `NextORM.Core.DeleteBuilder<TEntity>` (`Builders/DeleteBuilder.cs:13`, `sealed`, `internal` ctor `:20`) — **5** публичных методов: `Where(Expression<Func<TEntity,bool>>)` (`:34`), `All()` (`:47`), `ToSql()` (`:58`), `Delete()` (`:68`), `DeleteAsync(CancellationToken = default)` (`:77`);
- **3** extension'а `DataContextExtensions`: `DeleteFrom<TEntity>(this IDataContext, Action<EntityMetadataBuilder<TEntity>>? = null)` (`:81`), `Delete<TEntity>(this IDataContext, TEntity)` (`:97`), `DeleteAsync<TEntity>(this IDataContext, TEntity, CancellationToken = default)` (`:115`);
- **1** новый член `ISqlDialect` (DIM, source/binary-совместим): `SupportsDelete => true` (`ISqlDialect.cs:1166`); `SqlDialectBase`-двойник (`:735`) + `ClickHouseDialect` override `=> false` (`ClickHouseDialect.cs:38`);
- существующие публичные типы членов не поменяли.

Публичного конструктора у `DeleteBuilder<TEntity>` нет (`internal`), поэтому тип создаётся только через `DeleteFrom`; `Delete<TEntity>`/`DeleteAsync<TEntity>` — самостоятельные extension-терминалы. Приложение A — исторический список (закрыт 22.09.2026), не расширяется; тип идёт в список заморозки Шага 5.

**Именование — P0 нет.** `DeleteFrom<TEntity>()` для предиката и `Delete(entity)` для сущности — **зафиксированное решение** открытого вопроса №1 RFC `todo_delete.md`; `All()` — решение вопроса №2 (явный маркер полного удаления). BCL-конфликтов нет (`DeleteBuilder`/`DeleteFrom` в BCL отсутствуют); `IDataContext` — маркерный интерфейс (0 членов, `IDataContext.cs:17-25`), поэтому extension `Delete`/`DeleteAsync` не перекрывается instance-членом. `Async`-суффикс корректен — есть sync-пара `Delete` (AGENTS соблюдён); `CancellationToken` — последним (CA1068). `Supports*`/`Make*` — существующий паттерн `ISqlDialect`. XML-doc у всех новых публичных членов полный (summary/typeparam/param/returns), CS1591 не подавлен.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| DEL1 | **P1 (док/противоречие)** | `docs/advanced/limitations.md:3,15` (+`docs/ru/advanced/limitations.md:3,15`); `docs/guide/19-insert-statement.md:306` (+RU `:309`); `docs/advanced/api-reference.md:38-40` (+RU); `docs/providers/overview.md:46` (+RU) | **Публичные доки утверждают обратное фиче.** `limitations.md:3` (EN+RU) — «намеренно оставляет … `UPDATE`/`DELETE` … вызывающей стороне»; `:15` — «`UPDATE`/`DELETE`, полный `MERGE` … — нет», хотя DELETE реализован и покрыт тестами. `DeleteFrom`/`DeleteBuilder` **нигде** не упомянуты (`grep -rn 'DeleteFrom' docs/ --include=*.md` без `docs/specs` — **0**); guide 19 `:306`/`:309` по-прежнему «`DELETE` bodies … will be documented in their own guides»; в `api-reference` нет строки `DeleteBuilder<TEntity>`, в capability-таблице `providers/overview.md` — строки Delete. RFC `todo_delete.md` («Файлы к изменению → Документация») требует guide 19, providers, limitations, api-reference EN+RU. Ср. IDML19/MRG1 (закрыты тем же приёмом). AGENTS.md требует EN **и** RU | Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) `limitations.md` (EN+RU) — снять `DELETE` из «not implemented», описать формы (`Where`/`All`/`Delete(entity)`), гейты (in-memory/ClickHouse → `NotSupportedException`) и отсутствие `RETURNING` в фазе 1; (2) guide 19 (EN+RU) или отдельный гайд — раздел «DELETE» с примерами `ctx.DeleteFrom<T>().Where(...).Delete()`, `.All()`, `ctx.Delete(entity)` и таблицей провайдеров; (3) `api-reference` (EN+RU) — `DeleteBuilder<TEntity>` + три extension'а; (4) `providers/overview.md` (EN+RU) — строка «Delete (`DeleteFrom`)». Ссылки на `docs/specs/**` из публичных доков запрещены |
| DEL2 | P2 (контракт/док) | `Builders/DeleteBuilder.cs:47-51,95-105`; `DataContextExtensions.cs:81` | `All()` молча игнорируется, если вызван `Where(...)` (`Where` всегда побеждает): `DeleteFrom<T>().Where(...).All().Delete()` удаляет только отфильтрованные строки. Поведение не задокументировано. Кодовая сторона — `code-smells-review.md`, **Находка 103** | Либо гард `InvalidOperationException` на `Where`+`All`, либо явный `<remarks>`/guide «WHERE побеждает / last-wins». Поведение решает владелец. Синхронизировать с DEL1 |
| DEL3 | P2 (трекинг) | `Builders/DeleteBuilder.cs:13-81`; `DataContextExtensions.cs:81,97,115`; `ISqlDialect.cs:1166`; `SqlDialectBase.cs:735`; `ClickHouseDialect.cs:38`; `PublicAPI.*.txt` отсутствуют | Публичный тип + 5 членов + 3 extension'а + 1 DIM + 2 override'а не трекаются: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` нет (Шаг 5 открыт). **Продолжение IDML2/10/11/16/18/21/25/MRG2, не новая проблема** | При заморозке внести подписи (точный текст — из анализатора): `DeleteBuilder<TEntity>`, `.Where(Expression<Func<TEntity,bool>>)`, `.All()`, `.ToSql()`, `.Delete()`, `.DeleteAsync(CancellationToken)`, `DataContextExtensions.DeleteFrom<,>(…)`, `.Delete<,>(…)`, `.DeleteAsync<,>(…)`, `ISqlDialect.SupportsDelete.get` + `SqlDialectBase`/`ClickHouseDialect` override'ы |
| DEL4 | P2 (форма/именование) | `DataContextExtensions.cs:81,97,115`; `Builders/DeleteBuilder.cs:47` | Имена `DeleteFrom`/`Delete`/`DeleteAsync`/`All` SQL-ориентированы по RFC-плану (вход `DeleteFrom` — аналог `INSERT INTO`/`MERGE INTO`; `Delete(entity)` — терминал по ключу; `All()` — явный маркер полного удаления). Расхождение с linq2db (там `Delete()` без `Where` удаляет всё) **осознанное** (RFC, вопрос №2). Асимметрия — два «Delete» в одной поверхности (`DeleteFrom<T>()` vs `Delete(entity)`) плюс терминал `Delete()` у билдера | **Не переименовывать**; зафиксировать решение в release-notes/`linq2db-comparison.md` (EN+RU) и при заморозке. Любое переименование — одновременно `docs/**` **и** `docs/ru/**` (AGENTS.md) |

ℹ️ **Наблюдения.** (1) `DeleteBuilder` `sealed` + `internal` ctor — снаружи только через `DeleteFrom`, как `MergeBuilder`/`MutationCteQuery`. (2) Новый DIM `SupportsDelete` — source/binary-совместимое расширение `ISqlDialect` (default `true`), но ClickHouse-override обязателен; при заморозке — в KC1-перечень изменённых интерфейсных сигнатур. (3) XML-doc терминалов `Delete()`/`DeleteAsync()`/`ToSql()` не объявляет `<exception cref="NotSupportedException">` (in-memory/ClickHouse), XML `ToSql` и `RequireExecutor` различаются формулировкой — кодовая сторона ℹ️ D. (4) Surface-lock: `find PublicAPI*.txt` — **0**, Шаг 5 открыт (DEL3). (5) Переименование любой публичной подписи потребует `docs/**` **и** `docs/ru/**` (AGENTS.md) — пока переименований нет.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.DeleteBuilder` — **5** публичных методов, ctor `internal`; `roslyn types DeleteBuilder` — 1 публичный тип (второй матч — тест `DeleteBuilderTests`); `roslyn members NextORM.Core.DataContextExtensions` — `DeleteFrom`/`Delete`/`DeleteAsync` присутствуют; `SuppressMessage`/`pragma` по `src/` — 6/5 (все оправданные), `NoWarn` в `.csproj` — 0; `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`), build `0/0` ⇒ XML-doc у новых публичных членов полный; `grep -rn 'DeleteFrom' docs/ --include=*.md` (без `docs/specs`) — **0** (DEL1); `find -name 'PublicAPI*.txt'` — **0** (DEL3/Шаг 5). Тесты (Release): sqlite/postgres/sqlserver/mysql/mariadb/clickhouse `DeleteSqlGenerationTests` + core `DeleteBuilderTests` — passed; интеграция (`DOCKER_HOST`, Debug, `FullyQualifiedName~Delete_`) — **20/20, 0 skipped**. Кодовая сторона — `code-smells-review.md`, Находки 103–104.

**Закрытие по итогам правок (23.09.2026).** DEL1 — ✅ закрыта: публичные доки (EN+RU) обновлены — `limitations.md` (DELETE убран из «not implemented», описаны формы/гейты), `guide/19` содержит раздел `## Delete` (anchor `#delete`) с примерами и таблицей провайдеров, `api-reference` — строка `DeleteBuilder<TEntity>`, `providers/overview.md` — строка `DELETE`; ссылок на `docs/specs/**` нет. DEL2 — ✅ закрыта: `Where`+`All` теперь бросают `InvalidOperationException` (с публичным `<exception>` в XML). DEL3 — ⏳ открыта (surface-lock, Шаг 5). DEL4 — ✅ решение «не переименовывать» зафиксировано (расхождение с linq2db осознанное, RFC вопрос №2).

### Аудит 23.09.2026 — DML `DELETE` (фаза 2): `DeleteReturningBuilder<TEntity,TResult>`, `TruncateBuilder<TEntity>`, `RETURNING`/`OUTPUT` и `TRUNCATE`, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 3)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет; фаза 1 (`DeleteFrom`/`All`/`Delete`/`DeleteAsync`, DEL1–DEL4) остаётся. Новое к заморозке Шага 5:

- новый публичный тип `NextORM.Core.DeleteReturningBuilder<TEntity,TResult>` (`Builders/DeleteReturningBuilder.cs:14`, `sealed`, `internal` ctor `:21`) — **5** публичных методов: `Single()` `:37`, `SingleAsync(CancellationToken = default)` `:45`, `ToList()` `:51`, `ToListAsync(CancellationToken = default)` `:58`, `ToSql()` `:66`;
- новый публичный тип `NextORM.Core.TruncateBuilder<TEntity>` (`Builders/TruncateBuilder.cs:12`, `sealed`, `internal` ctor `:17`) — **3** публичных метода: `ToSql()` `:26`, `Execute()` `:37`, `ExecuteAsync(CancellationToken = default)` `:54`;
- `DeleteBuilder<TEntity>` — **+2** публичных члена: `Returning()` `:84`, `Returning<TResult>(Expression<Func<TEntity,TResult>>)` `:102` (итого 7);
- **+1** extension `DataContextExtensions.Truncate<TEntity>(this IDataContext, Action<EntityMetadataBuilder<TEntity>>? = null)` (`:132`);
- `ISqlDialect` — **+6** публичных DIM (source/binary-совместимо): `MakeDeletedOutput` `:1085`, `SupportsTruncate` `:1177`, `MakeTruncate` `:1186`, `MakeDeleteHead` `:1196`, `DeleteRequiresWhere` `:1203`, `MakeDeleteSuffix` `:1212` (плюс `SupportsDelete` `:1170` из фазы 1 = 7 новых членов интерфейса);
- `SqlDialectBase` — те же **6** public virtual (`:703,740,743,746,749,752`) + `SupportsDelete` `:737`;
- провайдерные override: `PostgresDialect.SupportsTruncate` `:40`, `SqlServerDialect.SupportsTruncate` `:43`, `MySqlDialect.SupportsTruncate` `:40` (MariaDB наследует), `ClickHouseDialect.SupportsTruncate` `:47` + `MakeDeleteHead` `:38`/`DeleteRequiresWhere` `:41`/`MakeDeleteSuffix` `:44`; override `ClickHouseDialect.SupportsDelete => false` **удалён** (наследует `true` из базы);
- существующие публичные типы членов, кроме `DeleteBuilder`, не меняли.

Публичного конструктора у обоих новых типов нет (`internal`), поэтому они создаются только через `Returning()`/`Truncate<T>()` (как `InsertReturningBuilder`/`MergeBuilder`). Приложение A — исторический список (закрыт 22.09.2026), не расширяется.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| DEL5 | **P1 (док/противоречие)** | `docs/guide/19-insert-statement.md:464-465,470` (+RU `:468-469,474`); `docs/advanced/limitations.md:15` (+RU `:15`); `docs/providers/overview.md:47` (+RU `:47`); `docs/advanced/api-reference.md:41` (+RU `:41`) | **Публичные доки противоречат фазе 2.** Guide 19 (EN `:464-465` / RU `:468-469`) — «ClickHouse: только асинхронная мутация `ALTER TABLE ... DELETE` → `NotSupportedException`», «In-memory: read-only → `NotSupportedException`»; EN `:470` / RU `:474` — «у `DELETE` нет `RETURNING`/`OUTPUT`, нет `TRUNCATE`». Фактически ClickHouse рендерит **синхронный** `ALTER TABLE <t> DELETE WHERE ... SETTINGS mutations_sync = 1` (тест clickhouse `DeleteSqlGenerationTests`), in-memory **поддерживает** `DELETE`/`TRUNCATE` (`DeleteBuilderTests` 10), `RETURNING`/`OUTPUT` на delete есть у PostgreSQL/SQLite/SQL Server, `TRUNCATE` — у всех, кроме SQLite. `limitations.md:15` (EN+RU) по-прежнему «ClickHouse … и in-memory его отклоняют»; `providers/overview.md:47` (EN+RU) — те же `NotSupportedException`; `api-reference.md:41` (EN+RU) перечисляет у `DeleteBuilder` только `Where`/`All`/`Delete`/`DeleteAsync`/`ToSql` и **не содержит** строк `DeleteReturningBuilder<TEntity,TResult>` и `TruncateBuilder<TEntity>` (extension `Truncate<T>()` тоже отсутствует). `grep -rn 'DeleteReturningBuilder\|TruncateBuilder' docs/ --include=*.md` без `docs/specs` — **0**. AGENTS.md требует EN **и** RU. Ср. DEL1/MRG1 (закрыты тем же приёмом) | Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) guide 19 (EN+RU) — переписать таблицу Delete (`ClickHouse` → `ALTER TABLE ... DELETE ... SETTINGS mutations_sync = 1`, `In-memory` → `DELETE`/`TRUNCATE` да), добавить разделы `## Returning the deleted rows` (формы `Returning()`/`Returning(x => new { ... })`, `Single`/`ToList`, гейты) и `## Truncate` (`Truncate<T>().Execute()`, SQLite → `NotSupportedException`), заменить EN `:470`/RU `:474`; (2) `limitations.md` (EN+RU `:15`) — снять ложное «ClickHouse/in-memory отклоняют DELETE»; (3) `providers/overview.md` (EN+RU `:47`) — исправить DELETE-строку и добавить строки «TRUNCATE» и «Delete Returning (`RETURNING`/`OUTPUT deleted.<col>`)»; (4) `api-reference.md` (EN+RU) — строки `DeleteReturningBuilder<TEntity,TResult>`, `TruncateBuilder<TEntity>`, extension `Truncate<T>` и `DeleteBuilder.Returning`. Ссылки на `docs/specs/**` из публичных доков запрещены. **⛔ Отозвана 23.09.2026 — in-memory-мутации сняты; см. «Обновление 23.09.2026» под таблицей.** |
| DEL6 | P2 (форма/именование) | `Builders/TruncateBuilder.cs:37,54` ↔ `Builders/InsertBuilder.cs:54`, `Builders/MergeBuilder.cs:149`, `Builders/DeleteBuilder.cs:113` | **Терминал TRUNCATE выбивается из ряда.** У всех DML-билдеров терминал — SQL-глагол (`Insert()`/`Merge()`/`Delete()`/`DeleteAsync()`), а у `TruncateBuilder` — `Execute()`/`ExecuteAsync()`. Асимметрия `ctx.Truncate<T>().Execute()` vs `ctx.DeleteFrom<T>().Delete()`. Не баг, но поверхность замораживается Шагом 5, и переименование после заморозки станет ломающим | Решение владельца: либо переименовать в `Truncate()`/`TruncateAsync()` (согласованно с siblings; `Async`-суффикс корректен — sync-пара есть; любое переименование — одновременно `docs/**` и `docs/ru/**` по AGENTS.md), либо явно зафиксировать отклонение в release-notes/`linq2db-comparison.md` при заморозке. Публичную форму без решения не менять |
| DEL7 | P2 (трекинг) | `Builders/DeleteReturningBuilder.cs:14-92`; `Builders/TruncateBuilder.cs:12-73`; `Builders/DeleteBuilder.cs:84,102`; `DataContextExtensions.cs:132`; `ISqlDialect.cs:1085,1170,1177,1186,1196,1203,1212`; `SqlDialectBase.cs:703,737,740,743,746,749,752`; `PublicAPI.*.txt` отсутствуют | Публичная поверхность фазы 2 не трекается: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` нет (`find` — **0**; Шаг 5 открыт). **Продолжение IDML2/10/11/16/18/21/25/MRG2/DEL3, не новая проблема** | При заморозке внести подписи (точный текст — из анализатора): `DeleteReturningBuilder<TEntity,TResult>` + 5 методов; `TruncateBuilder<TEntity>` + 3; `DeleteBuilder<TEntity>.Returning()/.Returning<TResult>(Expression<Func<TEntity,TResult>>)`; `DataContextExtensions.Truncate<TEntity>(...)`; `ISqlDialect.MakeDeletedOutput/SupportsTruncate/MakeTruncate/MakeDeleteHead/DeleteRequiresWhere/MakeDeleteSuffix` + `SupportsDelete` и соответствующие `SqlDialectBase`/провайдерные override'ы |
| DEL8 | P2 (док/XML-полнота) | `Builders/DeleteReturningBuilder.cs:66-72` (`ToSql`), `:89-90` (`RequireExecutor`) | `ToSql()` не объявляет `<exception cref="NotSupportedException">`, хотя `TruncateBuilder.ToSql` (`:25`) и все терминалы исполнения его имеют (продолжение ℹ️ D фазы 1). Сообщение `RequireExecutor` — «the in-memory provider is read-only» — устарело: фаза 2 сделала in-memory мутируемым (`DELETE`/`TRUNCATE`), не хватает только `RETURNING`-строк; это регрессия исправленного в фазе 1 ℹ️ E (тогда из `DeleteBuilder.RequireExecutor` убрали ClickHouse) | Doc-only (маршрут — `nextorm-design-engineer`): добавить `<exception cref="NotSupportedException">` на `DeleteReturningBuilder.ToSql()` и переформулировать сообщение `RequireExecutor` («…cannot return deleted rows; the in-memory provider supports DELETE/TRUNCATE but not RETURNING»). CS1591 не нарушен — только полнота `<exception>`. **⛔ Отозвана 23.09.2026 — `RequireExecutor` больше не упоминает in-memory; см. «Обновление 23.09.2026» под таблицей.** |

**Обновление 23.09.2026 (uncommitted working tree) — in-memory-мутации отозваны; DEL5/DEL8 сняты.** Реализация in-memory `DELETE`/`TRUNCATE` **удалена**: `DataContext/InMemoryDataContext.Mutations.cs` и internal-роль `DataContext/Roles/IInMemoryMutationExecutor.cs` больше не существуют, ветки `_dataContext is IInMemoryMutationExecutor` из `DeleteBuilder`/`TruncateBuilder` сняты — in-memory снова **query-only**, `DELETE`/`TRUNCATE` (как и `INSERT`) бросают `NotSupportedException` с текстом `"<ctx> does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB)."`. **DEL5 — ⛔ отозвана/закрыта:** утверждение «in-memory **поддерживает** `DELETE`/`TRUNCATE` (`DeleteBuilderTests` 10)» больше неверно; публичные доки EN+RU, обновлённые в этом изменении, теперь точны — `docs/guide/20-delete-statement.md` (+RU) строка In-memory `NotSupportedException` (query-only), `docs/providers/overview.md` (+RU) строки `DELETE`/`TRUNCATE`/join → in-memory «throws `NotSupportedException`», `docs/advanced/limitations.md` (+RU, DML-строка) «no write terminal (`INSERT`/`DELETE`/`TRUNCATE`) is available on it», `docs/advanced/api-reference.md` (+RU) содержит строки `DeleteReturningBuilder<TEntity,TResult>`/`TruncateBuilder<TEntity>`; `DeleteBuilderTests` — **8/8**. **DEL8 — ⛔ отозвана/закрыта:** `DeleteReturningBuilder.ToSql()` (`:66`) объявляет `<exception cref="NotSupportedException">`, а `RequireExecutor` (`:85-92`) больше не говорит «the in-memory provider is read-only» — сообщение «`does not support returning removed rows. Use a database-backed context with RETURNING/OUTPUT (SQLite, PostgreSQL or SQL Server).`» непротиворечиво для query-only in-memory. Остальное по DEL5 (синхронный ClickHouse `ALTER TABLE ... DELETE ... SETTINGS mutations_sync = 1`, наличие `RETURNING`/`OUTPUT` и `TRUNCATE`, строки `api-reference`) уже отражено в доках. Фактически в секции: P1 — **0** (DEL5 снята), P2 — **2** оставшихся открытых (DEL6/DEL7; DEL8 снята). `MRG1`, `DEL1`, `DEL9` перепроверены — их in-memory-утверждения (`NotSupportedException` для merge / delete / join-delete) **остаются верными**, правок не требуют. Кодовая сторона — `code-smells-review.md`, секция фазы 2: Находки 105–106 и наблюдение I помечены отозванными.

ℹ️ **Наблюдения.** (1) `DeleteReturningBuilder<TEntity,TResult>` **зеркалит** `InsertReturningBuilder<TEntity,TResult>` (те же 5 членов, internal ctor, `sealed`), `Returning()`/`Returning(projection)` — `InsertBuilder`-формы; извлечение `ReturningProjection` убрало дублировавшийся парсер. (2) BCL-конфликтов нет (`DeleteReturningBuilder`/`TruncateBuilder`/`Truncate` отсутствуют в BCL). (3) `Async`-суффикс корректен (`Single`/`ToList` — sync-пары), `CancellationToken` — последним (CA1068). (4) Новые члены `ISqlDialect` — DIM (source/binary-совместимо); `SupportsDelete` уже был DIM фазы 1, ClickHouse-override снят (наследует `true`); при заморозке `ISqlDialect` — интерфейс с наибольшим изменением сигнатур (7 членов), внести в KC1-перечень. (5) `TruncateBuilder`/`DeleteReturningBuilder` без публичного ctor — только через `Truncate<T>()`/`Returning()`. (6) XML-doc новых публичных членов полон (build `0/0`, `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), CS1591 не подавлен; пробелы `<exception>` — DEL8. (7) Переименований в изменении нет; любое будущее потребует `docs/**` **и** `docs/ru/**` (AGENTS.md).

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.DeleteReturningBuilder` — **5** публичных методов + internal ctor; `roslyn members NextORM.Core.TruncateBuilder` — **3** + internal ctor; `roslyn members NextORM.Core.DeleteBuilder` — **7** публичных (в т.ч. `Returning`/`Returning<TResult>`); `roslyn members NextORM.Core.DataContextExtensions` — `DeleteFrom` `:81`/`Delete` `:97`/`DeleteAsync` `:115`/`Truncate` `:132`; `roslyn members NextORM.Core.ISqlDialect` — 7 новых членов; `find -name 'PublicAPI*.txt'` — **0** (DEL7/Шаг 5); `grep -rn 'DeleteReturningBuilder\|TruncateBuilder' docs/ --include=*.md` без `docs/specs` — **0** (DEL5); `SuppressMessage`/`pragma` по `src/` — 6/5 (все оправданные), `NoWarn` в `.csproj` — **0**. Тесты (Release): core `DeleteBuilderTests` **10/10**; `DeleteSqlGenerationTests` **34/34** по 6 провайдерам. Интеграция (Debug, `DOCKER_HOST`) — **29 passed / 3 skipped** по прогону автора (MySQL returning ×2, SQLite truncate ×1). Кодовая сторона — `code-smells-review.md`, Находки 105–106.



### Аудит 23.09.2026 — multi-table DELETE (join, фаза 3): extension-терминалы `Delete`/`DeleteAsync`/`ToSql` на `JoinedEntityBuilder<T1..T7>`, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 5)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет; фазы 1–2 (`DeleteFrom`/`Returning`/`Truncate`, DEL1–DEL8) остаются. Новое к заморозке Шага 5:

- **18** extension'ов `DataContextExtensions`: `Delete<T1..Tn>(this JoinedEntityBuilder<T1..Tn>)`, `DeleteAsync<T1..Tn>(..., CancellationToken = default)`, `ToSql<T1..Tn>(...)` для n = 2..7 — `DataContextExtensions.cs:135,147,158` (n=2), `:165,172,179` (n=3), `:186,193,200`, `:207,214,221`, `:228,235,242`, `:249,256,263` (n=7); плюс private-хелперы `PrepareDeleteJoinSource` `:269`, `RequireInnerJoinsForDelete` `:280`, `ExecuteDeleteJoin`/`ExecuteDeleteJoinAsync`/`RenderDeleteJoin`/`UnsupportedDeleteJoin` `:293,301,309,317`;
- **6** `new`-переопределений `Where` на `JoinedEntityBuilder<T1..Tn>` (n = 2..7) — `Builders/Joins/JoinedEntityBuilder.cs:120,226,326,426,526,626`; публичная форма существующих типов;
- `ISqlDialect` — **+3** публичных DIM (source/binary-совместимо): `SupportsDeleteJoin` `:1220`, `MakeDeleteJoin` `:1238`, `DeleteJoinUsesUsing` `:1252`; двойники `SqlDialectBase` `:755,761,775`; provider-override'ы: `PostgresDialect.SupportsDeleteJoin`/`DeleteJoinUsesUsing`/`MakeDeleteJoin` `:43,46,53`, `SqlServerDialect.SupportsDeleteJoin` `:46`, `MySqlDialect.SupportsDeleteJoin` `:43` (MariaDB наследует); SQLite/ClickHouse оставляют `false`;
- internal (не в поверхность): `DeleteJoinCommand` (`Query/Mutations/DeleteJoinCommand.cs:9`), `SqlStatementType.DeleteJoin` (`MutationCommand.cs:17`);
- существующие публичные типы, кроме `JoinedEntityBuilder<...>`, не меняли.

**Именование — P0 нет.** Extension-приёмник `JoinedEntityBuilder<T1..Tn>` — BCL-конфликтов нет (`Delete`/`ToSql`-расширения живут в `NextORM.Core`); перекрытия instance-членом нет — `roslyn members NextORM.Core.EntityBuilder` не содержит instance `ToSql`/`Delete` (проверено). `Async`-суффикс корректен (есть sync-пара `Delete`, AGENTS соблюдён); `CancellationToken` — последним (CA1068). `Supports*`/`Make*` — существующий паттерн `ISqlDialect`. Тип цели `T1` (первая таблица) согласуется с RFC `todo_delete_join.md`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| DEL9 | **P1 (док/противоречие)** | `docs/guide/19-insert-statement.md:506` (+RU `:510`); `docs/advanced/limitations.md:15` (+RU `:15`); `docs/providers/overview.md:47` (+RU `:47`); `docs/advanced/api-reference.md:41-43` (+RU) | **Публичные доки утверждают обратное фиче.** Guide 19 (EN `:506` / RU `:510`) — «there is deliberately … no `DELETE ... USING`/multi-table join (use a subquery predicate)»; `limitations.md:15` (EN+RU) — «`DELETE ... USING`/multi-table join … are not»; строка `DELETE` в `providers/overview.md:47` (EN+RU) не содержит join-форму; `api-reference` (EN+RU `:41-43`) не упоминает ни одной из 18 перегрузок. `grep -rn 'DeleteJoin\|multi-table' docs/ --include=*.md` без `docs/specs` — **0**. AGENTS.md требует EN **и** RU | Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) guide 19 (EN+RU) — снять «no multi-table join», добавить раздел «Deleting by a join» с примером `ctx.From<T1>().Join(...).Where(...).Delete()`, правилом «только INNER» и таблицей провайдеров (PG `DELETE ... USING`, SQL Server/MySQL/MariaDB `DELETE <alias> FROM ... JOIN`, SQLite/ClickHouse/in-memory → `NotSupportedException`); (2) `limitations.md` (EN+RU `:15`) — убрать `DELETE ... USING`/multi-table join из «not implemented»; (3) `providers/overview.md` (EN+RU `:47`) — добавить строку «DELETE by join (`JoinedEntityBuilder.Delete`)»; (4) `api-reference` (EN+RU) — описать extension-терминалы на `JoinedEntityBuilder<T1..Tn>`. Ссылки на `docs/specs/**` запрещены |
| DEL10 | P2 (док/XML-полнота) | `DataContextExtensions.cs:164-267` (15 методов n=3..7), `:135-162` (n=2) | XML-doc неполон: у 15 терминалов n=3..7 только `<summary>` (нет `<typeparam>`/`<param>`/`<returns>`); ни у одного из 18 нет `<exception cref="NotSupportedException">` (гейт SQLite/ClickHouse/in-memory) и проверки не-INNER join (`RequireInnerJoinsForDelete` `:280` бросает `NotSupportedException`). CS1591/CS1573 не срабатывают (нет частичных `<param>`), сборка `0/0` — это полнота, не гейт | Doc-only (маршрут — `nextorm-design-engineer`): дополнить `typeparam`/`param`/`returns` и `<exception>` по образцу arity-2 `Delete`/`DeleteAsync`/`ToSql` `:135,147,158`. Продолжение ℹ️ D/DEL8 |
| DEL11 | P2 (форма/дублирование поверхности) | `DataContextExtensions.cs:135-267`; `JoinedEntityBuilder.cs:120,226,326,426,526,626` | **18 почти идентичных extension'ов** (тело `ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)))` различается только арностью) и **6 почти идентичных `new Where`**. Поверхность Шага 5 раздувается 24 членами, а граница расширяемости неочевидна. Функционального дефекта нет — вопрос размера/DRY поверхности | Решение владельца перед заморозкой: ввести internal non-generic интерфейс (напр. `IJoinedEntityQuery { IDataContext DataProvider { get; } IReadOnlyList<JoinExpression>? Joins { get; } Type TargetType { get; } }`, реализуемый 7 `JoinedEntityBuilder`), тогда 18 методов сводятся к 3 (`Delete`/`DeleteAsync`/`ToSql`), а `RequireInnerJoins`/тип цели читаются из интерфейса. `Where`-переопределения оставить — они повторяют уже принятый в файле паттерн `new Global`/`WithStrictness`/`ArrayJoin`. Публичную форму без решения не менять. Кодовая сторона — `code-smells-review.md`, Находки 107–108 |
| DEL12 | P2 (полнота поверхности) | `JoinedEntityBuilder.cs:644` (`<T1..T8>`), `:548` (`.Join<T8>` из `<T1..T7>`) | Публичный `JoinedEntityBuilder<T1..T8>` создаётся через `.Join` на `<T1..T7>`, но не имеет ни `new Where`, ни терминалов `Delete`/`DeleteAsync`/`ToSql` (их максимум — 7 таблиц). Пользователь может построить 8-table join и упрётся в ошибку компиляции; асимметрия поверхности замораживается | Решение владельца: либо довести терминалы до arity 8 (24 → 27 членов), либо явно зафиксировать предел «7 таблиц» в guide/XML и release-notes |
| DEL13 | P2 (именование) | `ISqlDialect.cs:1252`; `SqlDialectBase.cs:775`; `PostgresDialect.cs:46` | `DeleteJoinUsesUsing` — boolean-предикат, выбивающийся из конвенций `Supports*`/`Is*`/`Has*` (рядом `SupportsDeleteJoin`). Не баг, но публичный DIM замораживается | Решение владельца: оставить и зафиксировать (тогда не менять), либо переименовать в `SupportsDeleteJoinUsing`/`IsDeleteJoinUsing`. Любое переименование — публичный API + `docs/**` **и** `docs/ru/**` (AGENTS.md) |
| DEL14 | P2 (трекинг) | `DataContextExtensions.cs:135-267`; `JoinedEntityBuilder.cs:120,226,326,426,526,626`; `ISqlDialect.cs:1220,1238,1252`; `SqlDialectBase.cs:755,761,775`; `PublicAPI.*.txt` отсутствуют | Новая поверхность (18 extension + 6 `Where` + 3 DIM + 3 override) не трекается: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение DEL3/DEL7/IDML2/…, не новая проблема** | При заморозке внести подписи (текст — из анализатора): 18 extension'ов, `JoinedEntityBuilder<T1..Tn>.Where`, `ISqlDialect.SupportsDeleteJoin/.MakeDeleteJoin/.DeleteJoinUsesUsing` + `SqlDialectBase`/провайдерные override'ы |

ℹ️ **Наблюдения.** (1) `DeleteJoinCommand` — `sealed class : MutationCommand` (ref-тип, полиморфный `switch` в `DataContext.BuildMutationSql` `:363`) — корректно; value-type-промахов нет, `MakeJoinParts` возвращает value-tuple, `FromRenderOptions` — `readonly record struct`. (2) RFC `todo_delete_join.md:57` перечисляет sync `Delete<T1,T2>(query, CancellationToken)`, а реализация (`DataContextExtensions.cs:135`) у sync-терминала токена не имеет (только `DeleteAsync` `:147`) — RFC устарел; для sync-терминала это, скорее, корректно, но план следует выровнять. (3) Перегрузки `Delete` безопасны: `Delete<TEntity>(this IDataContext, TEntity)` и `Delete<T1,T2>(this JoinedEntityBuilder<T1,T2>)` различаются приёмником; коллизия возможна только если `TEntity` сам `JoinedEntityBuilder<...>` и вызван `ctx.Delete(builder)` — тогда сработает key-форма (ошибка «нет ключа»), а не join-форма; маловероятно. (4) Остальные DML-терминалы — instance-методы (`InsertBuilder.Insert`, `DeleteBuilder.Delete`, `TruncateBuilder.Execute`); здесь терминал сделан extension'ом вынужденно (нельзя добавить член к 6 generic-классам без дублирования), но в IntelliSense он не виден как член типа. (5) XML-doc `MakeDeleteJoin`/`SupportsDeleteJoin`/`DeleteJoinUsesUsing` (`ISqlDialect.cs:1214-1252`) полон и точен. (6) `RequireInnerJoinsForDelete` (`:280`) выполняется при каждом вызове терминала (не кэшируется) — холодный путь. (7) Тесты: core `DeleteJoinBuilderTests` 7 (гейты + арности 4–7), SQL-gen PG 4 / SQL Server 2 / MySQL 1 / MariaDB 1 / SQLite 1 / ClickHouse 1, интеграция `CommonTestSuite.Delete.DeleteJoin_ShouldRemoveRowsMatchingAnotherTable` (`:139`) — 1; пробелы: нет SQL-gen-теста `.Where` на арности ≥3, `WithStrictness`/`Global` при delete, quoting (`CreateQuoted`) для PG USING, негативов `CrossJoin`/`SemiJoin`/`Apply`.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.DataContextExtensions` — 18 новых `Delete`/`DeleteAsync`/`ToSql` + приватные хелперы; `roslyn members NextORM.Core.EntityBuilder` — instance `ToSql`/`Delete` отсутствуют (extension не перекрыт); `grep` `new JoinedEntityBuilder<...> Where` в `JoinedEntityBuilder.cs` — 6 (`:120,226,326,426,526,626`); `JoinedEntityBuilder<T1..T8>` `:644` без терминалов (DEL12); `grep -rn 'DeleteJoin\|multi-table' docs/` без `docs/specs` — **0** (DEL9); `find PublicAPI*.txt` — **0** (DEL14/Шаг 5); `wc -l` — `DataContextExtensions.cs` 599, `JoinedEntityBuilder.cs` 677, `SqlSourceRenderer.cs` 796, `SqlBuilder.cs` 747. Сфокусированные тесты (Release, `--no-build`, `FullyQualifiedName~DeleteJoin`) — **17/17, 0 skipped**: core 7, postgres 4, sqlserver 2, mysql 1, mariadb 1, sqlite 1, clickhouse 1; интеграция не перезапускалась. Кодовая сторона — `code-smells-review.md`, Находки 107–108.

### Аудит 23.09.2026 — DML `UPDATE`: `Update<T>()`/`Update(entity)`/`UpdateAsync(entity)` + `UpdateBuilder<TEntity>`, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 3)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет. Новое к заморозке Шага 5:

- новый публичный тип `NextORM.Core.UpdateBuilder<TEntity>` (`Builders/UpdateBuilder.cs:19`, `sealed`, `internal` ctor `:26`) — **7** публичных методов: `Set<TValue>(Expression<Func<TEntity,TValue>>, TValue)` `:44`, `Set<TValue>(Expression<Func<TEntity,TValue>>, Expression<Func<TEntity,TValue>>)` `:64`, `Set(TEntity)` `:81`, `Where(Expression<Func<TEntity,bool>>)` `:104`, `ToSql()` `:119`, `Update()` `:131`, `UpdateAsync(CancellationToken = default)` `:139`;
- **3** extension'а `DataContextExtensions`: `Update<TEntity>(this IDataContext, Action<EntityMetadataBuilder<TEntity>>? = null)` `:69` (вход-билдер), `Update<TEntity>(this IDataContext, TEntity)` `:86` (терминал по ключу), `UpdateAsync<TEntity>(this IDataContext, TEntity, CancellationToken = default)` `:104`;
- **2** новых члена `ISqlDialect` (DIM, source/binary-совместимо): `SupportsUpdate => true` `:1260`, `MakeUpdateHead(string, KeywordCase)` `:1270`; двойники `SqlDialectBase` `:778,781`; provider-override: `ClickHouseDialect.SupportsUpdate => false` `:47` (MariaDB/MySQL/SQLite/SQL Server/PostgreSQL наследуют `true`);
- `KeyValue` извлечён из `DeleteKey` (`Query/Mutations/KeyValue.cs`) — internal, не в поверхность; internal `UpdateCommand`/`UpdateAssignment`/`UpdateValueKind`;
- существующие публичные типы членов не меняли.

Публичного конструктора у `UpdateBuilder<TEntity>` нет (`internal`), поэтому тип создаётся только через `Update<T>()` (как `InsertBuilder`/`MergeBuilder`/`DeleteBuilder`). Приложение A — исторический список (закрыт 22.09.2026), не расширяется; тип идёт в список заморозки Шага 5.

**Именование — P0 нет.** `Update<T>()` (вход), `Set`/`Where` (конфигурация), `Update`/`UpdateAsync`/`ToSql` (терминалы) — BCL-конфликтов нет (`UpdateBuilder` в BCL отсутствует; generic `Update`/`Set` не сталкиваются). `Set` — SQL-ориентирован (SET-список), согласован с формой. `Async`-суффикс корректен — есть sync-пара `Update` (AGENTS соблюдён); `CancellationToken` — последним (CA1068). `Supports*`/`Make*` — существующий паттерн `ISqlDialect`. XML-doc у всех новых публичных членов полный (summary/typeparam/param/returns, у терминалов — `<exception>`), CS1591 не подавлен.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| UPD1 | **P1 (док/противоречие)** | `docs/advanced/limitations.md:3,15` (+RU `:3,15`); `docs/guide/09-cte.md:269` (+RU `:271`); `docs/guide/19-insert-statement.md:305` (+RU); `docs/guide/20-delete-statement.md:114` (+RU); `docs/advanced/api-reference.md` (+RU); `docs/providers/overview.md` (+RU) | **Публичные доки утверждают обратное фиче.** `limitations.md:3` (EN+RU) — «deliberately leaves change tracking, `UPDATE` … to the caller»; `:15` (EN+RU) — «`UPDATE`, full `MERGE` … are not»; `guide/09-cte.md:269` (RU `:271`) — «`UPDATE` and `DELETE` bodies are planned and will live in their respective guides»; `guide/19-insert-statement.md:305` — «`UPDATE` will be documented in its own guide»; `guide/20-delete-statement.md:114` (RU) — «`UPDATE` … are not part of this surface». `UpdateBuilder`/`UpdateBuilder.Update` нигде не упомянуты (`rg 'UpdateBuilder' docs/ -g '!specs/**'` — **0**), отдельного `docs/guide/21-update-statement.md` нет (EN и RU), в `api-reference.md` нет строки `UpdateBuilder<TEntity>`/extension'ов `Update`, в capability-таблице `providers/overview.md` нет строки Update. AGENTS.md требует EN **и** RU. Ср. DEL1/MRG1 (закрыты тем же приёмом) | Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) `limitations.md` (EN+RU) — снять `UPDATE` из «not implemented», описать формы (`Set`/`Where`, `Update(entity)`), гейты (in-memory → `NotSupportedException`; ClickHouse → `SupportsUpdate=false`); (2) новый `docs/guide/21-update-statement.md` (+RU) либо раздел в guide 19 — примеры `ctx.Update<T>().Set(x => x.Name, "a").Where(...).Update()`, `Set(x => x.Age, x => x.Age + 1)`, `ctx.Update(entity)`, таблица провайдеров; (3) `guide/09-cte.md` (EN+RU) и `guide/19`/`guide/20` — снять «UPDATE будет/не реализован»; (4) `api-reference.md` (EN+RU) — строка `UpdateBuilder<TEntity>` + три extension'а; (5) `providers/overview.md` (EN+RU) — строка `UPDATE`. Ссылки на `docs/specs/**` из публичных доков запрещены |
| UPD2 | P2 (трекинг) | `Builders/UpdateBuilder.cs:19-139`; `DataContextExtensions.cs:69,86,104`; `ISqlDialect.cs:1260,1270`; `SqlDialectBase.cs:778,781`; `ClickHouseDialect.cs:47`; `PublicAPI.*.txt` отсутствуют | Публичный тип + 7 членов + 3 extension'а + 2 DIM + 3 override'а не трекаются: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение DEL3/DEL7/MRG2/IDML2/…, не новая проблема** | При заморозке внести подписи (точный текст — из анализатора): `UpdateBuilder<TEntity>` + `Set<TValue>`×2/`Set`/`Where`/`ToSql`/`Update`/`UpdateAsync`; `DataContextExtensions.Update<TEntity>(Action<EntityMetadataBuilder<TEntity>>?)`/`Update<TEntity>(TEntity)`/`UpdateAsync<TEntity>(TEntity, CancellationToken)`; `ISqlDialect.SupportsUpdate.get` + `MakeUpdateHead` и override'ы `SqlDialectBase`/`ClickHouseDialect` |
| UPD3 | P2 (форма/именование) | `DataContextExtensions.cs:69,86`; `Builders/UpdateBuilder.cs:131` | **Вход выбивается из ряда DML-входов.** `InsertInto`/`MergeInto`/`DeleteFrom` построены как глагол+предлог, а UPDATE — `Update<T>()` без предлога; поскольку `IDataContext` — маркерный интерфейс, имя `Update` в одной поверхности используется трижды (вход `ctx.Update<T>()`, терминал по ключу `ctx.Update(entity)`, терминал билдера `.Update()`), а `ctx.Update<T>()` читается как терминал, хотя возвращает билдер. Не баг; поверхность замораживается Шагом 5 — переименование после заморозки станет ломающим | Решение владельца: либо зафиксировать как есть (линия linq2db/EF `context.Update`) и записать в release-notes/`linq2db-comparison.md` (EN+RU), либо переименовать вход (напр. `UpdateIn`/`UpdateFrom`). Любое переименование — публичный API **и** `docs/**` **и** `docs/ru/**` (AGENTS.md). Публичную форму без решения не менять |
| UPD4 | P2 (док/XML-полнота) | `DataContextExtensions.cs:86-92` (`Update(entity)`), `:104-110` (`UpdateAsync(entity)`) | XML-doc терминалов по ключу не объявляет `<exception cref="NotSupportedException">`, хотя in-memory-контекст бросает его в `UpdateBuilder.RequireExecutor` (нет `IMutationExecutor`), а ClickHouse — в `DataContext.BuildUpdateSql` по `SupportsUpdate=false`. Терминалы билдера (`Update`/`UpdateAsync`/`ToSql`, `UpdateBuilder.cs:119-140`) его объявляют. CS1591 не нарушен (build `0/0`) — это полнота контракта. Продолжение DEL8/ℹ️ D фазы DELETE | Doc-only (маршрут — `nextorm-design-engineer`): добавить `<exception cref="NotSupportedException">` в `<summary>`-блоки `Update(entity)`/`UpdateAsync(entity)` по образцу `UpdateBuilder.Update` |

ℹ️ **Наблюдения.** (1) `UpdateBuilder<TEntity>` `sealed` + `internal` ctor — снаружи только через `Update<T>()` (как `MergeBuilder`/`DeleteBuilder`). (2) Новые члены `ISqlDialect` — **DIM** (source/binary-совместимы), поэтому внешние реализации не ломаются; `ClickHouseDialect` обязан переопределить `SupportsUpdate => false` (переопределяет); при заморозке внести в KC1-перечень изменённых интерфейсных сигнатур. (3) Перегрузки `Set` безопасны: `Set(x => x.Name, "a")` → constant-форма, `Set(x => x.Name, x => x.Name)` → expression-форма; несовпадение типов (`Set(x => x.Name, x => x.Id)`) — ошибка компиляции (ожидаемо). Захваченное **свойство** в expression-форме отвергается — это кодовая сторона (`code-smells-review.md`, Находка 109), а не именование. (4) Форма XML-doc `Update<T>()` ссылается на `UpdateBuilder<TEntity>` корректным `<see cref>`; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, build `0/0`. (5) Surface-lock: `find PublicAPI*.txt` — **0**, Шаг 5 открыт (UPD2). (6) Переименований в изменении нет; любое будущее потребует `docs/**` **и** `docs/ru/**` (AGENTS.md). (7) `docs/specs/roadmap/todo_update.md` обновлён (статус «в работе (фаза 1)») — это internal-план, не публичная поверхность.

**Применено (23.09.2026, после аудита).** UPD1 закрыт: `limitations.md` (EN+RU `:3,15`), `guide/09-cte.md` (EN+RU), `guide/19`/`guide/20` (EN+RU), `api-reference` (EN+RU) и `providers/overview` (EN+RU) обновлены; создан `docs/guide/21-update-statement.md` + RU-зеркало и записи в `docs/guide/toc.yml` + `docs/ru/toc.yml`. UPD4 закрыт: `<exception cref="NotSupportedException">` добавлен в XML-doc `Update(entity)`/`UpdateAsync(entity)`. UPD2 (surface-lock) и UPD3 (имя входа `Update`) остаются на решение владельца до заморозки Шага 5.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.UpdateBuilder` — **7** публичных методов + internal ctor; `roslyn members NextORM.Core.DataContextExtensions` — `Update` `:69`/`Update(entity)` `:86`/`UpdateAsync` `:104`; `rg 'SupportsUpdate|MakeUpdateHead'` — `ISqlDialect.cs:1260,1270`, `SqlDialectBase.cs:778,781`, `ClickHouseDialect.cs:47`; `rg 'UpdateBuilder' docs/ -g '!specs/**'` — **0** (UPD1); `find -name 'PublicAPI*.txt'` — **0** (UPD2/Шаг 5); `NoWarn` в `.csproj` — **0**; `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`), build `0/0` ⇒ XML-doc новых публичных членов полон. Тесты (Release): core `UpdateBuilderTests` **4/4**; `UpdateSqlGenerationTests` **38/38** по 6 провайдерам (sqlite 6 / postgres 12 / sqlserver 6 / mysql 6 / mariadb 6 / clickhouse 2); интеграция (`DOCKER_HOST`, Debug) — **20/20, 0 skipped** (SQLite/PostgreSQL/SQL Server/MySQL). Кодовая сторона — `code-smells-review.md`, Находки 109–112.

### Аудит 23.09.2026 — DML `UPDATE`, фаза 2: `RETURNING`/`OUTPUT` (`UpdateReturningBuilder<TEntity,TResult>`), uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 1)

Продолжение раздела «Аудит 23.09.2026 — DML `UPDATE` (фаза 1): `Update<T>()`/`Update(entity)`/`UpdateAsync(entity)` + `UpdateBuilder<TEntity>` … UPD1–UPD4».

**Добавлено на публичную поверхность.**
- новый публичный тип `NextORM.Core.UpdateReturningBuilder<TEntity,TResult>` (`src/nextorm.core/Builders/UpdateReturningBuilder.cs:13`, `sealed`, ctor `internal` `:20`) — **5** публичных методов: `Single() -> TResult` `:36`; `SingleAsync(CancellationToken = default) -> Task<TResult>` `:44`; `ToList() -> IReadOnlyList<TResult>` `:50`; `ToListAsync(CancellationToken = default) -> Task<IReadOnlyList<TResult>>` `:57`; `ToSql() -> string` `:66`;
- `UpdateBuilder<TEntity>` +**2** члена (было 7 → **9**): `Returning() -> UpdateReturningBuilder<TEntity,TEntity>` (`Builders/UpdateBuilder.cs:119`); `Returning<TResult>(Expression<Func<TEntity,TResult>>) -> UpdateReturningBuilder<TEntity,TResult>` (`:137`).

**P0/P1 по именам — нет.** `UpdateReturningBuilder` зеркалит sibling'ов `DeleteReturningBuilder`/`InsertReturningBuilder` (`Builder`-суффикс, `sealed`, `internal` ctor, те же 5 терминалов `Single`/`SingleAsync`/`ToList`/`ToListAsync`/`ToSql`); `Returning()`/`Returning<TResult>` — точные копии перегрузок `InsertBuilder`/`DeleteBuilder` (единый DSL-паттерн). BCL-конфликтов нет (`UpdateReturningBuilder` в BCL отсутствует). `Async`-суффикс корректен (есть sync-пара), `CancellationToken` — последним (CA1068), возвраты — `TResult`/`IReadOnlyList<TResult>`/`Task<...>`. Переименований нет.

**XML-doc.** Весь новый публичный API документирован. Build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — **0** (CS1591 не подавлен) ⇒ покрытие 100 %. `ToSql()` **объявляет** `<exception cref="NotSupportedException">` (`:65`) — в отличие от DEL8 фазы DELETE; `Single`/`SingleAsync` дополнительно объявляют `InvalidOperationException`. Пробел `<exception>` key-формы `Update(entity)` — UPD4 (закрыт ранее).

**Findings.**

| ID | Приоритет | Место | Суть | Действие |
|----|-----------|-------|------|----------|
| UPD5 | **P1 (док/противоречие)** | `docs/advanced/limitations.md:3,15` (+RU `:3,15`); `docs/guide/21-update-statement.md:105-106` (+RU `:106-107`); `docs/advanced/api-reference.md:43` (+RU `:43`); `docs/providers/overview.md:48-54` (+RU `:48-54`) | **Публичные доки противоречат фазе 2.** `limitations.md:3` (EN+RU) — «deliberately leaves change tracking, **returning updated rows** … to the caller»; `:15` (EN+RU) — «**Returning the updated rows** (`RETURNING`/`OUTPUT`) … are not»; guide 21 EN `:105-106`/RU `:106-107` — «Returning the updated rows … are **not** part of this surface», и таблица провайдеров (`:92-99`/`:93-100`) не содержит строки возврата; `api-reference` (EN `:43`/RU `:43`) описывает `UpdateBuilder<TEntity>` без `Returning()`/`Returning(projection)` и **не содержит строки** `UpdateReturningBuilder<TEntity,TResult>` (ср. `InsertReturningBuilder` EN/RU `:39`, `DeleteReturningBuilder` EN/RU `:44`); в `providers/overview.md` (EN+RU) нет строки `UPDATE … RETURNING` (есть DELETE `:49` и INSERT `:54`). `rg 'UpdateReturningBuilder' docs/ -g '!specs/**'` — **0**. AGENTS.md требует EN **и** RU. Ср. DEL5/UPD1 (закрыты тем же приёмом) | Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) `limitations.md` (EN+RU `:3,15`) — снять «returning updated rows … not», описать возврат обновлённых строк на PostgreSQL/SQLite (`RETURNING`) и SQL Server (`OUTPUT inserted.<col>`); (2) guide 21 (EN+RU) — новый раздел `## Returning the updated rows`/`## Возврат обновлённых строк` (формы `Returning()`/`Returning(x => new { … })`, `Single`/`SingleAsync`/`ToList`/`ToListAsync`, гейты MySQL/MariaDB/ClickHouse → `NotSupportedException`, in-memory → `NotSupportedException`), убрать пункт «not part of this surface», добавить строку возврата в таблицу провайдеров; (3) `api-reference` (EN+RU) — строка `UpdateReturningBuilder<TEntity,TResult>` и упоминание `Returning()`/`Returning(projection)` в строке `UpdateBuilder<TEntity>`; (4) `providers/overview` (EN+RU) — строка `UPDATE … RETURNING` (`RETURNING` / `OUTPUT inserted.<col>` / `NotSupportedException`). Ссылки на `docs/specs/**` из публичных доков запрещены |
| UPD6 | P2 (трекинг) | `Builders/UpdateReturningBuilder.cs:13,36,44,50,57,66`; `Builders/UpdateBuilder.cs:119,137`; `PublicAPI.*.txt` отсутствуют | Новая публичная поверхность не трекается: `UpdateReturningBuilder<TEntity,TResult>` + 5 членов; `UpdateBuilder<TEntity>.Returning()/.Returning<TResult>` (7→9). **Продолжение UPD2**, не новая проблема: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт) | При заморозке внести подписи (точный текст — из анализатора): `UpdateReturningBuilder<TEntity,TResult>` + `.Single()/.SingleAsync(CancellationToken)/.ToList()/.ToListAsync(CancellationToken)/.ToSql()`; `UpdateBuilder<TEntity>.Returning()/.Returning<TResult>(Expression<Func<TEntity,TResult>>)` |

**ℹ️ Наблюдения.** (1) XML-summary `Returning()` (`UpdateBuilder.cs:112-118`): «The predicate form is required: the key form (Update(entity)) does not expose Returning» — key-форма действительно невыразима (проверено: `Update(entity)`/`UpdateAsync(entity)` возвращают `int`/`Task<int>`, `BuildReturningCommand` всегда `keys: null`), но фраза читается как «`Where` обязателен», хотя `Update<T>().Set(...).Returning()` без `Where` корректно возвращает все обновлённые строки (ср. guide 21 §«Updating every row»). Стоит уточнить: доступно только на билдере `Update<T>()`; key-форма его не предоставляет. (2) Фаза 2 **не добавила** членов `ISqlDialect` — переиспользованы `SupportsReturning`/`SupportsOutput`/`MakeReturning`/`MakeOutput` INSERT-фазы; `MakeOutput` даёт `inserted.<col>`, корректно и для `UPDATE` (см. `code-smells-review.md`, ℹ️ A). (3) `ITestProvider.SupportsInsertReturning` теперь гейтит и UPDATE-, и DELETE-возврат — тестовая поверхность, кандидат на `SupportsReturning` (кодовая сторона — ℹ️ F). (4) Поверхность не заморожена (uncommitted), переименований нет; любое будущее затронет `docs/**` **и** `docs/ru/**`.

**Применено (23.09.2026, после аудита).** UPD5 закрыт: `limitations.md` (EN+RU `:3,15`) снимает «returning updated rows … not» и описывает возврат обновлённых строк; guide 21 (EN+RU) получил раздел «Returning the updated rows»/«Возврат обновлённых строк» и строку возврата в матрице провайдеров; `api-reference` (EN+RU) получил строку `UpdateReturningBuilder<TEntity,TResult>` и упоминание `Returning()`/`Returning(projection)`; `providers/overview` (EN+RU) получил строку `UPDATE … RETURNING`. UPD6 (surface-lock) остаётся на заморозку Шага 5.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.UpdateReturningBuilder'2` — **5** публичных методов + internal ctor; `roslyn members NextORM.Core.UpdateBuilder'1` — **9** публичных методов + internal ctor; `rg 'UpdateReturningBuilder' docs/ -g '!specs/**'` — **0** (UPD5); `find -name 'PublicAPI*.txt'` — **0** (UPD6/Шаг 5); `NoWarn` по 7 `.csproj` — **0**, `GenerateDocumentationFile=true`, build `0/0` ⇒ XML-doc-покрытие полное; новых `ISqlDialect`-членов — 0. Тесты (Release, `--no-build`): core `UpdateBuilderTests` **5/5**; `UpdateSqlGenerationTests` **46/46** по 6 провайдерам; интеграция `Update_Returning*` — по отчёту автора. Кодовая сторона — `code-smells-review.md`, Находки 113–114.

### Аудит 23.09.2026 — DML `UPDATE`, финальный инкремент: multi-table `UPDATE ... FROM`/join (`UpdateJoinBuilder<TProjection>`) + ClickHouse `ALTER TABLE ... UPDATE`, uncommitted working tree (P0 — нет; P1 — нет; P2 — 2)

Продолжение разделов «Аудит 23.09.2026 — DML `UPDATE` … UPD1–UPD4» и «… фаза 2: `RETURNING`/`OUTPUT` … UPD5–UPD6».

**Добавлено на публичную поверхность.** Поверхность аддитивна, переименований нет.

- новый публичный тип `NextORM.Core.UpdateJoinBuilder<TProjection>` (`src/nextorm.core/Builders/UpdateJoinBuilder.cs:19`, `sealed`, ctor `internal` `:25`) — **6** публичных методов: `Set<TValue>(Expression<Func<TProjection,TValue>>, TValue) -> UpdateJoinBuilder<TProjection>` `:40`; `Set<TValue>(Expression<Func<TProjection,TValue>>, Expression<Func<TProjection,TValue>>) -> UpdateJoinBuilder<TProjection>` `:58`; `Where(Expression<Func<TProjection,bool>>) -> UpdateJoinBuilder<TProjection>` `:73`; `ToSql() -> string` `:87`; `Update() -> int` `:99`; `UpdateAsync(CancellationToken = default) -> Task<int>` `:107`;
- **7** extension'ов `DataContextExtensions.UpdateJoin<T1..T8>(this JoinedEntityBuilder<T1..T8>) -> UpdateJoinBuilder<Projection<T1..T8>>` (`src/nextorm.core/DataContext/DataContextExtensions.cs:520,532,545,559,574,590,607`);
- **5** новых члена `ISqlDialect` (DIM, source/binary-совместимо): `UpdateRequiresWhere => false` `:1279`, `MakeUpdateSuffix(...) => null` `:1288`, `SupportsUpdateJoin => false` `:1296`, `UpdateJoinRequiresFrom => false` `:1305`, `MakeUpdateJoin(...)` `:1324`; двойники `SqlDialectBase` `:784,787,790,793,800`;
- override'ы: `PostgresDialect` `SupportsUpdateJoin`/`UpdateJoinRequiresFrom`/`MakeUpdateJoin` `:75,78,85`; `SqliteDialect` `:25,27,34`; `SqlServerDialect` `:49,55`; `MySqlDialect` `:46,53` (MariaDB наследует); `ClickHouseDialect` — **`SupportsUpdate` `false` → `true`** `:47` + `MakeUpdateHead`/`UpdateRequiresWhere`/`MakeUpdateSuffix` `:50,53,56` (публичное свойство сменило значение, подпись — нет);
- `JoinedEntityBuilder<T1..T8>` получили модификатор `partial` (`:13,131,239,341,443,545,656`) — метаданные/бинарь не меняются (мёртвый модификатор — кодовая сторона, ℹ️ B).

**Именование — P0/P1 нет.** `UpdateJoinBuilder` — суффикс `Builder`, `sealed` + `internal` ctor, как `InsertBuilder`/`MergeBuilder`/`DeleteBuilder`/`UpdateBuilder`; `Set`/`Where`/`ToSql`/`Update`/`UpdateAsync` — точные копии `UpdateBuilder` (единый DSL). `Async`-суффикс корректен (есть sync-пара `Update`), `CancellationToken` последним (CA1068), возвраты `int`/`Task<int>`/`string`. `SupportsUpdateJoin`/`UpdateJoinRequiresFrom`/`MakeUpdateJoin` зеркалят `SupportsDeleteJoin`/`DeleteJoinRequiresUsing`/`MakeDeleteJoin`; `UpdateRequiresWhere`/`MakeUpdateSuffix`/`MakeUpdateHead` — `DeleteRequiresWhere`/`MakeDeleteSuffix`/`MakeDeleteHead`. BCL-конфликтов нет.

**XML-doc.** Весь новый публичный API задокументирован (summary/typeparam/param/returns; у терминалов — `<exception>`). Build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — **0** (CS1591 не подавлен) ⇒ покрытие 100 %. Полнота `<exception>`: `Set` объявляет `ArgumentException` (`UpdateJoinBuilder.cs:39`), терминалы — `InvalidOperationException`+`NotSupportedException` (`:85,97,105`); `Where` `ArgumentNullException` не объявляет (CS1591 не затронут) — косметика, как DEL8/UPD4.

| ID | Приоритет | Место | Суть | Действие |
|----|-----------|-------|------|----------|
| UPD7 | **P2 (трекинг)** | `Builders/UpdateJoinBuilder.cs:19,40,58,73,87,99,107`; `DataContextExtensions.cs:520-611`; `ISqlDialect.cs:1279-1340`; `SqlDialectBase.cs:784-812`; 5 провайдерских override'ов; `PublicAPI.*.txt` отсутствуют | Публичный тип + 6 членов + 7 extension'ов + 5 DIM + override'ы не трекаются: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение UPD2/UPD6**, не новая проблема | При заморозке внести подписи (точный текст — из анализатора): тип `UpdateJoinBuilder<TProjection>` + 6 методов; 7 `DataContextExtensions.UpdateJoin<T1..Tn>(JoinedEntityBuilder<...>)`; 5 DIM (`UpdateRequiresWhere`/`MakeUpdateSuffix`/`SupportsUpdateJoin`/`UpdateJoinRequiresFrom`/`MakeUpdateJoin`) и их override'ы |
| UPD8 | P2 (именование/DSL-консистентность) | `DataContextExtensions.cs:520`; ср. `Delete<T1,T2>` `:192` (терминал) | Асимметрия DML-поверхности: multi-table `DELETE` — extension-**терминал** `.Delete()`/`.DeleteAsync()` (`int`/`Task<int>`), а multi-table `UPDATE` — extension-**вход** `.UpdateJoin()` → билдер; имя `UpdateJoin` не содержит предлога (продолжение UPD3 про `ctx.Update<T>()`) и читается как терминал. Вариант переименования — `UpdateFrom`/`Set`-вход. Не дефект; поверхность замораживается Шагом 5 — переименование после станет ломающим | Решение владельца до заморозки: зафиксировать как есть (линия EF/linq2db) и отметить в release-notes, либо переименовать вход. Любое переименование — публичный API **и** `docs/**` **и** `docs/ru/**` (AGENTS.md). Публичную форму без решения не менять |

**ℹ️ Наблюдения.** (1) `ClickHouseDialect.SupportsUpdate` `false` → `true` (`src/nextorm.clickhouse/ClickHouseDialect.cs:47`) — публичное свойство сменило **значение** (подпись/бинарность не меняются): ClickHouse теперь поддержан через `ALTER TABLE ... UPDATE`. Отразить в release-notes/заморозке. При этом override **избыточен** — база `SqlDialectBase.SupportsUpdate => true` (`:778`) и DIM тоже `true` (кодовая сторона — `code-smells-review.md`, ℹ️ A). (2) Новые члены `ISqlDialect` — DIM (source/binary-совместимы), внешние реализации интерфейса не ломаются; внести в KC1-перечень при заморозке. (3) **Публичная документация обновлена в этом же изменении — P1 нет (в отличие от UPD1/UPD5):** `docs/guide/21-update-statement.md` + RU-зеркало содержат раздел `Updating from a join`/`Обновление из join` (EN `:113-148` / RU `:114-149`) с матрицей провайдеров; `docs/advanced/limitations.md:15` (+RU) описывает multi-table UPDATE и гейты (ClickHouse/in-memory отклоняют); `docs/advanced/api-reference.md:45` (+RU) содержит строку `UpdateJoinBuilder<TProjection>`; `docs/providers/overview.md:50` (+RU) — строку `UPDATE ... FROM (UpdateJoin)`. `rg 'UpdateJoin' docs/ -g '!specs/**'` — **8** файлов (EN+RU). (4) `UpdateJoinBuilder<TProjection>` раскрывает позиционный `Projection<...>` и DSL `p.Item1.Name` — согласовано с поверхностью join-запросов (`EntityBuilder<Projection<...>>`). (5) Переименований нет; поверхность uncommitted; `docs/specs/**`-обновления — internal-план, не публичная поверхность.

**Применено (23.09.2026, после аудита).** Кодовые наблюдения ℹ️ A/ℹ️ B закрыты: избыточный `ClickHouseDialect.SupportsUpdate => true` удалён (база `true`), мёртвый `partial` с 7 `JoinedEntityBuilder` снят; рефакторинги 116/117/118 — см. `code-smells-review.md` «Применено». UPD7 (surface-lock) остаётся на заморозку Шага 5; UPD8 (имя входа `UpdateJoin` vs терминал `Delete`) — решение владельца до заморозки, публичную форму не меняли.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.UpdateJoinBuilder'1` — **6** публичных методов + internal ctor; `roslyn members NextORM.Core.DataContextExtensions` — `UpdateJoin` `:520,532,545,559,574,590,607`; `rg 'SupportsUpdateJoin|UpdateJoinRequiresFrom|MakeUpdateJoin|UpdateRequiresWhere|MakeUpdateSuffix'` — ISqlDialect `:1279,1288,1296,1305,1324` / SqlDialectBase `:784-812` / PG `:75,78,85` / SQLite `:25,27,34` / SqlServer `:49,55` / MySQL `:46,53` / ClickHouse `:47,50,53,56`; `rg 'UpdateJoin' docs/ -g '!specs/**'` — **8** файлов EN+RU (P1 нет); `find -name 'PublicAPI*.txt'` — **0** (UPD7/Шаг 5); `NoWarn` по `.csproj` — **0**, `GenerateDocumentationFile=true`, build `0/0` ⇒ XML-doc-покрытие полное. Тесты (Release, `--no-build`): `~UpdateJoin` — **14/14, 0 skipped** (postgres 5 / sqlite 2 / sqlserver 2 / mysql 2 / mariadb 2 / clickhouse 1); core `~UpdateBuilder` — **6/6**; ClickHouse `~UpdateSqlGeneration` — **5/5**. Контейнерная интеграция аудитом не перезапускалась (по отчёту автора — 2879 total, 0 failed, 48 skipped). Кодовая сторона — `code-smells-review.md`, Находки 115–119.

### Аудит 23.09.2026 — DML `MERGE` (фаза 2): полный `MERGE` с ветками, `RETURNING`/`OUTPUT` и источник-запрос, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 3)

**Область (uncommitted worktree).** Продолжение раздела «Аудит 23.09.2026 — DML `MERGE` (key upsert) … MRG1–MRG5». Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- **4** новых публичных типа:
  - `NextORM.Core.MergeMatchedBuilder<TEntity>` (`src/nextorm.core/Builders/MergeBranchBuilders.cs:11`, `sealed`, `internal` ctor `:15`) — **3** публичных метода: `ThenUpdate() -> MergeBuilder<TEntity>` `:19`, `ThenUpdate(Expression<Func<TEntity, object?>>) -> MergeBuilder<TEntity>` `:24`, `ThenDelete() -> MergeBuilder<TEntity>` `:29`;
  - `NextORM.Core.MergeNotMatchedBuilder<TEntity>` (`:38`, ctor `:42`) — **2**: `ThenInsert() -> MergeBuilder<TEntity>` `:46`, `ThenInsert(Expression<Func<TEntity, object?>>) -> MergeBuilder<TEntity>` `:51`;
  - `NextORM.Core.MergeNotMatchedBySourceBuilder<TEntity>` (`:61`, ctor `:65`) — **1**: `ThenDelete() -> MergeBuilder<TEntity>` `:69` (только SQL Server);
  - `NextORM.Core.MergeReturningBuilder<TEntity,TResult>` (`src/nextorm.core/Builders/MergeReturningBuilder.cs:13`, `sealed`, ctor `:20`) — **5**: `Single() -> TResult` `:36`, `SingleAsync(CancellationToken = default) -> Task<TResult>` `:43`, `ToList() -> IReadOnlyList<TResult>` `:49`, `ToListAsync(CancellationToken = default) -> Task<IReadOnlyList<TResult>>` `:55`, `ToSql() -> string` `:64`;
- `MergeBuilder<TEntity>` — **+7** публичных членов (итого **15**): `Using(EntityBuilder<TEntity>)` `:102`, `Using(QueryCommand<TEntity>)` `:112`, `WhenMatched() -> MergeMatchedBuilder<TEntity>` `:175`, `WhenNotMatched() -> MergeNotMatchedBuilder<TEntity>` `:182`, `WhenNotMatchedBySource() -> MergeNotMatchedBySourceBuilder<TEntity>` `:190`, `Returning() -> MergeReturningBuilder<TEntity,TEntity>` `:198`, `Returning<TResult>(Expression<Func<TEntity,TResult>>) -> MergeReturningBuilder<TEntity,TResult>` `:215`;
- **6** новых членов `ISqlDialect` (DIM, source/binary-совместимы): `SupportsMergeStatement => false` `:1170`, `SupportsMergeDelete => false` `:1176`, `SupportsMergeBySourceDelete => false` `:1182`, `SupportsMergeTargetQualification => true` `:1189`, `MakeMergeStatementTerminator(KeywordCase = Lower) => ""` `:1195`, `MakeMergeReturning(IReadOnlyList<string>, KeywordCase = Lower)` `:1201`; `SqlDialectBase`-двойники `:734,737,740,743,746,752`; override'ы: PostgreSQL `SupportsMergeStatement`/`SupportsMergeDelete`/`SupportsMergeTargetQualification => false`/`MakeMergeReturning` (`src/nextorm.postgres/PostgresDialect.cs:31,34,37,40`), SQL Server `SupportsMergeStatement`/`SupportsMergeDelete`/`SupportsMergeBySourceDelete`/`MakeMergeStatementTerminator => ";"` (`src/nextorm.sqlserver/SqlServerDialect.cs:73,76,79,82`);
- internal `MergeBranch`/`MergeMatchKind`/`MergeActionKind` (`src/nextorm.core/Query/Mutations/MergeCommand.cs:4,15,29`) и новые члены `MergeCommand` (`Branches` `:99`, `ReturningColumns` override `:102`, `Source` `:109`) — **не** публичная поверхность;
- существующие публичные типы подписей не поменяли (аддитивно); `MergeCommand` теперь переопределяет `ReturningColumns` (`MutationCommand.cs:53`) — контракт, введённый фазой UPDATE (см. Находку 114).

Публичного конструктора ни у одного нового типа нет (`internal`), поэтому они создаются только терминалами `MergeBuilder` (`WhenMatched()`/`WhenNotMatched()`/`WhenNotMatchedBySource()`/`Returning()`); `MergeReturningBuilder` — только через `Returning()`/`Returning<TResult>()`. Приложение A — исторический список (закрыт 22.09.2026), не расширяется; 4 типа идут в список заморозки Шага 5.

**Именование — P0 нет.** Суффикс `Builder` у всех четырёх; `sealed` + `internal` ctor, как `InsertBuilder`/`DeleteBuilder`/`UpdateBuilder`/`UpdateJoinBuilder` и их `*ReturningBuilder`. `WhenMatched()`/`WhenNotMatched()`/`WhenNotMatchedBySource()`/`ThenUpdate()`/`ThenInsert()`/`ThenDelete()` — SQL-ориентированные (SQL Server `WHEN MATCHED`/`WHEN NOT MATCHED [BY TARGET]`/`WHEN NOT MATCHED BY SOURCE`, `THEN UPDATE/INSERT/DELETE`), совпадают с RFC `todo_merge.md` и linq2db-линией. `MergeReturningBuilder<TEntity,TResult>` симметричен `InsertReturningBuilder`/`DeleteReturningBuilder`/`UpdateReturningBuilder`. BCL-конфликтов нет (`MergeMatchedBuilder`/`MergeNotMatchedBuilder`/`MergeNotMatchedBySourceBuilder`/`MergeReturningBuilder` в BCL отсутствуют). `Async`-суффикс корректен — есть sync-пары `Single`/`ToList` (AGENTS соблюдён); `CancellationToken` последним (CA1068). XML-doc у всех новых публичных типов и членов полный (summary/typeparam/param/returns/exception), CS1591 не подавлен. `Supports*`/`Make*` — существующий паттерн `ISqlDialect`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| MRG6 | **P1 (док/противоречие)** | `docs/advanced/limitations.md:3,15` (+`docs/ru/advanced/limitations.md:3,15`); `docs/guide/19-insert-statement.md:431,466-467` (+RU `:434,468-469`); `docs/advanced/api-reference.md:40` (+RU); `docs/providers/overview.md:46` (+RU) | **Публичные доки утверждают обратное фазе 2.** `limitations.md:3` (EN+RU) — «намеренно оставляет … полный `MERGE` с ветками вызывающей стороне»; `:15` — «Full `MERGE` with arbitrary `WHEN MATCHED`/`WHEN NOT MATCHED` branches and `MERGE ... RETURNING` are not.» / RU «Полный `MERGE` … и `MERGE ... RETURNING` — нет». guide 19 `:431` (EN; RU `:434`) — «This is the *key upsert* form only — a full `MERGE` … (including `DELETE`) is not part of this surface»; `:466-467` / RU `:468-469` — «Key upsert, no full `MERGE`». `api-reference.md:40` / RU описывает `MergeBuilder` только как key-upsert; строк для 4 новых типов нет. `providers/overview.md:46` / RU — только строка «Key upsert», полного `MERGE` нет. Новые терминалы (`WhenMatched`/`ThenUpdate`/`ThenDelete`/`WhenNotMatchedBySource`/`Returning`) в публичных доках — **0** (`rg -i 'WhenMatched\(|ThenUpdate|ThenDelete|WhenNotMatchedBySource|MergeReturningBuilder|MergeMatchedBuilder' docs/ -g '!specs/**'` — совпадения только во внутреннем `docs/specs/roadmap/todo_merge.md`). RFC `todo_merge.md` («Файлы к изменению → Документация») и AGENTS.md (EN **и** RU) требуют обновить доки в том же изменении. Ср. MRG1/IDML19/DEL1 (закрывались тем же приёмом) | **Доки ещё не закрыты (P1 открыт).** Обязательно в том же изменении (маршрут — `nextorm-design-engineer`): (1) `limitations.md` (EN+RU) — снять полный `MERGE`/`MERGE ... RETURNING` из «not implemented», описать гейты (`SupportsMergeStatement` — SQL Server и PG 15+; `THEN DELETE` — оба; `WHEN NOT MATCHED BY SOURCE` — только SQL Server; SQLite/MySQL/MariaDB/ClickHouse/in-memory → `NotSupportedException`) и что query-источник доступен только на полном `MERGE`; (2) `guide/19-insert-statement.md` (EN+RU) — раздел «Full MERGE with branches» (`## Full MERGE with branches` / `## Полный MERGE с ветками`) с примером `ctx.MergeInto<T>().Using(e).OnKeys().WhenMatched().ThenUpdate(d => new { d.Name }).WhenNotMatched().ThenInsert().WhenNotMatchedBySource().ThenDelete().Merge()` и `.Returning(x => new { x.Id, x.Name })`, таблицей провайдеров; (3) `api-reference.md` (EN+RU) — строки `MergeMatchedBuilder<TEntity>`, `MergeNotMatchedBuilder<TEntity>`, `MergeNotMatchedBySourceBuilder<TEntity>`, `MergeReturningBuilder<TEntity,TResult>` и новые методы `MergeBuilder`; (4) `providers/overview.md` (EN+RU) — строка «Merge (full)». Ссылки на `docs/specs/**` из публичных доков запрещены |
| MRG7 | P2 (трекинг) | `Builders/MergeBranchBuilders.cs:11,38,61`; `Builders/MergeReturningBuilder.cs:13`; `Builders/MergeBuilder.cs:102,112,175,182,190,198,215`; `ISqlDialect.cs:1170-1206`; `SqlDialectBase.cs:734-753`; `PostgresDialect.cs:31,34,37,40`; `SqlServerDialect.cs:73,76,79,82`; `PublicAPI.*.txt` отсутствуют | Публичные 4 типа + 11 членов + 7 новых членов `MergeBuilder` + 6 DIM + `SqlDialectBase`/провайдерные override'ы не трекаются: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение MRG2/IDML2/DEL3/UPD2/UPD7**, не новая проблема | При заморозке внести подписи (точный текст — из анализатора): `MergeMatchedBuilder<TEntity>` + 3 метода; `MergeNotMatchedBuilder<TEntity>` + 2; `MergeNotMatchedBySourceBuilder<TEntity>` + 1; `MergeReturningBuilder<TEntity,TResult>` + 5; `MergeBuilder<TEntity>.Using(EntityBuilder<TEntity>)`/`.Using(QueryCommand<TEntity>)`/`.WhenMatched()`/`.WhenNotMatched()`/`.WhenNotMatchedBySource()`/`.Returning()`/`.Returning<TResult>(Expression<Func<TEntity,TResult>>)`; 6 DIM (`SupportsMergeStatement`/`SupportsMergeDelete`/`SupportsMergeBySourceDelete`/`SupportsMergeTargetQualification`/`MakeMergeStatementTerminator`/`MakeMergeReturning`) + override'ы |
| MRG8 | **P2 (именование)** | `ISqlDialect.cs:1189`; `SqlDialectBase.cs:743`; `PostgresDialect.cs:37`; ср. `SupportsMerge` `ISqlDialect.cs:1143` | **Закрыто (23.09.2026).** Булев `MergeStatementQualifiesTarget` не следовал `Supports*`-семейству `ISqlDialect` (`SupportsMerge`, `SupportsMergeStatement`, `SupportsMergeDelete`, `SupportsMergeBySourceDelete`), ни `Is`/`Has`/`Can`-конвенции булевых запросов (api-design): читался как команда/сущность, а не как флаг. Рядом соседствуют близкие по имени `SupportsMerge` (key-upsert через `MERGE`) и `SupportsMergeStatement` (полный `MERGE`) — различие только в слове `Statement`, что легко спутать | **Применено:** переименовано `MergeStatementQualifiesTarget` → `SupportsMergeTargetQualification` (`roslyn rename`, 4 файла: `ISqlDialect.cs:1189`, `SqlDialectBase.cs:743`, `SqlMutationBuilder.cs:485`, `PostgresDialect.cs:37`); публичная форма заморожена не была, поэтому правка безопасна. `SupportsMerge`/`SupportsMergeStatement` оставлены (семантика различима в XML-doc); возможное переименование `SupportsMerge` → `SupportsKeyUpsert` — при заморозке Шага 5 |
| MRG9 | P2 (форма/док-полнота) | `MergeBuilder.cs:154,162` (key-upsert ветки) ↔ `:175,182` (branch-входы); `MergeBuilder.cs:192-220`; `MergeBranchBuilders.cs:5-9,17,21,27,32-36,44,48,67` | Две параллельные DSL-линии на одном билдере: `WhenMatchedUpdate()`/`WhenNotMatchedInsert()` (флаги key-upsert) и `WhenMatched()`/`WhenNotMatched()` + `ThenUpdate`/`ThenInsert` (полный `MERGE`). Обе валидны, но комбинируются неочевидно (наличие любой ветки переключает команду на полный `MERGE` и игнорирует флаги), а префикс `WhenMatched` общий у обеих. Дополнительно: `Returning()` `:198` не объявляет `<exception cref="NotSupportedException">` (в отличие от `Returning<TResult>` `:215`), хотя `ReturningProjection.Parse` его бросает; XML-summary `WhenMatched()`/`WhenNotMatched()` упоминают `ThenUpdate()`/`ThenDelete()`/`ThenInsert()` текстом, без `<see cref>`. CS1591 не нарушен | Не переименовывать в этом изменении; при заморозке зафиксировать обе линии в `<remarks>` `MergeBuilder<TEntity>` и гайде (когда какую применять; флаги и ветки не смешиваются), дополнить `<exception cref="NotSupportedException">` у `Returning()` и `<see cref>` на терминалы. Любое переименование — `docs/**` **и** `docs/ru/**` |

ℹ️ **Наблюдения.** (1) Все 4 новых типа `sealed` + `internal` ctor — снаружи только через терминалы `MergeBuilder` (как `DeleteReturningBuilder`/`UpdateJoinBuilder`). (2) 6 новых членов `ISqlDialect` — DIM (source/binary-совместимы, дефолты `false`/`true`/`""`/`MakeReturning`): внешние реализации интерфейса не ломаются; внести в KC1-перечень при заморозке. (3) XML-doc полный: build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — **0** (CS1591 не подавлен) ⇒ покрытие новых публичных членов 100 %. (4) `Returning()` переиспользует `ReturningProjection.Parse` (`Builders/ReturningProjection.cs:21`) — тот же конвейер материализации, что у INSERT/DELETE/UPDATE returning (новых публичных хелперов нет). (5) `Using(QueryCommand<TEntity>)` публично принимает подготовленную команду; источник-запрос поддержан только на полном `MERGE` (SQL Server/PG), на key-upsert-провайдерах отклоняется `NotSupportedException` (`MergeBuilder.cs:458-459`) — контракт описан в XML-doc `Using(EntityBuilder)`/`Using(QueryCommand)`. (6) Переименований нет; поверхность uncommitted; `docs/specs/**` (в т. ч. обновлённый `todo_merge.md`) — internal-план, не публичная поверхность. (7) В отличие от фаз DELETE/UPDATE, **публичные доки в этом же изменении не обновлены** — это и есть единственный P1 (MRG6), он остаётся открытым до отдельной правки `nextorm-design-engineer`/main-agent; P0 нет.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn types Merge` — 4 новых публичных типа (`MergeMatchedBuilder`, `MergeNotMatchedBuilder`, `MergeNotMatchedBySourceBuilder`, `MergeReturningBuilder<,>`) + `MergeBuilder<TEntity>` (`MergeMatchKind`/`MergeActionKind`/`MergeBranch`/`MergeCommand` — internal, не поверхность); `roslyn members NextORM.Core.MergeBuilder` — **15** публичных методов, ctor `internal`; `roslyn members MergeReturningBuilder` — **5**, `MergeMatchedBuilder` — **3**, `MergeNotMatchedBuilder` — **2**, `MergeNotMatchedBySourceBuilder` — **1** (ctor `internal` у всех); подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах фазы 2 — **0**; `NoWarn` по `.csproj` — **0**; `find -name 'PublicAPI*.txt'` — **0** (MRG7/Шаг 5); `rg -i 'WhenMatched\(|ThenUpdate|ThenDelete|WhenNotMatchedBySource|MergeReturningBuilder|MergeMatchedBuilder' docs/ -g '!specs/**'` — **0** (MRG6); `rg MergeInto docs/ -g '!specs/**'` — EN+RU `limitations`/`guide 19`/`api-reference`/`providers/overview` (только key-upsert). Тесты (Release, `--no-build`): core `MergeBuilderTests` **9/9**; `MergeSqlGenerationTests` **postgres 15/15, sqlserver 10/10, sqlite 4/4, mysql 4/4, mariadb 2/2, clickhouse 2/2** — итого **46/46, 0 failed, 0 skipped**. Контейнерная интеграция аудитом не перезапускалась (по отчёту автора — 1222 total, 0 failed, 48 skipped; фаз 2 — PG `FullMerge_Returning`/`FullMerge_MatchedDelete`, SQL Server `FullMerge_MatchedDelete`/`FullMerge_NotMatchedBySource`). Кодовая сторона — `code-smells-review.md`, Находки 120–122.

**Применено (23.09.2026, после аудита).** **MRG6 — закрыто**: публичные доки EN+RU обновлены — `limitations.md` (снят «full `MERGE` … to the caller»/«are not», key upsert in-memory, гейты), `guide/19-insert-statement.md` (+RU) получил раздел «Full MERGE»/«Полный `MERGE`» (ветки `WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource` + `ThenUpdate`/`ThenInsert`/`ThenDelete`, источник-запрос, таблица провайдеров) и подраздел «Returning the merged rows»/«Возврат слитых строк», строка key upsert про in-memory поправлена; `api-reference.md` (+RU) — расширена строка `MergeBuilder<TEntity>` и добавлена строка `MergeReturningBuilder<TEntity,TResult>`; `providers/overview.md` (+RU) — key-upsert in-memory-ячейка исправлена, добавлены строки «Full `MERGE`» и «`MERGE ... RETURNING`/`OUTPUT`». MRG9 — **частично**: `Returning()` получил `<exception cref="NotSupportedException">` (симметрично `Returning<TResult>`); `<see cref>` на терминалы и фиксация двух DSL-линий в `<remarks>` — при заморозке. **MRG8** — **закрыто**: `MergeStatementQualifiesTarget` переименован в `SupportsMergeTargetQualification`. **MRG7** (surface-lock `PublicAPI.*.txt`) — решение владельца до заморозки Шага 5, публичную форму не меняли.

**Дополнено (23.09.2026, фаза 3a — `DO NOTHING`).** Новый публичный терминал `ThenDoNothing()` на `MergeMatchedBuilder<TEntity>`/`MergeNotMatchedBuilder<TEntity>`/`MergeNotMatchedBySourceBuilder<TEntity>` (3 метода, `sealed`-типы, XML-doc полный) + новый DIM `SupportsMergeDoNothing` (`ISqlDialect`/`SqlDialectBase`, default `false`; PostgreSQL override `true`). Именование SQL-ориентированное (`DO NOTHING`), симметрично `ThenUpdate`/`ThenInsert`/`ThenDelete`; `Async`-суффикса нет (терминал не асинхронный). Публичных доков добавлено: `guide/19-insert-statement.md` (+RU) — строка про `ThenDoNothing()` и колонка `THEN DO NOTHING` в таблице провайдеров. Трекинг surface-lock — в MRG7. Фаза 3b (`On(expr)`/`WHEN ... AND <cond>`) — план в `todo_merge.md`, реестр пополнить при реализации.

**Дополнено (23.09.2026, фаза 3b — условные ветки).** Новые публичные члены: `MergeBuilder<TEntity>.On(Expression<Func<TEntity, TEntity, bool>>)` (альтернатива `OnKeys()`); перегрузки `WhenMatched(Expression<Func<TEntity,TEntity,bool>>)`, `WhenNotMatched(...)`, `WhenNotMatchedBySource(...)`; новый DIM `SupportsMergeConditionalBranches` (`ISqlDialect`/`SqlDialectBase`, default `false`; SQL Server и PostgreSQL — `true`). Именование SQL-ориентированное (`On` = `ON <condition>`), форма условия `(target, source)` описана в `<param>`; `Async`-суффиксов нет. Публичные доки: `guide/19-insert-statement.md` (+RU) — `On`/`When...(condition)` и колонка `ON / WHEN ... AND`; `providers/overview.md` (+RU) — строка «Merge search condition». Тесты: SQL-gen PG/SS (`On`, `WhenMatched(cond)`, `WhenNotMatched(cond)`, BY SOURCE cond) + интеграция (PG/SS исполняют, SQLite/MySQL — `NotSupportedException`). Surface-lock — по-прежнему MRG7.

### Аудит 23.09.2026 — DML `MERGE`, фаза 3b: условные ветки (`On`/`WHEN ... AND`), uncommitted working tree (P0 — нет; P1 — нет; P2 — 2)

**Область (uncommitted worktree).** Продолжение разделов «ключевой upsert» (MRG1–MRG5) и «фаза 2» (MRG6–MRG9; фаза 3a — `DO NOTHING`). Поверхность **аддитивна**, переименований нет, новых публичных **типов** — **0**. Новое (к заморозке Шага 5):

- `MergeBuilder<TEntity>` — **+4** публичных члена (итого **19**): `On(Expression<Func<TEntity,TEntity,bool>>) -> MergeBuilder<TEntity>` (`src/nextorm.core/Builders/MergeBuilder.cs:161`), `WhenMatched(Expression<Func<TEntity,TEntity,bool>>) -> MergeMatchedBuilder<TEntity>` (`:202`), `WhenNotMatched(Expression<Func<TEntity,TEntity,bool>>) -> MergeNotMatchedBuilder<TEntity>` (`:221`), `WhenNotMatchedBySource(Expression<Func<TEntity,TEntity,bool>>) -> MergeNotMatchedBySourceBuilder<TEntity>` (`:242`); прежние безаргументные перегрузки (`:194,213,233`) и `OnKeys()` (`:130`) не менялись;
- **1** новый член `ISqlDialect` (DIM, source/binary-совместим): `SupportsMergeConditionalBranches => false` (`ISqlDialect.cs:1196`), `SqlDialectBase`-двойник `:746`; override'ы PostgreSQL (`PostgresDialect.cs:40 => true`) и SQL Server (`SqlServerDialect.cs:82 => true`);
- internal-механика (не поверхность): `MergeCommand.MatchCondition`/`Registry` (`Query/Mutations/MergeCommand.cs:133,139`), `MergeBranch.Condition` (`:61`), `MergeAliasProvider` (`Query/MergeAliasProvider.cs:8`), `QueryPlanner.RenderMergeCondition` (`QueryPlanner.cs:158`), `DataContext.BuildMergeSql` (`DataContext.cs:396`), `SqlMutationBuilder.ValidateMergeBranches`/`AppendMergeUsingSource`/`AppendMergeOnKeys`/`AppendMergeWhen` (`SqlMutationBuilder.cs:558,591,625,645`).

**Именование — P0 нет.** `On` — SQL-ориентированный (`ON <condition>`), читается как терминал, рядом с `OnKeys()` (обе — match-фаза, различие «по ключу» vs «произвольное условие»); перегрузки `WhenMatched(...)`/`WhenNotMatched(...)`/`WhenNotMatchedBySource(...)` не вводят новых имён, а лишь перегружают существующие входы веток; `WhenNotMatchedBySource` сохранён. `Async`-суффиксов нет (условия не асинхронны, sync-двойника нет). BCL-конфликтов нет (`SupportsMergeConditionalBranches` в BCL отсутствует). XML-doc у всех 4 новых членов и DIM полный (summary/param/returns/exception), CS1591 не подавлен. Форма условия `(target, source)` описана в гайде (`guide/19`:450, +RU `:453`); в `<param>` — только пример, без словесного «первый = target, второй = source» (косметика, ℹ️ G код-регистра).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| MRG10 | P2 (трекинг) | `MergeBuilder.cs:161,202,221,242`; `ISqlDialect.cs:1196`; `SqlDialectBase.cs:746`; `PostgresDialect.cs:40`; `SqlServerDialect.cs:82`; `PublicAPI.*.txt` отсутствуют | 4 новых публичных члена `MergeBuilder<TEntity>` + 1 DIM + `SqlDialectBase`/провайдерные override'ы не трекаются: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение MRG2/MRG7/IDML2/DEL3/UPD2/UPD7**, не новая проблема | При заморозке внести подписи (точный текст — из анализатора): `MergeBuilder<TEntity>.On(Expression<Func<TEntity,TEntity,bool>>)`, `.WhenMatched(Expression<Func<TEntity,TEntity,bool>>)`, `.WhenNotMatched(Expression<Func<TEntity,TEntity,bool>>)`, `.WhenNotMatchedBySource(Expression<Func<TEntity,TEntity,bool>>)`; DIM `SupportsMergeConditionalBranches.get` + override'ы `SqlDialectBase`/`PostgresDialect`/`SqlServerDialect`. Расширяет перечень MRG7, отдельной записи surface-lock не требуется |
| MRG11 | P2 (форма/контракт) | `MergeBuilder.cs:130` (`OnKeys`) ↔ `:161` (`On`); `:173` (`WhenMatchedUpdate`) ↔ `:202` (`WhenMatched(cond)`); `SqlMutationBuilder.cs:625-642` (`AppendMergeOnKeys`) | Две DSL-линии (MRG9) теперь пересекаются и на условиях: флаги `WhenMatchedUpdate()`/`WhenNotMatchedInsert()` и входы веток `WhenMatched(cond)`/… делят префикс `WhenMatched`, а `On`/`OnKeys` — фазу match. Контракт несимметричен: `On` отклоняет уже заданный `_keys`, но `OnKeys()` не отклоняет `_matchCondition`, поэтому `On(...).OnKeys()` молча игнорирует ключи (кодовая сторона — `code-smells-review.md`, **Находка 124**). CS1591 не нарушен | Не переименовывать; при заморозке зафиксировать в `<remarks>` `MergeBuilder<TEntity>` и гайде, что `On` и `OnKeys` взаимоисключающие, а флаги key-upsert и входы веток не смешиваются; исправить односторонний guard (Находка 124). Любое переименование — публичный API **и** `docs/**` **и** `docs/ru/**` (AGENTS.md) |

**ℹ️ Наблюдения.** (1) Новый член `ISqlDialect` — DIM (source/binary-совместим, дефолт `false`): внешние реализации интерфейса не ломаются; внести в KC1-перечень при заморозке. (2) XML-doc полный: build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — **0** (CS1591 не подавлен) ⇒ покрытие новых публичных членов 100 %. (3) Публичные доки обновлены в этом же изменении — **P1 нет**: `docs/guide/19-insert-statement.md` (+RU `:453`/`:450`) описывает `On(condition)` и `When...(condition)` с формой `(target, source)` и предупреждением про SQL Server BY SOURCE; таблица провайдеров получила колонку `ON`/`WHEN ... AND` (EN `:452`, RU `:455`); `docs/providers/overview.md:48` (+RU) — строка «Merge search condition»; `docs/advanced/limitations.md:15` (+RU) — полный `MERGE` с ветками уже снят из «not implemented» ещё фазой 2. `rg -i 'On\(|search condition|WhenMatched\(condition\)' docs/ -g '!specs/**'` — EN+RU guide/providers (P1 закрыт, ср. закрытые MRG1/MRG6). (4) Форма `(target, source)` совпадает с `(existing target, incoming source)` linq2db-линии и с SQL-алиасами `target`/`source`, которые рендерит `MergeAliasProvider`; публичной утечки имён нет. (5) Переименований нет; поверхность uncommitted; `docs/specs/**` — internal-план, не публичная поверхность. (6) `PostgresDialectTests.MergeHooks_ShouldMatchPostgres` не ассертит новый флаг (пробел покрытия — ℹ️ E код-регистра).

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn types Merge` — **0** новых публичных типов фазы 3b (только новые члены существующего `MergeBuilder<TEntity>`; `MergeAliasProvider`/`MergeCommand`/`MergeBranch` — internal, не поверхность); `roslyn members NextORM.Core.MergeBuilder` — **19** публичных методов (15 → +4), ctor `internal`; `roslyn refs NextORM.Core.ISqlDialect.SupportsMergeConditionalBranches` — определение `ISqlDialect.cs:1196` + `SqlDialectBase.cs:746` + `PostgresDialect.cs:40` + `SqlServerDialect.cs:82` + 2 теста; подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах фазы 3b — **0**; `NoWarn` по `.csproj` — **0**; `find -name 'PublicAPI*.txt'` — **0** (MRG10/Шаг 5); `rg -i 'On\(|search condition' docs/guide/19-insert-statement.md docs/ru/guide/19-insert-statement.md docs/providers/overview.md docs/ru/providers/overview.md` — EN+RU (P1 нет). Тесты (Release, `--no-build`, фильтр `FullyQualifiedName~Merge`): core `MergeBuilderTests` **11/11**; `MergeSqlGenerationTests` — postgres **21/21**, sqlserver **16/16**, sqlite **4/4**, mysql **4/4**, mariadb **2/2**, clickhouse **2/2** — итого **60/60, 0 failed, 0 skipped**. Контейнерная интеграция аудитом не перезапускалась (по отчёту автора — 3b-кейсы PG/SS исполнены, SQLite/MySQL получили `NotSupportedException`). Кодовая сторона — `code-smells-review.md`, Находки 123–125 (в т. ч. 🔴 123 — дублирование параметров при query-источнике, маршрут `nextorm-design-engineer`).

**Применено (23.09.2026, после аудита).** **Находка 123 (🔴) — закрыта** (дублирование `@pN` при параметризованном query-источнике; регрессия покрыта SQL-gen + интеграцией PG/SQL Server). **MRG11 — закрыто в части контракта:** guard'ы `On`/`OnKeys` сделаны симметричными (повторный `On` и `OnKeys()` после `On(...)` бросают). **MRG10** (tracking публичных подписей `On`/`When*(condition)`/`SupportsMergeConditionalBranches` для `PublicAPI.*.txt`) — остаётся за Шагом 5, как MRG7.

### Аудит 23.09.2026 — CTAS (`CREATE [TEMPORARY] TABLE ... AS SELECT`): `ToTempTable`/`ToTable` и материализация в таблицу, uncommitted working tree (P0 — нет; P1 — нет; P2 — 4)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- **`NextORM.Core.TempTableExtensions`** (`src/nextorm.core/Builders/TempTableExtensions.cs:20`, `public static class`) — **12** публичных extension-методов, 6 имён × 2 приёмника (`EntityBuilder<TResult>` до `Select` и готовый `QueryCommand<TResult>` после):
  - `ToTempTable<TResult>(this EntityBuilder<TResult>, string, TempTableOptions? = null) -> void` `:28`; `ToTempTable<TResult>(this QueryCommand<TResult>, string, TempTableOptions? = null) -> void` `:40`;
  - `ToTempTableAsync<TResult>(this EntityBuilder<TResult>, string, TempTableOptions? = null, CancellationToken = default) -> Task` `:51`; на `QueryCommand<TResult>` `:65`;
  - `ToTempTableSql<TResult>(this EntityBuilder<TResult>, string, TempTableOptions? = null) -> string` `:75`; на `QueryCommand<TResult>` `:88`;
  - `ToTable<TResult>(this EntityBuilder<TResult>, string, TempTableOptions? = null) -> void` `:97`; на `QueryCommand<TResult>` `:109`;
  - `ToTableAsync<TResult>(this EntityBuilder<TResult>, string, TempTableOptions? = null, CancellationToken = default) -> Task` `:120`; на `QueryCommand<TResult>` `:134`;
  - `ToTableSql<TResult>(this EntityBuilder<TResult>, string, TempTableOptions? = null) -> string` `:144`; на `QueryCommand<TResult>` `:157`.
- **`NextORM.Core.TempTableOptions`** (`src/nextorm.core/Builders/TempTableOptions.cs:22`, `public sealed record`) — 4 `init`-свойства: `IfNotExists` `:25`, `WithData` (default `true`) `:31`, `OnCommit` (тип `TempTableOnCommit`, default `PreserveRows`) `:38`, `Columns` (`IReadOnlyList<string>?`) `:44`; плюс сгенерированные record-члены (public ctor, `Equals`/`GetHashCode`/`ToString`).
- **`NextORM.Core.TempTableOnCommit`** (`:7`, `public enum`) — 3 значения: `PreserveRows`, `DeleteRows`, `Drop`.
- **`NextORM.Core.CreateTableAsClause`** (`src/nextorm.core/Query/CreateTableAsClause.cs:15`, `public readonly record struct`) — 6 позиционных параметров: `Table`, `Temporary`, `IfNotExists`, `Columns`, `OnCommit`, `WithData`; сгенерированные record-члены.
- **4** новых члена `ISqlDialect` (DIM, source/binary-совместимы): `SupportsCreateTableAsSelect => false` (`Dialect/ISqlDialect.cs:1395`), `SupportsCreateTableAsSelectColumnList => false` `:1402`, `SupportsCreateTableAsSelectOnCommit => false` `:1408`, `SupportsCreateTableAsSelectWithNoData => false` `:1415`; плюс `MakeCreateTableAsSelect(CreateTableAsClause, string, KeywordCase = Lower)` `:1428` (дефолт — `NotSupportedException`). `SqlDialectBase`-двойники `:867,870,873,876,883`; override'ы: PostgreSQL 4 флага + `MakeCreateTableAsSelect` (`src/nextorm.postgres/PostgresDialect.cs:397,400,403,406,409`), SQLite `SupportsCreateTableAsSelect` (`src/nextorm.sqlite/SqliteDialect.cs:243`), MySQL/MariaDB `SupportsCreateTableAsSelect`/`…ColumnList` (`src/nextorm.mysql/MySqlDialect.cs:401,404`; SQL Server/ClickHouse — дефолт `false`).
- internal-механика (не поверхность): `SqlStatementType.CreateTableAsSelect` (`Query/Mutations/MutationCommand.cs:25`), `CreateTableAsCommand` (`Query/Mutations/CreateTableAsCommand.cs:9`), `SqlMutationBuilder.MakeCreateTableAsSelect` (`DataContext/SqlMutationBuilder.cs:401`), `DataContext.BuildCreateTableAsSql` (`:453`).

**Именование — P0 нет.** BCL-конфликтов нет (`TempTableExtensions`/`TempTableOptions`/`TempTableOnCommit`/`CreateTableAsClause` в BCL отсутствуют; `ToTempTable`/`ToTable` не совпадают ни с одним существующим публичным членом — в репозитории их раньше не было). `Async`-суффикс корректен: у `ToTempTableAsync`/`ToTableAsync` есть sync-пары `ToTempTable`/`ToTable` (AGENTS.md соблюдён). `CancellationToken` — последним (CA1068). Возвраты: `void`/`Task`/`string`, `async void` нет. `Columns` — `IReadOnlyList<string>?` (не массив) — по api-design. `TempTableOnCommit` — enum без префикса членов. XML-doc у всех новых публичных типов и членов полный (summary/typeparam/param/returns/exception), CS1591 не подавлен. `Supports*`/`Make*` — существующий паттерн `ISqlDialect`; `*Clause` в сигнатуре диалекта — существующий паттерн (`TemporalClause`/`ForJsonClause`/`ForXmlClause`).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CTAS1 | P2 (именование) | `Builders/TempTableOptions.cs:22`; `Builders/TempTableExtensions.cs:97,109` | **`TempTableOptions` — имя temp-специфичное, но тип принимает и постоянный `ToTable`** (persistent). Доки (`guide/22` + api-reference EN+RU) называют его «Options … to control the statement» нейтрально, но имя вводит в заблуждение: `options.OnCommit` читается как «опция только временной таблицы», хотя объект передаётся и в постоянный терминал (там `OnCommit` недопустим → `NotSupportedException` при рендере) | До заморозки рассмотреть переименование `TempTableOptions` → `CreateTableAsOptions` (или `MaterializeOptions`). Проект alpha — переименование на месте допустимо; обязательно `docs/**` **и** `docs/ru/**` (guide 22 EN+RU, api-reference EN+RU, providers/overview EN+RU) в том же изменении |
| CTAS2 | P2 (форма/консистентность) | `Builders/TempTableExtensions.cs:75,88,144,157`; ср. `InsertBuilder.cs:151`, `UpdateBuilder.cs:151`, `DeleteBuilder.cs:71`, `MergeBuilder.cs:311`, `TruncateBuilder.cs:26`, `UpdateJoinBuilder.cs:87` | **Диагностические терминалы названы `ToTempTableSql`/`ToTableSql`, а не `ToSql()`** — единственное место, где конвенция «отрендерить без исполнения» облечена в `*Sql`-суффикс. Однозначность оправдана (на `EntityBuilder<TResult>`/`QueryCommand<TResult>` `ToSql()` отсутствует, и `ToSql()` без имени/опций не выразил бы выбор temp/persistent + target), но суффикс нестандартен | Не переименовывать вслепую; при заморозке либо ввести `ToSql`-перегрузки там, где однозначно, либо зафиксировать исключение и обоснование в `<remarks>` `TempTableExtensions`. Любое переименование — EN+RU доки |
| CTAS3 | P2 (трекинг) | `PublicAPI.*.txt` отсутствуют; `TempTableExtensions.cs:28-157`; `TempTableOptions.cs:22-44`; `CreateTableAsClause.cs:15`; `TempTableOptions.cs:7`; `ISqlDialect.cs:1395-1429`; `SqlDialectBase.cs:867-901`; `PostgresDialect.cs:397-441`; `SqliteDialect.cs:243`; `MySqlDialect.cs:401,404` | Вся CTAS-поверхность (**4** типа, **12** методов, **4** DIM + `MakeCreateTableAsSelect`, **8** провайдерных override (PG 5, SQLite 1, MySQL/MariaDB 2), record-сгенерированные члены) не трекается: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение MRG2/MRG7/MRG10/UPD2/IDML2/DEL3**, не новая проблема | При заморозке внести подписи (точный текст — из анализатора): 4 типа + `TempTableExtensions` × 12 + `TempTableOptions` (ctor/props/record-члены) + `CreateTableAsClause` (ctor + 6 props) + `TempTableOnCommit` (3 значения); DIM `SupportsCreateTableAsSelect*`/`MakeCreateTableAsSelect` + override'ы. Расширяет перечень MRG7, отдельной записи surface-lock не требуется |
| CTAS4 | P2 (именование) | `TempTableOptions.cs:7` (тип) ↔ `:38` (свойство `OnCommit`) | **`TempTableOnCommit` дублирует имя свойства**: `options.OnCommit` имеет тип `TempTableOnCommit`, т. е. «OnCommit типа TempTableOnCommit»; члены `PreserveRows`/`DeleteRows`/`Drop` без контекста читаются как действия над таблицей, а не как значения `ON COMMIT` | До заморозки рассмотреть `OnCommitAction`/`TempTableOnCommitAction` (или свойство `OnCommitAction`). Alpha допускает rename; EN+RU доки + guide 22 в том же изменении |

ℹ️ **Наблюдения.** (1) **XML-doc 100 %:** build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — **0** (CS1591 не подавлен) ⇒ покрытие новых публичных членов полное. (2) **Публичные доки обновлены в этом же изменении — P1 нет:** новый `docs/guide/22-create-table-as.md` EN+RU (ToTempTable/ToTable, options-таблица, провайдеры, read-back через `From("name")`), `docs/guide/toc.yml` EN (`:44`) / `docs/ru/toc.yml` RU (`:60`), `docs/advanced/api-reference.md:49` EN+RU (строка `TempTableExtensions`), `docs/providers/overview.md:56` EN+RU (строка «Materialize a query»), `docs/advanced/limitations.md:15` EN+RU (CTAS снят из «not implemented», провайдеры перечислены). `rg 'ToTempTable|ToTable|TempTableOptions|TempTableOnCommit' docs -g '!specs/**'` — совпадения EN+RU. (3) **12 перегрузок — принято:** dual-приёмник (builder + готовый `QueryCommand<TResult>`) повторяет ось других терминалов (`Delete`/`DeleteAsync`/`ToSql` на билдере и joined-билдере); сокращение поверхности не рекомендуется. (4) **`Temporary` вне `TempTableOptions` — принято:** две терминальные ветки дают явную интенцию и не оставляют молчаливого флага; цена — имя `TempTableOptions` (CTAS1). (5) **`CreateTableAsClause` публичен — необходимо и согласовано:** параметр публичного члена публичного `ISqlDialect` обязан быть публичным; `internal`-вариант унёс бы хук с интерфейса (ломает паттерн всех `Make*`); публичные `*Clause` в сигнатурах диалекта уже есть (`TemporalClause`, `ForJsonClause`, `ForXmlClause`). (6) **`OnCommit` только с `ToTempTable`, проверка на этапе сборки SQL — принято:** согласовано с контрактом «опция не поддержана ⇒ `NotSupportedException` при рендере» (флаги колонок/`WITH NO DATA`); ошибка ловится до БД (`ToTableSql` тоже), текст actionable; `PreserveRows` — дефолт и молчаливая no-op-опция. (7) **`record`-равенство о reference-член `Columns`** — кодовая сторона (`code-smells-review.md`, Находка 128). (8) **CTE-источник** рендерится невалидно — кодовая сторона (🔴 Находка 126), не API-находка. (9) Переименований нет; поверхность uncommitted; `docs/specs/**` — internal-план, не публичная поверхность.

**Проверка (23.09.2026).** Build Release — **0/0**; `roslyn members NextORM.Core.TempTableExtensions` — **12** публичных методов (6 имён × 2 приёмника) + 6 private; `roslyn types TempTable` — `TempTableExtensions`/`TempTableOnCommit`/`TempTableOptions` (+ `CreateTableAsClause` по полному имени); `rg 'SupportsCreateTableAsSelect|MakeCreateTableAsSelect' src --include=*.cs` — `ISqlDialect.cs:1395,1402,1408,1415,1428`, `SqlDialectBase.cs:867,870,873,876,883`, `PostgresDialect.cs:397,400,403,406,409`, `SqliteDialect.cs:243`, `MySqlDialect.cs:401,404` (SQL Server/ClickHouse/MariaDB — дефолт/наследование); подавления `src/` — **6+5=11** (0 неоправданных), новых в CTAS-файлах — **0**; `NoWarn` по `.csproj` — **0**; `find -name 'PublicAPI*.txt'` — **0** (CTAS3/Шаг 5); `rg 'ToTempTable|ToTable|TempTableOptions|TempTableOnCommit' docs -g '!specs/**'` — EN+RU guide 22/toc/api-reference/providers/limitations (P1 нет); BCL-конфликтов новых имён — нет. Тесты (Release, `--no-build`, фильтр `FullyQualifiedName~CreateTableAs`): core **4/4**, postgres **6/6**, sqlite **6/6**, mysql **6/6**, mariadb **5/5**, sqlserver **3/3**, clickhouse **3/3** — итого **33/33, 0 failed, 0 skipped**. Контейнерная интеграция аудитом не перезапускалась. Кодовая сторона — `code-smells-review.md`, Находки 126–128 (🔴 126 — CTE-порядок, маршрут `nextorm-design-engineer`).

---

**Закрытие планов (23.09.2026).** Рабочие планы `docs/specs/roadmap/todo_insert.md` и `todo_merge.md` удалены после выпуска всех их фич (DML-каркас и `INSERT`, key upsert, полный `MERGE`: ветки, `DO NOTHING`, `On(expr)`/`WHEN ... AND`). Инлайновые ссылки вида `todo_insert.md:NN`/`todo_merge.md` в записях выше — исторические артефакты проходов аудита; живой трекинг ведут этот реестр, `code-smells-review.md` и `sql-capabilities-gap-analysis.md` (item 18). Surface-lock (Шаг 5) остаётся открытым: `PublicAPI.*.txt` отсутствуют (MRG7/MRG10/IDML18/CTAS3).


### Аудит 23.09.2026 — Транзакции: роль `ITransactionManager` и привязка `DbCommand.Transaction`, uncommitted working tree (P0 — нет; P1 — 2 доковых/контрактных; P2 — 3)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований нет. Новое (к заморозке Шага 5):

- **`NextORM.Core.ITransactionManager`** (`src/nextorm.core/DataContext/Roles/ITransactionManager.cs:12`, `public interface`) — **6** публичных членов: `CurrentTransaction { get; } -> DbTransaction?` `:15`; `BeginTransaction() -> DbTransaction` `:19`; `BeginTransaction(IsolationLevel) -> DbTransaction` `:24`; `BeginTransactionAsync(CancellationToken = default) -> Task<DbTransaction>` `:29`; `BeginTransactionAsync(IsolationLevel, CancellationToken = default) -> Task<DbTransaction>` `:35`; `UseTransaction(DbTransaction?) -> void` `:43`. Реализуется `DataContext` **явно** (`DataContext.cs:14`, `:169,171,173,175,177,179`) — публичная поверхность конкретного типа не расширена; in-memory роль не реализует.
- **2 новых публичных overload'а `DbPreparedQueryCommand<TResult>.GetDbCommand`** (публичный sealed-тип): `(object[]?, Func<string,object?,DbParameter>, DbConnection, DbTransaction?) -> DbCommand` (`Cache/DbPreparedQueryCommand.cs:110`) и `(ReadOnlySpan<object?>, …, DbTransaction?) -> DbCommand` `:123`; существующие 3-арг overload'ы `:86,98` сохранены и делегируют с `null` (extend-only).
- **1 новый член `ISqlDialect`** (DIM, source/binary-совместим): `SupportsTransactions => true` (`Dialect/ISqlDialect.cs:543`), `SqlDialectBase`-двойник (`:188`, `virtual => true`), override `ClickHouseDialect` (`src/nextorm.clickhouse/ClickHouseDialect.cs:338 => false`).
- internal-механика (не поверхность): `DbConnectionManager` (`_transaction`/`_transactionOwnedByMe`, `BeginTransaction*`, `UseTransaction`, `EnsureTransactionsSupported`, `EnsureNoActiveTransaction`, `DisposeTransaction`, `Func<bool> supportsTransactions`), `Func<DbTransaction?> _currentTransaction` в `QueryExecutor`/`ResultSetEnumerator`, `DataContext`-делегаты `() => Dialect.SupportsTransactions` (`:68`) и `() => _connectionManager.CurrentTransaction` (`:83`).

**Именование — P0 нет.** `I`-префикс и `Manager`-суффикс соблюдены; `BeginTransaction`/`BeginTransactionAsync` совпадают с `DbConnection.BeginTransaction*` (BCL), `CurrentTransaction`/`UseTransaction` — с EF Core `Database.CurrentTransaction`/`Database.UseTransaction` (RFC `todo_transactions.md:96-98`). `Async`-суффикс корректен — есть sync-пара (AGENTS.md); `CancellationToken` — последним (CA1068). Возвраты: `DbTransaction`/`Task<DbTransaction>`/`void`, `async void` нет. Параметр-`null` у `UseTransaction` документирован как «detach». XML-doc у всех 6 членов интерфейса и DIM полный (summary/param/returns); CS1591 не подавлен. BCL-конфликта компиляции нет, но см. TR2.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| TR1 | **P1 (док/противоречие)** | `docs/advanced/limitations.md:46`; `docs/ru/advanced/limitations.md:46`; `docs/guide/16-connections-and-logging.md` (+RU); `docs/advanced/api-reference.md` (+RU, рядом с `IConnectionManager:29`); `docs/providers/overview.md` (+RU) | **Публичные доки утверждают обратное фиче.** `limitations.md:46` (EN+RU) — «read-only participation in a caller-managed transaction is not part of the API» / «участие только для чтения в транзакции, управляемой вызывающим кодом, не является частью API» — теперь **ложно**. В `guide/16` (EN+RU) транзакций **0**; в `api-reference.md` (EN+RU) строки `ITransactionManager` **нет**; в `providers/overview.md` (EN+RU) строки/колонки транзакций **нет**. RFC перечисляет именно эти файлы (`todo_transactions.md:280-282`), AGENTS требует EN **и** RU. Ср. закрытые MRG6/IDML19/DEL1 | **Доки ещё не закрыты (P1 открыт).** В том же изменении (маршрут `nextorm-design-engineer`): (1) `limitations.md` EN+RU — снять «не является частью API», описать read-only участие в **своей**/чужой транзакции, владение, ClickHouse `NotSupportedException`, что DML/`SaveChanges` по-прежнему нет; (2) `guide/16` EN+RU — раздел «Transactions» («Транзакции») с `((ITransactionManager)ctx).BeginTransactionAsync()` и `UseTransaction(efTx.GetDbTransaction())`; (3) `api-reference.md` EN+RU — строка `ITransactionManager` и 2 overload'а `GetDbCommand`; (4) `providers/overview.md` EN+RU — строка «Transactions». Ссылки на `docs/specs/**` из публичных доков запрещены |
| TR2 | P2 (именование) | `Roles/ITransactionManager.cs:12` | Имя на одну `I` отстоит от BCL `System.Transactions.TransactionManager` и читается как «менеджер транзакций» вообще, тогда как здесь — контекст-локальная ADO.NET-транзакция (`CurrentTransaction`/`UseTransaction`). Компиляционного конфликта нет (свой namespace), но семантика `System.Transactions` (ambient/`TransactionScope`) — ровно то, что RFC отнёс к фазе 2, что усиливает путаницу. Открытый вопрос RFC #2 (`todo_transactions.md:263`) | До заморозки рассмотреть `IDbTransactionManager` (точнее отражает `DbTransaction`-поверхность и дистанцируется от `System.Transactions`). Alpha допускает rename; обновить `ITransactionManager.cs`, `DataContext.cs:14,169-179`, тесты и EN+RU доки |
| TR3 | P2 (док/контракт) | `Roles/ITransactionManager.cs:6-43`; `DataContext.cs:198-205` | **(а) Потокобезопасность.** RFC требует задокументировать «контекст не thread-safe для параллельного изменения транзакции» (`todo_transactions.md:185-186`), но в XML-doc интерфейса и публичном гайде этого нет. **(б) `CreateCommand(sql)`.** Публичный метод биндит `cmd.Transaction` в момент создания (`DataContext.cs:202-203`), но XML-doc (`:195-197`) обещает только «bound to the current connection»; вызывающий, сохранивший команду между `BeginTransaction`/`UseTransaction`, получит устаревшую привязку | Дополнить `<remarks>` `ITransactionManager` и гайд: «not thread-safe; do not begin/detach a transaction concurrently with query execution on the same context»; в `<remarks>` `CreateCommand` одной фразой указать, что команда фиксирует текущую транзакцию на момент создания. Публичную форму не менять |
| TR4 | P2 (трекинг) | `Roles/ITransactionManager.cs:12-43`; `Cache/DbPreparedQueryCommand.cs:110,123`; `ISqlDialect.cs:543`; `SqlDialectBase.cs:188`; `ClickHouseDialect.cs:338`; `PublicAPI.*.txt` отсутствуют | Новая публичная поверхность (**1** тип + **6** членов интерфейса; **2** overload'а `GetDbCommand`; **1** DIM + `SqlDialectBase`/ClickHouse-override) не трекается: `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение MRG7/MRG10/CTAS3/UPD2/IDML18**, не новая проблема | При заморозке внести подписи (точный текст — из анализатора): `NextORM.Core.ITransactionManager` + `CurrentTransaction.get`/4 `BeginTransaction*`/`UseTransaction`; `DbPreparedQueryCommand<TResult>.GetDbCommand(object[]?, Func<string,object?,DbParameter>, DbConnection, DbTransaction?)` и span-версия; DIM `SupportsTransactions.get` + override'ы. Расширяет перечень MRG7 |
| TR5 | **P1 (контракт/владение)** | `Roles/ITransactionManager.cs:37-43`; `DbConnectionManager.cs:123-130` | `UseTransaction(null)` документирован как «pass null to detach», но **не различает чужую и контекстную** транзакцию. Для контекстной (`BeginTransaction*`) вызов молча снимает владение, не освобождая её: транзакция остаётся открытой на соединении, а `cmd.Transaction` больше не выставляется → на SQL Server/MySQL следующий запрос падает `InvalidOperationException` (кодовая сторона — `code-smells-review.md`, 🔴 Находка 129). Контракт владения RFC (`todo_transactions.md:100-107`) не выполняется | Уточнить контракт `UseTransaction`: `null` снимает только caller-supplied привязку; для контекстной — либо освобождение/откат, либо `InvalidOperationException`; отразить в XML-doc `<exception>` и гайде. Публичную форму не менять |

ℹ️ **Наблюдения.** (1) XML-doc полный: build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — 0 (CS1591 не подавлен) ⇒ покрытие 6 членов + DIM 100 %. (2) **Extend-only соблюдён:** 3-арг `GetDbCommand` (`:86,98`) сохранены и делегируют с `null`, новые 4-арг — аддитивны; сигнатуры `DbPreparedQueryCommand` не менялись. (3) 1 новый DIM — source/binary-совместим (внешние реализации `ISqlDialect` не ломаются, дефолт `true`); внести в KC1-перечень при заморозке. (4) Роль отдельна от `IConnectionManager` и не входит в `IDataContext` — SRP/как RFC (`IConnectionManager.cs:5-10`); in-memory роль не реализует. (5) `DataContext` реализует роль **явно** — публичная поверхность конкретного типа не выросла (RFC `todo_transactions.md:212-214`). (6) Гейт ClickHouse — `NotSupportedException` с явным сообщением на `BeginTransaction*`/`UseTransaction(tx != null)`, а не молчаливый no-op; `UseTransaction(null)` доступен всегда. (7) Переименований нет; поверхность uncommitted; `docs/specs/**` — internal-план (включая обновлённый `todo_transactions.md`), не публичная поверхность. (8) **Публичные доки в этом же изменении не обновлены — это и есть P1 (TR1)**, он открыт до правки `nextorm-design-engineer`; P0 нет.

**Проверка (23.09.2026).** `dotnet build src/nextorm.core -c Release` — **0/0** (полный solution — 3 errors в untracked bulk-insert тестах, вне области); `roslyn members NextORM.Core.ITransactionManager` — **6** публичных членов (5 методов + свойство); `roslyn refs NextORM.Core.ITransactionManager` — 7 (def + явные реализации `DataContext.cs:14,169,171,173,175,177,179`; тестов **0**); `roslyn members NextORM.Core.DbPreparedQueryCommand<TResult>` — 4 overload'а `GetDbCommand` (2 нов. 4-арг, 2 прежн. 3-арг); `rg 'SupportsTransactions' src` — `ISqlDialect.cs:543`, `SqlDialectBase.cs:188`, `DataContext.cs:68`, `ClickHouseDialect.cs:338`; `rg -i 'transaction|ITransactionManager|BeginTransaction|UseTransaction' docs -g '!specs/**'` — только EN+RU `limitations.md` (ложное утверждение, TR1) + не относящиеся упоминания (`guide/01` про `for update`, `guide/provider-specific/sqlserver`, `guide/16` без транзакций); `find -name 'PublicAPI*.txt'` — **0** (TR4/Шаг 5); подавления `src/` — 11/11 (новых 0). Тесты — `code-smells-review.md`, Находки 129–133 (🔴 129 — владение, маршрут `nextorm-design-engineer`).

**Применено (23.09.2026, тот же worktree).**
- **TR1 (P1) — закрыто.** `limitations.md` (+RU) снял «не является частью API» и описал участие в своей/чужой транзакции; `docs/guide/25-transactions.md` (+RU) — отдельная страница «Transactions»/«Транзакции» (указатель из guide 16); `api-reference.md` (+RU) — строка `ITransactionManager`; `providers/overview.md` (+RU) — строка «Transactions». `todo_transactions.md` удалён, ссылки в `todo_efcore_integration.md` перепоинтованы на guide 25.
- **TR2 (P2) — принято с обоснованием.** Имя `ITransactionManager` оставлено: совпадает с трекером (issue #32) и симметрично соседней роли `IConnectionManager`; компиляционного конфликта с `System.Transactions.TransactionManager` нет. Кандидат `IDbTransactionManager` зафиксирован в реестре (Шаг 5).
- **TR3 (P2) — исправлено.** `<remarks>` `ITransactionManager` дополнены «not thread-safe…»; XML-doc `CreateCommand` упоминает привязку к активной транзакции; гайд 16 содержит ту же оговорку.
- **TR4 (P2) — отложено (Шаг 5).** `PublicAPI.*.txt` не подключены; поверхность внесена в перечень заморозки (KC1/TR4/MRG7/BK4).
- **TR5 (P1) — закрыто.** См. Находку 129: `UseTransaction(null)` различает чужую и контекстную транзакцию; XML-doc уточнён.

### Аудит 23.09.2026 — Bulk insert: `BulkInsertBuilder<TEntity>` / `BulkInsertReturningBuilder<TEntity,TResult>` и dialect-hooks, uncommitted working tree (P0 — нет; P1 — 1 доковый; P2 — 5)

**Область (uncommitted worktree).** Поверхность **аддитивна**, переименований/удалений нет. Новое (к заморозке Шага 5):

- **`NextORM.Core.BulkInsertBuilder<TEntity>`** (`src/nextorm.core/Builders/BulkInsertBuilder.cs:21`, `public sealed class`, ctor `internal`) — **14** публичных методов: `Values(IEnumerable<TEntity>)` `:75`, `Values(IAsyncEnumerable<TEntity>)` `:91`, `Timeout(int)` `:106`, `MaxBatchSize(int)` `:116`, `MaxParameters(int)` `:126`, `MaxSqlLength(int)` `:136`, `KeepIdentity()` `:148`, `IgnoreDuplicates()` `:160`, `NotifyAfter(int, Action<int>)` `:173`, `BulkInsert()` `:187`, `BulkInsertAsync(CancellationToken = default)` `:199`, `ToSql()` `:218`, `ReturningKey<TKey>()` `:250`, `Returning<TResult>(Expression<Func<TEntity,TResult>>)` `:268`.
- **`NextORM.Core.BulkInsertReturningBuilder<TEntity,TResult>`** (`src/nextorm.core/Builders/BulkInsertReturningBuilder.cs:20`, `public sealed class`, ctor `internal`) — **3** публичных метода: `ToSql()` `:46`, `ToList()` `:75`, `ToListAsync(CancellationToken = default)` `:114`.
- **1 extension**: `DataContextExtensions.BulkInsertInto<TEntity>(this IDataContext, Action<EntityMetadataBuilder<TEntity>>? = null)` (`src/nextorm.core/DataContext/DataContextExtensions.cs:50`).
- **2 protected-члена `DataContext`** (public abstract, входят в публичную поверхность наследования): `BulkInsertRows(string, IReadOnlyList<string>, IReadOnlyList<IPropertyMetadata>, IEnumerable<object?[]>)` (`src/nextorm.core/DataContext/DataContext.cs:404`) и `BulkInsertRowsAsync(..., IAsyncEnumerable<object?[]>, CancellationToken)` `:415`.
- **9 DIM `ISqlDialect`** (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`): `SupportsBulkCopy => false` `:1111`, `SupportsInsertIgnore => false` `:1119`, `MakeInsertIgnoreInto(KeywordCase = Lower)` `:1127`, `SupportsOnConflictDoNothing => false` `:1135`, `MakeOnConflictDoNothing(KeywordCase = Lower)` `:1142`, `MakeOverridingSystemValue(KeywordCase = Lower)` `:1150`, `RequiresIdentityInsertToggle => false` `:1158`, `MakeIdentityInsertOn(string, KeywordCase = Lower)` `:1164`, `MakeIdentityInsertOff(string, KeywordCase = Lower)` `:1170`; base-двойники `SqlDialectBase.cs:714-730`; override'ы диалектов: PG 3 (`PostgresDialect.cs:31,34,37`), SQL Server 2 (`:43,46`), SQLite 2 (`:51,53`), MySQL 3 (`:45,48,51`), MariaDB 1 (`:21`), ClickHouse 3 (`:43,49,52`).
- internal-механика (не поверхность): `BulkInsertCommand`/`BulkBatchOptions`/`IBulkInsertExecutor` (`Query/Mutations/BulkInsertCommand.cs`), `PortableBulkInsertExecutor`/`SyncToAsyncEnumerable<T>`, явная реализация `DataContext` (`:14,356-381`).

**Именование — P0 нет.** BCL-конфликтов компиляции нет (`BulkInsertBuilder` не сталкивается с `System.Data.SqlClient.SqlBulkCopy`; `BulkInsertCommand` internal — CA1716/CA1724 чисто). Именование консистентно с существующей поверхностью: суффикс `*Builder`; `BulkInsertInto` зеркалит `InsertInto`; терминалы `BulkInsert()`/`BulkInsertAsync()` зеркалят `Insert()`/`InsertAsync()` (`InsertBuilder.cs:54,63`); `ReturningKey<TKey>()` зеркалит `InsertBuilder.ReturningKey<TKey>()` (`:107`); `BulkInsertReturningBuilder<TEntity,TResult>` зеркалит `InsertReturningBuilder<TEntity,TResult>`; `Supports*`/`Make*`/`Requires*` — существующий стиль `ISqlDialect`. `Async`-суффикс корректен (есть sync-пара, AGENTS.md), `CancellationToken` — последним (CA1068); возвраты `IReadOnlyList<TResult>`/`Task<int>`/`int`, `async void` нет. P2-замечания — BK2/BK3.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| BK1 | **P1 (док/противоречие)** | `Builders/BulkInsertBuilder.cs:100-111` (`Timeout` doc); `src/nextorm.sqlserver/SqlServerDataContext.cs:110-118` (doc) | Две XML-доки противоречат коду. (1) `Timeout` обещает «Only the native bulk path honours it», а нативный путь timeout не передаёт и `BulkCopyTimeout`/Npgsql-timeout не выставляет (кодовая сторона — Находка 134). (2) `SqlServerDataContext.BulkInsertRows` велит «bound the set with `BulkInsertBuilder.MaxBatchSize` for very large writes», но `UseNative` (`:375-376`) `MaxBatchSize` игнорирует, а набор буферизуется в `DataTable` целиком (Находка 137). Публичный контракт опций не выполняется | Привести XML-doc в соответствие коду: либо реализовать timeout/batch на нативном пути (маршрут `nextorm-design-engineer`), либо убрать обещание из `Timeout` и совет из SQL Server-doc. Публичную форму не менять (кроме новых protected-хуков) |
| BK2 | P2 (именование) | `Builders/BulkInsertBuilder.cs:154-164`; `Builders/BulkInsertBuilder.cs:53` (`IgnoreConflicts`); `ISqlDialect.cs:1119` (`SupportsInsertIgnore`) | `IgnoreDuplicates()` называет «дубликаты», тогда как семантика — пропуск строк, нарушающих **уникальное ограничение** (в т.ч. PK), что покрывают `INSERT IGNORE`/`ON CONFLICT DO NOTHING`; внутреннее имя и диалект-флаг — `IgnoreConflicts`/`SupportsInsertIgnore`. Расхождение публичного имени с семантикой и внутренним именем | До заморозки рассмотреть `IgnoreConflicts()` (симметрично внутреннему `_ignoreConflicts`) либо явно оговорить в XML-doc, что пропускается любой конфликт уникальности, а не только дубликаты. Alpha допускает rename; обновить 4 SQL-gen + интеграционный тест |
| BK3 | P2 (именование) | `ISqlDialect.cs:1127`; `SqlDialectBase.cs:718`; `MySqlDialect.cs:51`; `ClickHouseDialect.cs:52` | `MakeInsertIgnoreInto(keywordCase)` рендерит фиксированный head без таблицы, тогда как соседние `MakeDeleteHead`/`MakeUpdateHead` (`ISqlDialect.cs:1323,1399`) принимают `table`; «Into» в имени вводит в заблуждение (ClickHouse-override рендерит просто `insert into `). Открытый P2 до заморозки | Переименовать в `MakeInsertHead`/`MakeInsertIgnoreHead` (head без таблицы) либо принять как есть. DIM source/binary-совместим; обновить base + 2 override'а + `SqlMutationBuilder` |
| BK4 | P2 (трекинг) | `PublicAPI.*.txt` отсутствуют; `BulkInsertBuilder.cs:21-273`; `BulkInsertReturningBuilder.cs:20-119`; `DataContextExtensions.cs:50`; `DataContext.cs:404,415`; `ISqlDialect.cs:1111-1170`; `SqlDialectBase.cs:714-730`; 6 dialect-файлов | Новая публичная поверхность не трекается: **2** публичных типа (**14** + **3** public-метода, ctor `internal`), **1** extension, **2** protected `DataContext`, **9** DIM + **9** base-virtual + **14** override'ов. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены, `PublicAPI.*.txt` — **0** (Шаг 5 открыт). **Продолжение MRG7/TR4/CTAS3/UPD2/IDML18**, не новая проблема | При заморозке внести подписи (точный текст — из анализатора). Расширяет перечень MRG7/TR4 |
| BK5 | P2 (док/покрытие) | `docs/guide/19-insert-statement.md` (+RU); `docs/advanced/api-reference.md` (+RU); `docs/providers/overview.md` (+RU) | Публичная документация bulk-фичи **отсутствует**: в `guide/19-insert-statement.md` (EN+RU) — только построчный `InsertInto`/upsert/merge; строк `BulkInsertBuilder`/`BulkInsertInto`/`ITransactionManager` в `api-reference.md` нет; в `providers/overview.md` нет колонки native-bulk. В отличие от TR1 это не противоречие, а **пробел** (bulk заявлен как planned в `linq2db-comparison`), но AGENTS требует EN **и** RU | В том же изменении (маршрут `nextorm-design-engineer`): раздел «Bulk insert»/«Массовая вставка» в `guide/19` EN+RU (portable vs native, `KeepIdentity`/`IgnoreDuplicates`/`Returning*`/`MaxBatchSize`/`NotifyAfter`, ограничения `Timeout`); строка `BulkInsertBuilder<TEntity>` в `api-reference.md` EN+RU; строка native-bulk в `providers/overview.md` EN+RU. Ссылки на `docs/specs/**` запрещены |
| BK6 | P2 (покрытие) | `tests/nextorm.core.tests/BulkInsertBuilderTests.cs` (7 кейсов); `tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/BulkInsertSqlGenerationTests.cs`; `tests/nextorm.integration.tests/CommonTestSuite.BulkInsert.cs` (5) | Покрытие фрагментарно: нет теста на `Timeout` (Находка 134), bulk внутри транзакции (135), source-less returning-терминал (136), `NotifyAfter`/`MaxParameters`/`MaxSqlLength`/`Returning<TResult>`/нативный `KeepIdentity`; SQL-gen покрывает portable-форму, а нативные PG/SQL Server пути интеграция не исполняет (только `BulkInsert`/`ReturningKey` portable). `DOCKER_HOST`-прогон аудитом не выполнялся | Расширить core/SQL-gen и интеграционные тесты по перечню; маршрут `nextorm-design-engineer` |

ℹ️ **Наблюдения.** (1) **P0 нет**; BCL-конфликтов имён (CA1716/CA1724) нет. (2) Именование типов/членов консистентно с `InsertBuilder`/`InsertReturningBuilder`/`InsertInto` и стилем `ISqlDialect` (`Supports*`/`Make*`/`Requires*`); `Async`-суффикс и CA1068 соблюдены. (3) XML-doc полный: build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — 0 (CS1591 не подавлен) ⇒ покрытие **20** публичных/protected-членов (14+3+1+2) + **9** DIM + **9** base-virtual + **14** override'ов = 100 %. (4) **Extend-only соблюдён:** все новые члены `ISqlDialect` — DIM с дефолтами (внешние реализации интерфейса компилируются и не ломаются, source/binary-совместимо); `protected virtual BulkInsertRows*` — новые и не `abstract`; `DataContext` реализует internal `IBulkInsertExecutor` **явно** — публичная поверхность конкретного типа не выросла; переименований/удалений нет. (5) `Returning*` намеренно уводит на portable путь (нативный bulk не отдаёт ключи) — согласовано с XML-doc `BulkInsertBuilder:12-18`. (6) Внешние публичные доки не обновлены — это BK5 (пробел, не противоречие). (7) Поверхность uncommitted; `docs/specs/**` — internal-план, не публичная поверхность; Шаг 5 открыт.

**Проверка (23.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0** (проверено); `roslyn types BulkInsert` — **2** публичных типа (`BulkInsertBuilder<TEntity>`, `BulkInsertReturningBuilder<TEntity,TResult>`) + **3** internal (`BulkInsertCommand`, `PortableBulkInsertExecutor`, `IBulkInsertExecutor`) + `SyncToAsyncEnumerable<T>`; `roslyn members NextORM.Core.BulkInsertBuilder` — **14** public + `internal` ctor; `roslyn members NextORM.Core.BulkInsertReturningBuilder` — **3** public + `internal` ctor; `rg 'SupportsBulkCopy|SupportsInsertIgnore|MakeInsertIgnoreInto|SupportsOnConflictDoNothing|MakeOnConflictDoNothing|MakeOverridingSystemValue|RequiresIdentityInsertToggle' src` — DIM `ISqlDialect.cs:1111-1170`, base `SqlDialectBase.cs:714-730`, override'ы PG/SQL Server/SQLite/MySQL/MariaDB/ClickHouse (см. выше); `rg -i 'bulk' docs -g '!docs/specs/**'` — публичных страниц 0 (только `guide/19` про построчный insert + комментарии), BK5; `find -name 'PublicAPI*.txt'` — **0** (BK4/Шаг 5); подавления `src/` — 11/11 (новых 0). Кодовая сторона — `code-smells-review.md`, Находки 134–140 (🔴 134 — `Timeout`, 🔴 135 — `SqlBulkCopy`+tx, маршрут `nextorm-design-engineer`).

---

### Ре-аудит 24.09.2026 — Bulk insert: options-API (`BulkInsertOptions`/`BulkInsertOptionsBuilder`), uncommitted working tree (P0 — нет; новых P1 — нет; P2 — 6: BK2/BK3/BK4/BK5/BK6/BK7, из них BK7 новое; BK8 закрыто 24.09.2026)

**Что изменилось с аудита 23.09.2026.** Fluent-методы записи удалены из `BulkInsertBuilder<TEntity>` (было **14** публичных методов → стало **7**) и вынесены в новый публичный `BulkInsertOptions` (record) + `BulkInsertOptionsBuilder`; `BulkInsertBuilder<TEntity>` теперь принимает `BulkInsertOptions` в `internal` ctor. К `DataContextExtensions.BulkInsertInto<TEntity>` добавлены 2 перегрузки; options-перегрузка принимает `Func<BulkInsertOptionsBuilder, BulkInsertOptionsBuilder>` (expression-lambda, возвращающая builder), поэтому прежняя коллизия overload'ов снята (BK8). Прогресс стал `Action<int, CancellationToken>` (второй аргумент — токен из `ProgressCancellationTokenSource`); `NotifyAfter` намеренно синхронный. SQL Server-натив получает `commandTimeoutSeconds`/`maxBatchSize` и разворачивает `Microsoft.Data.OperationAbortedException` с внутренним `OperationCanceledException` через `ExceptionDispatchInfo` (`src/nextorm.sqlserver/SqlServerDataContext.cs:145-148,185-188`).

**Область (uncommitted worktree).** Поверхность **аддитивна**; переименований/удалений в опубликованном коде нет (весь bulk-инкремент uncommitted). Новое (к заморозке Шага 5):

- **`NextORM.Core.BulkInsertBuilder<TEntity>`** (`src/nextorm.core/Builders/BulkInsertBuilder.cs:23`, `public sealed class`, ctor `internal` :32) — **7** публичных методов: `Values(IEnumerable<TEntity>)` `:60`, `Values(IAsyncEnumerable<TEntity>)` `:76`, `BulkInsert()` `:89`, `BulkInsertAsync(CancellationToken = default)` `:101`, `ToSql()` `:120`, `ReturningKey<TKey>()` `:152`, `Returning<TResult>(Expression<Func<TEntity,TResult>>)` `:170`.
- **`NextORM.Core.BulkInsertReturningBuilder<TEntity,TResult>`** (`src/nextorm.core/Builders/BulkInsertReturningBuilder.cs:20`, ctor `internal` :27) — **3** публичных метода: `ToSql()` `:46`, `ToList()` `:74`, `ToListAsync(CancellationToken = default)` `:112`.
- **`NextORM.Core.BulkInsertOptions`** (`src/nextorm.core/Builders/BulkInsertOptions.cs:9`, `public sealed record`, `internal Validate()` :71) — **9** публичных `init`-свойств: `MaxBatchSize` :16, `MaxParameters` :19, `MaxSqlLength` :22, `KeepIdentity` :29, `IgnoreDuplicates` :36, `TimeoutSeconds` :43, `Progress` :55, `ProgressCancellationTokenSource` :64, `NotifyEvery` :67.
- **`NextORM.Core.BulkInsertOptionsBuilder`** (`src/nextorm.core/Builders/BulkInsertOptions.cs:93`, `public sealed class`) — **9** публичных методов: `MaxBatchSize(int)` :109, `MaxParameters(int)` :120, `MaxSqlLength(int)` :131, `KeepIdentity(bool = true)` :141, `IgnoreDuplicates(bool = true)` :150, `Timeout(int)` :160, `NotifyAfter(int, Action<int, CancellationToken>)` :173, `ProgressCancellationTokenSource(CancellationTokenSource)` :190, `Build()` :199.
- **3 перегрузки extension**: `DataContextExtensions.BulkInsertInto<TEntity>` — `(this IDataContext, Action<EntityMetadataBuilder<TEntity>>? = null)` `src/nextorm.core/DataContext/DataContextExtensions.cs:50`, `(this IDataContext, BulkInsertOptions, Action<EntityMetadataBuilder<TEntity>>? = null)` :71, `(this IDataContext, Func<BulkInsertOptionsBuilder, BulkInsertOptionsBuilder>, Action<EntityMetadataBuilder<TEntity>>? = null)` :92.
- **2 protected-члена `DataContext`** (сигнатуры расширены `int? commandTimeoutSeconds, int? maxBatchSize`): `BulkInsertRows` `src/nextorm.core/DataContext/DataContext.cs:416`, `BulkInsertRowsAsync` :431.
- **9 DIM `ISqlDialect`** без изменений (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:1111,1119,1127,1135,1142,1150,1158,1164,1170`), base-двойники `SqlDialectBase.cs:714-730` (**9**), override'ы **14** (Postgres 3, SQL Server 2, SQLite 2, MySQL 3, MariaDB 1, ClickHouse 3).
- internal-механика (не поверхность): `BulkInsertCommand` (`src/nextorm.core/Query/Mutations/BulkInsertCommand.cs:38`), `BulkBatchOptions` :7, `IBulkInsertExecutor` :128, `PortableBulkInsertExecutor` (`src/nextorm.core/DataContext/PortableBulkInsertExecutor.cs:10`), `SyncToAsyncEnumerable<T>`.

**Именование — P0 нет.** BCL-конфликтов (CA1716/CA1724) нет. `BulkInsertOptions` — `sealed record` с `init`-свойствами, **точно как прецедент `CreateTableAsOptions`** (`src/nextorm.core/Builders/CreateTableAsOptions.cs:22`, `sealed record`, передаётся как `CreateTableAsOptions? options = null` в `TempTableExtensions`); суффикс `*Options` соблюдён. `BulkInsertOptionsBuilder` — единственный `*OptionsBuilder` в `src/` (в DI есть только `*DataContextOptionsBuilderExtensions`), но это стандартная связка .NET `*Options`/`*OptionsBuilder` (`Microsoft.Extensions.Options.OptionsBuilder<TOptions>`), без BCL-конфликта. Терминалы `BulkInsert()`/`BulkInsertAsync()`/`ReturningKey<TKey>()`/`BulkInsertReturningBuilder<TEntity,TResult>`, `BulkInsertInto` и стиль `ISqlDialect` (`Supports*`/`Make*`/`Requires*`) — как в аудите 23.09.2026. `Async`-суффикс (есть sync-пара, AGENTS.md) и CA1068 соблюдены. Замечания к именам — BK2 (прежнее); BK8 закрыто (см. ниже).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| BK1 | **закрыто (исправлено)** | `SqlServerDataContext.cs:202`; `PostgresDataContext.cs:143-147`; `PortableBulkInsertExecutor.cs:110-115`; `BulkInsertOptions.cs:38-43` | Обе части прежнего P1 устранены. (1) `TimeoutSeconds` доходит до нативного хука (`DataContext.cs:370,385`); SQL Server выставляет `BulkCopyTimeout = commandTimeoutSeconds ?? 30`, а PostgreSQL `COPY` и portable-путь **явно отклоняют** timeout через `NotSupportedException`; XML-doc `BulkInsertOptions.TimeoutSeconds` соответствует коду. (2) `MaxBatchSize` тоже доходит до натива (`DataContext.cs:371,386`), SQL Server выставляет `SqlBulkCopy.BatchSize` (`:203`) — опция больше не no-op. | Закрыто. Остаток — BK7 (устаревшая ссылка в доке SQL Server). |
| BK2 | P2 (именование) | `BulkInsertOptions.cs:36`; `BulkInsertOptionsBuilder.IgnoreDuplicates` `:150`; `BulkInsertCommand.cs:74`; `ISqlDialect.cs:1119` | `IgnoreDuplicates` называет «дубликаты», тогда как семантика — пропуск строк, нарушающих **уникальное ограничение** (в т.ч. PK); внутреннее имя/флаг — `IgnoreConflicts`/`SupportsInsertIgnore`. XML-doc теперь явно говорит «violate a unique constraint» (`BulkInsertOptions.cs:31-36`) — расхождение смягчено, но публичное имя осталось. | До заморозки рассмотреть `IgnoreConflicts` (симметрично внутреннему) либо принять с текущей оговоркой в доке. Alpha допускает rename. |
| BK3 | P2 (именование) | `ISqlDialect.cs:1127`; `SqlDialectBase.cs:718`; `MySqlDialect.cs:51`; `ClickHouseDialect.cs:52` | Без изменений: `MakeInsertIgnoreInto(keywordCase)` рендерит фиксированный head без таблицы, тогда как `MakeDeleteHead`/`MakeUpdateHead` принимают `table`; «Into» вводит в заблуждение (ClickHouse-override рендерит `insert into `). | Переименовать в `MakeInsertHead`/`MakeInsertIgnoreHead` (head без таблицы) либо принять. DIM source/binary-совместим; обновить base + 2 override'а + `SqlMutationBuilder`. Наследуется из аудита 23.09.2026. |
| BK4 | P2 (трекинг) | `PublicAPI.*.txt` — **0**; перечень выше | Поверхность выросла: **4** публичных типа (`BulkInsertBuilder<T>` 7 методов, `BulkInsertReturningBuilder<T,TResult>` 3, `BulkInsertOptions` 9 init-свойств, `BulkInsertOptionsBuilder` 9 методов), **3** перегрузки extension, **2** protected `DataContext`, **9** DIM + **9** base-virtual + **14** override'ов. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены (Шаг 5 открыт). Продолжение MRG7/TR4/CTAS3/UPD2/IDML18/BK4 — не новая проблема. | При заморозке внести подписи (точный текст — из анализатора). |
| BK5 | P2 (док/покрытие) — **частично исправлено** | исправлено: `docs/guide/24-bulk-insert.md` (+RU, 247/248 строк), `docs/guide/toc.yml`, `docs/ru/toc.yml`, указатели в `docs/guide/19-insert-statement.md` (+RU); открыто: `docs/advanced/api-reference.md` (+RU), `docs/providers/overview.md` (+RU) | Гайд «Bulk insert»/«Массовая вставка» добавлен (EN+RU) и описывает options-API (`BulkInsertOptions`/`BulkInsertOptionsBuilder`, таблица «свойство ↔ метод», native-vs-portable, `Timeout` только SQL Server). Но в `api-reference.md` (+RU) строк `BulkInsertBuilder`/`BulkInsertReturningBuilder`/`BulkInsertOptions`/`BulkInsertOptionsBuilder` нет, а в support matrix `providers/overview.md` нет строки native-bulk. AGENTS требует EN **и** RU. | Добавить 2–4 строки в `api-reference.md` EN+RU и строку native-bulk в support matrix `providers/overview.md` EN+RU. Ссылки на `docs/specs/**` запрещены. |
| BK6 | P2 (покрытие) — **частично исправлено** | `tests/nextorm.core.tests/BulkInsertBuilderTests.cs` (**9** `[Fact]`); `tests/nextorm.integration.tests/CommonTestSuite.BulkInsert.cs` (**9** `[Fact]`); `BulkInsertSqlGenerationTests` — sqlite **6**, postgres **6**, sqlserver **4**, mysql **4** | Добавлены `InvalidOptions_ShouldThrow` (валидация record-пути), `MaxBatchSize`, `NotifyAfter`, `NotifyAfterThrows`, `ProgressCancellation`, `IgnoreDuplicates`, `IgnoreDuplicates_WithReturning`; SQL-gen переведён на `ctx.BulkInsertInto<T>(o => …)`. Остались без теста: bulk внутри транзакции (Находка 135 исправлена кодом), source-less returning-терминал (136 исправлена кодом), `MaxParameters`/`MaxSqlLength`, `Returning<TResult>` (проекция), нативные PG/SQL Server пути против контейнера. | Добавить негативные/интеграционные тесты по перечню; контейнерный прогон — вне этого аудита. |
| BK7 | **НОВОЕ P2 (док/регрессия переименования)** | `src/nextorm.sqlserver/SqlServerDataContext.cs:113-114` | XML-doc protected-хука `BulkInsertRows` советует «bound the set with `<c>BulkInsertBuilder.MaxBatchSize</c>` for very large writes», но `BulkInsertBuilder.MaxBatchSize` **удалён** в этом же изменении (правильные имена — `BulkInsertOptions.MaxBatchSize`/`BulkInsertOptionsBuilder.MaxBatchSize`). Плюс совет вводит в заблуждение по памяти: набор целиком буферизуется в `DataTable` до `WriteToServer` (`:131-135`, `:171-175`), а `SqlBulkCopy.BatchSize` (`:203`) лишь дробит отправку на сервер. | Переписать: сослаться на `BulkInsertOptions.MaxBatchSize` и явно сказать, что нативный путь буферизует набор целиком. Маршрут `nextorm-design-engineer`; публичную форму не менять. |
| BK8 | **закрыто (исправлено 24.09.2026)** | `DataContextExtensions.cs:50,71,92`; `EntityMetadataBuilder.cs:22`; `BulkInsertOptions.cs:199` | Прежняя коллизия снята: options-перегрузка теперь `Func<BulkInsertOptionsBuilder, BulkInsertOptionsBuilder>` (`:92`). Пустая лямбда `o => { }` и `o => o.Build()` больше не дают `CS0121` (в `Func` их тело не возвращает builder: пустое тело не конвертируется в `Func`, а `BulkInsertOptionsBuilder.Build()` возвращает `BulkInsertOptions`, не builder). XML-doc перегрузки (`:82-83`) прямо требует «expression lambda that returns the builder», напр. `o => o.MaxBatchSize(1_000)`. **Остаточная оговорка:** голый `null`-литерал (`BulkInsertInto<E>(null)`) совместим и с `Action<EntityMetadataBuilder<TEntity>>?`, и с `Func<...>?`, и с `BulkInsertOptions` ⇒ `CS0121`; на практике вызов всегда либо без аргумента, либо с лямбдой. Побочно: `o => o.Build()` теперь молча привязывается к entity-metadata-перегрузке (не ошибка компиляции, но no-op по смыслу опций). | Закрыто. Если bare-`null` станет реальным, ввести отдельное имя/`[OverloadResolutionPriority]`; публичную форму bulk больше не трогать. |

ℹ️ **Наблюдения (ре-аудит).** (1) **P0 нет; новых P1 нет** — обе прежние P1-темы (BK1) закрыты; BK7 — P2, BK8 закрыто 24.09.2026. (2) XML-doc полный: build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — 0 (CS1591 не подавлен) ⇒ member-уровневое покрытие **28** public-членов (7+3+9+9) + 3 extension + 2 protected + 32 dialect-члена = 100 %. (3) **Extend-only:** весь bulk-инкремент uncommitted; dialect-члены — DIM/virtual; `DataContext` реализует `IBulkInsertExecutor` явно; удаление fluent-методов из `BulkInsertBuilder` — перестройка **незарелиженной** alpha-поверхности, не breaking для пользователей. (4) `ProgressCancellationTokenSource` — `IDisposable` в публичном record, задокументирован как caller-owned: nextorm его не отменяет и не диспозит (`BulkInsertOptions.cs:56-64`); дизайн-компромисс для cooperative `CancelAfter`, не дефект. (5) `TimeoutSeconds`/`Timeout` и `NotifyEvery`+`Progress`/`NotifyAfter` — две формы одного и того же; гайд 24 фиксирует соответствие таблицей, но имена свойств и fluent-методов расходятся (наблюдение, не находка). (6) Шаг 5 открыт: `PublicAPI.*.txt` — 0; `docs/specs/**` — internal-план, не публичная поверхность.

**Проверка (24.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `roslyn types BulkInsert` — **4** публичных типа (`BulkInsertBuilder<TEntity>`, `BulkInsertReturningBuilder<TEntity,TResult>`, `BulkInsertOptions`, `BulkInsertOptionsBuilder`) + **3** internal (`BulkInsertCommand`, `PortableBulkInsertExecutor`, `IBulkInsertExecutor`); `roslyn members NextORM.Core.BulkInsertBuilder` — **7** public + `internal` ctor; `roslyn members NextORM.Core.BulkInsertOptions` — **9** public init-свойств + `internal Validate`; `roslyn members NextORM.Core.BulkInsertOptionsBuilder` — **9** public-методов; `roslyn members NextORM.Core.DataContextExtensions` — **3** `BulkInsertInto<TEntity>` (`Action<EntityMetadataBuilder<TEntity>>?` :50, `BulkInsertOptions` :71, `Func<BulkInsertOptionsBuilder, BulkInsertOptionsBuilder>` :92); `rg 'BulkInsertBuilder\.' docs src -g '!docs/specs/**'` — **1** (`SqlServerDataContext.cs:114`, BK7); `rg -i 'bulk' docs/advanced/api-reference.md docs/providers/overview.md` — **0** (BK5); `find -name 'PublicAPI*.txt'` — **0** (BK4/Шаг 5); подавления `src/` — **6+5=11** (0 неоправданных), в bulk-файлах — **0**. Тесты (Release, `--no-build`): core `BulkInsertBuilderTests` **9/9**; SQL-gen bulk — sqlite **6/6**, postgres **6/6**, sqlserver **4/4**, mysql **4/4**. Кодовая сторона — `code-smells-review.md`, ре-аудит находок 134–142 (закрыты 134/135/136/137/140; открыты 138/139; новые 141/142).

---

### Ре-аудит 24.09.2026 — CTAS на SQL Server (`SELECT ... INTO`) и ClickHouse (persistent `CREATE TABLE ... ENGINE = MergeTree ... AS SELECT`), uncommitted working tree (P0 — нет; P1 — **CTAS5 закрыто**; P2 — CTAS6 отложено, **CTAS7 закрыто**; состояние 24.09.2026)

**Что изменилось с аудита 23.09.2026.** `SupportsCreateTableAsSelect` уточнён до «материализация в **постоянную** таблицу» и стал `true` у SQL Server и ClickHouse; добавлены **4** члена `ISqlDialect` (DIM): `SupportsTemporaryCreateTableAsSelect` (`DataContext/Dialect/ISqlDialect.cs:1477`), `SupportsCreateTableAsSelectIfNotExists` (`:1484`), `CreateTableAsSelectUsesSelectInto` (`:1490`), `MakeCreateTableAsSelectInto(CreateTableAsClause, KeywordCase = Lower)` (`:1522`), с `SqlDialectBase`-двойниками (`:891,894,897,913`). Override'ы: SQL Server — `SupportsCreateTableAsSelect=true`/`…Temporary=false`/`…IfNotExists=false`/`CreateTableAsSelectUsesSelectInto=true`/`MakeCreateTableAsSelectInto` (`src/nextorm.sqlserver/SqlServerDialect.cs:58,61,64,67,70`); ClickHouse — `SupportsCreateTableAsSelect=true`/`…Temporary=false`/`MakeCreateTableAsSelect` с `engine = MergeTree order by tuple()` (`src/nextorm.clickhouse/ClickHouseDialect.cs:79,82,85`). Публичные `TempTableExtensions` (12 терминалов), `CreateTableAsOptions` (4 init-свойства) и `CreateTableAsClause` (6 позиционных параметров, `Query/CreateTableAsClause.cs:15`) **не менялись**.

**Именование — P0 нет.** BCL-конфликтов нет. `MakeCreateTableAsSelectInto` — стиль `Make*` с дефолтом-`NotSupportedException`, как `MakeCreateTableAsSelect` (`:1536`); `CreateTableAsSelectUsesSelectInto` описывает форму рендера, а не capability (прецеденты `ILockRenderer.UsesTableHints`, `SqlDialectBase.RequiresIdentityInsertToggle`); `SupportsCreateTableAsSelectIfNotExists` вписывается в `Supports*`-семейство. Все 4 члена — DIM/`virtual` (extend-only, source/binary-совместимо внешним реализациям `ISqlDialect`). XML-doc полный, CS1591 не подавлен. P2-замечания — CTAS6 (порядок слов в `SupportsTemporaryCreateTableAsSelect`, **отложено** до заморозки публичного API) и CTAS7 (оптимистичный дефолт `IfNotExists`, **исправлено** 24.09.2026).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CTAS5 | **P1 (док/противоречие) — закрыто (исправлено 24.09.2026)** | EN: `docs/guide/22-create-table-as.md:104-105` (и таблица опций `:90`); `docs/advanced/limitations.md:15`; `docs/providers/overview.md:57`. RU: `docs/ru/guide/22-create-table-as.md:114-115` (опции `:99`); `docs/ru/advanced/limitations.md:15`; `docs/ru/providers/overview.md:57`. Внутренние: `docs/specs/comparison/capability-matrix.md:76`; `docs/specs/roadmap/sql-capabilities-gap-analysis.md:42,357-361` | **Публичные доки EN+RU противоречат фиче.** `guide/22` (EN+RU) в таблице провайдеров: SQL Server — «no (its `SELECT ... INTO #t` form is not wired yet)» / «нет (форма `SELECT ... INTO #t` пока не подключена)», ClickHouse — «no…» / «нет…»; `limitations.md` (EN+RU) — «SQL Server … and ClickHouse … reject it» / «…их отклоняют»; `providers/overview.md` (EN+RU) — ячейки SQL Server и ClickHouse в строке `ToTempTable`/`ToTable` всё ещё «throws `NotSupportedException`». Фактически persistent `ToTable` теперь работает на обоих, а отклоняются только temp/неприменимые опции. Таблица опций `guide/22` (`IfNotExists`, `:90`/`:99`) не упоминает ClickHouse, который `IF NOT EXISTS` рендерит. AGENTS.md требует EN **и** RU в том же изменении. Ср. закрытые TR1/BK1 (P1 доковые) | Обновить EN+RU: `guide/22` — провайдерная таблица (`ToTable` да на SQL Server/ClickHouse, `ToTempTable` нет), таблица опций (`IfNotExists` + ClickHouse), пример `SELECT INTO`/`#name`; `limitations.md` — снять «не подключён/отклоняет», описать persistent `ToTable`; `providers/overview.md` — строку CTAS (SQL Server `SELECT ... INTO`, ClickHouse `ENGINE = MergeTree order by tuple()`; `ToTempTable` — throw). Внутренние specs синхронизировать при закрытии `todo_create_table_as.md` (шаг 8). **Исправлено 24.09.2026:** EN+RU `guide/22`/`providers/overview`/`limitations`/`api-reference` + `capability-matrix:79`/`sql-capabilities-gap-analysis` приведены к факту, `todo_create_table_as.md` удалён. Маршрут `nextorm-design-engineer` |
| CTAS6 | P2 (именование, **отложено** до заморозки API) | `ISqlDialect.cs:1477`; `SqlDialectBase.cs:891`; `SqlServerDialect.cs:61`; `ClickHouseDialect.cs:85` | `SupportsTemporaryCreateTableAsSelect` — единственный член семейства CTAS, где уточнение стоит **перед** корнем: соседи — `SupportsCreateTableAsSelectColumnList` (`:1498`), `…OnCommit` (`:1504`), `…WithNoData` (`:1511`). Читается как «supports temporary create table as select» | До заморозки рассмотреть `SupportsCreateTableAsSelectTemporary` (суффиксное семейство) либо принять. DIM source/binary-совместим; обновить base + 2 override'а. Alpha допускает rename |
| CTAS7 | P2 (контракт/дефолт) — закрыто (исправлено 24.09.2026) | `ISqlDialect.cs:1484`; `SqlDialectBase.cs:894` | `SupportsCreateTableAsSelectIfNotExists => true` — единственный CTAS-capability-флаг с дефолтом `true`, тогда как все соседи возвращают `false` по формуле «returning false so existing external implementations keep compiling». Внешняя реализация, впервые включающая `SupportsCreateTableAsSelect => true`, молча заявляет и поддержку `IF NOT EXISTS`; в репозитории opt-out только у SQL Server. Дефолт `true` есть у `SupportsTransactions`/`SupportsCommandBehaviorSingleRow`, но там он верен для ADO.NET в целом — `IF NOT EXISTS` универсальным не является | До заморозки рассмотреть `false` + явные `true`-override'ы (PG/SQLite/MySQL/MariaDB/ClickHouse — 5 строк) либо задокументировать оптимистичный дефолт в XML-doc. Публичную форму не менять. **Исправлено 24.09.2026:** дефолт `false` (`ISqlDialect.cs:1484`/`SqlDialectBase.cs:894`); `true`-override'ы у PostgreSQL (`PostgresDialect.cs:409`), SQLite (`SqliteDialect.cs:253`), MySQL (`MySqlDialect.cs:418`), ClickHouse (`ClickHouseDialect.cs:82`); SQL Server — `false` (`SqlServerDialect.cs:64`) |

ℹ️ **Наблюдения.** (1) **P0 нет; CTAS5 и CTAS7 закрыты 24.09.2026, CTAS6 отложен** (кодовая сторона — `code-smells-review.md`, Находка 143 исправлена, 144 принята). (2) XML-doc полный: build `0/0` при `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` по `.csproj` — 0 (CS1591 не подавлен) ⇒ 4 новых DIM + base-двойники + provider-override задокументированы. (3) **Extend-only:** новые члены — DIM/`virtual` с дефолтами; смена смысла `SupportsCreateTableAsSelect` (было «CREATE TABLE … AS SELECT», стало «persistent materialisation») не удаляет и не ломает сигнатуру, но внешний код, проверявший флаг ради конкретного синтаксиса, теперь должен смотреть `CreateTableAsSelectUsesSelectInto` — поверхность uncommitted/alpha. (4) `CreateTableAsClause` не изменён; `MakeCreateTableAsSelectInto` читает только `clause.Table`, остальные поля отклоняются валидацией до вызова (temporary/IfNotExists/columns), поэтому публичный record не расширяется. (5) **CTAS3 расширяется:** 4 новых члена + override'ы входят в уже открытый трекинг Шага 5 (`PublicAPI.*.txt` — 0). (6) Переименований/удалений нет; `docs/specs/**` — internal-план.

**Проверка (24.09.2026; обновлено после фиксов).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; EN+RU доки (CTAS5) — **закрыто**: `guide/22`, `providers/overview`, `limitations`, `api-reference` отражают persistent `ToTable` на SQL Server (`SELECT ... INTO`) и ClickHouse (`ENGINE = MergeTree`), `ToTempTable` — только PG/SQLite/MySQL/MariaDB; `rg -i 'not wired yet|их отклоняют' docs` — 0; `todo_create_table_as.md` удалён; `rg 'SupportsCreateTableAsSelectIfNotExists' src` — `ISqlDialect.cs:1484`/`SqlDialectBase.cs:894` = `false`, overrides `true` у PG `:409`/SQLite `:253`/MySQL `:418`/ClickHouse `:82`, SQL Server `:64` = `false`; `ClickHouseDialect.cs:79,82,85,88`; `find -name 'PublicAPI*.txt'` — **0** (CTAS3/Шаг 5); подавления `src/` — **6+5=11** (0 неоправданных), новых в CTAS-файлах — 0. Тесты (Release, `--no-build`, фильтр `~CreateTableAs`, перепрогон 24.09.2026): core **4/4**, postgres **7/7**, sqlite **6/6**, mysql **6/6**, mariadb **5/5**, sqlserver **10/10** (+2 новых на 143), clickhouse **6/6** — **44/44, 0 failed, 0 skipped**. Контейнерная интеграция аудитом не перезапускалась. Кодовая сторона — `code-smells-review.md`, ре-аудит 24.09.2026 (143 — исправлена, 144 — принята).

**Применено (24.09.2026, тот же worktree).** CTAS5 — доки EN+RU + internal specs синхронизированы, план удалён; CTAS7 — дефолт `false` + явные `true`-override'ы; CTAS6 — отложено до заморозки; 143 (code) — исправлено, 144 — принято. Подробности — `code-smells-review.md`, «Применено (24.09.2026)».

---

### Аудит 24.09.2026 — режимы ожидания блокировки строк (`NOWAIT`/`SKIP LOCKED`): `LockWaitMode`, перегрузки `ForUpdate`/`ForShare`, DIM `ILockRenderer.Render`, uncommitted working tree (P0 — нет; P1 — нет; P2 — 4: RLM1–RLM4)

**Область (uncommitted; транзакции/bulk/CTAS в worktree — вне прохода).** Новый публичный `NextORM.Core.LockWaitMode` (`src/nextorm.core/Query/LockWaitMode.cs:4`, `enum { Wait, NoWait, SkipLocked }` — 3 члена `:7,9,11`); **2** новых публичных метода `EntityBuilder<TEntity>.ForUpdate(LockWaitMode)` (`src/nextorm.core/Builders/EntityBuilder.cs:779`) и `.ForShare(LockWaitMode)` (`:797`) — старые `ForUpdate()`/`ForShare()` (`:769,787`) сохранены; **1** новый DIM `ILockRenderer.Render(LockMode, LockWaitMode, KeywordCase = Lower)` (`src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:267`, дефолт делегирует старому `Render` при `Wait`, иначе бросает `NotSupportedException`); внутренний `LockClause` расширен `Wait` (`Query/LockClause.cs:8`); прокидывание в `SqlBuilder.cs:94,395`. Provider-override'ы нового overload'а — на `internal sealed`-рендерерах: PostgreSQL `:511`, MySQL `:459`, MariaDB `:61`, SQL Server `:675`; `MariaDbDialect.Lock` override (`src/nextorm.mariadb/MariaDbDialect.cs:49`). Реализации `ISqlDialect`/internal-механика — не публичная поверхность.

**Именование — P0 нет; новых P1 нет.** BCL-конфликтов нет (`LockWaitMode`/`Wait`/`NoWait`/`SkipLocked` не сталкиваются с типами/членами BCL; CA1716/CA1724 чисто). Имя `LockWaitMode` описывает **режим ожидания**, а не «конфликт» (SKIP LOCKED — не конфликтный режим), члены `NoWait`/`SkipLocked` зеркалят SQL `NOWAIT`/`SKIP LOCKED`, а `READPAST` (SQL Server) документирован как аппроксимация `SkipLocked` в XML-doc (`LockWaitMode.cs:10`). Выбор **перегрузок** `ForUpdate()`/`ForUpdate(LockWaitMode)` вместо optional-параметра — **правильный для extend-only**: добавление параметра с дефолтом к существующему методу source-совместимо, но **бинарно ломает** уже скомпилированные вызовы `ForUpdate()` (токен метода исчезает); перегрузки сохраняют оба токена — так и задумано (план фичи, шаг 3). DIM повторяет прецедент `MakeIif`/IF5 (throwing-DIM, source/binary-совместим для внешних реализаторов `ILockRenderer`). `Async`-суффикс не применим (нет async-пары), `CancellationToken` отсутствует. XML-doc новых публичных членов полный: `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`) + `TreatWarningsAsErrors=true` (`Directory.Build.props:40`), `NoWarn` — **0**, build **0/0** ⇒ CS1591 не подавлен, покрытие «тип + 3 члена + 2 overload + 1 DIM» = 100 %. Параметр `wait` (не `waitMode`) согласован с `LockClause.Wait`/`Render`; допустимо. **Split MySQL/MariaDB обоснован и размещён верно:** MariaDB официально принимает lock-option у `LOCK IN SHARE MODE`, MySQL — нет, поэтому `MariaDbDialect.Lock` возвращает отдельный `MariaDbLockRenderer` (`:49,52`), а MySQL для Share+режим рендерит `for share <mode>` (при `Wait` сохраняется `lock in share mode` ради обратной совместимости SQL) — подтверждено планом фичи и SQL-gen/диалект-тестами.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| RLM1 | P2 (док/покрытие) | `docs/guide/01-querying-and-projections.md:489-517` (+RU `:490-518`); `docs/advanced/api-reference.md:107` (+RU `:107`); `docs/guide/provider-specific/postgresql.md:198-201` (+RU `:201-204`); `docs/providers/overview.md` (строки row-locking нет) | **Публичные доки EN+RU не обновлены.** `guide/01` §«Row locking» описывает только `ForUpdate()`/`ForShare()` и `LockMode { Update, Share }`; `api-reference.md` — только `LockMode`; `provider-specific/postgresql.md` — `LockMode.Update`/`LockMode.Share`. Новый `LockWaitMode`, перегрузки с режимом и эмитируемые формы (`for update nowait`/`skip locked`, `lock in share mode <mode>`, `with (updlock, nowait)`/`with (updlock, readpast)`) в публичных доках отсутствуют (`rg -i 'nowait|skip locked|LockWaitMode|readpast' docs -g '!docs/specs/**'` — **0**). Это **пробел**, не противоречие. План фичи (шаг 3) требовал обновить `guide/01`, `limitations`, `api-reference`, `providers/overview` (EN+RU) | Обновить EN+RU: подраздел «Row locking» в `guide/01` — `ForUpdate(LockWaitMode)`/`ForShare(LockWaitMode)` + таблица Wait/NoWait/SkipLocked по провайдерам (`SKIP LOCKED` на PG/MySQL/MariaDB, `READPAST` на SQL Server — аппроксимация, пропускает **любую** блокировку); строку `LockWaitMode` в `api-reference.md` (EN+RU); строку row-locking в support matrix `providers/overview.md` (EN+RU). Маршрут `nextorm-design-engineer`; ссылки на `docs/specs/**` запрещены (AGENTS.md) |
| RLM2 | P2 (трекинг) | `PublicAPI.*.txt` — **0**; поверхность выше | Новая публичная поверхность не трекается: `LockWaitMode` (+3 значения enum), `EntityBuilder.ForUpdate/ForShare(LockWaitMode)` ×2, DIM `ILockRenderer.Render(LockMode, LockWaitMode, KeywordCase)`. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены (Шаг 5 открыт). **Продолжение MRG7/TR4/CTAS3/UPD2/IDML18/BK4** — не новая проблема | При заморозке внести подписи (точный текст — из анализатора); DIM — с дефолтом `KeywordCase = Lower`. Отдельной записи поверх MRG7 не требуется |
| RLM3 | P2 (покрытие/контракт) | `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs:153-154` | Контрактный тест вызывает только старый `lockRenderer.Render(LockMode.Share)`, тогда как прецедент `MakeIif`/IF5 обобщил этот тест на новый DIM. Новый `Render(LockMode, LockWaitMode, KeywordCase)` не проверяется обобщённо: диалект, который выставит `Lock` и не переопределит режимный overload, молча упадёт в `NotSupportedException` (DIM) — тест этого не поймает | Дополнить циклом: `foreach (var wait in Enum.GetValues<LockWaitMode>()) lockRenderer.Render(mode, wait).Should().NotBeNullOrEmpty();` (для обоих `LockMode`). Все 4 нынешних носителя (PG/MySQL/MariaDB/SqlServer) переопределяют overload, SQLite/ClickHouse `Lock == null` — тест пройдёт. Маршрут `nextorm-design-engineer` |
| RLM4 | P2 (док/устаревание) | `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:239-244` (класс-док `ILockRenderer`) | Summary описывает только форму, возвращаемую `Render(LockMode, KeywordCase)`, и утверждает «since one shape is selected by `UsesTableHints` the renderer never has an unreachable method»; режимный DIM и его throwing-фолбэк (`:267-270`) не упомянуты, а фраза про «never has an unreachable method» теперь неточна для внешней реализации без override | Дополнить summary/`<remarks>`: упомянуть `Render(LockMode, LockWaitMode, KeywordCase)`, что режимный суффикс (`nowait`/`skip locked`; `readpast` на SQL Server) сливается с table-hint'ом, и что базовая поддержка режима — DIM (throwing для не-`Wait`). Низкий приоритет, публичную форму не менять |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет** — RLM1/RLM2/RLM3/RLM4 все P2. (2) **Extend-only соблюдён:** старый `Render(LockMode, KeywordCase)` сохранён, новый — DIM с дефолтом (source/binary-совместим внешним реализаторам `ILockRenderer`); перегрузки fluent-методов сохраняют старый токен `ForUpdate()`/`ForShare()`; удалений/переименований нет. (3) XML-doc-покрытие 100 % (build `0/0`, CS1591 гейтится). (4) MySQL/MariaDB split обоснован (см. выше), `MariaDbLockRenderer` — `internal`. (5) `docs/specs/roadmap/todo_row_locking_wait_modes.md` удалён после шипмента (work-plan, шаг 10 плана; см. «Закрытие» ниже). (6) Surface-lock: `find -name 'PublicAPI*.txt'` — **0**, Шаг 5 открыт (RLM2). (7) Кодовая сторона — `code-smells-review.md`, Находки 145 (хэш-ключ) и 146 (DRY).

**Проверка (24.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.LockWaitMode` — 3 значения (`Wait:7`, `NoWait:9`, `SkipLocked:11`); `rg` по `ForUpdate(LockWaitMode)`/`ForShare(LockWaitMode)` — `EntityBuilder.cs:779,797`, DIM `DialectCapabilities.cs:267`, override'ы `PostgresDialect.cs:511`, `MySqlDialect.cs:459`, `MariaDbDialect.cs:61`, `SqlServerDialect.cs:675`; `rg -i 'nowait|skip locked|LockWaitMode|readpast' docs -g '!docs/specs/**'` — **0** (RLM1); контракт-тест lock — `DialectCapabilityContractTests.cs:153-154` (только старый overload, RLM3); `find -name 'PublicAPI*.txt'` — **0** (RLM2/Шаг 5); подавления `src/` — **6+5=11** (в lock-файлах — 0); EOL всех 9 затронутых lock-файлов — CRLF. Диалект/SQL-gen тесты режимов — PG `PostgresDialectTests.cs:55-58`, MySQL `MySqlDialectTests.cs:26-31`, MariaDB `MariaDbDialectTests.cs:32-37`, SQL Server `SqlServerDialectTests.cs:190-193`. Контейнерная интеграция (RLM3-тест `CommonTestSuite.Locking.cs`) аудитом не перезапускалась.

**Закрытие (24.09.2026, тот же день).** RLM1 — ✅ закрыта: публичные доки EN+RU дополнены режимами (`guide/01-querying-and-projections.md` §Row locking +RU, `advanced/api-reference.md`/`+RU` — строка `LockWaitMode`, `advanced/limitations.md`/`+RU` — строка режимов ожидания, `guide/provider-specific/postgresql.md`/`+RU`, `providers/sqlserver.md`/`+RU`, `providers/overview.md`/`+RU` — строка «Row-locking wait modes»); `rg -i 'nowait|skip locked|LockWaitMode|readpast' docs -g '!docs/specs/**'` больше не пуст. RLM3 — ✅ закрыта: `DialectCapabilityContractTests.cs` перебирает `LockMode × LockWaitMode` (`:153-160`); добавлен `tests/nextorm.core.tests/LockRendererDefaultTests.cs`, покрывающий throwing-фолбэк DIM (`ILockRenderer` — 100 %). RLM4 — ✅ закрыта: класс-док `ILockRenderer` (`DialectCapabilities.cs:239-245`) упоминает режимный overload, DIM-фолбэк и `NOWAIT`/`SKIP LOCKED`. RLM2 остаётся открытой (трекинг до Шага 5/`PublicAPI.*.txt`, как MRG7/BK4). Кодовая Находка 145 (хэш-ключ `QueryPlanEqualityComparer`) — ✅ исправлена (`hash.Add(rowLock.Wait)`); Находка 146 (DRY `LockWaitSuffix`) — принята осознанно (холодный путь построения SQL, 4 короткие копии без `InternalsVisibleTo`). После правок `dotnet build nextorm.sln -c Release` — **0/0**; SQL-gen/диалект/ядро-тесты зелёные; контейнерная интеграция `RowLock_SkipLocked` — PostgreSQL/MySQL/SQL Server **passed**, SQLite `skipped` (нет блокировок строк).

---

### Аудит 24.09.2026 — sampling перенесён с fluent-методов builder'а в объект опций `From` (`FromOptions`), uncommitted working tree (P0 — нет; P1 — нет; P2 — 6: FO1–FO6; **FM6 — ✅ закрыто**)

**Область (uncommitted).** Новый публичный `NextORM.Core.FromOptions` (`src/nextorm.core/DataContext/FromOptions.cs:9`, `public sealed class`, неявный public ctor) с **3** публичными fluent-методами: `TableSample(double percent, TableSampleMethod method = TableSampleMethod.System, double? seed = null)` (`:25`), `Sample(double ratio)` (`:43`), `Sample(double ratio, double offset)` (`:54`); internal-состояние `TableSampleClause`/`SampleRatio`/`SampleOffset` (`:11-13`, private setters). **2** новых перегруза extension (`DataContextExtensions.cs`): `From<T>(IDataContext, Action<FromOptions>, Action<EntityMetadataBuilder<T>>? = null)` (`:747`) и `From(IDataContext, string, Action<FromOptions>)` (`:911`); `ResolveMetadata` из `private` стал `internal` (`:178`). **Удалены** 3 публичных fluent-метода `EntityBuilder<TEntity>`: `Sample(double)`, `Sample(double,double)`, `TableSample(double,TableSampleMethod,double?)`. Internal-плумбинг не изменён: `EntityBuilder._tablesample` (`:32`), props `TableSampleClause`/`SampleRatio`/`SampleOffset` (`:99,107,109`), `CopyTo` (`:883-888`), копирование в `JoinedEntityBuilder` (`:85,203,306,409,512,615`).

**Именование — P0 нет.** BCL-конфликтов нет (CA1716/CA1724 чисто). `FromOptions` — суффикс `*Options` (прецедент `BulkInsertOptions`/`CreateTableAsOptions`). `TableSample`/`Sample` — те же имена, что были у удалённых fluent-методов; Pascal-разбиение `TableSample` (QM1) сохранено. XML-doc полный: `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`) + `TreatWarningsAsErrors=true` (`Directory.Build.props:40`), `NoWarn` — **0** ⇒ CS1591 гейтится, build `0/0` = покрытие «тип + ctor + 3 метода + 2 перегруза `From`» 100 %.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| FO1 | **P2 (BC/трекинг Шага 5)** | `EntityBuilder.cs` (удалены `Sample`×2/`TableSample`); `DataContextExtensions.cs:747,911`; `FromOptions.cs:9` | Extend-only нарушен **удалением**: 3 публичных fluent-метода сняты. Для уже скомпилированного кода — binary break (`MissingMethodException`), для исходников — source break. Новые 2 перегруза + тип — аддитивны. Вся поверхность uncommitted/alpha, удаление — решение владельца (допустимо до релиза), но должно быть зафиксировано в release notes/migration. `PublicAPI.*.txt` — **0** (Шаг 5 открыт). Продолжение MRG7/BK4/CTAS3/RLM2 | При заморозке внести `FromOptions` + 3 метода + 2 перегруза `From` и **не** вносить удалённые методы; в release notes отметить `.TableSample(...)`/`.Sample(...)` → `From<T>(o => o.…)` |
| FO2 | P2 (именование/дизайн) | `FromOptions.cs:9`; `FromRenderOptions.cs:15`; `BulkInsertOptions.cs:9` + `BulkInsertOptionsBuilder` | Два похожих имени в одном namespace `NextORM.Core`: публичный `FromOptions` и internal `FromRenderOptions` (флаги рендера). CLR-конфликта нет (internal не виден потребителю), но risk путаницы при чтении/поиске. Плюс `FromOptions` — **mutable fluent**-объект (методы возвращают `this`), тогда как sibling-`*Options` (`BulkInsertOptions`, `CreateTableAsOptions`) — immutable records, а их fluent-двойник назван `BulkInsertOptionsBuilder` | До заморозки рассмотреть: (а) переименовать internal `FromRenderOptions` → `FromRenderParameters` (без публичного impact) либо (б) назвать новый тип `FromOptionsBuilder` (симметрия `BulkInsertOptionsBuilder`); owner просил `FromOptions`/`Action<FromOptions>` — минимум принять с оговоркой. Публичную форму менять только по решению владельца |
| FO3 | P2 (дизайн/консистентность) | `EntityBuilder.cs:314,622,1428,1448,1458`; `FromOptions.cs:9` | На `From`-options переехали только `TableSample`/`Sample`, а прочие модификаторы **первичного источника** остались fluent на builder'е: `Final()` (`:314`), `ForSystemTime(...)` (`:622`), `WithTableHint(...)` (`:1428`), `WithIndex(...)` (`:1448,1458`), а также `Settings`/`PreWhere`/`LimitBy`. Конфигурация primary-source расщеплена между `FromOptions` и builder'ом | Зафиксировать направление: follow-up переносит остальные primary-source модификаторы в `FromOptions` (тогда `EntityBuilder`-версии снова удаляются = BC-break), либо в XML-doc/гайде явно сказано, что `From`-options — только sampling. P2 design note, не блокер |
| FO4 | P2 (док/точность) | `FromOptions.cs:3-8`; `DataContextExtensions.cs:747` | Записанное в рабочем плане «accepted edge» — `From<T>(_ => {})` даёт `CS0121` — **не воспроизводится** (probe). Из-за tie-breaker'а C# «better function member» (метод без подстановки optional-параметров лучше) 1-параметровый `From<T>(Action<EntityMetadataBuilder<T>>?)` выигрывает у 2-параметрового `From<T>(Action<FromOptions>, Action<…>?)`; лямбда без обращений молча привязывается к **metadata**-overload'у (no-op). `CS0121` даёт только литерал `null`, и он разрешается между `Action<EntityMetadataBuilder<T>>?` и pre-existing `EntityBuilder<TResult>`/`QueryCommand<TResult>` — не связан с фичей. Существующие call-sites (все — `o => o.TableSample(…)/o => o.Sample(…)`) привязываются к `FromOptions` корректно; rebind'ов нет | CS0121-риска нет; `From<T>(_ => {})` — молчаливый no-op (значимого поведения у такой лямбды нет). Формулировка «accepted edge» удалена вместе с рабочим планом; доп. docs-митигация не требуется |
| FO5 | P2 (поведение/не rename) | `FromOptions.cs:27` (было `EntityBuilder.cs:650` в HEAD) | `TableSample` получил guard `!double.IsFinite(percent)` — **изменение поведения**, а не просто перенос: старая проверка `percent <= 0 \|\| percent > 100` пропускала `double.NaN` (оба сравнения ложны) и рендерила `tablesample NaN`; теперь `TableSample(double.NaN)` бросает `ArgumentOutOfRangeException`. Закрывает NaN-дыру и для `TableSample` (FM6 покрывал только `Sample`). Тест — `FromOptionsTests.TableSample_WithInvalidPercent_ShouldThrow`, `[InlineData(double.NaN)]` | Зафиксировать в release notes как bugfix-изменение поведения (нефинитный percent теперь отклоняется). Публичную форму не менять |
| FO6 | P2 (поведение/консистентность) | `DataContextExtensions.cs:729-734` (было `ContainsKey`); `:178-189` (`ResolveMetadata`) | `From<T>` переведён с ручной `DataContextCache.Metadata.ContainsKey` на общий `ResolveMetadata`, который **также** пересобирает метаданные, если кэш есть, но `TableName` пуст (`!TryGetValue(...) \|\| IsNullOrEmpty(metadata.TableName)`). Легитимное выравнивание с `InsertInto`/`Update`/`MergeInto`, но **изменение поведения** на крае: для типа в кэше с пустым `TableName` `configEntity` теперь может выполниться и запись перезапишется, тогда как раньше — нет. Регрессии не выявлено (обычный mapping даёт непустой `TableName`, пересборка идемпотентна) | Зафиксировать как осознанное выравнивание; при желании — тест идемпотентности повторного `From<T>`. Публичную форму не менять |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет.** FM6 **закрыто** (guard `Sample` был уже в HEAD, реестр был stale; теперь живёт в `FromOptions.Sample` — `FromOptions.cs:56,59`; `EntityBuilder.Sample` удалён). (2) **Утечки/алиасинга нет:** `new FromOptions()` создаётся на каждый вызов, значения копируются в builder; `TableSampleClause` — immutable positional record (`Query/TableSampleClause.cs:7`), `SampleRatio`/`SampleOffset` — value-копии; объект не удерживается, не `IDisposable`; мутация переданного экземпляра после вызова на builder не влияет. (3) **Cache-key:** `TableSample`/`Sample` уже входят в план-ключ (TableSample-блок `QueryPlanEqualityComparer`); новых ключей нет. (4) **God class не усугублён:** `EntityBuilder.cs` уменьшился на ~48 строк (pre-existing Находка 68 не переоткрывается). (5) **Suppression ratio — 11/11** (6 `SuppressMessage`, все с `Justification` + 5 парных `#pragma`), новых в изменении — 0. (6) `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 инертных `S*` без `SonarAnalyzer` + `CA2254`; пре-существующее, не переоткрывается). (7) **Stale internal doc:** `Query/TableSampleClause.cs:4` всё ещё гласит «attached to a query through `EntityBuilder.TableSample`» — метод удалён (см. `code-smells-review.md`, Находка 151). (8) **Extend-only по overload'ам `From`:** добавление перегрузок binary-совместимо; source-риск rebind'а отсутствует (все существующие вызовы member-touching). (9) Шаг 5 открыт: `PublicAPI.*.txt` — **0**; `docs/specs/**` — internal-план, не публичная поверхность.

**Проверка (24.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.FromOptions` — **3** public-метода (+ internal props с private-сеттерами) + неявный public ctor; `roslyn members NextORM.Core.DataContextExtensions` — 2 `From<T>` (`Action<EntityMetadataBuilder<T>>?` :729, `Action<FromOptions>` :747) + 2 `From` (`string` :899, `string, Action<FromOptions>` :911); `rg 'public EntityBuilder<TEntity> (Sample|TableSample)' src/nextorm.core/Builders/EntityBuilder.cs` — **пусто** (удалены); probe `/tmp` (net10.0, Release) — `From<T>(_ => {})` **компилируется** без `CS0121` (bind к metadata-overload, подтверждено изолированным тестом `F(Action<A>?)` vs `F(Action<B>, Action<A>?=null)` → выигрывает 1-параметровый), `From<T>(null)` даёт `CS0121` между `Action<EntityMetadataBuilder<T>>?` и `EntityBuilder<TResult>` (pre-existing, не из-за фичи); `rg '\.TableSample\(|\.Sample\(' docs --glob '!docs/specs/**'` — все вхождения в новом виде (`FromOptions.TableSample`/`o.TableSample`/`o.Sample`), EN+RU синхронны; `find -name 'PublicAPI*.txt'` — **0** (Шаг 5). Тесты (Release, `--no-build`): core `FromOptionsTests` **13/13**, `InMemoryTests` **80/80**; SQL-gen — postgres **260/260**, sqlserver **210/210**, clickhouse **190/190**, mysql **57/57**, sqlite **211/211** (0 failed, 0 skipped). Контейнерная интеграция аудитом не перезапускалась. Кодовая сторона — `code-smells-review.md`, Находка 151.

---

### Аудит 24.09.2026 — `ForJson`/`ForXml` стали терминалами, добавлены композибл-методы `WithForJson`/`WithForXml`, uncommitted working tree (P0 — нет; P1 — нет; P2 — 3: FJ1–FJ2 открыты, FJ3 — ✅ закрыта 24.09.2026)

**Область (uncommitted).** `src/nextorm.core/Query/QueryCommand.TResult.cs`: `ForJson(ForJsonMode, string?, bool, params object[]?)` (`:678`) и `ForXml(ForXmlMode, string?, string?, bool, params object[]?)` (`:700`) сменили возврат `QueryCommand<TResult>` → `string?` (терминалы, sync); добавлены `ForJsonAsync` (`:687`) и `ForXmlAsync` (`:710`) → `Task<string?>`, `WithForJson` (`:640`) / `WithForXml` (`:659`) → `QueryCommand<TResult>` (старые тела, переименованы); private `ExecuteDocument`/`ExecuteDocumentAsync`/`CreateDocumentCommand` (`:713/:719/:725`). `QueryCommand.cs` — новый `internal bool DocumentMode` (`:225`, рядом с `OneColumn`/`SingleRow`); `QueryCommand.Clone.cs` — `CopyTo` копирует `DocumentMode` (`:65`); `DataContext/QueryPlanner.cs:391` — при `DocumentMode` маппер не компилируется (скаляр через `DbCommand.ExecuteScalar`). Тесты: SQL-gen `nextorm.sqlserver.tests`/`nextorm.sqlite.tests` переведены на `WithForJson`/`WithForXml`; новые `tests/nextorm.integration.tests/SqlServerForJsonTests.cs` (6) и in-memory guard в `tests/nextorm.core.tests/InMemoryTests.cs:58-77` (3). Доки EN+RU (`guide/01-querying-and-projections`, `guide/18-json`, `guide/provider-specific/sqlserver`, `providers/sqlserver`, `advanced/api-reference`, `advanced/limitations`) обновлены.

**Именование — P0 нет.** BCL-конфликтов нет (`ForJson`/`ForXml`/`WithForJson`/`WithForXml` в BCL отсутствуют; `ForJson`/`ForXml` уже были членами). `Async`-суффикс корректен (есть sync-двойники `ForJson`/`ForXml`). `With*` соответствует прецеденту `WithNamingConvention`/`WithKeywordCase`/`WithTableHint`. XML-doc: `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`) + `TreatWarningsAsErrors=true` (`Directory.Build.props:40`), `NoWarn` — **0** ⇒ CS1591 гейтится; build `0/0` = все **6** новых/изменённых публичных методов покрыты (`<summary>`/`<param>`/`<returns>`); приватные `ExecuteDocument*`/`CreateDocumentCommand` под CS1591 не попадают.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| FJ1 | **P2 (BC/трекинг Шага 5)** | `QueryCommand.TResult.cs:678,700` | Extend-only нарушен **сменой возврата**: `ForJson(ForJsonMode,string?,bool)` и `ForXml(ForXmlMode,string?,string?,bool)` были `QueryCommand<TResult>` → стали `string?`. Это binary break (return-тип входит в подпись) и source break (цепочки `.ForJson().Hint(...)`/`.ForJson().ToList()` не компилируются). Поверхность uncommitted/alpha, решение владельца (допустимо до релиза); `WithForJson`/`WithForXml` сохраняют прежнюю семантику. `PublicAPI.*.txt` — **0**. Продолжение FO1/MRG7/BK4/CTAS3/UPD2/RLM2 | При заморозке внести 6 подписей (`ForJson`/`ForXml` terminal, `ForJsonAsync`/`ForXmlAsync`, `WithForJson`/`WithForXml`); в release notes отметить `.ForJson(...)`/`.ForXml(...)` → терминал, `.WithForJson(...)`/`.WithForXml(...)` → композиция |
| FJ2 | P2 (именование/дизайн) | `QueryCommand.TResult.cs:640,659` | `WithForJson`/`WithForXml` — механическая конкатенация `With`+`For…` (читается как «with for json»); прочие `With*` берут существительное/прилагательное (`WithKeywordCase`, `WithNamingConvention`, `WithTableHint`). Пара «терминал `ForJson` / композибл `WithForJson`» асимметрична: одно и то же имя с/без `With` означает «выполнить» vs «присоединить» | Оставить по решению владельца (директива). Опционально к заморозке: `ForJsonClause`/`ForXmlClause` (паттерн существительных `OrderBy`/`Where`) либо `AsJson`/`ToJson`; переименование — тоже source-break, цена та же |
| FJ3 | P2 (док/точность) — ✅ **ЗАКРЫТА 24.09.2026** | `QueryCommand.TResult.cs:709`; `tests/nextorm.integration.tests/SqlServerForJsonTests.cs:76-84`; `docs/guide/18-json.md:86` (+RU `:86`); `docs/guide/01-querying-and-projections.md:467,489` (+RU) | XML-`<returns>` `ForXml` и публичные доки утверждают «`null` при пустом результате». Для `ForJson` это подтверждено тестом, для `ForXml` пустой результат **не был покрыт**; гипотеза «с `ROOT('items')` вернётся `<items/>`, а не NULL» на живом SQL Server **не подтвердилась**. **Применено 24.09.2026:** сырой SQL `select 1 as x where 1=0 for xml path, root('items')` и `... for xml path` → SQL NULL (`emptyRoot=[<null>] emptyNoRoot=[<null>]`); добавлен контейнерный тест `ForXml_NoRows_ShouldReturnNull` (`:76-84`), `SqlServerForJsonTests` — **6/6 passed** | Контракт `<returns>` и доки EN+RU соответствуют факту — правок не требуется. Кодовая сторона — `code-smells-review.md`, Находка 153 (✅ закрыта) |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет.** (2) **CA1068 не сработал:** `ForJsonAsync`/`ForXmlAsync` ставят `CancellationToken` перед `params`; в .NET 10 CA1068 включён **как suggestion** (не warning, `TreatWarningsAsErrors` его не поднимает), и такой же порядок уже в `QueryCommand<TResult>.CreateAsyncEnumerator(CancellationToken, params object[]?)` (`:64`, тот же файл) — прецедент. Sync-`params` оставлен ради единой формы с прочими терминалами; при желании можно вынести параметры в отдельные overload'ы, но это чистая перестановка. (3) **Overload-resolution:** 4-й параметр (`params`) новый, старые вызовы `ForJson(mode, root, includeNullValues)`/`ForXml(mode, elementName, root, elements)` биндятся без изменений (`SqlGenerationTests` зелёные); амбивалентности с `WithForJson`/`WithForXml` нет (другие имена). (4) **Nullability честная:** `string?`/`Task<string?>` + `<returns>` описывает `null`; `params object[]?` аннотирован. (5) `DocumentMode` — internal, в `CopyTo` копируется (`Clone.cs:65`); с 24.09.2026 входит в план-ключ (`QueryPlanEqualityComparer.cs:120` сравнение, `:368` хеш) — коллизия null-мапперного плана закрыта (кодовая Находка 152 ✅). (6) **Guard `_dataContext is not DataContext` корректен:** `InMemoryDataContext : IDataContext` (`InMemoryDataContext.cs:13`) не наследует `DataContext` (`DataContext.cs:14`); roslyn implementations `IDataContext` — **8**, из них 7 реляционных идут от `DataContext` (`MariaDbDataContext : MySqlDataContext : DataContext`). (7) **Доки EN+RU и xref обновлены** под новые подписи (`api-reference` ссылается на `ForJson(...System.Object[])`/`WithForJson(...)`); `dotnet docfx docs/docfx.json` — **0 warnings / 0 errors**. (8) Шаг 5 открыт: `PublicAPI.*.txt` — **0**; `docs/specs/**` — internal.

**Проверка (24.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet docfx docs/docfx.json` — **0 warnings / 0 errors**; тесты (Debug): core `TestForJson|TestForXml` **3/3**, sqlserver `ForJson|ForXml` **6/6** (SQL-gen `WithForJson_*`/`WithForXml_*`), sqlite **2/2**; контейнерная интеграция SQL Server `SqlServerForJsonTests` — **6/6** (5 исходных + новый `ForXml_NoRows_ShouldReturnNull`). `roslyn refs NextORM.Core.QueryCommand.DocumentMode` — set `QueryCommand.TResult.cs:734`, copy `QueryCommand.Clone.cs:65`, read `QueryPlanner.cs:391`; с 24.09.2026 сравнение/хеш — `QueryPlanEqualityComparer.cs:120` (`Equals`) и `:368` (`GetHashCode`), кодовая Находка 152 ✅ закрыта. `find -name 'PublicAPI*.txt'` — **0**.

---

### Аудит 24.09.2026 — C# string semantics (G12): `IStringFormatFunctions`, 5 членов `ISqlDialect`, `CommonFunctions.collate`, `SqlFunctions.Sql`, uncommitted working tree (P0 — нет; P1 — нет; P2 — 4: SS1–SS4)

**Область (uncommitted).** Новый публичный `NextORM.Core.IStringFormatFunctions` (`src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:66`, 4 метода: `SupportsNumber(char)` `:69`, `RenderNumber(string,char,int)` `:76`, `SupportsDateFormat(string)` `:79`, `RenderDate(string,string)` `:82`). **5** новых членов `ISqlDialect` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`): `StringFormats` get `:746`, `SupportsCollation` get `:753`, `MakeCollate(string,string,KeywordCase=Lower)` `:759`, `SupportsOrdinalComparison` get `:765`, `MakeOrdinal(string,bool)` `:773` (первые три + `SupportsOrdinalComparison` — DIM, `MakeCollate`/`MakeOrdinal` — абстрактные; база `SqlDialectBase.cs:488-502`). Новый публичный член `CommonFunctions.collate(string?,string?)` (`src/nextorm.core/Query/SqlFunctions.cs:393`). Изменён `SqlFunctions.Sql` (`:26`): был `public static CommonFunctions Sql => default!;`, стал `public static CommonFunctions Sql { get; } = new CommonFunctions();` — подпись та же, поведение другое. Провайдерные override'ы (не новая публичная поверхность — расширяют `public virtual` из базы): `PostgresDialect.cs:374-388`, `SqlServerDialect.cs:319-336`, `MySqlDialect.cs:363-379` (MariaDB наследует), `SqliteDialect.cs:120-136`, `ClickHouseDialect.cs:482-492`; `internal sealed *StringFormats`-классы не публичны. Тесты — 7 новых файлов, 41 тест.

**Именование — P0 нет; новых P1 нет.** BCL-конфликтов нет (CA1716/CA1724 чисто): `IStringFormatFunctions` — префикс `I` + plural `Functions`, как `ISessionInfoFunctions`/`IUuidGenerators`; `StringFormats`/`Supports*`/`Make*` согласованы с существующей поверхностью `ISqlDialect` (`SupportsFullText`, `MakeUpper`, `MakeStringLastIndexOf`); `collate` сохраняет snake_case SQL-поверхности (`like`, `@in`, `current_user`, заявлено P1-10). Параметры `(string value, string collation, KeywordCase keywordCase = Lower)` — целевой объект, обязательные, затем optional; `CancellationToken` не применим (синхронные рендереры). XML-doc полный: `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`) + `TreatWarningsAsErrors=true` (`Directory.Build.props:40`), `NoWarn` — **0** ⇒ CS1591 гейтится, build `0/0` = покрытие «1 интерфейс + 4 метода + 5 членов `ISqlDialect` + `collate` + переписанный `Sql`» **100 %**. `Sql.get` в трекинге не меняется (подпись без изменений).

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| SS1 | P2 (BC/трекинг Шага 5) | `PublicAPI.*.txt` — **0**; поверхность выше | Новая публичная поверхность не трекается: `IStringFormatFunctions` (+4 метода), 5 членов `ISqlDialect` (4 DIM с дефолтом + 2 абстрактных), `CommonFunctions.collate`. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены (Шаг 5 открыт). Продолжение RLM2/FO1/FJ1/CTAS3/UPD2/BK4 | При заморозке внести подписи (точный текст — из анализатора); DIM — с дефолтами, `MakeCollate`/`MakeOrdinal` — как обычные члены. Отдельной записи поверх RLM2 не требуется |
| SS2 | P2 (док/охват) | `docs/guide/11-scalar-functions.md` (+RU), `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU); `rg -niE "collate\|string\.format\|tostring\(format\|ordinalignorecase" docs -g '!docs/specs/**'` — **0** | **Публичные доки EN+RU не обновлены.** Новые возможности (`SqlFunctions.Sql.collate`, ordinal `StringComparison`/`string.Equals`/`Compare`/`IndexOf`, `ToString(fmt)`/`string.Format`/интерполяция с форматом, ограничения культур) в публичной прозе отсутствуют; план фичи (шаг «Файлы») прямо требовал `guide/11`, `limitations`, `api-reference` (EN+RU). Изменённый `docs/guide/02-filtering-where.md` (+RU) относится к коррелированным подзапросам — другая фича | Дополнить EN+RU: раздел «Collation» (`collate` + `MakeCollate`; ClickHouse без `COLLATE`), «String comparison» (ordinal `StringComparison` → бинарная коллация), «Formatting» (`ToString(fmt)`/`string.Format`/`$"{x:fmt}"`, поддерживаемые `N/F/D/X` и дата-токены, отказ на культуру); строки `IStringFormatFunctions`/`StringFormats`/`SupportsCollation`/`SupportsOrdinalComparison`/`MakeCollate`/`MakeOrdinal`/`collate` в `api-reference`; ограничение in-memory/культуры в `limitations`. Маршрут `nextorm-design-engineer`; ссылки на `docs/specs/**` запрещены (AGENTS.md) |
| SS3 | P2 (API-контракт/BC) | `ISqlDialect.cs:746-765` (DIM) vs `:759,:773` (абстрактные, `;`); док `:742-753,:760-765` | `StringFormats`/`SupportsCollation`/`SupportsOrdinalComparison` объявлены DIM **«so that existing external implementations keep compiling»**, но `MakeCollate`/`MakeOrdinal` — **абстрактные** (без тела). Внешний класс, реализующий `ISqlDialect` напрямую (не через `SqlDialectBase`), получит CS0535 на оба метода — заявленная совместимость не достигается; для уже скомпилированного реализатора добавление абстрактного члена интерфейса — binary break. Прецедент обратного (`ILockRenderer.Render`/`MakeIif` — DIM с throwing-дефолтом) в реестре зафиксирован | Либо сделать `MakeCollate`/`MakeOrdinal` DIM с throwing-дефолтом (`=> throw new NotSupportedException(...)`), как `ILockRenderer.Render`, либо убрать из доков утверждение про «keep compiling» и зафиксировать BC-break в release notes. Публичную форму не менять |
| SS4 | P2 (поведение/контракт) | `SqlFunctions.cs:26` (был `=> default!`), док `:22-25,:302-305`; `Postgres`/`SqlServer`/`ClickHouse` `:34,:43,:63` — по-прежнему `default!` | `Sql` перестал быть null-заглушкой: подпись не изменилась, поведение — да. Док обещает «the in-memory provider may execute the small subset that has a native CLR equivalent», но нативных членов один (`collate`), а остальные отдают `default!`, что в in-memory даёт тихо неверный результат (детали и probe — `code-smells-review.md`, Находка 154). Плюс `Sql` теперь singleton, а `Postgres`/`SqlServer`/`ClickHouse` — нет: поверхность рассинхронизирована | Не менять форму, но закрыть тихий путь (Находка 154) и синхронизировать XML-doc `Sql`/`CommonFunctions` с фактическим поведением. Зафиксировать в release notes как изменение поведения |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет.** (2) **Extend-only:** 5 новых членов `ISqlDialect` + интерфейс + `collate` — аддитивны; DIM-свойства source/binary-совместимы (но см. SS3); удалений/переименований нет; `SqlFunctions.Sql` — без смены подписи. (3) XML-doc 100 % (build `0/0`). (4) `Sql` теперь с backing-field (`Sql.field`, `:26`) — трекинг `Sql.get` не меняется. (5) Имена `IStringFormatFunctions`/`StringFormats` родственны `ISessionInfoFunctions`/`SessionInfoFunctions`, не BCL; `*StringFormats`-классы провайдеров `internal`. (6) `CompositeFormat` — internal (не поверхность), одноимён `System.Text.CompositeFormat` — ℹ️ в `code-smells-review.md` (Наблюдение F). (7) Шаг 5 открыт: `find -name 'PublicAPI*.txt'` — **0**. (8) Доки EN+RU для G12 не тронуты; изменённый `docs/guide/02-filtering-where.md` (+RU) относится к коррелированным подзапросам, не к string semantics.

**Проверка (24.09.2026).** `dotnet build nextorm.slnx -c Release -p:NextOrmVersion=1.0.4-alpha` — **0 warnings / 0 errors** (без override — `NU1102` ×6 из-за uncommitted bump `NextOrmVersion` 1.0.5-alpha, см. `code-smells-review.md`, Наблюдение A); `roslyn members NextORM.Core.IStringFormatFunctions` — 4 метода (`DialectCapabilities.cs:69,76,79,82`); `roslyn members NextORM.Core.SqlFunctions` — `Sql` get `SqlFunctions.cs:26` (+ backing field `:26:35`); `grep -n` по `ISqlDialect.cs` — `StringFormats:746`, `SupportsCollation:753`, `MakeCollate:759`, `SupportsOrdinalComparison:765`, `MakeOrdinal:773`; `rg -niE "collate|string\.format|tostring\(format|ordinalignorecase" docs -g '!docs/specs/**'` — **0** (SS2); `find -name 'PublicAPI*.txt'` — **0** (SS1). Тесты `StringSemantics` — **41/41 passed** (core 5 + sqlite 5 + postgres 14 + mysql 5 + mariadb 3 + sqlserver 5 + clickhouse 4, Release `--no-build`).

### ✅ Статус после применения (24.09.2026, тот же рабочий tree)

- **SS1 (P2)** — принято: `PublicAPI.*.txt`/API-approval не заведены (Шаг 5 открыт, продолжение RLM2/FO1/FJ1/…). Новые подписи отслеживаются через этот реестр до заморозки поверхности. Публичную форму не меняли.
- **SS2 (P2)** — ✅ **закрыто.** Публичная проза EN+RU обновлена: `docs/guide/11-scalar-functions.md` (+RU) — новый раздел «Ordinal comparison and collation» / «Ordinal-сравнение и коллация», переписан «Formatting dates and numbers» / «Форматирование дат и чисел» (подмножество `N`/`F`/`D`/`X` и дата-токенов, invariant-only, таблица по провайдерам), строки в таблице строковых функций и в таблице сопоставления провайдеров, пополнен «Explicitly unsupported» / «Явно не поддерживается»; `docs/advanced/limitations.md` (+RU) — строки про подмножество форматирования/ordinal и про колонковую коллацию (`#28`); `docs/advanced/api-reference.md` (+RU) — `collate`/`SupportsCollation` в строке `CommonFunctions`. Ссылок на `docs/specs/**` нет.
- **SS3 (P2)** — ✅ **закрыто.** `MakeCollate`/`MakeOrdinal` объявлены **DIM с throwing-дефолтом** (`ISqlDialect.cs`), как `ILockRenderer.Render`: внешний реализатор `ISqlDialect` напрямую теперь компилируется без CS0535, заявленная совместимость достигнута. Публичную форму не меняли.
- **SS4 (P2)** — ✅ **закрыто.** `SqlFunctions.Sql` снова `=> default!`; смена поведения singleton'а откатана (Находка 154), XML-doc `Sql` и `CommonFunctions` синхронизированы с фактическим поведением (marker + rewrite в in-memory). `Postgres`/`SqlServer`/`ClickHouse` остаются `default!` — рассинхронизации больше нет.

---

### Аудит 24.09.2026 — колонки-длительности (`DurationUnit`, `DurationAttribute`, `Duration(unit, precision)`, `SupportsNativeDuration`/`MakeDurationType`), uncommitted working tree (P0 — нет; P1 — нет; P2 — 4: DU1–DU4)

**Область.** Публичная поверхность. `NextORM.Core.DurationUnit` — enum, 7 членов: `Ticks`, `Microseconds`, `Milliseconds`, `Seconds`, `Minutes`, `Hours`, `Days` (`src/nextorm.core/DurationUnit.cs:9`). `NextORM.Core.DurationAttribute` — `sealed`, `[AttributeUsage(Property, AllowMultiple=false, Inherited=true)]`, `.ctor()`, `.ctor(DurationUnit)`, `Unit { get;set; }`, `Precision { get;set; }` (`DurationAttribute.cs:16,21,30,38,44`). `EntityPropertyBuilder<T>.Duration(DurationUnit, int = 0)` (`EntityPropertyBuilder.cs:81`). DIM `IPropertyMetadata.DurationUnit => null` (`:66`) и `.DurationPrecision => 0` (`:74`). DIM `ISqlDialect.SupportsNativeDuration => false` (`ISqlDialect.cs:312`) и `MakeDurationType(DurationUnit?, int = 0) => "bigint"` (`:324`); override'ы — `PostgresDialect.cs:132`, `MySqlDialect.cs:324`, база `SqlDialectBase.cs:107`. `SelectExpression.DurationUnit { get;set; }`/`.DurationPrecision { get;set; }` (`SelectExpression.cs:35,40`). `DurationStorage` — internal, не поверхность.

**Именование — P0 нет; новых P1 нет.** BCL-конфликтов нет (CA1716/CA1724 чисто): `DurationUnit` — не `TimeUnit`/`Timeout`, члены единственного числа, без аббревиатур (`Microseconds`, а не `us`/`usec`); `DurationAttribute` несёт обязательный суффикс `Attribute` и sealed; `EntityPropertyBuilder<T>.Duration` — глагол-настройка в духе `Column`/`Key`; `SupportsNativeDuration`/`MakeDurationType` согласованы с существующими `Supports*`/`Make*` на `ISqlDialect`. Nullable-параметр `DurationUnit?` на `MakeDurationType` (null = native) и nullable-свойство `IPropertyMetadata.DurationUnit` намеренны; асимметрия с не-nullable `int DurationPrecision` (0 = default) отмечена в DU2. XML-doc полный: `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` — 0 ⇒ CS1591 гейтится, build `0/0` = покрытие «enum (7) + атрибут (5) + fluent + 2 DIM метаданных + 2 DIM диалекта + 2 свойства `SelectExpression`» **100 %**.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| DU1 | P2 (трекинг Шага 5) | `PublicAPI.*.txt` — **0**; поверхность выше | Новая публичная поверхность не трекается: `DurationUnit` + `DurationAttribute` + `EntityPropertyBuilder<T>.Duration` + 4 DIM + 2 `SelectExpression`-свойства. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены. Продолжение SS1/RLM2/FO1/… | При заморозке внести подписи (точный текст — из анализатора); DIM — с дефолтами |
| DU2 | P2 (контракт/мёртвая настройка) | `DurationAttribute.cs:44`, `IPropertyMetadata.cs:74`, `EntityPropertyBuilder.cs:81`, `SelectExpression.cs:40`, `ISqlDialect.cs:324`/`SqlDialectBase.cs:107` | `Precision` (и `MakeDurationType`) не используется продакшеном: тип колонки даёт `MakeTypeName(Type)`, `unit`/`precision` игнорируются; XML-doc обещает `TIME(3)`/`interval(3)`. `SelectExpression.DurationPrecision` — публичное write-only свойство (кодовая Находка 164) | Либо подключить `MakeDurationType`/precision в рендер типа колонки и добавить DDL-тест, либо не документировать как работающее и пометить `[Obsolete]`/перевести в internal до lock. Публичную форму без решения не менять |
| DU3 | P2 (консистентность хуков/BC) | `ClickHouseDialect.cs:640` (`MakeTypeName(TimeSpan) == "Int64"`) vs `MakeDurationType` (наследует `SqlDialectBase.cs:107` → `"bigint"`) | Два публичных hook'а дают разные имена типа для одной сущности; тест `ClickHouseDialectTests.DurationHooks_ShouldUseIntegerStorage` фиксирует оба. Внешний реализатор дезориентирован | Согласовать: override `MakeDurationType` в ClickHouse → `"Int64"` (как `MakeTypeName`) либо удалить неиспользуемый hook (см. DU2). **✅ Устранено 24.09.2026:** `ClickHouseDialect.MakeDurationType` → `"Int64"`. |
| DU4 | P2 (док/охват) | Публичные доки EN+RU: `grep -rniE "durationattribute|durationunit" docs -g '!docs/specs/**'` — **0** | Новая публичная возможность (маппинг `TimeSpan`, `[Duration]`, `DurationUnit`, хранение в integer vs native по провайдерам) не описана в публичной прозе; изменённый `docs/guide/02-filtering-where.md` (+RU) относится к коррелированным подзапросам, не к длительностям | Добавить раздел «TimeSpan / duration columns» (EN+RU) и строки `DurationAttribute`/`DurationUnit`/`Duration`/`SupportsNativeDuration`/`MakeDurationType` в `advanced/api-reference`; ссылки на `docs/specs/**` запрещены (AGENTS.md). **✅ Устранено 24.09.2026:** [`docs/guide/26-duration-columns.md`](../../guide/26-duration-columns.md) (+RU) + строки `DurationAttribute`/`DurationUnit` в `docs/advanced/api-reference.md` (+RU). |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет.** (2) **Extend-only:** все члены аддитивны, удалений/переименований/смены подписей нет; 4 DIM source/binary-совместимы (внешний реализатор `IPropertyMetadata`/`ISqlDialect` компилируется без доработки). (3) XML-doc 100 % (build `0/0`). (4) `DurationUnit` не конфликтует с BCL; `DurationAttribute` — `sealed`; `Unit`/`Precision` изменяемы (named-args атрибута) — допустимо. (5) Асимметрия `DurationUnit?` ↔ `int DurationPrecision` — осознанна (null = native, 0 = provider default), но для внешнего реализатора неочевидна (DU2). (6) Шаг 5 открыт: `find -name 'PublicAPI*.txt'` — **0**; `docs/specs/**` — internal. (7) Доки EN+RU не тронуты (DU4); `DurationStorage` — internal, в поверхность не входит.

**Проверка (24.09.2026).** `dotnet build nextorm.slnx -c Release -p:NextOrmVersion=1.0.4-alpha` — **0 warnings / 0 errors** (без override — `NU1102` ×6 из-за uncommitted bump 1.0.5-alpha, см. `code-smells-review.md`, Наблюдение A предыдущего раздела); `roslyn members NextORM.Core.DurationUnit` — 7 членов; `roslyn members NextORM.Core.DurationAttribute` — ctor ×2 + `Unit.get/set` + `Precision.get/set`; `roslyn callers ISqlDialect.MakeDurationType` — 10, все в тестах (DU2); `grep -rniE "durationattribute|durationunit" docs -g '!docs/specs/**'` — **0** (DU4); `find -name 'PublicAPI*.txt'` — **0** (DU1). XML-doc гейтится build `0/0`.

---

### Аудит 24.09.2026 — ширина `date_diff` (§8.3) и sub-day promotion (§8.2): `date_diff_big`, `MakeDateDiffBig`/`MakeDateDiffCore`, `PromoteDateOperand`, uncommitted working tree (P0 — нет; P1 — нет; P2 — 2: DB1–DB2)

**Область.** Публичная поверхность инкремента `todo_timespan_columns.md` §8.2/§8.3. Новые **публичные** члены: `CommonFunctions.date_diff_big(string, DateTime?, DateTime?) → long?` (`Query/SqlFunctions.cs:554`); DIM `ISqlDialect.MakeDateDiffBig(string,string,string)` (`DataContext/Dialect/ISqlDialect.cs:888`, дефолт → `MakeDateDiff`) и DIM `ISqlDialect.PromoteDateOperand(string,string)` (`:897`, дефолт → `value` без изменений). Новая **protected**-поверхность `SqlDialectBase`: `MakeDateDiffBig` (`:579`), `MakeDateDiffCore(string,string,string,bool)` (`:586`), `IsSubDayField(string) static` (`:607`), `PromoteDateOperand` (`:611`). Override'ы: `SqlServerDialect.MakeDateDiffBig` (`SqlServerDialect.cs:431` → `datediff_big`), `SqlServerDialect.PromoteDateOperand` (`:447` → `cast(value as datetime2)`), `PostgresDialect.MakeDateDiffBig` (`PostgresDialect.cs:293` → `MakeDateDiffCore(..., big:true)`, cast `bigint`), `ClickHouseDialect.PromoteDateOperand` (`ClickHouseDialect.cs:618` → `toDateTime`/`toDateTime64(v,3|6)`). `date_diff` остаётся `int?`; его тело делегировано `MakeDateDiffCore(..., big:false)` с идентичным прежнему SQL. Публичные доки EN+RU называют `date_diff_big` (`docs/guide/11-scalar-functions.md` (+RU), `docs/advanced/api-reference.md` (+RU)); ссылок на `docs/specs/**` нет.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| DB1 | P2 (трекинг Шага 5) | `PublicAPI.*.txt` — **0**; поверхность выше | Новая публичная/защищённая поверхность не трекается: `CommonFunctions.date_diff_big`, 2 DIM, `MakeDateDiffBig`/`MakeDateDiffCore`/`IsSubDayField`/`PromoteDateOperand` (`SqlDialectBase`) и их override'ы. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены | При заморозке внести подписи (точный текст — из анализатора); DIM — с дефолтными реализациями (ср. DU1/CNT1/SS1) |
| DB2 | P2 (контракт/принято) | `SqlFunctions.cs:546,554`; `ISqlDialect.cs:888`; `ClickHouseDialect.cs:602`; `SelectExpression.cs:95-101` | `date_diff` (`int?`) на ClickHouse/MySQL/MariaDB читается через `GetInt32`, хотя SQL (`dateDiff`/`timestampdiff`) даёт Int64/BIGINT — переполнение на широких `milliseconds`/`microseconds`; `date_diff_big` (`long?`) читается через `GetInt64` (на ClickHouse рендерит тот же `dateDiff`) | Осознанное аддитивное решение: `date_diff` остаётся 64-битным только по объявлению read-типа; широкий диапазон — через `date_diff_big` (задокументировано, `todo_timespan_columns.md` §8.3). Публичную форму не менять; не переоткрывать |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет.** (2) **Extend-only в границах изменения:** все члены аддитивны, удалений/переименований/смены подписей нет; 2 DIM source/binary-совместимы (внешний реализатор `ISqlDialect` компилируется без доработки, дефолты не бросают); новый `protected` на `SqlDialectBase` аддитивен для внешних наследников. (3) XML-doc 100 %: `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true` ⇒ CS1591 гейтится, build `0/0`. (4) Нейминг `date_diff_big`/`MakeDateDiffBig` следует конвенции `count_big`/`count_big_distinct`; `MakeDateDiffCore`/`IsSubDayField` — хелперы в духе `Make*`/`Supports*`. (5) BCL-конфликтов нет (CA1716/CA1724 чисто). (6) Шаг 5 открыт: `find -name 'PublicAPI*.txt'` — **0**; `docs/specs/**` — internal.

**Проверка (24.09.2026).** `dotnet build nextorm.slnx -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.ISqlDialect` — `MakeDateDiffBig` (`:888`) и `PromoteDateOperand` (`:897`) как DIM; `roslyn members NextORM.Core.SqlDialectBase` — `MakeDateDiffBig` (`:579`), `MakeDateDiffCore` (`:586`, protected), `IsSubDayField` (`:607`, protected static), `PromoteDateOperand` (`:611`); `roslyn members NextORM.Core.SqlFunctions.CommonFunctions` — `date_diff_big` (`:554`); `roslyn refs ISqlDialect.MakeDateDiffBig` — **4** ссылки (`BuiltinFunctionTranslator.cs:338` + 3 теста); `roslyn refs ISqlDialect.PromoteDateOperand` — **9** (прод: `BuiltinFunctionTranslator.cs:311`, `DateTimeFunctionTranslator.cs:47`; 7 тестовых); `find -name 'PublicAPI*.txt'` — **0** (DB1). Публичные доки EN+RU — `date_diff_big` присутствует (guide/11, api-reference).

---

### Аудит 24.09.2026 — трансляция `Regex.IsMatch`/`Regex.Replace` (G7/#68): `SupportsRegex`/`MakeRegexMatch`/`MakeRegexReplace`, uncommitted working tree (P0 — нет; P1 — нет; P2 — 3: RX1–RX3)

**Область.** Публичная поверхность инкремента `todo_regex.md` (tracking issue #68). **3** новых члена `ISqlDialect` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`): DIM `SupportsRegex => false` (`:766`, `.get :766:27`), DIM `MakeRegexMatch(string value, string pattern, bool ignoreCase)` (`:774`, get-дефолт бросает), DIM `MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase)` (`:782`, get-дефолт бросает). Виртуальные дефолты `SqlDialectBase` (`:451`/`:455`/`:458`). Провайдерные override'ы (не новая публичная поверхность — расширяют `public virtual`/реализуют интерфейс): `PostgresDialect.cs:410,413,417`; `MySqlDialect.cs:392,395,399`; `MariaDbDialect.cs:17,20,24`; `ClickHouseDialect.cs:495,498,502`; `SqliteDialect.cs:142,145,149`. Новых публичных типов нет: `RegexSqlTranslator` — `internal static` (`Visitors/RegexSqlTranslator.cs`), вызывается из `ScalarFunctionTranslator.cs:47-48`, `TypeFacts.cs:41-42`. Публичные доки EN+RU не обновлены. Тесты — 6 SQL-gen-файлов, in-memory (+3), интеграционный `CommonTestSuite.Functions` (3).

**Именование — P0 нет; новых P1 нет.** BCL-конфликтов нет (CA1716/CA1724 чисто): `SupportsRegex`/`MakeRegexMatch`/`MakeRegexReplace` следуют существующим `Supports*`/`Make*` (`SupportsFullText`, `MakeStringLastIndexOf`, `MakeStringAgg`); `Regex` — общепринятое BCL-имя, используется в членах, а не как имя типа (затенения нет). `MakeRegexMatch`/`MakeRegexReplace` — «Make + функция», как `MakeStringIndexOf`/`MakeStringAgg`. Параметр `bool ignoreCase` в духе `MakeOrdinal(string,bool)`; `CancellationToken` не применим (синхронные рендереры, целевой объект — value/pattern/options). XML-doc полный: `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` — 0 ⇒ CS1591 гейтится, build `0/0` = покрытие «3 DIM + 3 базы + 15 override'ов (5×3)» **100 %**.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| RX1 | P2 (трекинг Шага 5) | `PublicAPI.*.txt` — **0**; поверхность выше | Новая публичная поверхность не трекается: 3 члена `ISqlDialect` (DIM) + 3 виртуальных члена базы + 15 override'ов. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены (Шаг 5 открыт). Продолжение DB1/DU1/SS1/… | При заморозке внести подписи (точный текст — из анализатора); DIM — с дефолтными реализациями |
| RX2 | P2 (док/охват) | `docs/guide/11-scalar-functions.md` (+RU), `docs/guide/provider-specific/overview.md` (+RU), `docs/advanced/limitations.md` (+RU); `rg -ni 'IsMatch\|Regex\.Replace\|System\.Text\.RegularExpressions' docs -g '!docs/specs/**' -g '!docs/_site/**' -g '!docs/api/**'` — **0** | **Публичная проза EN+RU не обновлена.** План (`todo_regex.md` §10) прямо требовал `guide/11`, `providers/overview`, `limitations`; задокументированы только PG-специфичные `SqlFunctions.Postgres.regexp_*`. Пользователь не узнает про `Regex.IsMatch`/`Regex.Replace`, матрицу провайдеров и gate-off SQL Server | Добавить EN+RU: раздел «Regular expressions» (LINQ `Regex.IsMatch`/`Regex.Replace`, константный паттерн, `RegexOptions.IgnoreCase`, gate-off SQL Server, регистрация `regexp` на SQLite), ограничения ядер (RE2/ICU/POSIX, backreferences, `$1` vs `\1`), строки в `advanced/api-reference` и `limitations`. Маршрут `nextorm-design-engineer`; ссылки на `docs/specs/**` запрещены (AGENTS.md) |
| RX3 | P2 (док/контракт) | XML-doc `ISqlDialect.MakeRegexMatch`/`MakeRegexReplace` (`ISqlDialect.cs:766-783`); `todo_regex.md` §9.1 | Хуки описаны как «native regular-expression SQL», но не оговорено, что диалект паттерна зависит от провайдера (RE2/POSIX/ICU/CLR) и что синтаксис replacement-групп различается (`.NET $1` vs `PG/ClickHouse \1`). Публичный контракт читается как переносимый, хотя это best-effort перевод; RE2 (ClickHouse) не принимает backreferences/lookaround, а паттерн, валидный в C#, может молча дать другой результат | Дополнить `<summary>` обоих хуков и раздел public-доков оговоркой о непереносимости ядер и групп; при желании — отклонять заведомо непереносимые конструкции на ClickHouse. Публичную форму не менять |

**Резолюция RX2/RX3 (24.09.2026).** Публичная проза EN+RU добавлена: раздел «Regular expressions»/«Регулярные выражения» в `docs/guide/11-scalar-functions.md` (+RU), строка `Regex` в support-матрице `docs/providers/overview.md` (+RU), строка в `docs/advanced/limitations.md` (+RU); `<summary>` `ISqlDialect.SupportsRegex`/`MakeRegexReplace` дополнен оговоркой о непереносимости ядер и синтаксисе групп. RX1 (PublicAPI-трекинг, Шаг 5) остаётся открытым.

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет.** (2) **Extend-only:** 3 члена аддитивны, DIM source/binary-совместимы (внешний реализатор `ISqlDialect` компилируется без доработки; дефолты бросают, как `ILockRenderer.Render`); новые `public virtual` на `SqlDialectBase` аддитивны для внешних наследников; удалений/переименований/смены подписей нет. (3) XML-doc 100 % (build `0/0`). (4) SQL Server — осознанный gate-off (`SupportsRegex` не переопределён); `SupportsRegex` без контрактного теста — кодовая сторона, `code-smells-review.md`, Находка 169. (5) Шаг 5 открыт: `find -name 'PublicAPI*.txt'` — **0**; `docs/specs/**` — internal. (6) `RegexSqlTranslator` — `internal`, в поверхность не входит; портируемость ядер — кодовая сторона, `code-smells-review.md`, наблюдение A.

**Проверка (24.09.2026).** `dotnet build nextorm.slnx -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.ISqlDialect` — `SupportsRegex` (`:766`), `MakeRegexMatch` (`:774`), `MakeRegexReplace` (`:782`) как DIM; `roslyn callers NextORM.Sqlite.SQLiteFunctions.Register` — 1 (`SqliteDataContext.OnConnectionCreated:48`); `find -name 'PublicAPI*.txt'` — **0** (RX1); `rg -ni 'IsMatch|Regex\.Replace|System\.Text\.RegularExpressions' docs -g '!docs/specs/**' -g '!docs/_site/**' -g '!docs/api/**'` — **0** (RX2). Unit-тесты фильтр `~Regex`: core 3, postgres 10, mysql 4, mariadb 4, clickhouse 4, sqlite 3, sqlserver 1 — **29/29 passed**; интеграция `~Regex` — 12 passed / 3 skipped (сбой SQL Server-контейнера).

---

### Аудит 24.09.2026 — колонковая коллация (`#28`): `CollationAttribute`, DIM `IPropertyMetadata.Collation`, `EntityPropertyBuilder<T>.Collation`, uncommitted working tree (P0 — нет; P1 — нет; P2 — 1: CN1; CN2 закрыто)

**Область (uncommitted).** Новый публичный `NextORM.Core.CollationAttribute` (`src/nextorm.core/CollationAttribute.cs:18`): `sealed`, `[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]` (`:17`), `.ctor(string name)` (`:24`), `string Name { get; set; }` (`:33`, backing field `:33:19`). Новый DIM `IPropertyMetadata.Collation => null` (`src/nextorm.core/DataContext/Meta/IPropertyMetadata.cs:85`, nullable `string?`), реализация — internal `PropertyMetadata.Collation` (`…/Implementation/PropertyMetadata.cs:15`). Новый fluent-член `EntityPropertyBuilder<T>.Collation(string collation)` (`DataContext/Meta/EntityPropertyBuilder.cs:97`), значение пишется в `Build()` (`:113`). `EntityMetadataBuilder.AutoBuildProperties` читает `[Collation]` (в т.ч. с интерфейсного свойства, `DataContext/Meta/EntityMetadataBuilder.cs:65,78`). Internal (не поверхность): `MemberTranslator.ResolveCollation`/`AppendColumn` (`Visitors/MemberTranslator.cs:110,130`), `BaseExpressionVisitor.SuppressColumnCollation` (`:77`), `VisitToStringSuppressingColumnCollation` (`:391`). Хуки `ISqlDialect.SupportsCollation`/`MakeCollate` — пре-существующие (G12/SS), здесь не переоцениваются.

**Именование — P0 нет; новых P1 нет.** BCL-конфликтов нет (CA1716/CA1724 чисто): `Collation` — не ключевое слово, BCL-типа `CollationAttribute` нет; атрибут несёт обязательный суффикс `Attribute` и `sealed`; `Name` — в духе `DurationAttribute.Unit`/`Precision` (для named-args); fluent `Collation(string)` — глагол-настройка, как `Duration`/`Column`/`Key`, имя параметра `collation` совпадает с `IPropertyMetadata.Collation`. DIM `IPropertyMetadata.Collation => null` — extend-only (внешний реализатор компилируется без доработки). XML-doc полный: `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`) + `TreatWarningsAsErrors=true` (`Directory.Build.props:40`), `NoWarn` — **0** ⇒ CS1591 гейтится, build `0/0` = покрытие «`CollationAttribute` (тип + ctor + `Name.get/set`) + DIM `Collation` + fluent `Collation`» **100 %**.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| CN1 | P2 (трекинг Шага 5) | `PublicAPI.*.txt` — **0**; поверхность выше | Новая публичная поверхность не трекается: `CollationAttribute` (тип + ctor + `Name.get/set`), DIM `IPropertyMetadata.Collation`, `EntityPropertyBuilder<T>.Collation`. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены (Шаг 5 открыт). Продолжение RX1/DB1/DU1/SS1/… | При заморозке внести подписи (точный текст — из анализатора); DIM — с дефолтом `null` |
| CN2 | P2 (док/охват) | `docs/advanced/limitations.md:43` (+RU, строка «Коллация на уровне столбца (`#28`)»); `rg -n 'CollationAttribute\|IPropertyMetadata\.Collation\|\.Collation\(' docs -g '!docs/specs/**' -g '!docs/_site/**' -g '!docs/api/**'` — **0** | **Публичная проза EN+RU не обновлена и противоречит фиче.** `limitations` прямо утверждает, что «объявление коллации на свойстве сущности/в fluent-метаданных — отдельный пункт бэклога», хотя оно реализовано; `CollationAttribute`/`EntityPropertyBuilder<T>.Collation`/`IPropertyMetadata.Collation` не упомянуты ни в `docs/guide/11-scalar-functions.md` (+RU), ни в `docs/advanced/api-reference.md` (+RU). План (`todo_column_collation.md` §10) прямо требовал `guide/11`, `limitations`, `api-reference` (EN+RU) | Дополнить EN+RU: заменить строку `limitations` на «декларация существующей колонки, DDL не генерируется» + поведение ClickHouse (`NotSupportedException`) и in-memory (игнорирует, ordinal); раздел в `guide/11` (или отдельная страница) про `[Collation]`/`.Collation(...)`; строки `CollationAttribute`/`IPropertyMetadata.Collation`/`Collation` в `advanced/api-reference`. Маршрут `nextorm-design-engineer`; ссылки на `docs/specs/**` запрещены (AGENTS.md) |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 нет.** (2) **Extend-only:** члены аддитивны, удалений/переименований/смены подписей нет; DIM `IPropertyMetadata.Collation` source/binary-совместим; `CollationAttribute` — новый тип. (3) XML-doc 100 % (build `0/0`). (4) BCL-конфликтов нет; `CollationAttribute` — `sealed` с обязательным суффиксом; `Name` изменяем (named-args) — допустимо, как `DurationAttribute.Unit`. (5) Нейминг колонковой коллации симметричен expression-коллации (`SqlFunctions.Sql.collate` → `SupportsCollation`/`MakeCollate`), поверхность метаданных — `Collation`. (6) Шаг 5 открыт: `find -name 'PublicAPI*.txt'` — **0**; `docs/specs/**` — internal. (7) Кодовая сторона (неперенос `SuppressColumnCollation` через `Clone()`) — `code-smells-review.md`, Находка 170; замечания к `ResolveCollation`/fail-fast — там же, наблюдения A/C.

**Проверка (24.09.2026).** `dotnet build nextorm.slnx -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.CollationAttribute` — ctor `CollationAttribute(string)` (`:24`) + `Name.get/set` (`:33`); `roslyn members NextORM.Core.EntityPropertyBuilder<T>` — `Collation(string)` (`:97`); `find -name 'PublicAPI*.txt'` — **0** (CN1). XML-doc гейтится build `0/0`.

**Итог по пунктам.** CN1 — **открыт** (Шаг 5, `PublicAPI*.txt` нет; подписи `CollationAttribute`/`IPropertyMetadata.Collation`/`EntityPropertyBuilder<T>.Collation` внести при заморозке). CN2 — ✅ **закрыто (24.09.2026).** Публичная проза EN+RU обновлена: `docs/guide/11-scalar-functions.md` (+RU) — раздел «Column collation»/«Коллация на уровне столбца» + строка в таблице строковых функций и сопоставления провайдеров; `docs/advanced/limitations.md` (+RU) — строка про ClickHouse/DDL вместо «отдельного пункта бэклога»; `docs/advanced/api-reference.md` (+RU) — `CollationAttribute`, `IPropertyMetadata`, `EntityPropertyBuilder<T>`; `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 (п. 34) и `linq2db-backlog-gap-analysis.md` (G12) — follow-up `#28` отмечен сделанным; работа-план `todo_column_collation.md` удалён. Ссылок на `docs/specs/**` нет.

---

### Аудит 24.09.2026 — интерцепторы Фазы 1 (`IQueryInterceptor`/`IConnectionInterceptor`, `CommandEventData`/`ConnectionEventData`, `AddInterceptor`), uncommitted working tree (P0 — нет; P1 — 1 доковый: INT2; P2 — 3: INT1, INT3, INT4)

**Область (uncommitted).** Новые публичные типы `NextORM.Core.IQueryInterceptor` (`src/nextorm.core/Interceptors/IQueryInterceptor.cs:25`; 4 DIM: `CommandInitialized` `:32`, `CommandExecuting` `:37`, `CommandExecuted` `:43`, `CommandFailed` `:49`), `NextORM.Core.CommandEventData` (`:11`), `NextORM.Core.IConnectionInterceptor` (`Interceptors/IConnectionInterceptor.cs:22`; 2 DIM: `ConnectionOpening` `:26`, `ConnectionOpened` `:30`), `NextORM.Core.ConnectionEventData` (`:11`). Новые публичные члены: `DataContextBuilder.AddInterceptor(IQueryInterceptor)` (`DI/DataContextBuilder.cs:151`), `.AddInterceptor(IConnectionInterceptor)` (`:166`); `DataContext.AddInterceptor(IQueryInterceptor)` (`DataContext/DataContext.cs:170`), `.AddInterceptor(IConnectionInterceptor)` (`:182`). Internal (в поверхность не входят): `InterceptorHooks`, `QueryExecutor`/`QueryPlanner`/`DbConnectionManager`/`ResultSetEnumerator`-проводка.

**P0 — нет.** Имена — PascalCase, без аббревиатур; BCL-конфликтов нет (`CA1716`/`CA1724`): `IQueryInterceptor`, `IConnectionInterceptor`, `CommandEventData`, `ConnectionEventData`, `AddInterceptor` в BCL отсутствуют. DIM на обоих интерфейсах — source/binary-совместимый extend-only: внешний реализатор компилируется без доработки, новые события добавляются без binary-breaking.

**P1 — 1 доковый: INT2.** Остальное — P2.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| INT1 | P2 (нэйминг/форма) | `IQueryInterceptor.cs:11,25`; `IConnectionInterceptor.cs:11,22` | Два отклонения до заморозки. **(1)** Суффикс `EventData` вместо конвенционного для event-args `EventArgs` (api-design) — допустимо, т.к. это payload интерфейсных методов, а не `EventHandler`, но решение не зафиксировано. **(2)** Асимметрия payload: `ConnectionEventData` несёт сам `DbConnection` (`:11`), а `CommandEventData` несёт только `(DataContext, Sql)`, а `DbCommand` передаётся **отдельным параметром** метода (`:32-49`) — два разных соглашения у sibling-интерфейсов. **(3)** `IQueryInterceptor` шире «query»: те же события приходят на INSERT/UPDATE/DELETE/TRUNCATE/CTAS (см. INT2), т.е. имя описывает не весь наблюдаемый lifecycle (linq2db-аналог — `ICommandInterceptor`). | До заморозки решить и задокументировать: (а) `CommandEventArgs`/`ConnectionEventArgs` либо оставить `*EventData` с оговоркой в `<remarks>`; (б) выровнять payload (`DbCommand` — либо параметр обоих, либо поле обоих); (в) подтвердить `IQueryInterceptor` или переименовать в `ICommandInterceptor` (пока `PublicAPI.*` не заморожен — бесплатно). Открытый вопрос #3 `todo_interceptors.md:199` просил именно эту запись |
| INT2 | P1 (док/контракт) | `IQueryInterceptor.cs:27-32`; реализация `QueryExecutor.cs:274` (`CreateMutationCommand` `:260`), `QueryPlanner.cs:428` | **Публичный XML-doc неверен для мутаций.** `CommandInitialized` задокументирован как «Called **once**, when the command for a **query plan** has been created and its parameters bound». Фактически: для плановой команды — действительно один раз (только ветка cache-miss, `QueryPlanner.cs:428`); для **мутаций** `QueryExecutor.CreateMutationCommand` поднимает `CommandInitialized` на **каждое** исполнение (команда не переиспользуется, кэша нет). Сверх того `<remarks>` интерфейса (`:19-23`) говорит, что события поднимает «execution axis», тогда как `CommandInitialized` для планов поднимает planning axis. Пользователь, повесивший на `CommandInitialized` provider-опцию «на план», получает иной частотный контракт на DML. | Скорректировать `<summary>`/`<remarks>` `IQueryInterceptor`: развести «plan init — один раз на построение плана» и «mutation init — на каждое исполнение»; уточнить, что `CommandInitialized` поднимают и planning-, и execution-axis. Кодовая сторона (дублирование поднятия) — `code-smells-review.md`, Находка 171. Публичную форму менять не обязательно |
| INT3 | P2 (док/охват) | `docs/guide/**`, `docs/ru/guide/**`, `docs/advanced/api-reference.md` (+RU); `rg -l "Interceptor\|интерцеп" docs -g '!docs/specs/**'` — **0** | **Публичная проза EN+RU не добавлена.** План (`todo_interceptors.md:209-211`) требовал новый гайд `docs/guide/19-interceptors.md` (+RU), строки в `advanced/api-reference` (+RU) и `limitations` (+RU). Ни `IQueryInterceptor`/`IConnectionInterceptor`, ни `AddInterceptor`, ни `CommandEventData`/`ConnectionEventData` не упомянуты ни в одном публичном документе (EN или RU). Пользователь не узнает о фиче. | Добавить EN+RU: страницу «Interceptors» (регистрация builder vs per-instance, порядок вызова, `CommandInitialized` vs `CommandExecuting`, запрет менять `CommandText` кэшированного плана, требование потокобезопасности, connection-события) + строки в `advanced/api-reference.md` и `limitations`. Маршрут `nextorm-design-engineer`; ссылки на `docs/specs/**` в публичных доках запрещены (AGENTS.md) |
| INT4 | P2 (трекинг Шага 5) | `PublicAPI.*.txt` — **0**; `find -name 'PublicAPI*.txt'` | Новая публичная поверхность не трекается: `IQueryInterceptor` (4 DIM), `IConnectionInterceptor` (2 DIM), `CommandEventData`/`ConnectionEventData` (ctor ×1 + `DataContext.get/init`, `Sql.get/init` / `Connection.get/init`, `Deconstruct`, record-члены), `DataContextBuilder.AddInterceptor` ×2, `DataContext.AddInterceptor` ×2. `PublicApiAnalyzers`/ApiCompat/API-approval не подключены (Шаг 5 открыт). Продолжение RX1/CN1/DU1/DB1/SS1/… | При заморозке внести подписи (точный текст — из анализатора); DIM — с пустыми телами |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1, кроме докового INT2, нет.** (2) **Extend-only:** все члены аддитивны, удалений/переименований/смены подписей нет; DIM обоих интерфейсов source/binary-совместимы (внешний реализатор компилируется без доработки); `CommandEventData`/`ConnectionEventData` — новые типы. Отсутствие `abstract`-членов в интерфейсах выбрано осознанно (RFC `todo_interceptors.md:110-111`). (3) **XML-doc 100 %:** `GenerateDocumentationFile=true` (`src/Directory.Build.props:11`) + `TreatWarningsAsErrors=true` (`Directory.Build.props:40`), `NoWarn` — **0** ⇒ CS1591 гейтится, build `0/0` = покрытие «2 интерфейса + 2 record struct + 4 `AddInterceptor`» **100 %** (качество доки — INT2). (4) **NRT/порядок параметров чисты:** `string? Sql` — честная аннотация («not known»), `Exception exception` и `TimeSpan elapsed` — последними, `CancellationToken` в публичную сигнатуру фичи не входит, синхронных близнецов у событий нет (`Async`-суффикс не нужен), `record struct` — `readonly`, конверта в BCL-типы нет. (5) **Перегрузки `AddInterceptor` однозначны** — разные типы параметров, `CS0121` невозможен; имя `AddInterceptor` симметрично `Add*`-конвенции builder'ов. (6) Шаг 5 открыт: `docs/specs/**` — internal. (7) Кодовая сторона (DRY-дублирование, аллокация async-обёрток, мёртвый `InterceptorHooks.Empty`) — `code-smells-review.md`, Находки 171–173.

**Проверка (24.09.2026).** `dotnet build nextorm.slnx -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.IQueryInterceptor` — 4 DIM (`:32,37,43,49`), `NextORM.Core.IConnectionInterceptor` — 2 DIM (`:26,30`), `NextORM.Core.CommandEventData`/`ConnectionEventData` — ctor + позиционные свойства (`:11`); `roslyn refs NextORM.Core.InterceptorHooks.Empty` — 0; `find -name 'PublicAPI*.txt'` — **0** (INT4); `rg -l "Interceptor|интерцеп" docs -g '!docs/specs/**'` — **0** (INT3); подавления `src/` — **11/11**, новых 0; тесты core **3/3**, sqlite **9/9**. XML-doc гейтится build `0/0`.

---

### Аудит 24.09.2026 — follow-up колонок-длительностей: DIM `ISqlDialect.MakeNullableDurationType` (коммит `18ed028`) и фикс `make_interval`, uncommitted working tree (P0 — нет; P1 — 1 поведенческий/доковый: MI1; P2 — 3: ND1, ND2, MI2)

**Область.** **Аддитивный** член `ISqlDialect.MakeNullableDurationType(DurationUnit? unit, int precision = 0)` — DIM (`DataContext/Dialect/ISqlDialect.cs:338`, дефолт `=> MakeDurationType(unit, precision)`), `public virtual` `SqlDialectBase.cs:110`, override `ClickHouseDialect.cs:671` (`$"Nullable({MakeDurationType(unit, precision)})"`). Публичные доки EN+RU обновлены: `docs/guide/26-duration-columns.md:73` (+RU) — xref на `MakeDurationType`/`MakeNullableDurationType` и `Nullable(Int64)`. Фикс `make_interval` (`Visitors/ExtendedScalarFunctionTranslator.cs`) публичной поверхности **не добавляет** (`PostgresFunctions.make_interval` сигнатура без изменений).

**P0 — нет; новых P1 — 1 (MI1).** Нейминг: `MakeNullableDurationType` следует `MakeDurationType`/`Make*`-конвенции `ISqlDialect`, BCL-конфликтов нет (`CA1716`/`CA1724` чисто: не затеняет `System.Nullable`; `Nullable(...)` — строковый рендер, публичного типа нет). **XML-doc 100 %:** `<summary>` + `<param>`×2 + `<returns>` на DIM, `<inheritdoc/>` на базе/override; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, `NoWarn` — 0 ⇒ CS1591 гейтится, build `0/0`.

| # | Ур. | Место | Проблема | Рекомендация |
|---|-----|-------|----------|--------------|
| ND1 | P2 (трекинг Шага 5) | `PublicAPI.*.txt` — **0**; `ISqlDialect.cs:338`; `SqlDialectBase.cs:110` | Новый публичный член (DIM + `virtual`) не трекается. Продолжение DU1/DB1/SS1/INT4/… | При заморозке внести подпись DIM с дефолтом (точный текст — из анализатора) |
| ND2 | P2 (мёртвая публичная поверхность/контракт) | `ISqlDialect.cs:338`; `roslyn refs` — 7, **все** тесты | `MakeNullableDurationType` **не вызывается продакшеном** (0): тип колонки по-прежнему даёт `MakeTypeName`; цель члена (nullable ClickHouse DDL) проверена только вручную собранным DDL теста (`CommonTestSuite.Duration.cs:58`). Расширяет DU2/кодовую Находку 164 (не переоткрывается) | Либо подключить hook в продакшен-рендер типа колонки (передавая `DurationUnit`/`Precision` из метаданных) и добавить DDL-тест, либо до lock перевести в internal/убрать. Публичную форму без решения не менять |
| MI1 | **P1 (поведенческое изменение/BC + док)** | `ExtendedScalarFunctionTranslator.cs:102-107,170-201`; `SqlGenerationTests.cs:2867` | **Да — фикс `make_interval` нужно зафиксировать как поведенческое (не сигнатурное) ломающее изменение.** До фикса `make_interval(y,mo,d,h,mi,s)` рендерился как есть, и PG читал его как `(years, months, weeks, days, hours, mins)`: аргументы после `months` сдвинуты, секунды терялись. Source/binary-совместимо (сигнатура C# не менялась), но сгенерированный SQL существующих запросов меняется. Pre-1.0/поверхность не заморожена ⇒ не P0, но **обязательно в changelog/release notes (как Minor, не Patch)** | В release notes: «`make_interval` rendered SQL corrected — arguments after `months` were shifted and seconds dropped; re-verify queries using it». Смены подписи нет |
| MI2 | P2 (публичный контракт/док) | `SqlFunctions.Postgres.cs:440`; `PostgresFunctionsTests.cs:464-465`; `todo_timespan_columns.md` §8.7 | `current_time()` объявлен `TimeSpan?`, но PG возвращает `time with time zone` (`timetz`), который Npgsql не читает как `TimeSpan` (`InvalidCastException`); новый тест сознательно исключает `current_time()` и берёт `localtime()`. Публичный XML-doc дефект не отражает | До заморозки: сменить return type (`DateTimeOffset?`/`string?`) или задокументировать ограничение + `[Obsolete]`; тест-исключение уже зафиксировано в §8.7 |

ℹ️ **Наблюдения.** (1) **P0 нет; новых P1 — 1 (MI1, поведенческий/доковый).** (2) **Extend-only:** добавление DIM + `virtual` не ломает внешних реализаторов `ISqlDialect`/наследников `SqlDialectBase` (дефолт не бросает); удалений/переименований/смены подписей нет. (3) **XML-doc 100 %** (build `0/0`). (4) **Доки EN+RU** обновлены (`guide/26-duration-columns.md:73` +RU, xref). (5) **Шаг 5 открыт:** `find -name 'PublicAPI*.txt'` — **0**. (6) `current_time` — известный дефект, зафиксирован в `todo_timespan_columns.md` §8.7; реестр его только трекает (MI2), не переоткрывает. (7) Кодовая сторона — `code-smells-review.md`, Находка 174 + наблюдение A.

**Проверка (24.09.2026).** `dotnet build nextorm.slnx -c Release` — **0 warnings / 0 errors**; `roslyn members NextORM.Core.ISqlDialect` — `MakeNullableDurationType` (`:338:12`), `SqlDialectBase` (`:110:27`), `ClickHouseDialect` (`:671:28`); `roslyn refs ISqlDialect.MakeNullableDurationType` — **7**, все в тестах (ND2); `grep -rn "MakeNullableDurationType" docs/ -g '!docs/specs/**'` — `guide/26-duration-columns.md` EN+RU; `find -name 'PublicAPI*.txt'` — **0** (ND1); PG SQL-gen `MakeIntervalAndJustify` — **1/1**. Источник `MakeNullableDurationType` — коммит `18ed028`; фикс `make_interval` — uncommitted.

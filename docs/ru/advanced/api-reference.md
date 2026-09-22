# Справочник по API

> Курируемый указатель публичных типов nextorm, сгруппированных по пространству имён; каждый тип ссылается на свой сгенерированный справочник по API.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Provider overview](../providers/overview.md)

## Обзор

Это **курируемый** указатель; справочник, сгенерированный из комментариев XML doc на исходных типах,
опубликован в разделе **API reference** этого сайта (см. верхнюю навигацию). Каждая запись ниже даёт тип,
однострочное описание и — через само имя типа — ссылку на его сгенерированную страницу справочника по API.

Типы перечислены под своим определяющим пространством имён. Весь API запросов находится в [`NextORM.Core`](xref:NextORM.Core); каждый пакет провайдера
добавляет контекст, диалект и класс расширения [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) в своём собственном пространстве имён.

## Пространство имён [`NextORM.Core`](xref:NextORM.Core)

### Контекст и роли

| Тип | Описание |
|---|---|
| [`IDataContext`](xref:NextORM.Core.IDataContext) | Составной фасад над ролями контекста; точка входа, от которой обычно зависят потребители. |
| [`IQueryExecutor`](xref:NextORM.Core.IQueryExecutor) | Выполняет подготовленную команду и материализует её результат (терминалы). |
| [`IQueryMaterializer`](xref:NextORM.Core.IQueryMaterializer) | Самый узкий контракт для планирования + чтения строк, без терминалов. |
| [`IQueryPlanner`](xref:NextORM.Core.IQueryPlanner) | Строит/сбрасывает планы выполнения и разрешает источники [`From`](xref:NextORM.Core.DataContextExtensions). |
| [`IRowReaderFactory`](xref:NextORM.Core.IRowReaderFactory) | Создаёт читатели строк/перечислители над подготовленной командой. |
| [`IQueryCache`](xref:NextORM.Core.IQueryCache) | Хранит кэшированные планы и общий план [`Any`](xref:NextORM.Core.EntityBuilder`1). |
| [`IContextEnvironment`](xref:NextORM.Core.IContextEnvironment) | Окружающее состояние: логгеры, режим отображения, набор свойств. |
| [`IConnectionManager`](xref:NextORM.Core.IConnectionManager) | Владеет жизненным циклом соединения (не является частью [`IDataContext`](xref:NextORM.Core.IDataContext); провайдер in-memory его не реализует). |
| [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) | Встроенный in-memory [`IDataContext`](xref:NextORM.Core.IDataContext) по коллекциям CLR. |

### Построители запросов

| Тип | Описание |
|---|---|
| [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) | Fluent, неизменяемый построитель запросов для отображённого типа сущности. |
| [`EntityBuilderExtensions`](xref:NextORM.Core.EntityBuilderExtensions) | Терминальные операторы [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) ([`Any`](xref:NextORM.Core.EntityBuilder`1)/[`ToList`](xref:NextORM.Core.EntityBuilder`1)/[`First`](xref:NextORM.Core.EntityBuilder`1)/[`Single`](xref:NextORM.Core.EntityBuilder`1)/[`Last`](xref:NextORM.Core.EntityBuilder`1)/[`Count`](xref:NextORM.Core.EntityBuilder`1)/агрегаты/`To*`/[`Prepare`](xref:NextORM.Core.EntityBuilder`1)) в виде extension-методов. |
| [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) | Fluent-построитель для источника псевдонима/таблицы без типа сущности (режим [`TableAlias`](xref:NextORM.Core.TableAlias)). |
| [`JoinedEntityBuilder<T1,T2>`](xref:NextORM.Core.JoinedEntityBuilder`2) … [`JoinedEntityBuilder<T1..T8>`](xref:NextORM.Core.JoinedEntityBuilder`8) | Накопительные построители соединений; арность от 2 до 8. |
| [`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) | Fluent-конфигурация метаданных сущности, используемая `IDataContext.From<T>(...)`. |
| [`INamingConvention`](xref:NextORM.Core.INamingConvention) | Транслирует автоматически построенные имена таблиц/столбцов (см. `DataContextBuilder.UseNamingConvention`); встроенный [`SnakeCaseNamingConvention`](xref:NextORM.Core.SnakeCaseNamingConvention) отображает `SimpleEntity` на `simple_entity`, а `FirstName` на `first_name`. |
| [`Projection<T1,T2>`](xref:NextORM.Core.Projection`2) … [`Projection<T1..T8>`](xref:NextORM.Core.Projection`8) | Форма результата соединённого запроса; предоставляет `Item1`…`` |
| [`IProjection`](xref:NextORM.Core.IProjection) / [`IExtendableProjection`](xref:NextORM.Core.IExtendableProjection) | Маркеры для накопленных проекций соединений (арность 8 не расширяема). |
| [`CteQuery`](xref:NextORM.Core.CteQuery) | Fluent-область, собирающая объявления `WITH`. |
| [`CteDefinition`](xref:NextORM.Core.CteDefinition) | Один CTE: имя, определяющий запрос, флаг рекурсии и необязательная максимальная рекурсия. |
| [`TableAlias`](xref:NextORM.Core.TableAlias) / [`TableColumn`](xref:NextORM.Core.TableColumn) | Аксессоры столбцов в режиме псевдонима ([`GetInt32`](xref:NextORM.Core.TableAlias), [`GetString`](xref:NextORM.Core.TableAlias), …) и типизированная обёртка столбца ([`AsInt`](xref:NextORM.Core.TableColumn.AsInt), [`AsString`](xref:NextORM.Core.TableColumn.AsString), …). |
| [`Paging`](xref:NextORM.Core.Paging) | Значение [`Limit`](xref:NextORM.Core.Paging.Limit) / [`Offset`](xref:NextORM.Core.Paging.Offset) / [`HasWithTies`](xref:NextORM.Core.Paging.HasWithTies), используемое каждым построителем запросов. |

### Команды, планы и функции

| Тип | Описание |
|---|---|
| [`QueryCommand`](xref:NextORM.Core.QueryCommand) | Необобщённая команда запроса, хранящая план/состояние, общие для всех результатов. |
| [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) | Типизированная команда запроса с терминалами ([`ToList`](xref:NextORM.Core.EntityBuilder`1), [`First`](xref:NextORM.Core.EntityBuilder`1), [`Union`](xref:NextORM.Core.QueryCommand`1), [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct), [`Hint`](xref:NextORM.Core.QueryCommand`1), [`ForJson`](xref:NextORM.Core.QueryCommand`1), [`ForXml`](xref:NextORM.Core.QueryCommand`1), [`WithTableHint`](xref:NextORM.Core.EntityBuilder`1), [`Prepare`](xref:NextORM.Core.EntityBuilder`1), …). |
| [`QueryDefinition`](xref:NextORM.Core.QueryDefinition) | Неизменяемая форма запроса (проекция/тип-источник, условие, соединения, пагинация, сортировка, группировка, логгер), принимаемая конструкторами команд и [`CreateCommand`](xref:NextORM.Core.DataContextExtensions). |
| [`PrepareFromSqlMode`](xref:NextORM.Core.PrepareFromSqlMode) | Флаги для [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1): `None` (буферизованное/скалярное), [`Streaming`](xref:NextORM.Core.PrepareFromSqlMode.Streaming), `` |
| [`PreparedCommandOptions`](xref:NextORM.Core.PreparedCommandOptions) | Необобщённая настройка [`DbPreparedQueryCommand<TResult>`](xref:NextORM.Core.DbPreparedQueryCommand`1) (одна строка, сырой SQL, отсутствие параметров, обновление параметров). |
| [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) | Подготовленная команда; её члены по умолчанию выполняют её против переданного [`IDataContext`](xref:NextORM.Core.IDataContext). |
| [`SqlFunctions`](xref:NextORM.Core.SqlFunctions) | Статическая точка входа: [`Sql`](xref:NextORM.Core.SqlFunctions.Sql), [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres), [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer), [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) и [`Parameter`](xref:NextORM.Core.SqlFunctions). |
| [`CommonFunctions`](xref:NextORM.Core.CommonFunctions) | Поверхность SQL-функций: `exists`, `like`, `@in`, `any`/`all` (подзапрос и массив), условная `iif` ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif)/[`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render)), агрегаты (включая агрегаты с `FILTER`, `string_agg`/`array_agg` и агрегат произвольного значения `any_agg` на MySQL/ClickHouse), оконные функции (включая `percent_rank`/`cume_dist` и `nth_value`, последняя под флагом [`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue), а также оконные квантили `percentile_cont`/`percentile_disc` на SQL Server/MariaDB), `nullif`/`greatest`/`least`/`date_trunc`/`date_add`/`date_diff`/`date_from_parts`/`end_of_month`/`extract`/`date_part`, session/info-функции (`current_user`/`session_user`/`current_schema`/`current_database`/`version`), генераторы UUID (`gen_random_uuid`/`uuidv7`), функции массивов и JSON/JSONB PostgreSQL, текстовые JSON-функции SQL Server и MySQL/MariaDB (`json_value`/`json_query`/`json_modify`/`isjson`) и предикаты полнотекстового поиска (`contains`/`freetext`), а также табличные функции `generate_series`/`unnest`/`string_split`/`openjson`. |
| [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) | Текстовая JSON-поверхность (`json_value`/`json_query`/`json_modify`/`isjson`) для SQL Server и MySQL/MariaDB, условная функция SQL Server `choose` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)), постфиксные методы типа XML `xml_value`/`xml_query`/`xml_exist` и rowset `xml_nodes` ([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions); форма строки [`IXmlNodesRow`](xref:NextORM.Core.SqlFunctions.IXmlNodesRow)), а также табличные функции SQL Server `string_split`/`openjson` и полнотекстовые ранжирующие табличные функции `containstable`/`freetexttable` (форма строки [`IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1)). |
| [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) | Поверхность только для PostgreSQL: массивы (`any`/`all`, `cardinality`, `array_*`, `array_shuffle`/`array_sample`, `string_to_array`), нативные JSON/JSONB, расширенная библиотека скалярных функций (`asin`, `split_part`, `lpad`, `regexp_*`, `to_char`, `setseed`, `pg_typeof`, …), крипто-хеши `md5`/`digest` (pgcrypto)/`sha256`, PG-only агрегаты (`bool_*`, `bit_*`, `regr_*`, `percentile_*`, `mode`, `array_agg`), наборные табличные функции (`generate_series`/`unnest`, `regexp_matches`/`regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`, `jsonb_object_keys`, `jsonb_path_query`, `ts_stat`) и native-поверхность текстового поиска (`to_tsvector`/`to_tsquery`/`ts_rank`/`ts_rank_cd`/`ts_headline`/`@@`). |
| [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) | Поверхность только для ClickHouse: агрегаты `arg_min`/`arg_max`, `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12`, параметрические `quantile`/`quantile_exact`/`quantile_timing`/`quantiles`/`median`, агрегаты наиболее частых значений `top_k`/`top_k_weighted`, агрегат выбора строки `any_last`, агрегаты последовательностей/воронки `window_funnel`/`sequence_match`/`retention`, учитывающие фрейм оконные `lag_in_frame`/`lead_in_frame`, многоветвевный условный `multi_if` (собирается через `when`/`otherwise`), семейство строкового JSON `json_extract_string`/`json_extract_int`/`json_extract_float`/`json_extract_bool`/`json_extract_raw`/`json_has`/`json_length`/`json_type` `visit_param_extract_string`/`_int`/`_float`/`_bool`/`_raw` и JSONPath-функции `json_value`/`json_query`/`json_exists`, возвращающие массивы `json_extract_keys`/`json_extract_array_raw` (`string[]`) и `json_extract_keys_and_values<T>` (`Tuple<string, T>[]`), функции словарей `dict_get`/`dict_get_or_default`/`dict_has`, комбинатор `-If` (`count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`), возвращающие массивы агрегаты `group_array`/`group_uniq_array` (`groupArray`/`groupUniqArray`), распределённый предикат `global_in`, табличные функции `numbers`/`numbers_mt` и `zeros`/`zeros_mt`, серверные/кластерные табличные функции `url`/`s3`/`file`/`remote`/`remote_secure`/`cluster`/`cluster_all_replicas` (generic-интерфейс строки `TRow`), а также функции массивов над колонками/выражениями `Array(T)` (`array_join`, `length`, `has`, `index_of`, `has_any`, `has_all`, `starts_with`, `ends_with`, `has_substr`, `array_string_concat`, `split_by_char`, `array_sort`, `array_reverse`, `array_distinct`, `range`, `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`), а также higher-order (lambda) функции массивов (`array_map`, `array_filter`, `array_exists`, `array_all`, `array_count`, `array_first`/`array_first_index`, `array_last`/`array_last_index`). Поверхность кортежей отображает CLR `Tuple.Create`/`System.Tuple<...>.ItemN` на `tuple(...)`/`tupleElement(t, n)` под [`SupportsTupleFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTupleFunctions). |
| [`WindowFunction<T>`](xref:NextORM.Core.WindowFunction`1) | Незавершённый вызов окна; завершите его с помощью `Over(...)`. `Over(string)` ссылается на именованное окно, объявленное на запросе. |
| [`WindowOrder`](xref:NextORM.Core.WindowOrder) | Упорядоченный ключ окна плюс [`OrderDirection`](xref:NextORM.Core.OrderDirection). |
| [`WindowDefinition`](xref:NextORM.Core.WindowDefinition), [`NamedWindowOrderKey`](xref:NextORM.Core.NamedWindowOrderKey) | Именованное окно (`WINDOW w AS (...)`), объявленное через `EntityBuilder.Window`, и его упорядоченные ключи. |
| [`WindowFrame`](xref:NextORM.Core.WindowFrame), [`WindowFrameBound`](xref:NextORM.Core.WindowFrameBound), [`WindowFrameType`](xref:NextORM.Core.WindowFrameType), [`WindowFrameBoundKind`](xref:NextORM.Core.WindowFrameBoundKind), [`WindowFrameExclusion`](xref:NextORM.Core.WindowFrameExclusion) | Спецификация фрейма `ROWS`/`RANGE`/`GROUPS`, его границы и варианты `EXCLUDE`. |
| [`PivotAggregate`](xref:NextORM.Core.PivotAggregate), [`PivotValue`](xref:NextORM.Core.PivotValue), [`UnpivotColumn`](xref:NextORM.Core.UnpivotColumn), [`PivotExpression`](xref:NextORM.Core.PivotExpression) | Нативная конструкция источника SQL Server `PIVOT`/`UNPIVOT`: `EntityBuilder.Pivot`/`Unpivot` разворачивают простую таблицу или производный запрос в именованные колонки ([`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot)). |
| [`IIifRenderer`](xref:NextORM.Core.IIifRenderer), [`ISessionInfoFunctions`](xref:NextORM.Core.ISessionInfoFunctions), [`IUuidGenerators`](xref:NextORM.Core.IUuidGenerators), [`ILimitByRenderer`](xref:NextORM.Core.ILimitByRenderer) | Capability-объекты диалекта, доступные через [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) (`Iif`, `SessionInfoFunctions`, `UuidGenerators`, `LimitBy`). Наличие объекта и есть поддержка, поэтому поддержка и рендеринг исходят из одного факта и не могут разойтись; устаревшие пары `Supports*`/`Make*` удалены в пользу объектов. |
| [`IXmlFunctions`](xref:NextORM.Core.IXmlFunctions), [`ISequenceAggregateRenderer`](xref:NextORM.Core.ISequenceAggregateRenderer), [`IUniqAggregateRenderer`](xref:NextORM.Core.IUniqAggregateRenderer), [`IQuantileAggregateRenderer`](xref:NextORM.Core.IQuantileAggregateRenderer), [`ITopKAggregateRenderer`](xref:NextORM.Core.ITopKAggregateRenderer), [`IMultiIfRenderer`](xref:NextORM.Core.IMultiIfRenderer), [`IDistinctOnRenderer`](xref:NextORM.Core.IDistinctOnRenderer), [`ITableSampleMethods`](xref:NextORM.Core.ITableSampleMethods), [`IPivotRenderer`](xref:NextORM.Core.IPivotRenderer), [`IArrayJoinRenderer`](xref:NextORM.Core.IArrayJoinRenderer), [`IDateConversionRenderer`](xref:NextORM.Core.IDateConversionRenderer), [`IStringSplitRenderer`](xref:NextORM.Core.IStringSplitRenderer), [`ILockRenderer`](xref:NextORM.Core.ILockRenderer) | Остальные capability-объекты диалекта, доступные через [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) (`XmlFunctions`, `SequenceAggregates`, `UniqAggregates`, `QuantileAggregates`, `TopKAggregates`, `MultiIf`, `DistinctOn`, `TableSample`, `Pivot`, `ArrayJoinClause`, `DateConversion`, `StringSplit`, `Lock`). Каждый nullable-объект — единый источник поддержки и рендерера, поэтому возможности XML/sequence/uniq/quantile/topK/multiIf/distinct-on/tablesample/PIVOT/unpivot/array-join/date-conversion/split/lock не могут быть «объявлены, но не реализованы». |
| [`DataContextExtensions`](xref:NextORM.Core.DataContextExtensions) | Независимые от провайдера помощники: [`From`](xref:NextORM.Core.DataContextExtensions), [`From`](xref:NextORM.Core.DataContextExtensions), [`FromTableFunction`](xref:NextORM.Core.DataContextExtensions), [`FromSql`](xref:NextORM.Core.DataContextExtensions), точки входа CTE ([`With`](xref:NextORM.Core.DataContextExtensions) / [`WithRecursive`](xref:NextORM.Core.DataContextExtensions)) и терминалы подготовленных команд. |

### Атрибуты отображения

| Тип | Описание |
|---|---|
| [`SqlTableAttribute`](xref:NextORM.Core.SqlTableAttribute) | Отображает класс или интерфейс на имя таблицы (`[SqlTable("name")]`). |
| [`SqlFunctionAttribute`](xref:NextORM.Core.SqlFunctionAttribute) | Отображает метод CLR (или его объявляющий тип) на скалярную функцию базы данных; необязательные `Name`/`Schema`. |
| [`SqlTableFunctionAttribute`](xref:NextORM.Core.SqlTableFunctionAttribute) | Отображает статический метод (или его объявляющий тип) на табличную функцию, используемую как источник `FROM`. |

### Вспомогательные типы выражений

| Тип | Описание |
|---|---|
| [`OrderDirection`](xref:NextORM.Core.OrderDirection) | [`Asc`](xref:NextORM.Core.OrderDirection.Asc) / `` |
| [`JoinType`](xref:NextORM.Core.JoinType) | [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full), [`Cross`](xref:NextORM.Core.JoinType.Cross), [`FullCross`](xref:NextORM.Core.JoinType.FullCross), [`CrossApply`](xref:NextORM.Core.JoinType.CrossApply), [`OuterApply`](xref:NextORM.Core.JoinType.OuterApply). |
| [`JoinExpression`](xref:NextORM.Core.JoinExpression) | Одно соединение: условие, тип и присоединяемый источник. |
| [`JoinStrictness`](xref:NextORM.Core.JoinStrictness) | Модификатор join ClickHouse: [`Default`](xref:NextORM.Core.JoinStrictness.Default), [`Any`](xref:NextORM.Core.JoinStrictness.Any), [`All`](xref:NextORM.Core.JoinStrictness.All), [`Asof`](xref:NextORM.Core.JoinStrictness.Asof). Применяется через [`EntityBuilder.WithStrictness`](xref:NextORM.Core.EntityBuilder`1); вариант `GLOBAL` — через [`EntityBuilder.Global`](xref:NextORM.Core.EntityBuilder`1). |
| [`ArrayJoinKind`](xref:NextORM.Core.ArrayJoinKind) | Вид ClickHouse <c>ARRAY JOIN</c>: [`Inner`](xref:NextORM.Core.ArrayJoinKind.Inner) через [`EntityBuilder.ArrayJoin`](xref:NextORM.Core.EntityBuilder`1) (отбрасывает пустые массивы) или [`Left`](xref:NextORM.Core.ArrayJoinKind.Left) через [`EntityBuilder.LeftArrayJoin`](xref:NextORM.Core.EntityBuilder`1) (сохраняет их); разворачивает по строке на элемент массива. |
| [`ArrayJoinProjection<TEntity, TElement>`](xref:NextORM.Core.ArrayJoinProjection`2) | Проекция, возвращаемая [`EntityBuilder.ArrayJoinElement`](xref:NextORM.Core.EntityBuilder`1)/[`EntityBuilder.LeftArrayJoinElement`](xref:NextORM.Core.EntityBuilder`1): [`Item1`](xref:NextORM.Core.ArrayJoinProjection`2.Item1) — исходная сущность, [`Element`](xref:NextORM.Core.ArrayJoinProjection`2.Element) — вырожденный элемент массива. Требует диалект с клаузой <c>ARRAY JOIN</c>. |
| [`FromExpression`](xref:NextORM.Core.FromExpression) / [`SelectExpression`](xref:NextORM.Core.SelectExpression) | Источник FROM и метаданные проецируемого столбца. |
| [`TableSampleMethod`](xref:NextORM.Core.TableSampleMethod) | Алгоритм семплирования для [`EntityBuilder.TableSample`](xref:NextORM.Core.EntityBuilder`1): [`System`](xref:NextORM.Core.TableSampleMethod.System) / [`Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli) (Bernoulli — только PostgreSQL). |
| [`LockMode`](xref:NextORM.Core.LockMode) | Степень блокировки строк для [`EntityBuilder.ForUpdate`](xref:NextORM.Core.EntityBuilder`1)/[`EntityBuilder.ForShare`](xref:NextORM.Core.EntityBuilder`1): [`Update`](xref:NextORM.Core.LockMode.Update) / [`Share`](xref:NextORM.Core.LockMode.Share). |
| [`TemporalKind`](xref:NextORM.Core.TemporalKind) / [`TemporalClause`](xref:NextORM.Core.TemporalClause) | Клауза `FOR SYSTEM_TIME` для [`EntityBuilder.ForSystemTime`](xref:NextORM.Core.EntityBuilder`1): [`AsOf`](xref:NextORM.Core.TemporalKind.AsOf)/[`Between`](xref:NextORM.Core.TemporalKind.Between)/[`FromTo`](xref:NextORM.Core.TemporalKind.FromTo)/[`ContainedIn`](xref:NextORM.Core.TemporalKind.ContainedIn)/[`All`](xref:NextORM.Core.TemporalKind.All), создаётся статическими фабричными методами. |
| [`UnionType`](xref:NextORM.Core.UnionType) | `None`, [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct), [`All`](xref:NextORM.Core.UnionType.All), [`Intersect`](xref:NextORM.Core.QueryCommand`1), [`IntersectAll`](xref:NextORM.Core.QueryCommand`1), [`Except`](xref:NextORM.Core.QueryCommand`1), [`ExceptAll`](xref:NextORM.Core.QueryCommand`1). |

### Внедрение зависимостей

| Тип | Описание |
|---|---|
| [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) | Построитель параметров провайдера: [`UseLoggerFactory`](xref:NextORM.Core.DataContextBuilder), [`LogSensitiveData`](xref:NextORM.Core.DataContextBuilder), [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory), [`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder), [`UseNamingConvention`](xref:NextORM.Core.DataContextBuilder). |
| [`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions) | [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions) / [`AddKeyedNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions) (универсальные и управляемые options). |

## Пространство имён `nextorm.sqlite`

| Тип | Описание |
|---|---|
| [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) поверх `Microsoft.Data.Sqlite`; регистрирует пользовательские агрегаты. |
| [`SqliteDialect`](xref:NextORM.Sqlite.SqliteDialect) | Синглтон [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) для SQLite ([`Instance`](xref:NextORM.Sqlite.SqliteDialect.Instance)). |
| [`SqliteDataContextOptionsBuilderExtensions`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions) | `UseSqlite(string filepath)` и `UseSqlite(DbConnection)`. |

## Пространство имён `nextorm.postgres`

| Тип | Описание |
|---|---|
| [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) поверх `Npgsql`. |
| [`PostgresDialect`](xref:NextORM.Postgres.PostgresDialect) | Синглтон [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) для PostgreSQL ([`Instance`](xref:NextORM.Postgres.PostgresDialect.Instance)). |
| [`PostgresDataContextOptionsBuilderExtensions`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions) | `UsePostgres(string connectionString)` и `UsePostgres(DbConnection)`. |

## Пространство имён `nextorm.sqlserver`

| Тип | Описание |
|---|---|
| [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) поверх `Microsoft.Data.SqlClient`, с преобразованием числовых столбцов. |
| [`SqlServerDialect`](xref:NextORM.SqlServer.SqlServerDialect) | Синглтон [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) для SQL Server ([`Instance`](xref:NextORM.SqlServer.SqlServerDialect.Instance)). |
| [`SqlServerDataContextOptionsBuilderExtensions`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions) | `UseSqlServer(string connectionString)` и `UseSqlServer(DbConnection)`. |

## Пространство имён `nextorm.mysql`

| Тип | Описание |
|---|---|
| [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) поверх `MySqlConnector`. |
| [`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) | Синглтон [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) для MySQL ([`Instance`](xref:NextORM.MySql.MySqlDialect.Instance)); не `sealed`, чтобы MariaDB мог наследоваться. |
| [`MySqlDataContextOptionsBuilderExtensions`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions) | `UseMySql(string connectionString)` и `UseMySql(DbConnection)`. |

## Пространство имён `nextorm.mariadb`

| Тип | Описание |
|---|---|
| [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) поверх `MySqlConnector`, наследуется от [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext). |
| [`MariaDbDialect`](xref:NextORM.MariaDb.MariaDbDialect) | Синглтон [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) для MariaDB ([`Instance`](xref:NextORM.MariaDb.MariaDbDialect.Instance)); отрисовка MySQL плюс `INTERSECT ALL`/`EXCEPT ALL`. |
| [`MariaDbDataContextOptionsBuilderExtensions`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions) | `UseMariaDb(string connectionString)` и `UseMariaDb(DbConnection)`. |

## Пространство имён `nextorm.clickhouse`

| Тип | Описание |
|---|---|
| [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) поверх официального ADO.NET-провайдера `ClickHouse.Driver`. |
| [`ClickHouseDialect`](xref:NextORM.ClickHouse.ClickHouseDialect) | Синглтон [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) для ClickHouse ([`Instance`](xref:NextORM.ClickHouse.ClickHouseDialect.Instance)). |
| [`ClickHouseDataContextOptionsBuilderExtensions`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions) | `UseClickHouse(string connectionString)` и `UseClickHouse(DbConnection)`. |

## См. также

- [Provider overview](../providers/overview.md)
- [SQLite](../providers/sqlite.md)
- [SQL Server](../providers/sqlserver.md)
- [PostgreSQL](../providers/postgres.md)
- [MySQL](../providers/mysql.md)
- [MariaDB](../providers/mariadb.md)
- [ClickHouse](../providers/clickhouse.md)
- [In-memory](../providers/in-memory.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.sqlite/**`, `src/nextorm.postgres/**`,
`src/nextorm.sqlserver/**`, `src/nextorm.mysql/**`, `src/nextorm.mariadb/**`,
`src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).

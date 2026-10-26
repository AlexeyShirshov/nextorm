# Схемы результата табличных функций

> Некоторые табличные функции не объявляют выходные колонки в вызове: схему предоставляет
> вызывающий. Объявите её как отображённый тип строки runtime-фабрики и выберите, куда она рендерится.

**Предварительные требования:** [Табличные функции](13-table-valued-functions.md) · [Динамические колонки](29-dynamic-columns.md) · [Сырой SQL](14-raw-sql.md#курсоры-наборов-результатов)

## Динамическая схема результата <a name="dynamic-result-schema"></a>

Табличная функция, форма результата которой предоставляется вызывающим, берёт её из типа строки
фабрики — `T` в `TvfEntityBuilder<T>`. nextorm рендерит соответствующую нативную колончатую форму из
`[Column]`-метаданных этого типа и CLR-типов свойств, а каждая форма провайдера выражается
[`TableFunctionSchema`](xref:NextORM.Core.TableFunctionSchema) в дескрипторе:

```csharp
public TvfEntityBuilder(
    string name,
    string? schema,
    string? withClause,
    string? callClause,
    IReadOnlyList<int> verbatimArguments,
    TableFunctionSchema resultSchema);
```

Строки по-прежнему материализуются в тот же DTO с `[Column]`, что и у любой другой табличной функции —
отдельного динамического читателя нет, — а объявленная схема входит в ключ плана запроса, поэтому две
формы строки никогда не разделяют кэшированный план. `None` — значение по умолчанию, оно ничего не
рендерит: функция сама предоставляет форму результата.

## Где рендерится схема

| `ResultSchema` | Рендеримая форма | Провайдер |
|---|---|---|
| `None` | ничего — функция объявляет свои колонки | каждый провайдер |
| `LeadingArgument` | квотированная строка структуры как первый аргумент вызова: `values('a UInt8, b String', (1,'x'))` | ClickHouse |
| `AliasColumnList` | список определений колонок после псевдонима: `jsonb_to_recordset(@json) as "t1"(a integer, b text)` | PostgreSQL |

ClickHouse `values` читает литеральную таблицу из списка кортежей; аргумент `tuples` выводится verbatim,
поэтому это SQL, написанный разработчиком, — никогда не строите его из пользовательского ввода:

```csharp
public interface IPairRow
{
    [Column("a")]
    byte A { get; set; }
    [Column("b")]
    string? B { get; set; }
}

var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.values<IPairRow>("(1, 'x'), (2, 'y')"))
    .Select(r => new { r.A, r.B })
    .ToList();
```

```sql
select a, b
 from values('a UInt8, b String', (1, 'x'), (2, 'y')) as `t1`
```

PostgreSQL `jsonb_to_record`/`jsonb_to_recordset` разворачивают JSON-объект/массив в record, колонки
которого объявлены типом строки:

```csharp
public interface IPairRow
{
    [Column("a")]
    int A { get; set; }
    [Column("b")]
    string? B { get; set; }
}

using var json = JsonDocument.Parse("""[{"a":1,"b":"x"},{"a":2,"b":"y"}]""");

var rows = dataContext
    .From(() => SqlFunctions.Postgres.jsonb_to_recordset<IPairRow>(json))
    .Select(r => new { r.A, r.B })
    .ToList();
```

```sql
select a, b
 from jsonb_to_recordset(@json) as "t1"(a integer, b text)
```

Типы колонок берутся из CLR-to-SQL отображения типов провайдера (`byte`→`UInt8`, `int`→`integer`,
`string`→`text`/`String`); nullable value-тип становится `Nullable(T)` в ClickHouse.

## Ограничения провайдеров

Провайдер объявляет поддержку конкретной формы, которую может выразить
([`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema)));
объявленная форма, которую провайдер не реализует, отклоняется с `NotSupportedException` — она никогда
не выводится как SQL без списка колонок.

| Провайдер | `None` | `LeadingArgument` | `AliasColumnList` |
|---|---|---|---|
| PostgreSQL | да | отклонено | да |
| SQL Server | да | отклонено | отклонено |
| MySQL | да | отклонено | отклонено |
| MariaDB | да | отклонено | отклонено |
| SQLite | да | отклонено | отклонено |
| ClickHouse | да | да | отклонено |
| In-memory | отклонено (источники TVF не поддерживаются вовсе) | отклонено | отклонено |

Отказ называет функцию и форму:
`The table function '<name>' declares a result schema in the 'LeadingArgument' form, which is not supported
by this provider.` Тип строки без отображённых свойств падает с `BuildSqlCommandException` вместо вывода
пустого списка колонок.

Поскольку PostgreSQL требует список определений колонок после псевдонима, `jsonb_to_record`/`jsonb_to_recordset`
всегда нуждаются в объявленном вызывающим типе строки; свободный/частичный список колонок без него не
поддерживается. Для схемы без объявленных колонок используйте хранилище `[DynamicColumns]` (см.
[Динамические колонки](29-dynamic-columns.md)), сырой читатель `ToDataReader`/`ResultSet` (см.
[Сырой SQL](14-raw-sql.md#курсоры-наборов-результатов)) или функции разворачивания ключ/значение
`jsonb_each`/`jsonb_each_text`/`jsonb_object_keys` (см.
[Динамическая схема записи](provider-specific-sql/postgresql.md#динамическая-схема-записи)).

## См. также

* [Табличные функции](13-table-valued-functions.md) — объявление runtime-фабрики, аргументы, соединения и APPLY.
* [ClickHouse](provider-specific-sql/clickhouse.md) — табличная функция `values` и её аргумент структуры.
* [PostgreSQL](provider-specific-sql/postgresql.md#динамическая-схема-записи) — record-функции и их список колонок.
* [Динамические колонки](29-dynamic-columns.md) — чтение колонок, которые тип строки не объявляет.

---

Source: `src/nextorm.core/Expressions/TableFunctionSchema.cs`, `src/nextorm.core/DataContext/SqlSourceRenderer.cs` (`RenderResultSchema`), `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`;
диалекты провайдеров `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.clickhouse/ClickHouseDialect.cs`;
тесты: `tests/nextorm.<provider>.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.integration.tests/T320/T320ClickHouseTvfTests.cs`.

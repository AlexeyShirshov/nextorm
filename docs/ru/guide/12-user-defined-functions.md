# Пользовательские функции

> Вызывайте скалярную функцию базы данных из выражения LINQ, сопоставив метод-заглушку CLR с помощью
> `[SqlFunction]`.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](../engine/index.md) · [Скалярные функции](../scalar-functions/index.md)

## Обзор

[`SqlFunctionAttribute`](xref:NextORM.Core.SqlFunctionAttribute) сопоставляет метод CLR скалярной функции базы данных. Примените его к методу-заглушке
или к объявляющему типу, чтобы сопоставить каждый метод этого типа по имени:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SqlFunctionAttribute : Attribute
{
    public SqlFunctionAttribute();
    public SqlFunctionAttribute(string name);
    public string? Name { get; set; }     // defaults to the CLR method name
    public string? Schema { get; set; }   // optional schema/owner prefix
    public bool IsAggregate { get; set; } // defaults to false: a scalar function
}
```

Тело заглушки **никогда не выполняется** — оно лишь должно удовлетворять компилятор, поэтому достаточно
`=> throw new NotSupportedException()` или `=> default!`. Вызов преобразуется в
`[schema.]name(arg1, arg2, ...)`:

* `Name` переопределяет генерируемое имя функции; без него используется имя метода CLR (это же поведение
  действует, когда атрибут находится на объявляющем типе).
* `Schema` добавляет префикс к имени в виде `schema.name`.
* аргументы рендерятся через обычный посетитель выражений, поэтому **захваченное значение становится
  параметром** точно так же, как и везде;
* для **экземплярного** метода целевой объект (`node.Object`) генерируется как **первый аргумент**, перед
  аргументами метода.

Встроенные преобразования `string`/`Math`/`DateTime` выполняются первыми, поэтому `[SqlFunction]` не может
изменить их поведение. nextorm только генерирует вызов; функция уже должна существовать в целевой базе
данных.

## Сопоставление статического метода

```csharp
private static class Udf
{
    [SqlFunction("upper")]
    public static string ToUpper(string value) => throw new NotSupportedException();

    [SqlFunction("abs")]
    public static long Abs(long value) => throw new NotSupportedException();

    [SqlFunction("coalesce")]
    public static int? Coalesce(int? value, int? fallback) => throw new NotSupportedException();
}

var value = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == 2)
    .Select(e => Udf.ToUpper(e.String!))
    .First();
// "XXX"
```

```sql
-- SQLite
select upper(somestring) as 'V'
 from complex_entity
-- SQL Server
select upper(somestring) as [V]
 from complex_entity
-- PostgreSQL
select upper(somestring) as "V"
 from complex_entity
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| V   |
|-----|
| XXX |

## Имя и схема

`Name` переопределяет имя функции, а `Schema` квалифицирует его:

```csharp
private static class Udf
{
    [SqlFunction("my_fn", Schema = "dbo")]
    public static long WithSchema(long value) => throw new NotSupportedException();
}

var value = dataContext.From<IComplexEntity>()
    .Select(e => new { V = Udf.WithSchema(e.Id) })
    .First();
```

```sql
select dbo.my_fn(id) as 'V'
 from complex_entity
```

## Атрибут на объявляющем типе

Когда атрибут размещён на типе, каждый метод сопоставляется по его имени CLR (атрибут для каждого метода
не нужен):

```csharp
[SqlFunction]
private static class ImplicitUdf
{
    public static string Lower(string value) => throw new NotSupportedException();
}

var value = dataContext.From<IComplexEntity>()
    .Select(e => new { V = ImplicitUdf.Lower(e.String!) })
    .First();
```

```sql
select Lower(somestring) as 'V'
 from complex_entity
```

## Пометка сопоставленной функции как агрегата

`IsAggregate` объявляет, что сопоставленная функция сворачивает строки группы, а не возвращает
скалярное значение. По умолчанию свойство равно `false`:

```csharp
[SqlFunction("my_sum", IsAggregate = true)]
public static decimal MySum(decimal value) => throw new NotSupportedException();
```

`IsAggregate = true` делает вызов непрозрачной агрегатной границей для `SelectGroup`: его
аргументы, его `DISTINCT`-вход и его `FILTER`-предикат никогда не становятся ключами группировки, а
проекция без зависящих от строки членов становится глобальной агрегацией. Скалярная функция — без
атрибута или с `IsAggregate = false` — может, наоборот, быть выведена как целый ключ группировки.

Эффективная аннотация — это **либо** собственный атрибут метода, **либо**, когда у метода его нет,
атрибут объявляющего типа, и никогда их слияние. Поэтому атрибут метода заменяет атрибут типа
целиком: `[SqlFunction("f")]` на методе типа, помеченного `[SqlFunction(IsAggregate = true)]`,
делает этот метод скалярным, и ни `Name`, ни `Schema` из типа не наследуются.

Распознавание идёт по методу и объявляющему типу, а не по имени CLR: пользовательская функция с
именем `sum` рендерится под своим объявленным SQL-именем и никогда не считается встроенным
агрегатом, а тот же метод с `IsAggregate = true` является границей пользовательского агрегата.

Аннотация только классифицирует вызов. Она не обещает, что функция существует в целевой базе
данных, что диалект поддерживает `FILTER` или что провайдер in-memory умеет её вычислять:
неподдерживаемые функции отклоняются там явно, а не выполняются из-за совпадения имени со встроенной.

## Захваченный аргумент становится параметром

Захваченная локальная переменная параметризуется, и проход извлечения параметров переносит её:

```csharp
var start = 1;

var prepared = dataContext.From<IComplexEntity>()
    .Select(e => new { V = Udf.Slice(e.String!, start) })
    .Prepare();
```

```sql
-- SQLite placeholder; SQL Server/PostgreSQL use @start
select substr(somestring, $start) as 'V'
 from complex_entity
```

с сопоставленным методом:

```csharp
[SqlFunction("substr")]
public static string Slice(string value, int start) => throw new NotSupportedException();
```

## Экземплярные методы

Для экземплярного метода получатель является первым SQL-аргументом:

```csharp
public sealed class Formatter
{
    [SqlFunction("format")]
    public string Format(string value) => throw new NotSupportedException();
}
```

Запрос, использующий `new Formatter().Format(e.String!)`, рендерится как `format(<receiver>, somestring)`.
Это реализовано в `BaseExpressionVisitor.TryTranslateSqlFunction` (`node.Object` генерируется перед
аргументами метода).

## Различия между провайдерами

Имя функции и схема генерируются **дословно** каждым провайдером — ни один из встроенных диалектов не
заключает их в кавычки и не переименовывает, — поэтому один и тот же текст `[SqlFunction]` должен
указывать на существующий объект в каждой целевой базе данных. Различается только заключение в кавычки
окружающего идентификатора (псевдонима столбца):

| Провайдер | Поведение |
|---|---|
| SQLite | `[schema.]name(args)` дословно; псевдонимы в одинарных кавычках (`as 'V'`). |
| SQL Server | `[schema.]name(args)` дословно; псевдонимы в квадратных скобках (`as [V]`). |
| PostgreSQL | `[schema.]name(args)` дословно; псевдонимы в двойных кавычках (`as "V"`). |
| MySQL | `[schema.]name(args)` дословно; псевдонимы в обратных кавычках (`` as `V` ``). |
| MariaDB | `[schema.]name(args)` дословно; псевдонимы в обратных кавычках (`` as `V` ``). |
| ClickHouse | `[schema.]name(args)` дословно; псевдонимы в обратных кавычках (`` as `V` ``). |
| In-memory | Атрибут — это SQL-преобразование; провайдер in-memory вместо этого вычисляет метод CLR. |

## См. также

* [Скалярные функции](../scalar-functions/index.md) — встроенные преобразования, имеющие приоритет.
* [Табличные функции](13-table-valued-functions.md) — эквивалент для источника `FROM`.
* [Обзор провайдеров](../connecting-providers/overview.md) — кавычки и флаги возможностей.

---

Source: `src/nextorm.core/SqlFunctionAttribute.cs:14`, `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:563`;
`tests/nextorm.integration.tests/CommonTestSuite/Functions/CommonTestSuite.Udf.cs:14`, `:24`, `:36`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:713`, `:722`, `:737`, `:746`;
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:466`, `:476`, `:490`;
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:399`, `:409`, `:423`.

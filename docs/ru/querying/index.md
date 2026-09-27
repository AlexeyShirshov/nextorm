# Запросы и проекции

> Формируйте результат запроса с помощью [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})): одна колонка, анонимный тип, DTO или запись (record), кортеж, инициализатор членов, вложенная сущность или вычисляемая колонка.

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)

## Обзор

`dataContext.From<TEntity>()` возвращает [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). Каждый запрос начинается с
проецирования этой сущности с помощью [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

```csharp
public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
```

Лямбда не выполняется - она транслируется в список `SELECT` генерируемого оператора.
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) возвращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); терминальный метод ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])),
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) выполняет его. См. [Сортировку и постраничную выборку](../guide/04-sorting-and-paging.md)
для терминальных методов и их асинхронных форм. Если проекция — одна колонка `byte[]`/`string`, её вместо этого можно стримить через `ToStream`/`ToTextReader` — см. [Потоковое чтение больших объектов](../guide/30-large-objects.md).

Правила, применимые к любой проекции:

* Форма лямбды определяет список выборки. Колонки, которые не проецируются, не читаются.
* Проецируемый член, имя которого совпадает с именем отображаемой колонки (без учёта регистра),
  генерируется без псевдонима. Переименованный или вычисляемый член получает псевдоним: `as 'Calc'` в
  SQLite, `as [Calc]` в SQL Server, `as "Calc"` в PostgreSQL.
* Аргументы конструктора, элементы кортежа и присваивания в инициализаторе членов отображаются в
  элементы списка выборки; материализатор времени выполнения строит объект из reader'а.
* В in-memory провайдере SQL не существует; то же самое выражение компилируется и выполняется над
  набором данных в памяти.

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных
тестов: `simple_entity` содержит идентификаторы `1`-`10`, а `complex_entity` — три строки
(`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

## См. также

* [Фильтрация (WHERE)](../guide/01-filtering-where.md)
* [Сортировка и постраничная выборка](../guide/04-sorting-and-paging.md)
* [Табличные функции](../guide/11-table-valued-functions.md)
* [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)
* [Обзор провайдеров](../providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:26`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:45`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:75`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:94`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:103`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:112`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:122`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:352`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:367`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:382`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:727`;
`tests/nextorm.integration.tests/TestModels.cs:3`;
`tests/nextorm.integration.tests/TestModels.cs:12`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:111`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:217`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:242`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:268`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:223`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:248`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1117`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1132`.

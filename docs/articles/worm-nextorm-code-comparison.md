# WORM → Nextorm: карта соответствий в коде (первый проход)

> Исследовательские тезисы, **не проза статьи**. Сравниваем зафиксированные состояния кода: [WORM, 15.05.2020](https://github.com/AlexeyShirshov/oml/commit/886f29cda495ae08b3e66c922d68f6e0a54bb8e3) и [Nextorm, 27.10.2026](https://github.com/AlexeyShirshov/nextorm/commit/15a4d0bca5873525d4a78d034ce879298c04ec3a). Даты — даты срезов, не даты появления соответствующих идей.
>
> Автор подтвердил: Nextorm — новая реализация WORM на современном стеке с сохранением основных концепций. Этот файл проверяет **параллели в устройстве**, но не устанавливает буквальный перенос исходников.

## 1. Объектная модель SQL-запроса

- **WORM:** `QueryCmd` хранит структуру запроса, включая выборку; отдельный построитель `FCtor` создаёт выражения `PropertyAliasExpression`, `EntityExpression`, `TableExpression`, подзапросы — [`QueryCmd.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/QueryCmd/QueryCmd.vb#L18-L60), [`SelectBuilder.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Query/Builders/SelectBuilder.vb#L8-L70).
- **WORM:** собственный LINQ `IQueryProvider` преобразует `System.Linq.Expressions.Expression` через `QueryVisitor` в `QueryCmd` — [`Provider.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/net3.5/linqprovider/Provider.vb#L45-L82).
- **Nextorm:** `EntityBuilder<T>` создаёт `QueryCommand<TResult>` из `Expression<Func<...>>`; `Where` и `Select` типизированы — [`EntityBuilder.cs` (Select)](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/EntityBuilder.cs#L324-L357), [`Where`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/EntityBuilder.cs#L502-L520).
- **Преемственность идеи:** структурное построение запросов вместо ручной конкатенации SQL.
- **Точный путь трансляции LINQ в WORM:** [`WormLinqProvider.Execute<TResult>`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/net3.5/linqprovider/Provider.vb#L69-L75) передаёт `System.Linq.Expressions.Expression` в `QueryVisitor`; [`FilterVisitor.VisitBinary`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/net3.5/linqprovider/Provider.vb#L619-L710) переводит сравнения/логические операторы в собственные `Criteria.IFilter`; собственный [`ExpressionVisitor`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/net3.5/linqprovider/BaseVisitor.vb#L7-L76) обходит дерево LINQ. Затем [`IExpression.MakeStatement`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Query/Expressions/Interfaces.vb#L73-L99) и [`BinaryExpression.MakeStatement`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Query/Expressions/BinaryExpression.vb#L403-L414) рендерят собственные выражения в SQL с помощью `StmtGenerator`.
- **Точный путь в Nextorm:** [`EntityBuilder.Where(Expression<Func<T,bool>>)`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/EntityBuilder.cs#L502-L520) сохраняет типизированный предикат; SQL строится после подготовки `QueryCommand` (см. [`SqlBuilder.MakeSelect`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/SqlBuilder.cs#L31-L43)). [`BaseExpressionVisitor : System.Linq.Expressions.ExpressionVisitor`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Visitors/BaseExpressionVisitor.cs#L12-L16) и [`WhereExpressionVisitor`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Visitors/WhereExpressionVisitor.cs#L5-L22) обрабатывают узлы, [`PredicateTranslator.VisitBinary`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Visitors/PredicateTranslator.cs#L342-L425) формирует SQL-предикат с учётом диалекта.
- **Вывод именно об `Expressions → SQL`:** идея обхода `System.Linq.Expressions` существовала уже в LINQ-провайдере WORM; **механизм не идентичен**. В WORM LINQ-выражение сначала переводилось во внутреннюю модель запросов/фильтров, которая самостоятельно формировала SQL; в Nextorm expression visitors непосредственно участвуют в генерации SQL внутри конвейера подготовки/рендеринга. Это **не** означает, что у Nextorm отсутствует собственная промежуточная модель `QueryCommand`.
- **Изменение реализации:** WORM использует собственные expression-builder'ы и отдельный LINQ-провайдер; Nextorm строит основной fluent API непосредственно вокруг типизированных C# expression trees.
- **Тестовые примеры:** [WORM `TestFilter` и raw-table запрос](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/QueryTest.vb#L54-L103); [WORM LINQ-тесты](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/net3.5/TestLinq/TestLinq.vb); [Nextorm тесты построителей](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/tests/nextorm.core.tests/Builders/CommandBuilderTests.cs).

## 2. Генерация SQL / особенности СУБД

- **WORM:** `StmtGenerator` — абстракция различий SQL, в том числе параметров, операторов, группировки и генерации текста — [`StmtGenerator.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/StmtGenerators/StmtGenerator.vb#L10-L65).
- **WORM:** `SQL2000Generator` дополнительно создаёт `SqlCommand`, `SqlParameter`, `SqlConnection`: рендеринг диалекта и фабрика ADO.NET находятся в одной реализации — [`SQL2000Generator.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/StmtGenerators/db/MSSQL/SQL2000Generator.vb#L139-L175).
- **Nextorm:** `SqlBuilder` рендерит SQL с помощью `ISqlDialect`; контракт диалекта намеренно отделён от создания соединений, параметров и маппинга колонок — [`ISqlDialect.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/Dialect/ISqlDialect.cs#L1-L16), [`SqlBuilder.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/SqlBuilder.cs).
- **Преемственность идеи:** генерировать SQL с учётом возможностей конкретной СУБД.
- **Изменение реализации:** более явное разделение *рендеринга SQL* и *провайдерного исполнения* в Nextorm.
- **Вопрос автору после анализа:** было ли такое разделение сознательным ответом на сложности прежнего дизайна?

## 3. Отображение типов и результатов

- **WORM:** `ObjectMappingEngine` управляет схемами сущностей и их версиями — [`MappingEngine.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Mapping/MappingEngine.vb#L55-L115); [атрибуты полей](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Mapping/ObjectSchema/MetaAttributes.vb#L1-L75).
- **Nextorm:** `EntityMetadataBuilder<T>` поддерживает fluent-сопоставление таблиц/полей и отношений — [`EntityMetadataBuilder.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs#L485-L520).
- **Nextorm:** возможна работа **без класса сущности** через `From(string)` и `TableAlias` — [`DataContext.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/DataContext.cs#L223-L227), [`TableAlias.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/TableAlias.cs#L1-L26).
- **Nextorm:** фабрика маппинга формирует дерево выражений для чтения `IDataRecord` и компилирует делегат — [`RowMapperFactory.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/RowMapperFactory.cs#L339-L367).
- **Преемственность идеи:** отделить SQL-представление данных от CLR-представления.
- **Изменение реализации:** маппинг без обязательной сущности и компиляция мапперов. **Не утверждать** без отдельной проверки, что в WORM полностью отсутствовали динамические проекции или компилированные способы чтения.

## 4. Провайдеры и границы ответственности

- **WORM:** семейство SQL Server 2000/2005/2008/CE — [каталог MSSQL](https://github.com/AlexeyShirshov/oml/tree/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/StmtGenerators/db/MSSQL); отдельно [MySQL-проект](https://github.com/AlexeyShirshov/oml/tree/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/MySQL).
- **Nextorm:** отдельные реализации [SQL Server](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.sqlserver/SqlServerDialect.cs), [SQLite](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.sqlite/SqliteDialect.cs), [PostgreSQL](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.postgres/PostgresDialect.cs), [MySQL](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.mysql/MySqlDialect.cs); MariaDB, ClickHouse и in-memory — см. [README](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/readme.md).
- **Преемственность идеи:** независимость абстрактной модели запроса от конкретного диалекта.
- **Изменение реализации:** отдельный `ISqlDialect` и набор provider-проектов; нельзя автоматически приравнивать список диалектов к одинаковой функциональной полноте.
- **Проверка:** сравнить особенности `JOIN`, лимитов, идентификаторов и параметров на одном use case в двух кодовых базах.

## 5. Кэш: существенная сознательная граница

- **WORM:** хранение результатов и проверка их актуальности — [`OrmCache.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Cache/OrmCache.vb), [`CacheItem.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Cache/CacheItem.vb).
- **WORM:** тест попадания в кэш и сброса после изменения — [`TestTableFilterUpdate`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/Cache.vb#L168-L197).
- **Nextorm:** структурный `QueryPlan` — [`QueryPlan.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/Cache/QueryPlan.cs#L35-L103); потоково-локальный `QueryPlanStore` — [`QueryPlanStore.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/DataContext/Cache/QueryPlanStore.cs#L18-L65).
- **Подтверждение автора:** отказ от кэша результатов из-за сложности инвалидации, weak references и расхода памяти/аллокаций — [авторские заметки](author-notes-01-reasons-for-nextorm.md).
- **Не смешивать:** *кэш результата* (готовые строки/объекты) ≠ *кэш плана* (подготовленная структура запроса/SQL/маппинг).

## 6. Снятые возможности

- **WORM:** состояния сущностей, `SaveChanges` и управление модификациями — [`CachedEntity.vb`](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/obm/Entity/CachedEntity.vb#L1240-L1253), [тесты ModificationsTracker](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/TestRealSchema/TestTracker.vb#L140-L210).
- **Nextorm:** отказ от change tracking, явные DML-билдеры — [сравнение возможностей](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/ru/comparisons/index.md), [`UpdateBuilder.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/UpdateBuilder.cs), [`DeleteBuilder.cs`](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/DeleteBuilder.cs).
- **Статус:** подтверждённое отличие, не отрицание общей преемственности идей.

## Выводы первого прохода

- **Совпадение концепций подтверждено:** структурные запросы, генерация SQL, маппинг, абстракция SQL-диалекта.
- **Технический способ реализации различается:** собственный AST/построители WORM ↔ C# expression trees/fluent API Nextorm; совмещённые обязанности SQL generator ↔ выделенный `ISqlDialect`.
- **Сознательные ограничения Nextorm:** отсутствие кэша результатов и change tracking.
- **Не проверено:** прямое копирование кода; точные даты появления каждой подсистемы; полная эквивалентность поведения по use cases; сравнительные затраты памяти/скорость. Не делать этих заявлений в статье.

## Следующее исследование

- Взять **один use case на каждый механизм** и сопоставить: исходный вызов → дерево/команда → SQL → результат → тест.
- Проверить WORM и Nextorm на: проекциях и маппинге, сложных соединениях, обработке параметров, работе с несколькими СУБД.
- Только затем спросить автора о нетривиальных расхождениях, которых не видно из кода.

# Аудит публичного API nextorm: именования

**Дата:** 2026-09-17
**Область:** `src/nextorm.core`, `src/nextorm.postgres`, `src/nextorm.sqlite`, `src/nextorm.sqlserver`, `src/nextorm.core.sourcegenerator`
**Методика:** скилл `api-design` (Framework Design Guidelines) + `dotnet-xml-docs` (XML-документация). Основание для вывода — XML-комментарий (`<summary>`/`<param>`, если есть) либо тело метода/свойства. Проект в стадии **alpha**: обратная совместимость не поддерживается, имена меняются напрямую.

## 1. Как читать отчёт

Приоритеты:

| Уровень | Значение |
|---------|----------|
| **P0** | Имя вводит в заблуждение или конфликтует с BCL; ошибка проектирования |
| **P1** | Нарушение .NET-конвенций именования (тип/член) |
| **P2** | Несогласованность / косметика / загрязнение публичной поверхности |

**Alpha-политика:** обратная совместимость не сохраняется. Старое имя заменяется новым **на месте** — в коде, тестах, примерах и документации. Ничего не помечаем `[Obsolete]`, не заводим алиасов, дублирующих членов и type forwarders. Поверхность замораживается только к релизу 1.0.

## 2. Покрытие XML-документацией (baseline)

| Категория | Документировано | Всего | Доля |
|-----------|----------------:|------:|-----:|
| Публичные типы | 33 | 142 | 23 % |
| Публичные методы | 56 | 742 | 7 % |
| Публичные свойства | 34 | 208 | 16 % |

`GenerateDocumentationFile=true` включён в 4 проектах `src`, но `NoWarn=CS1591` скрывает пропуски — NuGet-потребители не видят IntelliSense-доков. Полный список недокументированных типов — в приложении A.

## 3. Находки

### P0 — вводят в заблуждение / конфликтуют с BCL

| # | Имя | Файл:строка | Проблема | Рекомендация (переименование) |
|---|-----|-------------|----------|-------------------------------|
| 1 | `HashCode` (struct) | `src/nextorm.core/Expressions/HashCode.cs:12` | Публичный тип затеняет `System.HashCode`; в коде — реализация xxHash32, не публичный контракт | Сделать `internal`, либо переименовать в `XxHash32` |
| 2 | `IQueryProvider` | `src/nextorm.core/Query/IQueryProvider.cs:5` | Конфликтует с `System.Linq.IQueryProvider`; по членам (`AddCommand`, `GetXxxEqualityComparer`, `AddOuterReference`) это реестр/координатор планов, а не LINQ provider | Переименовать в `IQueryRegistry` (или `IQueryPlanRegistry`) |
| 3 | `IDataContextExtensions` (static class) | `src/nextorm.core/DataContext/IDataContextExtensions.cs:6` | Статический класс-расширение с префиксом `I` (читается как интерфейс). Дублирует `DataContextExtensions` (`DataContext/DataContextExtensions.cs`) — API для одного и того же `IDataContext` разложен на два класса | Слить методы `With`/`WithRecursive` в `DataContextExtensions` и удалить дублирующий класс |
| 4 | `EntityP2` … `EntityP8` | `src/nextorm.core/Builders/Joins/JoinCommandBuilder.cs:5,52,92,127,162,197` | `P` = arity проекции; имя нечитаемо и не говорит «join». Публично возвращается из `Join/LeftJoin/...` | Переименовать в `JoinedEntityBuilder<T1,T2>` … `<T1..T8>` |
| 5 | `PrepareException` | `src/nextorm.core/DataContext/PrepareException.cs:6` | Слишком общее имя: неясно, что «prepare» относится к подготовке запроса | `QueryPreparationException`; наследовать от `DataContextException` |
| 6 | `DbQueryCommandExtension` | `src/nextorm.core/Query/DbQueryCommandExtension.cs:3` | Это не extension-класс и не extension-метод: DTO с **публичными изменяемыми полями** `ManualSql`, `MakeParams`. Имя (sing.) конфликтует с конвенцией `*Extensions` (pl.) | Переименовать в `RawSqlOverride`/`QueryOverride`; поля → свойства; при возможности `internal` |
| 7 | `Projection<T1,T2>.t1`, `.t2` (+ до `t8`) | `src/nextorm.core/Builders/Projection.cs:30,31,46,47,48,...` | Публичные свойства со строчной буквы — грубое нарушение PascalCase; попадают в результат join-запроса | Переименовать `t1`…`t8` в `T1`…`T8` |
| 8 | `BasicHelpers` + файл `BasicHelpders.cs` | `src/nextorm.core/BasicHelpders.cs:2` | «Basic/Helpers» — свалка разнородных extension-методов; **опечатка в имени файла** (`Helpders`) | Разнести по назначению (`TryExtensions`, `TransformExtensions`); переименовать файл в `BasicHelpers.cs` |
| 9 | `TableAlias.Int/Long/...` | `src/nextorm.core/Builders/TableAlias.cs:6-…` | Методы названы типами (существительными), а не глаголами; смесь алиасов C# (`Int`, `Long`) и CLR-имён (`Boolean`); параметр `string _` — нет пригодного имени для named-args | Переименовать в `GetInt32`/`GetInt64`/… либо `Col<T>`; дать параметрам осмысленные имена |

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

### P2 — несогласованность публичной поверхности

| # | Имя | Файл:строка | Проблема |
|---|-----|-------------|----------|
| 19 | `Paging.Limit/Offset`, `Sorting.Direction/PreparedExpression` | `Paging.cs:4-5`, `Sorting.cs:6-7` | Публичные **изменяемые поля** вместо свойств; нет валидации, сигнатуру нельзя менять без правки всех вызовов |
| 20 | `SqlTableAttribute` в файле `TableAttribute.cs`; `IColumnsProvider` в файле `ISourceProvider.cs` | `TableAttribute.cs:3`, `Query/ISourceProvider.cs:7` | Несоответствие «файл ↔ тип»; мешает навигации |
| 21 | `IPayload` | `src/nextorm.core/Payload/IPayload.cs:3` | Пустой маркерный интерфейс без документации и без членов |
| 22 | `DbContext`, `IDataContext`, `InMemoryContext`, `DbContextBuilder`, `DataContextOptionsBuilderExtensions` | `DataContext/DbContext.cs`, `DI/DataContextOptionsBuilder.cs:5`, providers `DI/…` | Смешение «DbContext» и «DataContext»/«Context»; builder называется `DbContextBuilder`, а его extensions — `DataContextOptionsBuilderExtensions` |
| 23 | `DbQueryCommandExtension` (sing.) vs `*Extensions` | `Query/DbQueryCommandExtension.cs:3` | Несогласованный суффикс с `EntityExtensions`, `QueryCommandExtensions`, `TypeExtensions`, ... |
| 24 | `DefaultColumnsProvider` в `Query/`, `DefaultAliasProvider` в `DataContext/`, `DefaultParamProvider` в `Query/IParamProvider.cs` | | Непоследовательное размещение `Default*`-реализаций |
| 25 | `AnonymousClassEqualityComparer` (public) | `src/nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs:6` | Публичный тип генератора; должен быть `internal` |
| 26 | namespace `nextorm.core` (lowercase) | все файлы `src` | Пространства имён .NET должны быть PascalCase (`NextORM.Core`) |

### Отмечено, но менять не рекомендуется

- `Sql*` (`SqlTableAttribute`, `ISqlDialect`, `SqlDialectBase`) — распространённая аббревиатура (как `System.Data.SqlClient`), допустима.
- `NORM_SQL`-методы `@in`, `var`, `all`, `any` — вынужденные C#-имена из-за DSL в деревьях выражений; при переименовании нужна таблица «PascalCase-метод → SQL-токен».
- `I*` интерфейсы, `*Exception`, `*Attribute`, `*Builder`, `*Options` — суффиксы соблюдены.

## 4. План работ

Проект в стадии **alpha** — обратная совместимость не сохраняется. Все пункты выполняются **прямыми переименованиями на месте**, с одновременным обновлением кода, тестов, примеров и документации в одном изменении.

### Шаг 1 — инвентаризация использований
- Для каждого пункта #1–#26 собрать вхождения старого имени по всему репозиторию:
  `rg -n "\bOldName\b" src test benchmarks docs`.
- Составить таблицу «старое имя → новое имя → где обновить» (код, тесты, примеры, XML-доки, `docs/**` EN, `docs/ru/**`).
- **Критерий:** для каждого старого имени известны все места правки; ничего не «забыто заранее».

### Шаг 2 — переименование кода и тестов
- Переименовать типы и члены из #1–#26 на месте, например:
  `HashCode` → `XxHash32`; `IQueryProvider` → `IQueryRegistry`;
  `EntityP2..P8` → `JoinedEntityBuilder<T1,T2>..<T1..T8>`; `Projection.t1..t8` → `T1..T8`;
  `NORM_SQL` → `SqlFunctions`; `IEntityMeta`/`IPropertyMeta` → `IEntityMetadata`/`IPropertyMetadata`;
  `Param`/`ParamList`/`IParamProvider`/`DefaultParamProvider` → `Parameter`/`ParameterList`/`IParameterProvider`/`DefaultParameterProvider`;
  `PrepareException` → `QueryPreparationException`; `DbQueryCommandExtension` → `RawSqlOverride`;
  `DbContextBuilder` → `DataContextBuilder`; `IDataContextExtensions` → слить в `DataContextExtensions`.
- Перевести в `internal` детали реализации, протёкшие в public: `SqlBuilder` (#18), `Buffer3`/`Buffer10`/`ValueList`/`ValueList3` (#16), `Paging`/`Sorting` (#17), `TestSpecialMethodCallVisitor` (#15), `AnonymousClassEqualityComparer` (#25).
- Публичные поля заменить свойствами: `Paging` (#19), `Sorting` (#19), `DbQueryCommandExtension` (#6).
- Привести имена файлов к типам: `BasicHelpders.cs` → `BasicHelpers.cs`, `ISourceProvider.cs` → `IColumnsProvider.cs`, `TableAttribute.cs` → `SqlTableAttribute.cs`.
- Прогнать `rg` по каждому старому имени в `src`/`test`/`benchmarks`, добить хвосты.
- **Критерий:** `dotnet build nextorm.sln` — 0/0; `dotnet vstest test/nextorm.core.tests/bin/linux/Debug/net10.0/nextorm.core.tests.dll` — 111 passed; старых имён в коде не осталось.

### Шаг 3 — namespace (пункт #26)
- `nextorm.core` → `NextORM.Core`, `nextorm.<provider>` → `NextORM.<Provider>` — прямая замена `namespace`/`using` по всему решению (в alpha без type forwarders).
- **Критерий:** build 0/0 после массовой замены.

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

### Шаг 5 — закрепление
- Включить `CS1591` как warning (вместо `NoWarn`) для публичных проектов; `dotnet_diagnostic.CS1591.severity = warning` в `.editorconfig`.
- Добавить анализаторы именования (`CA1716`, `CA1724`, `CA1002`, `CA1051`) в `Directory.Build.props`.
- Перед 1.0 — API-approval тест, фиксирующий принятую поверхность.
- **Критерий:** build 0/0; новые нарушения именования валят сборку.

## 5. Что сделано в этом изменении

- Проведён аудит, отчёт сохранён в `API-NAMING-REVIEW.md`.
- Переименования **не выполнялись** — только план и отчёт, согласно задаче.
- Добавлены XML-`<summary>`/`<remarks>` (и `<param>`/`<typeparam>` где нужно) для публичных типов и членов, отмеченных в находках P0/P1 и части P2:

  | Файл | Задокументировано |
  |------|-------------------|
  | `Expressions/HashCode.cs` | `HashCode` |
  | `Query/IQueryProvider.cs` | `IQueryProvider` |
  | `Query/ISourceProvider.cs` | `IColumnsProvider` |
  | `Query/IParamProvider.cs` | `IParamProvider`, `DefaultParamProvider` |
  | `Query/NORM.cs` | `NORM`, `NORM.NORM_SQL` |
  | `Query/DbQueryCommandExtension.cs` | тип + поля |
  | `ParamList.cs` | `ParamList`, `Param` |
  | `BasicHelpders.cs` | `BasicHelpers`, `BasicHelpers.Var` |
  | `Builders/EntityBuilder.cs` | `EntityBuilder<TEntity>`, `EntityBuilder` |
  | `Builders/EntityExtensions.cs` | `EntityExtensions` |
  | `Builders/Joins/JoinCommandBuilder.cs` | `EntityP2`…`EntityP8` |
  | `Builders/Projection.cs` | `Projection<T1..T8>` |
  | `Builders/TableAlias.cs` | `TableAlias`, `TableColumn` |
  | `Builders/Paging.cs` | `Paging` |
  | `Expressions/Sorting.cs` | `Sorting` |
  | `DataContext/SqlBuilder.cs` | `SqlBuilder` |
  | `DataContext/ValueList.cs` | `Buffer10`, `ValueList`, `Buffer3`, `ValueList3`, `ListExtensions` |
  | `DataContext/{DataContextException,BuildSqlCommandException,PrepareException}.cs` | иерархия исключений |
  | `DataContext/Meta/{IEntityMeta,IPropertyMeta,EntityMetadataBuilder,EntityPropertyBuilder}.cs` | metadata-контракты |
  | `DataContext/Cache/*.cs` | `IPreparedQueryCommand`, `PreparedQueryCommand`, `DbPreparedQueryCommand`, `InMemoryCompiledQuery`, `QueryPlan` |
  | `DI/DataContextOptionsBuilder.cs` | `DbContextBuilder` |
  | `ExpressionExtensions.cs` | `ExpressionExtensions`, `TypeExpressionVisitor`, `TwoTypeExpressionVisitor`, `ReplaceConstantsExpressionVisitor`, `PredicateExpressionVisitor` |
  | `Visitors/*.cs` | `BaseExpressionVisitor`, `AliasFromProjectionVisitor`, `CorrelatedQueryExpressionVisitor`, `MemberExpressionVisitor`, `WhereExpressionVisitor`, `TestSpecialMethodCallVisitor`, `ParamExpressionVisitor2`, `ReplaceParameterVisitor`, `ReplaceConstantVisitor` |
  | `nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs` | `AnonymousClassEqualityComparer` (найденная MISNOMER: генератор назван «EqualityComparer») |

- Проверка: `dotnet build nextorm.sln` — 0 warnings / 0 errors; `dotnet vstest` — 111 passed; `dotnet docfx docs/docfx.json` — 0 warnings / 0 errors.
- Остальные недокументированные типы (приложение A) — бэклог Шага 4.


## Приложение A — публичные типы без XML-документации (109)

Полный машинный список (типы верхнего уровня и вложенные). Использовать как бэклог Шага 4.

```
nextorm.core.sourcegenerator/AnonymousClassEqualityComparer.cs:6   AnonymousClassEqualityComparer
nextorm.core/BasicHelpders.cs:2                                  BasicHelpers
nextorm.core/BuildSqlCommandException.cs:6                       BuildSqlCommandException
nextorm.core/Builders/EntityBuilder.cs:16                        EntityBuilder<TEntity>
nextorm.core/Builders/EntityBuilder.cs:531                       EntityBuilder
nextorm.core/Builders/EntityExtensions.cs:3                      EntityExtensions
nextorm.core/Builders/InMemoryCommandBuilder.cs:31               InMemoryCommandBuilderExtensions
nextorm.core/Builders/Joins/JoinCommandBuilder.cs:5,52,92,...    EntityP2..EntityP8
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
nextorm.core/DataContext/PrepareException.cs:6                   PrepareException
nextorm.core/DataContext/ResultSetEnumerator.cs:10               ResultSetEnumerator<>
nextorm.core/DataContext/SqlBuilder.cs:9                         SqlBuilder
nextorm.core/DataContext/ValueList.cs:7,11,74,78,141             Buffer10, ValueList, Buffer3, ValueList3, ListExtensions
nextorm.core/ExpressionCache.cs:5,10                             ExpressionCache<T>, ExpressionKey
nextorm.core/ExpressionExtensions.cs:7,31,51,81,148              ExpressionExtensions, TypeExpressionVisitor, TwoTypeExpressionVisitor, ReplaceConstantsExpressionVisitor, PredicateExpressionVisitor
nextorm.core/Expressions/FromExpression.cs:3                     FromExpression
nextorm.core/Expressions/FromExpressionPlanEqualityComparer.cs:4 FromExpressionPlanEqualityComparer
nextorm.core/Expressions/HashCode.cs:12                          HashCode
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
nextorm.core/Query/DbQueryCommandExtension.cs:3                  DbQueryCommandExtension
nextorm.core/Query/DefaultColumnsProvider.cs:7                   DefaultColumnsProvider
nextorm.core/Query/ExpressionPlanEqualityComparer.cs:11          ExpressionPlanEqualityComparer
nextorm.core/Query/IAliasProvider.cs:5                           IAliasProvider
nextorm.core/Query/IParamProvider.cs:5,9                         IParamProvider, DefaultParamProvider
nextorm.core/Query/IQueryProvider.cs:5                           IQueryProvider
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

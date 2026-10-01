# Дизайн: `Create...Builder` — единый префикс фабрик и отделение старта от исполнения

Дата: 2026-10-01. Статус: разговорный **ревизованный** дизайн СОГЛАСОВАН пользователем; обновлённая
письменная спека ожидает ревью; продуктовая реализация этим артефактом **не авторизована** и не
начата. Путь файла сохранён (`...create-command-builders-design.md`), несмотря на расширение на query.

Tracking: GitHub issue [#147](https://github.com/AlexeyShirshov/nextorm/issues/147), милстоун
`1.0.9-b` (создано 2026-10-01). Создание issue не авторизует продуктовую реализацию; ревью спеки
пользователем остаётся незакрытым, гейт ревью не меняется.

## 1. Намерение

Единообразно называть независимые публичные фабрики, стартующие построение команды. Две разные
траектории:

- **DML/batch**: 16 существующих независимых фабрик переименовываются **напрямую**, без алиасов.
- **Query**: существующие `From`/`FromSql`/`FromTableFunction` **не переименовываются, не удаляются
  и не помечаются `[Obsolete]`**; рядом **добавляются** 11 публичных forwarder'ов с новым именем.

Цель — читаемость: `ctx.CreateInsertBuilder(...)` явно сообщает «создаю построитель», тогда как
`ctx.Update(entity)` / `ctx.UpdateAsync(...)` явно исполняют команду. Обе query-формы записи
поддерживаются одинаково; миграция существующих query-вызовов/примеров не требуется.

## 2. Принятое правило именования (ревизия)

- **DML/batch**: фабрика называется `Create<Имя>Builder` — `<Имя>` совпадает с именем возвращаемого
  построителя без суффикса `Builder`; старое имя заменяется на месте, без алиасов и `[Obsolete]`.
- **Query**: новое имя — `CreateQueryBuilder` (9 перегрузок, зеркало `From`),
  `CreateQueryBuilderFromSql` (зеркало `FromSql`), `CreateQueryBuilderFromTableFunction<T>`
  (зеркало `FromTableFunction<T>`). Оригиналы остаются без изменений.
- **Типы не переименовываются** нигде: `EntityBuilder<T>`, `FromOptions`, `TableAlias` и прочие
  неизменны; `cte.From` и все fluent-методы построителей не трогаются.
- Политика «без алиасов» ограничена **DML/batch**; для query добавление параллельных имён —
  осознанно принятое исключение.

## 3. Точная матрица DML/batch — 16 переименований

Все 16 — публичные `static` extension-члены на `NextORM.Core.DataContextExtensions`, кроме #16
(`NextORM.Core.BatchExtensions`). Переименование прямое, алиасов нет.

| # | Текущее имя | Новое имя | Возвращаемый тип (без изменений) | Receiver (без изменений) |
|---|---|---|---|---|
| 1 | `InsertInto<TEntity>` | `CreateInsertBuilder<TEntity>` | `InsertBuilder<TEntity>` | `IDataContext` |
| 2–4 | `BulkInsertInto<TEntity>` (все 3 перегрузки) | `CreateBulkInsertBuilder<TEntity>` | `BulkInsertBuilder<TEntity>` | `IDataContext` |
| 5 | `DeleteFrom<TEntity>` | `CreateDeleteBuilder<TEntity>` | `DeleteBuilder<TEntity>` | `IDataContext` |
| 6 | `Update<TEntity>(IDataContext, Action<EntityMetadataBuilder<TEntity>>? cfg)` — **только builder-перегрузка** | `CreateUpdateBuilder<TEntity>` | `UpdateBuilder<TEntity>` | `IDataContext` |
| 7 | `MergeInto<TEntity>` | `CreateMergeBuilder<TEntity>` | `MergeBuilder<TEntity>` | `IDataContext` |
| 8 | `Truncate<TEntity>` | `CreateTruncateBuilder<TEntity>` | `TruncateBuilder<TEntity>` | `IDataContext` |
| 9–15 | `UpdateJoin<T1..Tn>` (арности 2..8, все 7) | `CreateUpdateJoinBuilder<...>` | `UpdateJoinBuilder<...>` | `JoinedEntityBuilder<T1..Tn>` |
| 16 | `Batch` (`NextORM.Core.BatchExtensions.Batch`) | `CreateBatchBuilder` | `BatchBuilder` | `IDataContext` |

Итого DML/batch: 1 + 3 + 1 + 1 + 1 + 1 + 7 + 1 = **16 переименований**.
Для `UpdateJoin` receiver остаётся `JoinedEntityBuilder<T1..Tn>` — метод не переезжает в контекст.
Новые имена сохраняют generic-арность исходного члена.

## 4. Query API — 11 аддитивных forwarder'ов

Все — новые публичные `static` extension-члены на `NextORM.Core.DataContextExtensions`, добавленные
**рядом** с существующими оригиналами. Каждый зеркалит соответствующий оригинал: **тот же receiver,
generic-ограничения, имена/порядок/типы параметров, значения по умолчанию и тип возврата**.
Ограничения не изобретаются — они зеркалятся; implementer подтверждает сигнатуры через Roslyn.

| # | Оригинал (source:line) | Новое имя | Возвращаемый тип (тот же) |
|---|---|---|---|
| Q1 | `From<T>(IDataContext, Action<EntityMetadataBuilder<T>>? configEntity = null)` :1149 | `CreateQueryBuilder<T>` | `EntityBuilder<T>` |
| Q2 | `From<T>(IDataContext, Action<FromOptions> options, Action<EntityMetadataBuilder<T>>? configEntity = null)` :1168 | `CreateQueryBuilder<T>` | `EntityBuilder<T>` |
| Q3 | `From(IDataContext, string table)` :1323 | `CreateQueryBuilder` | `EntityBuilder<TableAlias>` |
| Q4 | `From(IDataContext, string table, Action<FromOptions> options)` :1335 | `CreateQueryBuilder` | `EntityBuilder<TableAlias>` |
| Q5 | `From<TResult>(IDataContext, QueryCommand<TResult> query)` :1386 | `CreateQueryBuilder<TResult>` | `EntityBuilder<TResult>` |
| Q6 | `From<TResult>(IDataContext, QueryCommand<TResult> query, Action<FromOptions> options)` :1399 | `CreateQueryBuilder<TResult>` | `EntityBuilder<TResult>` |
| Q7 | `From<TResult>(IDataContext, TempTableSource<TResult> tempTable)` :1430 | `CreateQueryBuilder<TResult>` | `EntityBuilder<TableAlias>` |
| Q8 | `From<TResult>(IDataContext, EntityBuilder<TResult> builder)` :1450 | `CreateQueryBuilder<TResult>` | `EntityBuilder<TResult>` |
| Q9 | `From<TResult>(IDataContext, EntityBuilder<TResult> builder, Action<FromOptions> options)` :1463 | `CreateQueryBuilder<TResult>` | `EntityBuilder<TResult>` |
| Q10 | `FromSql(IDataContext, string sql, object? parameters = null)` :1369 | `CreateQueryBuilderFromSql` | `EntityBuilder<TableAlias>` |
| Q11 | `FromTableFunction<T>(IDataContext, Expression<Func<IQueryable<T>>> call)` :1485 | `CreateQueryBuilderFromTableFunction<T>` | `EntityBuilder<T>` |

Итого query: 9 (`From`) + 1 (`FromSql`) + 1 (`FromTableFunction`) = **11 новых объявлений**.
**Всего изменений поверхности: 16 переименований + 11 добавлений = 27.**

## 5. Исключения и область

- DML/batch **не** переименовываются: исполняющие `Update(entity)->int`, `UpdateAsync`,
  `Delete(entity)`, `DeleteAsync`, joined `Delete`/`DeleteAsync`; builder-терминалы
  `Insert`/`Update`/`Delete`/`Merge`/`Truncate`/`Execute`.
- Стадийные переходы `Returning`/`ReturningKey`/`ReturningIdentity`,
  `Using`/`On`/`OnKeys`/`WhenMatched`/`WhenNotMatched`/`WhenNotMatchedBySource` — без изменений.
- Накопление батча `batch.CreateTable`/`CreateTempTable`/`Insert`/`Update`/`Delete`/`Truncate`/
  `Raw`/`Query`/`AddQuery`; `AsTempTable`; `ToTable`/`ToTempTable`; существующий `CreateCommand<T>`;
  все внутренние/приватные фабрики.
- Query-оригиналы `From`/`FromSql`/`FromTableFunction` **явно сохраняются** в текущем виде: не
  удаляются, не переименовываются, не помечаются `[Obsolete]`. Они — цель делегирования, а не
  объект ренейма. **В область входят** новые аддитивные query-forwarder'ы (`CreateQueryBuilder`,
  `CreateQueryBuilderFromSql`, `CreateQueryBuilderFromTableFunction<T>`) — это не противоречие:
  ренейм query-оригиналов исключён, добавление параллельных имён включено.
- `Select`/`Join`/`With`/`WithRecursive` — без изменений. `cte.From` и fluent-методы не трогаются.
- Никаких переименований типов; никаких «тип `QueryBuilder`» — такого типа в спеке нет.

## 6. Совместимость

- **DML/batch**: alpha-политика (`API-NAMING-REVIEW.md:66,80`) — обратная совместимость не
  поддерживается, имена заменяются на месте, без алиасов и `[Obsolete]`. Это **намеренно принятое**
  пользователем ломающее изменение исходной и бинарной совместимости 16 фабрик; не подавать как
  «универсально неломающее».
- **Query**: изменение **аддитивное и неломающее** — старые `From`/`FromSql`/`FromTableFunction`
  остаются валидными без `[Obsolete]`, миграция callsite'ов/примеров не требуется. Обе формы записи
  равноправны. Утверждение «без алиасов» к query **не** применяется.
- Заморозка/инструментовка API-freeze не входит в спеку и здесь не делается.

## 7. Неизменное поведение

- DML/batch: тела, порядок/типы параметров, дефолты, generic-ограничения, возвраты, receiver,
  валидация и жизненный цикл — без изменений; фабрика создаёт тот же построитель и не исполняет БД.
- Query-forwarder'ы: каждый **делегирует ровно один раз** соответствующей существующей перегрузке
  `From`/`FromSql`/`FromTableFunction`. Оригиналы сохраняют текущую реализацию. Нет рекурсии, нет
  независимого/скопированного создания построителя. Сохраняются: единственный раз побочные эффекты
  конфигурации метаданных, поведение `FromOptions`, null-валидация, логирование, SQL, иммутабельность
  и провайдерное поведение — как у оригинала.
- Кэш, транзакции и производительность от намерения не зависят; проверяется постольку, поскольку
  поведенческих правок нет.

## 8. Ограничения реализации

- DML-ренейм — **только через Roslyn**, сигнатурно-селективно. Scout: dry-run `roslyn rename` по
  полному члену `Update` разрешает группу по имени и может затронуть исполняющие перегрузки; поэтому
  групповой `apply` для `Update` без доказанного ограничения недопустим. Для
  `BulkInsertInto`/`UpdateJoin` групповой ренейм предполагается корректным, но проверяется.
  Если безопасно нацелить только builder-перегрузку `Update` нельзя — **STOP/escalate**, не догадка.
- Запрещены `sed`/замена вызовов по тексту и сценарий «переименовать всё, затем вслепую
  восстановить». Текстовые правки — только документация.
- Query-forwarder'ы добавляются по сигнатурам оригиналов; implementer подтверждает зеркальность
  (receiver, ограничения, параметры, возврат) через Roslyn. Никаких скопированных тел.
- Обновить через Roslyn все `.cs`-ссылки на переименованные DML-фабрики и все затронутые
  код/тесты/примеры; XML `cref`; DML-доки EN+RU; xrefs/curated API; релевантный реестр именования.
  Query-оригиналы в доках сохраняют валидные ссылки/имена; добавить новые XML-доки/xrefs/строки
  reference-листинга для 11 новых имён; доки EN+RU показывают обе query-формы как намеренные.
- Исторические завершённые спеки/логи массово не переписываются. Никаких публичных ссылок на
  внутренние спеки; артефакт внутренний.
- Сохранить несвязанные изменения рабочего дерева пользователя (guide 15 в обоих локалях, dialect,
  `SQLBuilder`, PostgresDialect/тесты, файлы CTE issue 144). Без новых подавлений предупреждений и
  отключённых тестов. CRLF. Без коммитов/push/merge.

## 9. Верификация и приёмка

- Семантический инвентарь API: **16 DML/batch** объявлений под новыми именами, старых публичных
  входных DML-фабрик нет (алиасов у DML нет); `Update(entity)`/`UpdateAsync` сохранили имена.
- **11 query**-объявлений добавлены; **все оригинальные сигнатуры** `From`/`FromSql`/
  `FromTableFunction` остаются, без `[Obsolete]`.
- Сборка с warnings-as-errors на .NET 10; компиляция/использование: все 16 новых DML-имён (включая
  joined арности 2..8) **и** все 11 новых query-перегрузок **вместе** со старыми query-формами;
  `FromSql` с параметрами; трансляция table function.
- Парные тесты old/new для query: одинаковые SQL, результаты, типы, поведение `FromOptions` и число
  регистраций колбэка конфигурации метаданных = **ровно один раз** (где выполнимо).
- Существующие тесты, в т.ч. прямые `Update(entity)` sync/async, batch, bulk/merge/truncate —
  остаются.
- Публичные примеры/тексты/xrefs EN+RU точны; нет ссылок на устаревшие DML-фабрики вне намеренно
  исторического текста/SQL; старые query-ссылки/имена валидны, обе query-формы показаны.
- Сборка DocFX. Бенчмарк не требуется: DML — именующее изменение, query — чистые
  прямые делегирующие forwarder'ы; исключение только при появлении поведенческих/перф-правок.
- Регрессионные провайдерные наборы; реальная контейнерная интеграция при запуске — через скилл
  `running-integration-tests` + `DOCKER_HOST`; прогон со `skipped`-провайдерами не объявлять зелёным.
- Финальный дифф мерить относительно снапшота, а не одного `HEAD` (учесть предсуществующие
  изменения); несвязанных модификаций нет.

## 10. Отвергнутые альтернативы

- Оставить только SQL-имена для DML — менее ясно, где фабрика, а где исполнение.
- Существительные-методы вида `*Builder` — менее естественно для .NET.
- **DML**: алиасы/дублирующие члены — дублируют поверхность, противоречат alpha-политике; для DML
  отклонены.
- **Query**: удаление/ренейм/`[Obsolete]` старых `From`/`FromSql`/`FromTableFunction` и массовая
  миграция всех callsite'ов — отклонены; вместо этого аддитивные forwarder'ы.
- «Универсальное» правило без алиасов — отклонено: политика без алиасов ограничена DML, для query
  параллельные имена приняты осознанно.

## 11. Статус и гейт ревью пользователя

- Разговорный ревизованный дизайн: **APPROVED** пользователем.
- Обновлённая письменная спека: **ожидает ревью пользователя**.
- Реализация: **НЕ авторизована и НЕ начата** до одобрения ревизованной спеки и последующего
  письменного плана с выбранным способом исполнения.

## 12. Передача (handoff)

- Следующий шаг: пользователь ревьюит ревизованную спеку; затем письменный план реализации с
  проверкой сигнатурной выборочности Roslyn для `Update` и с зеркальной проверкой 11 query-сигнатур.
- Не начинать правки кода до этих двух одобрений. Отдельный новый spec/plan-артефакт не создаётся.
- Tracking: GitHub issue [#147](https://github.com/AlexeyShirshov/nextorm/issues/147) (милстоун
  `1.0.9-b`, создано 2026-10-01). Issue не авторизует реализацию и не закрывает задачу; статус гейта
  из раздела 11 не меняется.

## Источники фактов

DML/batch: `src/nextorm.core/DataContext/DataContextExtensions.cs:74,92,113,134,157,175,193,212,362,745..832,1137`;
`src/nextorm.core/Builders/BatchBuilder.cs:37`. Query: `DataContextExtensions.cs:1149,1168,1323,1335,1369,1386,1399,1430,1450,1463,1485`.
`docs/advanced/api-reference.md:48..62` EN/RU; `docs/specs/design/API-NAMING-REVIEW.md:66,80`;
разведочная задача `ses_f07f99aa7ffeMFf4LADkBy0IWG` (продолжение) — доказательства в разговоре.

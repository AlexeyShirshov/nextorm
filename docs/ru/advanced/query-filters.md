# Глобальные фильтры запросов

> Фильтр запроса — это предикат, привязанный к типу сущности в метаданных отображения; nextorm добавляет его через `and` в каждый запрос, где сущность участвует — основной `FROM`, источники соединений, подзапросы и жадно загружаемые дочерние записи — если только запрос не вызывает `IgnoreFilters()`.

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Обзор провайдеров](../providers/overview.md) · [Фильтрация (`Where`)](../guide/01-filtering-where.md)

## Обзор

Глобальные фильтры запросов привязывают повторяющийся предикат — типовые случаи: soft-delete и multi-tenancy — к типу сущности, вместо того чтобы повторять его в каждом `Where`. Фильтр может читать исполняющий [`IDataContext`](xref:NextORM.Core.IDataContext), поэтому идентификатор тенанта или флаг soft-delete определяется для каждого запроса, а сгенерированный план остаётся кэшированным и разделяется между контекстами.

Это аналог глобальных фильтров запросов EF Core и query filters linq2db.

## Объявление фильтра

### Fluent

Объявите фильтр там, где настраивается отображение сущности через `From<T>`. Объявление выполняется один раз, при первом построении метаданных типа, поэтому условие становится частью отображения, а не одного запроса:

> В примерах на этой странице используется сущность `Document`, отображённая на `documents(id, tenant_id, is_deleted)`, и сущность `Attachment`, отображённая на `attachments(id, document_id, is_deleted)`. `Document` объявляет фильтр soft-delete с ключом `"soft-delete"` и фильтр тенанта с ключом `"tenant"`, читающий идентификатор тенанта из `c.Properties["tenant"]`; `Attachment` объявляет анонимный фильтр soft-delete; в примерах записи используется сущность `ArchivedDocument`, отображённая на `archived_documents(id, tenant_id, is_deleted)`, как цель `INSERT … SELECT`.

```csharp
ctx.From<Document>(m => m.HasQueryFilter(d => !d.IsDeleted));
```

Перегрузка с контекстом получает исполняющий [`IDataContext`](xref:NextORM.Core.IDataContext) вторым параметром, поэтому фильтр может читать состояние контекста:

```csharp
ctx.From<Document>(m => m.HasQueryFilter((d, c) => d.TenantId == (int)c.Properties["tenant"]));
```

[`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) предоставляет анонимные перегрузки и перегрузку с ключом:

```csharp
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(string filterKey, Expression<Func<T, IDataContext, bool>>? filter);
```

Повторный анонимный вызов добавляет ещё один предикат; все анонимные фильтры типа объединяются через `and`.

### Именованные (keyed) фильтры

Задайте фильтру **ключ**, чтобы его можно было точечно отключить через `IgnoreFilters`:

```csharp
ctx.From<Document>(m => m
    .HasQueryFilter("soft-delete", (d, c) => !d.IsDeleted)
    .HasQueryFilter("tenant", (d, c) => d.TenantId == (int)c.Properties["tenant"]));
```

Оба предиката затем добавляются через `and` в каждое чтение `Document`; значение тенанта — связанный параметр, а не встроенный литерал:

```csharp
var ids = ctx.From<Document>()
    .Where(d => d.Id == 10)
    .Select(d => d.Id)
    .ToList();
```

```sql
-- PostgreSQL
select id from documents
 where ((id = 10 and not (is_deleted)) and tenant_id = @p0)

-- SQL Server
select id from documents
 where ((id = 10 and not ((is_deleted) = 1)) and tenant_id = @p0)
```

Именованный фильтр занимает **слот**: повторный вызов с тем же ключом **заменяет** прежний фильтр, а передача `null` в `filter` **удаляет** слот. Анонимные фильтры (объявленные без ключа) аддитивны — несколько объединяются через `and` — и доступны под ключом [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey), то есть под пустой строкой `""`.

Повтор одного и того же ключа разрешается детерминированно: сначала fluent-вызовы (в порядке вызова), затем атрибуты `[QueryFilter]` от базового типа к самому производному, поэтому атрибут производного типа переопределяет атрибут базового, а атрибут переопределяет fluent-фильтр с тем же ключом. Повтор ключа **внутри одного типа** — отдельный случай: порядок атрибутов не гарантируется средой выполнения, поэтому два атрибута `[QueryFilter]` с одним непустым `FilterKey` на одном типе не имеют детерминированного победителя и **отвергаются** при построении метаданных. Объявите ключ один раз или переопределите его в производном типе. Анонимные фильтры аддитивны и могут повторяться. Ключи сравниваются ординально (с учётом регистра); `null`, пустой ключ или ключ из пробелов объявляет анонимный фильтр, а не слот.

### Атрибут

Те же фильтры можно объявить декларативно с помощью [`[QueryFilter]`](xref:NextORM.Core.QueryFilterAttribute) на классе или интерфейсе. `FilterLambda` называет **статический член** атрибутируемого типа — поле, свойство или метод без параметров — возвращающий лямбду фильтра:

```csharp
[QueryFilter(FilterLambda = nameof(Active))]
public sealed class Document
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }

    public static Expression<Func<Document, bool>> Active() => d => !d.IsDeleted;
}
```

Именованный член может также возвращать предикат, учитывающий контекст:

```csharp
[QueryFilter(FilterLambda = nameof(ForTenant))]
public sealed class Document
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public static Expression<Func<Document, IDataContext, bool>> ForTenant()
        => (d, c) => d.TenantId == (int)c.Properties["tenant"];
}
```

Задайте `FilterKey`, чтобы дать фильтру-атрибуту имя и сделать его адресуемым:

```csharp
[QueryFilter(FilterKey = "soft-delete", FilterLambda = nameof(Active))]
[QueryFilter(FilterKey = "tenant", FilterLambda = nameof(ForTenant))]
public sealed class Document { /* ... */ }
```

С ключом действуют те же правила слотов: повторение ключа заменяет прежний фильтр, а `FilterLambda = null` удаляет слот, унаследованный от базового типа. Атрибут без `FilterKey` и без `FilterLambda` некорректен и бросает исключение при построении метаданных.

`AllowMultiple = true` означает, что на один тип можно поместить несколько атрибутов `[QueryFilter]`; анонимные атрибуты объединяются через `and` так же, как повторные fluent-вызовы.

## Чтение исполняющего контекста

Фильтр, чья лямбда принимает `IDataContext`, читает значение из контекста во время выполнения:

```csharp
public static Expression<Func<Document, IDataContext, bool>> ForTenant()
    => (d, c) => d.TenantId == (int)c.Properties["tenant"];
```

Значение тенанта не встраивается в SQL. Оно становится **runtime-параметром**, а идентичность фильтра (а не его текущие значения) входит в ключ плана, поэтому:

- два контекста с разными значениями тенанта могут разделять один кэшированный план;
- каждое выполнение читает **своё** значение контекста, даже при попадании в кэш плана.

Тот же предикат компилируется и перечитывает контекст при каждом вызове в провайдере in-memory.

## Отключение фильтров для одного запроса

[`IgnoreFilters`](xref:NextORM.Core.EntityBuilder`1.IgnoreFilters) возвращает копию билдера с отключёнными выбранными фильтрами для этого запроса. Есть четыре перегрузки:

```csharp
public EntityBuilder<TEntity> IgnoreFilters();
public EntityBuilder<TEntity> IgnoreFilters(params Type[] entityTypes);
public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys);
public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes);
```

Их семантика:

| Вызов | Что отключает |
|---|---|
| `IgnoreFilters()` | все фильтры всех типов сущностей |
| `IgnoreFilters(typeof(A), typeof(B))` | все фильтры (любой ключ, включая анонимные), объявленные для `A` и `B` |
| `IgnoreFilters(["soft-delete"])` | фильтр с ключом `soft-delete` для всех типов сущностей |
| `IgnoreFilters(["soft-delete"], typeof(A))` | фильтр с ключом `soft-delete` только для `A` (пересечение ключа и типа) |

Отсюда следуют правила:

- **Только типы** отключают **все фильтры** этих типов сущностей (и анонимные, и именованные).
- **Только ключи** отключают **эти ключи для всех типов сущностей**.
- **Ключи и типы** отключают **пересечение** — только перечисленные ключи на перечисленных типах.
- **Пустой или `null` список ключей или типов — no-op**: ничего не отключается. Список ключей является «воротами», поэтому пустой список ключей не отключает ничего, даже если типы сущностей заданы (как в EF Core).
- [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey) (`""`) адресует анонимные фильтры, например `IgnoreFilters([QueryFilters.AnonymousKey])`.

Пример: отключаем только фильтр soft-delete, сохраняя ограничение по тенанту:

```csharp
var deleted = ctx.From<Document>()
    .IgnoreFilters(["soft-delete"])
    .Where(d => d.IsDeleted)
    .Select(d => d.Id)
    .ToList();
```

Фильтр тенанта всё ещё применяется; отброшен только предикат soft-delete:

```sql
-- PostgreSQL
select id from documents
 where (id = 10 and tenant_id = @p0)

-- SQL Server
select id from documents
 where (id = 10 and tenant_id = @p0)
```

Исходный билдер не меняется; фильтры игнорирует только возвращённая копия. **Повторные вызовы накапливаются (объединение), а не заменяют друг друга**: `IgnoreFilters(["a"]).IgnoreFilters(["b"])` отключает и `a`, и `b`, причём порядок вызовов и дубликаты ключей значения не имеют. Селекторы «только типы» и «только ключи» тоже накапливаются независимо, а один вызов `IgnoreFilters(keys, types)` — это **пересечение**: каждый его ключ применяется только к перечисленным типам. Такой комбинированный вызов никогда не сворачивается в объединение с другим селектором. Пустая область (пустой или `null` список ключей/типов) не отключает ничего, а all-or-nothing вызов `IgnoreFilters()` доминирует над любой селективной областью, объединённой с ним.

Когда команда запроса уже построена, [`QueryCommand.IgnoreFilters`](xref:NextORM.Core.QueryCommand.IgnoreFilters) возвращает результат чтения: `true`, когда отключён **любой** фильтр — и через all-or-nothing форму, и через селективную область, — и `false`, когда не отключён ни один. Его сеттер работает по принципу all-or-nothing (`true` отключает все фильтры, `false` очищает область), потому что `bool` не может выразить селективное отключение; авторитетное состояние — сама селективная область.

## Область применения: основной источник, соединения и подзапросы

Фильтр привязан к типу сущности и подмешивается везде, где этот тип встречается в форме запроса:

| Позиция | Куда подмешивается |
|---|---|
| Основной `FROM` | в `WHERE` инструкции, в сочетании с явным `Where` |
| Источник соединения | в условие `ON` соединения для присоединяемой сущности |
| Подзапрос | в подзапрос при его подготовке |
| Цель мутации (`UPDATE` / `DELETE`) | в `WHERE` инструкции, в сочетании с предикатом или равенством по ключу |
| Цель `MERGE` (SQL Server, PostgreSQL) | в условие `MERGE ... ON` и в каждую ветку `WHEN NOT MATCHED BY SOURCE` (см. [Изоляция цели записи](#изоляция-цели-записи)) |

Например, фильтр присоединяемой сущности подмешивается в `ON` этого соединения, а фильтр основного источника остаётся в `WHERE`:

```csharp
var rows = ctx.From<Attachment>()
    .Join(ctx.From<Document>(), (a, d) => a.DocumentId == d.Id)
    .Select(p => p.Item1.Id)
    .ToList();
```

```sql
-- PostgreSQL
select t1.id from attachments as "t1"
 join documents as "t2" on ((t1.document_id = t2.id and not (t2.is_deleted)) and t2.tenant_id = @p0)
 where not (t1.is_deleted)

-- SQL Server
select t1.id from attachments as [t1]
 join documents as [t2] on ((t1.document_id = t2.id and not ((t2.is_deleted) = 1)) and t2.tenant_id = @p0)
 where not ((t1.is_deleted) = 1)
```

Подзапрос, подготавливаемый в составе внешнего запроса, фильтруется так же:

```csharp
var rows = ctx.From<Document>()
    .Select(d => new { d.Id, sid = ctx.From<Attachment>()
        .Where(a => a.DocumentId == d.Id)
        .Select(a => a.Id)
        .First() })
    .ToList();
```

```sql
-- PostgreSQL
select t1.id, (select t2.id from attachments as "t2"
 where (t2.document_id = t1.id and not (t2.is_deleted))
limit 1) as "sid" from documents as "t1"
 where (not (t1.is_deleted) and t1.tenant_id = @p0)

-- SQL Server
select t1.id, (select top(1) t2.id from attachments as [t2]
 where (t2.document_id = t1.id and not ((t2.is_deleted) = 1))) as [sid] from documents as [t1]
 where (not ((t1.is_deleted) = 1) and t1.tenant_id = @p0)
```

Основной источник соединения — первая таблица, поэтому фильтр, объявленный для `T1`, применяется к `Item1` так же, как в обычном запросе.

Область отключения несёт тот запрос, который её начинает, поэтому тип, указанный в селективном `IgnoreFilters`, отключается везде, где он встречается в этом запросе, а `IgnoreFilters()` отключает все фильтры во всём запросе — включая жадно загружаемые дочерние записи, объявленные через [`LoadWith`](eager-loading.md). Это одинаково в split- и single-query (`EagerLoadMode.SingleQuery`) режимах: эффективная область дочерней стороны — это **объединение** её собственной области `IgnoreFilters` и родительской, `IgnoreFilters()` (`All`) поглощает это объединение, а селективная область ребёнка отключает фильтры только у этого ребёнка. См. [Eager loading](eager-loading.md#режим-single-query-eagerloadmodesinglequery).

## UPDATE и DELETE (DML)

Фильтр, объявленный для целевой сущности, применяется к мутации этой сущности так же, как к чтению:

| Инструкция | Фильтр |
|---|---|
| `CreateUpdateBuilder<T>().Where(...).Update()` | подмешивается в `WHERE` через `and` вместе с предикатом |
| `Update(entity)` (key-форма) | `WHERE <pk> = @p and <filter>` — строка, отсечённая фильтром, не обновляется |
| `CreateDeleteBuilder<T>().Where(...).Delete()` | подмешивается в `WHERE` через `and` вместе с предикатом |
| `Delete(entity)` (key-форма) | `WHERE <pk> = @p and <filter>` — строка, отсечённая фильтром, не удаляется |
| `CreateUpdateBuilder<T>().Set(...).Update()` без `Where` | обновляются все **отфильтрованные** строки (фильтр всё равно применяется) |
| `CreateDeleteBuilder<T>().All()` | явное удаление всей таблицы: фильтр **не** применяется |
| `CreateUpdateJoinBuilder(...)` / join-`Delete()` | цель (первая таблица) и все источники соединений фильтруются |

Для key-форм фильтр добавляется через `and` к равенству по ключу, поэтому `ctx.Delete(entity)` вернёт `0`, если фильтр отсекает этот ключ. Фильтр подмешивается в подготовленное условие мутации, поэтому отображаемый SQL отражает действующий набор фильтров. В отличие от чтения мутация не подготавливается и не кэшируется по плану, поэтому ключа плана, в котором фильтр мог бы участвовать, здесь нет.

[`UpdateBuilder<T>`](xref:NextORM.Core.UpdateBuilder`1), [`DeleteBuilder<T>`](xref:NextORM.Core.DeleteBuilder`1) и [`UpdateJoinBuilder<TProjection>`](xref:NextORM.Core.UpdateJoinBuilder`1) предоставляют те же четыре перегрузки `IgnoreFilters`, что и билдер чтения, с той же семантикой (только типы, только ключи, пересечение ключа и типа, пустой список — no-op). В отличие от билдера чтения, возвращающего копию, DML-билдер хранит состояние: вызов применяется к строящейся инструкции, а повторные вызовы накапливаются:

```csharp
// Отключаем только фильтр soft-delete; фильтр тенанта продолжает действовать.
ctx.CreateDeleteBuilder<Document>()
    .IgnoreFilters(["soft-delete"])
    .Where(d => d.IsDeleted)
    .Delete();

// Отключаем все фильтры цели, затем обновляем только указанный столбец.
ctx.CreateUpdateBuilder<Document>()
    .IgnoreFilters()
    .Set(d => d.Archived, true)
    .Update();
```

Эти правила видны в отрендеренной инструкции — активный фильтр и предикат делят один `WHERE`, а `IgnoreFilters(["soft-delete"])` убирает только часть soft-delete:

```sql
-- PostgreSQL
update documents set is_deleted = @p0 where ((id = 10 and not (is_deleted)) and tenant_id = @p1)

delete from documents where (is_deleted and tenant_id = @p0)

-- SQL Server
update documents set is_deleted = @p0 where ((id = 10 and not ((is_deleted) = 1)) and tenant_id = @p1)

delete from documents where ((is_deleted) = 1 and tenant_id = @p0)
```

`INSERT` / `MERGE` не подмешивают фильтр цели через `FROM` источника (у цели нет `FROM`); при активном фильтре полный `MERGE` ограничивает цель внутри инструкции, а формы key upsert, которые этого не могут, **отказывают** (см. [Изоляция цели записи](#изоляция-цели-записи)). `INSERT … SELECT` фильтрует свой источник как обычное чтение — а записываемые строки при этом проходят проверку (см. [INSERT и MERGE (проверка)](#insert-и-merge-проверка)).

## INSERT и MERGE (проверка)

Ни `INSERT`, ни `MERGE` не подмешивают фильтр цели через `FROM` источника — у цели нет `FROM` — поэтому запись никогда не ограничивается молча. **Полный `MERGE`** (и key upsert SQL Server, рендерящийся той же формой) вместо этого ограничивает цель атомарно внутри инструкции, а key upsert `ON CONFLICT` / `ON DUPLICATE KEY` / in-memory — который не может нести предикат — **отказывает** (см. [Изоляция цели записи](#изоляция-цели-записи)). Независимо от этого, значения, которые предстоит записать, проверяются на соответствие активным фильтрам целевой сущности (за вычетом области `IgnoreFilters`) **до** выполнения инструкции; нарушение бросает [`QueryFilterException`](xref:NextORM.Core.QueryFilterException) (наследник [`DataContextException`](xref:NextORM.Core.DataContextException)).

| Инструкция | Что проверяется |
|---|---|
| `CreateInsertBuilder<T>().Value(...)` / `Values(entity)` / пакетный `Values(...)` | каждая записываемая строка против фильтров цели |
| `CreateBulkInsertBuilder<T>()` | каждая строка источника |
| `CreateMergeBuilder<T>().Using(...)` | строки-источники **каждой** ветки `MERGE` — insert, update-only и delete-only |
| `CreateInsertBuilder<T>().Values(source, mapping)` (`INSERT … SELECT`) | серверный pre-check строк источника |
| `CreateMergeBuilder<T>().Using(query)` (`MERGE` из запроса) | серверный pre-check строк источника |

`INSERT` никогда не несёт фильтр цели; `INSERT … SELECT` фильтрует только свой источник:

```csharp
// Цель INSERT никогда не фильтруется; записываемая строка вместо этого проверяется.
ctx.CreateInsertBuilder<Document>()
    .Values(new Document { Id = 1, TenantId = 1, IsDeleted = false })
    .Insert();

// INSERT … SELECT фильтрует свой источник как чтение и предварительно проверяет записываемые строки.
ctx.CreateInsertBuilder<ArchivedDocument>()
    .Values(ctx.From<Document>().Where(d => d.Id > 0), d => new { d.Id, d.TenantId, d.IsDeleted })
    .Insert();
```

```sql
-- PostgreSQL
insert into documents (id, tenant_id, is_deleted) values (@p0, @p1, @p2)

insert into archived_documents (id, tenant_id, is_deleted)
select id, tenant_id as "TenantId", is_deleted as "IsDeleted" from documents
 where (((id > 0) and not (is_deleted)) and tenant_id = @p0)

-- SQL Server
insert into documents (id, tenant_id, is_deleted) values (@p0, @p1, @p2)

insert into archived_documents (id, tenant_id, is_deleted)
select id, tenant_id as [TenantId], is_deleted as [IsDeleted] from documents
 where (((id > 0) and not ((is_deleted) = 1)) and tenant_id = @p0)
```

На поддерживаемой форме полного `MERGE` проверка значений источника покрывает **каждую комбинацию веток** — `MERGE` только с update- или только с delete-веткой проверяет входящие строки источника так же, как вставляющий, — и не проходящая проверку строка источника бросает [`QueryFilterException`](xref:NextORM.Core.QueryFilterException) до любой мутации, даже если инструкция не вставляла бы строку.

Для материализованной сущности фильтр вычисляется прямо по ней. В колоночных/значениевых формах значение несут только записываемые колонки, поэтому проверка работает по принципу **fail-closed**: если активный фильтр читает колонку, которую инструкция **не** записывает, запись **отклоняется** с [`QueryFilterException`](xref:NextORM.Core.QueryFilterException), а не пропускается — значение по умолчанию в базе может как удовлетворить фильтр, так и нарушить его, и nextorm не угадывает. Запишите колонку явно или отключите фильтр для инструкции через `IgnoreFilters`.

`INSERT … SELECT` и `MERGE` из запроса проверяются отдельным запросом существования по источнику (в поиске строки, которую отклоняет фильтр цели). Это защита от гонки **TOCTOU**, а не транзакционная: между проверкой и записью параллельный писатель всё ещё может изменить строки, и проверка **не** делает запись атомарной. Оборачивайте и то, и другое в одну транзакцию, когда гонка важна.

Пакетная и bulk-запись тоже не атомарны. Синхронный bulk-источник проверяется целиком до отправки первого батча; **асинхронный** bulk-источник проверяется построчно по мере потока, поэтому при нарушении в поздней строке ранее записанные строки остаются.

`IgnoreFilters` на [`InsertBuilder<T>`](xref:NextORM.Core.InsertBuilder`1), [`BulkInsertBuilder<T>`](xref:NextORM.Core.BulkInsertBuilder`1) и [`MergeBuilder<T>`](xref:NextORM.Core.MergeBuilder`1) отключает соответствующие фильтры и от проверки, с теми же четырьмя перегрузками и семантикой, что и у билдера чтения:

```csharp
ctx.CreateInsertBuilder<Document>()
    .IgnoreFilters(["soft-delete"])
    .Values(document)
    .Insert();
```

Фильтр, объявленный в форме функции билдера (`FilterFunc`), нельзя проверить по записываемой строке — он вычисляется только при построении плана запроса, — поэтому запись, у которой активный фильтр **цели** объявлен функцией, отклоняется (fail-closed), если фильтр не отключён через `IgnoreFilters`; объявите фильтр предикатом (`FilterLambda`), когда запись должна проходить проверку. `INSERT … SELECT` по-прежнему фильтрует свой **источник** как чтение, а `UPDATE` / `DELETE` не затронуты (см. [Фильтры-функции билдера](#фильтры-функции-билдера-filterfunc)).

## Изоляция цели записи

Запись фильтруется с двух сторон, и они независимы:

- **источник** `INSERT … SELECT` или `MERGE` из запроса — обычное чтение, поэтому фильтр, объявленный для сущности-источника, подмешивается в исходный запрос как обычно;
- у **цели** нет `FROM`, поэтому её фильтр не входит ни в один исходный запрос. При активном, не проигнорированном фильтре цели операция ограничивается атомарно в самой инструкции там, где диалект это выражает; где не может — команда **отказывает** с `NotSupportedException` до любой мутации. Отказ по возможности/форме происходит до любого чтения, но merge с источником-запросом может сначала выполнить pre-check-чтение источника, прежде чем отказ по трансляции/рендерингу. Предикат никогда не отбрасывается молча: активный фильтр, который невозможно транслировать в предикат цели, — это fail-closed-ошибка, а не обход.

Для формы полного `MERGE` (SQL Server, PostgreSQL) предикат цели подмешивается в условие `MERGE ... ON` и — в SQL Server, у которого есть эта ветка — дописывается в каждую ветку `WHEN NOT MATCHED BY SOURCE`. Поэтому строка цели, скрытая фильтром, никогда не сопоставляется, не обновляется и не удаляется merge'ем, включая его ветку удаления. Key upsert SQL Server рендерится той же формой `MERGE` и получает тот же предикат, поэтому тоже фильтруется.

```csharp
ctx.CreateMergeBuilder<Document>()
    .Using(new Document { Id = 1, TenantId = 1, IsDeleted = true })
    .OnKeys()
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .Merge();
```

На форме полного `MERGE` активный фильтр цели присоединяется к условию поиска `ON`:

```sql
-- PostgreSQL
merge into documents as target using (values (@p1, @p2, @p3)) as source (id, tenant_id, is_deleted)
 on target.id = source.id and ((not (target.is_deleted) and target.tenant_id = @p0))
 when matched then update set tenant_id = source.tenant_id, is_deleted = source.is_deleted
 when not matched then insert (id, tenant_id, is_deleted) values (source.id, source.tenant_id, source.is_deleted)

-- SQL Server
merge into documents as target using (values (@p1, @p2, @p3)) as source (id, tenant_id, is_deleted)
 on target.id = source.id and ((not ((target.is_deleted) = 1) and target.tenant_id = @p0))
 when matched then update set target.tenant_id = source.tenant_id, target.is_deleted = source.is_deleted
 when not matched then insert (id, tenant_id, is_deleted) values (source.id, source.tenant_id, source.is_deleted);
```

**Правило формы.** Атомарная форма — настоящий много-веточный `MERGE`, требующий реальных веток `WhenMatched()`/`WhenNotMatched()`. Вызов `.On(...)` не превращает key-upsert-сокращение (`OnKeys()` + `WhenMatchedUpdate()` + `WhenNotMatchedInsert()`) в полный `MERGE`; это две разные формы. Поэтому на провайдере без поддержки полного `MERGE` (SQLite, MySQL, MariaDB, in-memory) безветочный `.On(...)` при активном фильтре отказывает с `NotSupportedException` до любого чтения источника.

**PostgreSQL — серверная предпосылка, а не клиентская защита.** PostgreSQL 15+ требуется, чтобы сервер выполнил общий `MERGE`; nextorm не обнаруживает и не настраивает версию сервера и **не читает её**. Рендеринг по возможностям не зависит от версии — библиотека выдаёт инструкцию только по флагам возможностей диалекта, а более старый сервер отклоняет её на своей стороне. Ветки «PostgreSQL < 15 отказывает» на стороне библиотеки **нет**.

| Провайдер / форма | Фильтр цели при активном, не проигнорированном фильтре |
|---|---|
| SQL Server — полный `MERGE` и key upsert (`MERGE`) | подмешивается в `ON` и в каждую ветку `WHEN NOT MATCHED BY SOURCE` |
| PostgreSQL (сервер 15+) — полный `MERGE` | подмешивается в `ON` (в PostgreSQL нет `WHEN NOT MATCHED BY SOURCE`) |
| PostgreSQL, SQLite — key upsert (`ON CONFLICT`) | `NotSupportedException` — fail closed |
| MySQL, MariaDB — key upsert (`ON DUPLICATE KEY`) | `NotSupportedException` — fail closed |
| In-memory — key upsert | `NotSupportedException` — fail closed |
| Любой провайдер — нет активного фильтра или фильтр отключён | нативное поведение, без дополнительного предиката |

Отказ по возможности — это **решение по метаданным, до любого соединения, команды или чтения** (ноль обращений к базе); сообщение исключения указывает на `IgnoreFilters()` или на провайдера/форму с полным `MERGE`. Это намеренное fail-closed-поведение, а не баг.

`IgnoreFilters` обходит фильтр цели для инструкции, с теми же четырьмя перегрузками, что и везде (all-or-nothing, по типу сущности, по ключу и пересечение ключа и типа). Область, отключающая фильтр целевой сущности — или все фильтры, — восстанавливает нативный upsert провайдера без предиката; селективная область, не покрывающая фильтр цели, оставляет его активным, поэтому key upsert `ON CONFLICT` / `ON DUPLICATE KEY` всё равно отказывает.

### Пределы изоляции цели

- **Оракул существования при совпадении по уникальному индексу (только insert-ветка).** Когда insert-ветка `MERGE` пытается вставить строку, ключ которой совпадает со скрытой фильтром строкой цели, провайдер сообщает нативную ошибку нарушения уникальности для того уникального индекса, который сработал, — не только по PK/ключу совпадения, — раскрывая существование скрытой строки цели; устранить это нельзя без удаления ограничения уникальности. Смягчение составным ключом (включить столбец тенанта/фильтра) применимо только к первичному/ключу совпадения: оно не даёт ключу скрытой строки совпасть с ключом видимой строки-источника, но не покрывает другие уникальные индексы. `MERGE` только с update- или только с delete-веткой никогда не вставляет, поэтому скрытая строка — это молчаливый no-op (скрытая фильтром строка просто не сопоставляется) и оракула не даёт.
- **Проверка значений источника — отдельное чтение.** Для `INSERT … SELECT` и `MERGE` из запроса записываемые значения источника проверяются отдельным запросом существования (`QueryFilterValidator`) с окном **TOCTOU**. Он проверяет значения источника, а не строки цели, поэтому не раскрывает существование скрытой строки цели и не делает запись атомарной (см. [INSERT и MERGE (проверка)](#insert-и-merge-проверка)).
- Отказ выше — намеренный безопасный исход, когда диалект не может изолировать цель атомарно.

Если фильтр **не** настроен, путь записи не меняется: предикат не добавляется, и ни одна операция не отказывает.

## Фильтры-функции билдера (`FilterFunc`)

Фильтр можно объявить не предикатом, а **функцией билдера**: `Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>`. nextorm вызывает её **один раз, при построении плана запроса**, передавая живой [`IDataContext`](xref:NextORM.Core.IDataContext); функция вызывает `Where` на свежем билдере, и nextorm сливает **только** этот предикат в `WHERE` основного источника или в `ON` соединения — точно так же, как у фильтра-предиката.

```csharp
ctx.From<Document>(m => m.HasQueryFilter(
    (b, c) => b.Where(d => d.TenantId == (int)c.Properties["tenant"])));

// Форма с ключом — функция занимает тот же слот, что и фильтр-предикат.
ctx.From<Document>(m => m.HasQueryFilter(
    "tenant", (b, c) => b.Where(d => d.TenantId == (int)c.Properties["tenant"])));
```

Читайте значение через `IDataContext` **внутри** предиката — или используйте [`SqlFunctions.Parameter<T>(idx)`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) — чтобы оно осталось bound-параметром, а сгенерированный SQL разделялся между контекстами, ровно как у фильтра-предиката. Цепочка вызовов `Where` допускается.

### Атрибут

`QueryFilterAttribute.FilterFunc` называет **статический член** атрибутируемого типа, возвращающий функцию; объявление задаёт либо `FilterLambda`, либо `FilterFunc`, но не оба:

```csharp
[QueryFilter(FilterKey = "tenant", FilterFunc = nameof(TenantFilter))]
public sealed class Document
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public static Func<EntityBuilder<Document>, IDataContext, EntityBuilder<Document>> TenantFilter()
        => (b, c) => b.Where(d => d.TenantId == (int)c.Properties["tenant"]);
}
```

### IgnoreFilters

Форма функции участвует в `IgnoreFilters` точно так же, как фильтр-предикат: у неё тот же ключ (или [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey)), поэтому keyed-, type-only- и intersection-области отключают её одинаково — `IgnoreFilters(["tenant"])`, `IgnoreFilters(typeof(Document))` и all-or-nothing `IgnoreFilters()` равно применяются.

### Функция не должна снимать снапшот runtime-значения

Функция выполняется **только при построении плана**, поэтому runtime-значение нужно читать через `IDataContext` **внутри** предиката. Захват его в локальную переменную до вызова `Where` отклоняется с `NotSupportedException`: значение было бы запечено в кэшированный план (и в in-memory вычисление) и переиспользовано каждым последующим выполнением и каждым другим контекстом, так что попадание в кэш плана молча вернуло бы неверные строки.

```csharp
// Отклоняется: `tenant` снимается до вызова Where и потому был бы заморожен в плане.
ctx.From<Document>(m => m.HasQueryFilter((b, c) =>
{
    var tenant = (int)c.Properties["tenant"];
    return b.Where(d => d.TenantId == tenant);
}));
```

### Неподдерживаемые случаи

- **Мутации, отличные от `Where`.** Сливается только предикат `Where`, поэтому функция, которая меняет на билдере что-либо ещё — источник, соединение, select, group, сортировку, пагинацию, eager loading или область фильтров, — возвращает другой билдер, либо возвращает билдер без `Where` / `null`, отклоняется с `NotSupportedException`. Выражайте фильтр только через `Where`.
- **Захваченные коллекции.** Предикат, замыкающий коллекцию — например `ids.Contains(e.Id)`, — захватывает её как runtime-значение и отклоняется с `NotSupportedException`. Функция выполняется только при построении плана, поэтому коллекция не может остаться bound-списком `IN`; объявите фильтр предикатом (`FilterLambda`) или используйте плейсхолдер `SqlFunctions.Parameter<T>(idx)`.
- **Чужой захваченный контекст.** Предикат, замыкающий `IDataContext`, отличный от переданного функции, отклоняется с `NotSupportedException`: чтение его на общем плане молча вернуло бы значения другого контекста. Читайте параметр `IDataContext` самой функции.
- **Проверка цели `INSERT` / `MERGE`.** Фильтр-функция вычисляется только при построении плана запроса, поэтому её нельзя проверить по записываемой строке; запись, у которой активный фильтр **цели** объявлен функцией, отклоняется (fail-closed), если фильтр не отключён через `IgnoreFilters`. Объявите фильтр предикатом (`FilterLambda`), когда запись должна проходить проверку.
- **Фильтры-функции на непривязанных `FromSql` / сырых источниках** не применяются (сырой SQL передаётся как есть; то же верно и для предикатных фильтров). Привяжите источник через [`BindEntity<TEntity>`](#фильтры-на-привязанных-сырых-источниках), чтобы включить их.

## Провайдер in-memory

Провайдер in-memory применяет тот же предикат к зарегистрированной последовательности до проекции и соединений, поэтому запросы soft-delete и multi-tenancy ведут себя так же, как у SQL-провайдеров. Перегрузки `IgnoreFilters` там тоже учитываются. Для записи — key-upsert merge — единственной применяемой там формы записи — активный, не проигнорированный фильтр заставляет операцию **отказать** с `NotSupportedException` (fail closed; см. [Изоляция цели записи](#изоляция-цели-записи)); `IgnoreFilters` восстанавливает нативное поведение.

## Фильтры на привязанных сырых источниках

У источника [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object))/`From(string)` нет отображаемого типа сущности, поэтому nextorm не знает, какие столбцы читает фильтр. Вызовите [`BindEntity<TEntity>`](xref:NextORM.Core.EntityBuilderExtensions.BindEntity``1(NextORM.Core.EntityBuilder{NextORM.Core.TableAlias},System.Collections.Generic.IReadOnlyCollection{System.String})) первой операцией над источником, чтобы привязать метаданные сущности и объявить столбцы, которые раскрывает сырой SQL; после этого nextorm применяет каждый активный глобальный фильтр к этому источнику по мере возможности:

```csharp
var rows = dataContext
    .FromSql("select id, tenant_id, is_deleted from documents")
    .BindEntity<Document>(["id", "tenant_id", "is_deleted"])
    .Where(t => t.Id > 10)
    .Select(t => t.Id)
    .ToList();
```

```sql
-- PostgreSQL
select id from (select id, tenant_id, is_deleted from documents) as "t1"
 where (((t1.id > 10) and not (t1.is_deleted)) and t1.tenant_id = @p0)

-- SQL Server
select id from (select id, tenant_id, is_deleted from documents) as [t1]
 where (((t1.id > 10) and not ((t1.is_deleted) = 1)) and t1.tenant_id = @p0)
```

`availableColumns` — это **выходные/SQL-имена** сырого списка `select` (сконфигурированное отображение столбца имеет приоритет, иначе берётся имя, которое ожидает проекция); сравнение регистронезависимо. Список — это объявление вызывающего, а не чтение схемы: nextorm не разбирает SQL и не запрашивает схему, а привязка не добавляет и не переименовывает выходные столбцы, поэтому столбцы, которые вы проецируете, остаются вашей ответственностью.

Точная сигнатура:

```csharp
public static EntityBuilder<TEntity> BindEntity<TEntity>(
    this EntityBuilder<TableAlias> source,
    IReadOnlyCollection<string> availableColumns)
```

Для каждого активного фильтра на `TEntity`:

- если объявлены все столбцы, которые читает фильтр, фильтр добавляется к источнику через `and`;
- если обязательный столбец отсутствует, фильтр **пропускается** (без исключения) и на категорию логгера `NextORM.QueryFilters` пишется предупреждение `RawSourceFilterSkipped` с причиной `MissingColumns` и именами отсутствующих столбцов — это **физические имена mapped-столбцов**, которые читает фильтр, а не объявленные вызывающим выходные имена;
- **при пустом объявленном списке столбцов** фильтр с доказанной пустой зависимостью (предикат, не читающий ни одного mapped-столбца, например константа) применяется; фильтр, зависящий от столбца, или неопределённый фильтр пропускается с предупреждением (причина `UndeterminedColumns`).

Фильтры вычисляются независимо — один пропущенный фильтр не останавливает остальные, — а [`IgnoreFilters`](#отключение-фильтров-для-одного-запроса) отключает выбранные фильтры до этого анализа, поэтому проигнорированный фильтр никогда не сообщается. Предупреждение выводится один раз на каждый пропущенный фильтр при подготовке плана запроса (промах кэша); попадание в кэш планов его не повторяет. Сообщение несёт только имя типа сущности, порядковый номер источника, ключ фильтра, причину и имена отсутствующих столбцов (только физические имена mapped-столбцов) — никогда текст SQL, имена таблиц, значения параметров или захваченные значения. Подключите `ILoggerFactory` и включите категорию `NextORM.QueryFilters` на уровне `Warning`, чтобы его увидеть (категории тематические, поэтому достаточно фильтра по категории):

```csharp
using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddFilter("NextORM.QueryFilters", LogLevel.Warning)
    .AddConsole());

var ctx = new SqliteDataContext(builder => builder.UseLoggerFactory(loggerFactory));
```

`BindEntity` должен быть первой операцией после создания источника; более поздний вызов (после `Where`/`Select`/`Join`/проекции) бросает `InvalidOperationException`. Другая форма источника или привязка к `TableAlias` бросает `NotSupportedException`; `null`-источник или `null`-коллекция столбцов бросает `ArgumentNullException`, а `null`/пустая строка в элементе — `ArgumentException` (пустая коллекция допустима, получатель не мутируется — возвращается новый типизированный билдер). `WithSql`, `PrepareFromSql` и `ExecuteRaw` не меняются и не участвуют, а провайдер in-memory по-прежнему отклоняет сырой источник.

> **Не гарантия безопасности.** nextorm доверяет вашему списку столбцов: фильтр, для которого вы забыли объявить столбцы, молча пропускается, и сырой источник возвращает строки, которые фильтр исключил бы. Воспринимайте `BindEntity` как удобство для композиции фильтров поверх доверенного сырого источника, а не как границу принуждения или изоляции; если авторизация на уровне строк обязана применяться, держите её в самом SQL.

### Присоединённые привязанные источники и главный источник

Привязка действует **на источник**, а не на тип сущности: каждый присоединённый сырой источник использует собственную привязку `BindEntity<TEntity>` и собственный объявленный список столбцов и никогда не заимствует привязку главного источника или другого вхождения. Один и тот же тип сущности, присоединённый дважды с разными объявленными столбцами, сохраняет обе привязки независимыми. Совместимые фильтры присоединённого источника добавляются в `ON` **именно этого соединения**; пропущенный фильтр в одном вхождении даёт отдельное предупреждение и не затрагивает остальные вхождения и остальные фильтры, а предикаты outer-соединения остаются в `ON` и никогда не переносятся в `WHERE`.

Фильтры главного источника всегда вычисляются по **его собственной привязке**, даже когда команда — присоединённая проекция. В этом случае удержанные предикаты перепривязываются к **главному** псевдониму проекции `Item1` (`Projection<T1, …>`) и помещаются в `WHERE`; они никогда не разрешаются из привязки или псевдонима присоединённой стороны.

`SourceOrdinal` указывает, где произошёл пропуск: главный источник — `0`, а соединение с индексом `j` (с нуля) — `j + 1`, при этом считаются **все** соединения, включая непривязанные, поэтому номер отражает позицию соединения, а не число привязанных источников.

Соединение `CROSS`/`CROSS APPLY` не имеет `ON`, поэтому у привязанного сырого источника на таком соединении нет предиката, к которому его можно прикрепить: его совместимые фильтры помещаются в `WHERE`, и nextorm никогда не выдумывает `ON` для соединения, у которого его нет. `OUTER APPLY`/`PASTE` остаются без изменений, так как перенос их предикатов в `WHERE` изменил бы результат.

## Метаданные

Объявленный фильтр доступен через [`IQueryFilterMetadata`](xref:NextORM.Core.IQueryFilterMetadata):

```csharp
public interface IQueryFilterMetadata
{
    string Key { get; }                    // QueryFilters.AnonymousKey ("") для анонимного фильтра
    LambdaExpression? Lambda { get; }      // предикат либо null для фильтра-функции билдера
    Delegate? Func { get; }                // объявление функции билдера либо null для фильтра-предиката
}
```

`Key` равен [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey) для анонимного фильтра и объявленному ключу для именованного. Фильтр-предикат отдаёт свой предикат в `Lambda` и `null` в `Func`; фильтр-функция билдера отдаёт `null` в `Lambda` и объявление `Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>` в `Func`. Фильтр, отдающий оба значения не равными `null`, отклоняется.

У `Func` есть реализация по умолчанию, возвращающая `null`, поэтому внешняя реализация, не объявляющая `Func`, продолжает компилироваться. Тип возврата этого члена изменился с `LambdaExpression?` на `Delegate?`, пока член не был выпущен, поэтому реализация, явно объявлявшая прежнюю форму, должна обновить тип возврата; на выпущенных потребителей это не влияет, так как член никогда не поставлялся.

## Ограничения

- **Процессно-глобальная регистрация, побеждает первая.** Фильтры регистрируются **глобально на процесс**, и для типа сущности побеждает первая регистрация (согласовано с метаданными nextorm): последующий `From<T>(cfg)` / `HasQueryFilter` для того же типа игнорируется — фильтры не привязаны к `DataContext`.
- **Область отключения наследует входной билдер.** Селективную область несёт билдер, который начинает запрос; вызов `IgnoreFilters` на билдере, который затем используется как источник соединения, не пробрасывается. Используйте перегрузку по типам/ключам на входном билдере запроса. Исключение — жадно загружаемые дочерние записи `LoadWith`: они наследуют область входного билдера объединением (см. [Eager loading](eager-loading.md)).
- **Key-формы мутаций применяют фильтры, но не предоставляют `IgnoreFilters`.** `Update(entity)` и `Delete(entity)` учитывают фильтр цели, но у их немедленного терминала нет fluent-вызова `IgnoreFilters`; используйте предикатную форму (`CreateUpdateBuilder<T>().Where(...)` / `CreateDeleteBuilder<T>().Where(...)`), когда нужно отключить фильтр на мутации.
- **Время жизни плана.** Значение контекста, читаемое фильтром, захватывается как runtime-параметр (безопасно для кэша плана). Подготовленный план сохраняет первый экземпляр `IDataContext` на всё время существования (ограниченно, один на форму плана).
- **Открытые единицы по адаптерам.** Срез «ClickHouse без EF-адаптера» и сертификация общего соединения специально для MariaDB отслеживаются как открытые единицы вехи `1.0.9-b`; до их появления используйте поддерживаемые EF-адаптеры (см. [Мост фильтров запросов EF Core](ef-core-query-filters.md)).

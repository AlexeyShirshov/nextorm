# Слияние данных (`MERGE` / upsert)

> nextorm строит `MERGE` (upsert) через [`CreateMergeBuilder<TEntity>()`](xref:NextORM.Core.DataContextExtensions.CreateMergeBuilder``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})): одна точка входа покрывает и переносимый **key upsert** (`INSERT ... ON CONFLICT` / `ON DUPLICATE KEY` / `MERGE`), и общий много-веточный **полный `MERGE`** с ветками `WHEN MATCHED`/`WHEN NOT MATCHED`. Изменения не отслеживаются, `SaveChanges` нет: каждый терминал выполняет ровно одну команду.

**Предварительно:** [Изменение данных (INSERT)](17-insert-statement.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Обзор провайдеров](../connecting-providers/overview.md)

## Обзор

`CreateMergeBuilder<TEntity>()` записывает набор строк-источника в целевую таблицу и позволяет базе построчно решить, что делать. Источник — отображённая сущность, батч или серверный запрос; совпадение — либо объявленный ключ (`OnKeys()`), либо произвольное условие (`On(...)`); действия задаются ветками. `Merge()`/`MergeAsync()` выполняют команду и возвращают число затронутых строк, а `ToSql()` рендерит утверждение без соединения.

Один билдер обслуживает две формы:

* **key upsert** — `OnKeys()` + `WhenMatchedUpdate()` + `WhenNotMatchedInsert()`. Каждый SQL-провайдер выражает её нативно, и только она применяется in-memory-провайдером (к зарегистрированной последовательности).
* **полный `MERGE`** — `WhenMatched()`/`WhenNotMatched()`/`WhenNotMatchedBySource()`, каждая завершается `ThenUpdate`/`ThenInsert`/`ThenDelete`/`ThenDoNothing`. Нативно на SQL Server и PostgreSQL (предпосылка времени выполнения: сервер 15+); остальные провайдеры его отклоняют.

## Upsert (key merge)

[`CreateMergeBuilder<TEntity>()`](xref:NextORM.Core.DataContextExtensions.CreateMergeBuilder``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) записывает набор строк-источника в таблицу и позволяет базе по объявленному ключу решить: обновить существующую строку или вставить новую. Источник — одна сущность или батч, ключ совпадения разрешается из маппинга сущности через `OnKeys()`, и обе ветки — `WhenMatchedUpdate()` (присвоить все не-key записываемые колонки из источника) и `WhenNotMatchedInsert()` — обязательны:

```csharp
ctx.CreateMergeBuilder<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })   // или Using(new[] { e1, e2 })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .Merge();
```

Билдер рендерит родную форму провайдера; `ToSql()` показывает её без соединения:

| Провайдер | Рендер |
|---|---|
| PostgreSQL, SQLite | `INSERT ... ON CONFLICT (<keys>) DO UPDATE SET <col> = excluded.<col>` |
| MySQL, MariaDB | `INSERT ... ON DUPLICATE KEY UPDATE <col> = VALUES(<col>)` |
| SQL Server | `MERGE ... USING (VALUES ...) AS source (...) ON ... WHEN MATCHED THEN UPDATE SET ... WHEN NOT MATCHED THEN INSERT ...;` |
| In-memory | применяется к зарегистрированной последовательности в контексте (без SQL) |
| ClickHouse | `NotSupportedException` (движкового DML-upsert нет) |

```sql
-- PostgreSQL
insert into simple_entity (id, name) values (@p0, @p1) on conflict (id) do update set name = excluded.name

-- SQLite
insert into simple_entity (id, name) values ($p0, $p1) on conflict (id) do update set name = excluded.name

-- MySQL / MariaDB
insert into simple_entity (id, name) values (@p0, @p1) on duplicate key update name = values(name)

-- SQL Server
merge into simple_entity as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set target.name = source.name when not matched then insert (id, name) values (source.id, source.name);
```

`Merge()`/`MergeAsync()` возвращают число затронутых строк. Ключ должен быть объявлен (`[Key]`/`.Key()`) и не быть генерируемым базой; сущность только из key-колонок отклоняется, так как обновлять нечего. Это форма *key upsert*; произвольные ветки (включая `DELETE`) — см. [Полный `MERGE`](#полный-merge).

## Полный `MERGE`

[`WhenMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenMatched) / [`WhenNotMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatched) / [`WhenNotMatchedBySource()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatchedBySource) расширяют тот же билдер до общего много-веточного `MERGE` на провайдерах, которые рендерят его нативно (SQL Server, PostgreSQL; сервер 15+). Каждая ветка завершается действием, и ветки выполняются в порядке объявления:

```csharp
ctx.CreateMergeBuilder<IDest>()
    .Using(source)                            // сущность, батч или запрос: Using(ctx.From<IDest>().Where(...))
    .OnKeys()                                 // или .On((t, s) => t.Id == s.Id && s.Age > 0)
    .WhenMatched().ThenUpdate()               // или .WhenMatched((t, s) => t.Name != s.Name).ThenUpdate()
    .WhenNotMatched().ThenInsert()            // или .WhenNotMatched((t, s) => s.Age > 0).ThenInsert()
    .WhenNotMatchedBySource().ThenDelete()    // только SQL Server
    .Merge();
```

```sql
-- SQL Server
merge into dest as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set target.name = source.name when not matched then insert (id, name) values (source.id, source.name) when not matched by source then delete;
```

`ThenUpdate()`/`ThenInsert()` без селектора пишут все подходящие колонки; явный набор присваиваний ограничивает записываемые колонки — см. [Присваивания колонок](#присваивания-колонок-выражения). Селектор не может назвать генерируемую базой колонку, а matched-ветка не может переписать ключ совпадения. `ThenDelete()` на matched-ветке и `WhenNotMatchedBySource().ThenDelete()` позволяют merge удалять строки — отличие от key upsert. `ThenDoNothing()` оставляет строку-кандидат без изменений; доступно только на PostgreSQL, так как в SQL Server действия `DO NOTHING` нет.

`On(condition)` заменяет матч по ключу произвольным условием совпадения (`ON <condition>`). Билдер ветки тоже принимает условие (`WhenMatched(condition)`, `WhenNotMatched(condition)`, `WhenNotMatchedBySource(condition)`), которое рендерится как `WHEN ... AND <condition>`; ветка срабатывает, только если условие истинно. Условия используют форму с двумя параметрами `(target, source)` — существующая строка это первый параметр лямбды, входящая строка-источник — второй, например `On((t, s) => t.Id == s.Id && s.Age > 0)`. В SQL Server условие `WHEN NOT MATCHED BY SOURCE` может ссылаться только на строку target.

**Ограничение источника, производного от `VALUES`.** При источнике `VALUES` — сущности или батче, переданных в `Using(IEnumerable<TEntity>)` — условие совпадения `On(...)` и условие любой ветки (`WhenMatched(...)`, `WhenNotMatched(...)`, `WhenNotMatchedBySource(...)`, каким бы действием она ни завершалась: `ThenUpdate`, `ThenDelete` или `ThenDoNothing`) не могут ссылаться на отображённую колонку источника, **не входящую в производный источник `VALUES`**. Такой источник объявляет только записываемые отображённые колонки, поэтому генерируемая базой колонка (identity или computed) не имеет производной колонки, и `ToSql()`/`Merge()` бросает `NotSupportedException`:

```csharp
// не поддерживается: Total — computed, поэтому источник VALUES не объявляет колонку Total
ctx.CreateMergeBuilder<IDest>()
    .Using(new Dest { Name = "a" })
    .On((t, s) => t.Name == s.Name && s.Total > 0)
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .ToSql();   // NotSupportedException

// поддерживается: источник-запрос проецирует всю строку, включая Total
ctx.CreateMergeBuilder<IDest>()
    .Using(ctx.From<IDest>())
    .On((t, s) => t.Name == s.Name && s.Total > 0)
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .ToSql();
```

Иначе ссылайтесь только на колонки, которые источник `VALUES` уже несёт.

| Провайдер | Полный `MERGE` | `THEN DELETE` | `THEN DO NOTHING` | `ON` / `WHEN ... AND` | `WHEN NOT MATCHED BY SOURCE` |
|---|---|---|---|---|---|
| SQL Server | да | да | — | да | да |
| PostgreSQL | да (сервер 15+) | да | да | да | — |
| SQLite, MySQL, MariaDB | — (только key upsert) | — | — | — | — |
| In-memory | — (только key upsert) | — | — | — | — |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` |

`Using(ctx.From<T>().Where(...))` или `Using(QueryCommand<T>)` задаёт строки серверным запросом (`USING (<select>) AS source`) вместо батча `VALUES`; источник-запрос принимает только форма полного `MERGE`.

При активном [глобальном фильтре запросов](../advanced/query-filters.md) полный `MERGE` подмешивает предикат цели в условие `MERGE ... ON` и в каждую ветку `WHEN NOT MATCHED BY SOURCE` (SQL Server), поэтому строка цели, скрытая фильтром, никогда не сопоставляется, не обновляется и не удаляется. Форма должна быть настоящим много-веточным `MERGE` (реальные ветки `WhenMatched()`/`WhenNotMatched()`); `.On(...)` не превращает key-upsert-сокращение в эту форму. Key upsert на провайдере, который не может выразить предикат (`ON CONFLICT` / `ON DUPLICATE KEY` / in-memory), **отказывает** с `NotSupportedException` до любой мутации, и предикат никогда не отбрасывается молча; `IgnoreFilters()` восстанавливает нативный upsert. Отказ по возможности/форме происходит до любого чтения, а merge с источником-запросом может сначала выполнить pre-check-чтение источника, прежде чем отказ по трансляции/рендерингу. На поддерживаемой форме полного `MERGE` каждая ветка — включая update-only и delete-only — проверяет входящие строки источника по фильтрам цели и бросает `QueryFilterException` до мутации (см. [Изоляция цели записи](../advanced/query-filters.md#изоляция-цели-записи) и [INSERT и MERGE (проверка)](../advanced/query-filters.md#insert-и-merge-проверка)).

### Возврат слитых строк

[`Returning()`](xref:NextORM.Core.MergeBuilder`1.Returning) и [`Returning(x => new { ... })`](xref:NextORM.Core.MergeBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) материализуют слитые строки через выходную клаузу провайдера. Читаются через `Single()`/`SingleAsync()`/`ToList()`/`ToListAsync()`:

```csharp
var rows = ctx.CreateMergeBuilder<IDest>()
    .Using(source)
    .OnKeys()
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .Returning(x => new { x.Id, x.Name })
    .ToList();
```

```sql
-- SQL Server
merge into dest as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set target.name = source.name when not matched then insert (id, name) values (source.id, source.name) output inserted.id, inserted.name;

-- PostgreSQL 17+
merge into dest as target using (values (@p0, @p1)) as source (id, name) on target.id = source.id when matched then update set name = source.name when not matched then insert (id, name) values (source.id, source.name) returning target.id, target.name;
```

Возврат доступен для **обеих** форм. SQL Server рендерит переносимый key upsert как
`MERGE ... OUTPUT inserted.<col>`, а PostgreSQL и SQLite добавляют `RETURNING` к своей форме
`ON CONFLICT ... DO UPDATE`; полный `MERGE` возвращает строки через SQL Server `OUTPUT inserted.<col>`
и PostgreSQL 17+ `RETURNING target.<col>`. MySQL/MariaDB не имеют возвращающей формы для key upsert,
поэтому `.Returning()` там бросает `NotSupportedException`; SQLite и ClickHouse по-прежнему отклоняют
полный `MERGE` (а для самой инструкции общего `MERGE` сервер должен быть PostgreSQL 15+).

## Присваивания колонок (выражения)

Обе формы принимают явный набор присваиваний вместо правила по умолчанию «каждая не-ключевая колонка — из одноимённой колонки источника». Селектор обязан быть анонимным инициализатором или типизированным инициализатором, присваиваемым сущности; голый член, пустой инициализатор или тело, не являющееся инициализатором, отклоняются с `ArgumentException` (параметр `assignments`) до построения любого SQL.

Перегрузки различаются арностью лямбды — три формы обновления и одна форма вставки рядом с вариантом без аргументов:

```csharp
// key upsert, на MergeBuilder<TEntity>
public MergeBuilder<TEntity> WhenMatchedUpdate();
public MergeBuilder<TEntity> WhenMatchedUpdate(Expression<Func<TEntity, TEntity, object?>> assignments);         // target, source
public MergeBuilder<TEntity> WhenMatchedUpdate(Expression<Func<TEntity, TEntity, TEntity, object?>> assignments); // target, source, excluded
public MergeBuilder<TEntity> WhenNotMatchedInsert();
public MergeBuilder<TEntity> WhenNotMatchedInsert(Expression<Func<TEntity, object?>> assignments);                 // только source

// полный MERGE, на построителях веток
public MergeBuilder<TEntity> ThenUpdate();
public MergeBuilder<TEntity> ThenUpdate(Expression<Func<TEntity, TEntity, object?>> assignments);
public MergeBuilder<TEntity> ThenUpdate(Expression<Func<TEntity, TEntity, TEntity, object?>> assignments);
public MergeBuilder<TEntity> ThenInsert(Expression<Func<TEntity, object?>> assignments);                          // только source
```

Инициализатор связывается **по имени члена**: каждый член инициализатора — это отображённое CLR-свойство назначения (`Name`, а не имя колонки в базе), а правая часть — значение, записываемое в это назначение. Записываются только присутствующие члены: пропущенная колонка сохраняет значение по умолчанию в базе при вставке и текущее значение при обновлении, неявного «скопируй все остальные колонки из источника» нет. Правая часть привязана к роли того **параметра лямбды, на котором она написана**, и никогда не выводится из совпадения имён членов: в `new { Name = source.Name }` чтение относится к роли source, хотя назначение тоже называется `Name`.

```csharp
// key upsert
ctx.CreateMergeBuilder<IDest>()
    .Using(new Dest { Id = 1, Name = "a", Age = 5 })
    .OnKeys()
    .WhenMatchedUpdate((target, source) => new { Name = source.Name, Age = target.Age + 1 })
    .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
    .Merge();

// полный MERGE: те же перегрузки на действиях веток
ctx.CreateMergeBuilder<IDest>()
    .Using(source)
    .OnKeys()
    .WhenMatched().ThenUpdate((target, source) => new { Name = source.Name, Age = target.Age + 1 })
    .WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name })
    .Merge();
```

Слева всегда отображённая колонка назначения; записываются только назначенные колонки. Identity-, computed- и ключевые назначения отклоняются с `NotSupportedException`, как и неизвестные или повторяющиеся назначения.

### Роли

Параметры лямбды — это роли, разрешаемые по позиции, а не по имени:

| Роль | Позиция | Смысл | Во что рендерится |
|---|---|---|---|
| target | 1-я | строка цели в состоянии до обновления | алиас цели (полный `MERGE`) или цитируемая ссылка на таблицу (нативный key upsert) |
| source | 2-я | исходная входная строка, переданная в `Using` | `source.<col>` (полный `MERGE`); параметр инструкции, прочитанный из единственной материализованной входной строки (нативный key upsert) |
| excluded | 3-я | строка, которую провайдер предлагает для вставки | `excluded.<col>` (PostgreSQL, SQLite) или `VALUES(<col>)` (MySQL, MariaDB) |

* Третий параметр необязателен. Объявить его, не читая, — значит не добавить роль; это допускается.
* Использованная роль `excluded` отклоняется на форме без предлагаемой строки (полный `MERGE`, key-upsert `MERGE` в SQL Server) с `NotSupportedException`, называющим роль.
* На нативном key upsert чтение роли **source** — в update-ветке или в выражении INSERT — требует ровно одной материализованной входной строки; батч или источник-запрос, использующий эту роль, отклоняется до выполнения (`... requires one materialized source row ...`). Объявленная, но неиспользованная роль source этот guard не вызывает.
* Выражение **INSERT** на key upsert должно присваивать каждый конфликтный ключ напрямую из его одноимённого свойства исходной строки (`Id = source.Id`). Константа, преобразованное выражение или другое свойство отклоняются с `ArgumentException` и называют ключ. Insert-форма полного `MERGE` от этого правила освобождена.
* Присваивание **INSERT** видит только строку source. `ThenInsert`/`WhenNotMatchedInsert` принимают одно-параметрическую лямбду, поэтому правая часть, называющая вторую роль, — ошибка компиляции, а не тихая подстановка.

`excluded` — не синоним `source`, и разница наблюдаема. На нативном key upsert возьмём строку цели с `Count = 10`, входную строку с `Count = 3` и insert-присваивание `Count = source.Count * 2` (провайдер предлагает `Count = 6`):

| Присваивание в update | Читает | Сохранённое значение |
|---|---|---|
| `new { Count = target.Count + source.Count }` | цель до обновления + оригинальный вход | `13` (`10 + 3`) |
| `new { Count = target.Count + excluded.Count }` | цель до обновления + предлагаемая строка | `16` (`10 + 6`) |

Предлагаемая строка — та, которую вставил бы сам движок: она несёт значения после insert-преобразования и, там где движок их определяет, нативные значения по умолчанию и эффекты триггеров, — именно поэтому она отличается от оригинального входа. nextorm никогда не синтезирует, не пересчитывает и не проецирует эту строку, и единой кросс-движковой гарантии по генерируемым колонкам или триггерам нет: в `excluded` лежит ровно то, что предоставляет нативная форма (см. таблицу провайдеров ниже).

### Параметры и неподдерживаемые формы

* Каждая константа и замыкание в правой части присваивания становятся **именованным параметром инструкции** (`@p0`, `$p0`, ...), а не встроенным литералом; CLR `null` остаётся null-значением параметра. Поддерживаемые скалярные выражения остаются в SQL (`target.Age + 1`), а серверная функция без параметров не вычисляется на клиенте.
* Одна последовательность параметров на инструкцию покрывает условия, правые части присваиваний и строки `VALUES`; повторное выполнение снова связывает текущие значения замыканий/источника.
* Правая часть, не являющаяся одно-строчным скалярным выражением — вложенный запрос, агрегат, лямбда, объектный граф ссылочного типа — отклоняется с `NotSupportedException`: `MERGE assignment for '<property>' cannot translate expression '<nodeKind>'.`
* Провайдеры без рендеринга присваиваний вообще (ClickHouse, in-memory-контекст) отказывают fail-closed: `Provider '<provider>' does not support MERGE assignment expressions.` Key upsert без аргументов в in-memory продолжает работать.
* При активном фильтре цели выражение `INSERT` отклоняется до выполнения на **каждой** поддерживаемой форме присваиваний и у каждого провайдера — планируемая строка строится из выражений, и проверить её против фильтра до выполнения инструкции нельзя. `IgnoreFilters()` явно отключает это поведение (см. [Изоляция цели записи](../advanced/query-filters.md#изоляция-цели-записи)).

### Миграция с удалённого селектора колонок

Старый одно-параметрический селектор обновления (`ThenUpdate(x => new { x.Name })`, `WhenMatchedUpdate(x => new { x.Name })`, `WhenMatched().ThenUpdate(x => new { x.Name })`) **удалён**; это намеренное ломающее изменение. Замена явно читает роль source:

```csharp
// было (не компилируется)
.WhenMatched().ThenUpdate(x => new { x.Name })

// стало
.WhenMatched().ThenUpdate((target, source) => new { Name = source.Name })
```

`ThenInsert(x => new { x.Name })` по-прежнему компилируется — та же сигнатура теперь читается как присваивание `Name = source.Name`. Тело-голый-член (`ThenInsert(x => x.Name)`), пустой инициализатор или тело, не являющееся инициализатором, отклоняются во время выполнения с `ArgumentException` (параметр `assignments`): набор присваиваний insert обязан быть инициализатором.

## Просмотр SQL

[`ToSql()`](xref:NextORM.Core.MergeBuilder`1.ToSql) рендерит параметризованный SQL, который выполнил бы `Merge()`, не открывая соединение:

```csharp
var sql = ctx.CreateMergeBuilder<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .ToSql();
```

```sql
-- PostgreSQL
insert into simple_entity (id, name) values (@p0, @p1) on conflict (id) do update set name = excluded.name
```

## Сводка по провайдерам

| Провайдер | Key upsert | Полный `MERGE` | Присваивания | `RETURNING`/`OUTPUT` | Примечание |
|---|---|---|---|---|---|
| SQL Server | `MERGE ... USING (VALUES ...)` | да | target + source (предлагаемой строки нет) | `OUTPUT inserted.<col>` (key upsert и полный) | все ветки, включая `WHEN NOT MATCHED BY SOURCE` |
| PostgreSQL | `ON CONFLICT ... DO UPDATE` | да (сервер 15+) | target + source + `excluded` | `RETURNING` (key upsert 9.5+, полный 17+) | `DO NOTHING`; без `BY SOURCE` |
| SQLite | `ON CONFLICT ... DO UPDATE` | — | target + source + `excluded` | `RETURNING` (key upsert, 3.35+) | только key upsert |
| MySQL | `ON DUPLICATE KEY UPDATE` | — | target + source + `VALUES(<col>)` | — | только key upsert; нет `RETURNING` |
| MariaDB | `ON DUPLICATE KEY UPDATE` | — | target + source + `VALUES(<col>)` | — | только key upsert; `RETURNING` не используется для `ON DUPLICATE KEY` |
| In-memory | применяется к зарегистрированной последовательности | — | — (`NotSupportedException`; upsert без аргументов продолжает применяться) | — | только key upsert, без SQL |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | движкового upsert нет |

## Примечания и ограничения

* **Значения — параметры.** Значение источника никогда не встраивается в SQL; константы становятся именованными параметрами (`@p0`, `$p0`, ...) и переносятся вместе со строками `VALUES`, как в `INSERT`. Правая часть присваивания подчиняется тому же правилу: её константы и замыкания — связанные параметры всей инструкции, а не каждой затронутой строки.
* **Ветки выполняются в порядке объявления.** SQL Server и PostgreSQL вычисляют клаузы `WHEN` сверху вниз; к строке применяется первая подходящая клауза.
* **Условие `WHEN NOT MATCHED BY SOURCE` на SQL Server — только по target.** `MERGE` в SQL Server не предоставляет в этой клаузе алиас источника, поэтому условие может ссылаться только на строку target.
* **Источник — серверный запрос.** `Using(ctx.From<T>().Where(...))` / `Using(QueryCommand<T>)` принимает только форма полного `MERGE`; key-upsert-провайдеры откатываются к батчу `VALUES`.
* **PostgreSQL 15+ — серверная предпосылка.** nextorm не обнаруживает и не настраивает версию сервера и не читает её; он рендерит общий `MERGE` только по флагам возможностей диалекта, а более старый сервер отклоняет саму инструкцию. Клиентской защиты «PostgreSQL < 15 отказывает» нет.
* **Мутации не готовятся и не кладутся в кэш планов.** Оптимизация в nextorm нацелена только на read-only запросы (`Prepare`, неявный кэш планов, бенчмарки); merge всегда рендерит и выполняет одну команду за вызов.
* **Число затронутых строк.** `Merge()`/`MergeAsync()` возвращают число строк, которое сообщает провайдер.
* **Глобальные фильтры запросов и цель merge.** Фильтр, объявленный для типа сущности, не подмешивается
  через `FROM` источника; входящие строки источника каждой ветки поддерживаемого полного `MERGE`
  (insert, update-only и delete-only) проверяются на соответствие активным фильтрам цели до
  выполнения, и нарушение бросает `QueryFilterException`. При активном фильтре полный `MERGE` дополнительно
  изолирует цель внутри инструкции (см.
  [Изоляция цели записи](../advanced/query-filters.md#изоляция-цели-записи)), а нефильтруемый key upsert
  (`ON CONFLICT` / `ON DUPLICATE KEY` / in-memory) **отказывает** с `NotSupportedException`; `IgnoreFilters`
  отключает фильтр от проверки и восстанавливает нативный upsert. См.
  [INSERT и MERGE (проверка)](../advanced/query-filters.md#insert-и-merge-проверка).
* In-memory-провайдер только для чтения: полный `MERGE` бросает `NotSupportedException`; только key-upsert merge применяется к зарегистрированной последовательности в контексте.

## См. также

- [Изменение данных (INSERT)](17-insert-statement.md)
- [Изменение данных (DELETE)](18-delete-statement.md)
- [Изменение данных (UPDATE)](19-update-statement.md)
- [Оптимистичная конкурентность и отслеживание изменений](26-optimistic-concurrency.md)
- [Глобальные фильтры запросов](../advanced/query-filters.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../connecting-providers/overview.md)
- [Краткий справочник API](../advanced/api-reference.md)

# WORM → Nextorm: сравнение примеров публичного API

> Рабочий материал для серии статей: тезисы, кодовые примеры, ссылки. **Это примеры из тестов и документации библиотек, а не найденные production-приложения.** Автор сообщил 27.10.2026, что публичных примеров реального использования WORM не обнаружил; старые проекты могут храниться на других носителях. Их наличие и содержание не подтверждены.
>
> Исходники зафиксированы: WORM — [коммит 15.05.2020](https://github.com/AlexeyShirshov/oml/commit/886f29cda495ae08b3e66c922d68f6e0a54bb8e3); Nextorm — [срез 27.10.2026](https://github.com/AlexeyShirshov/nextorm/commit/15a4d0bca5873525d4a78d034ce879298c04ec3a). Примеры ниже **не обязаны работать друг с другом**: используются разные модели сущностей и тестовые БД.

## 1. Фильтр и получение одной строки

- **WORM:** `QueryCmd` + `SelectEntity` + `Ctor.prop(...).eq(...)` + `Single<T>`.

```vb
Dim q As New QueryCmd()
q.SelectEntity(GetType(Entity))
q.Filter = Ctor.prop(GetType(Entity), "ID").eq(1)
Dim row As Entity = q.Single(Of Entity)(mgr)
```

- **Источник:** [QueryTest.vb](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/QueryTest.vb#L62-L76) — `TestFilter`.
- **Nextorm:** `From<T>()` + лямбда `Where` + терминальный метод.

```csharp
var row = dataContext.From<SimpleEntity>()
    .Where(x => x.Id == 1)
    .Select(x => new { x.Id })
    .Single();
```

- **Источники Nextorm:** [03-filtering-where.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/03-filtering-where.md#L8-L43), [07-subqueries.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/07-subqueries.md#L69-L82) (наличие `Single`); [EntityBuilder.cs](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/EntityBuilder.cs#L502-L520).
- **Сохранена идея:** типизированное описание фильтра без SQL-строки.
- **Изменена форма:** `GetType` + строковый псевдоним свойства → лямбда C#; выделенная проекция вместо `SelectEntity`.

## 2. Фильтр, сортировка, материализация списка

- **WORM:** тест объединяет `Where`, `OrderBy` и `ToList`; затем меняет `q.Sort`, исполняя запрос снова.

```vb
Dim q As QueryCmd = New QueryCmd().SelectEntity(GetType(Entity4)). _
    Where(Ctor.prop(GetType(Entity4), "Title").[like]("b%")). _
    OrderBy(SCtor.prop(GetType(Entity4), "ID"))
Dim rows = q.ToList(Of Entity4)(mgr)
q.Sort = SCtor.prop(GetType(Entity4), "ID").desc
```

- **Источник:** [QueryTest.vb](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/QueryTest.vb#L129-L143) — `TestSort`.
- **Nextorm:** типизированная сортировка; API строителя неизменяемый, `OrderByDescending` не эквивалентен мутации `q.Sort`.

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .OrderBy(x => x.Int)
    .OrderByDescending(x => x.Id)
    .Select(x => new { x.Id })
    .ToListAsync();
```

- **Источники Nextorm:** [06-sorting-and-paging.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/06-sorting-and-paging.md#L10-L69), [EntityBuilder.cs](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/src/nextorm.core/Builders/EntityBuilder.cs#L502-L520).
- **Сохранена идея:** SQL сортировка применяется к модели запроса до выполнения.
- **Изменена семантика:** WORM-тест меняет состояние существующей команды; Nextorm возвращает новые версии билдера. В этом кодовом примере алгоритм сортировки **не идентичен**, показана пара соответствующих механизмов.

## 3. INNER/LEFT JOIN по условию

- **WORM:** `JCtor.join(...).on(...).eq(...)` и `JCtor.left_join`.

```vb
Dim q As New QueryCmd()
q.SelectEntity(GetType(Entity4))
q.Joins = JCtor.join(GetType(Entity)).[on](GetType(Entity4), "ID").eq(GetType(Entity), "ID")
```

- **Источник:** [QueryTest.vb](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/QueryTest.vb#L190-L208) — `TestJoin2`.
- **Nextorm:** соединение строителей по типизированному `(left, right) =>` и проекция из обеих сторон.

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

- **Источник Nextorm:** [04-joins.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/04-joins.md#L38-L53) (в том же руководстве есть `LeftJoin`).
- **Сохранена идея:** конструирование SQL-соединений в запросном API.
- **Изменена форма:** `JCtor` и строковые поля → типизированный предикат и композиция проекций; здесь разные тестовые сущности, а не один исполняемый запрос.

## 4. GROUP BY + HAVING

- **WORM:** `FCtor.prop`, `GCtor.prop`, `Ctor.count`.

```vb
q.Select(FCtor.prop(GetType(Table1), "EnumStr")) _
    .GroupBy(GCtor.prop(GetType(Table1), "EnumStr")) _
    .Having(Ctor.count().eq(2))
```

- **Источник:** [QueryTest.vb](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/QueryTest.vb#L278-L290) — `TestHaving`.
- **Nextorm:** группировка по свойству + условие по `SqlFunctions.Sql.count()`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => e.Int)
    .Having(e => SqlFunctions.Sql.count() > 1)
    .Select(e => new { e.Int, Count = SqlFunctions.Sql.count() })
    .ToList();
```

- **Источник Nextorm:** [05-grouping-and-aggregates.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/05-grouping-and-aggregates.md#L39-L106).
- **Сохранена идея:** `GROUP BY` / `HAVING` как модель SQL, а не сырой SQL.
- **Изменена форма:** специализированные фабрики выражений → лямбды и функции, предназначенные для SQL-трансляции; порог агрегата в примерах **разный**.

## 5. DISTINCT и работа с подзапросами

- **WORM DISTINCT:** `q.Distinct(True).ToList<T>()` — [QueryTest.vb](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/QueryTest.vb#L266-L275).
- **Nextorm DISTINCT:** `From<T>().Select(...).Distinct().ToList()` — [09-distinct.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/09-distinct.md#L7-L47).
- **Идея:** дедупликация на SQL-уровне, но сравнивать нужно выбранные колонки (не идентичные модели результата).

- **WORM FROM-подзапрос:** `q.From(inner).ToList(Of Table1)()` — [InnerCmds.vb](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/InnerCmds.vb#L85-L99).
- **Nextorm FROM-подзапрос:** `dataContext.From(nested).Select(t => new { t.Id }).ToList()` — [07-subqueries.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/07-subqueries.md#L25-L44).
- **Идея:** вложенный запрос используется как источник другого запроса.
- **Важное отличие:** WORM-тест дополнительно проверяет состояния/загрузку сущностей; Nextorm не имеет change tracking.

## 6. Вычисляемые проекции

- **WORM:** `FCtor.prop` и `ECtor.prop(...)+ "-xx"` с псевдонимом `Title` — [QueryExpressions.vb](https://github.com/AlexeyShirshov/oml/blob/886f29cda495ae08b3e66c922d68f6e0a54bb8e3/TestProject1/Query/QueryExpressions.vb#L51-L69).
- **Nextorm:** вычисляемое поле прямо в лямбде C#, материализация анонимного типа — [02-projections.md](https://github.com/AlexeyShirshov/nextorm/blob/15a4d0bca5873525d4a78d034ce879298c04ec3a/docs/guide/02-projections.md#L31-L63).
- **Идея:** клиент описывает выражение; СУБД вычисляет его как часть `SELECT`.
- **Не утверждать:** эквивалентный SQL или семантика `null` без проверки на одинаковой СУБД.

## Границы сопоставления и последующие исследования

- **Есть:** примеры вызовов из WORM-тестов, работающие аналоги из документации Nextorm, ссылки на конкретные строки.
- **Нет:** сохранившегося публичного кода производственных приложений на WORM; одинаковых тестовых данных; запуска тестов обеих систем; замеров времени/памяти этих пар.
- **Следующий уровень:** взять один тест WORM, воспроизвести схему/данные в Nextorm и сравнить генерируемый SQL, результат и затраты без замены измерений ожиданиями.
- **Не нужно спрашивать автора по памяти:** названия прежних методов API подтверждаются тестами.
- **Возможный вопрос автору лишь после тестового анализа:** какие изменения API мотивированы опытом сопровождения, а какие — ограничениями/возможностями языка и платформы.

# NativeAOT (частичная поддержка)

> Публикуйте приложения с `PublishAot=true` на .NET 11 для `linux-x64`.
> Поддержка **частичная и экспериментальная**: нативно исполняется измеренное подмножество
> движка, остаток отслеживается в [#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

**Предварительно:** [Подключения](01-connections.md) · [Конвертеры значений и JSON-колонки](03-value-converters.md).

## Что проверено

Опубликованный нативный бинарник работает с реальной базой, а его результат построчно сравнивается
с JIT-оракулом. Это делают два инструмента: SQLite-раннер (`tests/nextorm.aot.runner` под
управлением `AotNativeRunTests`) и пробники провайдеров в `examples/` плюс самопровижнящий
`tests/nextorm.aot.sqlprobe` для MySQL и MariaDB. Гейт **fail-closed**: нативный сценарий, который
упал или чьи количество строк/digest расходятся с JIT-контролем, валит тест.

| Область | Статус |
|---|---|
| Цель | `net11.0`, `linux-x64` |
| Провайдеры | SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse (измеренная поверхность различается, см. ниже) |
| Поверхность запросов | типизированные и анонимные проекции, фильтры, сортировка, пейджинг, агрегаты, `GROUP BY`/`HAVING`, join'ы, CTE, оконные функции (где провайдер их поддерживает), JSON-функции SQLite, массивы ClickHouse, `SelectGroup`/`SelectGroupHaving`, `SelectWhereMin`/`SelectWhereMax`, повторное использование подготовленных команд |
| Паритет | каждый сценарий исполняется ещё и под JIT; гейт падает при любом расхождении статуса или значений |

Измеренные нативные результаты (значения JIT совпадают везде):

| Провайдер | Инструмент | Нативный результат |
|---|---|---|
| SQLite | `tests/nextorm.aot.runner`, 14 сценариев + 4 компонентных среза | **14/14** |
| PostgreSQL | `examples/nextorm.aotexamples.postgres.aviasales`, 10 запросов | **10/10** |
| SQL Server | `examples/nextorm.aotexamples.mssql.adventureworks`, 11 запросов | **11/11** |
| MySQL | `tests/nextorm.aot.sqlprobe --provider mysql`, 7 форм | **7/7** |
| MariaDB | `tests/nextorm.aot.sqlprobe --provider mariadb`, 7 форм | **7/7** |
| ClickHouse | `examples/nextorm.aotexamples.clickhouse.analytics`, 9 запросов | **9/9** |

На выходе — самодостаточный нативный исполняемый файл. Пакеты пока **не** несут маркер
`IsAotCompatible`, и эта страница не является гарантией zero-configuration.

## Что не проверено

- **Коррелированные подзапросы покрыты только для `Any` и `Count`.** Обе формы исполняются в
  нативном раннере и дают значения, совпадающие с JIT. Коррелированные проекция/навигация,
  корреляция глубже одного уровня и коррелированные источники `APPLY` пока не входят в измеренную
  нативную поверхность.
- **Tuple-колонка ClickHouse** с типами элементов вне фиксированной регистрации драйвера
  закрывается fail-closed понятным `NotSupportedException` на подготовке; проецируйте элементы
  кортежа отдельными колонками или читайте кортеж текстом.
- Мост EF Core (`nextorm.entityframeworkcore`), конвертеры `System.Text.Json` через
  сгенерированный `JsonTypeInfo<T>` (сериализация остаётся рефлексионной, поэтому потребителю
  по-прежнему нужен обычный `JsonSerializerContext`), bulk insert, маппинг сырого SQL и DML-билдеры
  не входят в измеренную нативную поверхность.
- Trimming сверх настроек провайдера по умолчанию.

### Пробники провайдеров

Пробники в `examples/` запускают nextorm-арм каждого примера-сравнения без linq2db, Testcontainers и
BenchmarkDotNet, поэтому процесс публикуется нативно и сравнивается с JIT-прогоном по количеству
строк и digest'ам строк. Пробник MySQL/MariaDB сам создаёт крошечную схему и наполнение, поэтому
набор данных ему не нужен. Замер 2026-11-13, `linux-x64`:

| Пробник | Нативный бинарник | Результат |
|---|---|---|
| `nextorm.aotexamples.postgres.aviasales` | 18 МБ | **10/10** запросов, digest'ы совпадают с JIT |
| `nextorm.aotexamples.mssql.adventureworks` | 28 МБ | **11/11** |
| `nextorm.aotexamples.clickhouse.analytics` | 16 МБ | **9/9** |
| `tests/nextorm.aot.sqlprobe` (MySQL / MariaDB) | — | **7/7** каждый |

Три класса сбоев, которые изначально показывали пробники — операторы `decimal`, вырезанные
trimming'ом, конструктор `System.Tuple`, найденный рефлексией, и компиляция деревьев выражений в
рантайме — исправлены и больше не наблюдаются ни в одном пробнике.

## Остаточные диагностики

Канонический publish нативного раннера даёт **198 уникальных AOT/trim-диагностик** (204 сырых;
измерено 2026-11-13). Они не подавлены и не заroot'ены:

| Код | Кол-во | Природа |
|---|---|---|
| `IL3050` | 107 | генерация кода в рантайме — `Expression.Lambda`, `Type.MakeGenericType`, `MethodInfo.MakeGenericMethod`, `Expression.NewArrayInit`, `System.Text.Json` |
| `IL2026` | 26 | API с `RequiresUnreferencedCode` |
| `IL2070` | 26 | рефлексия по `Type`, известному только в рантайме |
| `IL2075` | 15 | доступ к члену значения, типизированного в рантайме |
| `IL2060` | 12 | статически неанализируемая generic-инстанциация |
| `IL2090` | 5 | generic-параметр с требованиями `DynamicallyAccessedMembers` |
| `IL2072` | 3 | требование `DynamicallyAccessedMembers` не выполнено |
| `IL2055` | 2 | `MakeGenericType` по типу, известному в рантайме |
| `IL2111` | 1 | рефлексия по типу с аннотациями |
| `IL2067` | 1 | параметр с аннотацией `DynamicallyAccessedMembers` |

Остаток архитектурный: пайплайн запросов носит тип результата и проекции как `Type`, известный
только в рантайме (`QueryCommand.ResultType`, `ProjectionEntityItem.EntityType`), и разрешает по нему
часть generic-членов. Одними аннотациями эти строки не убрать; compile-time пайплайн и нативная
матрица провайдеров — область задачи
[#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

## Проверка на реальном приложении

`oc-report` — утилита отчётов по базе SQLite на 13 ГБ — работает нативно без правок исходников,
кроме `PublishAot=true`. Публикация и прогон выполнены на одной машине (тёплый page cache, медиана
трёх прогонов, окно 3 дня):

| Реализация | AOT publish | Нативный бинарник | Прогон | Пик RSS |
|---|---|---|---|---|
| nextorm, типизированные запросы | только `PublishAot=true` | 8.2 MB | 0.12 s | 30 MB |

Эталонное приложение —
[`examples/nextorm.examples.oc-report`](https://github.com/AlexeyShirshov/nextorm/tree/main/examples/nextorm.examples.oc-report).

## Как попробовать

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
```

```bash
dotnet publish -c Release
```

Действуют стандартные требования NativeAOT (`clang`, `zlib1g-dev`, нативный тулчейн под RID).
Используйте одного из проверенных провайдеров и оставайтесь в измеренной поверхности запросов выше.
Запрос вне её может упасть в рантайме — на этом этапе это ожидаемо.

## Как сообщить о проблеме

Откройте issue с минимальным падающим запросом, целевым RID, версией .NET SDK и диагностикой
publish. Нативный reproducer ценнее JIT-ового: ошибка из-за trimming проявляется только после
публикации.

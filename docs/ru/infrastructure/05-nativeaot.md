# NativeAOT (частичная поддержка)

> Публикуйте приложения с `PublishAot=true` на .NET 11 для `linux-x64` и провайдера SQLite.
> Поддержка **частичная и экспериментальная**: нативно исполняется измеренное подмножество
> движка, остаток отслеживается в [#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

**Предварительно:** [Подключения](01-connections.md) · [Конвертеры значений и JSON-колонки](03-value-converters.md).

## Что проверено

Опубликованный нативный бинарник работает с реальной базой SQLite, а его результат построчно
сравнивается с JIT-оракулом (`tests/nextorm.aot.runner` под управлением `AotNativeRunTests`).
CI-job `aot` публикует и запускает его на каждый push, а job `publish` требует этот гейт.

| Область | Статус |
|---|---|
| Цель | `net11.0`, `linux-x64` |
| Провайдеры | SQLite |
| Поверхность запросов | сущности-интерфейсы, захваченные предикаты, анонимные проекции, материализация nullable-скаляров, JSON-функции SQLite (`json_extract`/`json_get_text`), `GROUP BY` с агрегатами и `HAVING` |
| `nextorm.extensions` | компонентные срезы `SelectWhereMin`/`SelectWhereMax` и `SelectGroup`/`SelectGroupHaving` |
| Паритет | каждый сценарий исполняется ещё и под JIT, результаты сравниваются |

На выходе — самодостаточный нативный исполняемый файл. Пакеты пока **не** несут маркер
`IsAotCompatible`, и эта страница не является гарантией zero-configuration.

## Что не проверено

- SQL Server, PostgreSQL, MySQL, MariaDB и ClickHouse: поддержка AOT драйверами и нативные
  ассеты не проверены;
- мост EF Core (`nextorm.entityframeworkcore`);
- конвертеры `System.Text.Json` через сгенерированный `JsonTypeInfo<T>`: сериализация остаётся
  рефлексионной, поэтому потребителю по-прежнему нужен обычный `JsonSerializerContext`;
- trimming сверх настроек провайдера по умолчанию.

Канонический publish нативного раннера сейчас даёт **230 уникальных AOT/trim-диагностик**
(236 сырых; измерено 2026-11-07). Они не подавлены и не заroot'ены:

| Код | Кол-во | Природа |
|---|---|---|
| `IL3050` | 124 | генерация кода в рантайме — `Expression.Lambda`, `Type.MakeGenericType`, `MethodInfo.MakeGenericMethod`, `Expression.NewArrayInit`, один `Reflection.Emit`, `System.Text.Json` |
| `IL2070` | 30 | рефлексия по `Type`, известному только в рантайме |
| `IL2026` | 26 | API с `RequiresUnreferencedCode` |
| `IL2060` | 22 | статически неанализируемая generic-инстанциация |
| `IL2055`/`IL2067`/`IL2072`/`IL2075`/`IL2090`/`IL2111` | 28 | метаданные, которые могут быть вырезаны |

Остаток архитектурный: пайплайн запросов носит тип результата и проекции как `Type`, известный
только в рантайме (`QueryCommand.ResultType`, `ProjectionEntityItem.EntityType`), поэтому одними
аннотациями эти строки не убрать. Compile-time пайплайн и нативная матрица провайдеров — область
задачи [#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

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
Используйте провайдер SQLite и оставайтесь в проверенной поверхности запросов выше. Запрос вне её
может упасть в рантайме — на этом этапе это ожидаемо.

## Как сообщить о проблеме

Откройте issue с минимальным падающим запросом, целевым RID, версией .NET SDK и диагностикой
publish. Нативный reproducer ценнее JIT-ового: ошибка из-за trimming проявляется только после
публикации.

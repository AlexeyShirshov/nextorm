# WIP: Capability-объекты диалекта (устранение расхождения `Supports*` ↔ `Make*`)

> Design RFC / рабочий план. Источник: реестр `docs/specs/design/API-NAMING-REVIEW.md` —
> точечные аудиты 19–20.09.2026 (находки IF5/IF6 и серия LIM1/FM5/XP7/TF1/Z1/CNT1/GLI2).

## Пункт и цель

- Проблема: в `ISqlDialect` поддержка возможности выражена **двумя независимыми фактами** —
  булевым флагом `SupportsX` и методом-рендерером `MakeX`. Диалект может объявить `SupportsX => true`
  и не предоставить реализацию: тогда `MakeX` унаследует throwing-заглушку из `SqlDialectBase` и
  упадёт в рантайме.
- Цель: сделать такое расхождение **невозможным by construction**; убрать throwing-заглушки из базы;
  сделать добавление новой возможности не-source-breaking для внешних реализаторов `ISqlDialect`.
- Критерий приёмки (общий): не существует диалекта, который сообщает о поддержке возможности, но
  бросает при её рендере.

## Текущее состояние (инвентаризация)

Все ~55 флагов `Supports*` делятся на три класса; риск «заявлено, но не реализовано» есть только у
первого.

| Класс | Примеры | Риск |
|---|---|---|
| **A. База бросает** — override обязателен | `MakeIif`, `MakeSessionInfoFunction`, `MakeUuidGenerator`, `MakeLimitBy`; `protected` `MakeStringPosition`, `MakeStringReverse` | **есть** |
| **B. База даёт рабочий ANSI-дефолт** | `MakeGreatest`/`MakeLeast`, `MakeDateTrunc`, `MakeDateAdd`/`MakeDateDiff`, `MakeNullIf`, `MakeCoalesce` | нет (дефолт корректен) |
| **C. Флаг без `Make*`** — гейтит рендер в визиторах | `SupportsRollup`/`Cube`/`GroupingSets`, `SupportsPercentRankCumeDist`/`NthValue`, `SupportsFinal`/`Sample`/`PreWhere`, `SupportsGlobalPredicates` | другой (неверный флаг, а не отсутствие метода) |

Дополнительно: `SupportsSessionInfoFunction(name)` и `SupportsUuidGenerator(name)` —
*поимённые* предикаты при грубом парном флаге; точность даёт предикат, а не флаг.

Промежуточная мера уже внесена (не заменяет стратегию):

- `ISqlDialect.MakeIif` стал **default interface method** с throwing-телом — source-breaking для
  внешних реализаторов снят (вариант A из IF5).
- Контрактный тест `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs` проверяет
  рефлексией: `SupportsIif`/`SupportsSessionInfoFunctions`/`SupportsUuidGenerators`/`SupportsLimitBy`
  ⇒ соответствующий `Make*` переопределён (не унаследован от `SqlDialectBase`).

## Дизайн: capability-объект

Идея: слить «флаг» и «рендерер» в один объект; `null`/отсутствие объекта означает
«не поддерживается». Тогда «объявлено» и «реализовано» — это один и тот же факт.

```csharp
// Было: два независимых члена, которые могут разойтись
bool SupportsIif { get; }
string MakeIif(string condition, string whenTrue, string whenFalse);

// Стало: возможность — это объект; нет объекта — нет поддержки
IIifRenderer? Iif { get; }

public interface IIifRenderer
{
    string Render(string condition, string whenTrue, string whenFalse);
}
```

Поставщик даёт stateless-синглтон:

```csharp
internal sealed class PostgresIifRenderer : IIifRenderer
{
    public static readonly PostgresIifRenderer Instance = new();
    public string Render(string condition, string whenTrue, string whenFalse) =>
        $"case when {condition} then {whenTrue} else {whenFalse} end";
}

// PostgresDialect
public IIifRenderer? Iif => PostgresIifRenderer.Instance;
```

Совместимость на переходный период: `SupportIif` можно оставить вычисляемым алиасом
(`bool SupportsIif => Iif is not null`), удалив его на этапе заморозки (Фаза 3).

Варианты формы (обсуждаемо):

| Вариант | Форма | Плюсы | Минусы |
|---|---|---|---|
| A. По интерфейсу на возможность | `IIifRenderer? Iif`, `IUuidRenderer? Uuid`, … | типизировано, discoverable, XML-доки на каждый объект | много мелких типов |
| B. Агрегат `DialectCapabilities` | один объект с nullable-делегатами/свойствами | компактно, один член в `ISqlDialect` | слабее типизация, сложнее документировать |

Для **поимённых** семейств capability-объект должен нести и предикат, и рендер — это убирает
рассогласование «грубый флаг vs точный предикат»:

```csharp
public interface ISessionInfoFunctions
{
    bool Supports(string name);
    string Render(string name);
}
```

Класс C (флаги без `Make*`) можно оставить `bool`, если рендер полностью универсален; иначе —
тоже оформить capability-объектом с политикой/стратегией рендера.

## Область и этапы

- **Фаза 1 — точечно, без ломания API.** Ввести capability-объекты для класса A (`iif`, session-info,
  uuid, limit-by), оставив `Supports*`/`Make*` делегатами к ним; расширить контрактный тест на
  «capability-объект не null ⇒ `Render` не бросает».
- **Фаза 2 — группы.** Перевести оконные/агрегатные/условные флаги (`SupportsPercentRankCumeDist`,
  `SupportsNthValue`, `SupportsAnyValueAggregate`, `SupportsQuantileAggregates`, …) на capability-объекты;
  для класса B — по желанию, риска нет.
- **Фаза 3 — заморозка (IF6).** Удалить устаревшие пары `Supports*`/`Make*` (RS0017+RS0016), подключить
  `PublicApiAnalyzers`/`ApiCompat`, зафиксировать поверхность в `PublicAPI.Shipped/Unshipped.txt`.
- **Не трогать:** флаги класса B и класс C, где нет риска и рендер универсален.

## Влияние и совместимость

- Публичный API: `ISqlDialect` существенно меняется — это стадия **заморозки** (альфа-политика §1);
  без неё изменения source/binary-breaking для внешних реализаторов.
- План-кэш и производительность: capability-объекты — singleton на диалект (диалект тоже singleton),
  ключи план-кэша не меняются; вызов `Render` — прямой интерфейсный вызов вместо строкового `Make*`.
- Тесты: контрактный тест становится общим инвариантом «нет null-объекта с бросающим рендером»;
  провайдерские SQL-shape тесты остаются без изменений.

## Критерии приёмки

- Нет пути, при котором диалект заявляет поддержку, а рендер бросает: capability-объект либо есть
  и работает, либо `null` (и вызывающий код отклоняет конструкцию до рендера).
- Добавление новой возможности в `ISqlDialect` не ломает внешние реализации (nullable-объект вместо
  абстрактного члена).
- В `SqlDialectBase` не остаётся throwing-заглушек для возможностей (A).
- Release build 0/0, все unit- и интеграционные тесты зелёные, контрактный тест покрывает класс A.

## Открытые вопросы

- Именование объектов: `IIifRenderer` vs `IifCapability` vs делегат `Func<…, string>`?
- Один aggregate-объект против интерфейса на возможность (вариант A vs B).
- Сохранять ли `Supports*` как вычисляемые алиасы на переходный период или удалить сразу при заморозке.
- Нужны ли capability-объекты для класса C или достаточно bool.
- Порядок относительно заморозки IF6 (зависимость: форма поверхности определяет содержимое
  `PublicAPI.Unshipped.txt`).

## Ссылки

- Реестр: `docs/specs/design/API-NAMING-REVIEW.md` (IF5 — закрыт DIM+тест; IF6 — заморозка открыта).
- Контрактный тест: `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs`.
- Класс A: `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs` (`MakeIif`, `MakeSessionInfoFunction`,
  `MakeUuidGenerator`, `MakeLimitBy`, `MakeStringPosition`, `MakeStringReverse`).
- Контракт: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`.

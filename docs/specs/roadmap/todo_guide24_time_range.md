# TODO: глава 24 — диапазон MySQL/MariaDB `TIME` в деталях

> Рабочий план (design RFC). Источник: незакрытый `Deferred` статус-файла
> `docs/specs/status/guide-24-sql-1.md` (docs-поток закрыт, файл удалён 2026-09-27). Tracking issue:
> [#102 «TODO: глава 24 — диапазон MySQL/MariaDB TIME»](https://github.com/AlexeyShirshov/nextorm/issues/102),
> milestone `1.0.9-a`.

## Пункт и цель

Дописать в §«Details and limitations» / «Детали и ограничения» главы 24
(`docs/guide/24-duration-columns.md`, `docs/ru/guide/24-duration-columns.md`) ограничение нативной
колонки `TIME` в **MySQL/MariaDB**: её диапазон `−838:59:59`…`838:59:59` (≈ ±34.9 суток), тогда как
`TimeSpan` выражает куда больший диапазон. Практический вывод: для длительностей за пределами
диапазона `TIME` нужна целочисленная колонка (как это уже сделано для SQL Server `time`).

Это чисто документная деталь к уже реализованной поддержке duration-колонок; рантайм не трогаем.

## Объём

- **EN** — новый bullet в `docs/guide/24-duration-columns.md` (раздел «Details and limitations», ~`:95`),
  рядом с пунктами про PostgreSQL `interval` (`:97`) и SQL Server `time` (`:98`).
- **RU** — симметричный bullet в `docs/ru/guide/24-duration-columns.md` (раздел «Детали и ограничения», ~`:95`).
- EN и RU структурно идентичны; прочие разделы/примеры не меняются.

## Критерий приёмки

- Оба файла содержат пункт с точным диапазоном `−838:59:59`…`838:59:59` и выводом «для больших
  длительностей использовать целочисленную колонку».
- EN/RU паритет (одинаковое число bullets в разделе, один и тот же смысл).
- `dotnet docfx docs/docfx.json` — 0 warnings / 0 errors.
- CRLF сохранён; LF-only строк нет.

## Тест-стратегия

Docs-only: unit/integration не пишем. Верификация — `dotnet docfx docs/docfx.json` (0/0) + визуальная
EN/RU-симметрия.

## Перф-замер

Не требуется: markdown-only, рантайм-путь не затронут.

## Вне области

- Внешние закладки/redirects на старые URL главы (в исходном статусе помечено «вне цикла»).
- Изменения генерации типов колонок (`ISqlDialect.MakeDurationType`/`MakeNullableDurationType`) —
  поведение уже верное, правится только проза.

## Файлы к изменению

- `docs/guide/24-duration-columns.md`
- `docs/ru/guide/24-duration-columns.md`

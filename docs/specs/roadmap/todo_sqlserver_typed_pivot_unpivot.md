# TODO: типизированная проекция результата SQL Server `PIVOT`/`UNPIVOT`

> Tracking issue: [#232](https://github.com/AlexeyShirshov/nextorm/issues/232) — OPEN,
> label `enhancement`. Milestone: **`1.0.9-rc3`**.
>
> Цикл: `T232` (`N=1`, ревизия плана `r=1`, контракт `rv=1`).
> Статус-файл цикла (источник истины исполнения и приёмки):
> `docs/specs/status/D232-sqlserver-pivot-unpivot-1.md`.
> Публичные страницы на `docs/specs/**` не ссылаются.

## 0. Назначение

Сегодня SQL Server `Pivot`/`Unpivot` возвращают `EntityBuilder<TableAlias>`, и результат
читается по имени колонки (`t.GetNullableDecimal("[1]")`). Задача — объявить члены
формы и CLR-типы ячеек **один раз**, вывести значения PIVOT из проекции, корректно
эмитировать алиасы и вернуть полноценный `EntityBuilder<TShape>`, пригодный в
`Where`/`OrderBy`/`Select`, производных источниках и CTE.

Утверждённые (неизменяемые) сигнатуры — в статус-файле, §Goal. Ниже — план срезов;
иммутабельные идентификаторы приёмки (`AC-01..AC-12`) и единиц (`D-01..D-07`) берутся
из статус-файла и здесь не переопределяются.

## 1. Срезы (DO units)

Соответствие `D-01..D-07` и `AC-01..AC-12` — по таблице «DO units» и таблице `E232-*`
статус-файла. Сводка:

| Срез | Объём | Критерии |
|---|---|---|
| **D-01** Preparation probes | baseline inventory/build/perf/coverage; исполняемые probes жизненного цикла имени, эмиссии алиасов и порядка регистрации источника | AC-03, AC-08, AC-11, AC-12 |
| **D-02** Test contract | заморозка общего oracle/fixture-контракта; legacy-vs-typed колоночный oracle; тестовые данные | AC-01…AC-04, AC-07…AC-09 |
| **D-03** Core markers | документированные core-маркеры, translation-only поведение, узкое распознавание и `TableAlias`-binding | AC-05, AC-07, AC-09 |
| **D-04** Provider overloads | утверждённые overload'ы, identity-rewriter, реальные имена группировки, ordered extraction/guards; legacy-конструкция + `Select` | AC-01…AC-04, AC-07…AC-09 |
| **D-05** Integration/mutation | SQL Server parity/TVF/CTE/join/cache, all-provider capability, branch closure и шесть мутантов | AC-02, AC-07…AC-09, AC-11 |
| **D-06** Docs/example | типизированный пример, двуязычная семантика, XML completion и регистр | AC-04, AC-05, AC-10 |
| **D-07** Boundary/handoff | единственный boundary, coverage/perf оценка, sealing evidence, CHECK и авторизованный handoff | AC-01…AC-12 |

Срезы исполняются **последовательно** в текущем дереве (одна группа коллекции; без
веток/worktree/merge/push). Отображение «unit → evidence row» (`E232-01..E232-17`),
вариантная матрица (`V01..V25`) и каталог вызовов (`C01..C19`) — в статус-файле.

## 2. Границы (read-only под `r=1`)

Подтверждённая граница неизменяемого кода (правка любого файла = возврат в PLAN):
`PivotExpression.cs`, `BaseExpressionVisitor.cs`, `TableAliasAccessors.cs`,
`SqlSourceRenderer.cs`, `SqlBuilder.cs`, `SqlFunctions.cs`, `Dialect/**`, `Builders/**`,
project/package-файлы и существующие 14 тестов. Новые файлы (маркеры, rewriter, тесты)
правятся в рамках своих срезов.

## 3. Доказательства

Контракт `rv=1`, frozen manifest `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/manifest.json`
и весь каталог вызовов — в статус-файле. `verify.py` в каталоге evidence —
task-local plumbing (не продуктовый фреймворк): разворачивает argv/cwd, пишет
stdout/stderr/exit-коды и provenance/contract/counters JSON.

## 4. Отложено + триггер

- #53 PublicAPI freeze / `PIV-RD2` — существующее владение; триггер: возобновление #53
  (добавить обе новые публичные поверхности в его кандидаты).
- Типизированные runtime-generated dynamic-shape — вне утверждённого объёма; триггер:
  явное новое требование. Legacy API остаётся доступным.
- Repository-wide Stryker — вне объёма; триггер: одобренная инфраструктурная работа.
  Targeted mutation остаётся обязательной.

Любой нереализованный срез #232 остаётся активным в **`1.0.9-rc3`**; перенос в будущий
milestone запрещён (сплит — отдельная единица/issue с текущим milestone).

# План релиза 1.0.9-rc3 (milestone 1.0.9-rc3)

> Основа — предыдущие релизы (см. `docs/specs/release-1.0.9-rc2.md`, `git show v1.0.9-rc2`).
> Ядро прежнее: release notes в `docs/index.md`, строка `Status`, бамп `<Version>` в
> `Directory.Build.props` (одна строка на все 7 пакетов), тег `v1.0.9-rc3` → tag-triggered
> CI-публикация 7 пакетов (OIDC, `NuGet/login@v1`).
>
> **Статус: подготовка выполнена; публикация (§4) — за владельцем репозитория.**

- **Milestone:** [1.0.9-rc3](https://github.com/AlexeyShirshov/nextorm/milestone/4) — 39 issues, все закрыты (open=0, closed=39): 34 `completed`, 5 `not_planned` (#297, #244, #228, #210, #201 — принятые ограничения).
- **Тег:** `v1.0.9-rc3` (планируется)
- **Ветка релиза:** `1.0.9-rc3` (синхронна с `origin/1.0.9-rc3`)
- **База:** тег `v1.0.9-rc2` (`58025147`); HEAD до подготовки — `180ba117`, 343 коммита от `v1.0.9-rc2`.
- **Версии пакетов:** `1.0.9-rc3` задаётся **одной строкой** в `Directory.Build.props` (`<Version>`);
  CI переопределяет её тегом через `-p:Version` — источник истины тег; центральный бамп нужен
  локальному `dotnet pack` и зависимостям nuspec (`nextorm.mariadb → nextorm.mysql 1.0.9-rc3`).
- **Публикация:** CI (`dotnet.yml`, job `publish`), tag-triggered (`refs/tags/v*`), NuGet trusted
  publishing (OIDC) — без изменений с 1.0.4–1.0.9-rc2.

## 1. Скоуп милстоуна

| # | Release notes |
| --- | --- |
| [#275](https://github.com/AlexeyShirshov/nextorm/issues/275) | SQL Server: `OPTION (MAXRECURSION n)` больше не теряется при хоистинге рекурсивного CTE в источник мутации |
| [#265](https://github.com/AlexeyShirshov/nextorm/issues/265) | SQL Server: legacy `WithRecursive` с отличным `UNION` отклоняется fail-closed вместо невалидного T-SQL |
| [#243](https://github.com/AlexeyShirshov/nextorm/issues/243) | Perf: аллокации warm-path `Count`/`Sum` снижены (Count 2979.84 → 408 B/op, Sum 3389.44 → 970 B/op) |
| [#242](https://github.com/AlexeyShirshov/nextorm/issues/242) | Evidence pipeline: канонический global `pdca-dotnet` admission и свёртка `required_rows` в покрытие |
| [#241](https://github.com/AlexeyShirshov/nextorm/issues/241) | Единообразная обработка отмены при построении сортировки/группировки |
| [#240](https://github.com/AlexeyShirshov/nextorm/issues/240) | Perf: снижена стоимость конструирования fluent-команды (отдельный скоуп/дизайн) |
| [#239](https://github.com/AlexeyShirshov/nextorm/issues/239) | D233/T233: обновлены устаревшие `file:line` в `API-NAMING-REVIEW.md` |
| [#238](https://github.com/AlexeyShirshov/nextorm/issues/238) | PDCA skill: ложные `incomplete`/`STOP` из-за дополнительных audit-provenance gates больше не возникают |
| [#237](https://github.com/AlexeyShirshov/nextorm/issues/237) | Identifier safety: экранирование скобок в SQL Server и raw-имена `UNPIVOT`/`Column<T>` |
| [#236](https://github.com/AlexeyShirshov/nextorm/issues/236) | Build blocker: устранены NU1902/NU1903 для транзитивного `System.Security.Cryptography.Pkcs` |
| [#235](https://github.com/AlexeyShirshov/nextorm/issues/235) | Evidence caveats из T218: сертификация E07, allocation-oracle E08, запись sweep-cap E10 |
| [#234](https://github.com/AlexeyShirshov/nextorm/issues/234) | Evidence pipeline hardening: атомарный генератор evidence и линт устаревших хэшей |
| [#233](https://github.com/AlexeyShirshov/nextorm/issues/233) | Window functions: перегрузка `Over` с одним `partitionBy` и одним `WindowOrder`, без массивов |
| [#232](https://github.com/AlexeyShirshov/nextorm/issues/232) | SQL Server `PIVOT`/`UNPIVOT`: типизированная проекция результата вместо `TableAlias` по имени |
| [#230](https://github.com/AlexeyShirshov/nextorm/issues/230) | Join aliases: alias-цепочка, использованная в следующем запросе, больше не теряет нижестоящие цепочки |
| [#229](https://github.com/AlexeyShirshov/nextorm/issues/229) | Join aliases: anonymous-projection цепочка выдаёт диагностику вместо невалидного C# |
| [#227](https://github.com/AlexeyShirshov/nextorm/issues/227) | Flaky ClickHouse integration test `FloatNativeParity_GroupedSingleFloat64` стабилизирован |
| [#226](https://github.com/AlexeyShirshov/nextorm/issues/226) | `ProcedureResult`/`BatchResult`: раскрыто число затронутых строк (`RecordsAffected`) |
| [#225](https://github.com/AlexeyShirshov/nextorm/issues/225) | JoinInto: composite-junction M:N — документационный дельта-набор |
| [#224](https://github.com/AlexeyShirshov/nextorm/issues/224) | SQL-форматирование: `from`/`join` с новой строки |
| [#223](https://github.com/AlexeyShirshov/nextorm/issues/223) | JoinInto: composite-junction M:N — SQL link carrier |
| [#222](https://github.com/AlexeyShirshov/nextorm/issues/222) | Джойны: перегрузки без явного источника (`Join`/`LeftJoin`/`RightJoin`/`FullJoin` по типу) |
| [#221](https://github.com/AlexeyShirshov/nextorm/issues/221) | Интерцепторы: регистрация через DI (generic/factory-перегрузки) |
| [#220](https://github.com/AlexeyShirshov/nextorm/issues/220) | Документация: выделен раздел «Движок» (Engine), Querying растворён в Guide |
| [#219](https://github.com/AlexeyShirshov/nextorm/issues/219) | Session/server-identity функции: `SERVER_NAME`, `HOST_ID`/`HOST_NAME`, `APP_NAME`, `SUSER_NAME` и аналоги по провайдерам |
| [#218](https://github.com/AlexeyShirshov/nextorm/issues/218) | SQL Server последовательности (`NEXT VALUE FOR`): недостающая кросс-провайдерная поддержка |
| [#217](https://github.com/AlexeyShirshov/nextorm/issues/217) | Полноценная поддержка `DateTimeOffset`: трансляция `Now`/`UtcNow` и частей даты |
| [#216](https://github.com/AlexeyShirshov/nextorm/issues/216) | Row locking: разведены лок всего запроса (`FOR UPDATE`) и `FOR UPDATE OF` на конкретный источник |
| [#215](https://github.com/AlexeyShirshov/nextorm/issues/215) | Убрана зависимость OneOf |
| [#213](https://github.com/AlexeyShirshov/nextorm/issues/213) | TODO: JoinInto — составные ключи, составные junction-селекторы и M:N под single-query |
| [#212](https://github.com/AlexeyShirshov/nextorm/issues/212) | TODO: eager-load nested — level 2+ `LoadWith` |
| [#211](https://github.com/AlexeyShirshov/nextorm/issues/211) | TODO: eager-load per-parent `Take`/`Skip` — коррелированная детская выборка |
| [#209](https://github.com/AlexeyShirshov/nextorm/issues/209) | TODO: eager-load strategy — явный выбор стратегии загрузки и авто-диспетчер |
| [#207](https://github.com/AlexeyShirshov/nextorm/issues/207) | Единообразная обработка отмены при построении проекции |

Принятые ограничения (issue закрыты как `not_planned`):

| # | Причина |
| --- | --- |
| [#297](https://github.com/AlexeyShirshov/nextorm/issues/297) | COW/shared-description для структурного CTE-графа: аллокации снижены, но время не уложилось в ≤1.00× ни на одном варианте — изменение откачено (`f899e35a`) |
| [#244](https://github.com/AlexeyShirshov/nextorm/issues/244) | Остаточные аллокации warm CTE/RecursiveCte приняты: Cte 1.18–1.22×, RecursiveCte 1.59–1.61×, время 0.55–0.61×, `*_Warm_Reused` 0 B; bounded-кандидата нет |
| [#228](https://github.com/AlexeyShirshov/nextorm/issues/228) | Диаграмма пути команды: правка применена напрямую, отдельный трекинг не нужен |
| [#210](https://github.com/AlexeyShirshov/nextorm/issues/210) | Гарантия порядка eager-load свёрнута в #211 (ordering — часть API per-parent top-N) |
| [#201](https://github.com/AlexeyShirshov/nextorm/issues/201) | Паритет cached-пути M12 #3: критерий по аллокациям не достигнут, остаток вынесен в #240 |

**Breaking changes (consumer-visible):** намеренных удалений публичных сигнатур нет.
Поведенческие изменения: канонический SQL-текст `from`/`join` с новой строки (#224); fail-closed
для невалидного legacy `WithRecursive` на SQL Server (#265); исправленное экранирование
pre-quoted имён (#237); `OPTION (MAXRECURSION n)` больше не теряется (#275). #215 меняет только
транзитивный граф пакетов (публичные сигнатуры неизменны), #232 — аддитивная типизированная
проекция при сохранённом legacy `TableAlias`-пути, #216 — аддитивные lock-опции.

Новые страницы документации: раздел `docs/engine/**` (+RU), `docs/relationships/**` (+RU),
`docs/scalar-functions/09-system-functions.md`, `10-sequences.md` (+RU),
`docs/guide/32-inheritance-and-derived-entities.md` (+RU), `docs/advanced/extensibility.md` (+RU),
`docs/comparisons/feature-benchmarks-2026-10-17.md` (+RU). Остальные правки — существующие EN/RU-страницы.

Итог по пакетам: `nextorm` (core + in-memory) и провайдеры `nextorm.sqlite`, `nextorm.sqlserver`,
`nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`, `nextorm.clickhouse` (7 пакетов).
Пакет `nextorm.entityframeworkcore` в публикацию **не входит** (не в списке job `publish`).

## 2. Предрелизные проверки

Проверки выполнены на `180ba117` (ветка `1.0.9-rc3`) плюс правки §3 и фикс §3.5; продуктовый код
отличается от `180ba117` только фиксом §3.5.

- [x] `dotnet build nextorm.slnx -c Debug` — **0 warning / 0 error** (1m 41s).
- [x] `dotnet build nextorm.slnx -c Release` — **0 warning / 0 error** (1m 13s).
- [x] Полный прогон (`dotnet test nextorm.slnx -c Debug --no-build`,
      `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) —
      **total 13392, failed 0, succeeded 13184, skipped 208** (2m 28s). Все 9 test-проектов passed,
      `nextorm.integration.tests` реально стартовал контейнеры (3m 29s); скипы —
      capability-based (`*_UnsupportedProvider_ShouldThrow*` и аналоги), ни одного skip по
      недоступности провайдера.
- [x] Покрытие (CI run `39379117855`, `180ba117`): **Line 90.1 % / Branch 83.8 %**
      (порог 85 / 75); integration gate — **Line 81.5 % / Branch 75.4 %** (порог 65 / 75).
      Дельта §3.5 (~20 строк + 5 тестов) покрыта новыми тестами; запас до порогов — 5.1 / 8.8 п.п.
- [x] Docs EN + RU синхронны; `dotnet docfx docs/docfx.json` — **exit 0**, **0 error**
      (2 warning — дубликаты исходников пустого `nextorm.core.sourcegenerator`, известный шум).
- [x] `dotnet pack` всех 7 пакетов с `-p:Version=1.0.9-rc3` (dry-run) — **успешно** (7 `.nupkg`);
      nuspec `nextorm.mariadb` ссылается на `nextorm.mysql 1.0.9-rc3`, core-пакет — `1.0.9-rc3`.
- [x] Release-gate аудит дельты `v1.0.9-rc2..HEAD` (`nextorm-code-auditor`) — P0 нет; единственная P1
      (Находка 242: `JoinOptions.ForUpdate/ForShare` молча терялся на 7 из 9 путей join'а) **исправлена
      до тега** (§3.5), после чего блокирующих находок нет. Остальное — P2/ℹ️ (freeze-inventory
      N216-1/N222-1/N226-1, god-class `QueryPreparer`, `Dispose`-ordering, scalar-cache lock).
- [x] CI на `180ba117` (run `39379117855`) — **success**, все шаги: Test with coverage,
      Integration coverage, Benchmark table generator, Allocation regression gate, PDCA evidence
      pipeline, Coverage report/thresholds.

## 3. Изменения в репозитории (поверх HEAD `180ba117`)

### 3.1. Бамб версии

- **`Directory.Build.props`:** `<Version>1.0.9-rc2</Version>` → `1.0.9-rc3`.

### 3.2. `docs/index.md`

- `## Status` — `1.0.9-rc2` → `1.0.9-rc3`.
- `## Releases` — раздел `### 1.0.9-todo` заменён на `### 1.0.9-rc3` (34 issues + строка принятых
  ограничений #297/#244/#228/#210/#201); `### 1.0.9-rc2` и ниже не тронуты.

### 3.3. Служебное

- **`docs/specs/release-1.0.9-rc3.md`** — этот план.

### 3.4. CI-фиксы, вошедшие в релиз

- `34aba77c` — интеграционные тесты (pgcrypto в seed Postgres, независимость SQLite
  dynamic-columns от порядка) и рекурсивный поиск исходников бенчмарков в perf-скриптах.
- `180ba117` — allocation gate фильтрует по строкам манифеста (28 методов / 56 job'ов) вместо
  масок классов, иначе 176 бенчмарков не влезали в 20-минутный лимит.

### 3.5. Release-gate фикс: source-level lock больше не теряется

Аудит нашёл P1 (Находка 242): `JoinOptions.ForUpdate()`/`ForShare()` копировались в join только на
четырёх путях; на query-source join (7 перегрузок), APPLY (6 + alias-APPLY), non-M2M `JoinInto` (5),
raw `TableAlias` join (8 + 6) и ClickHouse `SemiJoin`/`AntiJoin` (4) опция молча игнорировалась —
вопреки обещанию «never silently ignored» в `docs/advanced/limitations.md` (+RU).

Фикс (продуктовый код): все 21 сайт `new JoinOptions()` прогоняют лок через единый
`JoinSourceLock.Apply` (`src/nextorm.core/Builders/JoinSourceLock.cs`), поэтому лок всегда доезжает
до `JoinExpression.From`. Существующий guard `QueryCommand.QueryPreparer.cs:341` отклоняет его на
APPLY/derived/CTE/query-источнике (`NotSupportedException`), `SqlBuilder` — на диалектах без
source-lock (ClickHouse, SQLite, MariaDB). Публичный API не менялся.

Регрессия: `tests/nextorm.mysql.tests/Joins/JoinSourceLockPlacementTests.cs` (JoinInto → `for update
of`; query-source join и APPLY → `NotSupportedException`) и
`tests/nextorm.clickhouse.tests/Joins/JoinSourceLockPlacementTests.cs` (Semi/AntiJoin →
`NotSupportedException`). Запись аудита обновлена: `docs/specs/design/code-smells-review.md`
§«Предрелизный аудит v1.0.9-rc3».

## 4. Публикация (шаги владельца)

- [ ] Закоммитить подготовленный объём (§3) на ветке `1.0.9-rc3` и запушить её.
- [ ] Убедиться, что ветка `1.0.9-rc3` зелёная (coverage hard-fail только на `main`).
- [ ] (Опционально) влить `1.0.9-rc3` в `main` через PR — как для предыдущих релизов.
- [ ] Создать тег `v1.0.9-rc3` → CI job `publish` (OIDC) отправляет 7 пакетов на nuget.org.
- [ ] Создать GitHub Release (prerelease): ссылка на
      <https://alexeyshirshov.github.io/nextorm/#109-rc3> + 7 пакетов + issues #207–#297.
- [ ] Убедиться, что milestone 1.0.9-rc3 закрыт (его issues уже closed, open=0).
- [ ] После публикации: бампнуть `examples/Directory.Packages.props` `NextOrmVersion` → `1.0.9-rc3`
      и обновить дату/строку прогона в `examples/VERIFICATION.md`.

## 5. Definition of Done

- [ ] Версии всех 7 пакетов = `1.0.9-rc3`; `docs/index.md` обновлён (Status + Releases).
- [ ] Тег `v1.0.9-rc3` запушен; CI отправил 7 пакетов на nuget.org.
- [ ] GitHub Release (prerelease) с заполненным описанием.
- [ ] Docs EN/RU задеплоены; якорь `#109-rc3` доступен.
- [ ] Milestone 1.0.9-rc3 закрыт.

## 6. Открытые вопросы

- **Breaking changes:** намеренных удалений публичных сигнатур нет; потребительски заметны
  SQL-текст (#224), fail-closed legacy `WithRecursive` (#265), экранирование имён (#237) и
  `MAXRECURSION` (#275). Pre-release, обратная совместимость между пререлизами не гарантируется
  (см. `readme.md` §Status).
- **`nextorm.entityframeworkcore`** — пакет есть, но в job `publish` (список из 7) его нет; решает владелец.
- **Заморозка публичного API** (`PublicAPI.Shipped/Unshipped.txt`, `PublicApiAnalyzers`, issue #53) —
  вне скоупа rc3; остаётся открытой в milestone 1.0.9 вместе с #231.
- **Perf-остатки** (#245, #246, #247, #250, #311 и follow-up #317/#318/#319) перенесены в milestone
  `1.0.9-rc5`; в rc3 не входят.

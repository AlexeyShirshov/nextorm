# План: бенчмарк nextorm в сьюте Dapper (PR DapperLib/Dapper#2025)

- **PR:** [#2025](https://github.com/DapperLib/Dapper/pull/2025) — `Add Nextorm to benchmarks`
- **Статус:** приостановлен. PR открыт, но не поддерживается с 2024-02-08 и конфликтует с текущим
  `main`; гейт зрелости у мейнтейнера не пройден.
- **Срез фактов:** 2026-10-22.
- **Ветки:** Dapper `main` — `eb47546a` (2026-09-23); PR head — `main` форка `AlexeyShirshov/Dapper`,
  `eacedf81` (2024-02-08).
- **Связанные документы:** [comparisons/benchmarks.md](../../comparisons/benchmarks.md),
  [comparison-benchmark-scenarios.md](../performance/comparison-benchmark-scenarios.md),
  [capability-matrix.md](capability-matrix.md).

Коротко: возобновление — это **не ребейз**, а новый PR с переписанным бенчмарком и ответом на
методологическую претензию. Возвращаться к нему имеет смысл, только когда закрыт гейт зрелости
(стабильный релиз + внешние пользователи), иначе повторяется ответ 2024 года.

## 1. Цель и границы

Цель: получить nextorm в таблице сравнения ORM внутри репозитория Dapper — это независимая витрина
производительности, которую читает ровно наша аудитория.

Не-цели:

- не менять публичный API Dapper и основную библиотеку;
- не добавлять зависимость в `Dapper`/`Dapper.StrongName`, только в проект бенчмарков;
- не трогать чужие бенчмарки (`Benchmarks.EntityFrameworkCore.cs`, `Benchmarks.EntityFramework.cs`) и
  общие `TargetFrameworks`;
- не менять правила fairness сьюта в свою пользу.

## 2. Хронология

| Дата | Событие |
|---|---|
| 2024-01-05 | первая публикация пакета `nextorm` на NuGet (`1.0.1-alpha`) |
| 2024-01-09 11:25Z | открыт PR #2025 |
| 2024-01-09 17:24Z | содержательный вердикт Marc Gravell (mgravell): nextorm слишком новый, «вернёмся, если появится momentum» |
| 2024-01-09 … 2024-01-10 | 23 инлайн-комментария; спор о `Prepare()`/`Compile()` в `Benchmarks.Nextorm.cs:39`; mgravell прогнал собственный замер `Iterations = 1…100` |
| 2024-02-08 16:21Z | последний коммит PR (`upgrade to 1.0.2-alpha`), дальше активности нет |
| 2026-09-23 | последний коммит `main` Dapper; PR конфликтует с ним (`CONFLICTING` / `DIRTY`) |
| 2026-10-20 | `nextorm` `1.0.9-rc3` — по-прежнему prerelease, стабильного релиза нет |

## 3. Что сказал мейнтейнер и что это значит

Два независимых возражения, оба не про качество кода:

1. **Зрелость.** «nextorm is brand new, barely seen — is that right? … I doubt it has enough maturity
   to be worth adding here *yet* … if nextorm gets some momentum, we can see about merging it in».
   На момент разговора пакет был опубликован за четыре дня до PR.
2. **Методология.** `Prepare()`/`Compile()` выносился в `GlobalSetup`, а подготовленный запрос в
   nextorm живёт на контексте/соединении. По его мнению, это меряет недоступную в реальности выгоду:
   если вызывающий платит за `Prepare()` на каждый запрос, замер должен это включать. Он сам показал,
   что prepared начинает выигрывать только на 50–75+ итерациях.
3. Дополнительно: `VersionOverride` в csproj отвергнут («why do we need all these version-overrides?»),
   вместо условий по TFM попросили `#if`, `PackageVersion` — в `Directory.Packages.props`.

Вторая половина фразы — условное «нет» без сроков и обязательств: дверь открыта, инициатива на нас,
аргументы принимаются только внешние и проверяемые (собственные замеры nextorm против Dapper для него
самооценка).

## 4. Текущее состояние PR

| Параметр | Значение |
|---|---|
| `state` | OPEN |
| `mergeable` / `mergeStateStatus` | `CONFLICTING` / `DIRTY` |
| Head | ветка **`main` форка**, `eacedf81`, 14 коммитов |
| База | `main`, который ушёл больше чем на 2.5 года вперёд |
| Конфликты | `Directory.Packages.props`, `Dapper.Tests.Performance.csproj`, `Benchmarks.EntityFrameworkCore.cs` |
| Изменённые файлы | 4 (+122/−7 в исходной ревизии) |

Отдельная проблема: head PR — это `main` форка. GitHub не позволяет сменить head у открытого PR, поэтому
ребейзить нужно не PR, а новую ветку, а #2025 закрыть со ссылкой на преемника.

## 5. Почему это не ребейз: дрейф API

Код PR написан под `1.0.2-alpha`; в `1.0.9` соответствующих символов нет.

| В PR (1.0.2-alpha) | Сейчас (1.0.9) |
|---|---|
| `using nextorm.core;` / `nextorm.sqlserver` | `NextORM.Core` / `NextORM.SqlServer` |
| `DbContextBuilder` | `DataContextBuilder` |
| `dataContext.Create<Post>(config => config.Table("posts"))` | `dataContext.From<Post>()` |
| `NORM.Param<int>(0)` | `SqlFunctions.Parameter<int>(0)` или захваченный локальный |
| `Compile()` (в обсуждавшейся ревизии) | `Prepare()` → `IPreparedQueryCommand<TResult>` |

Следствие: `Benchmarks.Nextorm.cs` переписывается целиком, а не переносится патчем.

## 6. Два независимых гейта

### G1 — зрелость («momentum»)

| Сигнал | nextorm (2026-10-22) | Минимум в сьюте Dapper |
|---|---|---|
| Стабильный релиз | нет; все 16 версий `alpha`/`b`/`rc`, последняя `1.0.9-rc3` | все ORM на стабильных пакетах |
| Загрузки NuGet | `nextorm` 1 004, `nextorm.sqlserver` 712, `nextorm.sqlite` 633 | `SqlMarshal` 47 200, `Susanoo` 106 024, `Mighty` 113 621, `Dashing` 144 692 |
| Внешние пользователи | 2 звезды, 0 форков; внешних зависимых в коде не видно (поиск по GitHub) | — |

Вывод: по объёму работы проект давно не «brand new», но по adoption — на 1.5–2 порядка ниже самого
скромного участника сьюта. Пока это так, повторный PR с высокой вероятностью получит тот же ответ.

### G2 — методология

Снять можно только кодом: арм, который меряет `Prepare()` на каждый вызов, и явное разделение
«однократно подготовлено» / «подготовка оплачивается как в реальном запросе».

## 7. План работ

### P0. Условие входа

- опубликован **стабильный `1.0.9`** (не `rc`): снимает первый аргумент и делает версию в
  `PackageVersion` нормальной (сейчас в CPM пришлось бы писать prerelease);
- есть хотя бы минимальная внешняя аудитория: публичный зависимый проект, issue от стороннего
  пользователя или независимый замер.

Пока P0 не выполнен, PR не открывается — максимум комментарий в #2025 с обновлением статуса.

### P1. Ветка и преемственность

- новая ветка от актуального `DapperLib/main` (например `nextorm-benchmarks`), не от форк-`main`;
- #2025 закрыть с описанием, что перенесено в новый PR, и ссылкой на обсуждение 2024 года;
- `main` форка оставить чистым.

### P2. Порт бенчмарка под harness

- `Benchmarks.Nextorm.cs` переписать под `1.0.9` (см. §5), целиком под `#if !NET4X`;
- `PackageReference` только в `ItemGroup Condition="'$(TargetFramework)' != 'net472'"`;
- `PackageVersion` для `nextorm.sqlserver` — в `Directory.Packages.props`, **без** `VersionOverride`;
- не менять `TargetFrameworks`, `Benchmarks.EntityFrameworkCore.cs`, версии чужих пакетов;
- структура армов как в сьюте: наследник `BenchmarkBase`, `[Description("Nextorm")]` для колонки ORM,
  `BaseSetup()` + `Step()` (цикл `i` 1..5000), SQL Server и connection string `Main`.

Почему это обязательно: nextorm собирается только под `net10.0`, а `Build.csproj` тянет
`benchmarks/**/*.csproj` и собирает оба TFM в CI.

### P3. Методология (главное)

- арм **warm**: `Prepare()` один раз в `GlobalSetup`, далее только выполнение;
- арм **per-invoke**: `Prepare()` внутри `[Benchmark]`, как платит вызывающий за один запрос;
- опционально арм **plan cache**: свежая fluent-команда с константами на каждый вызов — то, что
  nextorm делает неявно; это Category B из [comparisons/benchmarks.md](../../comparisons/benchmarks.md);
- названия честные: `Prepare`, не `Compile`; в комментарии к арму — что именно переиспользуется и
  почему это допустимо (или недопустимо) в реальном коде;
- сверить с уже существующими «compiled» армами (`EF.CompileQuery`, `LinqToDB.CompiledQuery`), чтобы
  сравнение было compiled-vs-compiled.

### P4. Локальная верификация

- `dotnet build Build.csproj -c Release /p:CI=true` — то же, что делает CI (net472 на Linux через
  `Microsoft.NETFramework.ReferenceAssemblies`);
- прогон: `dotnet run -c Release --project benchmarks/Dapper.Tests.Performance`; нужен SQL Server и
  connection string `Main` в `benchmarks/Dapper.Tests.Performance/app.config` (по умолчанию
  `Data Source=.;Initial Catalog=tempdb;Integrated Security=True` — на Linux меняется на SQL-логин);
- CLA: contributor-assistant; если бот попросит — комментарий
  `I have read the CLA Document and I hereby sign the CLA`.

### P5. Текст PR

Ответить по пунктам 2024 года: что изменилось в nextorm, почему армы теперь честные, что показывает
per-invoke замер. Честно указать adoption-метрики — попытка их обойти читается как незнание контекста.

## 8. Требования харнесса Dapper (сверено с `main` на 2026-09-23)

| Требование | Значение |
|---|---|
| TFM проекта бенчмарков | `net472;net10.0` |
| Сборка бенчмарков в CI | `Build.csproj` → `benchmarks/**/*.csproj` |
| BenchmarkDotNet | 0.15.8 |
| Версии пакетов | CPM, `Directory.Packages.props` |
| Конфигурация прогона | `ShortRun`, `UnrollFactor=500`, `IterationCount=10`, `MemoryDiagnoser` |
| Колонка ORM | атрибут `[Description]` на классе |
| Колонки результата | `ORM`, `Method`, `Return`, `Mean`, `StdDev`, `Error`, `Ratio`, `Allocated` |

## 9. Альтернатива, если G1 не закрыт

Не бороться за место в чужом сьюте:

- **RawDataAccessBencher** ([FransBouma/RawDataAccessBencher](https://github.com/FransBouma/RawDataAccessBencher)) —
  независимый замер, nextorm там нет; локальный клон `/home/alex/sources/RawDataAccessBencher`
  (ветка `master`, .NET 10, структура `RawBencher/Benchers` + `BenchController`);
- собственная страница [comparisons/benchmarks.md](../../comparisons/benchmarks.md) с методологией
  Category A/B — уже есть и полностью под нашим контролем;
- статья/доклад с теми же числами.

Эффект для «momentum» тот же, риска «условного нет» — нет.

## 10. Критерии возврата к PR

Любой из:

- стабильный `1.0.9` опубликован **и** появился хотя бы один публичный внешний пользователь;
- в Dapper пришёл сторонний запрос «почему нет nextorm в таблице»;
- в nextorm появился потокобезопасный типизированный compiled-query делегат — тогда арм «compiled»
  становится корректным без оговорок;
- мейнтейнер сам вернулся к теме.

Если ничего из этого не выполнено, новый PR не открывается: обновление или переоткрытие без новых
внешних фактов сжигает доверие.

## 11. Открытые вопросы

- нужен ли арм cold start (DapperAOT + EF compiled), как предлагал mgravell, и не раздувает ли он PR;
- как считать N+1-сценарий, чтобы он не выглядел как попытка выиграть на антипаттерне;
- одна версия nextorm на PR или матрица по провайдерам (сьют сейчас на SQL Server);
- где брать SQL Server для локальных прогонов (контейнер `mcr.microsoft.com/mssql/server` в CI сьюта
  уже используется).

## 12. Источники

- PR: <https://github.com/DapperLib/Dapper/pull/2025>
- Сьют и правила: `benchmarks/Dapper.Tests.Performance/{Benchmarks.cs,Config.cs,app.config}`, `Build.csproj`
- CI: `.github/workflows/main.yml`
- nextorm: `docs/comparisons/benchmarks.md`, `docs/engine/03-plan-cache-and-reuse.md`,
  `docs/advanced/limitations.md`
- Метрики пакетов: NuGet flat-container/registration и поисковый API (срез 2026-10-22)

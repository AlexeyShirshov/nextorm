# Профиль `oc-dev`: PDCA-оркестрация и двухтактные Plan/Check (сессия 2026-09-26)

От «кто вообще оркестрирует работу» до двухтактной схемы Plan/Check, которая
не грузит код в дорогой контекст Opus.

- Дата: 2026-09-26 (локальное время, UTC+5; в логах opencode — UTC).
- Окружение: WSL Ubuntu-22.04, opencode 1.18.32, репозиторий `~/sources/opencode-config`
  (снимок `~/.config/opencode`), рабочий проект `~/sources/nextorm`.
- Модели: «мозг» `opencode/claude-opus-5-5` (OpenCode Zen), «руки» `deepseek/deepseek-flash`.
- Связанный отчёт (другая сессия, про prompt-cache): `oc-dev-opus-cache-cost-2026-09-26.md`.

## 0. Резюме

1. **Оркестратор цикла — `build` (Opus).** Раньше PDCA был только намерением в
   инструкции; теперь это явный контракт: машина состояний
   `PLAN → DO → CHECK → ACT → (EXIT | PLAN)` с todo-фазами и gate'ами.
2. **Написан плагин `pdca-tracker.js`** (в песочнице, в основной конфиг **не
   внедрён**): трекает фазу из todo-префиксов и вызовов `task`, инжектит статус в
   системный промпт, считает loop-back и нарушения, опционально «пинает» на
   пропущенный Check.
3. **Согласование с project-local `implementing-todo-features`:** nextorm-агенты
   (`nextorm-code-auditor`, `nextorm-design-engineer`, `nextorm-*-perf-analyst`)
   **не вызываются** — их инструкции встроены в фазы; nextorm-специфику вынесли в
   локальный конфиг, глобальный профиль оставили общим.
4. **Ключевой вывод по стоимости:** и Plan, и Check сделаны **двухтактными** —
   `Gather` на DeepSeek (или детерминированными командами), `Triage` на Opus по
   сводке. Opus **не грузит код в свой контекст**. `dotnet-code-review-agent`
   перепришпилен с Opus на DeepSeek.
5. Аудитор-чеклист сделан **суперсетом** `nextorm-code-auditor ⊇
   dotnet-code-review-agent` (общее ревью + регистры/API-аудит).

## 1. Исходная точка

- Профиль `oc-dev.jsonc` уже существовал: `default_agent: "plan"`, модель Opus 5.5,
  субагенты на DeepSeek, кастомный `coder`, MCP (memory/context7/mslearn/deepwiki/gitmcp).
- В `oc-dev.instructions.md` PDCA был описан словами («Plan — Opus, Do — coder,
  Check — reviewer»), но **без состояния и gate'ов**: build не помнил фазу, не был
  обязан прогонять Check, не было loop-back и критерия выхода.
- Параллельная тема сессии — сверка статистики токенов (см. §2).

## 2. Сверка статистики токенов (побочная находка)

Вопрос: почему локальная БД opencode (~579M cache-read) расходится с провайдерским
CSV (~8.76B cache-hit).

- **Профили БД не делят.** `opencode db path` одинаков для `deepseek.jsonc`,
  `gp.jsonc`, `oc-dev.jsonc` → `~/.local/share/opencode/opencode.db`. Изолирует
  данные только `oc-sandbox` (свой `XDG_DATA_HOME`, 2 сессии).
- **Ключ совпадает** с CSV (`sk-9042…d5dd`) — это ключ этого окружения.
- Провайдер (09-14→09-26): cache-hit **8 764 673 906**, запросов **53 207**,
  cost **$85.82**. Локальная БД: **579M** cache-read, ~**3 917** шагов, **$5.49** —
  то есть ~**6.6%** объёма.
- Причина: **удаление сессий**. У `message`/`part` FK `ON DELETE CASCADE`, а
  `session.delete` чистит ещё и события (проверено по исходникам opencode), поэтому
  следов не остаётся (0 «сиротских» агрегатов), но **целые дни отсутствуют**
  (09-17/09-18 — ноль сессий; 09-21 — 2 сообщения; 09-24 — 23). Авторетеншена нет →
  сессии удаляли руками (`/clear` через `clear-session.js`, `session delete` в TUI).

Вывод: локальная статистика неполная by design, если сессии удаляют; для устойчивого
учёта нужен append-only ledger (не реализован).

## 3. Оркестратор PDCA = `build`

`oc-dev.instructions.md` теперь задаёт:

- **Роль `build`:** оркестратор, а не исполнитель. Не читает большие файлы, не
  правит, не запускает команды — делегирует.
- **Машина состояний:** `PLAN → DO → CHECK → ACT → (EXIT | PLAN)`; фазы трекаются
  todo-префиксами `P:` `D:` `C:` `A:` (ровно один `in_progress`), итерации `n/3`.
- **Gate'ы:** PLAN→DO (есть критерии приёмки + пройден дизайн-чеклист), DO→CHECK
  (все Do закрыты, сборка/тесты запущены), CHECK→ACT (тесты зелёные + аудит),
  ACT→EXIT (урок в memory + доки/AGENTS обновлены).
- **Loop-back:** дефект → DO, неверный план → PLAN; после 3 итераций — СТОП и вопрос
  пользователю.
- **Карта делегирования:** PLAN/решения/CHECK-триаж — Opus; объём Do — `coder` на
  DeepSeek.
- `build.steps`: **30 → 60** (цикл с запасом на один loop-back).

Проверено: `opencode debug agent build` → `opencode/claude-opus-5-5`, `steps 60`;
`coder` → `deepseek/deepseek-flash`, `steps 60`.

## 4. Плагин `pdca-tracker` (песочница, не внедрён)

Файлы (только sandbox `opencode-config-sandbox`):
- `config/opencode/plugins/pdca-tracker.js`
- `tests/pdca-tracker.test.mjs`

Что делает:
- регистрирует сессии оркестратора (`build`) через `chat.message`/`message.updated`,
  сессии субагентов игнорирует;
- выводит фазу из todo-префиксов (`todowrite`) и из вызовов `task`
  (`coder`→DO, `dotnet-code-review-agent`/`dotnet-security-reviewer`→CHECK);
- инжектит строку статуса в системный промпт
  (`experimental.chat.system.transform`): фаза, `итерация n/3`, открытые Do-задачи,
  текущий gate, требование Check;
- считает loop-back, при `итерация > maxIter` пишет `[лимит]`;
- ловит «нарушения» — если `build` сам зовёт `edit`/`write`/`bash` → `[нарушение]`;
- `session.idle` в DO без Check — один раз «пинает» (по умолчанию **выключено**).

Проверка: `node --test` → **11/11 pass**; `PDCA_DEBUG=1 oc-sandbox run "..."` →
в логе `tracker created` (opencode грузит плагин).

Env-ручки: `PDCA_AGENTS`, `PDCA_DO_AGENTS`, `PDCA_CHECK_AGENTS`,
`PDCA_HANDS_ON_TOOLS`, `PDCA_MAX_ITER` (3), `PDCA_MAX_NUDGES` (1), `PDCA_ENFORCE=1`,
`PDCA_DEBUG=1`, `PDCA_DEBUG_LOG`.

## 5. Согласование с `implementing-todo-features` и nextorm-агентами

Исходно: project-local skill `nextorm/.opencode/skills/implementing-todo-features/` +
5 project-local субагентов (`nextorm-code-auditor`, `nextorm-design-engineer`,
`nextorm-inmemory-perf-analyst`, `nextorm-db-perf-analyst`,
`nextorm-performance-analyst`). У этих агентов **модель не задана** →
`opencode debug agent` показал отсутствие `model` → при вызове из `build` они
наследуют модель родителя (**Opus**).

Конфликты с oc-dev: Check-карта указывала на dotnet-ревьюеров, а skill требует
`nextorm-code-auditor`/`nextorm-design-engineer`; skill написан как «сам правлю»,
а контракт запрещает build править; `plan` (read-only) не может записать work-plan
skill'а.

**Решение (по выбору пользователя):**
- nextorm-агенты **не вызываются** — их инструкции **встроены** в фазы;
- **глобальный** `oc-dev.instructions.md` содержит только общие формулировки;
- **nextorm-специфика** — в `nextorm/.opencode/nextorm-pdca.md`, подключённом через
  `nextorm/opencode.json` → `instructions: ["AGENTS.md", ".opencode/nextorm-pdca.md"]`.

Проверено (cwd=nextorm, профиль oc-dev) — резолвятся три инструкции:
`oc-dev.instructions.md`, `AGENTS.md`, `.opencode/nextorm-pdca.md`.

## 6. Двухтактные Plan и Check (главный вывод)

Проблема: если Plan/Check делает Opus и **читает код** (дифф фичи — тысячи строк),
Opus сжигает токены. По ставкам (из `models.json`):

| | Opus 5.5 | DeepSeek flash |
|---|---|---|
| context | 1M | 1M |
| input / cache-read ($/M) | 4 / 0.20 | 0.14 / **0.0028** |

Разница по cache-read ~**70×** (input ~29×). Решение — разделить фазу на `Gather` и
`Triage`.

**§PLAN (дизайн-ревью):**
1. `Gather` (DeepSeek) — `explore`/`general`/`dotnet-architect`/`dotnet-code-review-agent`
   ревьюят **область изменений** (не весь репозиторий) → находки, `file:line`,
   счётчики, sealing ratio, anti-pattern hits — без кода.
2. `Decide` (Opus) — `build` читает **только сводку**, выбирает fix now/deferred,
   декомпозирует. Код в контекст не тянет.

**§CHECK (аудит):**
1. `Gather` — (a) механика **без LLM**, командами через `coder`: сборка 0/0,
   `rg` по `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`, перечисление
   public-типов, CS1591; (b) семантика — `dotnet-code-review-agent` на DeepSeek,
   дифф режется по файлам/чанкам, возвращаются только находки.
2. `Triage` (Opus) — `build` читает **только сводный отчёт**, выносит pass/fail и
   ранжирование. Код в контекст не тянет.

Опционально — маленький скрипт `audit-metrics`, чтобы метрики (build/CS1591/
suppression ratio) вообще не гонять через модель (не реализован).

## 7. Пиннейм моделей

- `dotnet-code-review-agent` → **`deepseek/deepseek-flash`** (это `Gather`-ревьюер
  Check; раньше был Opus — в реальной сессии стоил ~$2.15).
- `dotnet-security-reviewer` → оставлен на **`opencode/claude-opus-5-5`** (точечный
  high-stakes аудит).
- Остальные `dotnet-*` и `general`/`explore`/`coder` — DeepSeek.

Проверено `opencode debug agent` (cwd=nextorm): `dotnet-code-review-agent` →
`deepseek/deepseek-flash`; `dotnet-security-reviewer` → Opus.

## 8. Аудитор-чеклист = суперсет

`nextorm-code-auditor` и `dotnet-code-review-agent` по назначению пересекаются.
Аудитор сделан суперсетом: сначала **общее ревью** (корректность, стандарты,
архитектура/DI, perf- и security- red flags, тест-импакт, severity
Critical/Warning/Suggestion, **роутинг специалистам**), затем **аудит
smells/API** (регистры `docs/specs/design/*`, `P0/P1/P2`, `🔴/🟡/ℹ️`, suppression
ratio, CS1591, optional-`null`), с nextorm-toolchain. Оба такта — в `Gather`; Opus
только триажит.

## 9. Изменённые файлы (за сессию)

`~/sources/opencode-config` (live `~/.config/opencode` ↔ репо):
- `config/profiles/oc-dev.instructions.md` — контракт PDCA (машина состояний,
  gate'ы, карта делегирования), §PLAN и §CHECK (двухтактные, общие), §Проектные
  чеклисты (приоритет).
- `config/profiles/oc-dev.jsonc` — `build.steps 60`, `dotnet-code-review-agent` →
  DeepSeek, обновлены комментарии.
- `README.md` — описание PDCA-контракта и двухтактных Plan/Check.

`~/sources/nextorm`:
- `.opencode/nextorm-pdca.md` — **новый** nextorm-оверлей (инварианты Plan, регистры
  и toolchain Check, аудитор-суперсет, политика «nextorm-агенты не вызываются»).
- `opencode.json` — добавлен `.opencode/nextorm-pdca.md` в `instructions`.
- `docs/specs/oc-dev-pdca-orchestration-2026-09-26.md` — этот отчёт.

`~/sources/opencode-config-sandbox` (не внедрено в основной конфиг):
- `config/opencode/plugins/pdca-tracker.js`, `tests/pdca-tracker.test.mjs`.

## 10. Открытые вопросы и дальше

1. **Внедрение `pdca-tracker`** в основной конфиг (копия в `~/.config/opencode/plugins/`
   + `PDCA_*` в профиле) — пока только песочница.
2. **Жёсткое обеспечение цикла:** плагин умеет только инъекцию в промпт + nudge;
   реальная блокировка «выйти без Check» — отдельный уровень (permission/policy).
3. **Скрипт `audit-metrics`** для метрик без гонять их через модель.
4. **Ledger использования** (append-only), чтобы учёт пережил удаление сессий (§2).
5. **`dotnet-code-review-agent` имеет `mode: all`** (виден как primary) — при желании
   ограничить `subagent`.
6. **`build.steps = 60`** может быть тесно для длинного цикла (8-шаговый skill +
   loop-back).

## 11. Приложение: проверки

```bash
# резолв конфига/агентов
OPENCODE_CONFIG=$HOME/.config/opencode/profiles/oc-dev.jsonc opencode debug config
OPENCODE_CONFIG=$HOME/.config/opencode/profiles/oc-dev.jsonc opencode debug agent build
OPENCODE_CONFIG=$HOME/.config/opencode/profiles/oc-dev.jsonc opencode debug agent dotnet-code-review-agent

# инструкции проекта nextorm (должны быть 3: профиль + AGENTS.md + nextorm-pdca.md)
cd ~/sources/nextorm && \
  OPENCODE_CONFIG=$HOME/.config/opencode/profiles/oc-dev.jsonc opencode debug config | grep -i instructions -A3

# плагин pdca-tracker (песочница)
node --test ~/sources/opencode-config-sandbox/tests/pdca-tracker.test.mjs
PDCA_DEBUG=1 ~/sources/opencode-config-sandbox/bin/oc-sandbox run "reply with exactly: ok"
```

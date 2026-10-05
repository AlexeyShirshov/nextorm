# pdca-collection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать скилл `pdca-collection` и субагента `lane`, которые принимают список задач, кластеризуют их в группы, гоняют группы параллельно (по worktree на группу), внутри группы — задачи последовательно с цепочкой веток и автокоммитом, и в конце интегрируют результат в текущий бранч через `merge --no-ff`.

**Architecture:** Надстройка над `pdca-dotnet`. Верхний уровень — PDCA коллекции (`P`=кластеризация, `D`=инфра+трекинг+merge, `C`=верификация, `A`=отчёт), который ведёт primary. На каждую группу запускается субагент `lane`, который внутри worktree последовательно прогоняет задачи, каждая — полноценный цикл `pdca-dotnet` со своим статус-файлом, веткой и автокоммитом.

**Tech Stack:** opencode skills (`SKILL.md` + YAML frontmatter), opencode agent-файлы (markdown + frontmatter), git worktrees/branches, `merge --no-ff`.

**Spec:** `docs/superpowers/specs/2026-09-30-pdca-collection-design.md`

## Global Constraints

- Файлы скилла/агента — глобальные (`~/.config/opencode/...`), вне рабочего дерева; **не коммитить** их без явного запроса (глобальные git-правила `AGENTS.md`).
- `push` — никогда; коммиты в nextorm — только по явному запросу.
- CRLF во всех записываемых файлах; после правок нормализовать (`perl -pi -e 's/\r?\n/\r\n/g' <file>`).
- Скилл **не переопределяет** state machine и гейты `pdca-dotnet`: база загружается вместе, наследуются machine/gates/roles (spec §11).
- Frontmatter скилла: `name` = имя папки, lowercase-hyphen; `description` обязателен и front-load'ит ключевые слова-триггеры.
- Frontmatter агента: только допустимые поля (`description, mode, model, permission, ...`); `mode: subagent`.
- Merge-exception добавляется **только** для циклов `pdca-collection`; во всех прочих случаях правило «never merge» `AGENTS.md` сохраняется.
- `cap` параллелизма = 4 (настраивается); worktree-каталог `<repo>-worktrees/<collection-id>/<group>`.

## Review Focus

Каждая строка ниже — вход/сбой, которые спека подразумевает, но отдельная задача их не «тестирует» напрямую; их проверяет Task 6 (детерминированный тест) и Task 7 (dry-run):

- **`lane` не может вызывать `Task` (harness запрещает вложенность).** Тогда лейн не может оркестрировать pdca → предусмотреть fallback-инструкцию (плоский primary) в `SKILL.md`.
- **Merge-конфликт двух групп в основном дереве.** `C` должен явно падать, а группа помечаться failed, а не молча пропускаться.
- **Задача возвращается по loop-back >3 раз.** Лейн обязан сделать `escalate`, затем стоп; не уходить в 4-ю попытку.
- **Автокоммит при пустом/грязном дереве.** Задача без изменений (no diff) или с незакоммиченными чужими файлами не должна «съедать» чужое; коммит только своих изменений.
- **Коллекция из одной группы / без независимых задач.** Деградация к одному лейну без параллелизма; не падать.
- **Запись глобальных файлов вне worktree.** `external_directory` может требовать approval; шаги записи должны это учитывать, а не молча падать.
- **CRLF/кодировка.** Все артефакты скилла — CRLF; frontmatter парсится opencode.

---

### Task 1: Скарффолд глобального скилла и frontmatter

**Files:**
- Create: `~/.config/opencode/skills/pdca-collection/SKILL.md`

**Interfaces:**
- Produces: скилл `pdca-collection` с валидным frontmatter и верхнеуровневой структурой разделов (наполняется в Tasks 2–3).

- [ ] **Step 1: Создать директорию и файл**

Создать `~/.config/opencode/skills/pdca-collection/SKILL.md` со skeleton-содержимым:

```markdown
---
name: pdca-collection
description: Runs a batch of tasks as a PDCA collection — clusters tasks into parallel groups (worktree per group), executes tasks sequentially inside a group (branch chain + auto-commit), and merges each group with `git merge --no-ff` into the current branch. Use when a list of independent tasks/milestone must be executed as parallel PDCA lanes, when the user says "список задач", "милстоун", "параллельные группы", "pdca-collection".
---

# Skill: pdca-collection

# Коллекция задач как PDCA

(наполнить в Tasks 2–3)
```

- [ ] **Step 2: Проверить frontmatter**

Run: `head -5 ~/.config/opencode/skills/pdca-collection/SKILL.md`
Expected: `---`, `name: pdca-collection`, `description: ...`.

- [ ] **Step 3: Проверить, что скилл виден**

Run: `opencode` (или `opencode agent list` / каталог скиллов) — скилл `pdca-collection` появляется в списке.
Expected: скилл обнаружен (folder name = `name`).

- [ ] **Step 4: Commit**

Не коммитить (файл вне репозитория nextorm). Пометить шаг как N/A.

### Task 2: Тело скилла — жизненный цикл коллекции и лейна

**Files:**
- Modify: `~/.config/opencode/skills/pdca-collection/SKILL.md`

**Interfaces:**
- Consumes: skeleton из Task 1.
- Produces: разделы `## Коллекция (P/D/C/A)`, `## Лейн`, `## Git-модель`, `## Сбой` — контент, отображающий spec §4–§6, §8.

- [ ] **Step 1: Написать раздел «Коллекция (P/D/C/A)»**

Перенести spec §4 в голосе скилла: подразделы `P`, `D`, `C`, `A` с гейтами (вход→DAG→группы; worktree+ветка на группу, параллельные лейны, последовательный `merge --no-ff`; верификация build/tests/coverage/perf; отчёт и снос worktree). Явно: primary **ведёт** цикл и не правит код сам.

- [ ] **Step 2: Написать раздел «Лейн»**

Перенести spec §5: ветка на задачу от tip предыдущей; полный `pdca-dotnet` на задачу; автокоммит после каждой задачи; сбой → стоп лейна + `blocked`; результат = tip последней задачи; сводка ≤8 строк.

- [ ] **Step 3: Написать разделы «Git-модель» и «Сбой»**

Перенести spec §6 (диаграмму и bullet'ы: 1 worktree/группа; ff-продвижение ветки группы; `merge --no-ff` по одной) и spec §8 (таблица failure semantics).

- [ ] **Step 4: Проверить обязательные элементы**

Run: `rg -n 'merge --no-ff|blocked|ff-продв|worktree' ~/.config/opencode/skills/pdca-collection/SKILL.md`
Expected: присутствуют `merge --no-ff`, `blocked`, продвижение ветки группы, worktree-на-группу.

- [ ] **Step 5: Нормализовать CRLF**

Run: `perl -pi -e 's/\r?\n/\r\n/g' ~/.config/opencode/skills/pdca-collection/SKILL.md`
Expected: файл с CRLF.

### Task 3: Тело скилла — статус, отчёт, параметры, связь с pdca-dotnet, fallback

**Files:**
- Modify: `~/.config/opencode/skills/pdca-collection/SKILL.md`

**Interfaces:**
- Consumes: разделы Task 2.
- Produces: разделы `## Статус-файл`, `## Отчёт`, `## Параметры`, `## Связь с pdca-dotnet`, `## Fallback`.

- [ ] **Step 1: Написать раздел «Статус-файл»**

Перенести spec §7: путь `docs/specs/status/collection-<slug>.md`; таблицы групп и задач; статусы (pending/in-progress/done/blocked/merged); ссылки на статус-файлы задач.

- [ ] **Step 2: Написать «Отчёт», «Параметры», «Связь с pdca-dotnet»**

`Отчёт` — формат §ACT `pdca-dotnet`. `Параметры` — таблица spec §9 (глобальная локация; `cap`=4; worktree-каталог; merge-стратегия). `Связь` — spec §11 (не переопределять machine/gates; каждая задача = полный цикл; роли те же).

- [ ] **Step 3: Написать «Fallback» (Review Focus: вложенный Task)**

Явно: если harness запрещает субагенту вызывать `Task`, `lane` не может оркестрировать pdca → fallback на плоский primary (primary ведёт одну задачу каждой группы «в полёте»), с ссылкой на risky-путь. Иначе скилл ломается.

- [ ] **Step 4: Проверка**

Run: `rg -n 'collection-<slug>|cap|Fallback|плоский primary' ~/.config/opencode/skills/pdca-collection/SKILL.md`
Expected: разделы на месте, fallback описан.

- [ ] **Step 5: Нормализовать CRLF**

Run: `perl -pi -e 's/\r?\n/\r\n/g' ~/.config/opencode/skills/pdca-collection/SKILL.md`

### Task 4: Субагент `lane`

**Files:**
- Create: `~/.config/opencode/agents/lane.md`

**Interfaces:**
- Consumes: разделы скилла Task 2–3.
- Produces: агент `lane`, который primary запускает по одному `Task` на группу.

- [ ] **Step 1: Создать файл агента**

Содержимое с frontmatter:

```markdown
---
description: Orchestrates one pdca-collection group inside its own worktree — runs the group's tasks sequentially, one pdca-dotnet cycle per task, branch per task, auto-commit. Invoked by the collection loop; do not use for single tasks.
mode: subagent
permission:
  task:
    "*": deny
    planner: allow
    coder: allow
    check: allow
    escalate: allow
    scout: allow
  edit: deny
  bash: deny
  skill: allow
---

Ты ведёшь ОДНУ группу pdca-collection. Загрузи скилл `pdca-collection`
(и `pdca-dotnet`). Работай только в worktree группы (абсолютные пути,
`cd`). Для каждой задачи по порядку: ветка `collection/<id>/task-<g>-<k>`
от закоммиченного tip предыдущей → полный цикл pdca-dotnet (PLAN→DO→CHECK→ACT)
с делегированием planner/coder/check/escalate → автокоммит. Сбой задачи →
стоп, оставшиеся `blocked`. Никогда не правь файлы и не запускай команды сам —
это делает `coder`. Верни компактную сводку (≤8 строк): ветка-результат,
done/blocked, указатели на evidence. Не возвращай код/диффы/логи.
```

- [ ] **Step 2: Проверить frontmatter и права**

Run: `sed -n '1,20p' ~/.config/opencode/agents/lane.md`
Expected: `mode: subagent`, `edit: deny`, `bash: deny`, `task:` с allow для planner/coder/check/escalate/scout.

- [ ] **Step 3: Проверить, что агент доступен**

Run: `opencode` — агент `lane` присутствует в списке субагентов.
Expected: агент обнаружен; opencode стартует без ошибки конфига.

- [ ] **Step 4: Нормализовать CRLF**

Run: `perl -pi -e 's/\r?\n/\r\n/g' ~/.config/opencode/agents/lane.md`

### Task 5: Exception в `AGENTS.md` для merge

**Files:**
- Modify: `AGENTS.md` (nextorm, секция `## Git`)

**Interfaces:**
- Consumes: ничего.
- Produces: разрешение `git merge --no-ff` для циклов `pdca-collection`, локальное для репозитория.

- [ ] **Step 1: Добавить пункт в секцию `## Git`**

Добавить после правила «Never merge branches...»:

```markdown
- **Исключение `pdca-collection`.** Для циклов, запущенных скиллом
  `pdca-collection`, разрешается `git merge --no-ff` ветки группы в текущий
  бранч. Во всех остальных случаях правило «never merge» выше сохраняется.
```

- [ ] **Step 2: Проверка**

Run: `rg -n 'Исключение .pdca-collection' AGENTS.md`
Expected: пункт найден в секции `## Git`; остальной текст не изменён.

- [ ] **Step 3: Нормализовать CRLF**

Run: `perl -pi -e 's/\r?\n/\r\n/g' AGENTS.md`

- [ ] **Step 4: Commit**

Коммит только по явному запросу пользователя (иначе — оставить незакоммиченным).

### Task 6: Детерминированный тест git-модели коллекции

**Files:**
- Create: `tests/pdca-collection/git-model-sim.sh` (скрипт-проверка git-механики; вне продуктового кода)

**Interfaces:**
- Consumes: ничего (изолированный temp-repo).
- Produces: воспроизводимый тест, что цепочка веток, автокоммит и `merge --no-ff` по группе работают как в spec §6.

- [ ] **Step 1: Написать тест-скрипт**

Скрипт в `mktemp -d` создаёт git-репо, «main»; создаёт две «группы» в отдельных worktree; в каждой — цепочку веток задач (`t1`→`t2`) с автокоммитами; затем `git merge --no-ff` каждой ветки группы в `main`; проверяет: обе группы смержены, есть два merge-коммита, файлы обеих групп присутствуют.

- [ ] **Step 2: Запустить тест и убедиться, что он проходит**

Run: `bash tests/pdca-collection/git-model-sim.sh`
Expected: `OK: group-A merged, group-B merged, 2 merge commits`.

- [ ] **Step 3: Проверить сценарий конфликта (Review Focus)**

Добавить в скрипт второй кейс: обе группы меняют одну строку → `git merge --no-ff` второй группы даёт конфликт → скрипт ассертит `merge упал` (ненулевой код) и «группа failed».
Run: `bash tests/pdca-collection/git-model-sim.sh`
Expected: `OK: conflict detected, group marked failed`.

- [ ] **Step 4: Commit**

Коммит только по явному запросу.

### Task 7: Dry-run на синтетической коллекции

**Files:**
- Create: `docs/superpowers/plans/pdca-collection-dryrun.md` (чек-лист прогона, результат)

**Interfaces:**
- Consumes: скилл (Task 1–3), агент (Task 4).
- Produces: зафиксированное доказательство, что скилл запускает 2 группы × 2 задачи, одна падает, отчёт корректен.

- [ ] **Step 1: Подготовить синтетическую коллекцию**

2 группы × 2 задачи; в одной задаче второй группы заранее внесён дефект, который валит её `C`. Вход — список задач (по spec §non-goals вход формирует пользователь).

- [ ] **Step 2: Прогнать коллекцию и свериться с чек-листом**

Чек-лист (spec §10): (1) лейны параллельны, каждый в своём worktree; (2) внутри группы ветки цепочкой + автокоммит; (3) успешные группы мержены `--no-ff` по одной; (4) упавшая задача стопит свой лейн, вторая группа доходит до merge; (5) отчёт помечает `blocked`; (6) гейты `pdca-dotnet` не пропущены.

- [ ] **Step 3: Записать результат в `pdca-collection-dryrun.md`**

Зафиксировать: статус каждого пункта, evidence (команды/выводы), обнаруженные расхождения со спекой. Расхождения → новые задачи/правки.

---

## Self-Review

- **Spec coverage:** §3→Task 1/4/5; §4→Task 2; §5/§6/§8→Task 2; §7/§9/§11→Task 3; §10→Task 6/7; §12 (риски)→Review Focus + Task 3 fallback. Пробелов нет.
- **Step scan:** шаги — один результат (создать файл / написать раздел / проверка / нормализация). Нет «TBD»/«допиши тесты».
- **Type consistency:** `collection/<id>/group-<g>` и `collection/<id>/task-<g>-<k>` единообразны в Task 2/4/6; агент `lane`; скилл `pdca-collection`.
- **Review Focus:** 7 строк: Task 3 — fallback (и триггер); Task 6 — конфликт merge (`--abort`) и цепочка веток; loop-back>3 и CRLF — текстом скилла/базой `pdca-dotnet`; автокоммит на грязном дереве, одна группа и права на глобальные файлы — **добавлены в dry-run чек-лист** (Task 7); живой прогон отложен до рестарта.
- **Proportion:** план соразмерен спеке; тела кода/скилла не транскрибируются — указываются разделы spec и обязательные элементы.

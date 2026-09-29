# 01 — Сценарий (прогон №3)

## 1.1 Задача

**GitHub issue #112**, milestone `1.0.9-a`:

> **Потоковая выдача csv в Stream**
>
> «Терминал над `QueryCommand<TResult>` (и `EntityBuilder<TEntity>`), который пишет результат
> `Select` как csv прямо в выходной `Stream`, не создавая `TResult` на строку и не бокся значения.»

Это же название и тело получили **все** стороны. Больше ничего (ни рубрика, ни `todo`-файлы, ни
ссылки) не передавалось.

## 1.2 База и санитизация

- Исходный `HEAD` реального репо — `0f1f222` (ветка `1.0.9-a`).
- Клон → clean-room-репозиторий `/home/alex/sources/nextorm-cleanroom-112`, **один orphan-коммит**
  `BASE = 7aacaffb1491a6fffabbbf6863dcd6bc3b9a8089`.
- **Санитизация (паритет входа):** вычищен «спойлер»-близнец #39 (JSON-версия той же задачи с
  готовым RFC: API, `JsonRowWriter`, файлы, тест-план):
  - удалён `docs/specs/roadmap/todo_json_streaming.md`;
  - вычищены 3 указателя на него: `docs/specs/comparison/linq2db-backlog-gap-analysis.md`,
    `docs/specs/roadmap/design-review-todos-2026-09-24.md` (строка и блок), `docs/specs/roadmap/todo_interface_poco.md`
    (абзац «Не путать с потоковой выдачей JSON»).
  - `src/`/`tests/` не тронуты (`TableParameterValue.WriteJson` — существующий TVP-JSON, не спойлер).
- **Контроль:** `git rev-list --all --objects | grep -c todo_json_streaming` = 0 (рецепт недостижим
  и в истории blob'ов); `git diff 0f1f222..HEAD -- src tests` пуст.
- Та же санитизация выполнена в ворктри `upstream` (коммит `cd45f8d`).

## 1.3 Стороны

| Сторона | Где | Среда | Модели решений |
|---|---|---|---|
| `a1` | `nextorm-cleanroom-worktrees-112/exp-a1-bare` | bare-агент, без скиллов | — |
| `a2` | `…/exp-a2-skill` | первым действием скилл `implementing-todo-features` | — |
| `a3` | `…/exp-a3-pdca` | PDCA (`pdca-dotnet` + `nextorm-pdca`), автономный режим | `plan`/`check` = DS, `coder` = DS |
| `upstream` | `nextorm-worktrees-112/upstream` (реальный репо) | штатный проектный процесс | `plan`/`check` = GPT-6 Sol, `coder` = DS |

Профили: `a1`/`a2` — `~/.config/opencode/profiles/deepseek.jsonc` (oc-ds), `a3` — `test-pdca.jsonc`
(все агенты цикла — DS). Все запуски — `opencode run --pure --agent build --auto --format json`.

## 1.4 Промпты рук (вход)

Общий преамбул (одинаков для всех трёх рук), затем дословный текст issue:

- **CLEAN-ROOM:** читать/искать/менять только внутри текущего ворктри; не читать соседние репо,
  другие ворктри, `~/.local/share/opencode`, `/tmp`; временные файлы — внутри ворктри в `./tmp`.
- **СЕТЬ И ССЫЛКИ ЗАПРЕЩЕНЫ:** без интернета и переходов по ссылкам issue; #39 и `todo` про
  JSON-стриминг — запрещённый источник идей.
- **РЕАЛИЗАЦИЯ ОБЯЗАТЕЛЬНА:** только код (`src/**`) + тесты + доки EN+RU; закрытие «вне scope /
  уже реализовано / документацией» считается невыполнением.
- Соблюдать `AGENTS.md` (сборка, CRLF, CPM, `TreatWarningsAsErrors`, доки EN+RU при изменении API);
  `git push` запрещён; коммит не обязателен.

Различия по рукам: `a1` — «не загружай скиллы вообще»; `a2` — «первым действием загрузи
`implementing-todo-features` и работай по его шагам»; `a3` — «проведи цикл PDCA автономно,
`question` не использовать, до PLAN загрузи оверлей `nextorm-pdca`».

## 1.5 Паритет входа и его оговорка

Руки получили заголовок **и** тело issue текстом и работали без сети. `upstream`-агент получил то же
(заголовок + тело), но его собственный gather-сабагент сходил за issue #112 в GitHub по сети
(`Fetch GitHub issue #112`). Формально поле входа то же, но `upstream` мог видеть поля issue
(milestone/метки/комментарии) — см. `06-caveats.md`.

## 1.6 Изоляция и порядок

- Руки — в клоне clean-room от замороженной базы; `git pull` не делался, реализация #112 в реальном
  репо в клон не затекала.
- `upstream` — отдельный ворктри в реальном репо от `0f1f222` (тот же санитайз). Прогон и коммит
  сделал пользователь, `main`/`1.0.9-a` не затронуты (коммит `c29de9d` в ветке `exp112/upstream`).
- Порядок: `a1 → a2 → a3` последовательно (фоновый `clean-run-all.sh 5400 2` — 90-мин таймаут,
  до 2 попыток на руку); `upstream` шёл параллельно.

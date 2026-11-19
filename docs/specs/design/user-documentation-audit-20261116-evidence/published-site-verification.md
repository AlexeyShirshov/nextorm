# Published-site spot checks

Checked on 2026-11-16 with read-only HTTPS requests. These observations concern
the deployed site and NuGet pages, not a freshly built local site. No DocFX
command, build, package installation, or example execution was performed.

## Confirmed public internal documents

Both requests returned HTTP 200 and the expected internal-document title:

| URL | HTML title |
|---|---|
| https://alexeyshirshov.github.io/nextorm/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.html | Полная совместимость nextorm с NativeAOT без ручной настройки \| Nextorm |
| https://alexeyshirshov.github.io/nextorm/superpowers/plans/2026-10-04-linq-phase-0-plan.html | Этап 0 — Inventory: детальный план (LINQ-провайдер) \| Nextorm |

This confirms actual publication of these two documents. The DocFX configuration
also includes 27 Markdown files under `docs/superpowers`; the other 25 live URLs
were not requested, so this check does not certify their deployed status.

## Confirmed visible text/structure defects

- https://alexeyshirshov.github.io/nextorm/getting-started/04-dependency-injection.html
  displays the truncated options-path clause:
  `factory that calls `` This is the path used with the provider Use… methods.`
- https://alexeyshirshov.github.io/nextorm/advanced/api-reference.html returns
  HTTP 200; its HTML contains the detached `TableFunctionSchema` pipe row in a
  paragraph (`<p>| ...`), rather than as a table row.
- https://www.nuget.org/packages/nextorm.sqlite/1.0.9 presents stable version
  1.0.9 while its rendered README says
  `All current nextorm releases are prereleases, so --prerelease is required`
  and `Beta: every release so far is a prerelease`.

## All 28 heuristic anchor misses rejected for the deployed snapshot

The scanner's 28 candidate occurrences resolve to ten unique anchors across
eight destination pages. Each destination returned HTTP 200, and its HTML
contains the exact requested `id`:

| Destination under https://alexeyshirshov.github.io/nextorm/ | Exact ids found |
|---|---|
| `guide/05-grouping-and-aggregates.html` | `keeping-the-extreme-row-selectwheremax--selectwheremin` |
| `ru/guide/05-grouping-and-aggregates.html` | `выбор-экстремальной-строки-selectwheremax--selectwheremin` |
| `advanced/extensibility.html` | `value-converters-ipropertyvalueconverter--valueconvertertmodeltprovider`; `interceptors-iqueryinterceptor--iconnectioninterceptor` |
| `ru/advanced/extensibility.html` | `преобразователи-значений-ipropertyvalueconverter--valueconvertertmodeltprovider`; `перехватчики-iqueryinterceptor--iconnectioninterceptor` |
| `guide/provider-specific-sql/sqlite.html` | `json_each--json_tree-table-functions` |
| `ru/guide/provider-specific-sql/sqlite.html` | `табличные-функции-json_each--json_tree` |
| `guide/31-provider-specific-features.html` | `row-locking-for-update--for-share` |
| `ru/guide/31-provider-specific-features.html` | `блокировка-строк-for-update--for-share` |

The local heuristic removed punctuation differently from DocFX. These 28
occurrences must not be reported as broken anchors. This does not establish
all possible anchors, external links, or generated xref UIDs throughout the
corpus.

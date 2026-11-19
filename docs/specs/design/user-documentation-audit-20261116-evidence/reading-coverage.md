# Prose-reading coverage and provenance

Date: 2026-11-16. This ledger records reported reading coverage, not an API
correctness certificate or a completed ten-row score for every page.
Paths are relative to `/home/alex/sources/nextorm`.

## Scope and reading streams

| Stream | Scope | Coverage provenance |
|---|---|---|
| Onboarding | `docs/getting-started/*.md`, `docs/infrastructure/*.md`, `docs/connecting-providers/*.md` and exact RU counterparts | Full-read ledger returned by `ses_e1b4bf3f5ffe445nwZxbDzFa3O`; some EOFs retained from checkpoints |
| Landing pages | `readme.md`, `docs/index.md`, `docs/ru/index.md`, `docs/ru/overview.md`, `docs/ru/motivation.md`, both `string-format-invariant-provider.md` pages | Same stream; English `overview.md` and `motivation.md` absent rather than unread |
| Core query guides | Numbered guides 01-23, including both 13-prefixed pages, EN and RU | Full-read ledger returned by `ses_e1b4bb7caffe7J2SlneVPLRLQs`; most earlier EOF numbers not retained |
| Relationships | All five EN and five RU pages | Same stream; EN EOFs 436/177/383/396/555, RU eager-loading 384 and relationships 555; remaining RU full-read statements retained from checkpoints |
| Later guides | Numbered EN guides 24-36 | `ses_e1b4b8338ffeyRaUJVUg6CKZfD` reported full EN reading; its initial RU coverage was contradictory and was not accepted |
| Later RU guides | Numbered RU guides 24-35 | Re-read explicitly by `ses_e1b48bd0bffe4JBzmpy2gp7c0x`; EOFs 106/306/327/149/224/195/511/194/395/196/256/442 |
| Vector guide | `docs/ru/guide/36-ai-and-vector-search.md` | Explicit reading evidence from the later-guides stream; technical spot-check assigned separately |
| Provider-specific SQL | Six EN and six RU pages under `guide/provider-specific-sql/` | Separate completed batches by `ses_e1b4b8338ffeyRaUJVUg6CKZfD`; RU EOFs overview88/sqlite384/mysql249/postgresql623/sqlserver643/clickhouse525 |
| Advanced | Seven EN and seven RU pages | Four EN pages in initial later-guides batch; EN api-reference/limitations/native-extrema and all seven RU pages completed by `ses_e1b4af328ffeY4O1HF686W7EVu` |
| Scalar functions | Eleven EN and eleven RU pages | Initial reference batch plus two explicit RU completion batches by `ses_e1b4af328ffeY4O1HF686W7EVu`; EN reference 1-482 plus 483-527 combined |
| Engine | Four EN and four RU pages | Initial reference batch plus explicit RU completion batch, same stream |
| Comparisons | Five EN and five RU pages | Initial reference batch plus explicit RU capabilities/provider-features completion, same stream |
| Package README files | Eight `src/**/readme.md` files | Full-read ranges returned by `ses_e1b48bd0bffe4JBzmpy2gp7c0x` |
| Runnable examples | Eight `examples/**/README.md` files and `examples/VERIFICATION.md` | Onboarding and package-documentation streams |
| CONTRIBUTING | `CONTRIBUTING.md:1-93` | Read directly by primary; SDK/config discrepancy independently gathered by configuration scout |

## Reading limits

- Some extraordinarily long Markdown table rows were truncated by the read
  presentation. Not every character of those rows was reviewed. This is a
  tool-output limitation, not evidence of truncated source files.
- Earlier child-session checkpoints retained full-read statements but not all
  source excerpts or exact EOF numbers. Do not reconstruct missing excerpts or
  infer a complete per-page diagnostic from these statements.
- All numbered guide pages are included, including the untracked guide 36
  files present in the working tree. Existing edits were not reverted.
- Technical source verification is selective and recorded separately. Prose
  reading does not prove that every documented overload, provider guarantee,
  SQL fragment, or command is correct.
- No DocFX, builds, documentation-example execution, benchmark reruns, or live
  database tests were performed for this audit.
- `tools/stryker/README.md` and `assets/promo/README.md` were incidentally read.
  They are not part of the user-guide denominator.

## Directly corroborated source-text observations

- DI EN line18 / RU line17: missing method after the factory-call clause,
  followed by a bare double-backtick sequence.
- Aggregates EN lines3,42 / RU lines3,44: clauses end with a bare
  double-backtick sequence instead of a named API.
- API reference EN line240 / RU line238: adjacent `CommandTimeout` and
  `UseQueryCache` links lack a separator; a later option list has stray
  punctuation.
- CONTRIBUTING line51: repository SDK is described as .NET10, while
  `global.json:3-4` pins 11.0.100.
- Seven provider/EF package README files line12: all releases are said to be
  prereleases; official NuGet indexes contain stable 1.0.9 (separate evidence).

## Do not promote these observations to confirmed defects

- Version-dated benchmark snapshots and explicit provider verification limits
  are qualifications, not timeless-documentation failures by themselves.
- Undefined application-specific helpers in clearly illustrative API recipes
  do not make those recipes broken runnable tutorials.
- The duration page explicitly explains SQLite's differing placeholder after
  a shared SQL-shape example; the approximation alone is not a proven runtime
  failure.
- A `read` truncation of a long table row is not missing file content.

# User documentation audit fixes — 2026-11-16

**Score before → after:** not scored → not scored; targeted corrections only.

This record tracks the targeted corrections requested from
[`user-documentation-audit-20261116.md`](user-documentation-audit-20261116.md).
The original audit and evidence snapshots remain unchanged. These edits do not
constitute a full corpus audit and do not assign an audit score.

## Corrections

| Finding | Correction | Verification and limits |
|---|---|---|
| D001 | Corrected `CONTRIBUTING.md` to name the pinned .NET SDK 11.0.100 with `latestFeature`, package TFMs `net10.0` and `net11.0`, and host TFM `net11.0` for repository tests, benchmarks, and examples. | Values were checked against repository settings. No build was run. |
| D002 | Restored the 14 confirmed truncated fragments across the English and Russian API reference, DI, grouping, and paging pages. Restored projection item names, `StoreInCache`, `Desc`, `CreateDataContext`, `SqlFunctions.Sql`, and `Offset`. | API members and generic overload destinations were checked with Roslyn; the core XML documentation was checked for the enum descriptions. DocFX was not run. |
| D004 | Separated the `CommandTimeout`, `UseQueryCache`, and `UseCacheSlidingExpiration` references in both API-reference pages. | Static Markdown inspection only. |
| D005 | Excluded `superpowers/**` from DocFX content inputs. | The bounded link search found no inbound public-document links to this directory. DocFX and deployment were not run; already deployed pages are unaffected by this source change. |
| D006 | Changed the CSV example to rely on its captured `from` value and removed the extra positional argument. Updated the comment to say “captured value.” | Conservative sample correction; no runtime failure is claimed. The sample was not compiled or executed. |
| D008 | Updated the six provider READMEs and the EF Core README with stable `1.0.9` installation commands without prerelease flags and stable-version badge URLs. Reworded prerelease compatibility caveats and removed the obsolete beta-status claim from `CONTRIBUTING.md`, preserving the instruction to call out breaking changes. | Stable version `1.0.9` is recorded for the package IDs in the supplied NuGet evidence. No package was installed, and no compatibility guarantee is made. |
| D009 | Made the supplied SQLite connection disposable in both Quickstart examples. | Static sample inspection only; no execution. |
| D010 | Changed the four independent Quickstart overview items from a numbered list to bullets in English and Russian. | Static Markdown inspection only. |
| D011 | Added the SQLite README prerequisite for the existing `orders` table and a minimal schema matching `IOrder` (`id` integer primary key and non-null `total`). | Schema was checked against the documented entity mapping. The README sample was not run. |
| D012 | Moved `TableFunctionSchema` into the Mapping attributes table in both API-reference pages. | Static Markdown structure inspection only; DocFX was not run. |
| D013 | Replaced the statement that several `count` functions lack entity terminals with a distinction between row-count `Count()` and SQL aggregate expressions through `SqlFunctions.Sql`. | `Count` and `SqlFunctions.Sql` were checked with Roslyn. No claim is made that every SQL aggregate has a matching entity terminal. |
| D014 | Updated Quickstart `From` links and DI type-path links to the generic overloads used by the examples. | Generic signatures and XML documentation IDs were checked with Roslyn and the generated core XML documentation. No DocFX validation was run. |
| Setup guidance | Clarified in the English and Russian installation pages that package target frameworks are separate from the consuming project's implicit-usings and nullable settings. | Repository defaults and TFMs were checked in project settings. Package archives were not inspected; the text does not claim these settings propagate from packages. |

## Verification limits

The primary review confirmed that the nine original audit/evidence snapshots
and eleven pre-existing user source/test files remain hash-unchanged. It also
confirmed that all five new unique xref UIDs exist in the core XML
documentation.

Verification is limited to static review, repository-setting inspection,
bounded cross-reference/link checks, and the recorded Roslyn spot checks. No
build, test, package installation, DocFX generation, or published-site
deployment was run. This record does not claim that DocFX will resolve every
xref or that a deployed site has changed.

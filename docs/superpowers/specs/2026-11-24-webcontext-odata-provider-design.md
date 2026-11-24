# WebContext OData provider design

**Date:** 2026-11-24 · **Milestone:** 1.0.12 · **Issue:** [#51](https://github.com/AlexeyShirshov/nextorm/issues/51)

**Status:** Written design approved by the user on 2026-11-24. A separate implementation plan is next for review; none has been created or approved. Implementation is not authorized and has not begun.

## Intent and approach

WebContext lets applications query an existing OData v4 service through typed, read-only NextORM queries. It is a client provider, not an OData server. It does not promise OData v2 or v3, native or custom protocols, or full OData v4 coverage. gRPC could be separate future work; it does not require an extension point here.

The user chose provider-specific paths over an additional common operator plan with two backends, WebContext and FileContext. A common plan could share validation, but it would couple remote translation to local file execution and its different semantics. WebContext translates queries and handles transport; the service executes the queries. Reuse existing NextORM APIs and structures where useful; do not introduce a shared nonrelational planner, model, or executor. No common-core redesign is a precondition. The approved [Valkey #310](https://github.com/AlexeyShirshov/nextorm/issues/310) and [Mongo #411](https://github.com/AlexeyShirshov/nextorm/issues/411) designs are precedents, not dependencies or implementations for this feature.

## Agreed v1 contract

- The application owns the client model and explicitly maps entity sets, properties, and navigations. The provider never issues `GET /$metadata` and does not require a working metadata endpoint. Metadata discovery, tooling, and generation are a separate project.
- Translate supported query shapes to the service. The v1 operation scope is:

  | NextORM operation | OData translation |
  | --- | --- |
  | `Where` | `$filter` |
  | `OrderBy` | `$orderby` |
  | `Take` | `$top` |
  | `Select` of mapped fields | `$select` with mapped service property names |
  | `Skip` | `$skip` |
  | `Count` | Scalar collection `/$count` with the applicable predicate; no local row fetch or count |
  | Collection navigation `Any` within `Where` | `$filter` with `any()` or an `any` lambda on a mapped collection |
  | Terminal entity-query `Any()` | Boolean existence from a bounded server query, not `/$count` or `/any` |
  | Explicit free-text search | Standard OData v4 `$search` expressions, evaluated by the service and composable with `$filter` |
  | Direct or nested `LoadWith` | `$expand` |

  Examples show decoded query options; parameter handling and URI escaping are planning details:

  - Filter, order, and limit: `Orders?$filter=Amount gt 10&$orderby=Amount&$top=2`.
  - Select local `Id` and `Total` mapped to service names `OrderID` and `Amount`: `Orders?$select=OrderID,Amount`.
  - Skip 10 and take 5 in `OrderID` order: `Orders?$orderby=OrderID&$skip=10&$top=5`.
  - Count orders matching a predicate: `Orders/$count?$filter=Amount gt 10`.
  - Match orders with any lines: `Orders?$filter=Lines/any()` (false for an empty collection). Match orders with a line above a quantity threshold: `Orders?$filter=Lines/any(l:l/Quantity gt 10)`. Neither filter needs `$expand` or child downloads.
  - Check for an open order with terminal `Any()`: `Orders?$filter=Status eq 'Open'&$top=1`. For plain or filtered sources, request at most one matching entity, optionally selecting a minimal mapped field. Return a Boolean, keep membership and predicates on the server, and preserve source limits, including returning `false` for `Take(0)`. Do not enumerate the collection. This does not commit to a terminal `Any(predicate)` overload.
  - Search for urgent open orders: `Orders?$search=urgent&$filter=Status eq 'Open'`. Preserve both operations when composed. V1 accepts standard OData v4 search expressions, including quoted phrases, `AND`, `OR`, `NOT`, and grouping. Encode the complete expression as one `$search` query-option value at the URI boundary, preserving semantic quotes, grouping, and operators. The service evaluates matching; do not rewrite search to `$filter`, evaluate it locally, or fall back on rejection. This does not require a new local parser, specify a CLR API, or allow arbitrary raw URL injection.

- Reject a non-translatable query before sending an outbound data request. V1 does not promise arbitrary computed CLR projections, every operator composition, or every OData v4 operator. Never silently discard an operation or fetch all rows as a fallback. If the service rejects a translated query, surface a useful service error. Do not fall back to client filtering, sorting, joining, or extra entity reads to reconstruct a graph.
- Use existing `LoadWith` for service-side eager loading of mapped reference and collection navigations. For example, loading order lines and each line's product uses `Orders?$expand=Lines($expand=Product)`. A pair of keys does not define a remote navigation: reject the query if no matching navigation is mapped. Respect depth and service limits without fetching around rejection. This does not promise every `LoadWith` overload.
- Deny writes.

## Execution defaults and planning latitude

When collecting all requested rows, follow service-issued continuation links for the original result, respecting `Take`. Follow expanded collection continuations when returned as part of eager loading. These links page a server-produced result; they are not fallback queries or extra joins. Do not silently truncate a result or present an incomplete eager graph as complete. If required continuation processing fails, report failure.

Planning decides the transport, mapping configuration API, identity handling, parameter and URI escaping, and representative OData fixture. These choices are not user-approved architecture or new public API commitments.

## Acceptance evidence

- Verify query shapes against a representative OData v4 service. Capture requests and assert that `Where`, `OrderBy`, and `Take` produce the required server options and rows, without requesting `/$metadata` or relying on a working metadata endpoint.
- Verify that `Select` sends `$select` with mapped service names and returns the correctly projected result; verify that `Skip` with `Take` sends `$skip` and `$top` and returns the expected page.
- Verify that unfiltered and filtered `Count` return a server scalar via collection `/$count`, applying `$filter` when needed, without fetching or enumerating entities. Confirm that projection, paging, and count send no `/$metadata` request.
- Verify both navigation `any` forms against empty and nonempty collections and matching and nonmatching child predicates. Check the outgoing `$filter`, with no unnecessary `$expand` or child downloads.
- Verify terminal `Any()` returns `true` and `false` for matching and nonmatching sources, including filters, and returns `false` for `Take(0)`. Verify that plain and filtered sources request at most one matching entity and do not enumerate the entity collection.
- Verify that the outgoing `$search` decodes to one complete value for `"red apple" OR blue` and for a grouped expression using `AND` and `NOT`, with quotes, grouping, and operators intact. Verify that a request can also carry `$filter` and that results satisfy both options. Exercise a service-side `$search` rejection and verify that the error surfaces without a fallback request or local filtering.
- Verify mapped reference and collection eager loads, including nested `Lines` and `Product`, by checking both the server-side expansion and the resulting navigation values.
- Verify root and expanded collection continuation handling, no silent truncation, enforcement of `Take`, and explicit failure when a required continuation cannot be consumed.
- Verify that unsupported shapes, unmapped navigations, and writes send no data request. Exercise a service-side rejection, such as an expansion depth limit, and verify a useful error without client fallback or extra entity reads.

No live DBMS is required for this verification.

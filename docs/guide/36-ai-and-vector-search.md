# AI and vector search

This guide is for developers building semantic search or retrieval-augmented generation (RAG) with
nextorm. Use it to store embeddings produced by external models and retrieve the nearest documents with
the shared vector-search API. nextorm handles database storage and search only; it does not generate
embeddings, integrate with embedding models or large language models (LLMs), or build prompts.

## Before you begin

Before you use the examples, make sure that you have:

- A data context configured for PostgreSQL or SQL Server. See [PostgreSQL provider setup](../connecting-providers/postgres.md)
  or [SQL Server vector setup](../connecting-providers/sqlserver.md#vector-search).
- A database vector column and an entity property mapped to it. Set the column dimension to match your
  embedding model; see [PostgreSQL vector-search details](provider-specific-sql/postgresql.md#unified-nearest-neighbor-api)
  or [SQL Server vector setup](../connecting-providers/sqlserver.md#vector-search).
- An external embedding model or ready-to-use coordinates. Use the same model and preprocessing for
  stored documents and query vectors, and keep their dimensions consistent.

A typical retrieval-augmented generation (RAG) flow is:

1. An external embedding model converts each document into a vector.
2. The application stores the document and vector in a database.
3. At query time, the same embedding model and compatible preprocessing convert the user's query into
   a vector of the same dimensions.
4. nextorm asks the database for the nearest `topK` rows.
5. The application may pass the retrieved text, together with the user's question, to an external large
   language model (LLM).

## Shared vector and search API

`IVectorValue` is the shared vector-value contract and exposes `Dimensions`. `Vector` is the
provider-independent implementation:

```csharp
using NextORM.Core;

var vector = new Vector(new[] { 0.12f, -0.34f, 0.56f });
var parsed = Vector.Parse("[0.12,-0.34,0.56]");
System.Console.WriteLine($"{vector.Dimensions}; {parsed.Dimensions}");
```

Expected output:

```text
3; 3
```

### Validate vector values

The constructor copies its `IEnumerable<float>`, rejects `NaN` and infinity, and throws
`ArgumentNullException` for a null input. `Parse` accepts a bracketed, comma-separated list of finite
single-precision numbers in invariant number format; malformed input throws `FormatException`.

You can construct or parse an empty vector, but you cannot use it as a search query: `VectorSearch`
requires at least one dimension. The query vector must also match the mapped vector column's type and
dimension. The database may reject a query whose dimensions do not match its stored vectors.

### Vector equality and hashing

`Vector` uses exact value equality: two vectors are equal when they have the same dimension and each
coordinate has the same `float` value in the same order. The comparison uses no tolerance or similarity
metric; `+0f` and `-0f` compare equal. Otherwise identical vectors with either value at the same
coordinate have the same hash code. Use `VectorSearch` or `SqlFunctions.VectorDistance` when you need
similarity or distance instead.

`vector.Equals(null)` returns `false`. The `==` operator returns `true` for two `null` operands and `false`
for a `null`/non-null pair; `!=` returns the inverse. `==`, `Equals`, `HashSet<Vector>`,
`Dictionary<Vector, TValue>`, and LINQ `Distinct()` use this value equality. Call
`object.ReferenceEquals(first, second)` to check whether two references identify the same object.
`GetHashCode()` supports hash-based collections; its result is process-local and isn't a persisted
identifier or a measure of similarity.

For example, the constructor and parser produce one set entry for equal coordinates:

```csharp
using System.Collections.Generic;
using NextORM.Core;

var first = new Vector(new[] { 0.12f, -0.34f });
var second = Vector.Parse("[0.12,-0.34]");
var uniqueVectors = new HashSet<Vector> { first, second };
System.Console.WriteLine(uniqueVectors.Count);
```

Expected output:

```text
1
```

`EntityBuilder<TEntity>.VectorSearch<TVector>` accepts `vectorSelector`, `queryVector`, `metric`, and a
positive `topK`; the optional `mode` defaults to `VectorSearchMode.Exact`. It constrains `TVector` to
`IVectorValue` and returns a `QueryCommand<VectorSearchResult<TEntity>>`. Each result has the matched
`Entity` and nullable `double? Distance`. Lower distance means nearer for every shared metric:

| Metric | Meaning |
|---|---|
| `VectorDistanceMetric.Cosine` | Cosine distance |
| `VectorDistanceMetric.Euclidean` | Euclidean (L2) distance |
| `VectorDistanceMetric.DotProduct` | Negative inner product, not the positive dot-product similarity |

Choose the metric expected by the embedding model and database index. If the model expects normalized
vectors, keep that normalization consistent when storing and querying. Do not assume that normalization
makes distances from different metrics or models comparable, or that one universal score threshold
applies.

Assume `context` is configured for a supported provider, and `Document` maps `Category` and `Embedding`.
The variable `category` contains the category to match. The following query returns exact cosine
neighbors for that category:

```csharp
var queryVector = new Vector(new[] { 0.12f, -0.34f, 0.56f });
var exactHits = context.From<Document>()
    .Where(document => document.Category == category)
    .VectorSearch(
        document => document.Embedding,
        queryVector,
        VectorDistanceMetric.Cosine,
        topK: 5,
        mode: VectorSearchMode.Exact)
    .ToList();

foreach (var hit in exactHits)
{
    Document document = hit.Entity;
    double? distance = hit.Distance;
}
```

To request approximate search, set the mode explicitly:

```csharp
var approximateHits = context.From<Document>()
    .Where(document => document.Category == category)
    .VectorSearch(
        document => document.Embedding,
        queryVector,
        VectorDistanceMetric.Cosine,
        topK: 5,
        mode: VectorSearchMode.Approximate)
    .ToList();
```

The example materializes the command with `.ToList()`. `Where` filters are retained. Vector search
establishes its own distance-ascending order and `topK` limit, replacing an earlier caller ordering,
limit, or offset. SQL Server approximate search has stricter validation: it rejects a caller-supplied
ordering or paging rather than replacing it.

For a scalar distance in an ordinary projection, use the SQL marker
`SqlFunctions.VectorDistance(left, right, metric)`. It returns a nullable distance and is translated by
supported database providers; it does not itself add nearest-neighbor ordering or a `topK` limit:

```csharp
var distances = context.From<Document>()
    .Select(document => new
    {
        document.Id,
        Distance = SqlFunctions.VectorDistance(
            document.Embedding, queryVector, VectorDistanceMetric.Cosine)
    })
    .ToList();
```

## Map entities and store embeddings

### Map the vector column

Map the vector property like any other entity column. Set the database column's dimension to match the
embedding model's output. The example uses three coordinates for illustration; real models commonly use a
different fixed dimension:

```csharp
public sealed class Document
{
    public int Id { get; set; }
    public string Category { get; set; } = "";
    public string Body { get; set; } = "";
    public Vector Embedding { get; set; } =
        new Vector(new[] { 0f, 0f, 0f });
}
```

### Store a document and its embedding

Assume `category` and `body` contain the document data, and `embeddingValues` is an `IEnumerable<float>`
returned by an embedding service outside nextorm. Wrap the values in `Vector`, then use the ordinary insert
builder:

```csharp
var document = new Document
{
    Id = 1,
    Category = category,
    Body = body,
    Embedding = new Vector(embeddingValues)
};

context.CreateInsertBuilder<Document>()
    .Values(new[] { document })
    .Insert();
```

The entity mapping, table, column, and database schema are still application responsibilities. PostgreSQL
uses the pgvector extension and a dimensioned `vector(n)` column; SQL Server uses a dimensioned
`VECTOR(n)` column. nextorm does not generate this DDL or create vector indexes. See
[provider setup and details](#provider-setup-and-details) for setup and engine prerequisites.

## Exact and approximate search by provider

`Exact` and `Approximate` are distinct requests, not interchangeable performance hints. Unsupported
providers reject vector search; nextorm does not silently switch metrics or fall back to another search
mode.

The table summarizes exact and approximate behavior for each provider:

| Provider | `Exact` | `Approximate` |
|---|---|---|
| PostgreSQL | Uses pgvector distance functions to order the filtered rows by the selected distance. This is exact nearest-neighbor search; the function ordering is not eligible for HNSW/IVFFlat distance-operator indexes. | Uses the matching pgvector distance operator (`<=>`, `<->`, or `<#>`). This makes the query eligible for a matching HNSW or IVFFlat index, but the PostgreSQL planner decides whether to use it. |
| SQL Server | Uses `VECTOR_DISTANCE` and orders by the selected distance. The caller must declare SQL Server major version 17 or later; nextorm does not probe the engine. | Uses the explicitly configured `VECTOR_SEARCH` grammar. Requires the database preview feature and a matching DiskANN index; see [SQL Server approximate-search requirements](#sql-server-approximate-search-requirements). |
| Other providers, including in-memory | Not supported unless the dialect explicitly advertises vector-search support. | Not supported unless the dialect explicitly advertises vector-search support. |

The SQL Server version declaration is a library capability gate, not proof that the connected engine
build supports a chosen `VECTOR_SEARCH` grammar. Select `TopN` or `WithApproximate` explicitly for the
target engine. `TopN` has been live-verified on SQL Server `17.0.5005.3`; `WithApproximate` has SQL
generation coverage only and still needs verification against the target build.

### SQL Server approximate-search requirements

SQL Server approximate search requires a matching DiskANN vector index. The database preview feature
must be enabled; the indexed table needs a primary key and at least 100 non-NULL vectors. Index and
schema DDL are application-managed. The query must use a direct physical entity table and cannot include
joins, grouping, `DISTINCT`, a derived source, caller ordering, or paging. `Where` filters are retained,
but nextorm does not promise that the engine prefilters approximate candidates or provides a particular
recall guarantee or re-scoring strategy.

## Verify the search

When you run a vector query against your database, check the following:

- Confirm that each returned entity matches the query filters.
- Check that the result count does not exceed `topK`.
- Check that non-null `Distance` values appear in ascending order, and handle null distances explicitly.

Compare `Approximate` with `Exact` for the same query, metric, and filters to assess result quality. Do
not expect identical result sets or a guaranteed recall level. Inspect the actual execution plan to
confirm whether the expected vector index is used; PostgreSQL index eligibility does not guarantee that
the planner selects the index. Measure performance with your workload; neither mode promises a specific
latency or throughput.

## Provider setup and details

For provider configuration, SQL forms, and prerequisites, see:

- [PostgreSQL provider setup](../connecting-providers/postgres.md)
- [PostgreSQL unified nearest-neighbor API](provider-specific-sql/postgresql.md#unified-nearest-neighbor-api)
- [SQL Server provider — vector search](../connecting-providers/sqlserver.md#vector-search)
- [SQL Server-specific vector SQL](provider-specific-sql/sqlserver.md#vector-nearest-neighbor-search)

Hi everyone!

I've been working on [Nextorm](https://github.com/AlexeyShirshov/nextorm), an open-source ORM for .NET, for quite a while. AI coding agents have made development significantly faster and easier, but this is still a substantial engineering project for me. I'd like to share an important milestone: **version 1.0.9, the first stable release**.

The idea behind Nextorm is to combine Dapper-level performance with the convenience of generating SQL from C#. Instead of writing SQL by hand, you describe queries using strongly typed expressions, with compile-time checking of types and member references, IntelliSense, and refactoring support.

Conceptually, I see Nextorm as closer to LINQ to SQL. However, it exposes its own expression-based API rather than implementing a standard LINQ provider. Expression trees are used to generate SQL and build efficient result mappers for .NET types. In a JIT runtime, the mapper expressions are compiled into reusable delegates.

Nextorm supports SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, and ClickHouse. Its API covers a broad range of database capabilities, including joins, grouping, CTEs, window functions, and provider-specific constructs. One of my goals is to avoid forcing developers back to handwritten SQL as soon as a query becomes more complex than a basic SELECT.

Performance has been a key priority from the start, with Dapper as the reference point. In the published SQLite benchmarks, Nextorm is competitive with Dapper across many scenarios and faster in some. The results, benchmark conditions, and test code are available in the repository.

There is also **experimental NativeAOT support**. Under NativeAOT, the expression-based mappers can run through the expression interpreter rather than generating executable code at runtime. Support is currently verified for a subset of scenarios with SQLite on .NET 11 targeting Linux x64. An example application in the repository was published as a native executable without application-code changes, simply by enabling `PublishAot=true`. This is still partial support, not a guarantee that every query and provider will work under AOT.

Another feature is **query execution without materializing rows into DTOs**. You can obtain a `DbDataReader` for direct access or write query results straight to a `Stream` as JSON or CSV. This is useful for sending data to an HTTP response or a file without creating a DTO for every row or buffering the entire result set in memory.

I'd welcome your feedback, suggestions, and criticism. I'm particularly interested in how the query API feels to use and what capabilities you'd need for your own projects.

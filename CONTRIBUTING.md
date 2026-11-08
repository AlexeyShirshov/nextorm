# Contributing to Nextorm

Thanks for your interest in improving Nextorm! Contributions of code, tests, documentation, bug reports, and new ideas are welcome.

**`nextorm.extensions` is intentionally designed for contributions from third-party developers.** It is the place to propose reusable, optional query operators built on Nextorm's public extension APIs. You can contribute an operator to this repository or publish your own extension package independently—forking or modifying the core ORM is not required.

## Where to contribute

| Area | What belongs here |
| --- | --- |
| [`src/nextorm.extensions`](src/nextorm.extensions) | Optional, reusable query operators and other extensions built on the public Nextorm API. This is the preferred starting point for community extension proposals. |
| [`src/nextorm.core`](src/nextorm.core) and provider projects | Changes to the engine, public extension contracts, and database-provider behavior. Discuss significant API or architectural changes before implementation. |
| [`tests/nextorm.extensions.tests`](tests/nextorm.extensions.tests) | Unit tests, SQL-shape checks, component-contract tests, and plan-cache tests for extensions. |
| [`tests/nextorm.extensions.integration.tests`](tests/nextorm.extensions.integration.tests) | Real-database integration tests for the extensions package, kept separate from tests of built-in operators. |
| [`docs`](docs) and [`docs/ru`](docs/ru) | Documentation, examples, and API guides. |

The existing `SelectWhereMin` / `SelectWhereMax` and `SelectGroup` / `SelectGroupHaving` operators in `nextorm.extensions` are useful examples.

If an extension has a specialized audience or its own release schedule, a **separate NuGet package** using the same public APIs is equally welcome. You do not need the project's approval to build one.

## Proposing an extension

Before implementing a substantial new operator, [open an issue](https://github.com/AlexeyShirshov/nextorm/issues) to discuss:

- The problem it solves and a short example of the intended C# API.
- Expected query semantics, including edge cases and unsupported combinations.
- Which database providers should be supported and whether provider-specific SQL is needed.
- Whether it belongs in `nextorm.extensions` or is better suited to an independent package.

Small bug fixes, tests, and documentation improvements can go straight to a pull request. Search existing issues and pull requests before opening a duplicate.

## Building extensions

Use the [extensibility guide](docs/advanced/extensibility.md) for the public contracts and lifecycle, and the [extensions package guide](docs/advanced/extensions-package.md) for working examples.

For structural query operators:

1. Implement `IQueryComponent` and attach it through `EntityBuilder.WithComponent`, normally behind a convenient C# extension method.
2. Validate the supported query shapes during `Prepare`. Return an immutable `IQueryComponentDescriptor` that represents every SQL-affecting structural choice. Captured runtime values must remain parameterized.
3. Use `BuildQuery` to modify the query through public query-building primitives, **not** by assembling raw SQL. A warm plan-cache hit can skip `BuildQuery`, so the component must not depend on it executing for each query.
4. Choose a stable component `Slot` and the appropriate contribution mode (`AdditivePredicate` or `ExclusiveRowSource`). Compose existing predicates explicitly and respect the limit on exclusive row-source components.
5. Keep components free of global mutable state and internal/friend-assembly dependencies. Do not require `InternalsVisibleTo`, a translator registry, or changes to the engine for each operator.
6. State any provider limitations clearly. Reject unsupported configurations explicitly rather than silently changing the query's meaning; SQL-only components must not claim in-memory support.

An extension must preserve query semantics, parameter binding, and plan-cache correctness. Add tests for both cold and warm query plans, component composition, replacement by slot, and relevant error cases.

For other kinds of extension, use the documented public hooks (SQL functions, value converters, query filters, interceptors, naming conventions, or supported custom-dialect hooks) rather than forcing everything into a query component.

## Build and test

The repository uses **.NET 10**. From the repository root:

```bash
dotnet restore
dotnet build --no-restore
dotnet test tests/nextorm.extensions.tests -c Debug --filter-not-trait Category=Diagnostic
```

When changing public extension contracts, also run the relevant tests in `tests/nextorm.publicextensibility.tests` and `tests/nextorm.querycomponents.tests`.

For changes that execute SQL, run the live extension suite:

```bash
dotnet test tests/nextorm.extensions.integration.tests -c Debug --filter-not-trait Category=Diagnostic
```

These integration tests use real databases through container-backed test infrastructure. Configure Docker or Podman (including `DOCKER_HOST` where needed) before running them. A run that skips a provider does **not** establish correctness for that provider.

To run the whole solution, keep the integration test modules sequential because they share database containers and test data:

```bash
dotnet test --filter-not-trait Category=Diagnostic --max-parallel-test-modules 1
```

Follow the existing test patterns, and include regression tests for bug fixes. New functionality should cover SQL generation and live behavior on the supported providers; include plan-cache tests when query structure or parameters are involved. CI checks builds, tests, and coverage.

## Documentation and compatibility

- Document a new operator's purpose, syntax, examples, supported providers, and rejection boundaries. Update the relevant source README and public guides.
- Keep English and Russian documentation in sync when changing public APIs or examples (`docs/**` and `docs/ru/**`).
- Respect existing formatting and code conventions. Package versions are managed centrally in `Directory.Packages.props`; do not add per-project package versions.
- Nextorm is currently in beta, and public APIs may change between prereleases. Call out any breaking change explicitly in the pull request.

## Submitting a pull request

1. Fork the repository, create a focused branch, and make your changes.
2. Add or update tests and documentation appropriate to the change.
3. Run the relevant build and test commands.
4. Open a pull request against `main`. Link any related issue, describe the user-facing behavior and design choices, and report the tests run (including unavailable or skipped providers).

Please keep unrelated refactoring out of the same pull request so changes are easier to review. Feedback and iteration during review are part of the process.

By contributing code, you agree that your contribution will be distributed under the repository's [MIT license](LICENSE).

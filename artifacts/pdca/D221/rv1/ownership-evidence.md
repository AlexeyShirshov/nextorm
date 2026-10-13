# D221 (#221) R221-03 DI-ownership evidence map (E221-02), rv=1 / corrective n=2/3

Scenario → test `file:line` mapping for the R221-03 "DI ownership" requirement. All listed tests are
green (see `core-u1.log`, `core-u2.log`, `core-un.log`, `core-di.log`, `sqlite-qn.log`,
`integration-t221.log`, all exit 0).

| R221-03 scenario | Test (`file:line`) |
|---|---|
| Scoped instance reused within a scope | `tests/nextorm.core.tests/InterceptorDiRegistrationTests.cs:357` (`ScopedLifetime_IsReusedWithinAScopeAndDiffersAcrossScopes`) |
| Scoped instance differs across scopes (no cross-scope capture) | same test, `…:357` (asserts `scopedFromSecond != scopedFromFirst`) |
| Singleton identity shared across scopes | `…:383` (`SingletonLifetime_IsSharedAcrossScopes`) |
| Transient resolved per creation | `…:404` (`TransientLifetime_IsResolvedPerCreation`), `tests/nextorm.sqlite.tests/InterceptorDiRegistrationTests.cs:279` (`TransientInterceptor_IsResolvedPerCreation`) |
| No eager resolution at registration | `…:425` (`TypeEntry_IsNotResolvedEagerlyAtRegistration`; counter 0 before `CreateDataContext`, 1 after) |
| Builder does not dispose a DI-owned interceptor | `…:450` (`Disposal_IsOwnedByTheServiceProvider`; scope disposal disposes, builder does not) |
| Ordinary scoped builder route | `…:495` (`OrdinaryRouteWithoutInterceptors_CreatesContextsRepeatedly`), `…:163` (`MissingRegistration_FailsBeforeTheFactoryIsInvoked`) |
| Keyed builder route captures the scope provider | `…:473` (`KeyedBuilderRoute_CapturesTheScopeProvider`) |
| Generic service-key route (`AddInterceptor<T>()`) | `…:92` (`GenericOverload_ResolvesQueryInterceptorFromCapturedProvider`) |
| Interface service key | `…:132` (`InterfaceServiceKey_IsResolvedFromProvider`) |
| Abstract service key | `…:294` (`AbstractServiceKey_IsResolvedFromProvider`) |
| DI service factory (`AddScoped<IQueryInterceptor>(sp => …)`) | `…:312` (`DistinctAliases_AreNotMergedByTheirRuntimeImplementation`) |
| Missing registration fails before the factory, unwrapped native error | `…:163` (`MissingRegistration_FailsBeforeTheFactoryIsInvoked`, `probe.Invocations == 0`) |
| Standalone type entry fails before the factory | `…:182` (`StandaloneTypeEntry_FailsBeforeTheFactoryIsInvoked`) |
| Real-provider resolution + callbacks per provider | `tests/nextorm.integration.tests/CommonTestSuite.InterceptorDiTests.cs:23` (`DiRegisteredInterceptor_ObservesProviderQuery`, all five providers; `…:69` standalone rejection) |
| SQLite real callbacks (query/connection) | `tests/nextorm.sqlite.tests/InterceptorDiRegistrationTests.cs:114` query, `…:139` connection |

No cross-scope capture, no eager resolution and no builder disposal of DI-owned instances are covered by
the above; no open gap remains for R221-03.

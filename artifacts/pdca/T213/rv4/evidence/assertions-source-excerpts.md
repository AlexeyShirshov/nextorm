# T213 rv4 n2-3 — exact source excerpts for the quoted assertions

Every line below is emitted verbatim from the working tree (final DO revision). These are the
assertions quoted in `../evidence-ledger.md`; the ledger adds `file:line` and the producing log.

```text
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:129: parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 10 }, "only the full composite-key match is grouped");
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:179: parents[0].Profile!.Id.Should().Be(50, "the full composite key selects the matching reference");
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:361: parents[0].Children.Where(c => c.Id == 10).Should().HaveCount(2, "duplicate junction rows are distinct occurrences");
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:306: parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:307: parents[0].Notes.Select(n => n.Id).Should().Equal(20);
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:399: joined.Count().Should().Be(2, "the non-list terminal counts parents, not denormalized rows");
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:474: parents.Should().HaveCount(2, "same first key component with a different later component must stay separate");
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:475: parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);
tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs:476: parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(11);
tests/nextorm.core.tests/JoinIntoCompositeNullSemanticsTests.cs:184: parents[0].Profile.Should().BeNull(
tests/nextorm.core.tests/JoinIntoCompositeNullSemanticsTests.cs:207: parents[0].Children.Should().BeEmpty("an absent LEFT child contributes no element");
tests/nextorm.core.tests/JoinIntoIdentitySelectorCacheTests.cs:106: first.Should().BeOfType<JoinIntoKey>("a composite key is represented structurally");
tests/nextorm.core.tests/JoinIntoIdentitySelectorCacheTests.cs:108: first.Should().NotBe(composite!(new CompositeKeyEntity { First = 1, Second = 3 }));
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:43: act.Should().Throw<NotSupportedException>().WithMessage("*same property type*");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:66: act.Should().Throw<NotSupportedException>().WithMessage("*requires an explicitly declared relationship*");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:101: act.Should().Throw<NotSupportedException>().WithMessage("*no junction metadata*");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:198: act.Should().Throw<NotSupportedException>().WithMessage("*expects a OneToMany relationship*");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:217: act.Should().NotThrow("a composite declared relationship key is supported since #213");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:230: act.Should().Throw<NotSupportedException>().WithMessage("*derived (As) or joined projection source*");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:260: act.Should().Throw<NotSupportedException>().WithMessage("*JoinInto cannot be combined with Select*");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:272: act.Should().Throw<NotSupportedException>().WithMessage("*JoinInto cannot be combined with As*");
tests/nextorm.core.tests/JoinIntoRejectionTests.cs:319: act.Should().Throw<NotSupportedException>().WithMessage("*at most 7 child collections*");
tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:24: private const string CompositeRejectionMessage = "composite-junction M:N is not supported; see #223";
tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:107: act.Should().Throw<NotSupportedException>().WithMessage(CompositeRejectionMessage);
tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:108: ctx.ConnectionsRequested.Should().Be(0, "the rejection happens at preparation; no SQL is executed");
tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:141: act.Should().NotThrow("the scalar junction link source is not rejected");
tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:189: .WithMessage("*do not cover every composite key component*");
tests/nextorm.core.tests/JoinIntoCompositeJunctionRejectionTests.cs:190: ctx.ConnectionsRequested.Should().Be(0, "a malformed junction key is rejected at declaration; no SQL is executed");
tests/nextorm.core.tests/JoinIntoCompositeCarrierSpikeTests.cs:25: => ((Action)(() => Prepare(ctx, joined)))
tests/nextorm.core.tests/JoinIntoCompositeCarrierSpikeTests.cs:26: .Should().Throw<QueryPreparationException>(because);
tests/nextorm.sqlite.tests/JoinIntoMixedProjectionPublicTests.cs:29: act.Should().Throw<QueryPreparationException>(
tests/nextorm.sqlite.tests/JoinIntoMixedProjectionPublicTests.cs:30: "a mapped entity plus scalar whole-projection has no working public terminal and must fail closed");
tests/nextorm.sqlite.tests/JoinIntoExecutionTests.cs:183: .ToCommand().Cache.Should().BeTrue("declaring a JoinInto must not disable the plan cache");
tests/nextorm.sqlite.tests/JoinIntoExecutionTests.cs:195: CountingInterceptor.Executions.Should().Be(2,
tests/nextorm.sqlite.tests/JoinIntoExecutionTests.cs:196: "the cached plan is reused and each repeated execution still issues exactly one denormalized round trip");
tests/nextorm.sqlite.tests/JoinIntoCompositeSqlGenerationTests.cs:146: .And.Contain("left join ck_child as t2 on (t1.tenant_id = t2.tenant_id and t1.id = t2.parent_id)");
tests/nextorm.mariadb.tests/JoinIntoCompositeSqlGenerationTests.cs:169: .And.Contain("left join ck_child as t2 on (t1.tenant_id = t2.tenant_id and t1.id = t2.parent_id)");
tests/nextorm.mariadb.tests/JoinIntoCompositeSqlGenerationTests.cs:61: sql.Should().Be(
tests/nextorm.mariadb.tests/JoinIntoCompositeSqlGenerationTests.cs:80: sql.Should().Be(
```

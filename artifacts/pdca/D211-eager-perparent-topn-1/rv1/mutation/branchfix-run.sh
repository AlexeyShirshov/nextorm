#!/usr/bin/env bash
# Collects coverage for the two D:06 changed-branch follow-up tests (Feature=D211).
set -u
cd /home/alex/sources/nextorm
dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- -noColor \
  -method nextorm.sqlite.tests.EagerLoadPerParentPagingTests.Split_OrdinalOrderBy_RendersTheColumnIndexInTheRankingWindow || exit $?
dotnet run --project tests/nextorm.sqlite.tests -c Debug --no-build -- -noColor \
  -method nextorm.sqlite.tests.EagerLoadPerParentPagingTests.Split_PagedChild_RepeatedOnSameContext_StaysCorrectThroughThePlanCache || exit $?

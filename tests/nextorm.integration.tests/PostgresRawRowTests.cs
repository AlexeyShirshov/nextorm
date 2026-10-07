using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using Npgsql;

namespace NextORM.Integration.Tests;

/// <summary>
/// Serializes <see cref="PostgresRawRowTests"/> against every other integration collection: its
/// clean-catalog limitation tests drop and recreate PostgreSQL type extensions and reload the
/// process-wide implicit Npgsql data source, which must not race a concurrent PostgreSQL read.
/// </summary>
[CollectionDefinition("PostgresRawRow", DisableParallelization = true)]
public sealed class PostgresRawRowCollection;

/// <summary>
/// Container-backed execution tests for the PostgreSQL raw <c>ROW(...)</c>/composite materialization
/// (issue #194, D194.3). These tests really execute against PostgreSQL: a run where PostgreSQL is
/// <c>skipped</c> is not passing evidence (see <c>.opencode/skills/running-integration-tests</c>).
///
/// Anonymous <c>ROW(a, ...)</c> of arity 1..7 materializes into the matching <see cref="Tuple"/> from
/// the driver's null-preserving <c>System.Object[]</c>; a caller-registered named composite (S5) is read
/// through a caller-owned <see cref="NpgsqlDataSource"/> passed to the existing external-connection
/// <see cref="PostgresDataContext"/> ctor. The NULL and guard matrices mirror the core unit tests.
/// </summary>
[Collection("PostgresRawRow")]
public sealed class PostgresRawRowTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    // ---------------------------------------------------------------- S1 positive (arity 1..7)

    [Fact]
    public void AnonymousRow_Arity1_ShouldMaterializeIntoSystemTuple()
        => ReadSingle<Tuple<int>>("select row(1::integer) as v").Should().Be(Tuple.Create(1));

    [Fact]
    public void AnonymousRow_Arity2_ShouldMaterializeIntoSystemTuple()
        => ReadSingle<Tuple<int, string>>("select row(1::integer, 'a'::text) as v")
            .Should().Be(Tuple.Create(1, "a"));

    [Fact]
    public void AnonymousRow_Arity3_ShouldMaterializeIntoSystemTuple()
        => ReadSingle<Tuple<int, string, bool>>("select row(1::integer, 'a'::text, true) as v")
            .Should().Be(Tuple.Create(1, "a", true));

    [Fact]
    public void AnonymousRow_Arity4_ShouldMaterializeIntoSystemTuple()
        => ReadSingle<Tuple<int, string, bool, double>>(
                "select row(1::integer, 'a'::text, true, 2.5::double precision) as v")
            .Should().Be(Tuple.Create(1, "a", true, 2.5));

    [Fact]
    public void AnonymousRow_Arity5_ShouldMaterializeIntoSystemTuple()
        => ReadSingle<Tuple<int, string, bool, double, decimal>>(
                "select row(1::integer, 'a'::text, true, 2.5::double precision, 3.5::numeric) as v")
            .Should().Be(Tuple.Create(1, "a", true, 2.5, 3.5m));

    [Fact]
    public void AnonymousRow_Arity6_ShouldMaterializeIntoSystemTuple()
        => ReadSingle<Tuple<int, string, bool, double, decimal, long>>(
                "select row(1::integer, 'a'::text, true, 2.5::double precision, 3.5::numeric, 42::bigint) as v")
            .Should().Be(Tuple.Create(1, "a", true, 2.5, 3.5m, 42L));

    [Fact]
    public void AnonymousRow_Arity7_ShouldMaterializeIntoSystemTuple()
    {
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");

        ReadSingle<Tuple<int, string, bool, double, decimal, long, Guid>>(
                "select row(1::integer, 'a'::text, true, 2.5::double precision, 3.5::numeric, 42::bigint, "
                + "'11111111-2222-3333-4444-555555555555'::uuid) as v")
            .Should().Be(Tuple.Create(1, "a", true, 2.5, 3.5m, 42L, guid));
    }

    // ---------------------------------------------------------------- R194-NULL

    [Fact]
    public void WholeRecordNull_ShouldMaterializeAsClrNull()
        => ReadSingle<Tuple<int?, string?>>("select null::record as v").Should().BeNull();

    [Fact]
    public void AllFieldsNull_ShouldMaterializeNonNullTupleWithNullItems()
    {
        var row = ReadSingle<Tuple<int?, string?>>("select row(null::integer, null::text) as v");

        row.Should().NotBeNull();
        row!.Item1.Should().BeNull();
        row.Item2.Should().BeNull();
    }

    [Fact]
    public void NullableItemWithSqlNull_ShouldMaterializeNullItem()
    {
        var row = ReadSingle<Tuple<int, string?>>("select row(1::integer, null::text) as v");

        row.Item1.Should().Be(1);
        row.Item2.Should().BeNull();
    }

    [Fact]
    public void NonNullableValueTypeItemWithSqlNull_ShouldThrowNeverDefault()
    {
        using var ctx = Provider.CreateContext();
        var act = () =>
        {
            using var result = ctx.ExecuteRaw("select row(null::integer, null::text) as v");
            result.Read<Tuple<int, string>>();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("PostgreSQL raw-row materialization failed:*")
            .Which.InnerException.Should().BeNull("there is no driver exception for a plain SQL NULL");
    }

    // ---------------------------------------------------------------- R194-ERROR guards

    [Fact]
    public void ValueTuple_ShouldGuard()
        => AssertGuard<(int, string)>("select row(1::integer, 'a'::text) as v");

    [Fact]
    public void NestedRow_ShouldGuard()
        => AssertGuard<Tuple<int, int>>("select row(row(1::integer, 'a'::text), 2::integer) as v");

    [Fact]
    public void TupleBeyondSeven_ShouldGuard()
    {
        var tuple8 = typeof(Tuple<,,,,,,,>).MakeGenericType(
            typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
            typeof(Tuple<int>));

        var ex = ReadWith(tuple8, "select row(1, 2, 3, 4, 5, 6, 7, 8) as v");

        ex.Should().BeOfType<NotSupportedException>();
        ex!.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void MixedRecordAndScalar_ShouldGuard()
        => AssertGuard<Tuple<int, string>>("select row(1::integer, 'a'::text) as v, 5::integer as n");

    [Fact]
    public void MultipleRecordColumns_ShouldGuard()
        => AssertGuard<Tuple<int, string>>(
            "select row(1::integer, 'a'::text) as v, row(2::integer, 'b'::text) as w");

    [Fact]
    public void EmptyRow_ShouldSurfaceContractedError_NotGenericTupleReason()
    {
        // A real empty ROW() is a contracted unsupported/data-mismatch shape; it must not fall back to
        // the generic "an anonymous ROW must be read as System.Tuple<...>" reason.
        using var ctx = Provider.CreateContext();
        var act = () =>
        {
            using var result = ctx.ExecuteRaw("select row() as v");
            result.Read<Tuple<int>>();
        };

        var ex = act.Should().Throw<Exception>().Which;
        ex.Message.Should().MatchRegex("^PostgreSQL raw-row materialization (failed|is not supported):");
        ex.Message.Should().NotContain("must be read as System.Tuple");
    }

    // ---------------------------------------------------------------- S5 named composite

    [Fact]
    public void NamedComposite_RegisteredViaMapComposite_ShouldMaterialize()
    {
        var schema = "d194_rawrow_" + Guid.NewGuid().ToString("N")[..10];
        var connectionString = PostgresContainer.ConnectionString;
        ExecuteDdl(connectionString, $"create schema {schema}");
        ExecuteDdl(connectionString, $"create type {schema}.pt_composite as (a integer, b text)");
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(connectionString)
                .MapComposite<PtComposite>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

            var rows = ctx.ExecuteRaw(
                $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v").Read<PtComposite>();

            rows.Should().ContainSingle();
            rows[0].A.Should().Be(1);
            rows[0].B.Should().Be("a");
        }
        finally
        {
            ExecuteDdl(connectionString, $"drop schema if exists {schema} cascade");
        }
    }

    [Fact]
    public void UnregisteredNamedComposite_ShouldGuard()
    {
        var schema = "d194_rawrow_" + Guid.NewGuid().ToString("N")[..10];
        var connectionString = PostgresContainer.ConnectionString;
        ExecuteDdl(connectionString, $"create schema {schema}");
        ExecuteDdl(connectionString, $"create type {schema}.pt_composite as (a integer, b text)");
        // The shared data source's catalog may have loaded before this type existed; reload it so the
        // authoritative predicate reports the genuine composite (PostgresCompositeType) rather than a
        // cold UnknownBackendType, which would degrade to the ordinary no-match error.
        WarmPostgresTypeCatalog();
        try
        {
            using var ctx = Provider.CreateContext();
            var act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v");
                result.Read<PtComposite>();
            };

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ")
                .And.Contain("MapComposite");
        }
        finally
        {
            ExecuteDdl(connectionString, $"drop schema if exists {schema} cascade");
        }
    }

    // ---------------------------------------------------------------- C1 converter regression pin

    [Fact]
    public void ConverterBackedJsonColumn_ShouldStillMaterializeViaConverter()
    {
        // Pre-D194 the PostgreSQL MapColumnExpression named-composite branch hijacked *any* class column
        // before the [JsonColumn] converter was resolved, so this LINQ read bypassed its converter and
        // failed inside GetFieldValue<...>. The pin keeps the converter path for ordinary entity columns.
        var ctx = Provider.CreateContext();
        const string table = "d194_rawrow_json_probe";
        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create table {table} (id bigint primary key, data jsonb)");
        try
        {
            var payload = new RawRowJsonPoco { Name = "carol", Count = 7 };
            ctx.CreateInsertBuilder<IRawRowJsonProbe>()
                .Values(new RawRowJsonProbe { Id = 1, Data = payload })
                .Insert();

            var row = ctx.From<RawRowJsonProbe>().Where(x => x.Id == 1).ToList().Single();

            row.Data.Name.Should().Be("carol");
            row.Data.Count.Should().Be(7);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
        }
    }

    // ---------------------------------------------------------------- whole-composite NULL

    [Fact]
    public void WholeCompositeSqlNull_ShouldMaterializeAsClrNull()
    {
        var schema = CreateCompositeSchema();
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtComposite>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

            using var result = ctx.ExecuteRaw($"select null::{schema}.pt_composite as v");
            var rows = result.Read<PtComposite>();

            rows.Should().ContainSingle();
            rows[0].Should().BeNull();
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void NonNullableStructCompositeSqlNull_ShouldWrapWithInnerPreserved()
    {
        // A SQL NULL read into a non-nullable struct composite reaches the driver (no IsDBNull
        // null-branch); the driver failure must be wrapped in the R194-ERROR InvalidOperationException
        // with the driver exception preserved as InnerException.
        var schema = CreateCompositeSchema();
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtStruct>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

            var act = () =>
            {
                using var result = ctx.ExecuteRaw($"select null::{schema}.pt_composite as v");
                result.Read<PtStruct>();
            };

            var ex = act.Should().Throw<InvalidOperationException>()
                .WithMessage("PostgreSQL raw-row materialization failed:*").Which;
            ex.InnerException.Should().NotBeNull("the driver exception must be preserved");
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void NullableStructComposite_Registered_ShouldMaterialize()
    {
        // The driver reports a registered struct composite's CLR type (PtStruct), not Nullable<PtStruct>,
        // so the named-composite eligibility must unwrap the declared Nullable<> for the branch to be
        // reachable. Whole-record SQL NULL then maps to CLR null.
        var schema = CreateCompositeSchema();
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtStruct>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

            using (var result = ctx.ExecuteRaw($"select row(2::integer, 'b'::text)::{schema}.pt_composite as v"))
            {
                var rows = result.Read<PtStruct?>();

                rows.Should().ContainSingle();
                rows[0].Should().NotBeNull();
                rows[0]!.Value.A.Should().Be(2);
                rows[0]!.Value.B.Should().Be("b");
            }

            using (var nullResult = ctx.ExecuteRaw($"select null::{schema}.pt_composite as v"))
            {
                nullResult.Read<PtStruct?>().Should().ContainSingle().Which.Should().BeNull();
            }
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    // ---------------------------------------------------------------- S1 through the tupled resolver

    [Fact]
    public void TupleEnabledDataSource_AnonymousRow_ShouldStillMaterialize()
    {
        // Caller-owned data source with the typed tuple resolver enabled; the seam still reads the
        // driver's null-preserving object[] via GetValue, so enabling tuples does not change the result.
        using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
            .EnableRecordsAsTuples()
            .Build();
        using var connection = dataSource.CreateConnection();
        using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

        using var result = ctx.ExecuteRaw("select row(1::integer, 'a'::text) as v");
        result.Read<Tuple<int, string>>().Should().ContainSingle().Which.Should().Be(Tuple.Create(1, "a"));
    }

    [Fact]
    public void TupleEnabledDataSource_NestedRow_ShouldGuardNotDataMismatch()
    {
        // S9 through the tupled resolver: even with the typed tuple resolver enabled the seam reads the
        // untyped GetValue for the record column, which is still a null-preserving object[] whose nested
        // ROW element is itself an object[] (D194.1 spike, Npgsql 10.0.3). The nested-ROW guard must fire
        // instead of the field conversion surfacing a data-mismatch InvalidOperationException.
        using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
            .EnableRecordsAsTuples()
            .Build();
        using var connection = dataSource.CreateConnection();
        using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

        var act = () =>
        {
            using var result = ctx.ExecuteRaw("select row(row(1::integer, 'a'::text), 2::integer) as v");
            result.Read<Tuple<int, int>>();
        };

        act.Should().Throw<NotSupportedException>()
            .WithMessage("PostgreSQL raw-row materialization is not supported:*nested ROW*")
            .Which.Message.Should().NotContain("materialization failed");
    }

    // ---------------------------------------------------------------- C3 arity (PostgreSQL)

    [Fact]
    public void RecordWiderThanDeclaredTuple_ShouldThrowContractedError()
        => AssertDataMismatch<Tuple<int>>("select row(1::integer, 2::integer) as v");

    [Fact]
    public void RecordNarrowerThanDeclaredTuple_ShouldThrowContractedError()
        => AssertDataMismatch<Tuple<int, string>>("select row(1::integer) as v");

    // ---------------------------------------------------------------- mixed composite + scalar

    [Fact]
    public void MixedRegisteredNamedCompositeAndScalar_ShouldGuard()
    {
        var schema = CreateCompositeSchema();
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtComposite>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

            var act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v, 5::integer as n");
                result.Read<PtComposite>();
            };

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void MixedAnonymousRecordAndScalar_DeclaredAsUnregisteredComposite_ShouldGuard()
    {
        // An unregistered named composite declared against a mixed record + scalar result set must
        // guard, not fall through to the generic "no mapped property" entity error.
        using var ctx = Provider.CreateContext();
        var act = () =>
        {
            using var result = ctx.ExecuteRaw("select row(1::integer, 'a'::text) as v, 5::integer as n");
            result.Read<PtComposite>();
        };

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    [Fact]
    public void MixedUnregisteredNamedCompositeAndScalar_ShouldGuard()
    {
        // A column cast to an *unregistered* named composite mixed with a scalar must guard, not fall
        // through to the generic "no mapped property" entity error.
        var schema = CreateCompositeSchema();
        // Reload the shared catalog after the type exists so the predicate reports the genuine
        // composite; otherwise the cold UnknownBackendType degrades to the ordinary no-match error.
        WarmPostgresTypeCatalog();
        try
        {
            using var ctx = Provider.CreateContext();
            var act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v, 5::integer as n");
                result.Read<PtComposite>();
            };

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    // ---------------------------------------------------------------- S2 pin (multi-scalar unchanged)

    [Fact]
    public void MultiScalarResultSet_ShouldStillMaterializeAsMappedEntity()
    {
        using var ctx = Provider.CreateContext();
        using var result = ctx.ExecuteRaw("select 1::integer as a, 'x'::text as b");

        var rows = result.Read<ScalarPairDto>();

        rows.Should().ContainSingle();
        rows[0].A.Should().Be(1);
        rows[0].B.Should().Be("x");
    }

    [Fact]
    public void EmptyResultSet_RegisteredCompositeRead_ShouldReturnEmptyWithoutThrowing()
    {
        // T11 integration empty-reader row (#203): the raw-row mapper is built from the reader's column
        // metadata before any row is read, so a `where false` result set still classifies the registered
        // composite at preparation time and then yields an empty list instead of throwing.
        var schema = CreateCompositeSchema();
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtComposite>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());
            using var result = ctx.ExecuteRaw(
                $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v where false");

            result.Read<PtComposite>().Should().BeEmpty();
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void MixedUnregisteredNamedComposite_PositionalRecord_ShouldGuardNotCtorMessage()
    {
        // The unregistered composite is mixed with a scalar and the declared result is a positional
        // record (no public parameterless ctor). The R194-ERROR guard must decide the raw-row shape
        // before name-mapping validation, so the generic "requires a public parameterless
        // constructor" message must not surface.
        var schema = CreateCompositeSchema();
        // Reload the shared catalog after the type exists so the predicate reports the genuine
        // composite; otherwise the cold UnknownBackendType degrades to the ordinary no-match error.
        WarmPostgresTypeCatalog();
        try
        {
            using var ctx = Provider.CreateContext();
            var act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v, 5::integer as n");
                result.Read<PositionalComposite>();
            };

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ")
                .And.NotContain("parameterless constructor");
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    // ---------------------------------------------------------------- T12 multi-column ordinals

    [Fact]
    public void MixedRegisteredNamedCompositeAtLaterOrdinal_ShouldGuard()
    {
        // T12: a registered genuine composite at a *later* ordinal (scalar first) is still detected by
        // the multi-column classification loop, mirroring the first-ordinal registered case above.
        var schema = CreateCompositeSchema();
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtComposite>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

            var act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select 5::integer as n, row(1::integer, 'a'::text)::{schema}.pt_composite as v");
                result.Read<PtComposite>();
            };

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void MixedUnregisteredNamedCompositeAtLaterOrdinal_ShouldGuard()
    {
        // T12: an unregistered genuine composite at a later ordinal (scalar first) must be identified by
        // the authoritative predicate at that ordinal, not only at ordinal 0.
        var schema = CreateCompositeSchema();
        WarmPostgresTypeCatalog();
        try
        {
            using var ctx = Provider.CreateContext();
            var act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select 5::integer as n, row(1::integer, 'a'::text)::{schema}.pt_composite as v");
                result.Read<PtComposite>();
            };

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void ScalarColumnMapsWhileCompositeAtLaterOrdinalIsIgnored_ShouldMaterialize()
    {
        // T12: a scalar column that maps by name still materializes while an unmapped unregistered
        // composite sits at a later ordinal. The composite guard is only consulted when nothing maps, so
        // the ordinary entity read succeeds instead of throwing (resolvable-but-unmapped columns are
        // ignored by design).
        var schema = CreateCompositeSchema();
        WarmPostgresTypeCatalog();
        try
        {
            using var ctx = Provider.CreateContext();
            using var result = ctx.ExecuteRaw(
                $"select 5::integer as a, row(1::integer, 'a'::text)::{schema}.pt_composite as v");

            var rows = result.Read<ScalarPairDto>();

            rows.Should().ContainSingle();
            rows[0].A.Should().Be(5);
            rows[0].B.Should().BeNull();
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void UnmappedColumnType_OrdinaryNoMatchRead_ShouldKeepTheEntityError()
    {
        // An ordinary entity no-match read over an unmapped column type (hstore) must keep the
        // "None of the result-set columns ..." error, never a misleading named-composite diagnostic.
        // The extension is created on a separate connection, then the shared implicit data source is
        // reloaded so the read connection resolves hstore (public.hstore / PostgresBaseType) instead of
        // leaving it unresolved; hstore is not a genuine composite either way.
        ExecuteDdl(PostgresContainer.ConnectionString, "create extension if not exists hstore");
        WarmPostgresTypeCatalog();
        AssertFixtureCatalogWarm("select 'a=>1'::hstore as v", "public.hstore");
        using var ctx = Provider.CreateContext();

        var act = () =>
        {
            using var result = ctx.ExecuteRaw("select 'a=>1'::hstore as v, 5::integer as n");
            result.Read<ScalarPairDto>();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*")
            .Which.Message.Should().NotContain("named composite");
    }

    [Fact]
    public void UnmappedLtreeColumn_OrdinaryNoMatchRead_ShouldKeepTheEntityError()
    {
        // ltree is schema-qualified (public.ltree) but is not a composite; the exception shape
        // (NotSupportedException), not the dotted name, is the discriminator, so the read keeps the
        // established "None of the result-set columns ..." error and never surfaces a misleading
        // named-composite diagnostic. The extension is created on a separate connection, then the shared
        // implicit data source is reloaded so the read connection resolves ltree.
        ExecuteDdl(PostgresContainer.ConnectionString, "create extension if not exists ltree");
        WarmPostgresTypeCatalog();
        AssertFixtureCatalogWarm("select 'a.b.c'::ltree as v", "public.ltree");
        using var ctx = Provider.CreateContext();

        var act = () =>
        {
            using var result = ctx.ExecuteRaw("select 'a.b.c'::ltree as v, 5::integer as n");
            result.Read<ScalarPairDto>();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("None of the result-set columns*")
            .Which.Message.Should().NotContain("named composite");
    }

    [Fact]
    public void UnmappedHstoreColumn_OnCleanCatalogConnection_ShouldTakeOrdinaryNoMatch()
    {
        // Corrected behaviour (#203): when a type catalog is loaded *before* the extension exists, the
        // driver reports the column as UnknownBackendType — not a genuine composite — so the authoritative
        // predicate is false and the read takes the ordinary "None of the result-set columns" error. The
        // shared implicit data source may already be warm, so this test isolates itself on a distinct
        // connection string — a distinct Npgsql data source whose catalog is deliberately loaded while
        // hstore is absent (a genuinely clean catalog; see the D-PROBE note on A1/A2 in the status file).
        // Assert the ordinary no-match error and that no named-composite diagnostic leaks in. The
        // classification must not query the catalog, reload types or issue a second command.
        var connectionString = PostgresContainer.ConnectionString;
        try
        {
            ExecuteDdl(connectionString, "drop extension if exists hstore cascade");

            var cleanConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = "nextorm_clean_catalog_hstore_" + Guid.NewGuid().ToString("N")[..10]
            }.ConnectionString;

            // Opening the clean data source now loads its type catalog while hstore is absent.
            using (var clean = new NpgsqlConnection(cleanConnectionString))
            {
                clean.Open();
            }

            ExecuteDdl(connectionString, "create extension if not exists hstore");

            // Setup precondition: hstore is present on the server, yet the connection under test still
            // uses the intended cold catalog (not the warm shared implicit data source).
            AssertExtensionPresent(connectionString, "hstore");
            AssertFixtureCatalogCold(cleanConnectionString, "select 'a=>1'::hstore as v");

            using var ctx = new PostgresDataContext(cleanConnectionString, new DataContextBuilder());
            var act = () =>
            {
                using var result = ctx.ExecuteRaw("select 'a=>1'::hstore as v, 5::integer as n");
                result.Read<ScalarPairDto>();
            };

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("None of the result-set columns*")
                .Which.Message.Should().NotContain("named composite");
        }
        finally
        {
            // Restore hstore (with a fresh OID) and reload the shared catalog for the other tests.
            ExecuteDdl(connectionString, "create extension if not exists hstore");
            WarmPostgresTypeCatalog();
        }
    }

    [Fact]
    public void SingleUnmappedHstoreColumn_OnCleanCatalogConnection_ShouldTakeOrdinaryNoMatch()
    {
        // T01 single-column cold state (#203): the mixed test above pins the multi-column branch; this one
        // exercises the SINGLE unresolvable-column branch of RawMapperFactory. The cold catalog leaves the
        // driver reporting hstore as UnknownBackendType, the authoritative predicate is false, and the
        // ordinary "None of the result-set columns" error is kept — never the named-composite diagnostic.
        // Classification must not warm the catalog or reload types inline. Same isolation and lifecycle as
        // the mixed clean-catalog test: a unique ApplicationName avoids the warm shared implicit data
        // source, and the extension is dropped before that catalog is loaded.
        var connectionString = PostgresContainer.ConnectionString;
        try
        {
            ExecuteDdl(connectionString, "drop extension if exists hstore cascade");

            var cleanConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = "nextorm_clean_catalog_hstore_single_" + Guid.NewGuid().ToString("N")[..10]
            }.ConnectionString;

            // Opening the clean data source now loads its type catalog while hstore is absent.
            using (var clean = new NpgsqlConnection(cleanConnectionString))
            {
                clean.Open();
            }

            ExecuteDdl(connectionString, "create extension if not exists hstore");

            AssertExtensionPresent(connectionString, "hstore");
            AssertFixtureCatalogCold(cleanConnectionString, "select 'a=>1'::hstore as v");

            using var ctx = new PostgresDataContext(cleanConnectionString, new DataContextBuilder());
            var act = () =>
            {
                using var result = ctx.ExecuteRaw("select 'a=>1'::hstore as v");
                result.Read<ScalarPairDto>();
            };

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("None of the result-set columns*")
                .Which.Message.Should().NotContain("named composite");
        }
        finally
        {
            // Restore hstore (with a fresh OID) and reload the shared catalog for the other tests.
            ExecuteDdl(connectionString, "create extension if not exists hstore");
            WarmPostgresTypeCatalog();
        }
    }

    [Fact]
    public void UnmappedLtreeColumn_OnCleanCatalogConnection_ShouldTakeOrdinaryNoMatch()
    {
        // Cold-state counterpart of the warm ltree test (#203): a catalog loaded before ltree exists
        // leaves the driver reporting UnknownBackendType, so the predicate is false and the read keeps the
        // established ordinary "None of the result-set columns" error — never the named-composite
        // diagnostic. This mirrors the hstore clean-catalog test (same isolation, same assertion shape), so
        // ltree is not evidenced only by the warm state. Classification must not reload types or issue a
        // second command while the reader is open.
        var connectionString = PostgresContainer.ConnectionString;
        try
        {
            ExecuteDdl(connectionString, "drop extension if exists ltree cascade");

            var cleanConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = "nextorm_clean_catalog_ltree_" + Guid.NewGuid().ToString("N")[..10]
            }.ConnectionString;

            // Opening the clean data source now loads its type catalog while ltree is absent.
            using (var clean = new NpgsqlConnection(cleanConnectionString))
            {
                clean.Open();
            }

            ExecuteDdl(connectionString, "create extension if not exists ltree");

            AssertExtensionPresent(connectionString, "ltree");
            AssertFixtureCatalogCold(cleanConnectionString, "select 'a.b.c'::ltree as v");

            using var ctx = new PostgresDataContext(cleanConnectionString, new DataContextBuilder());
            var act = () =>
            {
                using var result = ctx.ExecuteRaw("select 'a.b.c'::ltree as v, 5::integer as n");
                result.Read<ScalarPairDto>();
            };

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("None of the result-set columns*")
                .Which.Message.Should().NotContain("named composite");
        }
        finally
        {
            // Restore ltree (with a fresh OID) and reload the shared catalog for the other tests.
            ExecuteDdl(connectionString, "create extension if not exists ltree");
            WarmPostgresTypeCatalog();
        }
    }

    [Fact]
    public void SingleUnmappedLtreeColumn_OnCleanCatalogConnection_ShouldTakeOrdinaryNoMatch()
    {
        // T02 single-column cold state (#203): the mixed ltree test above pins the multi-column branch;
        // this one exercises the SINGLE unresolvable-column branch. A cold catalog leaves the driver
        // reporting ltree as UnknownBackendType, so the authoritative predicate is false and the ordinary
        // "None of the result-set columns" error is kept — never the named-composite diagnostic. Isolation
        // and lifecycle mirror the mixed clean-catalog test; classification must not warm the catalog or
        // reload types inline.
        var connectionString = PostgresContainer.ConnectionString;
        try
        {
            ExecuteDdl(connectionString, "drop extension if exists ltree cascade");

            var cleanConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = "nextorm_clean_catalog_ltree_single_" + Guid.NewGuid().ToString("N")[..10]
            }.ConnectionString;

            // Opening the clean data source now loads its type catalog while ltree is absent.
            using (var clean = new NpgsqlConnection(cleanConnectionString))
            {
                clean.Open();
            }

            ExecuteDdl(connectionString, "create extension if not exists ltree");

            AssertExtensionPresent(connectionString, "ltree");
            AssertFixtureCatalogCold(cleanConnectionString, "select 'a.b.c'::ltree as v");

            using var ctx = new PostgresDataContext(cleanConnectionString, new DataContextBuilder());
            var act = () =>
            {
                using var result = ctx.ExecuteRaw("select 'a.b.c'::ltree as v");
                result.Read<ScalarPairDto>();
            };

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("None of the result-set columns*")
                .Which.Message.Should().NotContain("named composite");
        }
        finally
        {
            // Restore ltree (with a fresh OID) and reload the shared catalog for the other tests.
            ExecuteDdl(connectionString, "create extension if not exists ltree");
            WarmPostgresTypeCatalog();
        }
    }

    // ---------------------------------------------------------------- T07 cold genuine composite

    [Fact]
    public void ColdCatalogGenuineComposite_ShouldTakeOrdinaryNoMatch_NotNamedComposite()
    {
        // T07: the type is created *after* a unique cold data source loaded its catalog, so the driver
        // reports the genuine composite as UnknownBackendType. The authoritative predicate is false and
        // the read takes the ordinary no-match error — never a named-composite diagnostic. The catalog is
        // deliberately never warmed here: classification must not query the catalog, reload types or issue
        // a second command. Cover a single unmappable column and a mixed composite + scalar result.
        var connectionString = PostgresContainer.ConnectionString;
        var schema = "d194_rawrow_" + Guid.NewGuid().ToString("N")[..10];
        var coldConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "nextorm_cold_catalog_composite_" + Guid.NewGuid().ToString("N")[..10]
        }.ConnectionString;

        // Open the cold data source while the composite type does not exist yet (unique ApplicationName so
        // it does not share the warm implicit data source).
        using (var cold = new NpgsqlConnection(coldConnectionString))
        {
            cold.Open();
        }

        ExecuteDdl(connectionString, $"create schema {schema}");
        ExecuteDdl(connectionString, $"create type {schema}.pt_composite as (a integer, b text)");
        try
        {
            // The type exists server-side (committed on another connection), yet the cold connection still
            // cannot classify it.
            AssertExtensionPresent(connectionString, $"{schema}.pt_composite");

            using var ctx = new PostgresDataContext(coldConnectionString, new DataContextBuilder());

            AssertFixtureCatalogCold(coldConnectionString,
                $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v");
            var single = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v");
                result.Read<ScalarPairDto>();
            };
            single.Should().Throw<InvalidOperationException>()
                .WithMessage("None of the result-set columns*")
                .Which.Message.Should().NotContain("named composite");

            AssertFixtureCatalogCold(coldConnectionString,
                $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v, 5::integer as n");
            var mixed = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v, 5::integer as n");
                result.Read<ScalarPairDto>();
            };
            mixed.Should().Throw<InvalidOperationException>()
                .WithMessage("None of the result-set columns*")
                .Which.Message.Should().NotContain("named composite");
        }
        finally
        {
            ExecuteDdl(connectionString, $"drop schema if exists {schema} cascade");
        }
    }

    // ---------------------------------------------------------------- T14 transaction visibility

    [Fact]
    public void TransactionVisibility_UncommittedRows_RegisteredCompositeReadUsesTheSameReader()
    {
        // T14: within one connection/transaction, set up uncommitted state (table + rows) and read a
        // registered genuine composite from it. Uncommitted rows are visible only through the connection
        // and transaction the raw read runs on, so a fallback connection, a second command on the open
        // reader or a transaction loss would fail or return no rows. The explicit connection assertion
        // pins "uses that reader only".
        var schema = CreateCompositeSchema();
        var table = schema + ".t";
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtComposite>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using (var setup = connection.CreateCommand())
            {
                setup.Transaction = transaction;
                setup.CommandText =
                    $"create table {table} (id integer, a integer, b text); "
                    + $"insert into {table} values (1, 7, 'z'); "
                    + $"insert into {table} values (2, 8, 'y');";
                setup.ExecuteNonQuery();
            }

            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());
            ((ITransactionManager)ctx).UseTransaction(transaction);

            ctx.GetConnection().Should().BeSameAs(
                connection,
                "classification must use the caller's connection, not a fallback");

            using var result = ctx.ExecuteRaw(
                $"select row(t.a, t.b)::{schema}.pt_composite as v from {table} t order by t.id");
            var rows = result.Read<PtComposite>();

            rows.Should().HaveCount(2);
            rows[0].A.Should().Be(7);
            rows[0].B.Should().Be("z");
            rows[1].A.Should().Be(8);
            rows[1].B.Should().Be("y");

            transaction.Connection.Should().BeSameAs(connection);
            connection.State.Should().Be(System.Data.ConnectionState.Open);
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    // ---------------------------------------------------------------- S7 / S8 / S13

    [Fact]
    public void RegisteredNamedCompositeDeclaredAsSystemTuple_ShouldGuard()
    {
        var schema = CreateCompositeSchema();
        try
        {
            using var dataSource = new NpgsqlDataSourceBuilder(PostgresContainer.ConnectionString)
                .MapComposite<PtComposite>($"{schema}.pt_composite")
                .Build();
            using var connection = dataSource.CreateConnection();
            using var ctx = new PostgresDataContext(connection, new DataContextBuilder());

            var act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    $"select row(1::integer, 'a'::text)::{schema}.pt_composite as v");
                result.Read<Tuple<int, string>>();
            };

            act.Should().Throw<NotSupportedException>()
                .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ")
                .And.Contain("System.Tuple");
        }
        finally
        {
            DropCompositeSchema(schema);
        }
    }

    [Fact]
    public void AnonymousRowDeclaredAsDto_ShouldGuard()
        => AssertGuard<ScalarPairDto>("select row(1::integer, 'a'::text) as v");

    [Fact]
    public void NonAllowListedRecordField_ShouldGuard()
        => AssertGuard<Tuple<int, JsonDocument>>("select row(1::integer, '{\"a\":1}'::jsonb) as v");

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Warms the shared implicit Npgsql data source's type catalog after an extension is created on a
    /// separate connection. Context connections use the same connection string, hence the same implicit
    /// data source, so the reload makes the newly created types resolvable for subsequent reads.
    /// </summary>
    private static void WarmPostgresTypeCatalog()
    {
        using var warm = new NpgsqlConnection(PostgresContainer.ConnectionString);
        warm.Open();
        warm.ReloadTypes();
    }

    /// <summary>
    /// Precondition guard: verifies the shared type catalog actually resolves the fixture type, so a cold
    /// catalog is reported as "fixture catalog not warm" instead of the misleading named-composite verdict.
    /// </summary>
    private static void AssertFixtureCatalogWarm(string sql, string expectedDataTypeName)
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        var resolved = false;
        try
        {
            resolved = reader.GetDataTypeName(0) == expectedDataTypeName
                && reader.GetPostgresType(0) is Npgsql.PostgresTypes.PostgresBaseType;
        }
        catch (Exception)
        {
            // The driver could not classify the column at all: a cold catalog, handled by the assertion.
        }

        Assert.True(resolved, "fixture catalog not warm");
    }

    /// <summary>
    /// Precondition guard for the clean-catalog limitation tests: verifies the fixture type really exists
    /// on the server (so a failure cannot be blamed on the extension not being created).
    /// </summary>
    private static void AssertExtensionPresent(string connectionString, string typeName)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"select to_regtype('{typeName}') is not null";
        Assert.True((bool)command.ExecuteScalar()!, $"fixture type {typeName} not present on the server");
    }

    /// <summary>
    /// Precondition guard for the clean-catalog limitation tests: verifies the connection string's
    /// implicit Npgsql data source still cannot classify the fixture type because its catalog was loaded
    /// before the extension existed, so the read genuinely exercises the intended cold catalog rather than
    /// a warm one.
    /// </summary>
    private static void AssertFixtureCatalogCold(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        var cold = false;
        try
        {
            cold = reader.GetPostgresType(0) is Npgsql.PostgresTypes.UnknownBackendType;
        }
        catch (Exception)
        {
            // The driver could not classify the column at all: still a cold catalog.
            cold = true;
        }

        Assert.True(cold, "fixture catalog unexpectedly warm");
    }

    private T ReadSingle<T>(string sql)
    {
        using var ctx = Provider.CreateContext();
        using var result = ctx.ExecuteRaw(sql);
        var rows = result.Read<T>();
        rows.Should().ContainSingle();
        return rows[0];
    }

    private void AssertDataMismatch<T>(string sql)
    {
        using var ctx = Provider.CreateContext();
        var act = () =>
        {
            using var result = ctx.ExecuteRaw(sql);
            result.Read<T>();
        };

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization failed: ");
    }

    private static string CreateCompositeSchema()
    {
        var schema = "d194_rawrow_" + Guid.NewGuid().ToString("N")[..10];
        ExecuteDdl(PostgresContainer.ConnectionString, $"create schema {schema}");
        ExecuteDdl(PostgresContainer.ConnectionString, $"create type {schema}.pt_composite as (a integer, b text)");
        return schema;
    }

    private static void DropCompositeSchema(string schema)
        => ExecuteDdl(PostgresContainer.ConnectionString, $"drop schema if exists {schema} cascade");

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private void AssertGuard<T>(string sql)
    {
        using var ctx = Provider.CreateContext();
        var act = () =>
        {
            using var result = ctx.ExecuteRaw(sql);
            result.Read<T>();
        };

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().StartWith("PostgreSQL raw-row materialization is not supported: ");
    }

    private Exception? ReadWith(Type resultType, string sql)
    {
        using var ctx = Provider.CreateContext();
        var read = typeof(ProcedureResult)
            .GetMethods()
            .Single(m => m.Name == nameof(ProcedureResult.Read)
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 0)
            .MakeGenericMethod(resultType);

        using var result = ctx.ExecuteRaw(sql);
        try
        {
            read.Invoke(result, null);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            return ex.InnerException;
        }
    }

    private static void ExecuteDdl(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public sealed class PtComposite
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    /// <summary>A non-nullable struct composite; a SQL NULL read must fail through the R194 contract.</summary>
    public struct PtStruct
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    /// <summary>A positional record (no public parameterless ctor) used to pin guard ordering.</summary>
    public sealed record PositionalComposite(int A, string? B);

    /// <summary>A multi-scalar raw result-set DTO (S2 pin): the ordinary entity-by-name path.</summary>
    public sealed class ScalarPairDto
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    /// <summary>The converter-backed POCO stored through <see cref="JsonColumnAttribute"/> (C1 pin).</summary>
    public sealed class RawRowJsonPoco
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }

    [SqlTable("d194_rawrow_json_probe")]
    public interface IRawRowJsonProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("data")]
        [JsonColumn]
        RawRowJsonPoco Data { get; set; }
    }

    public sealed class RawRowJsonProbe : IRawRowJsonProbe
    {
        public long Id { get; set; }
        public RawRowJsonPoco Data { get; set; } = new();
    }
}

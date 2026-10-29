using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T296b (#296, D4) / A20: a captured nullable value must not freeze an untyped SQL Server parameter
/// when the first preparation observes <c>null</c>. The test drives the real cache path with the same
/// expression shape and a captured variable that transitions null -> 42 -> null, and records the bound
/// <see cref="SqlParameter"/> metadata (<c>DbType</c>, <c>SqlDbType</c>, precision/scale, value) before
/// each execution.
/// </summary>
// Consolidated: the shared "Query plan cache" collection is defined in Support/QueryPlanCacheCollection.cs (T339, #339).
[Collection("Query plan cache")]
public sealed class T296bSqlServerParameterMetadataTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Fact]
    public void NullToValueToNull_ShouldKeepParameterMetadataAndRefreshTheValue()
    {
        var ctx = (DataContext)_sut.DataProvider;
        Execute(ctx, "drop table if exists t296b_meta_rows");
        Execute(ctx, "create table t296b_meta_rows (id bigint not null primary key, n int null)");
        Execute(ctx, "insert into t296b_meta_rows (id, n) values (1, null), (2, 42), (3, 7)");

        try
        {
            int? captured = null;
            var preparedCommands = new List<DbPreparedQueryCommand<long>>();
            var records = new List<MetadataRecord>();

            foreach (var step in new int?[] { null, 42, null })
            {
                captured = step;
                var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
                    ctx.From<IT296bMetaRow>().Where(x => x.N == captured).Select(x => x.Id),
                    createEnumerator: false,
                    storeInCache: true,
                    CancellationToken.None);

                // The metadata is read BEFORE the execution: on a cache hit the planner has already
                // refreshed the value, and the provider type must not depend on it.
                var parameter = prepared.DbCommand.Parameters.Cast<SqlParameter>().Should().ContainSingle().Subject;
                records.Add(new MetadataRecord(
                    step is null ? "null" : step.Value.ToString(),
                    parameter.ParameterName,
                    parameter.DbType.ToString(),
                    parameter.SqlDbType.ToString(),
                    parameter.Precision,
                    parameter.Scale,
                    parameter.Value is DBNull ? "DBNull" : parameter.Value?.ToString() ?? "null"));

                preparedCommands.Add(prepared);
                var ids = ctx.ToList(prepared);
                ids.Should().Equal(step is null ? new[] { 1L } : step.Value == 42 ? new[] { 2L } : new[] { 3L },
                    "the transition execution must observe the current captured value");
            }

            // The three preparations reuse one compiled command (the transition is a real cache hit).
            preparedCommands[1].Should().BeSameAs(preparedCommands[0]);
            preparedCommands[2].Should().BeSameAs(preparedCommands[0]);

            records.Select(r => r.DbType).Should().OnlyContain(t => t == "Int32",
                "a cold null must not leave the parameter untyped");
            records.Select(r => r.SqlDbType).Should().OnlyContain(t => t == "Int",
                "the captured int? maps to the provider's 32-bit integer type (SqlDbType.Int)");
            records.Select(r => (r.Precision, r.Scale)).Distinct().Should().ContainSingle(
                "precision/scale must stay stable across the whole transition");
            records.Select(r => r.Value).Should().Equal("DBNull", "42", "DBNull");
            records.Select(r => r.Step).Should().Equal("null", "42", "null");

            var metadataPayload = JsonSerializer.Serialize(new
            {
                provider = "sqlserver",
                capturedType = "int?",
                column = "t296b_meta_rows.n (int null)",
                steps = records,
            });
            TestContext.Current.TestOutputHelper?.WriteLine("T296B_METADATA " + metadataPayload);

            var dump = Environment.GetEnvironmentVariable("NEXTORM_T296B_EVIDENCE_DUMP");
            if (!string.IsNullOrEmpty(dump))
                File.AppendAllText(
                    dump,
                    "provider=sqlserver test=T296b_ParameterMetadata T296B_METADATA " + metadataPayload + "\n");
        }
        finally
        {
            Execute(ctx, "drop table if exists t296b_meta_rows");
        }
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var command = ((DataContext)ctx).CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private sealed record MetadataRecord(
        string Step,
        string ParameterName,
        string DbType,
        string SqlDbType,
        byte Precision,
        byte Scale,
        string Value);
}

/// <summary>Isolated metadata fixture: one NULL row (id 1) and non-null rows (ids 2 and 3).</summary>
[SqlTable("t296b_meta_rows")]
public interface IT296bMetaRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("n")]
    int? N { get; set; }
}

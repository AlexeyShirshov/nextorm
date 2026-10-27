using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T323 (#323) D5b: the issue's quarterly example against a live PostgreSQL server. A joined
/// <c>SelectGroup</c> projection — <c>date_trunc('quarter', OrderedAt)</c> as the key and a
/// <c>sum</c> of two measures — must return the same rows as the explicit
/// <c>GroupBy(...).Select(...)</c> form, with the same row count and the same key set. The marker
/// tables are class-owned and created/dropped per fact, so the suite seed schema is not involved.
/// </summary>
[Collection("Postgres")]
public sealed class T323SelectGroupPostgresSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    /// <summary>The joined projection is the issue's example verbatim (product, quarter, revenue, qty).</summary>
    [Fact]
    public void QuarterlyProjection_ShouldMatchExplicitGrouping()
    {
        CreateFixture();
        try
        {
            var inferred = InferredQuarterlyRows();
            var explicitRows = ExplicitQuarterlyRows();

            inferred.Should().HaveCount(4, "two products across two quarters form four groups");
            inferred.Should().BeEquivalentTo(explicitRows);

            var widgetQ1 = inferred.Single(r => r.ProductID == 1 && r.SalesQuarter == new DateTime(2023, 1, 1));
            widgetQ1.ProductName.Should().Be("widget");
            widgetQ1.QuarterlyRevenue.Should().Be(150.00m);
            widgetQ1.QuarterlyQty.Should().Be(3);

            var gadgetQ2 = inferred.Single(r => r.ProductID == 2 && r.SalesQuarter == new DateTime(2023, 4, 1));
            gadgetQ2.ProductName.Should().Be("gadget");
            gadgetQ2.QuarterlyRevenue.Should().Be(30.00m);
            gadgetQ2.QuarterlyQty.Should().Be(5);
        }
        finally
        {
            DropFixture();
        }
    }

    /// <summary>The second assertion: identical cardinality and an identical <c>(product, quarter)</c> key set.</summary>
    [Fact]
    public void QuarterlyProjection_ShouldKeepTheSameRowCountAndKeySet()
    {
        CreateFixture();
        try
        {
            var inferred = InferredQuarterlyRows();
            var explicitRows = ExplicitQuarterlyRows();

            inferred.Should().HaveSameCount(explicitRows);

            var inferredKeys = inferred.Select(r => (r.ProductID, r.ProductName, r.SalesQuarter)).ToList();
            var explicitKeys = explicitRows.Select(r => (r.ProductID, r.ProductName, r.SalesQuarter)).ToList();

            inferredKeys.Should().BeEquivalentTo(explicitKeys);
            inferredKeys.Should().BeEquivalentTo(new (int ProductId, string ProductName, DateTime? Quarter)[]
            {
                (1, "widget", new DateTime(2023, 1, 1)),
                (1, "widget", new DateTime(2023, 4, 1)),
                (2, "gadget", new DateTime(2023, 1, 1)),
                (2, "gadget", new DateTime(2023, 4, 1)),
            });
        }
        finally
        {
            DropFixture();
        }
    }

    private List<QuarterlyRow> InferredQuarterlyRows() => _sut.DataProvider
        .From<T323SgLine>()
        .Join(_sut.DataProvider.From<T323SgProduct>(), (line, product) => line.ProductId == product.Id)
        .SelectGroup(p => new QuarterlyRow
        {
            ProductID = p.Item1.ProductId,
            ProductName = p.Item2.Name,
            SalesQuarter = SqlFunctions.Sql.date_trunc("quarter", p.Item1.OrderedAt),
            QuarterlyRevenue = SqlFunctions.Sql.sum(p.Item1.LineTotal),
            QuarterlyQty = SqlFunctions.Sql.sum(p.Item1.OrderQty),
        })
        .ToList();

    // The oracle groups by exactly the keys the inferred form derives from its projection — the
    // product id, the product name and the quarter expression — so the two forms are comparable and
    // the statement is valid SQL (PostgreSQL rejects a projected column that is not in GROUP BY).
    private List<QuarterlyRow> ExplicitQuarterlyRows() => _sut.DataProvider
        .From<T323SgLine>()
        .Join(_sut.DataProvider.From<T323SgProduct>(), (line, product) => line.ProductId == product.Id)
        .GroupBy(p => new
        {
            p.Item1.ProductId,
            p.Item2.Name,
            Quarter = SqlFunctions.Sql.date_trunc("quarter", p.Item1.OrderedAt),
        })
        .Select(p => new QuarterlyRow
        {
            ProductID = p.Item1.ProductId,
            ProductName = p.Item2.Name,
            SalesQuarter = SqlFunctions.Sql.date_trunc("quarter", p.Item1.OrderedAt),
            QuarterlyRevenue = SqlFunctions.Sql.sum(p.Item1.LineTotal),
            QuarterlyQty = SqlFunctions.Sql.sum(p.Item1.OrderQty),
        })
        .ToList();

    /// <summary>The flat projection shape shared by both query forms.</summary>
    public sealed class QuarterlyRow
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public DateTime? SalesQuarter { get; set; }

        public decimal QuarterlyRevenue { get; set; }

        public int QuarterlyQty { get; set; }
    }

    /// <summary>An order line: the aggregate source, joined to its product by <c>ProductId</c>.</summary>
    [SqlTable("t323_sg_line")]
    public sealed class T323SgLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("ordered_at")]
        public DateTime? OrderedAt { get; set; }

        [Column("line_total")]
        public decimal LineTotal { get; set; }

        [Column("order_qty")]
        public int OrderQty { get; set; }
    }

    /// <summary>The joined product dimension.</summary>
    [SqlTable("t323_sg_product")]
    public sealed class T323SgProduct
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private void CreateFixture()
    {
        Execute("drop table if exists t323_sg_line");
        Execute("drop table if exists t323_sg_product");
        Execute("create table t323_sg_product (id int not null primary key, name text not null)");
        Execute("create table t323_sg_line (id int not null primary key, product_id int not null, "
                + "ordered_at timestamp not null, line_total numeric(10, 2) not null, order_qty int not null)");
        Execute("insert into t323_sg_product (id, name) values (1, 'widget'), (2, 'gadget')");
        Execute("insert into t323_sg_line (id, product_id, ordered_at, line_total, order_qty) values "
                + "(1, 1, timestamp '2023-01-10 00:00:00', 100.00, 1), "
                + "(2, 1, timestamp '2023-02-15 00:00:00', 50.00, 2), "
                + "(3, 2, timestamp '2023-01-20 00:00:00', 70.00, 3), "
                + "(4, 1, timestamp '2023-04-05 00:00:00', 200.00, 4), "
                + "(5, 2, timestamp '2023-05-06 00:00:00', 30.00, 5)");
    }

    private void DropFixture()
    {
        Execute("drop table if exists t323_sg_line");
        Execute("drop table if exists t323_sg_product");
    }

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}

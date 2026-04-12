using Microsoft.EntityFrameworkCore;
using EvolutionDb.EntityFrameworkCore.Extensions;

namespace EvolutionDb.EntityFrameworkCore.Tests;

/// <summary>
/// Integration tests against a running EvolutionDB instance (localhost:5433).
/// Requires: server running on port 5433 with admin/admin credentials.
/// </summary>
[Collection("Integration")]
public class IntegrationTests : IDisposable
{
    private const string ConnectionString = "Host=localhost;Port=9967;Username=admin;Password=admin;Database=testdb;Pooling=false;Timeout=10";

    private readonly ProductDbContext _db;

    public IntegrationTests()
    {
        _db = new ProductDbContext(ConnectionString);
        try { _db.Database.ExecuteSqlRaw("CREATE TABLE efcore_products (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100) NOT NULL, price DECIMAL(10,2) DEFAULT 0.00)"); } catch { }
        try { _db.Database.ExecuteSqlRaw("DELETE FROM efcore_products"); } catch { }
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Connection ──

    [Fact]
    public void CanConnect() => Assert.True(_db.Database.CanConnect());

    [Fact]
    public void ExecuteRawSql_Select1()
    {
        using var cmd = _db.Database.GetDbConnection().CreateCommand();
        _db.Database.OpenConnection();
        cmd.CommandText = "SELECT 1";
        var result = cmd.ExecuteScalar();
        Assert.Equal("1", result?.ToString());
    }

    // ── Raw SQL ──

    [Fact]
    public void RawSql_InsertAndCount()
    {
        _db.Database.ExecuteSqlRaw("INSERT INTO efcore_products (name, price) VALUES ('Widget', 9.99)");
        _db.Database.ExecuteSqlRaw("INSERT INTO efcore_products (name, price) VALUES ('Gadget', 24.50)");

        using var cmd = _db.Database.GetDbConnection().CreateCommand();
        _db.Database.OpenConnection();
        cmd.CommandText = "SELECT COUNT(*) FROM efcore_products";
        var count = cmd.ExecuteScalar();
        Assert.Equal("2", count?.ToString());
    }

    // ── LINQ SELECT ──

    [Fact]
    public void Select_Where_ReturnsFilteredResults()
    {
        InsertSampleProducts();

        var expensive = _db.Products.Where(p => p.Price > 20).ToList();

        Assert.Single(expensive);
        Assert.Equal("Gadget", expensive[0].Name);
    }

    [Fact]
    public void Select_OrderBy_Take()
    {
        InsertSampleProducts();

        var cheapest = _db.Products.OrderBy(p => p.Price).Take(1).ToList();

        Assert.Single(cheapest);
        Assert.Equal("Widget", cheapest[0].Name);
    }

    [Fact]
    public void Select_Count()
    {
        InsertSampleProducts();
        Assert.Equal(2, _db.Products.Count());
    }

    // ── Aggregates ──

    [Fact]
    public void Aggregate_Min_Max()
    {
        InsertSampleProducts();
        using var cmd = _db.Database.GetDbConnection().CreateCommand();
        _db.Database.OpenConnection();
        cmd.CommandText = "SELECT MIN(price), MAX(price) FROM efcore_products";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("9.99", reader.GetValue(0).ToString());
        Assert.Equal("24.5", reader.GetValue(1).ToString());
    }

    // ── String functions ──

    [Fact]
    public void StringFunctions_ToLower()
    {
        InsertSampleProducts();
        var lower = _db.Products.Select(p => p.Name.ToLower()).ToList();
        Assert.Contains("widget", lower);
        Assert.Contains("gadget", lower);
    }

    [Fact]
    public void StringFunctions_ToUpper()
    {
        InsertSampleProducts();

        var upper = _db.Products.Select(p => p.Name.ToUpper()).ToList();
        Assert.Contains("WIDGET", upper);
        Assert.Contains("GADGET", upper);
    }

    // ── NULL handling ──

    [Fact]
    public void NullHandling_SelectAll()
    {
        InsertSampleProducts();
        var all = _db.Products.ToList();
        Assert.Equal(2, all.Count);
    }

    // ── Helpers ──

    private void InsertSampleProducts()
    {
        _db.Database.ExecuteSqlRaw("INSERT INTO efcore_products (name, price) VALUES ('Widget', 9.99)");
        _db.Database.ExecuteSqlRaw("INSERT INTO efcore_products (name, price) VALUES ('Gadget', 24.50)");
        _db.ChangeTracker.Clear();
    }
}

// ── Test Entity ──

public class EfProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

// ── Test DbContext (single entity, no FK) ──

public class ProductDbContext : DbContext
{
    private readonly string _connectionString;

    public DbSet<EfProduct> Products => Set<EfProduct>();

    public ProductDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseEvolutionDb(_connectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfProduct>(entity =>
        {
            entity.ToTable("efcore_products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Price).HasColumnType("DECIMAL(10,2)");
        });
    }
}

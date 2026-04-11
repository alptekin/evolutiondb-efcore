# EvolutionDb.EntityFrameworkCore

Entity Framework Core provider for [EvolutionDB](https://github.com/alptekin/evolutiondb). Use EF Core with EvolutionDB — full LINQ support, migrations, scaffolding, and batch DML.

## Quick Start

### 1. Install

```bash
dotnet add package EvolutionDb.EntityFrameworkCore
```

### 2. Start EvolutionDB

```bash
docker run -d \
  --name evolutiondb \
  -p 5433:5433 \
  -e EVOSQL_PASSWORD=admin \
  -v evo-data:/data \
  evolutiondb/evolutiondb
```

### 3. Configure DbContext

```csharp
using EvolutionDb.EntityFrameworkCore.Extensions;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseEvolutionDb(
            "Host=localhost;Port=5433;Username=admin;Password=admin;Database=testdb");
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

### 4. Use It

```csharp
using var db = new AppDbContext();

db.Products.Add(new Product { Name = "Widget", Price = 9.99m });
db.SaveChanges();

var products = db.Products.Where(p => p.Price > 5).ToList();
```

## Connection String

```
Host=localhost;Port=5433;Username=admin;Password=admin;Database=testdb
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `Host` | `localhost` | Server hostname |
| `Port` | `5433` | PostgreSQL wire protocol port (auto-set if omitted) |
| `Username` | — | Database user |
| `Password` | — | User password |
| `Database` | — | Target database name |

The provider automatically sets `Port=5433` and `ServerCompatibilityMode=NoTypeLoading` if not specified.

## Entity Configuration

### Auto-Increment Primary Key

```csharp
modelBuilder.Entity<Product>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).ValueGeneratedOnAdd(); // AUTO_INCREMENT
});
```

### UUID Primary Key (v7 — time-ordered)

```csharp
public class Order
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
}

modelBuilder.Entity<Order>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid_v7()");
});
```

### Snowflake ID Primary Key

```csharp
public class Event
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

modelBuilder.Entity<Event>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasDefaultValueSql("snowflake_id()");
});
```

### Foreign Keys

```csharp
public class Order
{
    public Guid Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public Product Product { get; set; } = null!;
}

modelBuilder.Entity<Order>(entity =>
{
    entity.HasOne(e => e.Product)
          .WithMany()
          .HasForeignKey(e => e.ProductId)
          .OnDelete(DeleteBehavior.Cascade);
});
```

### Column Types

```csharp
entity.Property(e => e.Name).HasMaxLength(100);           // VARCHAR(100)
entity.Property(e => e.Price).HasColumnType("DECIMAL(10,2)");
entity.Property(e => e.Bio).HasColumnType("TEXT");
entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
entity.Property(e => e.IsActive).HasDefaultValue(true);
```

## Supported Type Mappings

| C# Type | EvolutionDB Type |
|---------|-----------------|
| `bool` | `BOOLEAN` |
| `short` | `SMALLINT` |
| `int` | `INT` |
| `long` | `BIGINT` |
| `float` | `FLOAT` |
| `double` | `DOUBLE` |
| `decimal` | `DECIMAL` |
| `string` | `TEXT` (or `VARCHAR(n)` with MaxLength) |
| `DateTime` | `TIMESTAMP` |
| `DateOnly` | `DATE` |
| `TimeOnly` | `TIME` |
| `Guid` | `UUID` |

## LINQ Query Support

### Filtering and Sorting

```csharp
var results = db.Products
    .Where(p => p.Price > 10 && p.Name.Contains("Widget"))
    .OrderByDescending(p => p.Price)
    .Skip(10)
    .Take(20)
    .ToList();
```

### Joins

```csharp
var query = from o in db.Orders
            join p in db.Products on o.ProductId equals p.Id
            where p.Price > 5
            select new { o.Quantity, p.Name, p.Price };
```

### Aggregates

```csharp
var stats = db.Products
    .GroupBy(p => p.Category)
    .Select(g => new
    {
        Category = g.Key,
        Count = g.Count(),
        AvgPrice = g.Average(p => p.Price),
        MaxPrice = g.Max(p => p.Price),
        Total = g.Sum(p => p.Price)
    })
    .ToList();
```

### String Functions

```csharp
db.Products.Where(p => p.Name.ToUpper() == "WIDGET");
db.Products.Where(p => p.Name.StartsWith("Wid"));
db.Products.Where(p => p.Name.Contains("get"));
db.Products.Select(p => p.Name.Substring(0, 3));
db.Products.Select(p => p.Name.Replace("old", "new"));
db.Products.Select(p => p.Name.Trim());
db.Products.Select(p => p.Name.PadLeft(20));
db.Products.Where(p => !string.IsNullOrEmpty(p.Name));
db.Products.Select(p => string.Concat(p.Name, " - ", p.Category));
db.Products.Select(p => p.Name.IndexOf("get"));
db.Products.OrderBy(p => p.Name.Length);
```

### Math Functions

```csharp
db.Products.Select(p => Math.Abs(p.Profit));
db.Products.Select(p => Math.Round(p.Price, 2));
db.Products.Select(p => Math.Ceiling(p.Price));
db.Products.Select(p => Math.Floor(p.Price));
db.Products.Select(p => Math.Sqrt(p.Value));
db.Products.Select(p => Math.Pow(p.Value, 2));
db.Products.Select(p => Math.Max(p.Price, p.MinPrice));
db.Products.Select(p => Math.Min(p.Price, p.MaxPrice));
```

### DateTime Functions

```csharp
db.Orders.Where(o => o.CreatedAt > DateTime.Today);
db.Orders.Where(o => o.CreatedAt.Year == 2026);
db.Orders.Select(o => o.CreatedAt.Month);
db.Orders.Select(o => o.CreatedAt.Day);
db.Orders.Select(o => o.CreatedAt.Hour);
db.Orders.Where(o => o.CreatedAt.DayOfWeek == DayOfWeek.Monday);
```

### Type Conversions

```csharp
db.Products.Select(p => Convert.ToString(p.Id));
db.Products.Select(p => Convert.ToInt32(p.PriceText));
db.Products.Select(p => p.Id.ToString());
```

### Subqueries

```csharp
var expensive = db.Products
    .Where(p => p.Price > db.Products.Average(x => x.Price))
    .ToList();
```

### Set Operations

```csharp
var combined = db.Products.Where(p => p.Price > 100)
    .Union(db.Products.Where(p => p.Featured))
    .ToList();

var common = db.Products.Where(p => p.Active)
    .Intersect(db.Products.Where(p => p.InStock))
    .ToList();
```

### EvolutionDB Functions

```csharp
using EvolutionDb.EntityFrameworkCore;

// Use in default values
entity.Property(e => e.Id).HasDefaultValueSql("snowflake_id()");
entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid_v7()");

// Available static functions (for LINQ queries):
// EvolutionDbFunctions.SnowflakeId()    → snowflake_id()
// EvolutionDbFunctions.NewUuidV7()      → gen_random_uuid_v7()
// EvolutionDbFunctions.NewUuid()        → gen_random_uuid()
```

## CRUD Operations

### Insert

```csharp
db.Products.Add(new Product { Name = "Widget", Price = 9.99m });
db.SaveChanges(); // INSERT ... RETURNING "Id"
// product.Id is auto-populated
```

### Bulk Insert

```csharp
var products = Enumerable.Range(1, 1000)
    .Select(i => new Product { Name = $"Product {i}", Price = i * 1.5m })
    .ToList();

db.Products.AddRange(products);
db.SaveChanges(); // Batched — up to 100 statements per roundtrip
```

### Update

```csharp
var product = db.Products.First(p => p.Id == 1);
product.Price = 19.99m;
db.SaveChanges(); // UPDATE ... SET ... WHERE ... RETURNING ...
```

### Delete

```csharp
var product = db.Products.First(p => p.Id == 1);
db.Products.Remove(product);
db.SaveChanges(); // DELETE FROM ... WHERE ...
```

## Migrations

```bash
# Add a migration
dotnet ef migrations add InitialCreate

# Apply to database
dotnet ef database update
```

Supported DDL operations:

| Operation | SQL |
|-----------|-----|
| Create table | `CREATE TABLE ... (columns, constraints)` |
| Drop table | `DROP TABLE IF EXISTS ...` |
| Add column | `ALTER TABLE ... ADD COLUMN ...` |
| Drop column | `ALTER TABLE ... DROP COLUMN ...` |
| Rename column | `ALTER TABLE ... RENAME COLUMN ... TO ...` |
| Create index | `CREATE [UNIQUE] INDEX ... ON ... (columns)` |
| Drop index | `DROP INDEX ...` |
| Add foreign key | `ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY ...` |
| Add check | `ALTER TABLE ... ADD CONSTRAINT ... CHECK (...)` |
| Create sequence | `CREATE SEQUENCE ...` |
| Drop sequence | `DROP SEQUENCE ...` |

## Scaffolding (Database-First)

```bash
dotnet ef dbcontext scaffold \
  "Host=localhost;Port=5433;Username=admin;Password=admin;Database=testdb" \
  EvolutionDb.EntityFrameworkCore
```

## Batch DML

`SaveChanges()` automatically batches up to 100 DML commands per roundtrip. Configure via `MaxBatchSize`:

```csharp
options.UseEvolutionDb("...", opts =>
{
    opts.Options.UseEvolutionDb("...");
});

// Or via RelationalOptionsExtension
services.AddDbContext<AppDbContext>(options =>
    options.UseEvolutionDb(connectionString));
```

Default batch size: 100. Each batch sends multiple `INSERT`/`UPDATE`/`DELETE` statements in a single roundtrip using EvolutionDB's Simple Query protocol.

## Transactions

```csharp
using var transaction = db.Database.BeginTransaction();

try
{
    db.Products.Add(new Product { Name = "A", Price = 10 });
    db.SaveChanges();

    db.Products.Add(new Product { Name = "B", Price = 20 });
    db.SaveChanges();

    transaction.Commit();
}
catch
{
    transaction.Rollback();
}
```

## Architecture

```
Your .NET App
    │
    ├── DbContext (EF Core)
    │     └── UseEvolutionDb()
    │
    ├── EvolutionDb.EntityFrameworkCore (this provider)
    │     ├── Query translation (LINQ → SQL)
    │     ├── Type mapping (C# ↔ EvolutionDB)
    │     ├── DML with RETURNING
    │     ├── Migration SQL generation
    │     └── Batch command support
    │
    ├── Npgsql (ADO.NET driver)
    │     └── PostgreSQL wire protocol
    │
    └── EvolutionDB Server (port 5433)
```

## Requirements

- .NET 8.0+
- EvolutionDB server (Docker or local build)

## License

MIT

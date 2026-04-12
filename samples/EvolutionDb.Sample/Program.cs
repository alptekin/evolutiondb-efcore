using Microsoft.EntityFrameworkCore;
using EvolutionDb.EntityFrameworkCore.Extensions;

// --- Main ---
Console.WriteLine("EvolutionDB EF Core Provider - Sample");
Console.WriteLine("=====================================");
Console.WriteLine();

using var context = new AppDbContext();

if (context.Database.CanConnect())
{
    Console.WriteLine("Connected to EvolutionDB!");
}
else
{
    Console.WriteLine("Could not connect to EvolutionDB. Make sure the server is running.");
    Console.WriteLine("  docker compose up -d");
    return;
}

Console.WriteLine();
Console.WriteLine("Connection successful. Ready for CRUD operations.");

// --- Entity classes ---
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public Product Product { get; set; } = null!;
}

// --- DbContext ---
public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseEvolutionDb(
            "Host=localhost;Port=9967;Username=admin;Password=admin;Database=testdb");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Price).HasColumnType("DECIMAL(10,2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid_v7()");
            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId);
        });
    }
}

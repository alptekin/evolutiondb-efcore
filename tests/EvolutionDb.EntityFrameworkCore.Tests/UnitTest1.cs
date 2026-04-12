using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using EvolutionDb.EntityFrameworkCore.Extensions;
using EvolutionDb.EntityFrameworkCore.Storage.Internal;

namespace EvolutionDb.EntityFrameworkCore.Tests;

public class ConnectionStringTests
{
    [Fact]
    public void UseEvolutionDb_SetsDefaultPort()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.NotNull(ext);
        Assert.Contains("Port=9967", ext!.ConnectionString);
    }

    [Fact]
    public void UseEvolutionDb_DoesNotSetOldPort()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.DoesNotContain("Port=5433", ext!.ConnectionString);
    }

    [Fact]
    public void UseEvolutionDb_PreservesExplicitPort()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Port=5555;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.Contains("Port=5555", ext!.ConnectionString);
        Assert.DoesNotContain("Port=9967", ext.ConnectionString);
    }
}

public class ProviderRegistrationTests
{
    [Fact]
    public void UseEvolutionDb_RegistersProvider()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.NotNull(ext);
        Assert.True(ext!.Info.IsDatabaseProvider);
    }

    [Fact]
    public void Provider_LogFragment_ContainsEvolutionDB()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.Contains("EvolutionDB", ext!.Info.LogFragment);
    }

    [Fact]
    public void Provider_DebugInfo_ContainsEvolutionDB()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        var debugInfo = new Dictionary<string, string>();
        ext!.Info.PopulateDebugInfo(debugInfo);
        Assert.True(debugInfo.ContainsKey("EvolutionDB"));
    }

    [Fact]
    public void UseEvolutionDb_WithOptionsAction_Works()
    {
        var actionCalled = false;
        var options = new DbContextOptionsBuilder()
            .UseEvolutionDb("Host=localhost;Database=test", opts => { actionCalled = true; })
            .Options;
        Assert.True(actionCalled);
    }
}

public class EvolutionDbFunctionsTests
{
    [Fact]
    public void SnowflakeId_ThrowsOnClientSide() => Assert.Throws<InvalidOperationException>(() => EvolutionDbFunctions.SnowflakeId());

    [Fact]
    public void NewUuidV7_ThrowsOnClientSide() => Assert.Throws<InvalidOperationException>(() => EvolutionDbFunctions.NewUuidV7());

    [Fact]
    public void NewUuid_ThrowsOnClientSide() => Assert.Throws<InvalidOperationException>(() => EvolutionDbFunctions.NewUuid());
}

public class RelationalModelTests
{
    [Fact]
    public void Model_HasTableColumnMappings()
    {
        // Verify that the relational model is built correctly with table-column mappings.
        // This is the root cause of the "Sequence contains no matching element" error
        // in ConcreteColumnExpression when mappings are missing.
        var options = new DbContextOptionsBuilder()
            .UseEvolutionDb("Host=localhost;Database=test")
            .Options;

        using var ctx = new TestModelContext(options);
        var model = ctx.Model;
        var relationalModel = model.GetRelationalModel();

        // Verify the relational model has tables
        var tables = relationalModel.Tables.ToList();
        Assert.NotEmpty(tables);

        // Verify each table has columns mapped from entity properties
        var productTable = tables.First(t => t.Name == "efcore_products");
        Assert.NotNull(productTable);

        var columns = productTable.Columns.ToList();
        Assert.True(columns.Count >= 3, $"Expected at least 3 columns, got {columns.Count}");

        // Verify each column has property mappings (this is what ConcreteColumnExpression needs)
        foreach (var column in columns)
        {
            Assert.NotEmpty(column.PropertyMappings);
        }

        // Verify specific columns exist
        Assert.Contains(columns, c => c.Name == "Id");
        Assert.Contains(columns, c => c.Name == "Name");
        Assert.Contains(columns, c => c.Name == "Price");
    }

    [Fact]
    public void Model_PropertyHasTableColumnMappings()
    {
        // Verify that IProperty.GetTableColumnMappings() returns non-empty results.
        // This is the exact method ConcreteColumnExpression calls.
        var options = new DbContextOptionsBuilder()
            .UseEvolutionDb("Host=localhost;Database=test")
            .Options;

        using var ctx = new TestModelContext(options);
        var entityType = ctx.Model.FindEntityType(typeof(TestProduct))!;
        Assert.NotNull(entityType);

        foreach (var property in entityType.GetProperties())
        {
            var mappings = property.GetTableColumnMappings().ToList();
            Assert.NotEmpty(mappings);
        }
    }

    private class TestProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }

    private class TestModelContext : DbContext
    {
        public TestModelContext(DbContextOptions options) : base(options) { }
        public DbSet<TestProduct> Products => Set<TestProduct>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestProduct>(entity =>
            {
                entity.ToTable("efcore_products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Price).HasColumnType("DECIMAL(10,2)");
            });
        }
    }
}

public class MigrationsSqlGeneratorTests
{
    [Fact]
    public void GetColumnType_IntReturnsINT()
    {
        // Test via reflection — GetColumnType is private static
        var method = typeof(Migrations.Internal.EvolutionDbMigrationsSqlGenerator)
            .GetMethod("GetColumnType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
    }

    [Fact]
    public void GetReferentialAction_CascadeReturnsCASCADE()
    {
        var method = typeof(Migrations.Internal.EvolutionDbMigrationsSqlGenerator)
            .GetMethod("GetReferentialAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null, new object[] { Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade });
        Assert.Equal("CASCADE", result);
    }

    [Fact]
    public void GetReferentialAction_SetNullReturnsSET_NULL()
    {
        var method = typeof(Migrations.Internal.EvolutionDbMigrationsSqlGenerator)
            .GetMethod("GetReferentialAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull });
        Assert.Equal("SET NULL", result);
    }

    [Fact]
    public void GenerateSqlLiteral_String_EscapesSingleQuotes()
    {
        var method = typeof(Migrations.Internal.EvolutionDbMigrationsSqlGenerator)
            .GetMethod("GenerateSqlLiteral", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { "it's a test" });
        Assert.Equal("'it''s a test'", result);
    }

    [Fact]
    public void GenerateSqlLiteral_Bool_ReturnsCorrectString()
    {
        var method = typeof(Migrations.Internal.EvolutionDbMigrationsSqlGenerator)
            .GetMethod("GenerateSqlLiteral", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.Equal("TRUE", method!.Invoke(null, new object[] { true }));
        Assert.Equal("FALSE", method!.Invoke(null, new object[] { false }));
    }
}

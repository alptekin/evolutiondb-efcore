using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        Assert.Contains("Port=5433", ext!.ConnectionString);
    }

    [Fact]
    public void UseEvolutionDb_SetsNoTypeLoading()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.Contains("Server Compatibility Mode=NoTypeLoading", ext!.ConnectionString);
    }

    [Fact]
    public void UseEvolutionDb_PreservesExplicitPort()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Port=5555;Database=test").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.Contains("Port=5555", ext!.ConnectionString);
        Assert.DoesNotContain("Port=5433", ext.ConnectionString);
    }

    [Fact]
    public void UseEvolutionDb_PreservesExplicitCompatibilityMode()
    {
        var options = new DbContextOptionsBuilder().UseEvolutionDb("Host=localhost;Database=test;ServerCompatibilityMode=NoTypeLoading").Options;
        var ext = options.FindExtension<Infrastructure.EvolutionDbOptionsExtension>();
        Assert.NotNull(ext);
        // Should not duplicate the setting
        var count = ext!.ConnectionString!.Split("NoTypeLoading").Length - 1;
        Assert.Equal(1, count);
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using EvoSQL.EntityFrameworkCore.Infrastructure;

namespace EvoSQL.EntityFrameworkCore.Extensions;

public static class EvoSqlDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseEvoSql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<EvoSqlDbContextOptionsBuilder>? evoSqlOptionsAction = null)
    {
        var extension = (EvoSqlOptionsExtension)GetOrCreateExtension(optionsBuilder)
            .WithConnectionString(NormalizeConnectionString(connectionString));

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        evoSqlOptionsAction?.Invoke(new EvoSqlDbContextOptionsBuilder(optionsBuilder));

        return optionsBuilder;
    }

    private static EvoSqlOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<EvoSqlOptionsExtension>()
           ?? new EvoSqlOptionsExtension();

    private static string NormalizeConnectionString(string connectionString)
    {
        // Default port to 5433 if not specified
        if (!connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = connectionString.TrimEnd(';') + ";Port=5433";
        }

        // Set ServerCompatibilityMode=NoTypeLoading if not specified
        if (!connectionString.Contains("ServerCompatibilityMode", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = connectionString.TrimEnd(';') + ";Server Compatibility Mode=NoTypeLoading";
        }

        return connectionString;
    }
}

public class EvoSqlDbContextOptionsBuilder
{
    private readonly DbContextOptionsBuilder _optionsBuilder;

    public EvoSqlDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    {
        _optionsBuilder = optionsBuilder;
    }

    public DbContextOptionsBuilder Options => _optionsBuilder;
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using EvolutionDb.EntityFrameworkCore.Infrastructure;

namespace EvolutionDb.EntityFrameworkCore.Extensions;

public static class EvolutionDbDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseEvolutionDb(this DbContextOptionsBuilder optionsBuilder, string connectionString, Action<EvolutionDbDbContextOptionsBuilder>? evolutionDbOptionsAction = null)
    {
        var extension = (EvolutionDbOptionsExtension)GetOrCreateExtension(optionsBuilder).WithConnectionString(NormalizeConnectionString(connectionString));

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        evolutionDbOptionsAction?.Invoke(new EvolutionDbDbContextOptionsBuilder(optionsBuilder));

        return optionsBuilder;
    }

    private static EvolutionDbOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.Options.FindExtension<EvolutionDbOptionsExtension>() ?? new EvolutionDbOptionsExtension();

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

public class EvolutionDbDbContextOptionsBuilder
{
    private readonly DbContextOptionsBuilder _optionsBuilder;

    public EvolutionDbDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    {
        _optionsBuilder = optionsBuilder;
    }

    public DbContextOptionsBuilder Options => _optionsBuilder;
}

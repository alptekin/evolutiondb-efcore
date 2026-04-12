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
        if (!connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase))
            connectionString = connectionString.TrimEnd(';') + ";Port=9967";

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

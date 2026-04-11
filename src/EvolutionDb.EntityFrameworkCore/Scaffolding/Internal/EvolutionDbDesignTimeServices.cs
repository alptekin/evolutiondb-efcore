using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace EvolutionDb.EntityFrameworkCore.Scaffolding.Internal;

public class EvolutionDbDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection) => serviceCollection.AddSingleton<IDatabaseModelFactory, EvolutionDbDatabaseModelFactory>();
}

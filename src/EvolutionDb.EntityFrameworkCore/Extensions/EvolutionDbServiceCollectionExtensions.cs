using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using EvolutionDb.EntityFrameworkCore.Infrastructure;
using EvolutionDb.EntityFrameworkCore.Migrations.Internal;
using EvolutionDb.EntityFrameworkCore.Query.Internal;
using EvolutionDb.EntityFrameworkCore.Storage.Internal;

namespace EvolutionDb.EntityFrameworkCore.Extensions;

public static class EvolutionDbServiceCollectionExtensions
{
    public static IServiceCollection AddEntityFrameworkEvolutionDb(this IServiceCollection serviceCollection)
    {
        var builder = new EntityFrameworkRelationalServicesBuilder(serviceCollection)
            .TryAdd<IDatabaseProvider, DatabaseProvider<EvolutionDbOptionsExtension>>()
            .TryAdd<ISqlGenerationHelper, EvolutionDbSqlGenerationHelper>()
            .TryAdd<IRelationalTypeMappingSource, EvolutionDbTypeMappingSource>()
            .TryAdd<IRelationalConnection, EvolutionDbRelationalConnection>()
            .TryAdd<IQuerySqlGeneratorFactory, EvolutionDbQuerySqlGeneratorFactory>()
            .TryAdd<IMethodCallTranslatorProvider, EvolutionDbMethodCallTranslatorProvider>()
            .TryAdd<IMemberTranslatorProvider, EvolutionDbMemberTranslatorProvider>()
            .TryAdd<IMigrationsSqlGenerator, EvolutionDbMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, EvolutionDbHistoryRepository>()
            .TryAdd<IRelationalDatabaseCreator, EvolutionDbDatabaseCreator>()
            .TryAdd<IUpdateSqlGenerator, EvolutionDbUpdateSqlGenerator>()
            .TryAdd<IModificationCommandBatchFactory, EvolutionDbModificationCommandBatchFactory>();

        builder.TryAddCoreServices();

        return serviceCollection;
    }
}

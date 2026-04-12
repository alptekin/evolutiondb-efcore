using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using EvolutionDb.EntityFrameworkCore.Infrastructure;
using EvolutionDb.EntityFrameworkCore.Metadata.Conventions;
using EvolutionDb.EntityFrameworkCore.Metadata.Internal;
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
            .TryAdd<LoggingDefinitions, EvolutionDbLoggingDefinitions>()
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
            .TryAdd<IModificationCommandBatchFactory, EvolutionDbModificationCommandBatchFactory>()
            .TryAdd<IRelationalAnnotationProvider, EvolutionDbAnnotationProvider>()
            .TryAdd<IModelValidator, EvolutionDbModelValidator>()
            .TryAdd<IProviderConventionSetBuilder, EvolutionDbConventionSetBuilder>();

        builder.TryAddCoreServices();

        return serviceCollection;
    }
}

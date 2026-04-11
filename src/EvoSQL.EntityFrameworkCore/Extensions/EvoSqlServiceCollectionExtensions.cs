using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using EvoSQL.EntityFrameworkCore.Infrastructure;
using EvoSQL.EntityFrameworkCore.Migrations.Internal;
using EvoSQL.EntityFrameworkCore.Query.Internal;
using EvoSQL.EntityFrameworkCore.Storage.Internal;

namespace EvoSQL.EntityFrameworkCore.Extensions;

public static class EvoSqlServiceCollectionExtensions
{
    public static IServiceCollection AddEntityFrameworkEvoSql(this IServiceCollection serviceCollection)
    {
        var builder = new EntityFrameworkRelationalServicesBuilder(serviceCollection)
            .TryAdd<IDatabaseProvider, DatabaseProvider<EvoSqlOptionsExtension>>()
            .TryAdd<ISqlGenerationHelper, EvoSqlSqlGenerationHelper>()
            .TryAdd<IRelationalTypeMappingSource, EvoSqlTypeMappingSource>()
            .TryAdd<IRelationalConnection, EvoSqlRelationalConnection>()
            .TryAdd<IQuerySqlGeneratorFactory, EvoSqlQuerySqlGeneratorFactory>()
            .TryAdd<IMigrationsSqlGenerator, EvoSqlMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, EvoSqlHistoryRepository>()
            .TryAdd<IRelationalDatabaseCreator, EvoSqlDatabaseCreator>()
            .TryAdd<IUpdateSqlGenerator, EvoSqlUpdateSqlGenerator>()
            .TryAdd<IModificationCommandBatchFactory, EvoSqlModificationCommandBatchFactory>();

        builder.TryAddCoreServices();

        return serviceCollection;
    }
}

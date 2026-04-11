using Microsoft.EntityFrameworkCore.Query;

namespace EvoSQL.EntityFrameworkCore.Query.Internal;

public class EvoSqlQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
{
    private readonly QuerySqlGeneratorDependencies _dependencies;

    public EvoSqlQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    public QuerySqlGenerator Create()
        => new EvoSqlQuerySqlGenerator(_dependencies);
}

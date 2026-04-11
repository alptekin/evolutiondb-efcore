using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EvoSQL.EntityFrameworkCore.Query.Internal;

public class EvoSqlQuerySqlGenerator : QuerySqlGenerator
{
    public EvoSqlQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        // EvoSQL supports LIMIT/OFFSET natively (PostgreSQL style)
        return base.VisitSelect(selectExpression);
    }

    protected override void GenerateLimitOffset(SelectExpression selectExpression)
    {
        if (selectExpression.Limit != null)
        {
            Sql.AppendLine().Append("LIMIT ");
            Visit(selectExpression.Limit);
        }

        if (selectExpression.Offset != null)
        {
            if (selectExpression.Limit == null)
                Sql.AppendLine();
            else
                Sql.Append(" ");

            Sql.Append("OFFSET ");
            Visit(selectExpression.Offset);
        }
    }
}

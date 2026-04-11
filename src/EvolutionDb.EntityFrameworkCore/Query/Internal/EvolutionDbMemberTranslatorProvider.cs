using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace EvolutionDb.EntityFrameworkCore.Query.Internal;

public class EvolutionDbMemberTranslatorProvider : RelationalMemberTranslatorProvider
{
    public EvolutionDbMemberTranslatorProvider(RelationalMemberTranslatorProviderDependencies dependencies)
        : base(dependencies)
    {
        var sqlExpressionFactory = dependencies.SqlExpressionFactory;

        AddTranslators(new IMemberTranslator[]
        {
            new EvolutionDbDateTimeMemberTranslator(sqlExpressionFactory),
            new EvolutionDbStringMemberTranslator(sqlExpressionFactory),
        });
    }
}

internal class EvolutionDbDateTimeMemberTranslator : IMemberTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbDateTimeMemberTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (member.DeclaringType != typeof(DateTime)) return null;

        return member.Name switch
        {
            nameof(DateTime.Now) => _sql.Function("NOW", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), returnType),
            nameof(DateTime.UtcNow) => _sql.Function("NOW", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), returnType),
            nameof(DateTime.Today) => _sql.Function("CURRENT_DATE", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), returnType),
            _ => null
        };
    }
}

internal class EvolutionDbStringMemberTranslator : IMemberTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbStringMemberTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (member.DeclaringType == typeof(string) && member.Name == nameof(string.Length))
            return _sql.Function("LENGTH", new[] { instance! }, nullable: true, argumentsPropagateNullability: new[] { true }, returnType);

        return null;
    }
}

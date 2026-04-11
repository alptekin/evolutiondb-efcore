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
        var sql = dependencies.SqlExpressionFactory;

        AddTranslators(new IMemberTranslator[]
        {
            new EvolutionDbDateTimeMemberTranslator(sql),
            new EvolutionDbStringMemberTranslator(sql),
        });
    }
}

/// <summary>
/// DateTime.Now → NOW(), DateTime.UtcNow → NOW(), DateTime.Today → CURRENT_DATE()
/// DateTime.Year → EXTRACT(YEAR FROM x), Month, Day, Hour, Minute, Second
/// DateTime.Date → CAST(x AS DATE)
/// DateTime.DayOfWeek → EXTRACT(DOW FROM x)
/// DateTime.DayOfYear → EXTRACT(DOY FROM x)
/// </summary>
internal class EvolutionDbDateTimeMemberTranslator : IMemberTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbDateTimeMemberTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (member.DeclaringType != typeof(DateTime) && member.DeclaringType != typeof(DateTimeOffset)) return null;

        // Static members (no instance)
        if (instance == null)
        {
            return member.Name switch
            {
                nameof(DateTime.Now) => _sql.Function("NOW", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), returnType),
                nameof(DateTime.UtcNow) => _sql.Function("NOW", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), returnType),
                nameof(DateTime.Today) => _sql.Function("CURRENT_DATE", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), returnType),
                _ => null
            };
        }

        // Instance members — EXTRACT(field FROM instance)
        var extractField = member.Name switch
        {
            nameof(DateTime.Year) => "YEAR",
            nameof(DateTime.Month) => "MONTH",
            nameof(DateTime.Day) => "DAY",
            nameof(DateTime.Hour) => "HOUR",
            nameof(DateTime.Minute) => "MINUTE",
            nameof(DateTime.Second) => "SECOND",
            nameof(DateTime.DayOfWeek) => "DOW",
            nameof(DateTime.DayOfYear) => "DOY",
            _ => null
        };

        if (extractField != null)
            return _sql.Function("EXTRACT", new[] { _sql.Fragment($"{extractField} FROM {instance}") }, nullable: true, argumentsPropagateNullability: new[] { true }, returnType);

        // DateTime.Date → CAST(x AS DATE)
        if (member.Name == nameof(DateTime.Date))
            return _sql.Function("CAST", new[] { _sql.Fragment($"{instance} AS DATE") }, nullable: true, argumentsPropagateNullability: new[] { true }, returnType);

        return null;
    }
}

/// <summary>
/// string.Length → LENGTH(x)
/// </summary>
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

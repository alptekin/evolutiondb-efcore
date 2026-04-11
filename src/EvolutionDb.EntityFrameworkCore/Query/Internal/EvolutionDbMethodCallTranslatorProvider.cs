using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EvolutionDb.EntityFrameworkCore.Query.Internal;

public class EvolutionDbMethodCallTranslatorProvider : RelationalMethodCallTranslatorProvider
{
    public EvolutionDbMethodCallTranslatorProvider(RelationalMethodCallTranslatorProviderDependencies dependencies)
        : base(dependencies)
    {
        var sqlExpressionFactory = dependencies.SqlExpressionFactory;
        var typeMappingSource = dependencies.RelationalTypeMappingSource;

        AddTranslators(new IMethodCallTranslator[]
        {
            new EvolutionDbStringMethodTranslator(sqlExpressionFactory),
            new EvolutionDbMathMethodTranslator(sqlExpressionFactory),
            new EvolutionDbGuidMethodTranslator(sqlExpressionFactory),
            new EvolutionDbFunctionsTranslator(sqlExpressionFactory),
        });
    }
}

internal class EvolutionDbStringMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbStringMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(string)) return null;

        return method.Name switch
        {
            nameof(string.ToUpper) => _sql.Function("UPPER", new[] { instance! }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.ToLower) => _sql.Function("LOWER", new[] { instance! }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.Trim) when arguments.Count == 0 => _sql.Function("TRIM", new[] { instance! }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.Substring) when arguments.Count == 1 => _sql.Function("SUBSTRING", new[] { instance!, _sql.Add(arguments[0], _sql.Constant(1)), _sql.Function("LENGTH", new[] { instance! }, nullable: true, argumentsPropagateNullability: new[] { true }, typeof(int)) }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            nameof(string.Substring) when arguments.Count == 2 => _sql.Function("SUBSTRING", new[] { instance!, _sql.Add(arguments[0], _sql.Constant(1)), arguments[1] }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            nameof(string.Replace) => _sql.Function("REPLACE", new[] { instance!, arguments[0], arguments[1] }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            nameof(string.Contains) when arguments.Count == 1 => _sql.Like(instance!, _sql.Add(_sql.Add(_sql.Constant("%"), arguments[0]), _sql.Constant("%"))),
            nameof(string.StartsWith) when arguments.Count == 1 => _sql.Like(instance!, _sql.Add(arguments[0], _sql.Constant("%"))),
            nameof(string.EndsWith) when arguments.Count == 1 => _sql.Like(instance!, _sql.Add(_sql.Constant("%"), arguments[0])),
            _ => null
        };
    }
}

internal class EvolutionDbMathMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbMathMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(Math)) return null;

        var funcName = method.Name switch
        {
            nameof(Math.Abs) => "ABS",
            nameof(Math.Ceiling) => "CEIL",
            nameof(Math.Floor) => "FLOOR",
            nameof(Math.Round) when arguments.Count == 1 => "ROUND",
            nameof(Math.Pow) => "POWER",
            nameof(Math.Sqrt) => "SQRT",
            nameof(Math.Log) when arguments.Count == 1 => "LOG",
            _ => null
        };

        if (funcName == null) return null;

        return _sql.Function(funcName, arguments, nullable: true, argumentsPropagateNullability: arguments.Select(_ => true).ToArray(), method.ReturnType);
    }
}

internal class EvolutionDbGuidMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbGuidMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // Guid.NewGuid() → gen_random_uuid_v7()
        if (method.DeclaringType == typeof(Guid) && method.Name == nameof(Guid.NewGuid))
            return _sql.Function("gen_random_uuid_v7", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), typeof(Guid));

        return null;
    }
}

internal class EvolutionDbFunctionsTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbFunctionsTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(EvolutionDbFunctions)) return null;

        return method.Name switch
        {
            nameof(EvolutionDbFunctions.SnowflakeId) => _sql.Function("snowflake_id", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), typeof(long)),
            nameof(EvolutionDbFunctions.NewUuidV7) => _sql.Function("gen_random_uuid_v7", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), typeof(Guid)),
            nameof(EvolutionDbFunctions.NewUuid) => _sql.Function("gen_random_uuid", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), typeof(Guid)),
            _ => null
        };
    }
}

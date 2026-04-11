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
        var sql = dependencies.SqlExpressionFactory;

        AddTranslators(new IMethodCallTranslator[]
        {
            new EvolutionDbStringMethodTranslator(sql),
            new EvolutionDbMathMethodTranslator(sql),
            new EvolutionDbGuidMethodTranslator(sql),
            new EvolutionDbFunctionsTranslator(sql),
            new EvolutionDbObjectMethodTranslator(sql),
            new EvolutionDbConvertMethodTranslator(sql),
        });
    }
}

/// <summary>
/// string.ToUpper, ToLower, Trim, Substring, Replace, Contains, StartsWith, EndsWith,
/// IndexOf, IsNullOrEmpty, IsNullOrWhiteSpace, Concat, PadLeft, PadRight, TrimStart, TrimEnd
/// </summary>
internal class EvolutionDbStringMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbStringMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType == typeof(string))
        {
            // Static methods
            if (method.Name == nameof(string.Concat))
                return TranslateConcat(arguments);

            if (method.Name == nameof(string.IsNullOrEmpty) && arguments.Count == 1)
                return _sql.OrElse(_sql.IsNull(arguments[0]), _sql.Equal(arguments[0], _sql.Constant("")));

            if (method.Name == nameof(string.IsNullOrWhiteSpace) && arguments.Count == 1)
                return _sql.OrElse(_sql.IsNull(arguments[0]), _sql.Equal(_sql.Function("TRIM", new[] { arguments[0] }, nullable: true, argumentsPropagateNullability: new[] { true }, typeof(string)), _sql.Constant("")));
        }

        if (instance == null || method.DeclaringType != typeof(string)) return null;

        return method.Name switch
        {
            nameof(string.ToUpper) => _sql.Function("UPPER", new[] { instance }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.ToLower) => _sql.Function("LOWER", new[] { instance }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.Trim) when arguments.Count == 0 => _sql.Function("TRIM", new[] { instance }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.TrimStart) when arguments.Count == 0 => _sql.Function("LTRIM", new[] { instance }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.TrimEnd) when arguments.Count == 0 => _sql.Function("RTRIM", new[] { instance }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType),
            nameof(string.Substring) when arguments.Count == 1 => _sql.Function("SUBSTRING", new[] { instance, _sql.Add(arguments[0], _sql.Constant(1)), _sql.Function("LENGTH", new[] { instance }, nullable: true, argumentsPropagateNullability: new[] { true }, typeof(int)) }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            nameof(string.Substring) when arguments.Count == 2 => _sql.Function("SUBSTRING", new[] { instance, _sql.Add(arguments[0], _sql.Constant(1)), arguments[1] }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            nameof(string.Replace) => _sql.Function("REPLACE", new[] { instance, arguments[0], arguments[1] }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            nameof(string.Contains) when arguments.Count == 1 => _sql.Like(instance, _sql.Add(_sql.Add(_sql.Constant("%"), arguments[0]), _sql.Constant("%"))),
            nameof(string.StartsWith) when arguments.Count == 1 => _sql.Like(instance, _sql.Add(arguments[0], _sql.Constant("%"))),
            nameof(string.EndsWith) when arguments.Count == 1 => _sql.Like(instance, _sql.Add(_sql.Constant("%"), arguments[0])),
            nameof(string.IndexOf) when arguments.Count == 1 => _sql.Subtract(_sql.Function("INSTR", new[] { instance, arguments[0] }, nullable: true, argumentsPropagateNullability: new[] { true, true }, typeof(int)), _sql.Constant(1)),
            nameof(string.PadLeft) when arguments.Count == 1 => _sql.Function("LPAD", new[] { instance, arguments[0] }, nullable: true, argumentsPropagateNullability: new[] { true, true }, method.ReturnType),
            nameof(string.PadLeft) when arguments.Count == 2 => _sql.Function("LPAD", new[] { instance, arguments[0], arguments[1] }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            nameof(string.PadRight) when arguments.Count == 1 => _sql.Function("RPAD", new[] { instance, arguments[0] }, nullable: true, argumentsPropagateNullability: new[] { true, true }, method.ReturnType),
            nameof(string.PadRight) when arguments.Count == 2 => _sql.Function("RPAD", new[] { instance, arguments[0], arguments[1] }, nullable: true, argumentsPropagateNullability: new[] { true, true, true }, method.ReturnType),
            _ => null
        };
    }

    /// <summary>
    /// string.Concat(a, b) → CONCAT(a, b)
    /// string.Concat(a, b, c) → CONCAT(CONCAT(a, b), c)  (EvolutionDB CONCAT is 2-arg)
    /// </summary>
    private SqlExpression? TranslateConcat(IReadOnlyList<SqlExpression> arguments)
    {
        if (arguments.Count < 2) return null;

        var result = _sql.Function("CONCAT", new[] { arguments[0], arguments[1] }, nullable: true, argumentsPropagateNullability: new[] { true, true }, typeof(string));

        for (var i = 2; i < arguments.Count; i++)
            result = _sql.Function("CONCAT", new SqlExpression[] { result, arguments[i] }, nullable: true, argumentsPropagateNullability: new[] { true, true }, typeof(string));

        return result;
    }
}

/// <summary>
/// Math.Abs, Ceiling, Floor, Round, Pow, Sqrt, Log, Max, Min, Truncate, Sign
/// </summary>
internal class EvolutionDbMathMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbMathMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(Math) && method.DeclaringType != typeof(MathF)) return null;

        var funcName = method.Name switch
        {
            nameof(Math.Abs) => "ABS",
            nameof(Math.Ceiling) => "CEIL",
            nameof(Math.Floor) => "FLOOR",
            nameof(Math.Round) when arguments.Count == 1 => "ROUND",
            nameof(Math.Round) when arguments.Count == 2 => "ROUND",
            nameof(Math.Pow) => "POWER",
            nameof(Math.Sqrt) => "SQRT",
            nameof(Math.Log) when arguments.Count == 1 => "LOG",
            nameof(Math.Sign) => "SIGN",
            nameof(Math.Truncate) => "TRUNCATE",
            nameof(Math.Max) when arguments.Count == 2 => null, // handled below as CASE WHEN
            nameof(Math.Min) when arguments.Count == 2 => null, // handled below as CASE WHEN
            _ => null
        };

        // Math.Max(a, b) → CASE WHEN a > b THEN a ELSE b END
        if (method.Name == nameof(Math.Max) && arguments.Count == 2)
            return _sql.Case(new[] { new CaseWhenClause(_sql.GreaterThan(arguments[0], arguments[1]), arguments[0]) }, arguments[1]);

        // Math.Min(a, b) → CASE WHEN a < b THEN a ELSE b END
        if (method.Name == nameof(Math.Min) && arguments.Count == 2)
            return _sql.Case(new[] { new CaseWhenClause(_sql.LessThan(arguments[0], arguments[1]), arguments[0]) }, arguments[1]);

        if (funcName == null) return null;

        return _sql.Function(funcName, arguments, nullable: true, argumentsPropagateNullability: arguments.Select(_ => true).ToArray(), method.ReturnType);
    }
}

/// <summary>
/// Guid.NewGuid() → gen_random_uuid_v7()
/// </summary>
internal class EvolutionDbGuidMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbGuidMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType == typeof(Guid) && method.Name == nameof(Guid.NewGuid))
            return _sql.Function("gen_random_uuid_v7", Array.Empty<SqlExpression>(), nullable: false, argumentsPropagateNullability: Array.Empty<bool>(), typeof(Guid));

        return null;
    }
}

/// <summary>
/// EvolutionDbFunctions.SnowflakeId() → snowflake_id()
/// EvolutionDbFunctions.NewUuidV7() → gen_random_uuid_v7()
/// EvolutionDbFunctions.NewUuid() → gen_random_uuid()
/// </summary>
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

/// <summary>
/// object.ToString() → CAST(x AS TEXT)
/// object.Equals() → x = y
/// </summary>
internal class EvolutionDbObjectMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    public EvolutionDbObjectMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.Name == nameof(object.ToString) && instance != null && arguments.Count == 0)
            return _sql.Function("CAST", new[] { _sql.Fragment($"{instance} AS TEXT") }, nullable: true, argumentsPropagateNullability: new[] { true }, typeof(string));

        if (method.Name == nameof(object.Equals) && instance != null && arguments.Count == 1)
            return _sql.Equal(instance, arguments[0]);

        return null;
    }
}

/// <summary>
/// Convert.ToInt32(x) → CAST(x AS INT)
/// Convert.ToString(x) → CAST(x AS TEXT)
/// Convert.ToDouble(x) → CAST(x AS DOUBLE)
/// Convert.ToInt64(x) → CAST(x AS BIGINT)
/// Convert.ToBoolean(x) → CAST(x AS BOOLEAN)
/// Convert.ToDecimal(x) → CAST(x AS DECIMAL)
/// Convert.ToInt16(x) → CAST(x AS SMALLINT)
/// Convert.ToSingle(x) → CAST(x AS FLOAT)
/// </summary>
internal class EvolutionDbConvertMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sql;

    private static readonly Dictionary<string, string> ConvertMappings = new()
    {
        { nameof(Convert.ToInt32), "INT" },
        { nameof(Convert.ToString), "TEXT" },
        { nameof(Convert.ToDouble), "DOUBLE" },
        { nameof(Convert.ToInt64), "BIGINT" },
        { nameof(Convert.ToBoolean), "BOOLEAN" },
        { nameof(Convert.ToDecimal), "DECIMAL" },
        { nameof(Convert.ToInt16), "SMALLINT" },
        { nameof(Convert.ToSingle), "FLOAT" },
    };

    public EvolutionDbConvertMethodTranslator(ISqlExpressionFactory sql)
    {
        _sql = sql;
    }

    public SqlExpression? Translate(SqlExpression? instance, System.Reflection.MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(Convert) || arguments.Count != 1) return null;

        if (!ConvertMappings.TryGetValue(method.Name, out var targetType)) return null;

        return _sql.Function("CAST", new[] { _sql.Fragment($"{arguments[0]} AS {targetType}") }, nullable: true, argumentsPropagateNullability: new[] { true }, method.ReturnType);
    }
}

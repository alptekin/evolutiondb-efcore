using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using evosql.Internal;

namespace evosql;

public class EvosqlCommand : DbCommand
{
    private static long _stmtCounter;

    private string _commandText = "";
    private EvosqlConnection? _connection;
    private EvosqlTransaction? _transaction;
    private readonly EvosqlParameterCollection _parameters = new();
    private bool _isPrepared;
    private string? _preparedName;
    private Dictionary<string, int>? _parameterMapping;

    public EvosqlCommand() { }

    public EvosqlCommand(string commandText)
    {
        _commandText = commandText;
    }

    public EvosqlCommand(string commandText, EvosqlConnection connection)
    {
        _commandText = commandText;
        _connection = connection;
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText { get => _commandText; set => _commandText = value ?? ""; }
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;
    protected override DbConnection? DbConnection { get => _connection; set => _connection = (EvosqlConnection?)value; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get => _transaction; set => _transaction = (EvosqlTransaction?)value; }

    public override void Cancel() { }
    public override void Prepare()
    {
        if (_isPrepared) return;

        if (_connection is null)
            throw new InvalidOperationException("Connection is not set.");
        if (_connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection is not open.");

        _preparedName = $"__evosql_stmt_{Interlocked.Increment(ref _stmtCounter)}";

        // Build parameter mapping: @name -> $N (1-based)
        _parameterMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var paramIndex = 1;
        for (var i = 0; i < _parameters.Count; i++)
        {
            var p = (EvosqlParameter)_parameters[i];
            var name = p.ParameterName.StartsWith('@') ? p.ParameterName : "@" + p.ParameterName;
            _parameterMapping[name] = paramIndex++;
        }

        // Convert @param placeholders to $1, $2, etc.
        var sql = _commandText;
        // Sort by name length descending to avoid partial replacements
        var sorted = _parameterMapping.OrderByDescending(kv => kv.Key.Length);
        foreach (var kv in sorted)
            sql = sql.Replace(kv.Key, "$" + kv.Value);

        var result = _connection.Client.PrepareStatement(_preparedName, sql);
        if (result.HasError)
            throw new EvosqlException(result.ErrorMessage ?? "Prepare failed", result.ErrorSqlState ?? "42000");

        _isPrepared = true;
    }
    protected override DbParameter CreateDbParameter() => new EvosqlParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var result = ExecuteQueryInternal();
        return new EvosqlDataReader(result);
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteDbDataReader(behavior));

    public override int ExecuteNonQuery()
    {
        var result = ExecuteQueryInternal();
        return ParseAffectedRows(result.CommandTag);
    }

    public override object? ExecuteScalar()
    {
        var result = ExecuteQueryInternal();

        if (result.Rows.Count == 0 || result.Columns.Count == 0)
            return null;

        var raw = result.Rows[0][0];
        if (raw is null)
            return DBNull.Value;

        var oid = result.Columns[0].PgTypeOid;
        return ConvertFromString(raw, oid);
    }

    private EvoQueryResult ExecuteQueryInternal()
    {
        if (_connection is null)
            throw new InvalidOperationException("Connection is not set.");

        if (_connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection is not open.");

        EvoQueryResult result;

        if (_isPrepared && _preparedName is not null && _parameterMapping is not null)
        {
            // Build parameter values array in $1, $2, ... order
            var paramValues = new string?[_parameterMapping.Count];
            for (var i = 0; i < _parameters.Count; i++)
            {
                var p = (EvosqlParameter)_parameters[i];
                var name = p.ParameterName.StartsWith('@') ? p.ParameterName : "@" + p.ParameterName;
                if (_parameterMapping.TryGetValue(name, out var idx))
                    paramValues[idx - 1] = FormatExecuteParameterValue(p.Value);
            }

            result = _connection.Client.ExecutePrepared(_preparedName, paramValues);
        }
        else
        {
            var sql = SubstituteParameters(_commandText);
            result = _connection.Client.ExecuteQuery(sql);
        }

        if (result.HasError)
            throw new EvosqlException(result.ErrorMessage ?? "Query failed", result.ErrorSqlState ?? "42000");

        return result;
    }

    private string SubstituteParameters(string sql)
    {
        if (_parameters.Count == 0)
            return sql;

        // Sort by name length descending to avoid partial replacements
        // e.g., @param10 should be replaced before @param1
        var sorted = new List<EvosqlParameter>();
        for (var i = 0; i < _parameters.Count; i++)
            sorted.Add((EvosqlParameter)_parameters[i]);
        sorted.Sort((a, b) => b.ParameterName.Length.CompareTo(a.ParameterName.Length));

        var result = new StringBuilder(sql);
        foreach (var param in sorted)
        {
            var name = param.ParameterName.StartsWith('@') ? param.ParameterName : "@" + param.ParameterName;
            result.Replace(name, FormatParameterValue(param.Value));
        }

        return result.ToString();
    }

    private static string? FormatExecuteParameterValue(object? value)
    {
        if (value is null || value == DBNull.Value)
            return null;

        return value switch
        {
            string s => s,
            bool b => b ? "TRUE" : "FALSE",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Guid g => g.ToString(),
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string FormatParameterValue(object? value)
    {
        if (value is null || value == DBNull.Value)
            return "NULL";

        return value switch
        {
            string s => "'" + s.Replace("'", "''") + "'",
            bool b => b ? "TRUE" : "FALSE",
            DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
            DateOnly d => "'" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'",
            Guid g => "'" + g.ToString() + "'",
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "NULL"
        };
    }

    private static int ParseAffectedRows(string? commandTag)
    {
        if (string.IsNullOrEmpty(commandTag))
            return 0;

        // Command tags: "INSERT 0 N", "UPDATE N", "DELETE N", "CREATE TABLE", etc.
        var parts = commandTag.Split(' ');

        if (parts.Length >= 3 && parts[0].Equals("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(parts[2], out var rows))
                return rows;
        }
        else if (parts.Length >= 2)
        {
            if (int.TryParse(parts[^1], out var rows))
                return rows;
        }

        return 0;
    }

    private static object ConvertFromString(string raw, int pgTypeOid) => pgTypeOid switch
    {
        16 => raw is "t" or "true" or "1",
        21 => short.Parse(raw, CultureInfo.InvariantCulture),
        23 => int.Parse(raw, CultureInfo.InvariantCulture),
        20 => long.Parse(raw, CultureInfo.InvariantCulture),
        700 => float.Parse(raw, CultureInfo.InvariantCulture),
        701 => double.Parse(raw, CultureInfo.InvariantCulture),
        1700 => decimal.Parse(raw, CultureInfo.InvariantCulture),
        2950 => Guid.Parse(raw),
        _ => raw
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing && _isPrepared && _preparedName is not null && _connection?.State == ConnectionState.Open)
        {
            try { _connection.Client.DeallocateStatement(_preparedName); } catch { }
            _isPrepared = false;
            _preparedName = null;
            _parameterMapping = null;
        }

        base.Dispose(disposing);
    }
}

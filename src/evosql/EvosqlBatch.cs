using System.Data;
using System.Globalization;

namespace evosql;

/// <summary>
/// Executes a prepared command multiple times with different parameter sets
/// in a single network roundtrip. Requires the command to be prepared first.
/// </summary>
public class EvosqlBatch
{
    private readonly EvosqlCommand _command;
    private readonly List<string?[]> _rows = new();
    private readonly int _paramCount;

    public EvosqlBatch(EvosqlCommand command)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));

        if (!_command.IsPrepared)
            _command.Prepare();

        _paramCount = _command.Parameters.Count;
    }

    /// <summary>
    /// Number of rows queued in this batch.
    /// </summary>
    public int Count => _rows.Count;

    /// <summary>
    /// Add a row with parameters matching the command's parameter order.
    /// Values are formatted as strings using invariant culture.
    /// </summary>
    public void Add(params object?[] values)
    {
        if (values.Length != _paramCount)
            throw new ArgumentException($"Expected {_paramCount} parameters, got {values.Length}");

        var row = new string?[_paramCount];
        for (var i = 0; i < _paramCount; i++)
            row[i] = FormatValue(values[i]);

        _rows.Add(row);
    }

    /// <summary>
    /// Execute all queued rows in a single server roundtrip.
    /// Returns the total number of affected rows.
    /// </summary>
    public long Execute()
    {
        if (_rows.Count == 0) return 0;

        var conn = (EvosqlConnection)_command.Connection!;
        if (conn.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection is not open.");

        var flat = new string?[_rows.Count * _paramCount];
        for (var r = 0; r < _rows.Count; r++)
        {
            for (var p = 0; p < _paramCount; p++)
                flat[r * _paramCount + p] = _rows[r][p];
        }

        var result = conn.Client.ExecuteBatch(_command.PreparedName!, _rows.Count, _paramCount, flat);

        _rows.Clear();

        if (result.HasError)
            throw new EvosqlException($"Batch failed at row {result.ErrorRow}: {result.ErrorMessage}", result.ErrorSqlState);

        return result.TotalAffected;
    }

    /// <summary>
    /// Discard queued rows without executing.
    /// </summary>
    public void Clear() => _rows.Clear();

    private static string? FormatValue(object? value) => value switch
    {
        null or DBNull => null,
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

using System.Data;
using System.Data.Common;
using System.Globalization;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Wraps NpgsqlDataReader to handle EvolutionDB's text-mode responses.
/// Npgsql's type handler resolution may return String for all fields when
/// connected to EvolutionDB. This wrapper converts string values to the
/// requested CLR types based on the DataTypeName from RowDescription OIDs.
/// </summary>
public class EvolutionDbDataReader : DbDataReader
{
    private readonly DbDataReader _inner;

    public EvolutionDbDataReader(DbDataReader inner)
    {
        _inner = inner;
    }

    public override int FieldCount => _inner.FieldCount;
    public override bool HasRows => _inner.HasRows;
    public override bool IsClosed => _inner.IsClosed;
    public override int RecordsAffected => _inner.RecordsAffected;
    public override int Depth => _inner.Depth;

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read() => _inner.Read();
    public override bool NextResult() => _inner.NextResult();
    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => _inner.ReadAsync(cancellationToken);
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => _inner.NextResultAsync(cancellationToken);
    public override void Close() => _inner.Close();

    public override string GetName(int ordinal) => _inner.GetName(ordinal);
    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
    public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
    public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => _inner.IsDBNullAsync(ordinal, cancellationToken);

    public override Type GetFieldType(int ordinal)
    {
        var typeName = _inner.GetDataTypeName(ordinal);
        return typeName switch
        {
            "integer" or "int4" => typeof(int),
            "bigint" or "int8" => typeof(long),
            "smallint" or "int2" => typeof(short),
            "boolean" or "bool" => typeof(bool),
            "real" or "float4" => typeof(float),
            "double precision" or "float8" => typeof(double),
            "numeric" or "decimal" => typeof(decimal),
            "text" or "varchar" or "character varying" or "bpchar" or "character" => typeof(string),
            "date" => typeof(DateOnly),
            "timestamp" or "timestamp without time zone" => typeof(DateTime),
            "uuid" => typeof(Guid),
            _ => typeof(string)
        };
    }

    public override object GetValue(int ordinal)
    {
        if (_inner.IsDBNull(ordinal)) return DBNull.Value;

        var raw = _inner.GetValue(ordinal);
        if (raw is DBNull) return raw;

        var str = raw.ToString() ?? "";
        var expectedType = GetFieldType(ordinal);

        if (expectedType == typeof(string)) return str;
        if (expectedType == typeof(int) && int.TryParse(str, out var i)) return i;
        if (expectedType == typeof(long) && long.TryParse(str, out var l)) return l;
        if (expectedType == typeof(short) && short.TryParse(str, out var sh)) return sh;
        if (expectedType == typeof(bool)) return str == "t" || str == "true" || str == "1";
        if (expectedType == typeof(float) && float.TryParse(str, CultureInfo.InvariantCulture, out var f)) return f;
        if (expectedType == typeof(double) && double.TryParse(str, CultureInfo.InvariantCulture, out var d)) return d;
        if (expectedType == typeof(decimal) && decimal.TryParse(str, CultureInfo.InvariantCulture, out var dec)) return dec;
        if (expectedType == typeof(DateTime) && DateTime.TryParse(str, CultureInfo.InvariantCulture, out var dt)) return dt;
        if (expectedType == typeof(DateOnly) && DateOnly.TryParse(str, CultureInfo.InvariantCulture, out var dateOnly)) return dateOnly;
        if (expectedType == typeof(Guid) && Guid.TryParse(str, out var guid)) return guid;

        return raw;
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
            values[i] = GetValue(i);
        return count;
    }

    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override Guid GetGuid(int ordinal) => Guid.Parse(GetValue(ordinal).ToString()!);
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override string GetString(int ordinal) => _inner.IsDBNull(ordinal) ? "" : (_inner.GetValue(ordinal)?.ToString() ?? "");

    public override T GetFieldValue<T>(int ordinal)
    {
        if (_inner.IsDBNull(ordinal)) return default!;

        var value = GetValue(ordinal);
        if (value is T typed) return typed;

        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override System.Collections.IEnumerator GetEnumerator() => new DbEnumerator(this);
    public override DataTable GetSchemaTable() => _inner.GetSchemaTable()!;
}

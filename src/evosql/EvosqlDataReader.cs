using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;
using evosql.Internal;

namespace evosql;

public class EvosqlDataReader : DbDataReader
{
    private readonly EvoQueryResult _result;
    private int _currentRow = -1;
    private bool _isClosed;

    internal EvosqlDataReader(EvoQueryResult result)
    {
        _result = result;
    }

    public override int FieldCount => _result.Columns.Count;
    public override bool HasRows => _result.Rows.Count > 0;
    public override bool IsClosed => _isClosed;
    public override int RecordsAffected => _result.RecordsAffected;
    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (_isClosed)
            return false;

        _currentRow++;
        return _currentRow < _result.Rows.Count;
    }

    public override bool NextResult() => false;

    public override void Close()
    {
        _isClosed = true;
    }

    public override string GetName(int ordinal) => _result.Columns[ordinal].Name;

    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < _result.Columns.Count; i++)
        {
            if (string.Equals(_result.Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new IndexOutOfRangeException($"Column '{name}' not found.");
    }

    public override bool IsDBNull(int ordinal) => _result.Rows[_currentRow][ordinal] is null;

    public override Type GetFieldType(int ordinal)
    {
        var oid = _result.Columns[ordinal].PgTypeOid;
        return oid switch
        {
            16 => typeof(bool),
            21 => typeof(short),
            23 => typeof(int),
            20 => typeof(long),
            700 => typeof(float),
            701 => typeof(double),
            1700 => typeof(decimal),
            25 or 1042 or 1043 => typeof(string),
            1082 => typeof(DateOnly),
            1114 => typeof(DateTime),
            2950 => typeof(Guid),
            _ => typeof(string)
        };
    }

    public override string GetDataTypeName(int ordinal)
    {
        var oid = _result.Columns[ordinal].PgTypeOid;
        return oid switch
        {
            16 => "boolean",
            21 => "smallint",
            23 => "integer",
            20 => "bigint",
            700 => "real",
            701 => "double precision",
            1700 => "numeric",
            25 => "text",
            1042 => "character",
            1043 => "character varying",
            1082 => "date",
            1114 => "timestamp without time zone",
            2950 => "uuid",
            _ => "text"
        };
    }

    public override object GetValue(int ordinal)
    {
        var raw = _result.Rows[_currentRow][ordinal];
        if (raw is null)
            return DBNull.Value;

        var oid = _result.Columns[ordinal].PgTypeOid;
        return ConvertValue(raw, oid);
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
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override string GetString(int ordinal) => _result.Rows[_currentRow][ordinal] ?? string.Empty;

    public override Guid GetGuid(int ordinal)
    {
        var raw = _result.Rows[_currentRow][ordinal]
                  ?? throw new InvalidCastException("Cannot convert DBNull to Guid.");
        return Guid.Parse(raw);
    }

    public override T GetFieldValue<T>(int ordinal)
    {
        if (IsDBNull(ordinal))
            return default!;

        var value = GetValue(ordinal);
        if (value is T typed)
            return typed;

        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => throw new NotSupportedException();

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var s = GetString(ordinal);
        if (buffer is null)
            return s.Length;

        var count = Math.Min(length, s.Length - (int)dataOffset);
        s.CopyTo((int)dataOffset, buffer, bufferOffset, count);
        return count;
    }

    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    public override DataTable GetSchemaTable()
    {
        var table = new DataTable("SchemaTable");
        table.Columns.Add("ColumnName", typeof(string));
        table.Columns.Add("ColumnOrdinal", typeof(int));
        table.Columns.Add("DataType", typeof(Type));
        table.Columns.Add("DataTypeName", typeof(string));

        for (var i = 0; i < _result.Columns.Count; i++)
        {
            var row = table.NewRow();
            row["ColumnName"] = _result.Columns[i].Name;
            row["ColumnOrdinal"] = i;
            row["DataType"] = GetFieldType(i);
            row["DataTypeName"] = GetDataTypeName(i);
            table.Rows.Add(row);
        }

        return table;
    }

    private static object ConvertValue(string raw, int pgTypeOid)
    {
        return pgTypeOid switch
        {
            16 => raw is "t" or "true" or "1",
            21 => short.Parse(raw, CultureInfo.InvariantCulture),
            23 => int.Parse(raw, CultureInfo.InvariantCulture),
            20 => long.Parse(raw, CultureInfo.InvariantCulture),
            700 => float.Parse(raw, CultureInfo.InvariantCulture),
            701 => double.Parse(raw, CultureInfo.InvariantCulture),
            1700 => decimal.Parse(raw, CultureInfo.InvariantCulture),
            25 or 1042 or 1043 => raw,
            1082 => DateOnly.Parse(raw, CultureInfo.InvariantCulture),
            1114 => DateTime.Parse(raw, CultureInfo.InvariantCulture),
            2950 => Guid.Parse(raw),
            _ => raw
        };
    }
}

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace evosql;

public class EvosqlParameter : DbParameter
{
    private string _parameterName = "";
    private object? _value;
    private DbType _dbType = DbType.String;
    private ParameterDirection _direction = ParameterDirection.Input;
    private string _sourceColumn = "";

    public EvosqlParameter()
    {
        //...
    }

    public EvosqlParameter(string parameterName, object? value)
    {
        _parameterName = parameterName;
        _value = value;
    }

    [AllowNull]
    public override string ParameterName
    {
        get => _parameterName;
        set => _parameterName = value ?? "";
    }

    public override object? Value
    {
        get => _value;
        set => _value = value;
    }

    public override DbType DbType
    {
        get => _dbType;
        set => _dbType = value;
    }

    public override ParameterDirection Direction
    {
        get => _direction;
        set => _direction = value;
    }

    public override bool IsNullable { get; set; }

    public override int Size { get; set; }

    public override byte Precision { get; set; }

    public override byte Scale { get; set; }

    [AllowNull]
    public override string SourceColumn
    {
        get => _sourceColumn;
        set => _sourceColumn = value ?? "";
    }

    public override bool SourceColumnNullMapping { get; set; }

    public override DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;

    public override void ResetDbType() => _dbType = DbType.String;
}

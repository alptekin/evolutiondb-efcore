using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace EvoSQL.EntityFrameworkCore.Storage.Internal;

public class EvoSqlTypeMappingSource : RelationalTypeMappingSource
{
    private static readonly BoolTypeMapping Bool = new("BOOLEAN", DbType.Boolean);
    private static readonly ShortTypeMapping SmallInt = new("SMALLINT", DbType.Int16);
    private static readonly IntTypeMapping Int = new("INT", DbType.Int32);
    private static readonly LongTypeMapping BigInt = new("BIGINT", DbType.Int64);
    private static readonly FloatTypeMapping Float = new("FLOAT", DbType.Single);
    private static readonly DoubleTypeMapping Double = new("DOUBLE", DbType.Double);
    private static readonly DecimalTypeMapping Decimal = new("DECIMAL", DbType.Decimal);
    private static readonly StringTypeMapping Text = new("TEXT", DbType.String);
    private static readonly DateOnlyTypeMapping Date = new("DATE", DbType.Date);
    private static readonly TimeOnlyTypeMapping Time = new("TIME", DbType.Time);
    private static readonly DateTimeTypeMapping Timestamp = new("TIMESTAMP", DbType.DateTime);
    private static readonly GuidTypeMapping Uuid = new("UUID", DbType.Guid);

    private static readonly Dictionary<Type, RelationalTypeMapping> ClrTypeMappings = new()
    {
        { typeof(bool), Bool },
        { typeof(short), SmallInt },
        { typeof(int), Int },
        { typeof(long), BigInt },
        { typeof(float), Float },
        { typeof(double), Double },
        { typeof(decimal), Decimal },
        { typeof(string), Text },
        { typeof(DateOnly), Date },
        { typeof(TimeOnly), Time },
        { typeof(DateTime), Timestamp },
        { typeof(Guid), Uuid },
    };

    private static readonly Dictionary<string, RelationalTypeMapping> StoreTypeMappings
        = new(StringComparer.OrdinalIgnoreCase)
    {
        { "boolean", Bool },
        { "bool", Bool },
        { "smallint", SmallInt },
        { "int2", SmallInt },
        { "int", Int },
        { "integer", Int },
        { "int4", Int },
        { "bigint", BigInt },
        { "int8", BigInt },
        { "float", Float },
        { "real", Float },
        { "float4", Float },
        { "double", Double },
        { "double precision", Double },
        { "float8", Double },
        { "decimal", Decimal },
        { "numeric", Decimal },
        { "text", Text },
        { "date", Date },
        { "time", Time },
        { "timestamp", Timestamp },
        { "datetime", Timestamp },
        { "uuid", Uuid },
    };

    public EvoSqlTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        var storeTypeName = mappingInfo.StoreTypeName;

        if (storeTypeName != null)
        {
            // Handle VARCHAR(n), CHAR(n)
            var baseName = storeTypeName;
            var parenIdx = storeTypeName.IndexOf('(');
            if (parenIdx > 0)
                baseName = storeTypeName[..parenIdx].Trim();

            if (baseName.Equals("varchar", StringComparison.OrdinalIgnoreCase)
                || baseName.Equals("character varying", StringComparison.OrdinalIgnoreCase))
            {
                return new StringTypeMapping(storeTypeName, DbType.String, size: mappingInfo.Size ?? 255);
            }

            if (baseName.Equals("char", StringComparison.OrdinalIgnoreCase)
                || baseName.Equals("character", StringComparison.OrdinalIgnoreCase)
                || baseName.Equals("bpchar", StringComparison.OrdinalIgnoreCase))
            {
                return new StringTypeMapping(storeTypeName, DbType.StringFixedLength, size: mappingInfo.Size ?? 1);
            }

            if (StoreTypeMappings.TryGetValue(baseName, out var storeMapping))
                return storeMapping;
        }

        if (clrType != null)
        {
            if (ClrTypeMappings.TryGetValue(clrType, out var clrMapping))
            {
                // Handle string with MaxLength
                if (clrType == typeof(string) && mappingInfo.Size is > 0)
                {
                    return new StringTypeMapping(
                        $"VARCHAR({mappingInfo.Size})", DbType.String, size: mappingInfo.Size);
                }

                return clrMapping;
            }

            // byte[] → TEXT (base64) — no native binary type
            if (clrType == typeof(byte[]))
                return Text;
        }

        return base.FindMapping(mappingInfo);
    }
}

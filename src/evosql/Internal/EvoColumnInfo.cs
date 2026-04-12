namespace evosql.Internal;

public class EvoColumnInfo
{
    public string Name { get; set; } = "";
    public int PgTypeOid { get; set; }
    public int TypeModifier { get; set; } = -1;
}

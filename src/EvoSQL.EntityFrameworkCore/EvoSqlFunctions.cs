namespace EvoSQL.EntityFrameworkCore;

/// <summary>
/// EvoSQL-specific database functions for use in LINQ queries.
/// These methods are translated to SQL and cannot be evaluated client-side.
/// </summary>
public static class EvoSqlFunctions
{
    /// <summary>
    /// Generates a 64-bit time-ordered Snowflake ID.
    /// Translates to: snowflake_id()
    /// </summary>
    public static long SnowflakeId()
        => throw new InvalidOperationException(
            "EvoSqlFunctions.SnowflakeId() can only be used in EF Core LINQ queries.");

    /// <summary>
    /// Generates a UUID v7 (time-ordered, RFC 9562).
    /// Translates to: gen_random_uuid_v7()
    /// </summary>
    public static Guid NewUuidV7()
        => throw new InvalidOperationException(
            "EvoSqlFunctions.NewUuidV7() can only be used in EF Core LINQ queries.");

    /// <summary>
    /// Generates a UUID v4 (random).
    /// Translates to: gen_random_uuid()
    /// </summary>
    public static Guid NewUuid()
        => throw new InvalidOperationException(
            "EvoSqlFunctions.NewUuid() can only be used in EF Core LINQ queries.");
}

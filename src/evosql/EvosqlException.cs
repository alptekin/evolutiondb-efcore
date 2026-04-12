using System.Data.Common;

namespace evosql;

public class EvosqlException : DbException
{
    public new string? SqlState { get; }

    public EvosqlException(string message, string? sqlState)
        : base(message)
    {
        SqlState = sqlState;
    }

    public EvosqlException(string message, string? sqlState, Exception? innerException)
        : base(message, innerException)
    {
        SqlState = sqlState;
    }
}

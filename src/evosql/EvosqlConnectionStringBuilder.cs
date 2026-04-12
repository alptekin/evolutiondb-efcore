using System.Data.Common;

namespace evosql;

public class EvosqlConnectionStringBuilder : DbConnectionStringBuilder
{
    public string Host { get => GetString("Host", "localhost"); set => this["Host"] = value; }
    public int Port { get => GetInt("Port", 9967); set => this["Port"] = value; }
    public string Username { get => GetString("Username", GetString("User", "admin")); set => this["Username"] = value; }
    public string Password { get => GetString("Password", ""); set => this["Password"] = value; }
    public string Database { get => GetString("Database", "testdb"); set => this["Database"] = value; }
    public int Timeout { get => GetInt("Timeout", 30); set => this["Timeout"] = value; }

    public EvosqlConnectionStringBuilder()
    {
        //...
    }

    public EvosqlConnectionStringBuilder(string connectionString)
    {
        ConnectionString = connectionString;
    }

    private string GetString(string key, string defaultValue) => TryGetValue(key, out var v) && v != null ? v.ToString()! : defaultValue;
    private int GetInt(string key, int defaultValue) => TryGetValue(key, out var v) && v != null && int.TryParse(v.ToString(), out var i) ? i : defaultValue;
}

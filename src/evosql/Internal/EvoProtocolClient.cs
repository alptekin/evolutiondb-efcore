using System.Net.Sockets;
using System.Text;

namespace evosql.Internal;

public class EvoQueryResult
{
    public bool IsSelect { get; set; }
    public bool HasError { get; set; }
    public string? ErrorSqlState { get; set; }
    public string? ErrorMessage { get; set; }
    public string CommandTag { get; set; } = "";
    public List<EvoColumnInfo> Columns { get; set; } = new();
    public List<string?[]> Rows { get; set; } = new();
    public int RecordsAffected { get; set; } = -1;
}

public class EvoProtocolClient : IDisposable
{
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;

    public bool IsConnected => _tcp?.Connected == true;

    public void Connect(string host, int port, int timeoutMs = 30000)
    {
        _tcp = new TcpClient();
        _tcp.SendTimeout = timeoutMs;
        _tcp.ReceiveTimeout = timeoutMs;
        _tcp.Connect(host, port);

        var stream = _tcp.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Authenticate(string username, string password)
    {
        // Greeting
        SendLine("EVO");
        var hello = ReadLine();
        if (hello == null || !hello.StartsWith("HELLO"))
            throw new EvosqlException("Invalid server greeting: " + (hello ?? "null"), "08000");

        // TLS negotiation (decline)
        var next = ReadLine();
        if (next == "STARTTLS")
        {
            SendLine("NOTLS");
            next = ReadLine();
        }

        if (next != "AUTH_REQUIRED")
            throw new EvosqlException("Expected AUTH_REQUIRED, got: " + (next ?? "null"), "08000");

        // Send credentials
        SendLine($"AUTH {username} {password}");
        var authResult = ReadLine();

        if (authResult == "AUTH_OK") return;

        if (authResult != null && authResult.StartsWith("ERR "))
        {
            var parts = authResult[4..].Split(' ', 2);
            throw new EvosqlException(parts.Length > 1 ? parts[1] : "Authentication failed", parts[0]);
        }

        throw new EvosqlException("Authentication failed: " + (authResult ?? "null"), "28P01");
    }

    public EvoQueryResult ExecuteQuery(string sql)
    {
        var sqlBytes = Encoding.UTF8.GetBytes(sql);
        SendLine($"SQL {sqlBytes.Length}");
        SendLine(sql);

        var result = new EvoQueryResult();

        while (true)
        {
            var line = ReadLine();
            if (line == null) throw new EvosqlException("Connection closed unexpectedly", "08006");

            if (line == "READY") break;

            if (line == "RESULT")
            {
                result.IsSelect = true;
                ReadResultSet(result);
            }
            else if (line == "OK")
            {
                result.IsSelect = false;
            }
            else if (line.StartsWith("TAG "))
            {
                result.CommandTag = line[4..];
                result.RecordsAffected = ParseRecordsAffected(result.CommandTag);
            }
            else if (line.StartsWith("ERR "))
            {
                result.HasError = true;
                var parts = line[4..].Split(' ', 2);
                result.ErrorSqlState = parts[0];
                result.ErrorMessage = parts.Length > 1 ? parts[1] : "Unknown error";
            }
        }

        return result;
    }

    public void SendQuit()
    {
        try { SendLine("QUIT"); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { SendQuit(); } catch { }
        _writer?.Dispose();
        _reader?.Dispose();
        _tcp?.Dispose();
    }

    private void ReadResultSet(EvoQueryResult result)
    {
        // Read COLS <n>
        var colsLine = ReadLine();
        if (colsLine == null || !colsLine.StartsWith("COLS ")) return;
        var numCols = int.Parse(colsLine[5..]);

        // Read COL <name> <oid> <modifier>
        for (var i = 0; i < numCols; i++)
        {
            var colLine = ReadLine();
            if (colLine == null || !colLine.StartsWith("COL ")) break;

            var col = new EvoColumnInfo();
            var parts = colLine[4..].Split(' ');
            col.Name = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var oid)) col.PgTypeOid = oid;
            if (parts.Length > 2 && int.TryParse(parts[2], out var mod)) col.TypeModifier = mod;
            result.Columns.Add(col);
        }

        // Read rows
        while (true)
        {
            var line = ReadLine();
            if (line == null || line == "END") break;

            if (line == "ROW")
            {
                var fields = new string?[numCols];
                for (var i = 0; i < numCols; i++)
                {
                    var fieldLine = ReadLine();
                    if (fieldLine == null) break;

                    if (fieldLine == "FIELD NULL")
                        fields[i] = null;
                    else if (fieldLine.StartsWith("FIELD "))
                        fields[i] = fieldLine[6..];
                    else
                        fields[i] = fieldLine;
                }
                result.Rows.Add(fields);
            }
        }
    }

    private static int ParseRecordsAffected(string tag)
    {
        // "INSERT 0 1" -> 1, "UPDATE 5" -> 5, "DELETE 3" -> 3, "SELECT 42" -> -1
        var parts = tag.Split(' ');
        if (parts.Length == 0) return -1;

        var cmd = parts[0].ToUpperInvariant();
        if (cmd == "SELECT") return -1;

        if (parts.Length >= 3 && int.TryParse(parts[2], out var n3)) return n3;
        if (parts.Length >= 2 && int.TryParse(parts[1], out var n2)) return n2;

        return -1;
    }

    private void SendLine(string line) => _writer!.WriteLine(line);
    private string? ReadLine() => _reader!.ReadLine();
}

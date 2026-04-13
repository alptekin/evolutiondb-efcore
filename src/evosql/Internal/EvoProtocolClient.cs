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

public class EvoBatchResult
{
    public long TotalAffected { get; set; }
    public bool HasError { get; set; }
    public int ErrorRow { get; set; } = -1;
    public string ErrorSqlState { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

public class EvoProtocolClient : IDisposable
{
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;

    public bool IsConnected => _tcp?.Connected == true;

    private NetworkStream? _stream;

    public void Connect(string host, int port, int timeoutMs = 30000)
    {
        _tcp = new TcpClient();
        _tcp.SendTimeout = timeoutMs;
        _tcp.ReceiveTimeout = timeoutMs;
        _tcp.Connect(host, port);

        _stream = _tcp.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
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

        if (next == "AUTH_SCRAM")
        {
            AuthenticateScram(username, password);
            return;
        }

        if (next != "AUTH_REQUIRED")
            throw new EvosqlException("Expected AUTH_REQUIRED or AUTH_SCRAM, got: " + (next ?? "null"), "08000");

        // Cleartext auth (backward compatibility)
        SendLine($"AUTH {username} {password}");
        var authResult = ReadLine();

        if (authResult == "AUTH_OK") return;

        ThrowAuthError(authResult);
    }

    private void AuthenticateScram(string username, string password)
    {
        var scram = new ScramClient();

        // Step 1: Send client-first-message
        var clientFirst = scram.CreateClientFirst(username);
        SendLine($"SCRAM-CLIENT-FIRST {clientFirst}");

        // Step 2: Receive server-first-message
        var serverFirstLine = ReadLine();
        if (serverFirstLine == null || !serverFirstLine.StartsWith("SCRAM-SERVER-FIRST "))
            ThrowAuthError(serverFirstLine);
        var serverFirst = serverFirstLine!["SCRAM-SERVER-FIRST ".Length..];

        // Step 3: Send client-final-message
        var clientFinal = scram.CreateClientFinal(serverFirst, password);
        SendLine($"SCRAM-CLIENT-FINAL {clientFinal}");

        // Step 4: Receive server-final-message
        var serverFinalLine = ReadLine();
        if (serverFinalLine == null || !serverFinalLine.StartsWith("SCRAM-SERVER-FINAL "))
            ThrowAuthError(serverFinalLine);
        var serverFinal = serverFinalLine!["SCRAM-SERVER-FINAL ".Length..];

        if (!scram.ValidateServerFinal(serverFinal))
            throw new EvosqlException("SCRAM: server signature verification failed", "28000");
    }

    private static void ThrowAuthError(string? response)
    {
        if (response != null && response.StartsWith("ERR "))
        {
            var parts = response[4..].Split(' ', 2);
            throw new EvosqlException(parts.Length > 1 ? parts[1] : "Authentication failed", parts[0]);
        }

        throw new EvosqlException("Authentication failed: " + (response ?? "null"), "28P01");
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

    public EvoQueryResult PrepareStatement(string name, string sql)
    {
        var sqlBytes = Encoding.UTF8.GetBytes(sql);
        SendLine($"PREPARE {name} {sqlBytes.Length}");
        SendLine(sql);

        return ReadCommandResponse();
    }

    public EvoQueryResult ExecutePrepared(string name, string?[] parameters)
    {
        SendLine($"EXECUTE {name} {parameters.Length}");
        foreach (var p in parameters)
            SendLine(p is null ? "\\N" : p);

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

    /// <summary>
    /// Execute a prepared statement with N parameter sets in a single roundtrip.
    /// Server protocol: EXECUTE_BATCH name rowCount paramCount\n then rowCount * paramCount lines.
    /// Response: BATCH_OK totalAffected\n READY\n  or  BATCH_ERR rowIndex sqlstate msg\n READY\n
    /// </summary>
    public EvoBatchResult ExecuteBatch(string name, int rowCount, int paramCount, string?[] flattenedParams)
    {
        if (flattenedParams.Length != rowCount * paramCount)
            throw new ArgumentException($"Expected {rowCount * paramCount} params, got {flattenedParams.Length}");

        // Build the entire request in a single buffer to avoid per-line TCP writes.
        var sb = new StringBuilder(rowCount * paramCount * 16);
        sb.Append("EXECUTE_BATCH ").Append(name).Append(' ').Append(rowCount).Append(' ').Append(paramCount).Append('\n');
        foreach (var p in flattenedParams)
        {
            if (p is null) sb.Append("\\N");
            else sb.Append(p);
            sb.Append('\n');
        }
        // Write directly to NetworkStream, bypassing StreamWriter buffering quirks
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        _stream!.Write(bytes, 0, bytes.Length);
        _stream.Flush();

        var result = new EvoBatchResult();
        while (true)
        {
            var line = ReadLine();
            if (line == null) throw new EvosqlException("Connection closed unexpectedly", "08006");

            if (line == "READY") break;

            if (line.StartsWith("BATCH_OK "))
            {
                result.TotalAffected = long.Parse(line[9..]);
            }
            else if (line.StartsWith("BATCH_ERR "))
            {
                var parts = line[10..].Split(' ', 3);
                result.HasError = true;
                result.ErrorRow = int.Parse(parts[0]);
                result.ErrorSqlState = parts.Length > 1 ? parts[1] : "?????";
                result.ErrorMessage = parts.Length > 2 ? parts[2] : "";
            }
            else if (line.StartsWith("ERR "))
            {
                var parts = line[4..].Split(' ', 2);
                result.HasError = true;
                result.ErrorSqlState = parts[0];
                result.ErrorMessage = parts.Length > 1 ? parts[1] : "Unknown error";
            }
        }

        return result;
    }

    public EvoQueryResult DeallocateStatement(string name)
    {
        SendLine($"DEALLOCATE {name}");
        return ReadCommandResponse();
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

    private EvoQueryResult ReadCommandResponse()
    {
        var result = new EvoQueryResult();

        while (true)
        {
            var line = ReadLine();
            if (line == null) throw new EvosqlException("Connection closed unexpectedly", "08006");

            if (line == "READY") break;

            if (line == "OK")
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

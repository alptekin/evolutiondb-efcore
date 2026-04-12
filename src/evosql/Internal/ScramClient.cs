using System.Security.Cryptography;
using System.Text;

namespace evosql.Internal;

internal class ScramClient
{
    private string? _clientNonce;
    private string? _clientFirstBare;
    private string? _serverFirst;
    private string? _clientFinalWithoutProof;
    private byte[]? _serverKey;

    public string CreateClientFirst(string username)
    {
        var nonceBytes = new byte[24];
        RandomNumberGenerator.Fill(nonceBytes);
        _clientNonce = Convert.ToBase64String(nonceBytes);

        _clientFirstBare = $"n={username},r={_clientNonce}";
        return $"n,,{_clientFirstBare}";
    }

    public string CreateClientFinal(string serverFirst, string password)
    {
        _serverFirst = serverFirst;

        // Parse server-first-message: r=<combined_nonce>,s=<base64_salt>,i=<iterations>
        var fields = ParseMessage(serverFirst);

        var combinedNonce = fields["r"];
        var salt = Convert.FromBase64String(fields["s"]);
        var iterations = int.Parse(fields["i"]);

        // Verify combined nonce starts with client nonce
        if (!combinedNonce.StartsWith(_clientNonce!))
            throw new EvosqlException("SCRAM: server nonce does not start with client nonce", "28000");

        // SaltedPassword = PBKDF2-SHA256(password, salt, iterations, 32)
        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        // ClientKey = HMAC-SHA256(SaltedPassword, "Client Key")
        var clientKey = HmacSha256(saltedPassword, "Client Key"u8);

        // StoredKey = SHA-256(ClientKey)
        var storedKey = SHA256.HashData(clientKey);

        // ServerKey = HMAC-SHA256(SaltedPassword, "Server Key")
        _serverKey = HmacSha256(saltedPassword, "Server Key"u8);

        // client-final-without-proof
        _clientFinalWithoutProof = $"c=biws,r={combinedNonce}";

        // AuthMessage = client-first-bare + "," + server-first + "," + client-final-without-proof
        var authMessage = $"{_clientFirstBare},{_serverFirst},{_clientFinalWithoutProof}";
        var authMessageBytes = Encoding.UTF8.GetBytes(authMessage);

        // ClientSignature = HMAC-SHA256(StoredKey, AuthMessage)
        var clientSignature = HmacSha256(storedKey, authMessageBytes);

        // ClientProof = ClientKey XOR ClientSignature
        var clientProof = new byte[clientKey.Length];
        for (var i = 0; i < clientKey.Length; i++)
            clientProof[i] = (byte)(clientKey[i] ^ clientSignature[i]);

        return $"{_clientFinalWithoutProof},p={Convert.ToBase64String(clientProof)}";
    }

    public bool ValidateServerFinal(string serverFinal)
    {
        if (_serverKey == null || _clientFirstBare == null || _serverFirst == null || _clientFinalWithoutProof == null)
            return false;

        // Parse v=<base64_server_signature>
        var fields = ParseMessage(serverFinal);
        if (!fields.TryGetValue("v", out var serverSigBase64))
            return false;

        var receivedSignature = Convert.FromBase64String(serverSigBase64);

        // AuthMessage = client-first-bare + "," + server-first + "," + client-final-without-proof
        var authMessage = $"{_clientFirstBare},{_serverFirst},{_clientFinalWithoutProof}";
        var authMessageBytes = Encoding.UTF8.GetBytes(authMessage);

        // ServerSignature = HMAC-SHA256(ServerKey, AuthMessage)
        var expectedSignature = HmacSha256(_serverKey, authMessageBytes);

        return CryptographicOperations.FixedTimeEquals(receivedSignature, expectedSignature);
    }

    private static byte[] HmacSha256(byte[] key, ReadOnlySpan<byte> data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data.ToArray());
    }

    private static Dictionary<string, string> ParseMessage(string message)
    {
        var result = new Dictionary<string, string>();
        foreach (var part in message.Split(','))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex > 0)
                result[part[..eqIndex]] = part[(eqIndex + 1)..];
        }
        return result;
    }
}

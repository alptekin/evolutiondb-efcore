using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace EvolutionDb.EntityFrameworkCore.Tests;

/// <summary>
/// Unit tests for the SCRAM-SHA-256 client implementation.
/// Pure crypto tests — no server required.
/// </summary>
public class ScramTests
{
    // ScramClient is internal in evosql; accessible via InternalsVisibleTo.
    private static readonly Type ScramType = typeof(evosql.Internal.EvoProtocolClient).Assembly
        .GetType("evosql.Internal.ScramClient")!;

    private static object CreateScramClient() => Activator.CreateInstance(ScramType)!;

    private static string InvokeCreateClientFirst(object client, string username)
    {
        var method = ScramType.GetMethod("CreateClientFirst")!;
        return (string)method.Invoke(client, new object[] { username })!;
    }

    private static string InvokeCreateClientFinal(object client, string serverFirst, string password)
    {
        var method = ScramType.GetMethod("CreateClientFinal")!;
        return (string)method.Invoke(client, new object[] { serverFirst, password })!;
    }

    private static bool InvokeValidateServerFinal(object client, string serverFinal)
    {
        var method = ScramType.GetMethod("ValidateServerFinal")!;
        return (bool)method.Invoke(client, new object[] { serverFinal })!;
    }

    // ---- Test 1: CreateClientFirst format --------------------------------

    [Fact]
    public void CreateClientFirst_GeneratesValidFormat()
    {
        var client = CreateScramClient();
        var msg = InvokeCreateClientFirst(client, "testuser");

        // Must start with "n,," (GS2 header: no channel binding, no authzid)
        Assert.StartsWith("n,,", msg);

        // After GS2 header: n=<user>,r=<nonce>
        var bare = msg[3..]; // strip "n,,"
        var parts = bare.Split(',');
        Assert.Equal(2, parts.Length);
        Assert.StartsWith("n=", parts[0]);
        Assert.Equal("testuser", parts[0][2..]);
        Assert.StartsWith("r=", parts[1]);

        // Nonce must be non-empty base64
        var nonce = parts[1][2..];
        Assert.True(nonce.Length > 0, "Nonce should not be empty");
        // Verify it's valid base64
        var decoded = Convert.FromBase64String(nonce);
        Assert.Equal(24, decoded.Length); // 24 random bytes
    }

    // ---- Test 2: Client nonce is random ----------------------------------

    [Fact]
    public void CreateClientFirst_ProducesDifferentNonces()
    {
        var client1 = CreateScramClient();
        var msg1 = InvokeCreateClientFirst(client1, "user");
        var nonce1 = msg1.Split(",r=")[1];

        var client2 = CreateScramClient();
        var msg2 = InvokeCreateClientFirst(client2, "user");
        var nonce2 = msg2.Split(",r=")[1];

        Assert.NotEqual(nonce1, nonce2);
    }

    // ---- Test 3: CreateClientFinal computes correct proof ----------------

    [Fact]
    public void CreateClientFinal_ComputesCorrectProof()
    {
        // Use known values to verify the SCRAM computation.
        // We control the client nonce by creating the client and calling CreateClientFirst,
        // then build a server-first-message with a known salt/iterations.
        var client = CreateScramClient();
        var clientFirst = InvokeCreateClientFirst(client, "admin");

        // Extract client nonce from client-first
        var clientFirstBare = clientFirst[3..]; // strip "n,,"
        var clientNonce = clientFirstBare.Split(",r=")[1];

        // Construct a deterministic server-first-message
        var serverNonce = clientNonce + "serverportion";
        var salt = new byte[16];
        for (int i = 0; i < salt.Length; i++) salt[i] = (byte)(i + 1);
        var saltB64 = Convert.ToBase64String(salt);
        int iterations = 4096;

        var serverFirst = $"r={serverNonce},s={saltB64},i={iterations}";

        var clientFinal = InvokeCreateClientFinal(client, serverFirst, "pencil");

        // Verify format: c=biws,r=<combined_nonce>,p=<proof_base64>
        Assert.StartsWith("c=biws,", clientFinal);
        Assert.Contains($"r={serverNonce}", clientFinal);
        Assert.Contains(",p=", clientFinal);

        // Extract proof and verify it decodes to 32 bytes (SHA-256 output)
        var proofB64 = clientFinal.Split(",p=")[1];
        var proofBytes = Convert.FromBase64String(proofB64);
        Assert.Equal(32, proofBytes.Length);

        // Independently compute the expected proof
        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("pencil"), salt, iterations, HashAlgorithmName.SHA256, 32);
        var clientKey = HmacSha256(saltedPassword, Encoding.UTF8.GetBytes("Client Key"));
        var storedKey = SHA256.HashData(clientKey);
        var clientFinalWithoutProof = $"c=biws,r={serverNonce}";
        var authMessage = $"{clientFirstBare},{serverFirst},{clientFinalWithoutProof}";
        var clientSignature = HmacSha256(storedKey, Encoding.UTF8.GetBytes(authMessage));

        var expectedProof = new byte[32];
        for (int i = 0; i < 32; i++)
            expectedProof[i] = (byte)(clientKey[i] ^ clientSignature[i]);

        Assert.Equal(expectedProof, proofBytes);
    }

    // ---- Test 4: ValidateServerFinal accepts valid signature -------------

    [Fact]
    public void ValidateServerFinal_AcceptsValidSignature()
    {
        var client = CreateScramClient();
        var clientFirst = InvokeCreateClientFirst(client, "admin");
        var clientFirstBare = clientFirst[3..];
        var clientNonce = clientFirstBare.Split(",r=")[1];

        var serverNonce = clientNonce + "srvpart";
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var saltB64 = Convert.ToBase64String(salt);
        int iterations = 4096;
        var password = "testpass";

        var serverFirst = $"r={serverNonce},s={saltB64},i={iterations}";
        var clientFinal = InvokeCreateClientFinal(client, serverFirst, password);

        // Compute the correct server signature
        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
        var serverKey = HmacSha256(saltedPassword, Encoding.UTF8.GetBytes("Server Key"));

        var clientFinalWithoutProof = $"c=biws,r={serverNonce}";
        var authMessage = $"{clientFirstBare},{serverFirst},{clientFinalWithoutProof}";
        var serverSignature = HmacSha256(serverKey, Encoding.UTF8.GetBytes(authMessage));

        var serverFinal = $"v={Convert.ToBase64String(serverSignature)}";

        Assert.True(InvokeValidateServerFinal(client, serverFinal));
    }

    // ---- Test 5: ValidateServerFinal rejects invalid signature -----------

    [Fact]
    public void ValidateServerFinal_RejectsInvalidSignature()
    {
        var client = CreateScramClient();
        var clientFirst = InvokeCreateClientFirst(client, "admin");
        var clientFirstBare = clientFirst[3..];
        var clientNonce = clientFirstBare.Split(",r=")[1];

        var serverNonce = clientNonce + "srvbad";
        var salt = new byte[16];
        for (int i = 0; i < salt.Length; i++) salt[i] = (byte)i;
        var saltB64 = Convert.ToBase64String(salt);
        int iterations = 4096;

        var serverFirst = $"r={serverNonce},s={saltB64},i={iterations}";
        InvokeCreateClientFinal(client, serverFirst, "correctpass");

        // Send a garbage server signature
        var fakeSignature = new byte[32];
        RandomNumberGenerator.Fill(fakeSignature);
        var serverFinal = $"v={Convert.ToBase64String(fakeSignature)}";

        Assert.False(InvokeValidateServerFinal(client, serverFinal));
    }

    // ---- Test 6: ValidateServerFinal rejects wrong password signature ----

    [Fact]
    public void ValidateServerFinal_RejectsWrongPasswordSignature()
    {
        var client = CreateScramClient();
        var clientFirst = InvokeCreateClientFirst(client, "admin");
        var clientFirstBare = clientFirst[3..];
        var clientNonce = clientFirstBare.Split(",r=")[1];

        var serverNonce = clientNonce + "srv2";
        var salt = new byte[16];
        for (int i = 0; i < salt.Length; i++) salt[i] = (byte)(i + 10);
        var saltB64 = Convert.ToBase64String(salt);
        int iterations = 4096;

        var serverFirst = $"r={serverNonce},s={saltB64},i={iterations}";
        // Client uses "correctpass"
        InvokeCreateClientFinal(client, serverFirst, "correctpass");

        // Server signature computed with a DIFFERENT password
        var wrongSaltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("wrongpass"), salt, iterations, HashAlgorithmName.SHA256, 32);
        var wrongServerKey = HmacSha256(wrongSaltedPassword, Encoding.UTF8.GetBytes("Server Key"));

        var clientFinalWithoutProof = $"c=biws,r={serverNonce}";
        var authMessage = $"{clientFirstBare},{serverFirst},{clientFinalWithoutProof}";
        var wrongServerSig = HmacSha256(wrongServerKey, Encoding.UTF8.GetBytes(authMessage));

        var serverFinal = $"v={Convert.ToBase64String(wrongServerSig)}";

        Assert.False(InvokeValidateServerFinal(client, serverFinal));
    }

    // ---- Test 7: CreateClientFinal rejects mismatched nonce --------------

    [Fact]
    public void CreateClientFinal_RejectsMismatchedNonce()
    {
        var client = CreateScramClient();
        InvokeCreateClientFirst(client, "admin");

        // Server sends a combined nonce that does NOT start with the client nonce
        var salt = new byte[16];
        var saltB64 = Convert.ToBase64String(salt);
        var serverFirst = $"r=COMPLETELY_DIFFERENT_NONCE,s={saltB64},i=4096";

        var ex = Assert.ThrowsAny<Exception>(() =>
            InvokeCreateClientFinal(client, serverFirst, "pass"));

        // The inner exception should mention nonce
        var msg = ex.InnerException?.Message ?? ex.Message;
        Assert.Contains("nonce", msg, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Test 8: ValidateServerFinal returns false before handshake ------

    [Fact]
    public void ValidateServerFinal_ReturnsFalseWithoutPriorHandshake()
    {
        var client = CreateScramClient();
        // No CreateClientFirst / CreateClientFinal called
        Assert.False(InvokeValidateServerFinal(client, "v=AAAA"));
    }

    // ---- Test 9: GS2 header is always "n,," (no channel binding) ---------

    [Fact]
    public void CreateClientFirst_Gs2Header_IsNoBind()
    {
        var client = CreateScramClient();
        var msg = InvokeCreateClientFirst(client, "any_user");

        // "n,," means: no channel binding, no authzid
        Assert.True(msg.StartsWith("n,,"), $"Expected GS2 header 'n,,', got: {msg[..Math.Min(5, msg.Length)]}");

        // "c=biws" in client-final is base64("n,,") = "biws"
        Assert.Equal("biws", Convert.ToBase64String(Encoding.UTF8.GetBytes("n,,")));
    }

    // ---- Test 10: Full SCRAM round-trip with known test vector -----------

    [Fact]
    public void FullRoundTrip_WithKnownValues()
    {
        // Simulate a complete SCRAM-SHA-256 exchange and verify consistency.
        var password = "hunter2";
        var salt = Encoding.UTF8.GetBytes("saltysaltysalty!!");
        var iterations = 4096;

        var client = CreateScramClient();
        var clientFirst = InvokeCreateClientFirst(client, "user1");
        var clientFirstBare = clientFirst[3..];
        var clientNonce = clientFirstBare.Split(",r=")[1];

        var combinedNonce = clientNonce + "server_random_part";
        var serverFirst = $"r={combinedNonce},s={Convert.ToBase64String(salt)},i={iterations}";

        var clientFinal = InvokeCreateClientFinal(client, serverFirst, password);

        // Verify the client-final message can be parsed
        Assert.StartsWith("c=biws,", clientFinal);
        var parts = clientFinal.Split(',');
        Assert.Equal(3, parts.Length);
        Assert.Equal("c=biws", parts[0]);
        Assert.Equal($"r={combinedNonce}", parts[1]);
        Assert.StartsWith("p=", parts[2]);

        // Compute server signature for validation
        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
        var serverKey = HmacSha256(saltedPassword, Encoding.UTF8.GetBytes("Server Key"));
        var clientFinalWithoutProof = $"c=biws,r={combinedNonce}";
        var authMessage = $"{clientFirstBare},{serverFirst},{clientFinalWithoutProof}";
        var serverSig = HmacSha256(serverKey, Encoding.UTF8.GetBytes(authMessage));

        var serverFinal = $"v={Convert.ToBase64String(serverSig)}";
        Assert.True(InvokeValidateServerFinal(client, serverFinal));

        // Verify server also would accept the client proof
        var clientKey = HmacSha256(saltedPassword, Encoding.UTF8.GetBytes("Client Key"));
        var storedKey = SHA256.HashData(clientKey);
        var clientSig = HmacSha256(storedKey, Encoding.UTF8.GetBytes(authMessage));
        var proofB64 = parts[2][2..]; // strip "p="
        var proof = Convert.FromBase64String(proofB64);

        // Recover ClientKey from proof: ClientKey = ClientProof XOR ClientSignature
        var recoveredClientKey = new byte[32];
        for (int i = 0; i < 32; i++)
            recoveredClientKey[i] = (byte)(proof[i] ^ clientSig[i]);

        // SHA-256(recovered ClientKey) should equal StoredKey
        var recoveredStoredKey = SHA256.HashData(recoveredClientKey);
        Assert.Equal(storedKey, recoveredStoredKey);
    }

    // ---- Helpers ---------------------------------------------------------

    private static byte[] HmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }
}

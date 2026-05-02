using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    ///     Manages Google API authentication for all three auth modes.
    ///     <b>API Key:</b> Returns <c>null</c> — the caller appends <c>?key=…</c> to the URL.
    ///     <b>OAuth 2.0:</b>
    ///     1. User clicks "Sign in with Google" → <see cref="StartOAuthFlowAsync" /> opens the browser.
    ///     2. After the user grants access, <see cref="GoogleSheetsOAuthServer" /> captures the code.
    ///     3. The code is exchanged for an access + refresh token via the Google token endpoint.
    ///     4. The refresh token is stored in <see cref="EditorPrefs" />; the access token is cached
    ///     in memory for 55 minutes and silently refreshed when needed.
    ///     <b>Service Account (legacy):</b>
    ///     Builds an RS256-signed JWT and exchanges it for a short-lived access token.
    ///     Includes a pure-C# PKCS#8 DER fallback parser for Unity Mono compatibility.
    /// </summary>
    public static class GoogleSheetsAuthService
    {
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string OAuthAuthBaseUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string SheetsScope = "https://www.googleapis.com/auth/spreadsheets";

        private const string RefreshPrefPrefix = "LiveGameDataEditor.OAuthRefresh.";

        // In-memory access token cache: configGuid → (token, expiresAt)
        private static readonly Dictionary<string, (string token, DateTime expiresAt)> _tokenCache = new();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        ///     Returns the value for the HTTP <c>Authorization</c> header (e.g. "Bearer ya29.…"),
        ///     or <c>null</c> when API Key mode is used.
        ///     Throws <see cref="GoogleSheetsAuthException" /> on failure.
        /// </summary>
        public static async Task<string> GetAuthHeaderAsync(GoogleSheetsConfig config)
        {
            if (config.AuthMode == GoogleSheetsAuthMode.ApiKey) return null;

            if (config.AuthMode == GoogleSheetsAuthMode.OAuth)
            {
                var oauthToken = await GetOAuthAccessTokenAsync(config);
                return "Bearer " + oauthToken;
            }

            // Service Account (legacy)
            var saToken = await GetServiceAccountAccessTokenAsync(config);
            return "Bearer " + saToken;
        }

        /// <summary>
        ///     Returns true when a stored refresh token or valid cached access token exists
        ///     for this config (i.e. the user has previously signed in and tokens have not expired).
        /// </summary>
        public static bool IsOAuthAuthenticated(GoogleSheetsConfig config)
        {
            if (config == null) return false;
            var guid = GetConfigGuid(config);
            if (TryGetCachedToken(guid, out _)) return true;
            return !string.IsNullOrEmpty(EditorPrefs.GetString(RefreshPrefPrefix + guid, ""));
        }

        /// <summary>
        ///     Triggers the full OAuth 2.0 "installed app" browser flow:
        ///     1. Starts a local callback server.
        ///     2. Opens the system browser at the Google consent URL.
        ///     3. Waits for the redirect with the authorization code.
        ///     4. Exchanges the code for access + refresh tokens.
        ///     5. Caches and persists the tokens.
        /// </summary>
        public static async Task StartOAuthFlowAsync(GoogleSheetsConfig config, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(config.OAuthClientId))
                throw new GoogleSheetsAuthException(
                    "OAuth Client ID is not set.\n" +
                    "Fill in GoogleSheetsConfig.OAuthClientId before signing in.");

            var port = GoogleSheetsOAuthServer.FindAvailablePort();
            var redirectUri = $"http://localhost:{port}/";
            var authUrl = BuildOAuthUrl(config.OAuthClientId, redirectUri);

            // Application.OpenURL must run on the Unity main thread (already the case for button clicks).
            Application.OpenURL(authUrl);

            // WaitForCodeAsync blocks on a background thread until the browser redirect arrives.
            var code = await GoogleSheetsOAuthServer.WaitForCodeAsync(port, ct);

            // Exchange the code for tokens — UnityWebRequest is marshalled to the main thread internally.
            var tokens = await ExchangeCodeForTokensAsync(config, code, redirectUri);

            var guid = GetConfigGuid(config);
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
                EditorPrefs.SetString(RefreshPrefPrefix + guid, tokens.RefreshToken);
            _tokenCache[guid] = (tokens.AccessToken, DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 60));
        }

        /// <summary>Signs out: clears the in-memory token and the persisted refresh token.</summary>
        public static void SignOut(GoogleSheetsConfig config)
        {
            if (config == null) return;
            var guid = GetConfigGuid(config);
            _tokenCache.Remove(guid);
            EditorPrefs.DeleteKey(RefreshPrefPrefix + guid);
        }

        /// <summary>Clears only the in-memory cache, forcing a refresh on the next request.</summary>
        public static void InvalidateCache(GoogleSheetsConfig config)
        {
            if (config == null) return;
            _tokenCache.Remove(GetConfigGuid(config));
        }

        // ── OAuth 2.0 ──────────────────────────────────────────────────────────

        private static async Task<string> GetOAuthAccessTokenAsync(GoogleSheetsConfig config)
        {
            var guid = GetConfigGuid(config);

            if (TryGetCachedToken(guid, out var cached)) return cached;

            // Attempt a silent refresh with the stored refresh token.
            var storedRefresh = EditorPrefs.GetString(RefreshPrefPrefix + guid, "");
            if (!string.IsNullOrEmpty(storedRefresh))
                try
                {
                    return await RefreshAccessTokenAsync(config, storedRefresh, guid);
                }
                catch (GoogleSheetsAuthException)
                {
                    // Refresh token expired or revoked — require re-auth.
                    EditorPrefs.DeleteKey(RefreshPrefPrefix + guid);
                }

            throw new GoogleSheetsAuthException(
                "Not signed in to Google.\n" +
                "Click 'Sign in with Google' in the Sheets panel to authenticate.");
        }

        private static async Task<string> RefreshAccessTokenAsync(
            GoogleSheetsConfig config, string refreshToken, string guid)
        {
            var body =
                "grant_type=refresh_token" +
                "&refresh_token=" + Uri.EscapeDataString(refreshToken) +
                "&client_id=" + Uri.EscapeDataString(config.OAuthClientId) +
                "&client_secret=" + Uri.EscapeDataString(config.OAuthClientSecret ?? "");

            var response = await PostFormAsync(TokenEndpoint, body);
            var json = JObject.Parse(response);

            var accessToken = (string)json["access_token"];
            if (string.IsNullOrEmpty(accessToken))
            {
                var err = (string)json["error"] ?? "unknown_error";
                var desc = (string)json["error_description"] ?? "";
                throw new GoogleSheetsAuthException($"Token refresh failed: {err} — {desc}");
            }

            var expiresIn = (int?)json["expires_in"] ?? 3600;
            _tokenCache[guid] = (accessToken, DateTime.UtcNow.AddSeconds(expiresIn - 60));
            return accessToken;
        }

        private static async Task<OAuthTokens> ExchangeCodeForTokensAsync(
            GoogleSheetsConfig config, string code, string redirectUri)
        {
            var body =
                "grant_type=authorization_code" +
                "&code=" + Uri.EscapeDataString(code) +
                "&client_id=" + Uri.EscapeDataString(config.OAuthClientId) +
                "&client_secret=" + Uri.EscapeDataString(config.OAuthClientSecret ?? "") +
                "&redirect_uri=" + Uri.EscapeDataString(redirectUri);

            var response = await PostFormAsync(TokenEndpoint, body);
            var json = JObject.Parse(response);

            var accessToken = (string)json["access_token"];
            var refreshToken = (string)json["refresh_token"];
            var expiresIn = (int?)json["expires_in"] ?? 3600;

            if (string.IsNullOrEmpty(accessToken))
            {
                var err = (string)json["error"] ?? "unknown_error";
                var desc = (string)json["error_description"] ?? "";
                throw new GoogleSheetsAuthException(
                    $"Code exchange failed: {err} — {desc}\n" +
                    "Check that your Client ID and Client Secret are correct.");
            }

            return new OAuthTokens(accessToken, refreshToken, expiresIn);
        }

        private static string BuildOAuthUrl(string clientId, string redirectUri)
        {
            return OAuthAuthBaseUrl +
                   "?client_id=" + Uri.EscapeDataString(clientId) +
                   "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                   "&response_type=code" +
                   "&scope=" + Uri.EscapeDataString(SheetsScope) +
                   "&access_type=offline" +
                   "&prompt=consent"; // Always request consent to ensure a refresh token is returned.
        }

        // ── Service Account (legacy) ───────────────────────────────────────────

        private static async Task<string> GetServiceAccountAccessTokenAsync(GoogleSheetsConfig config)
        {
            var guid = GetConfigGuid(config);
            if (TryGetCachedToken(guid, out var cached)) return cached;
            var token = await FetchServiceAccountTokenAsync(config);
            _tokenCache[guid] = (token, DateTime.UtcNow.AddMinutes(55));
            return token;
        }

        private static async Task<string> FetchServiceAccountTokenAsync(GoogleSheetsConfig config)
        {
            var jsonPath = config.ServiceAccountJsonPath;
            if (!Path.IsPathRooted(jsonPath)) jsonPath = Path.Combine(Application.dataPath, "..", jsonPath);
            jsonPath = Path.GetFullPath(jsonPath);

            if (!File.Exists(jsonPath))
                throw new GoogleSheetsAuthException(
                    $"Service account JSON key file not found: {jsonPath}\n" +
                    "Check GoogleSheetsConfig.ServiceAccountJsonPath.");

            var keyJson = File.ReadAllText(jsonPath);
            JObject keyObj;
            try
            {
                keyObj = JObject.Parse(keyJson);
            }
            catch (Exception ex)
            {
                throw new GoogleSheetsAuthException($"Failed to parse service account JSON: {ex.Message}");
            }

            var clientEmail = (string)keyObj["client_email"];
            var privateKeyPem = (string)keyObj["private_key"];

            if (string.IsNullOrEmpty(clientEmail) || string.IsNullOrEmpty(privateKeyPem))
                throw new GoogleSheetsAuthException(
                    "Service account JSON is missing 'client_email' or 'private_key'.");

            var jwt = BuildJwt(clientEmail, privateKeyPem);
            var token = await ExchangeJwtForTokenAsync(jwt);
            return token;
        }

        private static string BuildJwt(string clientEmail, string privateKeyPem)
        {
            var headerJson = JsonConvert.SerializeObject(new { alg = "RS256", typ = "JWT" });
            var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var claimsJson = JsonConvert.SerializeObject(new
            {
                iss = clientEmail,
                scope = SheetsScope,
                aud = TokenEndpoint,
                exp = now + 3600,
                iat = now
            });
            var claimsB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(claimsJson));

            var signingInput = headerB64 + "." + claimsB64;
            var signature = SignWithPkcs8Pem(signingInput, privateKeyPem);
            return signingInput + "." + Base64UrlEncode(signature);
        }

        private static byte[] SignWithPkcs8Pem(string input, string pem)
        {
            var stripped = pem
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                .Replace("-----END RSA PRIVATE KEY-----", "")
                .Replace("\n", "").Replace("\r", "").Replace(" ", "");

            var der = Convert.FromBase64String(stripped);

            RSAParameters rsaParams;
            try
            {
                // Try the platform-native import first (works on .NET 5+ / IL2CPP).
                using var rsaTry = RSA.Create();
                rsaTry.ImportPkcs8PrivateKey(der, out _);
                rsaParams = rsaTry.ExportParameters(true);
            }
            catch
            {
                // Fall back to a manual ASN.1 DER parser — works on all Mono versions.
                try
                {
                    rsaParams = ParsePkcs8RsaKey(der);
                }
                catch (Exception ex)
                {
                    throw new GoogleSheetsAuthException(
                        "Failed to import the RSA private key.\n" +
                        $"Underlying error: {ex.Message}");
                }
            }

            using var rsa = RSA.Create(rsaParams);
            return rsa.SignData(Encoding.UTF8.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        // ── PKCS#8 / ASN.1 DER parser ─────────────────────────────────────────
        // Parses PKCS#8 → PKCS#1 → RSAParameters without any external library,
        // providing full compatibility with Unity's Mono runtime.

        private static RSAParameters ParsePkcs8RsaKey(byte[] pkcs8Der)
        {
            // PKCS#8 PrivateKeyInfo structure:
            //   SEQUENCE { version INTEGER, algorithm SEQUENCE { OID, NULL }, privateKey OCTET STRING { ... } }
            var offset = 0;
            ConsumeTag(pkcs8Der, ref offset, 0x30); // outer SEQUENCE
            ConsumeLength(pkcs8Der, ref offset);
            ConsumeTag(pkcs8Der, ref offset, 0x02); // version INTEGER
            var vLen = ConsumeLength(pkcs8Der, ref offset);
            offset += vLen;
            ConsumeTag(pkcs8Der, ref offset, 0x30); // algorithm SEQUENCE
            var algLen = ConsumeLength(pkcs8Der, ref offset);
            offset += algLen;
            ConsumeTag(pkcs8Der, ref offset, 0x04); // privateKey OCTET STRING
            ConsumeLength(pkcs8Der, ref offset);

            return ParsePkcs1RsaKey(pkcs8Der, ref offset);
        }

        private static RSAParameters ParsePkcs1RsaKey(byte[] der, ref int offset)
        {
            // RSAPrivateKey ::= SEQUENCE { version, modulus, publicExp, privateExp, p, q, dp, dq, qInv }
            ConsumeTag(der, ref offset, 0x30);
            ConsumeLength(der, ref offset);
            ConsumeTag(der, ref offset, 0x02); // version INTEGER
            var vLen = ConsumeLength(der, ref offset);
            offset += vLen;

            var modulus = ReadDerInteger(der, ref offset);
            var exponent = ReadDerInteger(der, ref offset);
            var d = ReadDerInteger(der, ref offset);
            var p = ReadDerInteger(der, ref offset);
            var q = ReadDerInteger(der, ref offset);
            var dp = ReadDerInteger(der, ref offset);
            var dq = ReadDerInteger(der, ref offset);
            var inverseQ = ReadDerInteger(der, ref offset);

            var keyLen = modulus.Length;
            var halfLen = keyLen / 2;

            return new RSAParameters
            {
                Modulus = modulus,
                Exponent = exponent,
                D = PadLeft(d, keyLen),
                P = PadLeft(p, halfLen),
                Q = PadLeft(q, halfLen),
                DP = PadLeft(dp, halfLen),
                DQ = PadLeft(dq, halfLen),
                InverseQ = PadLeft(inverseQ, halfLen)
            };
        }

        private static byte[] ReadDerInteger(byte[] data, ref int offset)
        {
            ConsumeTag(data, ref offset, 0x02);
            var length = ConsumeLength(data, ref offset);
            // Strip the leading 0x00 sign byte present in positive DER integers.
            if (length > 0 && data[offset] == 0x00)
            {
                offset++;
                length--;
            }

            var value = new byte[length];
            Array.Copy(data, offset, value, 0, length);
            offset += length;
            return value;
        }

        private static void ConsumeTag(byte[] data, ref int offset, byte expectedTag)
        {
            if (data[offset] != expectedTag)
                throw new InvalidOperationException(
                    $"DER parse: expected tag 0x{expectedTag:X2} at offset {offset}, " +
                    $"got 0x{data[offset]:X2}.");
            offset++;
        }

        private static int ConsumeLength(byte[] data, ref int offset)
        {
            int first = data[offset++];
            if ((first & 0x80) == 0) return first;
            var numBytes = first & 0x7F;
            var length = 0;
            for (var i = 0; i < numBytes; i++) length = (length << 8) | data[offset++];
            return length;
        }

        private static byte[] PadLeft(byte[] data, int targetLength)
        {
            if (data.Length >= targetLength) return data;
            var padded = new byte[targetLength];
            Array.Copy(data, 0, padded, targetLength - data.Length, data.Length);
            return padded;
        }

        // ── HTTP helpers ───────────────────────────────────────────────────────

        /// <summary>
        ///     Posts a URL-encoded form body and returns the response text.
        ///     Schedules the <see cref="UnityWebRequest" /> on the Unity main thread via
        ///     <see cref="EditorApplication.delayCall" /> to satisfy Unity's threading requirement.
        /// </summary>
        private static Task<string> PostFormAsync(string url, string formBody)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(formBody);
            var tcs = new TaskCompletionSource<string>();

            EditorApplication.delayCall += () =>
            {
                var req = new UnityWebRequest(url, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(bodyBytes),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

                var op = req.SendWebRequest();
                op.completed += _ =>
                {
                    var body = req.downloadHandler?.text ?? "";
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        tcs.TrySetException(new GoogleSheetsAuthException(
                            $"HTTP {req.responseCode}: {req.error} — {body}"));
                        return;
                    }

                    tcs.TrySetResult(body);
                };
            };

            return tcs.Task;
        }

        private static async Task<string> ExchangeJwtForTokenAsync(string jwt)
        {
            var body =
                "grant_type=" + Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:jwt-bearer") +
                "&assertion=" + Uri.EscapeDataString(jwt);

            var response = await PostFormAsync(TokenEndpoint, body);
            var json = JObject.Parse(response);
            var token = (string)json["access_token"];

            if (string.IsNullOrEmpty(token))
                throw new GoogleSheetsAuthException(
                    "Token endpoint returned no access_token.\n" +
                    $"Response: {response}");
            return token;
        }

        // ── Utilities ──────────────────────────────────────────────────────────

        private static bool TryGetCachedToken(string guid, out string token)
        {
            if (_tokenCache.TryGetValue(guid, out var entry) && DateTime.UtcNow < entry.expiresAt)
            {
                token = entry.token;
                return true;
            }

            token = null;
            return false;
        }

        private static string GetConfigGuid(GoogleSheetsConfig config)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        // ── Value types ────────────────────────────────────────────────────────

        private readonly struct OAuthTokens
        {
            public string AccessToken { get; }
            public string RefreshToken { get; }
            public int ExpiresIn { get; }

            public OAuthTokens(string access, string refresh, int expiresIn)
            {
                AccessToken = access;
                RefreshToken = refresh;
                ExpiresIn = expiresIn;
            }
        }
    }

    /// <summary>Thrown when Google Sheets authentication fails.</summary>
    public sealed class GoogleSheetsAuthException : Exception
    {
        public GoogleSheetsAuthException(string message) : base(message)
        {
        }
    }
}
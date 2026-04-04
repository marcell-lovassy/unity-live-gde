using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    /// Manages Google API authentication for both API Key and Service Account modes.
    ///
    /// Service Account flow:
    ///   1. Read <c>client_email</c> + <c>private_key</c> from the JSON key file.
    ///   2. Build an RS256-signed JWT with the required Google OAuth2 claims.
    ///   3. Exchange the JWT for a short-lived access token at the Google token endpoint.
    ///   4. Cache the token in memory (keyed by config asset GUID); refresh when expired.
    ///
    /// API Key flow:
    ///   Returns <c>null</c> — the caller appends <c>?key=…</c> to the URL directly.
    /// </summary>
    public static class GoogleSheetsAuthService
    {
        // Cached tokens: assetGUID → (accessToken, expiresAt)
        private static readonly Dictionary<string, (string token, DateTime expiresAt)> _tokenCache
            = new Dictionary<string, (string, DateTime)>();

        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string SheetsScope   = "https://www.googleapis.com/auth/spreadsheets";

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the value for the HTTP <c>Authorization</c> header (e.g. "Bearer ya29.…"),
        /// or <c>null</c> when API Key mode is used (the caller handles auth via query param).
        /// Throws <see cref="GoogleSheetsAuthException"/> on failure.
        /// </summary>
        public static async Task<string> GetAuthHeaderAsync(GoogleSheetsConfig config)
        {
            if (config.AuthMode == GoogleSheetsAuthMode.ApiKey)
            {
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
            if (TryGetCachedToken(guid, out string cached))
            {
                return "Bearer " + cached;
            }

            string token = await FetchServiceAccountTokenAsync(config);
            _tokenCache[guid] = (token, DateTime.UtcNow.AddMinutes(55)); // tokens last 60 min
            return "Bearer " + token;
        }

        /// <summary>Clears any cached access token for the given config (forces re-auth on next request).</summary>
        public static void InvalidateCache(GoogleSheetsConfig config)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
            _tokenCache.Remove(guid);
        }

        // ── Token cache ────────────────────────────────────────────────────────

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

        // ── Service Account JWT + token exchange ───────────────────────────────

        private static async Task<string> FetchServiceAccountTokenAsync(GoogleSheetsConfig config)
        {
            // 1. Load and parse the service account JSON key file.
            string jsonPath = config.ServiceAccountJsonPath;
            if (!System.IO.Path.IsPathRooted(jsonPath))
            {
                jsonPath = System.IO.Path.Combine(Application.dataPath, "..", jsonPath);
            }
            jsonPath = System.IO.Path.GetFullPath(jsonPath);

            if (!System.IO.File.Exists(jsonPath))
            {
                throw new GoogleSheetsAuthException(
                    $"Service account JSON key file not found: {jsonPath}\n" +
                    "Check GoogleSheetsConfig.ServiceAccountJsonPath.");
            }

            string keyJson = System.IO.File.ReadAllText(jsonPath);
            JObject keyObj;
            try
            {
                keyObj = JObject.Parse(keyJson);
            }
            catch (Exception ex)
            {
                throw new GoogleSheetsAuthException($"Failed to parse service account JSON: {ex.Message}");
            }

            string clientEmail = (string)keyObj["client_email"];
            string privateKeyPem = (string)keyObj["private_key"];

            if (string.IsNullOrEmpty(clientEmail) || string.IsNullOrEmpty(privateKeyPem))
            {
                throw new GoogleSheetsAuthException(
                    "Service account JSON is missing 'client_email' or 'private_key'. " +
                    "Make sure you downloaded the correct key type (JSON).");
            }

            // 2. Build and sign the JWT.
            string jwt = BuildJwt(clientEmail, privateKeyPem);

            // 3. Exchange the JWT for an access token.
            string token = await ExchangeJwtForTokenAsync(jwt);
            return token;
        }

        private static string BuildJwt(string clientEmail, string privateKeyPem)
        {
            // Header
            string headerJson  = JsonConvert.SerializeObject(new { alg = "RS256", typ = "JWT" });
            string headerB64   = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

            // Claims
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string claimsJson = JsonConvert.SerializeObject(new
            {
                iss   = clientEmail,
                scope = SheetsScope,
                aud   = TokenEndpoint,
                exp   = now + 3600,
                iat   = now
            });
            string claimsB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(claimsJson));

            string signingInput = headerB64 + "." + claimsB64;

            // Sign with RSA-SHA256 (PKCS1 padding)
            byte[] signature = SignWithPkcs8Pem(signingInput, privateKeyPem);
            string sigB64    = Base64UrlEncode(signature);

            return signingInput + "." + sigB64;
        }

        private static byte[] SignWithPkcs8Pem(string input, string pem)
        {
            // Strip PEM headers and decode to DER bytes.
            string stripped = pem
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                .Replace("-----END RSA PRIVATE KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(" ", "");

            byte[] der = Convert.FromBase64String(stripped);

            using RSA rsa = RSA.Create();
            try
            {
                // Service account keys use PKCS8 ("BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(der, out _);
            }
            catch (Exception ex)
            {
                throw new GoogleSheetsAuthException(
                    "Failed to import the RSA private key.\n" +
                    "This requires .NET Standard 2.1 with RSA.ImportPkcs8PrivateKey support.\n" +
                    $"Underlying error: {ex.Message}\n\n" +
                    "Workaround: use API Key mode with a public sheet instead.");
            }

            byte[] data = Encoding.UTF8.GetBytes(input);
            return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        private static async Task<string> ExchangeJwtForTokenAsync(string jwt)
        {
            string body =
                "grant_type=" + Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:jwt-bearer") +
                "&assertion=" + Uri.EscapeDataString(jwt);

            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            // UnityWebRequest must run on the main thread; we use a TaskCompletionSource
            // to bridge the async gap cleanly.
            var tcs = new TaskCompletionSource<string>();

            // Schedule on the main thread (this method is already on main thread in editor context).
            EditorApplication.delayCall += () =>
            {
                var req = new UnityEngine.Networking.UnityWebRequest(TokenEndpoint, "POST")
                {
                    uploadHandler   = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes),
                    downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer()
                };
                req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

                var op = req.SendWebRequest();
                op.completed += _ =>
                {
                    if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        tcs.TrySetException(new GoogleSheetsAuthException(
                            $"Token exchange failed ({req.responseCode}): {req.error}\n" +
                            $"Response: {req.downloadHandler.text}"));
                        return;
                    }

                    try
                    {
                        var json        = JObject.Parse(req.downloadHandler.text);
                        string token    = (string)json["access_token"];
                        if (string.IsNullOrEmpty(token))
                        {
                            tcs.TrySetException(new GoogleSheetsAuthException(
                                "Token endpoint returned no access_token.\n" +
                                $"Response: {req.downloadHandler.text}"));
                            return;
                        }
                        tcs.TrySetResult(token);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(new GoogleSheetsAuthException(
                            $"Failed to parse token response: {ex.Message}"));
                    }
                };
            };

            return await tcs.Task;
        }

        // ── Utilities ──────────────────────────────────────────────────────────

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }

    /// <summary>Thrown when Google Sheets authentication fails.</summary>
    public sealed class GoogleSheetsAuthException : Exception
    {
        public GoogleSheetsAuthException(string message) : base(message) { }
    }
}

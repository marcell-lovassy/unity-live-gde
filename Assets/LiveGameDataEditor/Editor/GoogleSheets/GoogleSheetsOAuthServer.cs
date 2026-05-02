using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    ///     Temporary local HTTP server that receives the Google OAuth 2.0 redirect callback
    ///     and extracts the authorization code from the URL query string.
    ///     Flow:
    ///     1. Caller obtains a port via <see cref="FindAvailablePort" /> and builds an auth URL
    ///     with <c>redirect_uri=http://localhost:{port}/</c>.
    ///     2. Browser opens the auth URL; user grants access.
    ///     3. Google redirects to <c>http://localhost:{port}/?code=AUTH_CODE</c>.
    ///     4. <see cref="WaitForCodeAsync" /> captures the code and returns it to the caller.
    /// </summary>
    internal static class GoogleSheetsOAuthServer
    {
        internal const int DefaultPort = 4242;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        ///     Starts a local <see cref="HttpListener" /> on <paramref name="port" />, waits for
        ///     the OAuth redirect, and returns the authorization code.
        ///     Throws <see cref="GoogleSheetsAuthException" /> on error or denial.
        ///     Throws <see cref="OperationCanceledException" /> when <paramref name="ct" /> fires.
        /// </summary>
        internal static async Task<string> WaitForCodeAsync(int port, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<string>();
            var listener = new HttpListener();

            listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                throw new GoogleSheetsAuthException(
                    $"Could not start local OAuth callback server on port {port}.\n" +
                    $"Check that nothing else is using that port.\n{ex.Message}");
            }

            // Stop the listener when cancellation is requested.
            var reg = ct.Register(() =>
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                    /* already stopped */
                }

                tcs.TrySetCanceled();
            });

            // Accept exactly one request on a thread-pool thread.
            _ = Task.Run(async () =>
            {
                try
                {
                    var ctx = await listener.GetContextAsync();

                    var code = ctx.Request.QueryString["code"];
                    var error = ctx.Request.QueryString["error"];

                    // Send a friendly page back to the browser.
                    var success = string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(code);
                    var html = success
                        ? BuildHtmlPage("✓ Authenticated!", "You can close this tab and return to Unity.")
                        : BuildHtmlPage("✗ Authentication failed",
                            WebUtility.HtmlEncode(error ?? "No authorization code received."));

                    var htmlBytes = Encoding.UTF8.GetBytes(html);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = htmlBytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(htmlBytes, 0, htmlBytes.Length, CancellationToken.None);
                    ctx.Response.Close();

                    if (!string.IsNullOrEmpty(error))
                        tcs.TrySetException(new GoogleSheetsAuthException(
                            $"Google OAuth denied: {error}"));
                    else if (string.IsNullOrEmpty(code))
                        tcs.TrySetException(new GoogleSheetsAuthException(
                            "OAuth callback received but contained no authorization code."));
                    else
                        tcs.TrySetResult(code);
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped by the cancellation registration — normal path.
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    reg.Dispose();
                    try
                    {
                        listener.Stop();
                    }
                    catch
                    {
                        /* already stopped */
                    }
                }
            }, CancellationToken.None);

            return await tcs.Task;
        }

        /// <summary>
        ///     Returns a free port to listen on, preferring <see cref="DefaultPort" />.
        /// </summary>
        internal static int FindAvailablePort()
        {
            if (IsPortAvailable(DefaultPort)) return DefaultPort;

            // Ask the OS for any free port.
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
            return port;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static bool IsPortAvailable(int port)
        {
            try
            {
                using var testListener = new HttpListener();
                testListener.Prefixes.Add($"http://localhost:{port}/");
                testListener.Start();
                testListener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildHtmlPage(string title, string message)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>{title}</title>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
           display: flex; align-items: center; justify-content: center;
           min-height: 100vh; margin: 0; background: #1a1a2e; color: #eee; }}
    .card {{ background: #16213e; padding: 48px 64px; border-radius: 14px; text-align: center;
             box-shadow: 0 8px 32px rgba(0,0,0,0.4); }}
    h1 {{ margin: 0 0 12px; font-size: 26px; }}
    p  {{ margin: 0; color: #9aabbc; font-size: 15px; }}
  </style>
</head>
<body>
  <div class='card'>
    <h1>{title}</h1>
    <p>{message}</p>
  </div>
</body>
</html>";
        }
    }
}
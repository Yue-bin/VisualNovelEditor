using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VNEditor.Services;

public sealed class GitHubDeviceAuthorizationStart
{
    public required string DeviceCode { get; init; }
    public required string UserCode { get; init; }
    public required string VerificationUri { get; init; }
    public int ExpiresInSeconds { get; init; }
    public int IntervalSeconds { get; init; }
}

/// <summary><see cref="GitHubDeviceOAuthService.RequestDeviceCodeAsync"/> 的结果：成功含 <see cref="Start"/>，失败含 <see cref="ErrorMessage"/>（含 GitHub 返回的 error / error_description）。</summary>
public sealed class GitHubDeviceCodeRequestResult
{
    public GitHubDeviceAuthorizationStart? Start { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>GitHub OAuth Device Flow：打开浏览器在 github.com/login/device 输入设备码并完成授权。</summary>
public static class GitHubDeviceOAuthService
{
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";
    private const string UserApiUrl = "https://api.github.com/user";

    public static async Task<GitHubDeviceCodeRequestResult> RequestDeviceCodeAsync(string clientId, CancellationToken cancellationToken)
    {
        using var http = CreateHttp();
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["scope"] = "repo read:user"
        });

        using var resp = await http.PostAsync(DeviceCodeUrl, body, cancellationToken).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            return new GitHubDeviceCodeRequestResult
            {
                ErrorMessage = FormatDeviceCodeHttpError(resp.StatusCode, text)
            };
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return new GitHubDeviceCodeRequestResult
            {
                ErrorMessage = $"响应不是合法 JSON（HTTP {(int)resp.StatusCode}）。"
            };
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var errProp))
            {
                return new GitHubDeviceCodeRequestResult
                {
                    ErrorMessage = FormatOAuthErrorJson(root)
                };
            }

            if (!root.TryGetProperty("device_code", out var dc) || !root.TryGetProperty("user_code", out var uc)
                || !root.TryGetProperty("verification_uri", out var vu))
            {
                return new GitHubDeviceCodeRequestResult
                {
                    ErrorMessage = "响应缺少 device_code、user_code 或 verification_uri。"
                };
            }

            var interval = root.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5;
            var expires = root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 900;

            return new GitHubDeviceCodeRequestResult
            {
                Start = new GitHubDeviceAuthorizationStart
                {
                    DeviceCode = dc.GetString() ?? string.Empty,
                    UserCode = uc.GetString() ?? string.Empty,
                    VerificationUri = vu.GetString() ?? "https://github.com/login/device",
                    ExpiresInSeconds = expires,
                    IntervalSeconds = Math.Max(5, interval)
                }
            };
        }
    }

    private static string FormatDeviceCodeHttpError(System.Net.HttpStatusCode status, string body)
    {
        var oauth = TryParseOAuthErrorBody(body);
        if (!string.IsNullOrEmpty(oauth))
        {
            return oauth;
        }

        var trimmed = body.Trim();
        if (trimmed.Length > 400)
        {
            trimmed = trimmed.Substring(0, 400) + "…";
        }

        return $"HTTP {(int)status}: {trimmed}";
    }

    private static string FormatOAuthErrorJson(JsonElement root)
    {
        var code = root.TryGetProperty("error", out var e) ? e.GetString() : null;
        var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;
        if (!string.IsNullOrEmpty(desc))
        {
            return string.IsNullOrEmpty(code) ? desc : $"{code}: {desc}";
        }

        return string.IsNullOrEmpty(code) ? "未知 OAuth 错误。" : code;
    }

    private static string? TryParseOAuthErrorBody(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("error", out _))
            {
                return null;
            }

            return FormatOAuthErrorJson(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>轮询直到用户完成授权或超时。</summary>
    public static async Task<string?> PollAccessTokenAsync(
        string clientId,
        string deviceCode,
        int intervalSeconds,
        CancellationToken cancellationToken)
    {
        using var http = CreateHttp();
        var delay = TimeSpan.FromSeconds(intervalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            });

            using var resp = await http.PostAsync(AccessTokenUrl, body, cancellationToken).ConfigureAwait(false);
            var text = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var at))
            {
                return at.GetString();
            }

            if (root.TryGetProperty("error", out var err))
            {
                var code = err.GetString() ?? string.Empty;
                if (code.Equals("authorization_pending", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (code.Equals("slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    delay = TimeSpan.FromSeconds(Math.Min(60, intervalSeconds + 5));
                    continue;
                }

                return null;
            }
        }

        return null;
    }

    public static async Task<string?> GetGitHubLoginAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VNEditor/1.0");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await http.GetAsync(UserApiUrl, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        var text = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("login", out var login) ? login.GetString() : null;
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VNEditor/1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }
}

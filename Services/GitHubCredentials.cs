namespace VNEditor.Services;

/// <summary>用于 HTTPS 远程（含 github.com）的 Basic 认证：用户名 + PAT。</summary>
public sealed record GitHubCredentials(string UserName, string PersonalAccessToken);

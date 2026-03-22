using System;
using System.IO;
using System.Text.Json;

namespace VNEditor.Services;

/// <summary>持久化 GitHub 设备流 OAuth access token（%LocalAppData%\VNEditor）。</summary>
public static class GitHubDeviceTokenCache
{
    private sealed class FileDto
    {
        public string? AccessToken { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private static string CachePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VNEditor", "github-device-oauth.json");

    public static bool TryLoad(out GitHubCredentials? creds)
    {
        creds = null;
        try
        {
            if (!File.Exists(CachePath))
            {
                return false;
            }

            var json = File.ReadAllText(CachePath);
            var dto = JsonSerializer.Deserialize<FileDto>(json);
            var token = dto?.AccessToken?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            creds = new GitHubCredentials("x-access-token", token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(GitHubCredentials creds)
    {
        try
        {
            var dir = Path.GetDirectoryName(CachePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(new FileDto { AccessToken = creds.PersonalAccessToken }, JsonOptions);
            File.WriteAllText(CachePath, json);
        }
        catch
        {
            // ignore
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                File.Delete(CachePath);
            }
        }
        catch
        {
            // ignore
        }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NoChat.App.Logging;

namespace NoChat.App.Update;

public class UpdateInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public UpdateAsset[] Assets { get; set; } = [];
}

public class UpdateAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}

public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public string? ReleaseNotes { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/nipeone/nochat/releases/latest";
    private static readonly HttpClient _httpClient = new();

    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NoChat-App");
    }

    public static string GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        if (version == null) return "1.0.0";
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = GetCurrentVersion()
        };

        try
        {
            AppLogger.Info($"[更新] 检查更新: {GitHubApiUrl}");

            var response = await _httpClient.GetAsync(GitHubApiUrl);

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"HTTP {response.StatusCode}";
                AppLogger.Info($"[更新] 检查失败: {response.StatusCode}");
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json);

            if (updateInfo == null)
            {
                result.ErrorMessage = "解析响应失败";
                return result;
            }

            result.LatestVersion = updateInfo.TagName.TrimStart('v');

            // Compare versions
            if (CompareVersions(result.LatestVersion, result.CurrentVersion) > 0)
            {
                result.HasUpdate = true;
                result.ReleaseNotes = updateInfo.Body;

                // Find the appropriate asset for current platform
                var platformSuffix = GetPlatformSuffix();
                foreach (var asset in updateInfo.Assets)
                {
                    if (asset.Name.Contains(platformSuffix) || asset.Name.EndsWith(".zip") || asset.Name.EndsWith(".deb") || asset.Name.EndsWith(".rpm"))
                    {
                        result.DownloadUrl = asset.BrowserDownloadUrl;
                        break;
                    }
                }

                // Fallback: use the first asset
                if (string.IsNullOrEmpty(result.DownloadUrl) && updateInfo.Assets.Length > 0)
                {
                    result.DownloadUrl = updateInfo.Assets[0].BrowserDownloadUrl;
                }

                AppLogger.Info($"[更新] 发现新版本: {result.LatestVersion} > {result.CurrentVersion}");
            }
            else
            {
                AppLogger.Info($"[更新] 已是最新版本: {result.CurrentVersion}");
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            AppLogger.Error("[更新] 检查更新异常", ex);
        }

        return result;
    }

    public static async Task<string?> DownloadUpdateAsync(string url, IProgress<double>? progress = null)
    {
        try
        {
            AppLogger.Info($"[更新] 下载: {url}");

            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var fileName = Path.GetFileName(url);
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    progress?.Report((double)totalRead / totalBytes);
                }
            }

            AppLogger.Info($"[更新] 下载完成: {tempPath}");
            return tempPath;
        }
        catch (Exception ex)
        {
            AppLogger.Error("[更新] 下载更新异常", ex);
            return null;
        }
    }

    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.');
        var parts2 = v2.Split('.');

        for (var i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            var p1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
            var p2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;

            if (p1 > p2) return 1;
            if (p1 < p2) return -1;
        }

        return 0;
    }

    private static string GetPlatformSuffix()
    {
        if (OperatingSystem.IsWindows())
        {
            return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) && Environment.Is64BitOperatingSystem
                ? "win-x64" : "win-x86";
        }
        if (OperatingSystem.IsLinux())
        {
            return Environment.Is64BitOperatingSystem ? "linux-x64" : "linux-x86";
        }
        if (OperatingSystem.IsMacOS())
        {
            return Environment.Is64BitOperatingSystem ? "macos-x64" : "macos-arm64";
        }
        return "";
    }
}

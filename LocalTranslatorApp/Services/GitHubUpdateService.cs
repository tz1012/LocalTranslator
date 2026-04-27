using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using LocalTranslatorApp.Models;

namespace LocalTranslatorApp.Services;

public sealed class GitHubUpdateService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    public GitHubUpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LocalTranslatorUpdater/0.6");
    }

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public async Task<UpdateInfo?> CheckLatestAsync(AppSettings settings)
    {
        if (!settings.CheckForUpdates || string.IsNullOrWhiteSpace(settings.UpdateRepository))
        {
            return null;
        }

        var repository = NormalizeRepository(settings.UpdateRepository);
        if (string.IsNullOrWhiteSpace(repository))
        {
            throw new InvalidOperationException("GitHub repository must be written as owner/repository.");
        }

        var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(
            $"https://api.github.com/repos/{repository}/releases/latest");
        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
        {
            return null;
        }

        var latestVersion = ParseVersion(release.TagName);
        if (latestVersion <= CurrentVersion)
        {
            return null;
        }

        var asset = PickInstallerAsset(release.Assets, settings.UpdateAssetPattern);
        if (asset is null || string.IsNullOrWhiteSpace(asset.DownloadUrl))
        {
            throw new InvalidOperationException("A newer release was found, but no downloadable installer asset was available.");
        }

        return new UpdateInfo(
            latestVersion,
            release.TagName,
            release.Name ?? release.TagName,
            release.HtmlUrl ?? "",
            release.Body ?? "",
            asset.Name ?? "LocalTranslatorUpdate.exe",
            asset.DownloadUrl,
            asset.Size);
    }

    public async Task<string> DownloadInstallerAsync(UpdateInfo update)
    {
        var updateDirectory = Path.Combine(Path.GetTempPath(), "LocalTranslator", "Updates");
        Directory.CreateDirectory(updateDirectory);

        var safeName = string.Join("_", update.AssetName.Split(Path.GetInvalidFileNameChars()));
        var filePath = Path.Combine(updateDirectory, safeName);

        using var response = await _httpClient.GetAsync(update.DownloadUrl);
        response.EnsureSuccessStatusCode();

        await using var file = File.Create(filePath);
        await response.Content.CopyToAsync(file);

        return filePath;
    }

    private static GitHubAsset? PickInstallerAsset(IReadOnlyList<GitHubAsset>? assets, string pattern)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        var normalizedPattern = string.IsNullOrWhiteSpace(pattern) ? "Setup.exe" : pattern.Trim();
        return assets.FirstOrDefault(asset =>
                   asset.Name?.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase) == true) ??
               assets.FirstOrDefault(asset =>
                   asset.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true) ??
               assets.FirstOrDefault(asset =>
                   asset.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static Version ParseVersion(string tagName)
    {
        var text = tagName.Trim().TrimStart('v', 'V');
        var versionText = new string(text
            .TakeWhile(character => char.IsDigit(character) || character == '.')
            .ToArray());

        if (Version.TryParse(versionText, out var version))
        {
            return version;
        }

        throw new InvalidOperationException($"Could not parse release version from tag '{tagName}'. Use tags like v0.6.0.");
    }

    private static string NormalizeRepository(string value)
    {
        value = value.Trim();
        if (value.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            value = value["https://github.com/".Length..];
        }

        value = value.Trim('/');
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : value;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}

public sealed record UpdateInfo(
    Version Version,
    string TagName,
    string Title,
    string ReleaseUrl,
    string ReleaseNotes,
    string AssetName,
    string DownloadUrl,
    long Size);

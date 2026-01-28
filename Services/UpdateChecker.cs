using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace FT_AlarmFixer.Services;

public sealed class UpdateChecker
{
    private const string RepoOwner = "joshfromtessy";
    private const string RepoName = "FTView-Bit-Fixer";
    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
    private static readonly HttpClient HttpClient = new();

    public async Task<UpdateInfo?> CheckForUpdatesAsync(Version currentVersion, string cachePath, CancellationToken cancellationToken = default)
    {
        var cache = await ReadCacheAsync(cachePath, cancellationToken).ConfigureAwait(false);

        var release = await FetchLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (release is null)
        {
            if (cache is null)
            {
                return null;
            }
            return BuildUpdateFromCache(cache, currentVersion);
        }

        cache = new UpdateCache
        {
            LastCheckedUtc = DateTime.UtcNow,
            LatestVersion = release.Version,
            LatestUrl = release.Url
        };
        await WriteCacheAsync(cachePath, cache, cancellationToken).ConfigureAwait(false);

        return BuildUpdateFromRelease(release, currentVersion);
    }

    public static string GetDefaultCachePath()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FT_AlarmFixer");
        return Path.Combine(directory, "update.json");
    }

    private static UpdateInfo? BuildUpdateFromCache(UpdateCache cache, Version currentVersion)
    {
        if (cache.LatestVersion is null || cache.LatestUrl is null)
        {
            return null;
        }

        if (!TryParseVersion(cache.LatestVersion, out var latestVersion) || latestVersion is null)
        {
            return null;
        }

        if (latestVersion <= currentVersion)
        {
            return null;
        }

        return new UpdateInfo(cache.LatestVersion, cache.LatestUrl, latestVersion);
    }

    private static UpdateInfo? BuildUpdateFromRelease(ReleaseInfo release, Version currentVersion)
    {
        if (!TryParseVersion(release.Version, out var latestVersion) || latestVersion is null)
        {
            return null;
        }

        if (latestVersion <= currentVersion)
        {
            return null;
        }

        return new UpdateInfo(release.Version, release.Url, latestVersion);
    }

    private static bool TryParseVersion(string value, out Version? version)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        return Version.TryParse(trimmed, out version);
    }

    private static async Task<UpdateCache?> ReadCacheAsync(string cachePath, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(cachePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(cachePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<UpdateCache>(json);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(string cachePath, UpdateCache cache, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cachePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static async Task<ReleaseInfo?> FetchLatestReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
            request.Headers.UserAgent.ParseAdd("FT_AlarmFixer");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<ReleasePayload>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (payload is null || payload.TagName is null || payload.HtmlUrl is null)
            {
                return null;
            }

            return new ReleaseInfo(payload.TagName!, payload.HtmlUrl!);
        }
        catch
        {
            return null;
        }
    }

    private sealed record ReleaseInfo(string Version, string Url);

    private sealed record UpdateCache
    {
        public DateTime? LastCheckedUtc { get; init; }
        public string? LatestVersion { get; init; }
        public string? LatestUrl { get; init; }
    }

    private sealed record ReleasePayload
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }

    public sealed record UpdateInfo(string DisplayVersion, string Url, Version Version);
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

public sealed partial class ReleaseUpdateService : IReleaseUpdateService
{
    private const string LatestReleaseEndpoint = "https://api.github.com/repos/NelsonSantos/DataDeveloper/releases/latest";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDialogService _dialogService;
    private readonly HttpClient _httpClient;
    private readonly string _stateFilePath;
    private readonly string _currentVersion;
    private readonly Func<DateTimeOffset> _utcNow;

    public ReleaseUpdateService(IDialogService dialogService, AppDataFileService appDataFileService)
        : this(
            dialogService,
            CreateHttpClient(),
            appDataFileService.GetFullPath("release-update-state.json"),
            ApplicationVersionHelper.GetCurrentVersion(typeof(ReleaseUpdateService).Assembly),
            () => DateTimeOffset.UtcNow)
    {
    }

    public ReleaseUpdateService(
        IDialogService dialogService,
        HttpClient httpClient,
        string stateFilePath,
        string currentVersion,
        Func<DateTimeOffset>? utcNow = null)
    {
        _dialogService = dialogService;
        _httpClient = httpClient;
        _stateFilePath = stateFilePath;
        _currentVersion = currentVersion;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task NotifyIfUpdateAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = await GetAvailableUpdateAsync(cancellationToken);
            if (notification is null)
                return;

            var result = await _dialogService.ShowReleaseUpdateAsync(BuildMessage(notification), "New Version Available");
            if (result == DialogResult.Download && !string.IsNullOrWhiteSpace(notification.ReleaseUrl))
                await BrowserLauncher.OpenAsync(notification.ReleaseUrl);

            var state = LoadState();
            state.LastNotifiedTagName = notification.TagName;
            SaveState(state);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            // Silent failure: startup should not break on update-check errors.
        }
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = await GetAvailableUpdateAsync(cancellationToken);
            if (notification is null)
            {
                await _dialogService.ShowMessageAsync(
                    $"Data Developer is up to date.\n\nCurrent version: {_currentVersion}",
                    "Check for Updates");
                return;
            }

            var result = await _dialogService.ShowReleaseUpdateAsync(BuildMessage(notification), "New Version Available");
            if (result == DialogResult.Download && !string.IsNullOrWhiteSpace(notification.ReleaseUrl))
                await BrowserLauncher.OpenAsync(notification.ReleaseUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            await _dialogService.ShowMessageAsync(
                "Unable to check for updates right now.\n\nPlease try again in a moment.",
                "Check for Updates");
        }
    }

    public async Task<ReleaseUpdateNotification?> GetAvailableUpdateAsync(CancellationToken cancellationToken = default)
    {
        var state = LoadState();
        var now = _utcNow();

        if (state.CachedRelease is not null && now - state.LastCheckedUtc < CacheDuration)
             return BuildNotification(state.CachedRelease, state.LastNotifiedTagName);

        var latestRelease = await FetchLatestReleaseAsync(cancellationToken);
        state.LastCheckedUtc = now;
        state.CachedRelease = latestRelease;
        SaveState(state);

        return BuildNotification(latestRelease, state.LastNotifiedTagName);
    }

    private async Task<ReleaseUpdateRelease?> FetchLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(LatestReleaseEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(
            contentStream,
            SerializerOptions,
            cancellationToken);

        if (release is null || release.Draft || release.PreRelease || string.IsNullOrWhiteSpace(release.TagName))
            return null;

        return new ReleaseUpdateRelease
        {
            TagName = release.TagName,
            Name = release.Name,
            HtmlUrl = release.HtmlUrl,
            Body = release.Body
        };
    }

    private ReleaseUpdateNotification? BuildNotification(ReleaseUpdateRelease? release, string? lastNotifiedTagName)
    {
        if (release is null)
            return null;

        if (!IsNewerVersion(_currentVersion, release.TagName))
            return null;

        if (string.Equals(lastNotifiedTagName, release.TagName, StringComparison.OrdinalIgnoreCase))
            return null;

        return new ReleaseUpdateNotification
        {
            CurrentVersion = _currentVersion,
            LatestVersion = NormalizeVersionString(release.TagName),
            TagName = release.TagName,
            ReleaseName = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            ReleaseUrl = release.HtmlUrl,
            Summary = ExtractSummary(release.Body)
        };
    }

    private static string BuildMessage(ReleaseUpdateNotification notification)
    {
        var summary = string.IsNullOrWhiteSpace(notification.Summary)
            ? "No summary was provided for this release."
            : notification.Summary;

        return
            $"A new version of Data Developer is available.\n\n" +
            $"Current version: {notification.CurrentVersion}\n" +
            $"Latest version: {notification.LatestVersion}\n" +
            $"Release: {notification.ReleaseName}\n\n" +
            $"Summary\n{summary}";
    }

    public static string ExtractSummary(string? releaseBody)
    {
        if (string.IsNullOrWhiteSpace(releaseBody))
            return string.Empty;

        var normalized = releaseBody.Replace("\r\n", "\n", StringComparison.Ordinal);
        var summarySection = TryExtractSummarySection(normalized);
        if (!string.IsNullOrWhiteSpace(summarySection))
            return summarySection;

        var paragraphs = normalized
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.TrimStart().StartsWith('#'))
                continue;

            var stripped = StripMarkdown(paragraph);
            if (!string.IsNullOrWhiteSpace(stripped))
                return stripped.Length > 600 ? $"{stripped[..597]}..." : stripped;
        }

        return string.Empty;
    }

    private static string TryExtractSummarySection(string releaseBody)
    {
        var lines = releaseBody.Split('\n');
        var inSummary = false;
        var summaryLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (!inSummary)
            {
                if (IsSummaryHeading(trimmed))
                    inSummary = true;

                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (summaryLines.Count > 0)
                    summaryLines.Add(string.Empty);

                continue;
            }

            if (trimmed.StartsWith('#'))
                break;

            summaryLines.Add(StripMarkdownForSummary(line));
        }

        while (summaryLines.Count > 0 && string.IsNullOrWhiteSpace(summaryLines[^1]))
            summaryLines.RemoveAt(summaryLines.Count - 1);

        return string.Join('\n', summaryLines).Trim();
    }

    private static bool IsSummaryHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var heading = line.TrimStart('#').Trim();
        return heading.Equals("Summary", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNewerVersion(string currentVersion, string releaseVersion)
    {
        var normalizedCurrent = NormalizeVersionString(currentVersion);
        var normalizedRelease = NormalizeVersionString(releaseVersion);

        if (Version.TryParse(GetNumericVersion(normalizedCurrent), out var current)
            && Version.TryParse(GetNumericVersion(normalizedRelease), out var release))
        {
            var comparison = release.CompareTo(current);
            if (comparison != 0)
                return comparison > 0;

            var currentIsPrerelease = normalizedCurrent.Contains('-', StringComparison.Ordinal);
            var releaseIsPrerelease = normalizedRelease.Contains('-', StringComparison.Ordinal);
            return currentIsPrerelease && !releaseIsPrerelease;
        }

        return !string.Equals(normalizedCurrent, normalizedRelease, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersionString(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return string.Empty;

        var trimmed = version.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];

        var metadataSeparator = trimmed.IndexOf('+');
        return metadataSeparator >= 0 ? trimmed[..metadataSeparator] : trimmed;
    }

    private static string GetNumericVersion(string version)
    {
        var prereleaseSeparator = version.IndexOf('-');
        return prereleaseSeparator >= 0 ? version[..prereleaseSeparator] : version;
    }

    private static string StripMarkdown(string value)
    {
        var text = value.Trim();
        text = text.Replace("### ", string.Empty, StringComparison.Ordinal)
            .Replace("## ", string.Empty, StringComparison.Ordinal)
            .Replace("# ", string.Empty, StringComparison.Ordinal);
        text = MarkdownLinkRegex().Replace(text, "$1");
        text = MarkdownDecorationRegex().Replace(text, string.Empty);
        text = MarkdownListPrefixRegex().Replace(text, string.Empty);
        text = MultiWhitespaceRegex().Replace(text, " ").Trim();
        return text;
    }

    private static string StripMarkdownForSummary(string value)
    {
        var text = MarkdownLinkRegex().Replace(value.Trim(), "$1");
        text = MarkdownDecorationRegex().Replace(text, string.Empty);
        return text.Trim();
    }

    private UpdateCheckState LoadState()
    {
        if (!File.Exists(_stateFilePath))
            return new UpdateCheckState();

        var content = File.ReadAllText(_stateFilePath);
        return JsonSerializer.Deserialize<UpdateCheckState>(content, SerializerOptions) ?? new UpdateCheckState();
    }

    private void SaveState(UpdateCheckState state)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var content = JsonSerializer.Serialize(state, new JsonSerializerOptions(SerializerOptions) { WriteIndented = true });
        File.WriteAllText(_stateFilePath, content);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"DataDeveloper/{ApplicationVersionHelper.GetCurrentVersion(typeof(ReleaseUpdateService).Assembly)}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"[*_`>~]")]
    private static partial Regex MarkdownDecorationRegex();

    [GeneratedRegex(@"^\s*[-*]\s+", RegexOptions.Multiline)]
    private static partial Regex MarkdownListPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        public string? Name { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        public string? Body { get; init; }

        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; init; }
    }

    private sealed class UpdateCheckState
    {
        public DateTimeOffset LastCheckedUtc { get; set; }
        public string? LastNotifiedTagName { get; set; }
        public ReleaseUpdateRelease? CachedRelease { get; set; }
    }

    private sealed class ReleaseUpdateRelease
    {
        public string TagName { get; init; } = string.Empty;
        public string? Name { get; init; }
        public string? HtmlUrl { get; init; }
        public string? Body { get; init; }
    }

    public sealed class ReleaseUpdateNotification
    {
        public string CurrentVersion { get; init; } = string.Empty;
        public string LatestVersion { get; init; } = string.Empty;
        public string TagName { get; init; } = string.Empty;
        public string ReleaseName { get; init; } = string.Empty;
        public string? ReleaseUrl { get; init; }
        public string Summary { get; init; } = string.Empty;
    }
}

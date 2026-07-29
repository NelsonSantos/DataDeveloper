using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class ReleaseUpdateServiceTests
{
    [Fact]
    public async Task GetAvailableUpdateAsync_ReturnsNotificationWithReleaseSummary()
    {
        var stateFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            using var httpClient = new HttpClient(new StubHttpMessageHandler("""
            {
              "tag_name": "v1.2.0",
              "name": "Version 1.2.0",
              "html_url": "https://github.com/NelsonSantos/DataDeveloper/releases/tag/v1.2.0",
              "body": "## Summary\n\n- Added [release notes](https://example.com) for the new build.\n\n## Details\n\nExtra section.",
              "draft": false,
              "prerelease": false
            }
            """));

            var service = new ReleaseUpdateService(
                new RecordingDialogService(),
                httpClient,
                stateFilePath,
                "1.1.0",
                () => new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero));

            var notification = await service.GetAvailableUpdateAsync();

            Assert.NotNull(notification);
            Assert.Equal("1.2.0", notification.LatestVersion);
            Assert.Equal("- Added release notes for the new build.", notification.Summary);
        }
        finally
        {
            if (File.Exists(stateFilePath))
                File.Delete(stateFilePath);
        }
    }

    [Fact]
    public async Task GetAvailableUpdateAsync_ReturnsNullWhenReleaseIsNotNewer()
    {
        var stateFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            using var httpClient = new HttpClient(new StubHttpMessageHandler("""
            {
              "tag_name": "v1.2.0",
              "name": "Version 1.2.0",
              "html_url": "https://github.com/NelsonSantos/DataDeveloper/releases/tag/v1.2.0",
              "body": "Current release.",
              "draft": false,
              "prerelease": false
            }
            """));

            var service = new ReleaseUpdateService(
                new RecordingDialogService(),
                httpClient,
                stateFilePath,
                "1.2.0",
                () => new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero));

            var notification = await service.GetAvailableUpdateAsync();

            Assert.Null(notification);
        }
        finally
        {
            if (File.Exists(stateFilePath))
                File.Delete(stateFilePath);
        }
    }

    [Fact]
    public async Task NotifyIfUpdateAvailableAsync_ShowsDialogOnlyOncePerReleaseTag()
    {
        var stateFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            using var httpClient = new HttpClient(new StubHttpMessageHandler("""
            {
              "tag_name": "v1.3.0",
              "name": "Version 1.3.0",
              "html_url": "https://github.com/NelsonSantos/DataDeveloper/releases/tag/v1.3.0",
              "body": "New release summary.",
              "draft": false,
              "prerelease": false
            }
            """));
            var dialogService = new RecordingDialogService();
            var service = new ReleaseUpdateService(
                dialogService,
                httpClient,
                stateFilePath,
                "1.2.0",
                () => new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero));

            await service.NotifyIfUpdateAvailableAsync();
            await service.NotifyIfUpdateAvailableAsync();

            Assert.Single(dialogService.ReleaseUpdateMessages);
            Assert.Contains("New release summary.", dialogService.ReleaseUpdateMessages[0]);
            Assert.DoesNotContain("Release notes:", dialogService.ReleaseUpdateMessages[0]);
        }
        finally
        {
            if (File.Exists(stateFilePath))
                File.Delete(stateFilePath);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShowsUpToDateMessage_WhenNoNewReleaseExists()
    {
        var stateFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            using var httpClient = new HttpClient(new StubHttpMessageHandler("""
            {
              "tag_name": "v1.2.0",
              "name": "Version 1.2.0",
              "html_url": "https://github.com/NelsonSantos/DataDeveloper/releases/tag/v1.2.0",
              "body": "Current release.",
              "draft": false,
              "prerelease": false
            }
            """));
            var dialogService = new RecordingDialogService();
            var service = new ReleaseUpdateService(
                dialogService,
                httpClient,
                stateFilePath,
                "1.2.0",
                () => new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero));

            await service.CheckForUpdatesAsync();

            Assert.Single(dialogService.Messages);
            Assert.Contains("Data Developer is up to date.", dialogService.Messages[0]);
            Assert.Empty(dialogService.ReleaseUpdateMessages);
        }
        finally
        {
            if (File.Exists(stateFilePath))
                File.Delete(stateFilePath);
        }
    }

    [Fact]
    public void ExtractSummary_ReturnsSummarySectionWithoutReleaseDraftHeader()
    {
        const string releaseBody = """
        # Release Notes Draft

        - Created at: 2026-04-02T04:32:53.300Z
        - Commit: consolidated from abc123, def456
        - Source PR: #9 #11

        ## Summary

        - feature/oracle sqlite
        - feat: add oracle and sqlite connection support
        - feat: support oracle and sqlite execution workflows

        ## Details

        Extra details here.
        """;

        var summary = ReleaseUpdateService.ExtractSummary(releaseBody);

        Assert.Equal(
            """
            - feature/oracle sqlite
            - feat: add oracle and sqlite connection support
            - feat: support oracle and sqlite execution workflows
            """,
            summary);
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public List<string> Messages { get; } = [];
        public List<string> ReleaseUpdateMessages { get; } = [];
        public DialogResult ReleaseUpdateResult { get; set; } = DialogResult.Cancel;

        public Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info)
            => Task.FromResult(DialogResult.Ok);

        public Task<DialogResult> ShowDialogResult(string message, string? title = null)
            => Task.FromResult(DialogResult.Ok);

        public Task ShowMessageAsync(string message, string? title = null)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowAboutAsync(string version, Func<Task> checkForUpdatesAsync) => Task.CompletedTask;

        public Task<DialogResult> ShowReleaseUpdateAsync(string message, string? title = null)
        {
            ReleaseUpdateMessages.Add(message);
            return Task.FromResult(ReleaseUpdateResult);
        }

        public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null)
            => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenFileAsync(string? title = null)
            => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenDatabaseFileAsync(string? title = null)
            => Task.FromResult<string?>(null);

        public Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null)
            => Task.FromResult<string?>(null);

        public Task<string?> ShowSaveJsonFileDialogAsync(string? suggestedName = null, string? title = null)
            => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenJsonFileDialogAsync(string? title = null)
            => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenImportFileAsync(string? title = null)
            => Task.FromResult<string?>(null);
    }

    private sealed class StubHttpMessageHandler(string responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });
        }
    }
}

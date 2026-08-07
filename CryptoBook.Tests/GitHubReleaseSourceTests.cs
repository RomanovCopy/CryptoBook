using CryptoBook.DTO;
using CryptoBook.Services;

using System.Net;
using System.Net.Http;
using System.Text;

using Xunit;

namespace CryptoBook.Tests;

public sealed class GitHubReleaseSourceTests
{
    [Fact]
    public async Task GetLatestAsync_MapsStableGitHubRelease()
    {
        const string json = """
            {
              "tag_name": "v1.2.0",
              "html_url": "https://github.com/RomanovCopy/CryptoBook/releases/tag/v1.2.0",
              "name": "CryptoBook 1.2.0",
              "body": "Release notes",
              "draft": false,
              "prerelease": false,
              "published_at": "2026-08-07T08:00:00Z",
              "assets": [
                {
                  "name": "CryptoBook-Setup-1.2.0.exe",
                  "browser_download_url": "https://github.com/RomanovCopy/CryptoBook/releases/download/v1.2.0/CryptoBook-Setup-1.2.0.exe"
                },
                {
                  "name": "CryptoBook-win-x64.zip",
                  "browser_download_url": "https://github.com/RomanovCopy/CryptoBook/releases/download/v1.2.0/CryptoBook-win-x64.zip"
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        var source = new GitHubReleaseSource(client, Options());

        ApplicationRelease? release = await source.GetLatestAsync();

        Assert.NotNull(release);
        Assert.Equal("1.2.0", release.Version.ToString());
        Assert.Equal("CryptoBook 1.2.0", release.Name);
        Assert.Equal("Release notes", release.Notes);
        Assert.Equal(
            "https://github.com/RomanovCopy/CryptoBook/releases/tag/v1.2.0",
            release.ReleaseUri.AbsoluteUri);
        Assert.Equal(
            "https://github.com/RomanovCopy/CryptoBook/releases/download/v1.2.0/CryptoBook-Setup-1.2.0.exe",
            release.InstallerUri?.AbsoluteUri);
        Assert.Equal(
            "https://api.github.com/repos/RomanovCopy/CryptoBook/releases/latest",
            handler.LastRequestUri?.AbsoluteUri);
        Assert.Contains(
            "CryptoBook-UpdateChecker",
            handler.LastUserAgent ?? string.Empty);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetLatestAsync_IgnoresDraftsAndPreReleases(
        bool draft,
        bool preRelease)
    {
        string json = $$"""
            {
              "tag_name": "v2.0.0-beta.1",
              "html_url": "https://github.com/RomanovCopy/CryptoBook/releases/tag/v2.0.0-beta.1",
              "draft": {{draft.ToString().ToLowerInvariant()}},
              "prerelease": {{preRelease.ToString().ToLowerInvariant()}}
            }
            """;
        using var client = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        var source = new GitHubReleaseSource(client, Options());

        Assert.Null(await source.GetLatestAsync());
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsNullWhenRepositoryHasNoRelease()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        var source = new GitHubReleaseSource(client, Options());

        Assert.Null(await source.GetLatestAsync());
    }

    private static GitHubReleaseOptions Options() =>
        new("RomanovCopy", "CryptoBook");

    private sealed class StubHttpMessageHandler(HttpResponseMessage response):
        HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string? LastUserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastUserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(response);
        }
    }
}

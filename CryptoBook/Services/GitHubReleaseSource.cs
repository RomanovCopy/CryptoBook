using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoBook.Services
{
    public sealed class GitHubReleaseSource: IReleaseSource
    {
        private readonly HttpClient httpClient;
        private readonly GitHubReleaseOptions options;

        public GitHubReleaseSource(
            HttpClient httpClient,
            GitHubReleaseOptions options)
        {
            this.httpClient = httpClient ??
                throw new ArgumentNullException(nameof(httpClient));
            this.options = options ??
                throw new ArgumentNullException(nameof(options));

            if(string.IsNullOrWhiteSpace(options.Owner))
                throw new ArgumentException("GitHub owner is required.", nameof(options));
            if(string.IsNullOrWhiteSpace(options.Repository))
                throw new ArgumentException("GitHub repository is required.", nameof(options));
        }

        public async Task<ApplicationRelease?> GetLatestAsync(
            CancellationToken cancellationToken = default)
        {
            string path =
                $"repos/{Uri.EscapeDataString(options.Owner)}/" +
                $"{Uri.EscapeDataString(options.Repository)}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("CryptoBook-UpdateChecker/1.0");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if(response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            await using Stream content = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            GitHubReleaseResponse? release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(
                content,
                cancellationToken: cancellationToken);
            if(release is null || release.Draft || release.PreRelease)
                return null;
            if(!SemanticVersion.TryParse(release.TagName, out SemanticVersion? version) ||
               version is null)
            {
                throw new InvalidDataException(
                    $"GitHub release tag '{release.TagName}' is not a valid semantic version.");
            }
            if(!Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out Uri? releaseUri) ||
               releaseUri.Scheme != Uri.UriSchemeHttps ||
               !string.Equals(releaseUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("GitHub release URL is invalid.");
            }

            Uri? installerUri = null;
            string installerName = $"CryptoBook-Setup-{version}.exe";
            GitHubAsset? installer = release.Assets?
                .FirstOrDefault(asset => string.Equals(
                    asset.Name,
                    installerName,
                    StringComparison.OrdinalIgnoreCase));
            if(installer is not null)
            {
                if(!Uri.TryCreate(installer.BrowserDownloadUrl, UriKind.Absolute, out Uri? candidate) ||
                   candidate.Scheme != Uri.UriSchemeHttps ||
                   !string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("GitHub installer download URL is invalid.");
                }
                installerUri = candidate;
            }

            return new ApplicationRelease(
                version,
                string.IsNullOrWhiteSpace(release.Name)
                    ? $"CryptoBook {version}"
                    : release.Name,
                release.Body ?? string.Empty,
                releaseUri,
                release.PublishedAt)
            {
                InstallerUri = installerUri
            };
        }

        private sealed class GitHubReleaseResponse
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; init; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; init; }

            [JsonPropertyName("name")]
            public string? Name { get; init; }

            [JsonPropertyName("body")]
            public string? Body { get; init; }

            [JsonPropertyName("draft")]
            public bool Draft { get; init; }

            [JsonPropertyName("prerelease")]
            public bool PreRelease { get; init; }

            [JsonPropertyName("published_at")]
            public DateTimeOffset? PublishedAt { get; init; }

            [JsonPropertyName("assets")]
            public GitHubAsset[]? Assets { get; init; }
        }

        private sealed class GitHubAsset
        {
            [JsonPropertyName("name")]
            public string? Name { get; init; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; init; }
        }
    }
}

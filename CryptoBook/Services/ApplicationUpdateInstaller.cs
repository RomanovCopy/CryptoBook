using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Services
{
    public sealed class ApplicationUpdateInstaller: IApplicationUpdateInstaller
    {
        private readonly HttpClient httpClient;
        private readonly IFileLauncherService? fileLauncherService;
        private readonly string updatesDirectory;
        private readonly UnsignedReleasePolicy unsignedReleasePolicy;
        private readonly IAuthenticodeVerifier authenticodeVerifier;

        private const int MaximumChecksumManifestBytes = 64 * 1024;
        private const int MaximumSigningStatusBytes = 4 * 1024;
        private const string SignedReleaseStatus =
            "CryptoBook binaries in this release are signed with Authenticode.";
        private const string UnsignedReleaseStatus =
            "CryptoBook binaries in this release are not digitally signed.";

        public ApplicationUpdateInstaller(
            HttpClient httpClient,
            IFileLauncherService? fileLauncherService = null)
            : this(
                httpClient,
                fileLauncherService,
                UnsignedReleasePolicy.RequireAuthenticodeSignature)
        {
        }

        public ApplicationUpdateInstaller(
            HttpClient httpClient,
            IFileLauncherService? fileLauncherService,
            UnsignedReleasePolicy unsignedReleasePolicy)
            : this(
                httpClient,
                fileLauncherService,
                null,
                unsignedReleasePolicy,
                new WindowsAuthenticodeVerifier())
        {
        }

        internal ApplicationUpdateInstaller(
            HttpClient httpClient,
            IFileLauncherService? fileLauncherService,
            string? updatesDirectory)
            : this(
                httpClient,
                fileLauncherService,
                updatesDirectory,
                UnsignedReleasePolicy.RequireAuthenticodeSignature,
                new WindowsAuthenticodeVerifier())
        {
        }

        internal ApplicationUpdateInstaller(
            HttpClient httpClient,
            IFileLauncherService? fileLauncherService,
            string? updatesDirectory,
            UnsignedReleasePolicy unsignedReleasePolicy,
            IAuthenticodeVerifier authenticodeVerifier)
        {
            this.httpClient = httpClient ??
                throw new ArgumentNullException(nameof(httpClient));
            this.fileLauncherService = fileLauncherService;
            this.authenticodeVerifier = authenticodeVerifier ??
                throw new ArgumentNullException(nameof(authenticodeVerifier));
            this.unsignedReleasePolicy = unsignedReleasePolicy;
            this.updatesDirectory = updatesDirectory ?? Path.Combine(
                Path.GetTempPath(),
                "CryptoBook",
                "Updates");
        }

        public async Task InstallAsync(
            ApplicationRelease release,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(release);
            Uri installerUri = release.InstallerUri
                ?? throw new InvalidOperationException(
                    "The release does not contain a Windows installer asset.");
            Uri checksumsUri = release.Sha256ChecksumsUri
                ?? throw new InvalidDataException(
                    "The release does not contain a SHA-256 manifest.");
            Uri signingStatusUri = release.SigningStatusUri
                ?? throw new InvalidDataException(
                    "The release does not contain a signing-status declaration.");
            string fileName = $"CryptoBook-Setup-{release.Version}.exe";
            ValidateReleaseAssetUri(installerUri, release.ReleaseUri, fileName);
            ValidateReleaseAssetUri(
                checksumsUri,
                release.ReleaseUri,
                "SHA256SUMS.txt");
            ValidateReleaseAssetUri(
                signingStatusUri,
                release.ReleaseUri,
                "SIGNING-STATUS.txt");

            string checksumManifest = await DownloadTextAssetAsync(
                checksumsUri,
                MaximumChecksumManifestBytes,
                cancellationToken);
            byte[] expectedHash = ParseExpectedSha256(checksumManifest, fileName);
            string signingStatus = await DownloadTextAssetAsync(
                signingStatusUri,
                MaximumSigningStatusBytes,
                cancellationToken);
            bool releaseDeclaresSigned = ParseSigningStatus(signingStatus);

            string directory = updatesDirectory;
            Directory.CreateDirectory(directory);
            string targetPath = Path.Combine(directory, fileName);
            string temporaryPath = targetPath + ".download";

            try
            {
                progress?.Report(
                    0.0,
                    LocalizationManager.GetString("Update.DownloadPreparing"));
                using HttpResponseMessage response = await httpClient.GetAsync(
                    installerUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                await DownloadAsync(
                    response,
                    temporaryPath,
                    progress,
                    cancellationToken);

                progress?.Report(
                    0.9,
                    LocalizationManager.GetString("Update.VerifyingInstaller"));
                await VerifyInstallerAsync(
                    temporaryPath,
                    expectedHash,
                    releaseDeclaresSigned,
                    cancellationToken);

                File.Move(temporaryPath, targetPath, overwrite: true);
                DeleteOldInstallers(
                    directory,
                    targetPath,
                    progress,
                    cancellationToken);
                progress?.Report(
                    1.0,
                    LocalizationManager.GetString("Update.StartingInstaller"));
                if(fileLauncherService is not null)
                {
                    LaunchResult result = fileLauncherService.RunAsAdmin(targetPath);
                    if(!result.Success)
                        throw new InvalidOperationException(
                            result.Error ?? "The installer process could not be started.");
                }
                else
                {
                    // The optional launcher keeps this service usable in small hosts
                    // and tests; production injects FileLauncherService below.
                    System.Diagnostics.Process? process =
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = targetPath,
                            UseShellExecute = true,
                            WorkingDirectory = directory
                        });
                    if(process is null)
                        throw new InvalidOperationException("The installer process could not be started.");
                }
            }
            catch
            {
                TryDelete(temporaryPath);
                throw;
            }
        }

        private async Task<string> DownloadTextAssetAsync(
            Uri uri,
            int maximumBytes,
            CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is long contentLength &&
               contentLength > maximumBytes)
                throw new InvalidDataException("The release metadata is too large.");

            await using Stream input = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            using var output = new MemoryStream();
            byte[] buffer = new byte[4096];
            int bytesRead;
            while((bytesRead = await input.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken)) > 0)
            {
                if(output.Length + bytesRead > maximumBytes)
                    throw new InvalidDataException("The release metadata is too large.");
                await output.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(output.ToArray());
            }
            catch(DecoderFallbackException exception)
            {
                throw new InvalidDataException(
                    "The release metadata is not valid UTF-8.",
                    exception);
            }
        }

        private async Task VerifyInstallerAsync(
            string installerPath,
            byte[] expectedHash,
            bool releaseDeclaresSigned,
            CancellationToken cancellationToken)
        {
            byte[] actualHash;
            await using(FileStream installer = new(
                installerPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                useAsync: true))
            {
                actualHash = await SHA256.HashDataAsync(
                    installer,
                    cancellationToken);
            }
            if(!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                throw new InvalidDataException(
                    "The downloaded installer does not match its published SHA-256 checksum.");

            cancellationToken.ThrowIfCancellationRequested();
            AuthenticodeStatus authenticodeStatus =
                authenticodeVerifier.Verify(installerPath);
            if(releaseDeclaresSigned)
            {
                if(authenticodeStatus != AuthenticodeStatus.Valid)
                    throw new InvalidDataException(
                        "The release declares a signed installer, but its Authenticode signature is not valid.");
                return;
            }

            if(authenticodeStatus == AuthenticodeStatus.Valid)
                throw new InvalidDataException(
                    "The installer signing state does not match the release declaration.");
            if(authenticodeStatus == AuthenticodeStatus.Invalid)
                throw new InvalidDataException(
                    "The installer contains an invalid Authenticode signature.");
            if(unsignedReleasePolicy != UnsignedReleasePolicy.AllowWithVerifiedChecksum)
                throw new InvalidDataException(
                    "Unsigned releases are disabled by the update security policy.");
        }

        private static byte[] ParseExpectedSha256(
            string manifest,
            string installerFileName)
        {
            string? matchingHash = null;
            foreach(string sourceLine in manifest.Split('\n'))
            {
                string line = sourceLine.Trim().TrimStart('\uFEFF');
                if(line.Length < 66 || !char.IsWhiteSpace(line[64]))
                    continue;

                string hash = line[..64];
                if(hash.Any(character => !Uri.IsHexDigit(character)))
                    continue;

                string assetPath = line[64..].Trim();
                if(assetPath.StartsWith('*'))
                    assetPath = assetPath[1..];
                string candidateName = assetPath
                    .Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault() ?? string.Empty;
                if(!string.Equals(
                    candidateName,
                    installerFileName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if(matchingHash is not null)
                    throw new InvalidDataException(
                        "The SHA-256 manifest contains duplicate installer entries.");
                matchingHash = hash;
            }

            if(matchingHash is null)
                throw new InvalidDataException(
                    "The SHA-256 manifest does not contain the installer.");
            return Convert.FromHexString(matchingHash);
        }

        private static bool ParseSigningStatus(string signingStatus)
        {
            string normalized = signingStatus.Trim().TrimStart('\uFEFF');
            if(string.Equals(
                normalized,
                SignedReleaseStatus,
                StringComparison.Ordinal))
            {
                return true;
            }
            if(string.Equals(
                normalized,
                UnsignedReleaseStatus,
                StringComparison.Ordinal))
            {
                return false;
            }

            throw new InvalidDataException(
                "The release signing-status declaration is missing or invalid.");
        }

        private static async Task DownloadAsync(
            HttpResponseMessage response,
            string temporaryPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            long? contentLength = response.Content.Headers.ContentLength;
            string status = LocalizationManager.GetString("Update.DownloadingInstaller");
            progress?.Report(contentLength > 0 ? 0.0 : null, status);

            await using Stream input = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            await using var output = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                useAsync: true);
            byte[] buffer = new byte[128 * 1024];
            long downloadedBytes = 0;
            int bytesRead;

            while((bytesRead = await input.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken)) > 0)
            {
                await output.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
                downloadedBytes += bytesRead;
                if(contentLength > 0)
                {
                    double downloadProgress = Math.Clamp(
                        (double)downloadedBytes / contentLength.Value,
                        0.0,
                        1.0);
                    progress?.Report(downloadProgress * 0.9, status);
                }
            }

            await output.FlushAsync(cancellationToken);
            progress?.Report(0.9, status);
        }

        private static void DeleteOldInstallers(
            string directory,
            string currentInstallerPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken)
        {
            string[] obsoleteFiles = Directory
                .EnumerateFiles(
                    directory,
                    "CryptoBook-Setup-*.exe",
                    SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(
                    directory,
                    "CryptoBook-Setup-*.download",
                    SearchOption.TopDirectoryOnly))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path =>
                    !string.Equals(
                        Path.GetFullPath(path),
                        Path.GetFullPath(currentInstallerPath),
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            string status = LocalizationManager.GetString(
                "Update.RemovingOldInstallers");
            progress?.Report(0.9, status);

            if(obsoleteFiles.Length == 0)
            {
                progress?.Report(1.0, status);
                return;
            }

            for(int index = 0; index < obsoleteFiles.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDelete(obsoleteFiles[index]);
                progress?.Report(
                    0.9 + (0.1 * (index + 1) / obsoleteFiles.Length),
                    status);
            }
        }

        private static void ValidateReleaseAssetUri(
            Uri assetUri,
            Uri releaseUri,
            string expectedFileName)
        {
            if(!IsSecureGitHubUri(assetUri) || !IsSecureGitHubUri(releaseUri))
            {
                throw new InvalidDataException("The release asset URL is invalid.");
            }

            string[] releaseParts = GetPathParts(releaseUri);
            string[] assetParts = GetPathParts(assetUri);
            bool matchesRelease =
                releaseParts.Length == 5 &&
                assetParts.Length == 6 &&
                string.Equals(releaseParts[0], assetParts[0], StringComparison.OrdinalIgnoreCase) &&
                string.Equals(releaseParts[1], assetParts[1], StringComparison.OrdinalIgnoreCase) &&
                string.Equals(releaseParts[2], "releases", StringComparison.Ordinal) &&
                string.Equals(releaseParts[3], "tag", StringComparison.Ordinal) &&
                string.Equals(assetParts[2], "releases", StringComparison.Ordinal) &&
                string.Equals(assetParts[3], "download", StringComparison.Ordinal) &&
                string.Equals(releaseParts[4], assetParts[4], StringComparison.Ordinal) &&
                string.Equals(assetParts[5], expectedFileName, StringComparison.OrdinalIgnoreCase);
            if(!matchesRelease)
                throw new InvalidDataException(
                    "The release asset does not belong to the expected GitHub release.");
        }

        private static bool IsSecureGitHubUri(Uri uri) =>
            uri.IsAbsoluteUri &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment);

        private static string[] GetPathParts(Uri uri) => uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        private static void TryDelete(string path)
        {
            try
            {
                if(File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // A failed cleanup must not hide the download/start error.
            }
        }
    }
}

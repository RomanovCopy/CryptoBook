using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Net.Http;

namespace CryptoBook.Services
{
    public sealed class ApplicationUpdateInstaller: IApplicationUpdateInstaller
    {
        private readonly HttpClient httpClient;
        private readonly IFileLauncherService? fileLauncherService;
        private readonly string updatesDirectory;

        public ApplicationUpdateInstaller(
            HttpClient httpClient,
            IFileLauncherService? fileLauncherService = null)
            : this(httpClient, fileLauncherService, null)
        {
        }

        internal ApplicationUpdateInstaller(
            HttpClient httpClient,
            IFileLauncherService? fileLauncherService,
            string? updatesDirectory)
        {
            this.httpClient = httpClient ??
                throw new ArgumentNullException(nameof(httpClient));
            this.fileLauncherService = fileLauncherService;
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
            ValidateInstallerUri(installerUri);

            string directory = updatesDirectory;
            Directory.CreateDirectory(directory);
            string fileName = $"CryptoBook-Setup-{release.Version}.exe";
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

        private static void ValidateInstallerUri(Uri uri)
        {
            if(uri.Scheme != Uri.UriSchemeHttps ||
               !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The installer download URL is invalid.");
            }
        }

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

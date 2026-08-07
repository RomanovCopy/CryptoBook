using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Net.Http;

namespace CryptoBook.Services
{
    public sealed class ApplicationUpdateInstaller: IApplicationUpdateInstaller
    {
        private readonly HttpClient httpClient;
        private readonly IFileLauncherService? fileLauncherService;

        public ApplicationUpdateInstaller(
            HttpClient httpClient,
            IFileLauncherService? fileLauncherService = null)
        {
            this.httpClient = httpClient ??
                throw new ArgumentNullException(nameof(httpClient));
            this.fileLauncherService = fileLauncherService;
        }

        public async Task InstallAsync(
            ApplicationRelease release,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(release);
            Uri installerUri = release.InstallerUri
                ?? throw new InvalidOperationException(
                    "The release does not contain a Windows installer asset.");
            ValidateInstallerUri(installerUri);

            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook",
                "Updates");
            Directory.CreateDirectory(directory);
            string fileName = $"CryptoBook-Setup-{release.Version}.exe";
            string targetPath = Path.Combine(directory, fileName);
            string temporaryPath = targetPath + ".download";

            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(
                    installerUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                await using Stream input = await response.Content.ReadAsStreamAsync(
                    cancellationToken);
                await using var output = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    128 * 1024,
                    useAsync: true);
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Close();

                File.Move(temporaryPath, targetPath, overwrite: true);
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

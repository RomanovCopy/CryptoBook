using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Net;
using System.Net.Http;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ApplicationUpdateInstallerTests
{
    [Fact]
    public async Task InstallAsync_ReportsDownloadAndRemovesOldInstallers()
    {
        using var directory = new TemporaryDirectory();
        string oldInstaller = directory.WriteFile(
            "CryptoBook-Setup-1.1.0.exe",
            [1, 2, 3]);
        string incompleteDownload = directory.WriteFile(
            "CryptoBook-Setup-1.1.1.exe.download",
            [4, 5, 6]);
        string unrelatedFile = directory.WriteFile("notes.txt", [7]);
        byte[] installerBytes = new byte[300 * 1024];
        Random.Shared.NextBytes(installerBytes);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(installerBytes)
        };
        using var client = new HttpClient(new StubHttpMessageHandler(response));
        var launcher = new FileLauncherStub();
        var progress = new RecordingProgressReporter();
        var installer = new ApplicationUpdateInstaller(
            client,
            launcher,
            directory.Path);
        ApplicationRelease release = CreateRelease("1.2.0");

        await installer.InstallAsync(release, progress);

        string expectedInstaller = System.IO.Path.Combine(
            directory.Path,
            "CryptoBook-Setup-1.2.0.exe");
        Assert.Equal(expectedInstaller, launcher.LaunchedPath);
        Assert.Equal(installerBytes, File.ReadAllBytes(expectedInstaller));
        Assert.False(File.Exists(oldInstaller));
        Assert.False(File.Exists(incompleteDownload));
        Assert.True(File.Exists(unrelatedFile));
        Assert.Equal(0.0, progress.Values.First());
        Assert.Equal(1.0, progress.Values.Last());
        Assert.Contains(progress.Values, value => value is > 0.0 and < 0.9);
        Assert.Contains(progress.Values, value => value is > 0.9 and < 1.0);
        Assert.True(progress.Messages.Distinct().Count() >= 4);
    }

    [Fact]
    public async Task InstallAsync_WithoutContentLength_ReportsIndeterminateDownload()
    {
        using var directory = new TemporaryDirectory();
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent([1, 2, 3, 4])
        };
        using var client = new HttpClient(new StubHttpMessageHandler(response));
        var progress = new RecordingProgressReporter();
        var installer = new ApplicationUpdateInstaller(
            client,
            new FileLauncherStub(),
            directory.Path);

        await installer.InstallAsync(CreateRelease("1.2.0"), progress);

        Assert.Contains(null, progress.Values);
        Assert.Equal(1.0, progress.Values.Last());
    }

    private static ApplicationRelease CreateRelease(string version)
    {
        Assert.True(SemanticVersion.TryParse(
            version,
            out SemanticVersion? semanticVersion));
        return new ApplicationRelease(
            semanticVersion!,
            $"CryptoBook {version}",
            string.Empty,
            new Uri($"https://github.com/RomanovCopy/CryptoBook/releases/tag/v{version}"),
            DateTimeOffset.UtcNow)
        {
            InstallerUri = new Uri(
                $"https://github.com/RomanovCopy/CryptoBook/releases/download/v{version}/CryptoBook-Setup-{version}.exe")
        };
    }

    private sealed class RecordingProgressReporter: IProgressReporter
    {
        public List<double?> Values { get; } = [];
        public List<string> Messages { get; } = [];

        public void Report(double? value, string? currentInfo = null)
        {
            Values.Add(value);
            if(currentInfo is not null)
                Messages.Add(currentInfo);
        }
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response):
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class UnknownLengthContent(byte[] content): HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) => stream.WriteAsync(content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class TemporaryDirectory: IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public string WriteFile(string fileName, byte[] content)
        {
            string filePath = System.IO.Path.Combine(Path, fileName);
            File.WriteAllBytes(filePath, content);
            return filePath;
        }

        public void Dispose()
        {
            if(Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class FileLauncherStub: IFileLauncherService
    {
        public string? LaunchedPath { get; private set; }

        public LaunchResult RunAsAdmin(
            string path,
            string? arguments = null)
        {
            LaunchedPath = path;
            return LaunchResult.Ok("runas", path);
        }

        public LaunchResult Open(string target) =>
            throw new NotSupportedException();
        public LaunchResult Open(string target, string verb) =>
            throw new NotSupportedException();
        public LaunchResult ShellExecute(ShellLaunchOptions options) =>
            throw new NotSupportedException();
        public LaunchResult OpenWith(
            string applicationPath,
            string target,
            string? arguments = null,
            string? workingDirectory = null) =>
            throw new NotSupportedException();
        public LaunchResult StartProcess(ProcessLaunchOptions options) =>
            throw new NotSupportedException();
        public LaunchResult RevealInExplorer(string path, bool select = true) =>
            throw new NotSupportedException();
        public LaunchResult Print(string path) =>
            throw new NotSupportedException();
        public LaunchResult Edit(string path) =>
            throw new NotSupportedException();
        public LaunchResult RunCmd(
            string command,
            string? workingDirectory = null,
            bool runAsAdmin = false) =>
            throw new NotSupportedException();
        public LaunchResult RunPowerShell(
            string command,
            string? workingDirectory = null,
            bool runAsAdmin = false) =>
            throw new NotSupportedException();
    }
}

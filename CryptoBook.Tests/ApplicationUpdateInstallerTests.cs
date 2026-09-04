using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

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
        using var client = CreateClient(installerBytes);
        var launcher = new FileLauncherStub();
        var progress = new RecordingProgressReporter();
        var installer = new ApplicationUpdateInstaller(
            client,
            launcher,
            directory.Path,
            UnsignedReleasePolicy.AllowWithVerifiedChecksum,
            new AuthenticodeVerifierStub(AuthenticodeStatus.NotSigned));
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
        byte[] installerBytes = [1, 2, 3, 4];
        using var client = CreateClient(
            installerBytes,
            unknownInstallerLength: true);
        var progress = new RecordingProgressReporter();
        var installer = new ApplicationUpdateInstaller(
            client,
            new FileLauncherStub(),
            directory.Path,
            UnsignedReleasePolicy.AllowWithVerifiedChecksum,
            new AuthenticodeVerifierStub(AuthenticodeStatus.NotSigned));

        await installer.InstallAsync(CreateRelease("1.2.0"), progress);

        Assert.Contains(null, progress.Values);
        Assert.Equal(1.0, progress.Values.Last());
    }

    [Fact]
    public async Task InstallAsync_RejectsInstallerWhenSha256DoesNotMatch()
    {
        using var directory = new TemporaryDirectory();
        byte[] installerBytes = [1, 2, 3, 4];
        using var client = CreateClient(
            installerBytes,
            checksum: new string('0', 64));
        var launcher = new FileLauncherStub();
        var verifier = new AuthenticodeVerifierStub(AuthenticodeStatus.NotSigned);
        var installer = new ApplicationUpdateInstaller(
            client,
            launcher,
            directory.Path,
            UnsignedReleasePolicy.AllowWithVerifiedChecksum,
            verifier);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => installer.InstallAsync(CreateRelease("1.2.0")));

        Assert.Contains("SHA-256", exception.Message);
        Assert.Null(launcher.LaunchedPath);
        Assert.Equal(0, verifier.VerifyCount);
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task InstallAsync_RejectsSignedReleaseWithInvalidAuthenticodeSignature()
    {
        using var directory = new TemporaryDirectory();
        byte[] installerBytes = [1, 2, 3, 4];
        using var client = CreateClient(installerBytes, signed: true);
        var launcher = new FileLauncherStub();
        var installer = new ApplicationUpdateInstaller(
            client,
            launcher,
            directory.Path,
            UnsignedReleasePolicy.AllowWithVerifiedChecksum,
            new AuthenticodeVerifierStub(AuthenticodeStatus.Invalid));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => installer.InstallAsync(CreateRelease("1.2.0")));

        Assert.Contains("Authenticode", exception.Message);
        Assert.Null(launcher.LaunchedPath);
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task InstallAsync_AllowsSignedReleaseWithValidAuthenticodeSignature()
    {
        using var directory = new TemporaryDirectory();
        byte[] installerBytes = [1, 2, 3, 4];
        using var client = CreateClient(installerBytes, signed: true);
        var launcher = new FileLauncherStub();
        var installer = new ApplicationUpdateInstaller(
            client,
            launcher,
            directory.Path,
            UnsignedReleasePolicy.RequireAuthenticodeSignature,
            new AuthenticodeVerifierStub(AuthenticodeStatus.Valid));

        await installer.InstallAsync(CreateRelease("1.2.0"));

        Assert.NotNull(launcher.LaunchedPath);
    }

    [Fact]
    public async Task InstallAsync_RejectsUnsignedReleaseWhenPolicyRequiresSignature()
    {
        using var directory = new TemporaryDirectory();
        byte[] installerBytes = [1, 2, 3, 4];
        using var client = CreateClient(installerBytes);
        var launcher = new FileLauncherStub();
        var installer = new ApplicationUpdateInstaller(
            client,
            launcher,
            directory.Path,
            UnsignedReleasePolicy.RequireAuthenticodeSignature,
            new AuthenticodeVerifierStub(AuthenticodeStatus.NotSigned));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => installer.InstallAsync(CreateRelease("1.2.0")));

        Assert.Contains("disabled", exception.Message);
        Assert.Null(launcher.LaunchedPath);
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task InstallAsync_RejectsVerificationAssetFromAnotherRelease()
    {
        using var directory = new TemporaryDirectory();
        using var client = CreateClient([1, 2, 3, 4]);
        var launcher = new FileLauncherStub();
        var installer = new ApplicationUpdateInstaller(
            client,
            launcher,
            directory.Path,
            UnsignedReleasePolicy.AllowWithVerifiedChecksum,
            new AuthenticodeVerifierStub(AuthenticodeStatus.NotSigned));
        ApplicationRelease release = CreateRelease("1.2.0") with
        {
            Sha256ChecksumsUri = new Uri(
                "https://github.com/RomanovCopy/CryptoBook/releases/download/v1.1.0/SHA256SUMS.txt")
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => installer.InstallAsync(release));

        Assert.Null(launcher.LaunchedPath);
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
            InstallerUri = AssetUri(version, $"CryptoBook-Setup-{version}.exe"),
            Sha256ChecksumsUri = AssetUri(version, "SHA256SUMS.txt"),
            SigningStatusUri = AssetUri(version, "SIGNING-STATUS.txt")
        };
    }

    private static Uri AssetUri(string version, string fileName) => new(
        $"https://github.com/RomanovCopy/CryptoBook/releases/download/v{version}/{fileName}");

    private static HttpClient CreateClient(
        byte[] installerBytes,
        string? checksum = null,
        bool signed = false,
        bool unknownInstallerLength = false)
    {
        checksum ??= Convert.ToHexString(SHA256.HashData(installerBytes));
        string manifest =
            $"{checksum}  artifacts\\CryptoBook-Setup-1.2.0.exe\r\n";
        string signingStatus = signed
            ? "CryptoBook binaries in this release are signed with Authenticode."
            : "CryptoBook binaries in this release are not digitally signed.";
        return new HttpClient(new StubHttpMessageHandler(request =>
        {
            string fileName = request.RequestUri is null
                ? string.Empty
                : System.IO.Path.GetFileName(request.RequestUri.AbsolutePath);
            HttpContent content = fileName switch
            {
                "SHA256SUMS.txt" => new StringContent(
                    manifest,
                    Encoding.UTF8,
                    "text/plain"),
                "SIGNING-STATUS.txt" => new StringContent(
                    signingStatus,
                    Encoding.UTF8,
                    "text/plain"),
                _ when unknownInstallerLength => new UnknownLengthContent(
                    installerBytes),
                _ => new ByteArrayContent(installerBytes)
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };
        }));
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

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory):
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class AuthenticodeVerifierStub(AuthenticodeStatus status):
        IAuthenticodeVerifier
    {
        public int VerifyCount { get; private set; }

        public AuthenticodeStatus Verify(string filePath)
        {
            VerifyCount++;
            return status;
        }
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
        public LaunchResult ShowOpenWithDialog(string target) =>
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

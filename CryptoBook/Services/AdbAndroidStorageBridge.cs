using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services;

/// <summary>
/// Optional ADB transport. It is useful for development and power users; the
/// provider contract also permits a WPD/MTP transport for ordinary users.
/// </summary>
public sealed class AdbAndroidStorageBridge: IAndroidStorageBridge
{
    private readonly string? _adbExecutable;

    public AdbAndroidStorageBridge()
    {
        _adbExecutable = ResolveAdbExecutable();
    }

    public async Task<IReadOnlyList<AndroidDeviceInfo>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        if(_adbExecutable is null)
            return Array.Empty<AndroidDeviceInfo>();

        ProcessResult result;
        try
        {
            result = await RunAsync(["devices", "-l"], cancellationToken);
        }
        catch(System.ComponentModel.Win32Exception)
        {
            return Array.Empty<AndroidDeviceInfo>();
        }

        if(result.ExitCode != 0)
            return Array.Empty<AndroidDeviceInfo>();

        var devices = new List<AndroidDeviceInfo>();
        foreach(string rawLine in result.StandardOutput.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if(rawLine.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
               rawLine.StartsWith('*'))
                continue;

            string[] fields = rawLine.Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if(fields.Length < 2)
                continue;

            string serial = fields[0];
            AndroidDeviceState state = fields[1] switch
            {
                "device" => AndroidDeviceState.Online,
                "offline" => AndroidDeviceState.Offline,
                "unauthorized" => AndroidDeviceState.Unauthorized,
                _ => AndroidDeviceState.Unknown
            };
            var properties = fields.Skip(2)
                .Select(field => field.Split(':', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
            properties.TryGetValue("model", out string? model);
            properties.TryGetValue("product", out string? product);
            properties.TryGetValue("transport_id", out string? transportId);
            string displayName = string.IsNullOrWhiteSpace(model)
                ? serial
                : model.Replace('_', ' ');
            devices.Add(new AndroidDeviceInfo(
                serial,
                displayName,
                state,
                product,
                model,
                transportId));
        }
        return devices;
    }

    public async Task<AndroidRemoteEntry?> GetMetadataAsync(
        string serial,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        string script = "stat -c '%F|%s|%Y' -- " + Quote(objectId);
        ProcessResult result = await RunShellAsync(serial, script, cancellationToken);
        if(result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            return null;

        string[] fields = result.StandardOutput.Trim().Split('|');
        if(fields.Length < 3)
            return null;
        bool isContainer = fields[0].Contains("directory", StringComparison.OrdinalIgnoreCase);
        _ = long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long size);
        DateTime? modified = long.TryParse(
            fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : null;
        string name = objectId.TrimEnd('/').Split('/').LastOrDefault() ?? objectId;
        return new AndroidRemoteEntry(
            objectId,
            name,
            isContainer,
            isContainer ? 0 : size,
            modified,
            name.StartsWith('.'));
    }

    public async Task<IReadOnlyList<AndroidRemoteEntry>> GetChildrenAsync(
        string serial,
        string containerObjectId,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunShellAsync(
            serial,
            "find " + Quote(containerObjectId) + " -mindepth 1 -maxdepth 1 -print0",
            cancellationToken);
        EnsureSuccess(result, "enumerate Android storage");

        var entries = new List<AndroidRemoteEntry>();
        foreach(string path in result.StandardOutput.Split(
            new[] { '\0' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AndroidRemoteEntry? entry = await GetMetadataAsync(
                serial,
                path.Trim(),
                cancellationToken);
            if(entry is not null)
                entries.Add(entry);
        }
        return entries;
    }

    public async Task PullAsync(
        string serial,
        string sourceObjectId,
        string localDestination,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunAsync(
            ["-s", serial, "pull", sourceObjectId, localDestination],
            cancellationToken);
        EnsureSuccess(result, "pull from Android");
    }

    public async Task PushAsync(
        string serial,
        string localSource,
        string destinationObjectId,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunAsync(
            ["-s", serial, "push", localSource, destinationObjectId],
            cancellationToken);
        EnsureSuccess(result, "push to Android");
    }

    public async Task DeleteAsync(
        string serial,
        string objectId,
        CancellationToken cancellationToken = default) => EnsureSuccess(
        await RunShellAsync(
            serial,
            "rm -rf -- " + Quote(objectId),
            cancellationToken),
        "delete Android object");

    public async Task CopyAsync(
        string serial,
        string sourceObjectId,
        string destinationObjectId,
        CancellationToken cancellationToken = default) => EnsureSuccess(
        await RunShellAsync(
            serial,
            "cp -R -- " + Quote(sourceObjectId) + " " + Quote(destinationObjectId),
            cancellationToken),
        "copy Android object");

    public async Task MoveAsync(
        string serial,
        string sourceObjectId,
        string destinationObjectId,
        CancellationToken cancellationToken = default) => EnsureSuccess(
        await RunShellAsync(
            serial,
            "mv -- " + Quote(sourceObjectId) + " " + Quote(destinationObjectId),
            cancellationToken),
        "move Android object");

    public async Task CreateContainerAsync(
        string serial,
        string objectId,
        CancellationToken cancellationToken = default) => EnsureSuccess(
        await RunShellAsync(
            serial,
            "mkdir -p -- " + Quote(objectId),
            cancellationToken),
        "create Android directory");

    private Task<ProcessResult> RunShellAsync(
        string serial,
        string script,
        CancellationToken cancellationToken) =>
        RunAsync(["-s", serial, "shell", "sh", "-c", script], cancellationToken);

    private async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _adbExecutable ?? throw new NotSupportedException(
                "ADB was not found. Configure CRYPTOBOOK_ADB_PATH or bundle platform-tools."),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach(string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static void EnsureSuccess(ProcessResult result, string operation)
    {
        if(result.ExitCode == 0)
            return;
        string error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        throw new IOException($"Unable to {operation}: {error}");
    }

    private static string Quote(string value) =>
        "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string? ResolveAdbExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("CRYPTOBOOK_ADB_PATH");
        if(!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        string bundled = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
        if(File.Exists(bundled))
            return bundled;

        string? environmentPath = Environment.GetEnvironmentVariable("PATH");
        foreach(string directory in (environmentPath ?? string.Empty).Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                string candidate = Path.Combine(directory, "adb.exe");
                if(File.Exists(candidate))
                    return candidate;
            }
            catch(ArgumentException)
            {
            }
        }
        return null;
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
